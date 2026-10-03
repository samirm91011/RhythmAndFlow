# Test report

Last updated: 3 October 2026. Three layers of testing are used; each covers what the others cannot.

## 1. Automated tests (run on every push by GitHub Actions)
| Where | What | Count |
|---|---|---|
| `backend/RhythmFlow.Api.Tests` (xUnit, real database engine) | progress and access rules, bookings, PayFast signing and encoding, signed video links, password reset, subscription payments and cancel, account deletion and data export, push notifications | about 65 |
| `app/src/test` (JUnit) | server error messages, date/time and money formatting | 17 |
| `deploy/smoke-test.sh` (against a real API + PostgreSQL in Docker) | the full customer and admin journey through the live HTTP API, plus a load test | 61 checks |

Note: the backend tests cannot run on the developer laptop (Windows Smart App Control blocks freshly built .NET programs), so they are run by GitHub Actions only.

## 2. Manual screen-by-screen walkthrough (Android emulator, demo mode with sample data)
Checked on a phone-size screen at normal text size unless noted.

| Area | Result |
|---|---|
| Welcome, log in, create account | pass |
| Forgot password: send code, code + new password, resend countdown, back to log in | pass |
| Home (mood check-in, next class, rhythm cards, stats, banner) | pass (banner corners and padding fixed) |
| Move list, filters, search; lesson detail; Begin Practice | pass |
| Video player | screenshots and recording are blocked as designed; **playback itself could not be verified on the emulator** (no route to the sample video host from this PC). A clear error with a Retry button now shows, and failures are reported to the admin error log. To be verified on a real phone against the hosted API |
| Classes: book, "Booked" state, My bookings, cancel booking (with confirmation) | pass |
| Subscribe (opens PayFast in the browser) and return: "You're subscribed!" | pass (a bug where the You tab brought this screen back was found and fixed) |
| Subscription screen and cancel flow (confirmation, PayFast message, access kept until period end) | pass |
| Journal tabs, Affirmations, Explore (back arrow added), Notifications (bell, unread, mark read) | pass |
| You, Settings, Edit profile, Change password | pass |
| Settings: Privacy Policy and Terms of Use (open the client's website pages), Download my data (share sheet), Delete my account (wrong password refused); sign-up screen shows the Terms/Privacy notice with links | pass (link target pages themselves not opened: emulator has no route to the internet) |
| Admin: dashboard, error log, lessons, classes, plans | pass |
| Log out | pass |

### Layout and accessibility
| Check | Result |
|---|---|
| Tablet size (about 1600 x 2560): side navigation rail, centred readable column | pass on Home and Classes; other screens use the same layout |
| Maximum text size (2x): bottom bar labels and mood tiles broke mid-word | **fixed**; Move, Journal and Classes reflow without clipping (tab rows scroll) |
| Screen-reader labels, headings, 4.5:1 text contrast on orange text and buttons | reviewed and corrected in code |
| TalkBack (screen reader) walkthrough, keyboard navigation of forms | **not yet done** |

## 3. Not testable until the hosted API exists (Azure)
- Real PayFast sandbox payment with the confirmation (ITN) call-back
- Real e-mails (password reset, admin error alerts)
- Push notifications arriving on a phone (needs the Firebase key on the server)
- Video playback through the API's protected proxy
- Running the smoke test against the live address

## Known limits
- Demo mode (`-PdemoMode=true`) uses fake data and is for screenshots and checks only. Never ship it.
- Class scheduling in the admin screen takes the date as text (yyyy-mm-dd); a date picker would be nicer.
