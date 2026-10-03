# Switching from test details to the client's live details

Everything that differs between "our testing" and "the client's launch" is in configuration, not code.

| What | Where | Test (now) | Live (client) |
|---|---|---|---|
| PayFast merchant ID / key / passphrase | `backend/RhythmFlow.Api/appsettings.Local.json` → `PayFast` | Team's sandbox account | Client's live merchant; set `Sandbox` to `false` |
| PayFast notification URL | `PayFast:PublicBaseUrl` | `http://localhost:5080` (or ngrok URL) | The deployed API's HTTPS address; set `ValidateWithServer` to `true` |
| Email sender | `Smtp` section | Team Gmail + App Password | Client's Gmail/domain mailbox or a sending service |
| Admin alert recipients | `Admin:AlertEmails` | Team email | Client's staff emails |
| API address in the app | `app/build.gradle.kts` → `API_BASE_URL` (release block) | `http://10.0.2.2:5080/` (emulator) | `https://` address of the deployed API |
| Signing secrets | `Jwt:Key`, `Media:SigningKey` | Generated demo values | New long random values stored as environment variables / secret store |
| Database | `Database:Provider`, `ConnectionStrings:Default` | SQLite file | PostgreSQL |
| Videos | Admin tools → Lessons & videos | Public test clips | Client's videos |
| Plans, classes, copy | Admin tools / seed data | Placeholders | Client's real values |
| Legal pages | Settings → Terms / Privacy | Placeholder text | Client's documents |

Checklist before launch: remove the debug "Simulate payment" button path (it only exists in debug builds), turn on HTTPS, rotate every secret, confirm PayFast recurring billing is enabled on the live account, create a release-signed build.
