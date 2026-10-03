# Azure setup – instructions for the group member who has Azure access

**Goal:** host the Rhythm & Flow API and its database on the campus Azure subscription so the demo (and the rubric's "Hosting" section) works without anyone's laptop.
**Time needed:** about 30–45 minutes. Nothing here is permanent; everything lives in one resource group that can be deleted in one click afterwards.
**Please do the steps in order and tick them off.** If any step shows an error, take a screenshot and send it to Samir.

> Keep passwords and keys out of chat and screenshots. Type them straight into the Azure portal / GitHub pages named below.

---

## Part 0 – Check you have access
1. Go to **https://portal.azure.com** and sign in with the campus account.
2. Search for **Subscriptions** (top search bar). You should see at least one subscription with status *Active*.
   - If the list is empty, or it says you cannot create resources: stop and tell Samir (we need the lecturer/IT to grant access, or we use a different plan).

## Notes for an **Azure for Students** subscription (read before Part 1)
Student subscriptions have extra limits. None of them is a dead end – use the workaround:
- **Region is restricted.** If the portal says a region is "not available", "restricted" or shows `RequestDisallowedByAzure`, pick **any region the dropdown lets you select**. Closest to South Africa first: *South Africa North → UK South → West Europe → North Europe → Germany West Central → Sweden Central*. Use the **same region for the database and the web app**. Latency is not a problem for the demo.
- **Basic (B1) web apps can show "no quota/capacity" in some regions.** Try another allowed region. Only as a last resort use the *Free F1* plan (it sleeps and is slow – tell Samir first).
- **The credit is US$100 and nothing bills a card.** Check it under *Cost Management → Credits*. Expected cost for this setup: roughly US$20–30 per month (database B1ms ≈ US$13, web app B1 ≈ US$13), so it comfortably covers the demo. When the credit ends the resources stop; nothing is charged.
- **If .NET 10 is not in the runtime list** (Part 3), or the database cannot be created in any allowed region: stop and tell Samir. There are two fallbacks (a small Azure virtual machine running the same stack, or deploying the app as a container) – do **not** pick an older .NET version.
- **Who adds GitHub secrets:** only the repository *owner* (Samir) can. You send him the publish profile (Part 5, step 1) privately and the app name; he adds them. You do not need to touch GitHub settings.
- **Never put passwords in chat messages, screenshots or GitHub.** The repository is **public**.

## Part 1 – Resource group
1. Search **Resource groups** → **Create**.
2. Subscription: the campus one. Name: `rg-rhythmflow`. Region: **South Africa North** (if it isn't allowed, choose **West Europe**; use the *same region for everything below*).
3. **Review + create** → **Create**.

## Part 2 – Database (PostgreSQL)
1. Search **Azure Database for PostgreSQL flexible servers** → **Create** → *Flexible server*.
2. Resource group `rg-rhythmflow`. Server name: `rhythmflow-db-<something unique>` (lowercase, e.g. `rhythmflow-db-g11`). Same region as above.
3. PostgreSQL version: the newest offered (16 or higher is fine).
4. Workload type: **Development**. Compute: **Burstable, B1ms** (cheapest). Storage: 32 GiB is fine.
5. Authentication: **PostgreSQL authentication only**. Admin username: `rfadmin`. Password: make a strong one and **save it somewhere private** (you will need it in Part 4).
6. **Networking** tab: *Public access*. Tick **Allow public access from any Azure service within Azure to this server**. (Do not tick "add current client IP" unless you want to connect from your own PC.)
7. **Review + create** → **Create** (takes a few minutes).
8. When it is ready, open the server → **Databases** → **Add** → name: `rhythmflow` → **Save**.
9. Open **Overview** and copy the **Server name** (it ends in `.postgres.database.azure.com`). You need it in Part 4.

## Part 3 – The API host (App Service)
1. Search **App Services** → **Create** → *Web App*.
2. Resource group `rg-rhythmflow`. Name: `rhythmflow-api-<something unique>` (this becomes the web address `https://rhythmflow-api-xxxx.azurewebsites.net`). **Write the exact name down.**
3. Publish: **Code**. Runtime stack: **.NET 10 (LTS)**. Operating System: **Linux**. Region: same as before.
   - If ".NET 10" is not in the list, tell Samir (we will switch the build to match what is available).
4. Pricing plan: **Basic B1** (Linux). Do **not** use the Free plan (it sleeps and is too slow for a demo).
5. **Review + create** → **Create**.
6. When ready, open the app → **Settings → Configuration → General settings** and set:
   - **HTTPS Only**: On
   - **Always On**: On
   - **SCM Basic Auth Publishing Credentials**: **On** (needed so GitHub can deploy). Save.
7. **Monitoring → Health check**: enable it, path `/health`. Save.

## Part 4 – Settings for the API (Environment variables)
Open the App Service → **Settings → Environment variables** (older portals: *Configuration → Application settings*) → **Add** each name/value below → **Apply** → **Confirm** (the app restarts).

| Name | Value |
|---|---|
| `Database__Provider` | `Postgres` |
| `ConnectionStrings__Default` | `Host=<server name from Part 2>;Port=5432;Database=rhythmflow;Username=rfadmin;Password=<db password>;SSL Mode=Require;Trust Server Certificate=true` |
| `Jwt__Key` | a random secret, 48+ characters (see tip below) |
| `Media__SigningKey` | a *different* random secret, 48+ characters |
| `PayFast__Sandbox` | `true` |
| `PayFast__MerchantId` | from Samir's `appsettings.Local.json` (PayFast section) |
| `PayFast__MerchantKey` | same file |
| `PayFast__Passphrase` | same file |
| `PayFast__PublicBaseUrl` | `https://<your app name>.azurewebsites.net` (no slash at the end) |
| `PayFast__ValidateWithServer` | `true` |
| `Smtp__Host` | `smtp.gmail.com` |
| `Smtp__Port` | `587` |
| `Smtp__EnableSsl` | `true` |
| `Smtp__User` | the Gmail address (Samir's `appsettings.Local.json`, Smtp section) |
| `Smtp__Password` | the Gmail app password (same file) |
| `Smtp__FromAddress` | the Gmail address |
| `Admin__AlertEmails__0` | the Gmail address (error alerts go here) |
| `Seed__Users__0__FullName` | `Rhythm Admin` |
| `Seed__Users__0__Username` | `admin` |
| `Seed__Users__0__Email` | `admin@rhythmandflow.test` |
| `Seed__Users__0__Password` | choose a strong password (tell Samir privately) |
| `Seed__Users__0__Role` | `ADMIN` |
| `Seed__Users__1__FullName` | `Alex Demo` |
| `Seed__Users__1__Username` | `alex` |
| `Seed__Users__1__Email` | `alex@rhythmandflow.test` |
| `Seed__Users__1__Password` | choose a strong password (tell Samir privately) |
| `Seed__Users__1__Role` | `CUSTOMER` |

**Tip – make a random secret:** open PowerShell and run
`[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))`
Run it twice (one for `Jwt__Key`, one for `Media__SigningKey`). Do not reuse the same value.

Tick *"Deployment slot setting"* on none of them. Mark the secret ones as normal settings (they are hidden after saving).

## Part 5 – Let GitHub deploy to this app
1. In the App Service **Overview**, click **Download publish profile**. A file `…PublishSettings` downloads. Open it in Notepad and copy **all** the text.
2. Go to the GitHub repository **https://github.com/samirm91011/rythmflowdev** (you need to be added as a collaborator by Samir, with *Write* or *Admin* access, or Samir does this step).
3. **Settings → Secrets and variables → Actions → Secrets → New repository secret**
   - Name: `AZURE_WEBAPP_PUBLISH_PROFILE`  Value: paste the text you copied. Save.
4. Same page → **Variables** tab → **New repository variable**, add three:
   - `DEPLOY_TARGET` = `azure`  (this switches Azure deployment on; without it nothing is deployed)
   - `AZURE_WEBAPP_NAME` = the exact app name from Part 3 (just the name, e.g. `rhythmflow-api-g11`)
   - `AZURE_API_URL` = `https://<app name>.azurewebsites.net` (no slash at the end)
5. **Settings → Environments → New environment** → name `production` → Save (optional: add Samir as a required reviewer).
6. Delete the downloaded publish-profile file from your PC afterwards.

## Part 6 – Check it
1. Samir (or you) opens the repository → **Actions → Deploy → Run workflow** (branch `main`).
2. When it finishes (green tick, ~5–8 minutes), open `https://<app name>.azurewebsites.net/health` in a browser. You should see `{"status":"ok", …}`.
3. If it is red, click the failed step and send Samir a screenshot of the error.

## Tell Samir when done (send privately, not in public chat)
- The app name and web address
- That Parts 1–6 are complete (or the screenshot of any error)
- The admin and demo passwords you chose in Part 4

## After the presentation
To stop all charges: delete the resource group `rg-rhythmflow` (Resource groups → rg-rhythmflow → Delete). Samir keeps a copy of the code on GitHub.
