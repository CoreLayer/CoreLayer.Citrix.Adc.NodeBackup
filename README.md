# CoreLayer.Citrix.Adc.NodeBackup


## Introduction

This project allows you to automatically backup your Citrix ADC (formerly known as NetScaler) configuration to a filepath available to the client.
Developed in .NetCore using C#, we provide a fully running application in several flavors:
- Single-file executables for Linux, MacOS and Windows
- Dotnet-dependent DLL
- Linux container (https://hub.docker.com/r/corelayer/corelayer-citrix-adc-nodebackupservice)

## Prerequisites
### Citrix ADC Configuration

For optimal security, create a separate user on Citrix ADC for backup purposes.
The command policy below limits the allowed commands to the absolute minimum:

```
add system cmdPolicy corelayer_backup ALLOW "(^(show\\s+system\\s+backup)|(create|rm)\\s+system\\s+backup\\s+.*)|(^show\\ssystem\\sfile\\s[\\w\\.-]+\\s-fileLocation\\s\"/var/ns_sys_backup\")"


add system user <username> <password> -externalAuth DISABLED -timeout 900 -allowedManagementInterface API
bind system user backup corelayer_backup 0
```

## Running the application
### Docker-compose
#### Template configuration
```
version: '3'
services:
  nsprod-nodebackup-service:
    image: corelayer/corelayer-citrix-adc-nodebackupservice:dev-latest
    container_name: nsprod-nodebackupservice
    environment:
      - Logging__LogLevel__Default=Information
      - NodeBackupConfiguration__Node__OwnerName=Organization
      - NodeBackupConfiguration__Node__EnvironmentName=Environment
      - NodeBackupConfiguration__Node__NodeName=NodeName
      - NodeBackupConfiguration__Node__NodeAddress=(http|https)://(IP|URL)
      - NodeBackupConfiguration__Node__Username=Username
      - NodeBackupConfiguration__Node__Password=Password
      - NodeBackupConfiguration__Node__CertificateValidation=(Enabled|Disabled)
      - NodeBackupConfiguration__Backup__Start=(HH:mm)
      - NodeBackupConfiguration__Backup__Interval=1800
      - NodeBackupConfiguration__Backup__BasePath=/var/corelayer/adc/nodebackup/
      - NodeBackupConfiguration__Backup__CreateSubdirectoryForNode=(true | false)
      - NodeBackupConfiguration__Prometheus__EnableMetricsServer=(true | false)
      - NodeBackupConfiguration__Prometheus__NamePrefix=corelayer
    volumes:
       - $PWD:/var/corelayer/adc/nodebackup
```

#### Example
Example values for the configuration
- Organization: CoreLayer
- Environment: Production
- NodeName: nsprod
- NodeAddress: https://nsprod.prd.corelayer.local
- Username: backup
- Password: backup
- CertificateValidation: false
- Start: 00:00 (Hours:Minutes)
- Interval: 1800 (in seconds, must be factor of 300)
- BasePath: /var/corelayer/adc/nodebackup
- CreateSubdirectoryForNode: Enabled
- EnableMetricsServer: true
- NamePrefix: corelayer (metric names prefix)

```
version: '3'
services:
  nsprod-nodebackup-service:
    image: corelayer/corelayer-citrix-adc-nodebackupservice:dev-latest
    container_name: nsprod-nodebackupservice
    environment:
      - Logging__LogLevel__Default=Information
      - NodeBackupConfiguration__Node__OwnerName=CoreLayer
      - NodeBackupConfiguration__Node__EnvironmentName=Production
      - NodeBackupConfiguration__Node__NodeName=nsprod
      - NodeBackupConfiguration__Node__NodeAddress=https://nsprod.prd.corelayer.local
      - NodeBackupConfiguration__Node__Username=backup
      - NodeBackupConfiguration__Node__Password=backup
      - NodeBackupConfiguration__Node__CertificateValidation=disabled
      - NodeBackupConfiguration__Backup__Start=00:00
      - NodeBackupConfiguration__Backup__Interval=1800
      - NodeBackupConfiguration__Backup__BasePath=/var/corelayer/adc/nodebackup/
      - NodeBackupConfiguration__Backup__CreateSubdirectoryForNode=true
      - NodeBackupConfiguration__Prometheus__EnableMetricsServer=true
      - NodeBackupConfiguration__Prometheus__NamePrefix=corelayer
    volumes:
       - $PWD:/var/corelayer/adc/nodebackup
```