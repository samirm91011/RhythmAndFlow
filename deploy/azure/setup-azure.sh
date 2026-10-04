#!/usr/bin/env bash
# Rhythm & Flow - creates the Azure resources from docs/AZURE-SETUP.md (Parts 1-4) in one go.
#
# OPTIONAL FAST PATH. It has not been run against a real Azure subscription yet. If anything fails, stop, send the
# message to Samir, and carry on with the click-by-click steps in docs/AZURE-SETUP.md instead (nothing here is permanent:
# deleting the resource group removes everything).
#
# How to run it (about 10 minutes):
#   1. Open https://portal.azure.com and click the Cloud Shell icon (>_) at the top. Choose Bash.
#   2. Upload two files with the Upload button: this script, and rf-azure-secrets.env (Samir sends it privately).
#      If Samir also sends firebase-service-account.json, upload that too (push notifications; optional).
#   3. Run:   SUFFIX=g11 bash setup-azure.sh
#      (SUFFIX is a short lowercase word to make the names unique, for example the group number.)
#      If your region is restricted, add LOCATION=westeurope (or uksouth, northeurope, germanywestcentral) in front.
#   4. When it finishes it prints the web address and writes rf-publish-profile.xml. Send Samir the address and the
#      contents of rf-publish-profile.xml privately (never in a public chat or screenshot).

set -euo pipefail

SUFFIX="${SUFFIX:?Set SUFFIX first, for example:  SUFFIX=g11 bash setup-azure.sh}"
LOCATION="${LOCATION:-southafricanorth}"
RG="rg-rhythmflow"
DB="rhythmflow-db-${SUFFIX}"
APP="rhythmflow-api-${SUFFIX}"
PLAN="plan-rhythmflow"
SECRETS_FILE="${SECRETS_FILE:-rf-azure-secrets.env}"
FIREBASE_FILE="${FIREBASE_FILE:-firebase-service-account.json}"

say() { printf '\n== %s\n' "$*"; }
need() { command -v "$1" >/dev/null 2>&1 || { echo "Missing tool: $1 (Azure Cloud Shell has it; run this script there)."; exit 1; }; }
need az; need jq; need openssl

[ -f "$SECRETS_FILE" ] || { echo "Cannot find $SECRETS_FILE. Upload it to Cloud Shell first (Samir sends it privately)."; exit 1; }
# shellcheck disable=SC1090
set -a; source "$SECRETS_FILE"; set +a
for v in PAYFAST_MERCHANT_ID PAYFAST_MERCHANT_KEY PAYFAST_PASSPHRASE SMTP_USER SMTP_PASSWORD ADMIN_PASSWORD DEMO_PASSWORD; do
  [ -n "${!v:-}" ] || { echo "$SECRETS_FILE is missing $v"; exit 1; }
done

say "Checking you are signed in"
az account show --query "{subscription:name, state:state}" -o table

say "1/6 Resource group $RG in $LOCATION"
az group create -n "$RG" -l "$LOCATION" -o none

say "2/6 Database server $DB (about 5 minutes)"
DB_PASSWORD="$(openssl rand -base64 36 | tr -dc 'A-Za-z0-9' | head -c 28)Aa1"
az postgres flexible-server create -g "$RG" -n "$DB" -l "$LOCATION" \
  --admin-user rfadmin --admin-password "$DB_PASSWORD" \
  --tier Burstable --sku-name Standard_B1ms --storage-size 32 --version 16 \
  --public-access 0.0.0.0 --yes -o none
az postgres flexible-server db create -g "$RG" -s "$DB" -d rhythmflow -o none

say "3/6 Web app $APP"
az appservice plan create -g "$RG" -n "$PLAN" --is-linux --sku B1 -l "$LOCATION" -o none
RUNTIME="$(az webapp list-runtimes --os-type linux -o tsv | grep -i dotnet | grep -E '10(\.0)?$' | head -1 || true)"
if [ -z "$RUNTIME" ]; then
  echo ".NET 10 is not offered in this region/subscription. Stop here and tell Samir (do NOT pick an older .NET)."
  exit 1
fi
echo "Using runtime: $RUNTIME"
az webapp create -g "$RG" -p "$PLAN" -n "$APP" --runtime "$RUNTIME" -o none
az webapp update -g "$RG" -n "$APP" --https-only true -o none
az webapp config set -g "$RG" -n "$APP" --always-on true --generic-configurations '{"healthCheckPath": "/health"}' -o none
# Lets GitHub Actions deploy with the publish profile.
az resource update -g "$RG" -n scm --namespace Microsoft.Web --resource-type basicPublishingCredentialsPolicies \
  --parent "sites/$APP" --set properties.allow=true -o none

