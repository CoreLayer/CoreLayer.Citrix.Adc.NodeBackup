using CoreLayer.Citrix.Adc.NodeBackupWorker.Interfaces;

namespace CoreLayer.Citrix.Adc.NodeBackupWorker.Configuration
{
    public class NodeBackupConfiguration : IValidateable
    {
        public NodeConfiguration Node { get; set; }
        public BackupConfiguration Backup { get; set; }
        public PrometheusConfiguration Prometheus { get; set; }
    }
}