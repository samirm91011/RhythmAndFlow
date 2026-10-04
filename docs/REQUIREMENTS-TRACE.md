# Requirements trace (Task 1 → code → evidence)

Maps every requirement in the Task 1 document ("INSY7315 Task 1 - Documentation - Group 11", sections 9.1.5 and 9.1.8) to where it is built and how it is checked.
Updated 4 October 2026. Use it for presentation point 2, "how requirements and non-functional requirements are met".

Evidence key: **T** = automated test (`backend/RhythmFlow.Api.Tests`), **S** = end-to-end `deploy/smoke-test.sh`, **M** = checked by hand on the emulator (`TEST-REPORT.md`).

## Functional requirements
| ID | Requirement (short) | Where it is built | Evidence |
|---|---|---|---|
| FR-01 | Create an account | `AuthController.Register`, `SignUpScreen` | S, M |
| FR-02 | Log in and securely reach protected functions | `AuthController.Login`, JWT, `SignInScreen` | S, M |
| FR-03 | Browse fitness programmes | `GET /api/programmes`, Programmes row on the Move tab (`MoveScreens.kt`) | S, M |
| FR-04 | View information about a programme | `ProgrammeScreen` (description, lessons, which plan includes it) | S, M |
| FR-05 | View plans/tiers and select one | `GET /api/plans`, `PlansScreen` | S, M |
| FR-06 | Start monthly recurring payment with PayFast | `SubscriptionsController.Checkout`, `PayFastService`, `PaymentScreen` | T, S, M |
| FR-07 | Receive, validate and record the payment result | `PayFastController.Itn` (signature, merchant, server check, amount), `SubscriptionService.ApplyPaymentResultAsync`, payment history | T, S |
| FR-08 | Activate the subscription after confirmed payment | `ApplyPaymentResultAsync` (only on COMPLETE) | T, S |
| FR-09 | Decide access from the active subscription and plan rules | `EntitlementService` (tiers) | T, S |
| FR-10 | Show programmes and lessons the customer may use | `ContentController`, Move tab, locked/unlocked states | T, S, M |
| FR-11 | View video lessons | signed expiring links and the `/media` streaming endpoint, `PlayerScreen` | T, S |
| FR-12 | Block restricted content without entitlement | 403 on playback, lock badges, `ProgrammeScreen` upsell | T, S, M |
| FR-13 | View workout progress | `ProgressScreen` (My progress), Home "continue" card, progress bars | M |
| FR-14 | Record watch progress and calculate completion % | `ProgressService` (watch time ÷ duration, capped at 100%) | T, S |
| FR-15 | View upcoming classes | `ClassesScreen`, `GET /api/classes` | S, M |
| FR-16 | Class details (date, time, coach, location) | class cards and `ClassDto` | S, M |
| FR-17 | Book an available class | `BookingService.BookAsync` | T, S, M |
| FR-18 | View upcoming/booked classes | `BookingsScreen`, Home "your next class" | T, S, M |
| FR-19 | Cancel a booking where the rules allow | `BookingService.CancelAsync` (2-hour cut-off) | T, S, M |
| FR-20 | Confirmation, loading and error feedback | snackbars, skeleton loading, confirm dialogs, inline errors, video retry | M |
| FR-21 | Authenticated administrator login | role claim + `[Authorize(Roles = Admin)]`, admin screens only for admins | T, S, M |
| FR-22 | Administrators manage programmes, plans, lessons, class scheduling | Admin tools: Programmes (add, edit, hide), Lessons & videos, Classes (date/time pickers, cancel), Subscription plans; plus Customers and Error log | T, S, M |

## Business rules
| ID | Rule | Where | Evidence |
|---|---|---|---|
| BR-01 | Account before account-specific functions | `[Authorize]` on all customer endpoints | S |
| BR-02 | Authenticate before protected functions | JWT + security stamp checked on every request | T, S |
| BR-03 | Select a plan before paying | checkout takes a plan id | S |
| BR-04 | Not active until PayFast confirms | `ApplyPaymentResultAsync` | T, S |
| BR-05 | More than one subscription only where rules permit | checkout refused while a plan is renewing (would double-charge); a cancelled plan can be followed by a new one | T, S, M |
| BR-06 | Active subscription gives access to its programmes | `EntitlementService` | T, S |
| BR-07 | No access without entitlement | 403 + locked UI | T, S |
| BR-08 | Content includes video lessons | `Lesson.VideoProvider/VideoReference` | S |
| BR-09 | Progress belongs to the customer and the lesson | `Progress` unique per user and lesson | T |
| BR-10 | Completion derived from watch time ÷ duration, max 100% | `ProgressService` | T, S |
| BR-11 | Booking links a valid customer and class | `Booking` entity | T |
| BR-12 | No booking at full capacity | `BookingService` | T, S |
| BR-13 | Booking does not need a subscription | `BookingService` (no entitlement check) | T |
| BR-14 | Only valid bookings in the upcoming list | list filters on BOOKED and future | T, S |
| BR-15 | Admin functions only for ADMIN | `AdminController` role policy | T, S |

## Non-functional requirements
| Area | Requirement | How it is met |
|---|---|---|
| Usability | simple, intuitive, clear navigation and feedback | bottom bar (side rail on tablets), consistent components, empty/loading/error states, confirmations (`TEST-REPORT.md`) |
| Security | protect accounts and personal data | PBKDF2 hashing, JWT with security stamp, role checks, rate limiting, HTTPS only, signed video links, encrypted token storage, secrets outside git, password reset by emailed code, account deletion and data export |
| Performance | reasonable response, avoid unnecessary data use | lazy lists, debounced search, small payloads, load and memory figures in the stack test; cold start still to be measured on a phone |
| Availability | available when infrastructure is | health endpoint, Always On, hosted on Azure App Service |
| Scalability | support growth | stateless API, PostgreSQL, sizing numbers from the stack test |
| Maintainability | separation of concerns, modular | services/controllers/DTOs on the API; screens, view models, repository, API interface on the app; CI on every change |
| Reliability | reliable payments, subscriptions, bookings, clear failures | idempotent payment notifications, unique-booking index, PayFast cancel before local cancel, error log with admin alerts |
| Accessibility | readable text, contrast, clear controls, consistent navigation | 4.5:1 contrast, large-text layouts, labels, 48dp targets, keyboard keys (live TalkBack check still to do) |
| Compatibility | supported Android versions | minSdk 26 (Android 8.0), target 35; tested on API 37 emulator |
| Privacy | collect only what is needed, handle securely | minimal data, data export and deletion in Settings, client's privacy policy linked |

## Where the delivered solution differs from the Task 1 plan (explain in the presentation)
| Task 1 plan | Delivered | Why |
|---|---|---|
| Hosting on AWS (RDS, S3/CloudFront, ECS Fargate, ECR, CloudWatch) | Azure App Service + Azure Database for PostgreSQL, deployed by GitHub Actions; Docker image and Compose stack also provided | the group's cloud access is an Azure for Students account |
| Staging environment before production | a throw-away "stack test" (real container + PostgreSQL) runs the full smoke test in CI before anything is deployed | cheaper than a permanent staging environment, same safety |
| E-commerce Shop out of scope | a "coming soon" Shop entry from the client's wireframes | kept as a placeholder only; nothing is sold in the app |
| Monitoring with CloudWatch | health check, in-app admin error log with e-mail alerts; Azure monitoring can be added | covers the failure-handling intent |
| Database kept private (RDS) | Azure PostgreSQL reachable only with its password and by Azure services | tightening the firewall to the web app's addresses is a go-live item |
