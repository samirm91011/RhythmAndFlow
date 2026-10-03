# Task 2 rubric checklist (INSY7315 module manual §9.4–9.5)

Task 2 = Code & Implementation (70%) + Implementation Presentation (20%). The presentation also asks the lecturer to **approve implementation for the client**.
Status key: DONE / PARTIAL / TODO. Update this file as work lands; it doubles as the presentation outline.

## 9.4.1 Front end
| Criterion | What we show | Status |
|---|---|---|
| Look & feel | Brand teal/tangerine/black/grey/white, serif headings, consistent cards and buttons, visual refresh in progress | PARTIAL |
| Usability | Bottom navigation, clear flows (subscribe, book, watch), empty/error/loading states | PARTIAL |
| Affordance | Filled buttons, chips, lock badges, press feedback | PARTIAL |
| Branding | Logo (black/white), brand colours (RGB values), serif heading font stand-in until Amaris files arrive | PARTIAL |
| Responsive design | Must adapt to phone **and tablet** (and large screens): adaptive layout with navigation rail + width limits; test with emulator at tablet size | TODO |
| Accessibility | Screen-reader labels (TalkBack), 48dp touch targets, contrast ≥4.5:1, scalable text up to 200%, keyboard/IME navigation | TODO |
| Colour & typography | Palette defined once in `Theme.kt`; deep teal used for text-bearing buttons for contrast | PARTIAL |
| Consistency | Shared components in `ui/components` | PARTIAL |
| Feedback & system response | Snackbars, spinners, inline errors, confirmations, notifications | PARTIAL |
| Performance | Lazy lists, debounce on search, cached tokens, small API payloads; measure cold start | TODO |

## 9.4.2 Back end
| Criterion | What we show | Status |
|---|---|---|
| Programming skills | Basic (validation, loops), intermediate (services, DTOs, error handling), advanced (signed media streaming with Range, PayFast signature + ITN verification, concurrency-safe booking, entitlement tiers) | DONE |
| Database integration | Entity model + relationships + indexes (SQLite now, PostgreSQL hosted) | DONE / PostgreSQL TODO |
| APIs | REST API consumed by the app; external: PayFast (checkout, ITN, cancel), email (SMTP) | PARTIAL |
| Security | JWT, role checks, PBKDF2 password hashing, rate limiting, input validation, signed expiring video links, FLAG_SECURE, encrypted token storage, security stamp, secrets outside source control | PARTIAL |
| Data flow & logic | Subscription state machine, progress %, booking capacity/cancellation, notifications | DONE |

## 9.4.3 Hosting  (REQUIRED – not optional)
Host the **API**, the **database**, and make the app reach them in the hosted environment; solution must be accessible, stable and responsive.
Plan: Azure (campus subscription) App Service for the API + Azure Database for PostgreSQL; app release build points at the HTTPS API. Status: TODO.

## 9.4.4 GitHub / pipelines  (REQUIRED)
| Criterion | Plan | Status |
|---|---|---|
| Branch management | `main` (release) ← `develop` ← `feature/*`, pull requests, protected `main` | TODO (local repo exists; not on GitHub) |
| Automated testing | xUnit tests for API logic + Android unit tests/lint/build, run on every PR | TODO |
| Automated deployment | GitHub Actions deploys the API to Azure when `main` changes | TODO |

## 9.5 Presentation (20%) – must explain
1. Key features (front end, back end, hosting, APIs) – live demo.
2. How requirements and non-functional requirements are met (map to Task 1 FR/BR/NFR).
3. Technical decisions, design choices and challenges overcome (PayFast signature issue, TLS interception, signed video links, SQLite→PostgreSQL, etc.).
4. Automated testing, deployment pipeline, reliability measures (error logging + admin alerts, rate limiting, health check).