say "4/6 Application settings"
HOST="$(az postgres flexible-server show -g "$RG" -n "$DB" --query fullyQualifiedDomainName -o tsv)"
CONN="Host=${HOST};Port=5432;Database=rhythmflow;Username=rfadmin;Password=${DB_PASSWORD};SSL Mode=Require;Trust Server Certificate=true"
JWT_KEY="$(openssl rand -base64 48 | tr -d '\n')"
MEDIA_KEY="$(openssl rand -base64 48 | tr -d '\n')"
URL="https://${APP}.azurewebsites.net"
FIREBASE_JSON=""
if [ -f "$FIREBASE_FILE" ]; then FIREBASE_JSON="$(jq -c . "$FIREBASE_FILE")"; else echo "(No $FIREBASE_FILE uploaded: push notifications stay off; the app still works.)"; fi

jq -n \
  --arg conn "$CONN" --arg jwt "$JWT_KEY" --arg media "$MEDIA_KEY" --arg url "$URL" \
  --arg pfid "$PAYFAST_MERCHANT_ID" --arg pfkey "$PAYFAST_MERCHANT_KEY" --arg pfpass "$PAYFAST_PASSPHRASE" \
  --arg smtpuser "$SMTP_USER" --arg smtppass "$SMTP_PASSWORD" --arg alert "${ADMIN_ALERT_EMAIL:-$SMTP_USER}" \
  --arg fb "$FIREBASE_JSON" --arg adminpw "$ADMIN_PASSWORD" --arg demopw "$DEMO_PASSWORD" '
  [
    {name:"Database__Provider", value:"Postgres"},
    {name:"ConnectionStrings__Default", value:$conn},
    {name:"Jwt__Key", value:$jwt},
    {name:"Media__SigningKey", value:$media},
    {name:"PayFast__Sandbox", value:"true"},
    {name:"PayFast__MerchantId", value:$pfid},
    {name:"PayFast__MerchantKey", value:$pfkey},
    {name:"PayFast__Passphrase", value:$pfpass},
    {name:"PayFast__PublicBaseUrl", value:$url},
    {name:"PayFast__ValidateWithServer", value:"true"},
    {name:"Smtp__Host", value:"smtp.gmail.com"},
    {name:"Smtp__Port", value:"587"},
    {name:"Smtp__EnableSsl", value:"true"},
    {name:"Smtp__User", value:$smtpuser},
    {name:"Smtp__Password", value:$smtppass},
    {name:"Smtp__FromAddress", value:$smtpuser},
    {name:"Admin__AlertEmails__0", value:$alert},
    {name:"Seed__Users__0__FullName", value:"Rhythm Admin"},
    {name:"Seed__Users__0__Username", value:"admin"},
    {name:"Seed__Users__0__Email", value:"admin@rhythmandflow.test"},
    {name:"Seed__Users__0__Password", value:$adminpw},
    {name:"Seed__Users__0__Role", value:"ADMIN"},
    {name:"Seed__Users__1__FullName", value:"Alex Demo"},
    {name:"Seed__Users__1__Username", value:"alex"},
    {name:"Seed__Users__1__Email", value:"alex@rhythmandflow.test"},
    {name:"Seed__Users__1__Password", value:$demopw},
    {name:"Seed__Users__1__Role", value:"CUSTOMER"}
  ] + (if $fb == "" then [] else [{name:"Firebase__ServiceAccountJson", value:$fb}] end)
  | map(. + {slotSetting:false})' > rf-settings.json
az webapp config appsettings set -g "$RG" -n "$APP" --settings @rf-settings.json -o none
rm -f rf-settings.json

say "5/6 Publish profile (lets GitHub deploy to this app)"
az webapp deployment list-publishing-profiles -g "$RG" -n "$APP" --xml > rf-publish-profile.xml

say "6/6 Done"
cat <<EOF

Web address (send to Samir):   $URL
Web app name (send to Samir):  $APP
Publish profile:               open the file rf-publish-profile.xml (Cloud Shell: Manage files > Download) and send
                               its contents to Samir PRIVATELY.  Then delete the file:  rm rf-publish-profile.xml

The app has no code yet: Samir (or you) now runs GitHub > Actions > Deploy > Run workflow. Afterwards open
$URL/health  - it should show {"status":"ok", ...}.

To remove everything later:    az group delete -n $RG --yes
EOF
