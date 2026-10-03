# xneelo Cloud – setup guide (API + database on one Ubuntu server)

**What you get:** `https://<your address>` serving the Rhythm & Flow API, with PostgreSQL, automatic HTTPS, nightly backups, and automatic deployment from GitHub.
**Time:** about 45–60 minutes the first time. **Cost:** see "Which package" below – roughly R5 per day while it exists.
**Who does what:** whoever owns the xneelo account does Parts 1–2; anyone with the project folder does the rest.

> Never paste passwords, keys or the `.env` file into chat. Type them only where this guide says.

---

## Which package to take (the answer to "what do I buy?")
| Item | Choice | Why |
|---|---|---|
| xneelo Cloud instance | **`s-g-1cpu-2gb`** (1 CPU, 2 GB RAM) – R0.14/hour ≈ R103/month | The stack (API + PostgreSQL + HTTPS + operating system) needs roughly 1 GB; 1 GB servers have no headroom. 2 GB is the smallest comfortable size. Measured numbers are in the **Stack test** run summary on GitHub (Actions → Stack test → Summary). |
| Boot volume | **30 GB Premium (NVMe)** – R1.80/GB/month ≈ R54/month | Room for Docker images, logs and 7 days of backups (the default 10 GB is tight) |
| Public IPv4 address | included (currently free) | |
| Traffic | unlimited (fair use) | videos stream through the API, so this matters |
| Operating system | **Ubuntu 24.04 LTS** | what the setup script is written for |
| **Total** | **≈ R157/month, ≈ R0.215/hour ≈ R5.20/day** | A 10-day demo ≈ R50. Billing is hourly: delete the server afterwards. |

Prices from xneelo's Cloud page (checked 2026-10-02). If the demo needs more power you can resize the instance later (xneelo bills by the hour) – no need to over-buy now.

---

