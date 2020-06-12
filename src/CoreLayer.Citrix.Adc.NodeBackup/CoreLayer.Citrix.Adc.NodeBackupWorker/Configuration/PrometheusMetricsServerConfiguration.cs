using System.Net;

namespace CoreLayer.Citrix.Adc.NodeBackupWorker.Configuration
{
    public class PrometheusMetricsServerConfiguration
    {
        public bool Enabled { get; set; }
        public int Port { get; set; } = 5000;
        public bool UseHttps { get; set; } = false;
    }
}