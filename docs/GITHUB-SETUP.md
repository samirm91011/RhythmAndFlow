# GitHub workflow (rubric §9.4.4)

Repository: https://github.com/samirm91011/RhythmAndFlow

## Branches
| Branch | Purpose |
|---|---|
| `main` | Release. Merging here **deploys** the API to Azure and builds a demo APK. |
| `develop` | Integration branch. Pull requests from features land here first. |
| `feature/<name>` | One branch per piece of work (e.g. `feature/password-reset`). Merge to `develop` by pull request. |

Flow: `feature/*` → pull request → CI passes → merge to `develop` → pull request → CI passes → merge to `main` → deploy.

## Workflows (`.github/workflows`)
| File | Runs when | Does |
|---|---|---|
| `ci.yml` | pull request to `develop`/`main`; push to `develop` or `feature/**` | Builds the API and runs the xUnit tests; builds the Android app, runs its unit tests and lint |
| `deploy.yml` | push to `main`, or run by hand | Builds + tests the API, deploys it to Azure App Service, waits for `/health`, then builds a demo APK that points at the live API |

## One-time repository settings (repository owner, in the GitHub website)
1. **Settings → Branches → Add branch ruleset / protection rule** for `main` (and optionally `develop`):
   - Require a pull request before merging
   - Require status checks to pass: select **API build and tests** and **Android build, unit tests and lint**
   - Block force pushes
2. **Settings → Secrets and variables → Actions**: secret `AZURE_WEBAPP_PUBLISH_PROFILE`; variables `AZURE_WEBAPP_NAME`, `AZURE_API_URL` (see `AZURE-SETUP.md`, Part 5).
3. **Settings → Environments**: create `production`.

## Showing it in the presentation
- Show the Actions tab: a green CI run on a pull request, then the Deploy run after merging to `main`.
- Show the branch protection rule and the branch list.
- Show a pull request with the test results.
