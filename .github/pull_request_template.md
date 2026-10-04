## What this changes
<!-- One or two sentences: what and why. -->

## How it was checked
- [ ] Backend tests pass (`dotnet test backend/RhythmFlow.sln`) - or CI is green
- [ ] App tests and lint pass (`gradlew :app:testDebugUnitTest :app:lintDebug`) - or CI is green
- [ ] Tried on the emulator or a phone (screens affected: )
- [ ] If the database layout changed: the Azure database must be reset (tell the group member who runs Azure)

## Safe to merge
- [ ] No passwords, keys, `.env`, `google-services.json` or `appsettings.*.json` files in this change
- [ ] Docs updated where behaviour changed (`docs/`)
