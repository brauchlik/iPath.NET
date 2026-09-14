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

## Reverse proxy (Apache 2)

iPath listens on plain HTTP (`:5000`) and expects a reverse proxy in front of it. Three
things need explicit configuration — without them the app looks fine on a dev machine and
misbehaves in production.

### 1. Tell the app which proxy to trust

Forwarded headers are ignored unless the proxy's address is known, which leaves the app
believing every request is plain HTTP from localhost. Set this in `appsettings.json`:

```json
"iPathConfig": {
  "ReverseProxyAddresse": "127.0.0.1"
}
```

Without it, `Request.IsHttps` is false behind a TLS-terminating proxy, so cookies that
should be marked `Secure` are not, and absolute URLs (external-login redirects) use the
wrong scheme and host.

### 2. WebSockets, for Blazor Server render mode

The interactive circuit needs a WebSocket upgrade. Load `mod_proxy_wstunnel` and proxy
`/_blazor` **before** the catch-all — `ProxyPass` rules match in order, so a catch-all
placed first wins and the upgrade never happens. SignalR then silently falls back to long
polling, which is what "reconnecting…" and dropped sessions usually mean.

```apache
ProxyPreserveHost On
RequestHeader set X-Forwarded-Proto "https"

# WebSocket upgrade for the Blazor circuit — must precede the catch-all
ProxyPass        /_blazor/ ws://127.0.0.1:5000/_blazor/
ProxyPassReverse /_blazor/ ws://127.0.0.1:5000/_blazor/

ProxyPass        / http://127.0.0.1:5000/
ProxyPassReverse / http://127.0.0.1:5000/
```

Not needed if `iPathClientConfig:RenderMode` is `wasm`, which has no circuit.

### 3. Server-Sent Events (notifications) must not be buffered or compressed

Notifications stream from `/api/v1/notifications/events/stream` as `text/event-stream`.
`mod_deflate` buffers the response, so events arrive in bursts or not at all. This applies
to **every** render mode, including WebAssembly.

```apache
<Location /api/v1/notifications/events/stream>
    SetEnv no-gzip 1
    SetEnv proxy-nokeepalive 0
    SetEnv proxy-sendchunked 1
</Location>
```

### Verifying

- Sign in, then force a network drop. Notifications must resume **without a page reload**.
- Save a long questionnaire — a large `QuestionnaireResponse` crossing the circuit used to
  exceed the 32 KB default message cap.
- Check that a guest CaseRoom link sets its cookie with the `Secure` flag.