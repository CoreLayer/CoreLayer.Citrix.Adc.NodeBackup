using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using CoreLayer.Citrix.Adc.NitroClient;
using CoreLayer.Citrix.Adc.NitroClient.Api.Configuration.Login;
using CoreLayer.Citrix.Adc.NitroClient.Interfaces;
using CoreLayer.Citrix.Adc.NitroData.Api.Configuration.System;
using CoreLayer.Citrix.Adc.NitroOperations;
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
        private readonly MetricServer _metricServer;

        private readonly string _outputPath;

        private readonly string[] metricLabelNames;
        private readonly string[] metricLabels;
        
        private Counter _successfulBackupCounter;
        private Counter _failedBackupCounter;
        private Gauge _backupProcessDuration;
        private Gauge _backupSize;


        public NodeBackupHostedService(IHostApplicationLifetime hostApplicationLifetime,
            ILogger<NodeBackupHostedService> logger,
            IOptions<NodeBackupConfiguration> configuration)
        {
            this._logger = logger;
            _nodeBackupConfiguration = configuration.Value;

            // Configure INitroServiceClient
            _logger.LogDebug("Setting up NitroServiceClient");
            _nitroServiceClient = new NitroServiceClient(
                new NitroLoginRequestData(
                    _nodeBackupConfiguration.Node.Username,
                    _nodeBackupConfiguration.Node.Password),
                new NitroServiceConnectionSettings(
                    _nodeBackupConfiguration.Node.NodeAddress,
                    5,
                    NitroServiceConnectionAuthenticationMethod.AutomaticHeaderInjection),
                _nodeBackupConfiguration.Node.CertificateValidation);

            // Configure timer
            _logger.LogDebug("Configuring NodeBackup Timer");
            _timer = new System.Timers.Timer(_nodeBackupConfiguration.Backup.Interval * 1000);
            _timer.Elapsed += OnBackupTimerElapsed;

            // Configure output
            _logger.LogDebug("Configuring Output path");
            _outputPath = _nodeBackupConfiguration.Backup.BasePath;
            if (_nodeBackupConfiguration.Backup.CreateSubdirectoryForNode)
                _outputPath = Path.Combine(_nodeBackupConfiguration.Backup.BasePath,
                    _nodeBackupConfiguration.Node.NodeName);

            // Configure metrics
            _logger.LogDebug("Configuring Prometheus Metric Labels");
            metricLabelNames = new[]
            {
                "owner",
                "environment",
                "node"
            };
            metricLabels = new[]
            {
                _nodeBackupConfiguration.Node.OwnerName,
                _nodeBackupConfiguration.Node.EnvironmentName,
                _nodeBackupConfiguration.Node.NodeName
            };
            ConfigureMetrics();

            // Validate configuration
            if (!ConfigurationIsValid(new CancellationToken()).Result)
                hostApplicationLifetime.StopApplication();

            // Start embedded Prometheus MetricsServer
            if (!_nodeBackupConfiguration.Prometheus.MetricsServer.Enabled) return;
            _logger.LogInformation(
                "Starting metrics server on port {0}",
                _nodeBackupConfiguration.Prometheus.MetricsServer.Port);
            _metricServer = new MetricServer(
                port: _nodeBackupConfiguration.Prometheus.MetricsServer.Port, 
                useHttps: _nodeBackupConfiguration.Prometheus.MetricsServer.UseHttps);
            _metricServer.Start();
        }

        private void ConfigureMetrics()
        {
            _logger.LogDebug("Configuring Prometheus Metrics");

            _successfulBackupCounter = Metrics.CreateCounter(
                _nodeBackupConfiguration.Prometheus.NamePrefix + "_adc_nodebackup_success",
                "Successful backups",
                new CounterConfiguration
                {
                    LabelNames = metricLabelNames
                });

            _failedBackupCounter = Metrics.CreateCounter(
                _nodeBackupConfiguration.Prometheus.NamePrefix + "_adc_nodebackup_failure",
                "Failed backups",
                new CounterConfiguration
                {
                    LabelNames = metricLabelNames
                });

            _backupProcessDuration = Metrics.CreateGauge(
                _nodeBackupConfiguration.Prometheus.NamePrefix + "_adc_nodebackup_processingtime",
                "Failed backups",
                new GaugeConfiguration
                {
                    LabelNames = metricLabelNames
                });

            _backupSize = Metrics.CreateGauge(
                _nodeBackupConfiguration.Prometheus.NamePrefix + "_adc_nodebackup_size",
                "Failed backups",
                new GaugeConfiguration
                {
                    LabelNames = metricLabelNames
                });
        }

        private async Task<bool> ConfigurationIsValid(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Validating configuration");

            if (_nodeBackupConfiguration.Backup.Interval % 300 != 0)
            {
                _logger.LogCritical("Backup interval {0} is invalid, must be factor of 300 (5 minutes)", _nodeBackupConfiguration.Backup.Interval);
                return false;
            }
            
            if (!CanWriteToOutputPath()) return false;
            if (!await CanListBackupOnNode()) return false;
            
            _logger.LogInformation("Executing startup backup to test permissions and connectivity");
            if (!await ExecuteBackupProcedure(DateTime.Now)) return false;
            
            _logger.LogInformation("Configuration is valid, starting service");
            
            return true;
        }
        
        private async Task<bool> CanListBackupOnNode()
        {
            try
            {
                var listBackupResponse = await NitroBackup.GetAllAsync(_nitroServiceClient);
                if (listBackupResponse.ErrorCode.Equals(0)) return true;
                _logger.LogCritical(listBackupResponse.Message);
                return false;

            }
            catch (HttpRequestException ex)
            {
                _logger.LogCritical(ex.Message);
                if (ex.InnerException != null)
                    _logger.LogCritical(ex.InnerException.Message);
                return false;
            }
        }

        private bool CanWriteToOutputPath()
        {
            try
            {
                Directory.CreateDirectory(_outputPath);
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
            _logger.LogDebug("Waiting 5 seconds before starting countdown");
            await Task.Delay(5000, cancellationToken);
            
            await ExecutionLoopAsync(cancellationToken);
            
            _logger.LogInformation("Stopping backup timer");
            _timer.Stop();
        }

        private async Task ExecutionLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var nextIntervalSeconds = CalculateNextIntervalSeconds();

                if (!_timer.Enabled)
                {
                    if (nextIntervalSeconds == _nodeBackupConfiguration.Backup.Interval)
                    {
                        _logger.LogInformation("Starting backup timer");
                        _timer.Enabled = true;
                        await ExecuteBackupProcedure(DateTime.Now);
                    }
                    else
                    {
                        _logger.LogInformation("Counting down to backup timer start: {0} at {1} - {2} seconds",
                            TimeSpan.FromSeconds(nextIntervalSeconds).ToString(@"hh\:mm\:ss"),
                            TimeSpan.FromSeconds(DateTime.Now.Ticks / TimeSpan.TicksPerSecond + nextIntervalSeconds).ToString(@"hh\:mm\:ss"),
                            nextIntervalSeconds.ToString());
                    }
                }
                else
                {
                    _logger.LogInformation("Counting down to next backup: {0} at {1} - {2} seconds",
                        TimeSpan.FromSeconds(nextIntervalSeconds).ToString(@"hh\:mm\:ss"),
                        TimeSpan.FromSeconds(DateTime.Now.Ticks / TimeSpan.TicksPerSecond + nextIntervalSeconds).ToString(@"hh\:mm\:ss"),
                        nextIntervalSeconds.ToString());
                }
                
                await Task.Delay(1000, cancellationToken);
            }
        }

        private long CalculateNextIntervalSeconds()
        {
            return
                DateTime.Now.Ticks / TimeSpan.TicksPerSecond < _nodeBackupConfiguration.Backup.Start.Ticks / TimeSpan.TicksPerSecond 
                    ? CalculateIntervalSecondsFromNowToBackupStart() : CalculateIntervalSecondsFromBackupStartToNow();
        }

        private long CalculateIntervalSecondsFromBackupStartToNow()
        {
            _logger.LogDebug("Current time is later than target start time");
            var nextTargetSeconds = ((DateTime.Now.Ticks - _nodeBackupConfiguration.Backup.Start.Ticks) /
                                     TimeSpan.TicksPerSecond);

            if (nextTargetSeconds < 0)
                nextTargetSeconds = _nodeBackupConfiguration.Backup.Interval - nextTargetSeconds;
            else
            {
                nextTargetSeconds = _nodeBackupConfiguration.Backup.Interval -
                                    nextTargetSeconds % _nodeBackupConfiguration.Backup.Interval;
            }

            _logger.LogDebug(
                "Seconds to next interval : {0}",
                nextTargetSeconds.ToString());

            return nextTargetSeconds;
        }

        private long CalculateIntervalSecondsFromNowToBackupStart()
        {
            _logger.LogDebug("Current time is before target start time");
            var nextTargetSeconds = (_nodeBackupConfiguration.Backup.Start.Ticks - DateTime.Now.Ticks) /
                                     TimeSpan.TicksPerSecond;
            
            while (nextTargetSeconds > _nodeBackupConfiguration.Backup.Interval)
            {
                nextTargetSeconds -= _nodeBackupConfiguration.Backup.Interval;
            }

            nextTargetSeconds++;
            _logger.LogDebug("Seconds to next interval: {0}", nextTargetSeconds.ToString());

            return nextTargetSeconds;
        }

        private async void OnBackupTimerElapsed(Object source, ElapsedEventArgs e)
        {
            _logger.LogInformation("Backup was triggered at {0:HH:mm:ss}",
                e.SignalTime);
            
            await ExecuteBackupProcedure(e.SignalTime);
        }

        private async Task<bool> ExecuteBackupProcedure(DateTime time)
        {
            var filename = _nodeBackupConfiguration.Node.NodeName + "_" + time.ToString("yyyyMMdd_HHmmss") + ".tgz";

            if (!await CreateBackup(filename)) return false;
            if (!await DownloadBackup(filename)) return false;

            var backupDeleteResponse = await NitroBackup.DeleteAsync(_nitroServiceClient, filename);
            IncreaseSuccessfulBackupCounter();
            return true;
        }

        private async Task<bool> CreateBackup(string filename)
        {
            var backupCreateResponse = await NitroBackup.CreateAsync(_nitroServiceClient, filename);

            if (backupCreateResponse.ErrorCode.Equals(0)) return true;
            
            IncreaseFailedBackupCounter();
            _logger.LogWarning("Backup failed - " + backupCreateResponse.Message);
            return false;

        }

        private async Task<bool> DownloadBackup(string filename)
        {
            var backupFile = await NitroBackup.DownloadAsBase64Async(_nitroServiceClient, filename);

            return await StoreBackupToDisk(backupFile);
        }

        private async Task<bool> StoreBackupToDisk(SystemFileConfiguration fileConfiguration)
        {
            try
            {
                Directory.CreateDirectory(_outputPath);

                await File.WriteAllBytesAsync(_outputPath + "/" + fileConfiguration.FileName,
                    Convert.FromBase64String(fileConfiguration.FileContent));

                SetBackupSizeGauge(Double.Parse(fileConfiguration.FileSize));
                _logger.LogInformation("Stored backup {0} in {1}", fileConfiguration.FileName, _outputPath);

                return true;
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
                return false;
            }
        }

        private void IncreaseSuccessfulBackupCounter()
        {
            _successfulBackupCounter.WithLabels(metricLabels).Inc();
        }

        private void IncreaseFailedBackupCounter()
        {
            _failedBackupCounter.WithLabels(metricLabels).Inc();
        }

        private void SetBackupSizeGauge(double size)
        {
            _backupSize.WithLabels(metricLabels).IncTo(size);
        }
    }
}