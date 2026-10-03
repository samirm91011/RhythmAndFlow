# REMINDERS – things that are temporary and must change

Samir: read this before every demo and before launch. This list is kept up to date; ask "what's on the reminders list?" any time.

## A. We are using TEST details now – swap for the CLIENT's details at launch
| # | Item | Test value now | Change to | Where |
|---|---|---|---|---|
| 1 | PayFast account | Our own **sandbox** merchant | Client's **live** merchant ID/key/passphrase; `Sandbox: false` | `appsettings.Local.json` (local) / Azure App Service settings (hosted) |
| 2 | PayFast notify URL | localhost / hosted test URL | Final HTTPS API address; `ValidateWithServer: true` | same |
| 3 | Email sender | Our Gmail + App Password | Client's mailbox or sending service | `Smtp` section |
| 4 | Admin alert emails | Our email | Client's admin staff | `Admin:AlertEmails` |
| 5 | Videos | Public test clips | Client's videos | Admin tools → Lessons & videos |
| 6 | Plans/prices | R99 / R199 / R299 placeholders | Client's plans | Admin tools → Subscription plans |
| 7 | Classes, studio, capacity, cancel cut-off | Placeholders (20 spots, 2h) | Client's values | Admin tools / `Booking:CancelCutoffHours` |
| 8 | Heading font | Serif stand-in | Amaris (client's licensed files) | `ui/theme/Theme.kt` + `res/font` |
| 9 | Terms / Privacy / Help text | Placeholder text | Client's legal text | Settings screen |
| 10 | Explore articles, affirmations | Placeholder text | Client's content | `JournalExploreScreens.kt` |
| 11 | Demo accounts (alex / admin) | Seeded demo users | **Delete** them; create the client's real admin | database / seed config |
| 12 | `Jwt:Key`, `Media:SigningKey` | Generated demo secrets | New long random secrets in the host's secret store | Azure settings |
| 13 | Debug "Simulate payment" button | Debug builds only | Not present in release builds – confirm | `PlanScreens.kt` |
| 14 | Hosting account | Campus Azure subscription | Client-owned subscription (or confirm who pays) | Azure / AWS |
| 15 | App signing key | Debug key | Release keystore (keep it safe; losing it blocks updates) | Android Studio |
| 16 | Google Play Developer account | none | Client or team pays ~$25 once, only if publishing to Play Store | Play Console |

| 17 | Gmail **app password** was typed in chat on 2026-10-02 | Treat as exposed | After the presentation: revoke it (Google Account → Security → App passwords) and create a new one for the client's mailbox | `appsettings.Local.json` + Azure settings |
| 18 | GitHub repo | `samirm91011/RhythmAndFlow` (Samir's personal account) | Client's/organisation's repo or transfer ownership; remove any group-member access no longer needed | GitHub |
| 19 | Azure resources | **Azure for Students** subscription (team member's account, US$100 credit), resource group `rg-rhythmflow`. Student credit stops when used up/expired and the account is tied to one student | Client-owned paid subscription (or move to the client's chosen host); delete `rg-rhythmflow` after the presentation if not needed | Azure portal |
| 20 | Seeded **placeholder** content (3 plans, 4 programmes, 8 lessons with public test clips, 6 classes) is created automatically on first start in *every* environment | Placeholders | Replace via Admin tools, then remove the sample seeding in `Data/SeedData.cs` before the client's real launch | `SeedData.cs` |
| 21 | Hosted demo users `admin@rhythmandflow.test` / `alex@rhythmandflow.test` | Test accounts | Delete; create the client's real admin only | Azure settings `Seed__Users__*` |
| 22 | `PayFast__Sandbox`, `PayFast__ValidateWithServer`, `PayFast__PublicBaseUrl` on Azure | sandbox / true / test URL | live merchant, `Sandbox=false`, final HTTPS domain | Azure settings |
| 23 | Smart App Control on Samir's laptop blocks local .NET runs | Use hosted API + GitHub Actions | (Optional, permanent) turn off only if Samir decides to | Windows Security |
| 24 | **Hosting host not final.** Campus Azure was withdrawn (2026-10-02). Options: Azure for Students ($100, no card) or Render free | see `docs/HOSTING-OPTIONS.md` | For the real launch pick a paid, always-on host (Render free **sleeps after 15 min and its database expires after 30 days**) | Render/Azure |
| 25 | On Render free, Gmail SMTP is blocked → email goes via **Brevo** web API (`Email__Provider=Brevo`) | Brevo free (300/day), sender `rythmandflow12@gmail.com` | Verify the client's own sender address/domain in Brevo (or switch back to SMTP on a host that allows it) | Brevo + host settings |
| 26 | Public API docs page `/docs` and `/openapi/v1.json` are open to anyone | Handy for the lecturer | Restrict or remove for production if the client prefers | `Program.cs` |
| 27 | Pre-warm the free host before the demo (open `/health` 5+ minutes earlier) | — | — | browser |

| 28 | *(Only if the xneelo backup is used – currently NOT used)* xneelo Cloud instance bills **every hour it exists** (≈R5.20/day incl. disk) | Running for the demo | **Delete the instance AND its volume after the presentation** (or keep it for the client's pilot) | xneelo Cloud console |
| 29 | Server SSH key `xneelo_rhythmflow` (private file on Samir's PC, also stored as GitHub secret `VPS_SSH_KEY`) | Team key | Create a new key pair for the client; remove ours from the server and from GitHub secrets | PC, server, GitHub |
| 30 | HTTPS address `<ip>.sslip.io` (if used) | IP-based demo address | Real domain such as `api.clientdomain.co.za` (A record → server IP), then update `API_DOMAIN`, `PayFast__PublicBaseUrl`, GitHub variable `AZURE_API_URL` and rebuild the app | DNS + server `.env` + GitHub |
| 31 | The server's `/opt/rhythmflow/.env` holds every live secret | Team values | Client's values; keep file mode 600; never copy it into chat/email | Server |
| 32 | Live smoke test creates a throw-away `smoke…@example.com` customer on each deploy | Harmless demo clutter | Remove the smoke step (or add cleanup) before the real launch; delete those accounts | `deploy.yml` / database |

| 33 | **Code lives in Samir's personal private repo** `samirm91011/RhythmAndFlow` for now. The lecturer will provide a **campus repo** to use for submission | Personal private repo | Move to the campus repo: add it as a second remote (`git remote add campus <url>`), push all branches (`git push campus --all`), then **re-create the GitHub Actions secrets/variables and branch rules there** (secrets do NOT travel with the code), re-check the workflows run, and update `AZURE_API_URL`/`VPS_*` settings. Decide which repo is the submission and say so in the presentation | GitHub |

## B. Before every demo
- API reachable (hosted: open `/health`); database reset to demo data; PayFast sandbox login works; email sending works.
- Fresh demo customer account with no subscription (to show the paywall) + one subscribed customer + admin.
- Emulator/phone charged, internet checked, volume on, screen-sleep off.
- Known-good build frozen; do **not** change code on the day.

## C. Open decisions / waiting on someone
- **Presentation: Wednesday 7 October 2026** (confirmed).
- Azure: group member to follow `docs/AZURE-SETUP.md` and report back. (Samir)
- GitHub: repo `samirm91011/RhythmAndFlow` given; code is pushed to GitHub only when Samir says "go". Repo owner must also add the secrets/variables and branch rules (`docs/GITHUB-SETUP.md`). (Samir)
- Client answers: videos, pricing, classes, legal text, fonts, her PayFast account.
| 34 | **Terms of Use and Privacy Policy** are the client's own pages on her website: https://rhythmandflow.co.za/terms-of-use/ and https://rhythmandflow.co.za/privacy-policy/. The app (sign-up screen and Settings) links to them; the API's `/terms` and `/privacy` redirect there | Client's real pages | If the website addresses change, update `LegalLinks` in `app/.../ui/components/Legal.kt` and the API settings `Legal__TermsUrl` / `Legal__PrivacyUrl`. Check the pages mention the app, subscriptions (PayFast) and POPIA rights (data export and account deletion) | Before launch |
| 35 | Heading font is Cormorant Garamond, a stand-in for the brand's Amaris (files not supplied) | Stand-in | Add the Amaris font file to `app/src/main/res/font/` and change `HeadingFont` in `Theme.kt` | Before launch |
| 36 | Demo mode build (`-PdemoMode=true`) uses a fake in-app server for screenshots and tests | Off in normal builds | Never ship an APK built with demoMode | Release |
| 37 | **Firebase project (push notifications) is the team's test project**, created under the team Gmail. Two private files: `app/google-services.json` and `backend/RhythmFlow.Api/firebase-service-account.json` (both git-ignored). In Azure the key goes in the app setting `Firebase__ServiceAccountJson` | Team test project | Client creates (or is given ownership of) their own Firebase project; replace both files and the Azure setting; rebuild the app. Delete the old service-account key in the Firebase console | Go-live |
