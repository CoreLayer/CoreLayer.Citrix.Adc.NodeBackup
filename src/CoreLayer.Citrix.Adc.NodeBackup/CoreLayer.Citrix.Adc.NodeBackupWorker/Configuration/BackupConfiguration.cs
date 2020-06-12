using System;

namespace CoreLayer.Citrix.Adc.NodeBackupWorker.Configuration
{
    public class BackupConfiguration
    {
        public DateTime Start { get; set; }
        public int Interval { get; set; } = 3600;
        public string BasePath { get; set; }
        public bool CreateSubdirectoryForNode { get; set; } = true;
    }
}