using System;
using CoreLayer.Citrix.Adc.NitroClient;

namespace CoreLayer.Citrix.Adc.NodeBackupWorker.Configuration
{
    public class  NodeConfiguration
    {
        public string OwnerName { get; set; }
        public string EnvironmentName { get; set; }
        public string NodeName { get; set; }
        
        public Uri NodeAddress { get; set; }
        public NitroHttpClientCertificateValidation CertificateValidation { get; set; }
        
        public string Username { get; set; }
        public string Password { get; set; }
    }
}