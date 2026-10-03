# Rhythm & Flow – Project Log

Living record of what exists, what changed, and what is left. Newest entries at the top of each section.
**Priority:** a stable demo for the lecturer's trial presentation. Test details are the team's own; client details are swapped in at go-live (see `GO-LIVE.md`).

## Status at a glance (2026-10-02)
| Area | State |
|---|---|
| Android app (Compose) | Built and running on the emulator. Welcome, sign-in, Home, Move, lesson detail, video player, Classes, You verified on screen. Other screens built but not yet reviewed visually. |
| Backend API (ASP.NET Core 10, SQLite) | Running. Auth, plans, PayFast checkout, protected video, progress, classes, bookings, journal, admin endpoints tested by script. |
| Payments | PayFast **sandbox** checkout link accepted by PayFast. Payment confirmation (ITN) cannot reach `localhost`; demo uses the debug "Simulate payment" button. |
| In progress | Password reset, subscription cancel through PayFast, notifications + class reminders, error logging to admins, elegant visual refresh, full screen-by-screen test. |
| Dropped | Google sign-in (decided 2026-10-02: not needed, extra setup). Per-user video watermark (decided out of scope). |

## Decisions
- 2026-10-02 (evening) **Hosting is now Azure for Students** (a team member's account): Azure App Service + Azure Database for PostgreSQL, deployed by the `Deploy` workflow when `DEPLOY_TARGET=azure`. xneelo is only a backup (nothing bought); branches/files named "xneelo" are historical. Repo made **public** by Samir (history scanned: no secrets). Stack test (real container + PostgreSQL) results so far: backend unit tests 100% pass on Linux; all end-to-end feature checks pass against PostgreSQL except one test-script issue (docs redirect), fixed.
- 2026-10-02 **First push to GitHub** (`samirm91011/RhythmAndFlow`, private; history scanned first: no secrets in any commit). Branches on GitHub: `main` (baseline, no workflows), `develop`, `feature/hosting-and-api-docs`, `feature/xneelo-vps-deploy` (contains the Stack test). GitHub's own "Initial commit" was merged in. The campus repo from the lecturer will be used later (see REMINDERS #33).
- 2026-10-02 **Hosting = xneelo Cloud** (s-g-1cpu-2gb, 30 GB premium boot volume, Ubuntu 24.04, ≈R157/month, hourly billing). Built on branch `feature/xneelo-vps-deploy`: `deploy/` (docker-compose with Caddy + API + PostgreSQL, server setup/deploy/backup scripts, end-to-end `smoke-test.sh`), `stack-test.yml` workflow (real container + PostgreSQL, full feature test, load test, memory sizing summary), `deploy-vps` job in `deploy.yml`, guide `docs/XNEELO-SETUP.md`. Not yet pushed or run: the stack test needs GitHub (Samir to say "go").
- 2026-10-02 **Azure subscription removed by campus.** Host-neutral setup prepared: `backend/Dockerfile`, `render.yaml` (Render + free PostgreSQL), deploy workflow switchable by `DEPLOY_TARGET`, database URLs of the form `postgres://…` accepted, Brevo web-API email option (Render free blocks SMTP), interactive API docs at `/docs` (OpenAPI + Scalar; this is the "Swagger" style documentation page, not a host). Recommendation: try Azure for Students first, Render as fallback. Work is on branch `feature/hosting-and-api-docs` (local, not pushed).
- 2026-10-02 **Presentation date: 7 Oct 2026.** Option A chosen: keep Smart App Control on; backend runs on Azure (App Service + PostgreSQL), built/tested by GitHub Actions. Samir's GitHub repo: `samirm91011/RhythmAndFlow`. A group member will set up Azure from `docs/AZURE-SETUP.md`.
- 2026-10-02 Gmail account `rythmandflow12@gmail.com` (team test mailbox) configured for password-reset emails and admin error alerts.
- 2026-10-02 Use the team's own PayFast sandbox, Gmail and test data now; switch to the client's details at launch.
- 2026-10-02 Remove Google sign-in. (Google sign-in itself is free; the ~$25 fee is the Play Store developer account and is only needed to publish.)
- 2026-10-02 Email via Gmail SMTP with an App Password (settings in the git-ignored `appsettings.Local.json`).
- Earlier: cloud-neutral backend (SQLite locally, PostgreSQL by config); videos are placeholder public clips; plans R99/R199/R299 are placeholders; Shop is a "coming soon" screen.

## Work log
### 2026-10-03
- **Legal pages:** the client's website already has the Terms of Use (rhythmandflow.co.za/terms-of-use/) and Privacy Policy (rhythmandflow.co.za/privacy-policy/). The app now links to them from the sign-up screen and Settings, says where they can be found, and the API's draft pages were replaced by redirects to them.
- **Real push notifications built (Feature 3B):** the API stores each phone's Firebase token (`DeviceToken` table, max 5 per person, removed on log out and on account deletion) and pushes every notification through Firebase Cloud Messaging (data-only messages, same notification id as the in-app copy so nothing shows twice). Off automatically when no key is configured. The app registers after sign-in, builds without `google-services.json` so CI still works. 12 new backend tests (run in CI). **Not yet proven end to end:** real delivery can only be tried once the API runs on Azure with `Firebase__ServiceAccountJson` set.
- **App icon, accessibility first pass and 17 Android unit tests done (Features 8-10).**
- 2026-10-03 **New private repo with a fresh history:** the project moved to `samirm91011/RhythmAndFlow` (private) as a single clean starting commit; branches `main`, `develop`, `feature/app-features`. The earlier repo `rythmflowdev` is retired (delete or keep private once the pipeline works here). Secrets and variables must be re-added on the new repo (`docs/GITHUB-SETUP.md`). The campus repo comes last.
- **Visual refresh done (Feature 6):** brand fonts (Cormorant Garamond headings, Open Sans body), white cards with soft shadows, gradient pill buttons with press feedback, selected-state mood tiles, skeleton loading, screen transitions. Checked on the emulator in demo mode (Home, Move, Classes, Journal, You).
- **Account deletion and data export done (Feature 11):** API `GET /api/account/export` and `POST /api/account/delete` (password re-check, active PayFast subscription cancelled first, personal content erased, payment records kept anonymised, last admin protected), public `/terms` and `/privacy` draft pages, Settings rows in the app. 8 new unit tests (run in GitHub Actions). Verified in demo mode on the emulator: wrong password is refused, data share sheet opens.
- Demo mode (`-PdemoMode=true`) added so every screen can be checked without the server.

### 2026-10-02
- PayFast sandbox merchant details added to `appsettings.Local.json`; verified live: PayFast sandbox accepts signed one-off and monthly-subscription requests built with our passphrase (this also proves our signing code is correct).
- Google sign-in removed from the backend (service, package, user fields).
- Backend features finished and verified by script against the running API: **password reset** (6-digit emailed code, 15-min expiry, 5 guesses, 3 requests/hour, old logins signed out), **change password**, **notifications** (payment, subscription, class-cancelled events), **subscription cancel** (calls PayFast's merchant API first; access kept until the paid period ends; demo subscriptions cancel locally), **error logging** (any API crash and any app-reported error is stored, de-duplicated, shown to admins, notified in-app and emailed when SMTP is configured), unique-booking index (double-tap safe), login tokens revoked on password change/disable.
- **Automated tests written** (`backend/RhythmFlow.Api.Tests`, ~45 xUnit tests: progress, entitlement, booking rules, PayFast signing/encoding, signed video links, password reset, subscription payments/cancel). They compile, but **cannot run on this PC**: Windows Smart App Control blocks the freshly built test DLL (0x800711C7). They will run in GitHub Actions. Do NOT turn Smart App Control off (it cannot be turned back on without reinstalling Windows).
- Demo database reset (new tables).
- Backend: added tables and services for password-reset codes, in-app notifications and error logs; email service (Gmail/SMTP with log fallback); PayFast merchant-API client for cancelling subscriptions; unique-booking index; login tokens now carry a security stamp (planned wiring). **Not yet wired into controllers/Program or built** – the running API is still the previous build.
- Created `appsettings.Local.json` (private settings template) and this documentation set.

### 2026-10-01
- Built the Android app and API from the Task 1 doc and wireframes; fixed PayFast sandbox (shared test merchant rejects signatures, so unsigned in sandbox without a passphrase); fixed logo transparency and text-encoding bugs; installed Git, Temurin JDK 17, Gradle 8.13; fixed Java TLS trust on this network. Local git repo created.

## Known issues / risks
- **BLOCKER (2026-10-02): Windows Smart App Control is enforcing on this laptop and now blocks every locally built .NET DLL** (error 0x800711C7): the test project, then the API itself (Debug and Release publish). Building still works; *running* the result does not. It was fine on 2026-10-01 (Smart App Control probably switched from evaluation to enforcement overnight).
  - Consequence: the backend cannot run on this laptop. The API must run in the cloud (Azure), built by GitHub Actions or built locally and uploaded; tests run in GitHub Actions.
  - Options for Samir: (A) keep Smart App Control on and use Azure for everything (recommended; also required by the rubric); (B) turn Smart App Control off in Windows Security → App & browser control → Smart App Control (**cannot be turned back on without reinstalling Windows**; do not do this); (C) run the backend inside WSL2/Linux.
  - The Android app is NOT affected (APKs are not loaded by Windows).
- Some screens have not been viewed on screen yet (plans/payment, Journal, Explore, Shop, Settings, Edit Profile, Bookings, post-workout, admin).
- Emulator sometimes hangs on first boot (cold boot fixes it).
- The demo database must be reset when new tables are added (no migrations yet).