## Part 1 – Create the server (xneelo account owner)
1. Log in to your xneelo account and open **Cloud** (xneelo.co.za → Cloud → *Log in / Get started*). If Cloud isn't enabled on your account yet, activate it there and tell Samir how long xneelo says it takes.
2. **Create an SSH key first** (so no password is ever used). On your Windows PC open PowerShell and run:
   ```
   ssh-keygen -t ed25519 -f $env:USERPROFILE\.ssh\xneelo_rhythmflow -C "rhythmflow-deploy"
   ```
   Press Enter twice (no passphrase – the pipeline needs to use it). Two files appear in `C:\Users\<you>\.ssh\`:
   `xneelo_rhythmflow` (**private – never share**) and `xneelo_rhythmflow.pub` (public – this one goes to xneelo).
3. In the xneelo Cloud console add the **public** key (open `xneelo_rhythmflow.pub` in Notepad and paste its single line) under *SSH keys / key pairs*.
4. **Create instance**:
   - Image: **Ubuntu 24.04**
   - Size: **s-g-1cpu-2gb**
   - Boot volume: **Premium**, **30 GB**
   - SSH key: the one you added
   - Network/security: allow **TCP 22 (SSH), 80 (HTTP), 443 (HTTPS)** only. (If you can attach a security group, make one with just those three; the server's own firewall also enforces this.)
   - Attach a **floating/public IPv4** address.
5. When it is running, note the **public IP address** (e.g. `41.x.x.x`) and the **login user** xneelo shows (usually `ubuntu` or `root`).

## Part 2 – Choose the web address
Pick ONE:
- **A. IP-based name (zero setup, good for the demo):** take the IP, replace dots with dashes and add `.sslip.io`. IP `41.12.34.56` → **`41-12-34-56.sslip.io`**. It points at your server automatically and gets a normal HTTPS certificate.
- **B. Your own domain (nicer, better for launch):** in the DNS settings of your domain add an **A record**: name `api`, value = the server IP. The address is then `api.yourdomain.co.za`. DNS can take minutes to hours.

Write the chosen address down – it is **API_DOMAIN** below.

## Part 3 – Prepare the server (run on your PC, in the project folder)
Open PowerShell in `C:\Users\samir\AndroidStudioProjects\RhythmAndFlow` (replace `LOGIN` and `IP`):
```
scp -i $env:USERPROFILE\.ssh\xneelo_rhythmflow deploy\setup-server.sh deploy\docker-compose.yml deploy\Caddyfile deploy\deploy.sh deploy\backup.sh deploy\smoke-test.sh deploy\.env.example LOGIN@IP:/tmp/
ssh -i $env:USERPROFILE\.ssh\xneelo_rhythmflow LOGIN@IP
```
(Answer `yes` to the fingerprint question.) You are now logged in to the server. Run:
```
sudo bash /tmp/setup-server.sh
```
It takes a few minutes and ends with "Server is ready." It installs Docker, locks the firewall to 22/80/443, turns on brute-force protection and automatic security updates, adds swap, creates the pipeline user `rfdeploy`, and turns off password logins.
Then move the stack files into place and create the settings file:
```
sudo cp /tmp/docker-compose.yml /tmp/Caddyfile /tmp/deploy.sh /tmp/backup.sh /tmp/smoke-test.sh /opt/rhythmflow/
sudo cp /tmp/.env.example /opt/rhythmflow/.env
sudo chown -R rfdeploy:rfdeploy /opt/rhythmflow
sudo chmod 600 /opt/rhythmflow/.env
sudo chmod +x /opt/rhythmflow/*.sh
sudo bash /tmp/setup-server.sh        # run it again so the nightly backup job gets installed
sudo -u rfdeploy nano /opt/rhythmflow/.env
```

## Part 4 – Fill in the settings file (`nano` opens it)
Replace every `CHANGE-ME` (arrow keys to move, type to edit; **Ctrl+O, Enter** saves; **Ctrl+X** exits):
| Setting | Value |
|---|---|
| `API_DOMAIN` | the address from Part 2 (no `https://`) |
| `DB_PASSWORD` | a long password, letters and numbers only |
| `Jwt__Key`, `Media__SigningKey` | two *different* random values. To make one, open a **second** terminal on the server and run `openssl rand -base64 48 \| tr -d '/+='` – copy the output |
| `PayFast__MerchantId / MerchantKey / Passphrase` | from your private `appsettings.Local.json` (PayFast section) |
| `PayFast__PublicBaseUrl` | `https://` + the `API_DOMAIN` |
| `Smtp__User`, `Smtp__FromAddress`, `Admin__AlertEmails__0` | the Gmail address; `Smtp__Password` = its 16-letter app password (no spaces) |
| `Seed__Users__0__Password`, `Seed__Users__1__Password` | choose passwords for the demo admin and demo customer (tell Samir) |

**Check email can leave the server:** run `nc -zv smtp.gmail.com 587`. If it says *succeeded* you are fine. If it times out, the port is blocked: use Brevo instead (steps in `docs/HOSTING-OPTIONS.md`, "Brevo") by adding `Email__Provider=Brevo`, `Brevo__ApiKey=…`, `Brevo__SenderEmail=…` to `.env`.

## Part 5 – Connect GitHub (repository owner)
GitHub repo → **Settings → Secrets and variables → Actions**
- **Secrets → New repository secret:**
  - `VPS_HOST` = the server IP
  - `VPS_USER` = `rfdeploy`
  - `VPS_SSH_KEY` = the **entire contents of the private key file** `xneelo_rhythmflow` (open in Notepad; include the `-----BEGIN…` and `-----END…` lines)
- **Variables → New repository variable:**
  - `DEPLOY_TARGET` = `vps`
  - `AZURE_API_URL` = `https://` + the `API_DOMAIN` (no slash at the end; the name is shared by all hosts)
- **Settings → Environments → New environment:** `production`.

## Part 6 – Deploy
1. GitHub → **Actions → Deploy → Run workflow** (branch `main`).
2. It builds and tests the API, publishes the container image, copies the files to the server, starts everything, waits for it to be healthy, and runs the smoke test **against the live address**.
3. Open `https://<API_DOMAIN>/health` → `{"status":"ok"…}`; `https://<API_DOMAIN>/docs` → the API documentation.
4. Build the phone app for it: the pipeline attaches **rhythm-and-flow-demo-apk** to the run (Actions → the run → Artifacts); or locally: `gradlew assembleDebug -PapiBaseUrl=https://<API_DOMAIN>/`.

## Everyday operations (on the server: `ssh -i … rfdeploy@IP`, then `cd /opt/rhythmflow`)
| Task | Command |
|---|---|
| Is everything running? | `docker compose ps` |
| API log (live) | `docker compose logs -f api` |
| Restart the API | `docker compose restart api` |
| Back up now | `./backup.sh` (also runs nightly 02:30; files in `backups/`, 7 days kept) |
| Restore a backup | `gunzip -c backups/rhythmflow-DATE.sql.gz \| docker compose exec -T db psql -U rfadmin rhythmflow` |
| Memory use | `docker stats --no-stream` and `free -m` |
| Test the live server | `./smoke-test.sh https://<API_DOMAIN>` |
| Change a setting | edit `.env`, then `docker compose up -d` |

## Cost control and clean-up
- Billing is hourly. **After the presentation delete the instance and its volume** in the xneelo console (or keep it for the client's pilot).
- Before launch for the client: see `docs/REMINDERS.md` (rotate every secret, real domain, remove demo data/accounts).
- **Rollback:** every deployment is tagged with the Git commit. On the server run `API_IMAGE=ghcr.io/<owner>/rhythmflow-api:<older-commit-sha> ./deploy.sh`.

## If something fails
| Symptom | Likely cause |
|---|---|
| HTTPS certificate error at first | DNS not pointing at the IP yet, or ports 80/443 closed at xneelo – wait 5 minutes, then `docker compose logs caddy` |
| Deploy workflow fails at "Copy the stack files" | `/opt/rhythmflow` missing or wrong owner (`sudo chown -R rfdeploy:rfdeploy /opt/rhythmflow`), or the wrong key in `VPS_SSH_KEY` |
| API unhealthy | `docker compose logs api` – usually a missing/misspelled setting in `.env` |
| Password-reset emails don't arrive | port 587 blocked → use Brevo (Part 4) |
| PayFast payment succeeds but plan not active | `PayFast__PublicBaseUrl` wrong, or `PayFast__ValidateWithServer=true` with wrong merchant details – see API log lines containing "ITN rejected" |
