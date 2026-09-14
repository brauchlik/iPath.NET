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
 - /keys       # DataProtection key ring (see "DataProtection key persistence" below)

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

---

## DataProtection key persistence

ASP.NET Core's data-protection key ring is what encrypts the auth cookie and antiforgery
tokens. Without persistent keys, every restart of `ipath-server` silently logs every user
out — the cookie is still on the browser, but the server has lost the key that can decrypt it.

For production set:

```json
"iPathConfig": {
  "DataProtectionKeysPath": "/opt/ipath/keys"
}
```

Or via env var (handy for systemd overrides):

```
Environment="iPathConfig__DataProtectionKeysPath=/opt/ipath/keys"
```

The directory must exist and be writable by the service user (`www-data` in the example unit
file above):

```bash
sudo mkdir -p /opt/ipath/keys
sudo chown www-data:www-data /opt/ipath/keys
sudo chmod 700 /opt/ipath/keys
```

The app also soft-prunes keys on startup — any key whose `expirationDate` was more than 30 days
ago is removed. The 30-day buffer is well past any reasonable cookie lifetime (Identity's
default is 14 days), so this cannot invalidate in-flight sessions.

**Back up `/opt/ipath/keys` like any other stateful data** — without the keys, every user
gets logged out on the next restart. Backups and the live directory must use the same
encryption-at-rest policy.

### Verifying

- After restart, `ls /opt/ipath/keys/` should show at least one `key-<guid>.xml`.
- Check the journal for `Using an in-memory repository` — if you see it, the path is unset
  or the service user can't write to the directory.
- Sign in, restart the service, reload the browser: you should still be logged in.

---

## Operations

For day-to-day troubleshooting (cookie behavior, key loss, Apache gotchas), see
[`docs/OPERATIONS.md`](OPERATIONS.md).