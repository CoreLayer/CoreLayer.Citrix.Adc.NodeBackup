using System;

namespace CoreLayer.Citrix.Adc.NodeBackupWorker.Configuration
{
    public class BackupConfiguration
    {
        public DateTime Start { get; set; }
        public int Interval { get; set; }
        public string BasePath { get; set; }
        public bool CreateSubdirectoryForNode { get; set; }
    }
}