using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using CoreLayer.Citrix.Adc.NitroClient;
using CoreLayer.Citrix.Adc.NitroClient.Api.Configuration.Login;
using CoreLayer.Citrix.Adc.NitroClient.Api.Configuration.System.SystemBackup;
using CoreLayer.Citrix.Adc.NitroClient.Commands.Configuration.System.SystemBackup;
using CoreLayer.Citrix.Adc.NitroClient.Interfaces;
using CoreLayer.Citrix.Adc.NodeBackupWorker.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Prometheus;

namespace CoreLayer.Citrix.Adc.NodeBackupWorker
{
    public class NodeBackupHostedService : BackgroundService
    {
        private readonly ILogger<NodeBackupHostedService> _logger;
        private readonly NodeBackupConfiguration _nodeBackupConfiguration;
        private readonly INitroServiceClient _nitroServiceClient;

        private readonly System.Timers.Timer _timer;

        private Counter _successfulBackupCounter;
        private Counter _failedBackupCounter;
        private Gauge _backupProcessDuration;
        private Gauge _backupSize;
        

        public NodeBackupHostedService(IHostApplicationLifetime hostApplicationLifetime, ILogger<NodeBackupHostedService> logger,
            IOptions<NodeBackupConfiguration> configuration)
        {
            this._logger = logger;
            _nodeBackupConfiguration = configuration.Value;
            
            
            _nitroServiceClient = new NitroServiceClient(
                new NitroLoginRequestData(
                    _nodeBackupConfiguration.Node.Username, 
                    _nodeBackupConfiguration.Node.Password), 
                new NitroServiceConnectionSettings(
                    _nodeBackupConfiguration.Node.NodeAddress,
                    5,
                    NitroServiceConnectionAuthenticationMethod.AutomaticHeaderInjection),
                _nodeBackupConfiguration.Node.CertificateValidation);

            _timer = new System.Timers.Timer(2000);
            _timer.Elapsed += TriggerNodeBackup;
            
            if (!ConfigurationIsValid(new CancellationToken()).Result)
                hostApplicationLifetime.StopApplication();
            
            if (_nodeBackupConfiguration.Prometheus.Enabled)
                ConfigureMetrics();
        }

        private void ConfigureMetrics()
        {
            _successfulBackupCounter = Metrics.CreateCounter(
                _nodeBackupConfiguration.Prometheus.NamePrefix + "_adc_nodebackup_success",
                "Successful backups",
                new CounterConfiguration
                {
                    LabelNames = new[]
                    {
                        "owner",
                        "environment",
                        "node"
                    }
                });
            
            _failedBackupCounter = Metrics.CreateCounter(
                _nodeBackupConfiguration.Prometheus.NamePrefix + "_adc_nodebackup_failure",
                "Failed backups",
                new CounterConfiguration
                {
                    LabelNames = new[]
                    {
                        "owner",
                        "environment",
                        "node"
                    },
                });
            
            _backupProcessDuration = Metrics.CreateGauge(
                _nodeBackupConfiguration.Prometheus.NamePrefix + "_adc_nodebackup_processingtime",
                "Failed backups",
                new GaugeConfiguration
                {
                    LabelNames = new[]
                    {
                        "owner",
                        "environment",
                        "node"
                    },
                });
            
            _backupSize = Metrics.CreateGauge(
                _nodeBackupConfiguration.Prometheus.NamePrefix + "_adc_nodebackup_size",
                "Failed backups",
                new GaugeConfiguration
                {
                    LabelNames = new[]
                    {
                        "owner",
                        "environment",
                        "node"
                    },
                });
        }
        
        private void TriggerNodeBackup(Object source, ElapsedEventArgs e)
        {
            _logger.LogCritical("Backup was triggered at {0:HH:mm:ss}",
                e.SignalTime);
            _successfulBackupCounter.WithLabels(new []
            {
                _nodeBackupConfiguration.Node.OwnerName,
                _nodeBackupConfiguration.Node.EnvironmentName,
                _nodeBackupConfiguration.Node.NodeName
            }).Inc();
        }

        private async Task<bool> ConfigurationIsValid(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Validating configuration");
            
            if (!await CanListBackupOnNode()) return false;
            // if (!IsValidStartHour()) return false;
            // if (!IsValidInterval()) return false;
            if (!CanWriteToOutputPath()) return false;
            
            _logger.LogInformation("Configuration is valid, starting service");
            
            return true;
        }
        
        private async Task<bool> CanListBackupOnNode()
        {
            try
            {
                var result = await ListBackupOnNode();
                if (!result.ErrorCode.Equals(0))
                {
                    _logger.LogCritical(result.Message);
                    return false;
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogCritical(ex.Message);
                if (ex.InnerException != null)
                    _logger.LogCritical(ex.InnerException.Message);
                return false;
            }

            return true;
        }
        
        private async Task<SystemBackupGetResponse> ListBackupOnNode()
        {
            var systemBackupGetCommand = NitroCommandFactory.Create<SystemBackupGetCommand>(
                _nitroServiceClient);

            var result = await systemBackupGetCommand.GetResponse();
            return result;
        }
        
        private bool CanWriteToOutputPath()
        {
            try
            {
                Directory.CreateDirectory(_nodeBackupConfiguration.Backup.BasePath);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogCritical(ex.Message);
                return false;
            }
            catch (ArgumentNullException ex)
            {
                _logger.LogCritical(ex.Message);
                return false;
            }
            catch (ArgumentException ex)
            {
                _logger.LogCritical(ex.Message);
                return false;
            }
            catch (PathTooLongException ex)
            {
                _logger.LogCritical(ex.Message);
                return false;
            }
            catch (DirectoryNotFoundException ex)
            {
                _logger.LogCritical(ex.Message);
                return false;
            }
            catch (NotSupportedException ex)
            {
                _logger.LogCritical(ex.Message);
                return false;
            }
            catch (IOException ex)
            {
                _logger.LogCritical(ex.Message);
                return false;
            }

            return true;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            await Task.Delay(1000, cancellationToken);
            
            while (!cancellationToken.IsCancellationRequested)
            {
                if (!_timer.Enabled && (DateTime.Now.Ticks/10000000).Equals(_nodeBackupConfiguration.Backup.Start.Ticks/10000000))
                {
                    _logger.LogCritical("Starting timer");
                    _timer.Enabled = true;
                }
                
                if (!_timer.Enabled)
                {
                    _logger.LogInformation("Counting down to first backup: {0}",
                        (_nodeBackupConfiguration.Backup.Start - DateTime.Now).ToString(@"hh\:mm\:ss"));
                }

                await Task.Delay(1000, cancellationToken);
            }
            _timer.Stop();
            _logger.LogInformation("Stopping Execution");
        }
    }
}