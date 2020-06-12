namespace CoreLayer.Citrix.Adc.NodeBackupWorker.Configuration
{
    public class PrometheusConfiguration
    {
        public PrometheusMetricsServerConfiguration MetricsServer { get; set; }
        public string NamePrefix { get; set; } = "corelayer";
    }
}