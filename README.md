# CoreLayer.Citrix.Adc.NodeBackup


## 1. Introduction

This project allows you to automatically backup your Citrix ADC (formerly known as NetScaler) configuration to a filepath available to the client.
Developed in .NetCore using C#, we provide a fully running application in several flavors:
- Single-file executables for Linux, MacOS and Windows
- Dotnet-dependent DLL
- Linux container (https://hub.docker.com/r/corelayer/corelayer-citrix-adc-nodebackupservice)

---
## 2. Configuration
### 2.1. Prerequisites
#### 2.1.1. Citrix ADC

For optimal security, create a separate user on Citrix ADC for backup purposes.
The command policy below limits the allowed commands to the absolute minimum.

*Replace <username> and <password> with values of your own choosing.*

```text
add system cmdPolicy corelayer_backup ALLOW "(^(show\\s+system\\s+backup)|(create|rm)\\s+system\\s+backup\\s+.*)|(^show\\ssystem\\sfile\\s[\\w\\.-]+\\s-fileLocation\\s\"/var/ns_sys_backup\")"


add system user <username> <password> -externalAuth DISABLED -timeout 900 -allowedManagementInterface API
bind system user backup corelayer_backup 0
```

### 2.2. NodeBackup
NodeBackup can be configured using the following options.

Depending on your enviroment, you may choose to provide values for these parameters in one of the following ways:

|Provider|Target|
|---|---|
|Configuration file|appsettings.json|
|Environment variables|Environment variable per option|
|Command-line parameters|Parameter per option|

#### 2.2.1. Node Configuration:

|Parameter|Value|Default|Description|
|---|---|---|---|
|Organization|CoreLayer|N/A|Organization Name|
|Environment|Production|N/A|ADC Environment Name|
|NodeName|nsprod|N/A|ADC Node Name|
|NodeAddress|https://nsprod.prd.corelayer.local|N/A|ADC NSIP or URL|
|Username|backup|N/A|Username|
|Password|backup|N/A|Password|
|CertificateValidation|Disabled|Enabled|Validate ADC SSL certificate (Disable for self-signed certificates|

#### 2.2.2. Backup Configuration:

|Parameter|Value|Default|Description|
|---|---|---|---|
|Start|00:00|N/A|Start time for backups (HH:MM)|
|Interval|3600|3600|Interval in seconds, must be factor of 300 (5minutes)|
|BasePath|/var/corelayer/adc/nodebackup|N/A|Base path to store backups|
|CreateSubdirectoryForNode|true|true|Create a subdirectory for the node|

#### 2.2.3. Prometheus Configuration:

|Parameter|Value|Default|Description|
|---|---|---|---|
|Enable Metrics Server|true|true|Enable the Prometheus Metrics Endpoint|
|Metrics Server Port|5000|5000|TCP Endpoint for the Metrics server|
|Metrics Server Use https|false|true|Run the TCP Endpoint with https|
|Metrics Name Prefix|corelayer|corelayer|Prefix for the metric names|

## 3. Platforms
### 3.1. Docker
#### 3.1.1. Docker-compose
##### 3.1.1.1. Template configuration

- *Replace __nodename__ with the actual node name.*
- *Replace the __hostport__ with the desired external port number.*

```yaml
version: '3'
services:
  nodename-nodebackup-service:
    image: corelayer/corelayer-citrix-adc-nodebackupservice:dev-latest
    container_name: nodename-nodebackupservice
    environment:
      - Logging__LogLevel__Default=Information
      - NodeBackupConfiguration__Node__OwnerName=
      - NodeBackupConfiguration__Node__EnvironmentName=
      - NodeBackupConfiguration__Node__NodeName=
      - NodeBackupConfiguration__Node__NodeAddress=
      - NodeBackupConfiguration__Node__Username=
      - NodeBackupConfiguration__Node__Password=
      - NodeBackupConfiguration__Node__CertificateValidation=
      - NodeBackupConfiguration__Backup__Start=
      - NodeBackupConfiguration__Backup__Interval=
      - NodeBackupConfiguration__Backup__BasePath=
      - NodeBackupConfiguration__Backup__CreateSubdirectoryForNode=
      - NodeBackupConfiguration__Prometheus__MetricsServer__Enabled=
      - NodeBackupConfiguration__Prometheus__MetricsServer__Port=5000
      - NodeBackupConfiguration__Prometheus__MetricsServer__UseHttps=
      - NodeBackupConfiguration__Prometheus__NamePrefix=
    volumes:
       - $PWD:/var/corelayer/adc/nodebackup
    ports:
       - "hostport:5000/tcp"
```

#### 2.3.2 Example

```yaml
version: '3'
services:
  <nodename>-nodebackup-service:
    image: corelayer/corelayer-citrix-adc-nodebackupservice:dev-latest
    container_name: <nodename>-nodebackupservice
    environment:
      - Logging__LogLevel__Default=Information
      - NodeBackupConfiguration__Node__OwnerName=CoreLayer
      - NodeBackupConfiguration__Node__EnvironmentName=Production
      - NodeBackupConfiguration__Node__NodeName=nsprod01
      - NodeBackupConfiguration__Node__NodeAddress=https://nsprod01.corelayer.local
      - NodeBackupConfiguration__Node__Username=backup
      - NodeBackupConfiguration__Node__Password=backup
      - NodeBackupConfiguration__Node__CertificateValidation=Enabled
      - NodeBackupConfiguration__Backup__Start=00:00
      - NodeBackupConfiguration__Backup__Interval=3600
      - NodeBackupConfiguration__Backup__BasePath=/var/corelayer/adc/nodebackup
      - NodeBackupConfiguration__Backup__CreateSubdirectoryForNode=true
      - NodeBackupConfiguration__Prometheus__MetricsServer__Enabled=true
      - NodeBackupConfiguration__Prometheus__MetricsServer__Port=5000
      - NodeBackupConfiguration__Prometheus__MetricsServer__UseHttps=false
      - NodeBackupConfiguration__Prometheus__NamePrefix=corelayer
    volumes:
       - $PWD:/var/corelayer/adc/nodebackup
    ports:
       - "5000:5000/tcp"
```