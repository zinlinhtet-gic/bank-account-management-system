# Deploy BAMS to your VPS

This guide deploys the current .NET 10 API to your test VPS at `150.95.27.125`, alongside your two existing PHP sites. It assumes Ubuntu/Debian with systemd and an existing compatible MySQL server. Your .NET SDK is already installed. Build on Windows to avoid adding build load to the 2 GB VPS.

Replace `api.example.com` everywhere with your BAMS subdomain. Commands marked **Windows** run in PowerShell in the project workspace. Commands marked **VPS** run over SSH. Examples use your current root SSH account; `sudo` can be omitted when already root.

## 1. Check the existing VPS services

**VPS:**

```bash
cat /etc/os-release
dotnet --list-runtimes
command -v dotnet
systemctl is-active nginx apache2 mysql mariadb
sudo ss -ltnp
free -h
```

Confirm `Microsoft.AspNetCore.App 10.x` appears. Note the absolute path printed for `dotnet`; the service below assumes `/usr/bin/dotnet`. Use your actual path if different. It must be readable and executable by the `bams` service user; a root-only installation under `/root` will not work for that user.

Identify which web server currently owns public ports 80 and 443. Reuse it. Do not install a competing web server, replace the PHP sites' configuration, or remove their default site. If a hosting control panel manages your sites, create the BAMS domain/proxy through that panel instead of editing generated files.

The database commands below target MySQL. If your PHP sites use MariaDB, confirm compatibility with the project's `MySql.EntityFrameworkCore` provider before reusing it; do not replace the existing database server to follow this guide.

## 2. Set up the subdomain

Create a DNS `A` record for your chosen subdomain pointing to:

```text
150.95.27.125
```

Check DNS from the VPS:

```bash
getent ahostsv4 api.example.com
```

If an `AAAA` record exists, it must point to this VPS's working IPv6 address as well. For initial setup, use a DNS-only record if your DNS provider offers an additional proxy service.

## 3. Create a separate BAMS database and user

**VPS:** open the existing MySQL instance using an administrative account:

```bash
sudo mysql
```

Run the following SQL, replacing the password. Use a new database name if `bams` already belongs to another application.

```sql
CREATE DATABASE bams CHARACTER SET utf8mb4;
CREATE USER 'bams'@'localhost' IDENTIFIED BY 'REPLACE_WITH_DATABASE_PASSWORD';
GRANT ALL PRIVILEGES ON bams.* TO 'bams'@'localhost';
EXIT;
```

The grant is limited to the BAMS database. It permits schema changes because this application applies EF migrations during startup. No separate MySQL installation or public database port is needed. If your existing database is remote, use that server's provisioning procedure and connection address instead.

## 4. Publish and upload

**Windows:** publish into a new, empty output folder:

```powershell
dotnet publish .\bams.server\bams.server.csproj -c Release -o .\publish\bams-v1
Test-Path .\publish\bams-v1\bams.server.dll
Test-Path .\publish\bams-v1\Data\Nrc\full_nrc_dataset.json
scp -r .\publish\bams-v1 root@150.95.27.125:/root/bams-release-v1
```

Both `Test-Path` commands should return `True`. Upload the entire publish output, including DLL dependencies, JSON files, and the `Data/Nrc` folder. For a later release, use a new suffix such as `v2` to avoid nesting an upload inside an existing destination.

## 5. Install the application files

**VPS:** create a dedicated service account once:

```bash
sudo useradd --system --user-group --home-dir /var/www/bams --shell /usr/sbin/nologin bams
sudo install -d -o root -g bams -m 750 /var/www/bams
sudo cp -a /root/bams-release-v1/. /var/www/bams/
sudo chown -R root:bams /var/www/bams
sudo chmod -R u=rwX,g=rX,o= /var/www/bams
sudo install -d -o bams -g bams -m 750 /var/www/bams/uploads
sudo install -d -o bams -g bams -m 750 /var/lib/bams
```

If the `bams` account already exists, skip `useradd`. These ownership commands are for the first installation into a new application directory. Preserve uploaded files on later deployments (see step 12).

The API writes documents to `uploads`. The SDK/CLI also gets a separate writable directory at `/var/lib/bams` for first-run supporting files, configured through `DOTNET_CLI_HOME` below. Keep uploaded documents private; do not add an Nginx alias or Apache directory exposing this folder.

## 6. Create production configuration

