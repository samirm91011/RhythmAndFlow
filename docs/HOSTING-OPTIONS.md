# Hosting – options and decision (rubric §9.4.3)

**Situation (2026-10-02):** the campus Azure subscription was removed from Samir's account. Smart App Control blocks running the API on the laptop, so the API **must** be hosted. Presentation: **Wed 7 Oct 2026**.
Swagger/OpenAPI is *documentation* for the API, not a host – it is added (page `/docs`) but does not solve hosting.

## What the rubric needs
Host the **API** and the **database** so the solution is accessible, stable and responsive. Any provider is acceptable; the Task 1 document proposed AWS, and the host can be changed with a note in the presentation ("campus Azure access was withdrawn, so we moved to X – the app is host-neutral because of the Docker container + environment settings").

## Option 1 – Azure for Students (try first, ~15 minutes)
- Free **US$100 credit, no credit card**, with a university e-mail: https://azure.microsoft.com/free/students
- If it works, follow `docs/AZURE-SETUP.md` unchanged (the pipeline already supports it). $100 comfortably covers the cheapest App Service + PostgreSQL for the week.
- Risk: verification can fail for some campus e-mail domains.

## Option 2 – Render (free fallback, ready in the repo)
Facts (from Render's docs, checked 2026-10-02):
- Free web service **sleeps after 15 minutes idle; waking takes about a minute** → open `/health` a few minutes before the demo (or add a free uptime pinger).
- Free PostgreSQL **expires after 30 days** (14-day grace) → fine for the demo; **not** for the real launch.
- Free services **cannot use SMTP ports 25/465/587** → Gmail SMTP does not work there. The API therefore supports **Brevo's web API** for email (`Email__Provider=Brevo`).
- Needs: a Render account (sign in with GitHub) and a free Brevo account.

### Render setup (repository owner, ~25 minutes)
1. Sign up at https://render.com with GitHub and allow access to `samirm91011/rythmflowdev`.
2. **New → Blueprint**, pick the repository, branch `main`. Render reads `render.yaml` and proposes `rhythmflow-api` (web, Docker) + `rhythmflow-db` (PostgreSQL). Region: Frankfurt (closest on offer).
3. When asked for the values marked *sync: false*, type them in the Render page (never in chat):
   - PayFast: `PayFast__MerchantId`, `PayFast__MerchantKey`, `PayFast__Passphrase` (from your private `appsettings.Local.json`)
   - `Seed__Users__0__Password` (admin) and `Seed__Users__1__Password` (demo customer): choose strong passwords
   - `Admin__AlertEmails__0`: `rythmandflow12@gmail.com`
   - `Brevo__ApiKey`, `Brevo__SenderEmail` (see Brevo below)
   - `PayFast__PublicBaseUrl`: leave blank for now.
4. After the first deploy, open the service: its address looks like `https://rhythmflow-api-xxxx.onrender.com`. Set `PayFast__PublicBaseUrl` to exactly that and save (Render restarts it).
5. Browser check: `<address>/health` → `{"status":"ok"…}`; `<address>/docs` → interactive API documentation.
6. Wire the pipeline: Render → the service → **Settings → Deploy Hook** → copy the URL. On GitHub: **Settings → Secrets and variables → Actions**:
   - Secret `RENDER_DEPLOY_HOOK_URL` = that URL
   - Variable `DEPLOY_TARGET` = `render`
   - Variable `AZURE_API_URL` = the `https://…onrender.com` address (the name is shared by both hosts)
   - Environment `production` (Settings → Environments)

### Brevo (email without SMTP) – ~10 minutes
1. Free account at https://www.brevo.com (300 emails/day).
2. **Senders, domains & dedicated IPs → Senders → Add a sender**: `rythmandflow12@gmail.com`, then click the verification link Brevo e-mails to that inbox.
3. **SMTP & API → API keys → Generate a new API key** → copy it into Render as `Brevo__ApiKey`; the verified address goes in `Brevo__SenderEmail`.

## Not recommended
- Running the API from the laptop (Smart App Control blocks it; a laptop is also a single point of failure at the demo).
- Free .NET shared hosts with no PostgreSQL / old .NET versions.

## CURRENT DECISION (2026-10-02, evening): **Azure for Students** (Azure App Service + Azure Database for PostgreSQL)
A team member obtained an Azure for Students subscription (US$100 credit, no card), so **Azure is the hosting plan**; follow `docs/AZURE-SETUP.md`. The pipeline deploys to it when the GitHub variable `DEPLOY_TARGET` is `azure`.
**xneelo Cloud is kept only as a backup** (nothing has been bought). Branch names that contain "xneelo" are historical. The container stack in `deploy/` and the Stack test are host-neutral: the Stack test also proves the API works against PostgreSQL, which is what Azure uses.

## Backup plan: xneelo Cloud (not bought)
Render and the original Azure subscription were ruled out earlier (the campus Azure subscription was removed; xneelo is South African, rand-priced and billed by the hour).
- Package: **s-g-1cpu-2gb + 30 GB Premium boot volume, Ubuntu 24.04** ≈ R157/month ≈ R5.20/day.
- The xneelo *Volume Plan* is shared PHP/MySQL hosting and cannot run this .NET API – it is not used.
- Everything needed is in the repo: `deploy/` (compose stack, Caddy HTTPS, server setup, deploy, backup and smoke-test scripts), `.github/workflows/stack-test.yml` (builds the real container + PostgreSQL, tests every feature, load-tests and measures memory) and the `deploy-vps` job in `deploy.yml`.
- Step-by-step: `docs/XNEELO-SETUP.md`.

## Decision log
| Date | Decision |
|---|---|
| 2026-10-02 | **xneelo Cloud chosen** (Ubuntu VPS, Docker, Caddy, PostgreSQL). Render/Azure not used. |
| 2026-10-02 | Azure subscription lost. Try Azure for Students; Render + Brevo prepared as fallback (Dockerfile, `render.yaml`, deploy workflow, URL-style DB connection strings, Brevo email option). |
