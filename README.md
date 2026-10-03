# Rhythm & Flow – Fitness Subscription App (INSY7315, Group 11)

Android app (Kotlin + Jetpack Compose) and ASP.NET Core API for **Rhythm & Flow** (client: Ms Denise Da Silva).
Customers subscribe monthly through PayFast, watch protected workout videos, track progress, keep a journal and book classes.
Administrators manage lessons, classes and plans.

```
RhythmAndFlow/
├─ app/                    Android app (Compose, Navigation, Retrofit, Media3/ExoPlayer)
├─ backend/RhythmFlow.Api/ ASP.NET Core 10 Web API (EF Core, JWT, PayFast, signed video links)
└─ README.md
```

## Run it locally (demo)

**1. Start the API** (needs the .NET 10 SDK)

```
cd backend/RhythmFlow.Api
set ASPNETCORE_ENVIRONMENT=Development
dotnet run
```

It listens on `http://localhost:5080`, creates a local SQLite database and seeds demo data (3 plans, 4 programmes, 8 lessons, 6 classes).
Demo logins are in `backend/RhythmFlow.Api/appsettings.Development.json` (this file is git-ignored; copy it between group members privately, never commit it).

**2. Run the app**: open this folder in Android Studio, let Gradle sync, pick an emulator and press Run.
The debug build talks to `http://10.0.2.2:5080/`, which is the emulator's name for your PC.
On a physical phone, change `API_BASE_URL` in `app/build.gradle.kts` to your PC's LAN address and allow it in `network_security_config.xml`.

**3. Try the full flow**: register → Move tab → open a locked lesson → *View plans* → *Subscribe* → in debug builds tap
*Simulate PayFast sandbox payment* (PayFast cannot call `localhost`) → the lesson unlocks → watch it → book a class on the Classes tab.

## What is implemented (maps to the Task 1 requirements)

| Requirement | Where |
|---|---|
| FR-01/02 register, login, JWT, roles | `AuthController`, `AuthScreens.kt` |
| FR-03/04 programmes & lessons | `ContentController`, `MoveScreens.kt` |
| FR-05/06 plans and PayFast monthly checkout | `SubscriptionsController`, `PayFastService`, `PlanScreens.kt` |
| FR-07/08 verified payment → active subscription | `PayFastController.Itn`, `SubscriptionService` |
| FR-09–12 entitlement and protected video | `EntitlementService`, `/media/{id}` signed links |
| FR-13/14 progress and completion % | `ProgressService` (watch time ÷ duration, capped at 100%) |
| FR-15–19 classes, booking, cancellation | `BookingService`, `ClassScreens.kt` |
| FR-21/22 admin login and management | `AdminController`, `AdminScreens.kt` |
| From the wireframes | Home check-in, Rhythm Today, Journal, Explore, You, Edit Profile, Settings, Shop (placeholder) |

## How video sharing is limited
1. The app never receives the video's real address. It asks the API, which checks the subscription and returns a **signed link that expires after 60 minutes** and is tied to the user.
2. The API streams the video itself (Range requests supported) so the origin URL is never exposed; tampered links return 401.
3. The player screen uses `FLAG_SECURE`, which blocks screenshots and screen recording.
4. No download button, no direct file links.
Nothing can stop someone filming a screen with another device; adding a per-user watermark is the next step if the client needs it.

## PayFast (sandbox)
Defaults use PayFast's public **sandbox** merchant details (see `appsettings.Development.json`). For a real ITN callback, expose the API with a tunnel (e.g. ngrok), set `PayFast:PublicBaseUrl` to that URL and `PayFast:ValidateWithServer` to `true`.
The ITN handler checks the signature, merchant id, the server-to-server validation and that the amount equals the plan price before activating anything.
Going live needs the client's own PayFast merchant account; keys go in environment variables / a secret store, never in git.

## Known placeholders / decisions for the client
- Plans (R99 / R199 / R299), classes, studio location, 2-hour cancellation cut-off and class capacity (20) are **placeholders**.
- Videos are public test clips. Replace them via **Admin tools → Lessons & videos**, or point `VideoProvider = "Local"` at files in the API's `media` folder.
- Heading font: Amaris is a licensed typeface and no font files were supplied, so a serif stands in (`ui/theme/Theme.kt`).
- Brand colours: the blueprint's printed hex codes disagree with its RGB values; the RGB values are used.
- Google sign-in and password reset are shown in the design but not built (they need Google Cloud setup / an email service).
- Cancelling in the app ends access at the paid period's end; the recurring PayFast charge must also be cancelled in PayFast (or via PayFast's API, not yet wired).

## Hosting (cloud-neutral)
The API runs anywhere .NET runs. Switch to PostgreSQL by setting `Database:Provider=Postgres` and `ConnectionStrings:Default`; set `Jwt:Key`, `Media:SigningKey` and PayFast values as environment variables.
`EnsureCreated` builds the schema on first start for the demo; switch to EF Core migrations before production.

## Android Studio setup notes
- **Gradle JDK:** this project uses Android Gradle Plugin 8.11 + Gradle 8.13, which need **JDK 17** (JDK 25, bundled with very new Android Studio builds, is too new for Gradle 8.13).
  In Android Studio: *Settings → Build, Execution, Deployment → Build Tools → Gradle → Gradle JDK* and choose **temurin-17** (install Eclipse Temurin 17 first if it isn't listed).
- If your network intercepts HTTPS (campus Wi-Fi) and Gradle can't download plugins (`PKIX path building failed`), add this to `%USERPROFILE%\.gradle\gradle.properties`:
  `org.gradle.jvmargs=-Xmx3072m -Dfile.encoding=UTF-8 -Djavax.net.ssl.trustStoreType=WINDOWS-ROOT -Djavax.net.ssl.trustStore=NONE`
- Android Studio may offer to upgrade the Android Gradle Plugin. Declining is fine.
- Command-line build: `gradlew.bat :app:assembleDebug` (set `JAVA_HOME` to JDK 17 first). The APK appears in `app/build/outputs/apk/debug/`.

## Branching (from the Task 1 plan)
`main` (release) ← `develop` ← `feature/*` via pull requests.
