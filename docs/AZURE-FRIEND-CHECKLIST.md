# Azure checklist for the group member (kept up to date as the project moves)

This is the short, ordered version. The full click-by-click steps are in [`AZURE-SETUP.md`](AZURE-SETUP.md) (part numbers below point there).
Presentation: **Wednesday 7 October 2026.** Please aim to finish "Do now" by **Sunday 4 October** so there is time to fix surprises.

> Never post passwords, keys, publish profiles or `.json` key files in chat, e-mail, screenshots or GitHub. Send them to Samir privately, one at a time, and only when he asks.

## A. Do now (nothing here depends on new code)
- [ ] **A1. Check access** (Part 0): sign in at portal.azure.com, confirm a subscription shows *Active*. Tell Samir at once if it doesn't.
- [ ] **A2. Resource group** (Part 1): `rg-rhythmflow`.
- [ ] **A3. Database** (Part 2): PostgreSQL flexible server, Burstable B1ms, admin `rfadmin`, allow access from Azure services, create the database `rhythmflow`. **Save the database password privately.**
- [ ] **A4. Web app** (Part 3): Linux, **.NET 10**, Basic B1. Turn on HTTPS Only, Always On, SCM Basic Auth Publishing Credentials. Health check path `/health`. **Write down the exact app name.**
  - If ".NET 10" is not offered, or no region allows the database or the web app: stop and tell Samir. Do not pick an older .NET.
- [ ] **A5. Settings** (Part 4): add every environment variable in the table, **including the newer one** `Firebase__ServiceAccountJson` (see B1). Leave a value out only if Samir says to.

## A+. Optional fast path: one script instead of A2-A5 (about 10 minutes)
Instead of clicking through the portal, you can create the resource group, database, web app and every setting with one script in **Azure Cloud Shell**. It has not been tried on a real subscription yet, so if it stops with an error, send the message to Samir and use A2-A5 instead.
- [ ] Samir sends you two files privately: `rf-azure-secrets.env` (and `firebase-service-account.json` if he has it).
- [ ] portal.azure.com -> Cloud Shell icon (>_) -> **Bash**.
- [ ] Use the **Upload** button to upload `setup-azure.sh` (from the repository folder `deploy/azure`), `rf-azure-secrets.env` and the Firebase file.
- [ ] Run: `SUFFIX=g11 bash setup-azure.sh` (put a short lowercase word of your choice instead of g11; add `LOCATION=westeurope` in front if your region is restricted).
- [ ] It prints the web address and writes `rf-publish-profile.xml`. Send Samir the address and the contents of that file privately, then delete the file (`rm rf-publish-profile.xml`).

## B. Things Samir sends you privately (wait for his message)
- [ ] **B1. `Firebase__ServiceAccountJson`**: the whole contents of the Firebase key file, pasted into one setting value. Needed for instant push notifications. Without it the app still works.
- [ ] **B2. PayFast and Gmail values** for Part 4 (from Samir's private settings file).

## C. Things you send Samir privately
- [ ] **C1. The app name and web address**, for example `rhythmflow-api-g11` and `https://rhythmflow-api-g11.azurewebsites.net`.
- [ ] **C2. The publish profile** (App Service → Overview → *Download publish profile*; send the file's contents). Delete the downloaded file from your PC afterwards.
- [ ] **C3. The admin and demo passwords** you chose in Part 4.

## D. Samir does these on GitHub (the repository owner), then you check
- [ ] **D1.** Repository `samirm91011/RhythmAndFlow` (private). You are a collaborator.
- [ ] **D2.** Secret `AZURE_WEBAPP_PUBLISH_PROFILE`; variables `DEPLOY_TARGET=azure`, `AZURE_WEBAPP_NAME`, `AZURE_API_URL`; environment `production` (Part 5).
- [ ] **D3.** Branch rules for `main` (see `GITHUB-SETUP.md`).

## E. First deploy and check (Part 6) — only when Samir says "go"
> **Do not deploy before Samir says the pull requests are merged into `main`.** The deploy builds whatever is on `main`; until the merge, `main` is missing most of the app.
- [ ] **E1.** GitHub → **Actions → Deploy → Run workflow** (branch `main`).
- [ ] **E2.** When green, open `https://<app name>.azurewebsites.net/health`. Expect `{"status":"ok", …}`.
- [ ] **E3.** Also open `/docs` (API documentation) - it should load. `/terms` and `/privacy` should jump to the Rhythm & Flow website pages (rhythmandflow.co.za/terms-of-use and /privacy-policy).
- [ ] **E4.** If anything is red or blank: screenshot the failed step or page (without secrets in view) and send it to Samir.

## F. Later changes you may be asked to do
- **Database reset (only when Samir says the database layout changed).** The app creates its tables the first time it starts and does not change existing tables. When new tables or columns are added after the first deploy: PostgreSQL server → **Databases** → delete `rhythmflow` → **Add** → `rhythmflow` again → then **restart** the web app (App Service → Overview → Restart). The demo accounts and sample classes are re-created automatically.
- **Redeploy.** Actions → Deploy → Run workflow (branch `main`). Takes 5–8 minutes.
- **Rotate a secret** (for example after the presentation): change the value under Environment variables → Apply → Confirm.
- **Before the presentation:** open the app address 10 minutes early so the server is awake, and confirm `/health`.
- **After the presentation:** delete the resource group `rg-rhythmflow` to stop all charges (only after Samir confirms nothing more is needed).

## G. Changes since `AZURE-SETUP.md` was first written
| Date | Change | What it means for you |
|---|---|---|
| 3 Oct | The repository is now **private** and has a new name, `RhythmAndFlow`. | Use the new address. You need to be a collaborator (Samir has added you). |
| 3 Oct | New app setting `Firebase__ServiceAccountJson`. | Add it (A5/B1). |
| 3 Oct | New database table for push notification phones. | Nothing to do. The server now adds this table itself when it starts (even on a database created by an earlier version), so **no database reset is needed**. |
| 3 Oct | Account deletion/data export added to the API, and `/terms` and `/privacy` now redirect to the client's website pages. | Check E3 after deploying. Optional settings `Legal__TermsUrl` and `Legal__PrivacyUrl` exist but are not needed. |
| 4 Oct | New optional script `deploy/azure/setup-azure.sh` (see A+). New optional setting `Seed__KeepSampleClassesUpcoming` (on by default). More API endpoints: payment history, customers, programmes. | Nothing extra to add. After deploying, the live check script (run by Samir) also covers the new endpoints. |
