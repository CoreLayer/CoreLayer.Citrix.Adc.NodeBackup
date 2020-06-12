namespace CoreLayer.Citrix.Adc.NodeBackupWorker.Configuration
{
    public class PrometheusConfiguration
    {
        public bool EnableMetricsServer { get; set; } = false;
        public string NamePrefix { get; set; } = "corelayer";
    }
}