**VPS:** generate a JWT secret and copy it for the next step:

```bash
openssl rand -hex 32
sudo install -m 600 /dev/null /etc/bams.env
sudo nano /etc/bams.env
```

Run the `install` command only when creating a new file: it would empty an existing configuration. Paste the following and replace every password and secret:

```ini
ASPNETCORE_ENVIRONMENT=Production
DOTNET_ENVIRONMENT=Production
DOTNET_CLI_HOME=/var/lib/bams
ASPNETCORE_URLS=http://127.0.0.1:5000
AllowedHosts=api.example.com;localhost;127.0.0.1
ConnectionStrings__DefaultConnection="Server=localhost;Port=3306;Database=bams;User=bams;Password=REPLACE_WITH_DATABASE_PASSWORD;"
Jwt__Key=REPLACE_WITH_GENERATED_SECRET
Jwt__Issuer=BAMS.Server
Jwt__Audience=BAMS.Client
Jwt__ExpiryMinutes=30
DefaultPasswords__ByRole__manager=REPLACE_WITH_MANAGER_PASSWORD
DefaultPasswords__ByRole__officer=REPLACE_WITH_OFFICER_PASSWORD
DefaultPasswords__ByRole__auditor=REPLACE_WITH_AUDITOR_PASSWORD
RateLimiting__ApiRequestsPerMinute=120
RateLimiting__LoginRequestsPerMinute=20
SampleData__SeedOperations=false
```

This is a **systemd environment file**. Use `NAME=value`, without `export`. It is not automatically read by ASP.NET Core: the service's `EnvironmentFile` setting loads it. Environment variables override the matching `appsettings.json` settings; `__` represents a nested configuration key. Use [systemd environment-file quoting rules](https://www.freedesktop.org/software/systemd/man/latest/systemd.exec.html#EnvironmentFile=) if values contain spaces or quotes. A semicolon inside the database password also needs connection-string escaping; using a long random alphanumeric database password avoids that ambiguity.

