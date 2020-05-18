using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using CoreLayer.Citrix.Adc.NodeBackupWorker;
using CoreLayer.Citrix.Adc.NodeBackupWorker.Configuration;
using Microsoft.Extensions.Configuration;
using Prometheus;

namespace CoreLayer.Citrix.Adc.NodeBackupService
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var server = new MetricServer(hostname: "localhost", port:5001, useHttps: false);
            server.Start();
            await CreateHostBuilder(args).Build().RunAsync();
        }

        private static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices(
                    (hostContext, services) =>
                    {
                        services.AddOptions<NodeBackupConfiguration>()
                            .Bind(hostContext.Configuration.GetSection("NodeBackupConfiguration"));
                        services.AddHostedService<NodeBackupHostedService>();
                    });
    }
}