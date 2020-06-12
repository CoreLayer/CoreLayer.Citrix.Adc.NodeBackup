using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using CoreLayer.Citrix.Adc.NodeBackupWorker;
using CoreLayer.Citrix.Adc.NodeBackupWorker.Configuration;
using Prometheus;

namespace CoreLayer.Citrix.Adc.NodeBackupService
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
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