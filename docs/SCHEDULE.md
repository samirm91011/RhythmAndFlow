# Schedule to the presentation – Wednesday 7 October 2026

Updated as things move. ✔ = done, ◐ = in progress, ☐ = to do. **Items marked 👤 need Samir or a group member.**

## Fri 2 Oct (today)
- ✔ PayFast sandbox details verified; Gmail details added
- ✔ Backend: password reset, notifications, cancel subscription, error logging + admin alerts, security hardening
- ✔ Automated tests written (run on GitHub, not on this PC – Smart App Control)
- ✔ GitHub workflows (CI + deploy) and Azure instructions written
- 👤 Samir: give `docs/AZURE-SETUP.md` to the group member; reply "go" so the code can be pushed to GitHub
- ◐ Android: reset/notifications/cancel/error-report screens

## Sat 3 Oct
- ✔ Hosting decided: **Azure for Students** (team member's account) – follow `docs/AZURE-SETUP.md`. The team member creates the Azure resources; Samir adds the GitHub secrets/variables. (xneelo = backup only, nothing bought.)
- ◐ Stack test (real container + PostgreSQL) – all feature checks pass except fixes in progress; load/memory numbers pending
- (earlier note, superseded) **Hosting options:** try Azure for Students (azure.microsoft.com/free/students). If it fails within ~15 min, use Render + Brevo (`docs/HOSTING-OPTIONS.md`). Hosting is the critical path – everything else needs the live API.
- 👤 Group member completes the hosting setup (Azure Parts 1–6, or the Render steps)
- ☐ First deploy to Azure; verify `/health`, login, video playback, real sandbox payment end to end on the hosted API
- ☐ Android: notifications + class reminders, admin error log screen

## Sun 4 Oct
- ☐ Visual refresh (elegant, same brand), tablet layout, accessibility pass (screen reader, text size, contrast)
- ☐ Admin + customer flows complete on hosted API

## Mon 5 Oct
- ☐ Full screen-by-screen test on phone-size and tablet-size emulator; fix everything found
- ☐ Presentation deck + demo script drafted

## Tue 6 Oct
- ☐ **Code freeze**: known-good build tagged; demo APK installed on emulator/phone; demo data reset
- ☐ Full rehearsal (with timings); backup screen recording of the whole demo in case of network trouble

## Wed 7 Oct – presentation
- Before: open `/health`, log in as admin + customer, confirm email works, charge devices, check internet
- Do not change code on the day
