# Task 2 rubric checklist (INSY7315 module manual §9.4–9.5)

Task 2 = Code & Implementation (70%) + Implementation Presentation (20%). The presentation also asks the lecturer to **approve implementation for the client**.
Status key: DONE / PARTIAL / TODO. Last updated 4 October 2026. This file doubles as the presentation outline.

## 9.4.1 Front end
| Criterion | What we show | Status |
|---|---|---|
| Look & feel | Brand teal/tangerine/black/grey/white, brand fonts (Cormorant stand-in for Amaris + Open Sans), soft shadows, gradient buttons, press feedback, skeleton loading, screen transitions | DONE |
| Usability | Bottom navigation (side rail on tablets), clear flows (subscribe, book, watch, cancel), empty/error/loading states; every screen walked through on the emulator (`TEST-REPORT.md`) | DONE |
| Affordance | Filled pill buttons, chips, lock badges, back arrows, tappable cards with press feedback, pickers instead of typed dates | DONE |
| Branding | Logo (black/white), adaptive app icon, brand colours (RGB values), fonts | PARTIAL (client's Amaris font and photography still to come) |
| Responsive design | Phone, tablet (navigation rail, centred 720dp column) and maximum text size (layout switches so words never break) | DONE |
| Accessibility | Headings, screen-reader labels, 48dp touch targets, 4.5:1 text contrast, text scaling to 200% checked | PARTIAL (TalkBack walk-through and keyboard navigation of forms still to do) |
| Colour & typography | Palette defined once in `Theme.kt`; deeper tangerine for small text and buttons with white text (contrast) | DONE |
| Consistency | Shared components in `ui/components`; one theme for dialogs and pickers | DONE |
| Feedback & system response | Snackbars, skeletons, inline errors, confirmations, notifications (in-app, phone, push), video error with Retry | DONE |
| Performance | Lazy lists, debounced search, cached token, small payloads; load and memory numbers from the stack test | PARTIAL (cold start not yet measured on a real phone) |

## 9.4.2 Back end
| Criterion | What we show | Status |
|---|---|---|
| Programming skills | Basic (validation, loops), intermediate (services, DTOs, error handling), advanced (signed media streaming with Range, PayFast signature + ITN verification, concurrency-safe booking, entitlement tiers, push via Firebase, account deletion/export) | DONE |
| Database integration | Entity model + relationships + indexes; SQLite for development, PostgreSQL verified in the stack test | DONE (Azure PostgreSQL instance pending; migrations before real customer data) |
| APIs | REST API consumed by the app, interactive docs at `/docs`; external: PayFast (checkout, ITN, cancel), e-mail (SMTP), Firebase Cloud Messaging | PARTIAL (live PayFast sandbox, e-mail and push through the hosted API still to be proven) |
| Security | JWT with security stamp, role checks, PBKDF2 hashing, rate limiting, input validation, signed expiring video links, screen-capture blocking on the player, encrypted token storage, secrets outside source control, account deletion and data export | DONE (verify HTTPS settings on Azure) |
| Data flow & logic | Subscription state machine, progress %, booking capacity/cancellation, notifications | DONE |

## 9.4.3 Hosting  (REQUIRED – not optional)
Host the **API**, the **database**, and make the app reach them in the hosted environment; solution must be accessible, stable and responsive.
Plan: Azure (a group member's Azure for Students account) App Service for the API + Azure Database for PostgreSQL; the release APK points at the HTTPS API. Step-by-step: `AZURE-SETUP.md` and `AZURE-FRIEND-CHECKLIST.md`. **Status: TODO (waiting for the Azure resources).**

## 9.4.4 GitHub / pipelines  (REQUIRED)
| Criterion | What we show | Status |
|---|---|---|
| Branch management | `main` (release) ← `develop` ← `feature/*` in the private repo `RhythmAndFlow`; pull requests and branch protection to be switched on | PARTIAL |
| Automated testing | `ci.yml`: builds the API and runs the xUnit tests; builds the app, runs its unit tests and lint, on every push and pull request | PARTIAL (workflow in place; first green run on the new repo to be confirmed) |
| Automated deployment | `deploy.yml`: deploys the API to Azure when `main` changes, waits for `/health`, builds an APK that points at the live API | PARTIAL (needs the Azure secret and variables, then a first run) |

## 9.5 Presentation (20%) – must explain
1. Key features (front end, back end, hosting, APIs) – live demo.
2. How requirements and non-functional requirements are met (map to Task 1 FR/BR/NFR).
3. Technical decisions, design choices and challenges overcome (PayFast signature issue, TLS interception, Smart App Control blocking local test runs, signed video links, SQLite→PostgreSQL, push design, etc.).
4. Automated testing, deployment pipeline, reliability measures (error logging + admin alerts, rate limiting, health check). Evidence: `TEST-REPORT.md`.
