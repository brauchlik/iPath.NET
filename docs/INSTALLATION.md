# Installation 



## Installation in Docker
to be written


## Installation as Linux Service
- install the .NET SDK from https://dotnet.microsoft.com/en-us/download
- clone the repository
- inside the source folder run:

```
dotnet publish --configuration Release -o publish .
```

### Service folder (just an example, change according to your needs)
/opt/ipath
 - /data
 - /conf
 - /temp
 - /bin

copy all files from the publish folder to the bin folder
```
cp -r publish/* /opt/ipath/bin/
```

- create an appsetting.json inside the conf folder
- make sure data and tmp are writeable by the service user (e.g. www-data)

#### Install as saervice
create a service file for systemd (e.g. `/etc/systemd/system/ipath-server.service`)
```
[Unit]
Description=iPath.NET blazor

[Service]
WorkingDirectory=/opt/ipath/bin
ExecStart=/usr/bin/dotnet /opt/ipath/bin/iPath.Blazor.Server.dll
Restart=always
# Restart service after 10 seconds if the dotnet service crashes:
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=ipath-server
User=www-data
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_NOLOGO=true
Environment=CONFIG_PATH=/opt/ipath/conf
Environment=ASPNETCORE_URLS=http://*:5000

[Install]
WantedBy=multi-user.target
```

now enable and start the service (once)
```
sudo systemctl enable ipath-server
```

now start or stop the service ....

```
sudo service ipath-server start
sudo service ipath-server stop
```