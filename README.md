# Rhythm & Flow

A fitness-subscription platform for **Rhythm & Flow** (client: Ms Denise Da Silva): an Android app, a REST API and a PostgreSQL database.
Customers subscribe monthly through PayFast, watch protected workout videos, book live classes, keep a journal and get reminders.
Administrators manage content, classes, plans and customers, and are alerted when something goes wrong.

INSY7315 (WIL) – Group 11. Task 2: Code & Implementation.

## What it does
**Customers**
- Sign up, log in, forgot / change password (6-digit emailed code), delete their account, download their data
- Browse **programmes** and the **Move** library, see which plan includes what, play videos (subscription-checked, signed expiring links, screen capture blocked), see their progress, pick up where they left off
- **Subscribe and cancel** with PayFast (sandbox now), payment history with receipt numbers
- **Book and cancel classes**, reminders one hour before, notifications in the app, on the phone and by push (Firebase)
- Mood check-ins, journal, affirmations, explore pages
- Phone and tablet layouts, large-text support, screen-reader labels

**Administrators**
- Dashboard, customers (search, switch accounts off/on), programmes, lessons and videos, classes (date/time pickers), plans
- **Error log**: every app crash, API error and playback failure is recorded, de-duplicated, shown in the app and e-mailed

## How it fits together
```
 Android app (Kotlin, Jetpack Compose)  ──HTTPS──▶  ASP.NET Core 10 API  ──▶  PostgreSQL (SQLite for local dev)
   Retrofit, Media3 player, WorkManager               JWT, EF Core                │
   Firebase Messaging (push)  ◀── push ───────────────┤                          └─ users, plans, subscriptions,
                                                      ├─▶ PayFast (checkout, ITN call-back, cancel)  payments, lessons, progress,
                                                      ├─▶ SMTP e-mail (reset codes, admin alerts)    classes, bookings, journal,
                                                      └─▶ Firebase Cloud Messaging                   notifications, error logs
```
Hosting: Azure App Service (API) + Azure Database for PostgreSQL. Deployment is automatic from GitHub Actions.

## Repository layout
| Folder | Contents |
|---|---|
| `app/` | Android app (Compose, Navigation, Retrofit, Media3, WorkManager, Firebase Messaging) |
| `backend/RhythmFlow.Api/` | the API (controllers, services, EF Core model, PayFast and e-mail integrations) |
| `backend/RhythmFlow.Api.Tests/` | xUnit tests (94) and the app's JUnit tests (17) |
| `deploy/` | Docker Compose stack, server scripts, end-to-end `smoke-test.sh` (about 97 checks), `azure/setup-azure.sh` |
| `.github/workflows/` | `ci.yml` (build + tests + lint), `stack-test.yml` (real container + PostgreSQL), `deploy.yml` (Azure) |
| `docs/` | project log, requirements checklist, test report, Azure and GitHub guides, go-live and reminders lists |

## Documentation
| File | For |
|---|---|
| `docs/REQUIREMENTS-TRACE.md` | every Task 1 requirement (FR, BR, NFR) mapped to the code and the evidence |
| `docs/RUBRIC-CHECKLIST.md` | the module rubric with the current status of every criterion |
| `docs/TEST-REPORT.md` | what was tested, how, and the results |
| `docs/AZURE-SETUP.md`, `docs/AZURE-FRIEND-CHECKLIST.md` | creating the hosting, step by step |
| `docs/GITHUB-SETUP.md` | branches, workflows, secrets, branch protection |
| `docs/GO-LIVE.md`, `docs/REMINDERS.md` | swapping test details for the client's (PayFast, e-mail, Firebase, fonts, legal pages) |
| `docs/PROJECT-LOG.md` | decisions and what changed, newest first |

## Run it locally
**API** (needs the .NET 10 SDK):
```
cd backend/RhythmFlow.Api
set ASPNETCORE_ENVIRONMENT=Development
dotnet run
```
It listens on `http://localhost:5080`, creates a local SQLite database and seeds demo data. Demo logins and keys live in git-ignored settings files (`appsettings.Local.json`, `appsettings.Development.json`); see `appsettings.Development.example.json` for the shape. Interactive API documentation is at `/docs`.

**App**: open the folder in Android Studio (JDK 17 for Gradle), pick an emulator and run. Debug builds talk to `http://10.0.2.2:5080/`. To point at a hosted API:
```
gradlew assembleDebug -PapiBaseUrl=https://<your-app>.azurewebsites.net/
```
**Demo mode** (no server, fake data, for screenshots only): `gradlew assembleDebug -PdemoMode=true`. Never use it for real.
Add `-PallowCapture=true` only when you need to screen-record the video player (it normally blocks recording).

Push notifications need `app/google-services.json` (from the Firebase console) and the API setting `Firebase__ServiceAccountJson`. Both are private; without them everything else still works.

## Testing and pipelines
- `dotnet test backend/RhythmFlow.sln` – API unit tests; `gradlew :app:testDebugUnitTest` – app unit tests; `gradlew :app:lintDebug` – Android lint.
- `deploy/smoke-test.sh <base-url>` – drives a running API through the whole customer and admin journey (about 97 checks).
- GitHub Actions run all of this on every push and pull request; merging to `main` deploys to Azure and runs the smoke test against the live address.
- Branches: `main` (release) ← `develop` ← `feature/*`, merged by pull request.

## Security in short
Passwords are PBKDF2-hashed; JWT tokens carry a security stamp so a password change, switch-off or deletion signs the person out everywhere; admin routes are role-checked; sign-in endpoints are rate-limited; video links are signed, tied to the user and expire; the player blocks screenshots; the token is stored encrypted on the phone; secrets live only in environment settings and git-ignored files; the card is entered on PayFast's own page, never in the app.

## Placeholders and client items
Plans (R99 / R199 / R299), the sample timetable, the studio location, placeholder videos and the stand-in heading font are test content. The full list of what to replace at launch is in `docs/REMINDERS.md`. Terms of Use and Privacy Policy are the client's own pages at https://rhythmandflow.co.za/terms-of-use/ and https://rhythmandflow.co.za/privacy-policy/.
