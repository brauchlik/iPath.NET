# Operations

Day-to-day playbook for the bits that bite operators but aren't covered by installation.

## DataProtection keys

**What they are.** ASP.NET Core's `Microsoft.AspNetCore.DataProtection` subsystem uses an
on-disk key ring to encrypt auth cookies, antiforgery tokens, password-reset links, and
anything else that calls `IDataProtector.Protect(...)`. The keys live as XML files like
`/opt/ipath/keys/key-<guid>.xml`. On Linux with no certificate configured, the `<masterKey>`
inside is **plaintext** (XML, not encrypted at rest) — that's why the directory must be
`chmod 700` and owned by the service user.

**What they protect.**
- Auth cookie (`Identity.Application`) — proves who the user is on each request
- Antiforgery token (`__RequestVerificationToken`) — CSRF protection on form posts
- Password reset links — until consumed or expired
- External-login correlation (OAuth state)

They do **not** protect user passwords — those are bcrypt-hashed in the `users` table.

**Key lifecycle (auto-managed).**
- New key generated every 90 days; old keys kept for decryption
- Each key has a 2-day propagation delay between `creationDate` and `activationDate`
- On startup, the app soft-prunes keys whose `expirationDate` was more than 30 days ago
  (well past any cookie lifetime)

**Disk impact.** ~500 bytes per key, ~4 keys per year, ~10 KB after 5 years. Don't bother
pruning manually.

### "Users got logged out after a redeploy"

If `iPathConfig:DataProtectionKeysPath` is unset (or the service user can't write there),
the app falls back to an **in-memory key store** — and every restart regenerates new keys,
which cannot decrypt cookies issued by the old process. Users appear silently logged out.

**Diagnosis:**
```bash
sudo journalctl -u ipath-server.service -n 100 --no-pager | grep -i "key ring\|repository"
# If you see "Using an in-memory repository", the path is unset or unwritable.
```

**Fix:**
```bash
SVC_USER=$(systemctl show ipath-server.service -p User --value)
sudo mkdir -p /opt/ipath/keys
sudo chown "$SVC_USER":"$SVC_USER" /opt/ipath/keys
sudo chmod 700 /opt/ipath/keys
sudo systemctl restart ipath-server.service
```

Then add `DataProtectionKeysPath` to `appsettings.json` so it survives a redeploy.

### Smoke test

```bash
# Confirm the service user can write to the keys dir:
SVC_USER=$(systemctl show ipath-server.service -p User --value)
sudo -u "$SVC_USER" bash -c 'touch /opt/ipath/keys/.write-test && rm /opt/ipath/keys/.write-test && echo OK'
# Should print "OK"
```

---

## Apache reverse proxy — the `X-Forwarded-Proto` gotcha

**Symptom.** Browser on `https://test.ipath-network.com/` is redirected to
`http://test.ipath-network.com/Account/Login?ReturnUrl=...`. The browser blocks this as
**mixed content** because the page is HTTPS but the redirect target is HTTP. The login form
never loads.

**Why.** The Kestrel process behind Apache sees `http://localhost:5000/...` because Apache
terminates TLS before forwarding. Without `X-Forwarded-Proto: https` set by Apache, the app
thinks the original request was HTTP and uses `http://` in the Location header.

**Fix in Apache:**
```apache
RequestHeader set X-Forwarded-Proto "https"
```

**Fix in iPath config:**
```json
"iPathConfig": {
  "ReverseProxyAddresse": "<apache-server-ip>"
}
```

Without this IP in `ReverseProxyAddresse`, ASP.NET Core drops the forwarded header for
security (it doesn't trust untrusted proxies by default).

**Verify:**
```bash
curl -I -k https://test.ipath-network.com/api/v1/admin/database
# Look at the Location header in the 302 response.
# Must start with https://, not http://
```

---

## Auth cookie behavior

| Question | Answer |
|---|---|
| Does the cookie survive a server restart? | Yes — if keys are persisted (`/opt/ipath/keys` set up). |
| Does the cookie live forever? | No — `IdentityOptions.SignIn.Cookie.ExpireTimeSpan` (default 14 days, sliding). |
| Does changing my password invalidate existing cookies? | Not by default. Add `ValidateInterval` / `SecurityStamp` config to enforce it. |
| Does the framework delete the key file when the cookie expires? | No — keys and cookies have separate lifetimes. Old keys (more than 30 days past expiry) are auto-pruned on startup. |

---

## Backup checklist

`/opt/ipath/keys` MUST be in your regular backup. Backups and the live directory should
use the same encryption-at-rest policy. Losing the keys forces every user to re-login.

---

## When all else fails

1. `sudo journalctl -u ipath-server.service -n 200 --no-pager` — usually shows the actual error
2. `curl -I -k https://<host>/` — verifies the proxy is forwarding correctly
3. Check `appsettings.json` has `iPathConfig:DataProtectionKeysPath` set
4. Verify `ls -la /opt/ipath/keys/` shows files owned by the service user, not root