[`DOTNET_CLI_HOME`](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-environment-variables#dotnet_cli_home) moves CLI first-run files away from the application directory. Without this, an SDK installation can attempt to create `/var/www/bams/.dotnet` and fail because the service user cannot write to the application root. Changing ownership to `ubuntu:bams` does not grant the `bams` user write access when group permissions are read-only.

For a remote database, configure the server address and verified TLS settings required by that database provider. Do not commit `/etc/bams.env` or real production secrets to the repository.

`Production` skips the development customer/account sample data. Initial roles and staff users are still seeded on a fresh security database. Configured passwords apply to initial users, newly created users, and resets; changing configuration does not rewrite existing password hashes.

## 7. Create the service and run migrations

**VPS:**

```bash
sudo nano /etc/systemd/system/bams.service
```

Paste:

```ini
[Unit]
Description=BAMS API
After=network.target

[Service]
WorkingDirectory=/var/www/bams
ExecStart=/usr/bin/dotnet exec /var/www/bams/bams.server.dll
User=bams
Group=bams
EnvironmentFile=/etc/bams.env
Restart=on-failure
RestartSec=10
UMask=0027
SyslogIdentifier=bams

[Install]
WantedBy=multi-user.target
```

Update `ExecStart` if step 1 showed a different system-wide `dotnet` path. If MySQL is on this VPS, add its actual service name (for example, `mysql.service`) to `After=network.target` to order BAMS startup after it.

Use the explicit [`dotnet exec` application mode](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet) shown above, rather than `dotnet run` or an SDK command. Confirm `/var/www/bams/bams.server.dll`, `bams.server.runtimeconfig.json`, and `bams.server.deps.json` exist and are readable by `bams`. Uploading the parent folder instead of its contents may leave them nested under `bams-v1/`.

For an existing database, take a backup before starting this application version. Then:

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now bams
sudo systemctl status bams --no-pager
sudo journalctl -u bams -n 100 --no-pager
```

**Migrations run automatically here.** `Program.cs` calls `Database.MigrateAsync()` before seeding and before serving HTTP requests. You do not need the repository, `dotnet ef`, or a separate migration command on the VPS. The database must already exist, and the configured user must have migration permissions. This includes the new `FailedLoginAttempts` and `LoginLockedUntilUtc` columns.

Look for `Now listening on: http://127.0.0.1:5000` in the logs. `systemctl` reporting a process as active briefly is not sufficient if startup then fails. If migrations fail, stop the restart loop with `sudo systemctl stop bams`, fix the logged cause, and start the service again. Startup migration also performs normal application startup and seeding; it is not a migration-only mode.

Check the local HTTP listener:

```bash
curl -i http://127.0.0.1:5000/api/account-types
```

Without credentials this protected route should return HTTP 401. A root URL may return 404 because this is an API. If HTTP redirects to HTTPS before proxy setup, validate through the HTTPS proxy after step 8. Do not send real login passwords over public HTTP.

The published-app and systemd setup follows [Microsoft's Linux hosting guidance](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/linux-nginx?view=aspnetcore-10.0).

## 8. Add a reverse proxy and HTTPS

Keep Kestrel on loopback port 5000. Choose **one** branch below based on the existing public web server. If another application already uses port 5000, choose a free loopback port and change both `ASPNETCORE_URLS` and the proxy target.

### Option A: Existing Nginx

Create a separate site:

```bash
sudo nano /etc/nginx/sites-available/bams
```

```nginx
server {
    listen 80;
    server_name api.example.com;
    client_max_body_size 60m;
    server_tokens off;
    autoindex off;

    # Reserve a separate directory for certificate challenge files only.
    location ^~ /.well-known/acme-challenge/ {
        root /var/www/letsencrypt;
        try_files $uri =404;
    }

    # Reject hidden files/directories, including .env, .git and .aws.
    location ~ /\. {
        return 404;
    }

    # Reject application settings even when requested under /api/.
    location ~* /appsettings[^/]*\.json(?:$|/) {
        return 404;
    }

    # Reject configuration, database, source, binary and backup file paths.
    location ~* \.(?:ini|conf|config|ya?ml|toml|sql|sqlite3?|db|bak|backup|old|orig|save|swp|log|cs|csproj|sln|dll|pdb|php|aspx?)(?:$|[./~]) {
        return 404;
    }

    # Keep this a normal prefix: ^~ here would bypass the regex blocks above.
    location /api/ {
        proxy_pass http://127.0.0.1:5000;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    # All public API endpoints are under /api/; reject other paths.
    location / {
        return 404;
    }
}
```

The proxy target has no trailing URI path, so `/api/...` is preserved. Unknown paths under `/api/` are still checked by ASP.NET Core routing and authorization. File-path blocks are an extra filter, not a replacement for endpoint permissions. Do not point Nginx's `root` or an `alias` at `/var/www/bams`; the only directly served directory above contains ACME challenge files. See [Nginx location matching rules](https://nginx.org/en/docs/http/ngx_http_core_module.html#location).

Create the separate challenge directory if it does not exist:

```bash
sudo install -d -m 755 /var/www/letsencrypt/.well-known/acme-challenge
```

If HTTPS is already configured, apply the same location rules to the existing BAMS `listen 443 ssl` server block while keeping its certificate settings. Rules in a port-80 redirect block do not protect requests served by the HTTPS block.

Enable and validate it without changing the PHP sites:

```bash
sudo ln -s /etc/nginx/sites-available/bams /etc/nginx/sites-enabled/bams
sudo nginx -t
sudo systemctl reload nginx
```

Skip `ln -s` if this link already exists. Reload only after `nginx -t` succeeds. If this installation uses a `conf.d` layout instead, add the new server block in its configured include directory.

Reuse the existing Certbot installation if your PHP sites already use it. Otherwise, on Ubuntu/Debian using distribution packages:

```bash
sudo apt update
sudo apt install certbot python3-certbot-nginx
```

Do not install a second Certbot through apt if the VPS already uses a snap or panel-managed installation. Request the new domain's certificate:

```bash
sudo certbot --nginx -d api.example.com --redirect
```

This command must complete certificate installation into Nginx, not just issuance. `certbot certonly` does not install an HTTPS virtual host. If Certbot finds a valid existing certificate, select the option to reinstall it when offered; forced renewal is unnecessary. If it cannot find a matching server block, verify the enabled BAMS site's `server_name` exactly matches your real domain, run `nginx -t`, and reload before retrying. See [Certbot's distinction between obtaining and installing certificates](https://eff-certbot.readthedocs.io/en/stable/using.html).

Verify the resulting active configuration explicitly:

```bash
sudo certbot certificates
sudo nginx -T 2>&1 | less
```

In `less`, search for your real domain with `/your.domain.example` and inspect the surrounding server block. The BAMS domain must have `listen 443 ssl`, its matching `server_name`, certificate/key paths, and the `/api/` proxy and blocking locations shown above. Certbot may modify the original site or create another block; inspect active configuration rather than assuming a filename. Port 80 should redirect the domain to HTTPS. Preserve all other PHP sites and their TLS settings.

If there is no BAMS HTTPS block, installation is incomplete. Do not treat a successful `renew --dry-run` as proof that Nginx serves this domain correctly: it checks certificate renewal, not API routing. HTTPS might currently be answered by another site's default virtual host and return an Nginx HTML 404.

```bash
sudo nginx -t
sudo systemctl reload nginx
sudo certbot renew --dry-run
```

After HTTPS setup, verify sensitive paths return 404 and the protected API route returns 401 without a token:

```bash
curl -I https://api.example.com/.env
curl -I https://api.example.com/.git/config
curl -I https://api.example.com/api/appsettings.json
curl -I https://api.example.com/api/backup.sql
curl -i https://api.example.com/api/account-types
```

The local proxy headers match BAMS's trusted loopback proxy configuration. For the current Certbot installation choices and renewal procedure, see the [official Certbot instructions](https://certbot.eff.org/instructions?ws=nginx&os=snap).

### Option B: Existing Apache

Reuse Apache instead of installing Nginx. The configuration uses Apache's [reverse-proxy directives](https://httpd.apache.org/docs/2.4/mod/mod_proxy.html). Enable proxy support:

```bash
sudo a2enmod proxy proxy_http headers ssl
sudo nano /etc/apache2/sites-available/bams.conf
```

```apache
<VirtualHost *:80>
    ServerName api.example.com
    ProxyPreserveHost On
    ProxyAddHeaders On
    RequestHeader set X-Forwarded-Proto "http"
    ProxyPass / http://127.0.0.1:5000/
    ProxyPassReverse / http://127.0.0.1:5000/
    LimitRequestBody 62914560
</VirtualHost>
```

```bash
sudo a2ensite bams.conf
sudo apache2ctl configtest
sudo systemctl reload apache2
```

Reuse your existing Certbot installation or, if none exists, install the distribution Apache plugin:

```bash
sudo apt update
sudo apt install certbot python3-certbot-apache
sudo certbot --apache -d api.example.com --redirect
```

Open the SSL virtual host generated by Certbot (typically `/etc/apache2/sites-available/bams-le-ssl.conf`). Inside its `<VirtualHost *:443>` block, ensure the proxy directives above are present and change `RequestHeader set X-Forwarded-Proto "http"` to:

```apache
RequestHeader set X-Forwarded-Proto "https"
```

Keep the HTTP virtual host's redirect to HTTPS. Validate and reload:

```bash
sudo apache2ctl configtest
sudo systemctl reload apache2
sudo certbot renew --dry-run
```

## 9. Check network access and verify HTTPS

Allow public TCP ports 80 and 443 through the VPS provider's firewall and existing host firewall. Preserve SSH access and the PHP sites' rules. Do not expose ports 5000 or 3306. If UFW is already in use, inspect its rules with `sudo ufw status`; do not enable or reset a firewall blindly over SSH.

**VPS:**

```bash
curl -i https://api.example.com/api/account-types
sudo journalctl -u bams -n 50 --no-pager
```

Expect HTTP 401 for this protected endpoint without a token, with the standard JSON API error. A 502 points to a stopped API or incorrect proxy target. A redirect loop usually means the HTTPS scheme is not being forwarded correctly.

Verify both existing PHP sites still respond normally.

## 10. Connect and sign in

On Windows, change `bams.desktop/Constants/ApiConstants.cs`:

```csharp
public const string ServerBaseAddress = "https://api.example.com";
```

Rebuild the desktop client. This guide describes the client edit; deploying the API does not make that edit automatically.

On a fresh database, sign in as `manager`, `officer`, or `auditor` using the corresponding password from `/etc/bams.env`. Initial accounts require a password change. If users already exist, use their existing passwords. Do not repeatedly try the checked-in development defaults after configuring different passwords.

The API enforces:

- 120 requests per minute per IP across controller endpoints.
- An additional 20 login requests per minute per IP.
- Five consecutive incorrect passwords lock that account's login for five minutes, including correct-password attempts during the lock. Successful login clears failures; expiry starts a fresh failure cycle. Attempts during a lock do not extend it.
- HTTP 429 plus `Retry-After` for throttling or account login lockout. Account locks survive restart; request budgets are in memory. Existing tokens are not revoked by a login lock.

Clients behind the same public IP share request budgets. Do not deliberately lock a staff account merely to check deployment health.

## 11. Monitor the 2 GB VPS

```bash
free -h
vmstat 1 10
ps -eo pid,comm,rss --sort=-rss | head -20
sudo journalctl -u bams -f
```

Your earlier measurements showed about 989 MiB available and no active swapping, so a light test deployment is reasonable. Recheck alongside busy PHP traffic, reports, uploads, reconciliation, and monthly jobs. Look for sustained `si`/`so` activity, growing latency, or memory kills; occupied swap alone does not establish current pressure. Keep one BAMS instance on this test VPS.

## 12. Back up and deploy later releases

Back up both the database and uploaded documents. Keep a protected copy of `/etc/bams.env` and the service/proxy configuration separately. Database and file backups should be taken during a maintenance window with writes stopped for consistency.

**VPS, before updating:**

```bash
sudo systemctl stop bams
sudo install -d -m 700 /root/bams-backups
sudo sh -c 'umask 077; mysqldump --single-transaction --no-tablespaces bams > /root/bams-backups/bams-before-update.sql'
sudo tar -czf /root/bams-backups/bams-before-update.tar.gz -C /var/www bams
```

These backup commands assume local administrative MySQL socket access; otherwise use your database administrator's backup procedure. Confirm both commands succeed before proceeding. Use unique backup names for subsequent updates so earlier backups are retained. Copy backups off the VPS as well.

**Windows:** publish/upload a new empty version folder, for example:

```powershell
dotnet publish .\bams.server\bams.server.csproj -c Release -o .\publish\bams-v2
scp -r .\publish\bams-v2 root@150.95.27.125:/root/bams-release-v2
```

**VPS:** install `rsync` if it is unavailable, then copy without deleting the private upload directory:

```bash
sudo rsync -a --exclude='uploads/' --chown=root:bams --chmod=D750,F640 /root/bams-release-v2/ /var/www/bams/
sudo systemctl start bams
sudo journalctl -u bams -n 100 --no-pager
curl -i https://api.example.com/api/account-types
```

Keep `/etc/bams.env` and `uploads/` in place. This copy intentionally retains destination-only files; review stale application files if a release removes or renames them. The next startup applies pending migrations. Configuration-only changes require `sudo systemctl restart bams`; service-file changes also require `sudo systemctl daemon-reload` first.

Do not start two BAMS instances against the database while upgrading its schema. Restoring old DLLs does not undo a migration; database rollback requires a compatible schema or the verified backup.

## Troubleshooting

| Symptom | What to check |
| --- | --- |
| Missing connection string | `/etc/bams.env` name, `EnvironmentFile`, and `ConnectionStrings__DefaultConnection`. |
| MySQL access denied | Database user, password, host match, grants, and connection address. |
| Migration failure | Full journal output; stop the service restart loop before investigating. |
| Service exit 203/EXEC | Absolute `dotnet` path and service-user execute permissions. |
| Access denied to `/var/www/bams/.dotnet` | Create `/var/lib/bams` owned by the service user, set `DOTNET_CLI_HOME=/var/lib/bams` in `/etc/bams.env`, and restart BAMS. |
| `NuGet-Migrations` IOException during startup | This is SDK/NuGet initialization, not EF database migration. Verify the service command uses `dotnet exec` with the actual published DLL path and that the complete publish output is readable by the service user. |
| NRC file missing | Publish/upload `Data/Nrc/full_nrc_dataset.json`, preserving Linux path case. |
| Upload permission denied | `/var/www/bams/uploads` must belong to `bams:bams` and be writable by the service. |
| Upload HTTP 413 | Proxy request-body limit; BAMS's default total request limit is 60 MiB and individual file limit is 10 MiB. |
| HTTP 400 invalid host | Domain must match `AllowedHosts`. |
| HTTP 429 | Respect `Retry-After`; check rate budgets or wait for the five-minute login lock. |
| Nginx/Apache cannot bind | Another server already owns the port; use the existing public web server. |
| PHP sites stop responding | Validate the new virtual host and existing site configuration; BAMS should not replace them. |
