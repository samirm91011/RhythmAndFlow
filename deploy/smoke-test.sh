#!/usr/bin/env bash
# End-to-end check of a running Rhythm & Flow API.
#
#   deploy/smoke-test.sh https://api.example.com            (live server: only safe, non-dev checks)
#   deploy/smoke-test.sh http://localhost:8080 --dev         (test stack: also pays with the simulator, cancels, forces an error)
#
# Optional environment variables:
#   ADMIN_EMAIL, ADMIN_PASSWORD   enable the admin checks
#   RESET_CODE_CMD                a shell command that prints the latest 6-digit reset code (reads it from the API log)
# Exit code is 0 only when every check passes.
set -uo pipefail

BASE="${1:?usage: smoke-test.sh <base-url> [--dev]}"
BASE="${BASE%/}"
DEV=0; [ "${2:-}" = "--dev" ] && DEV=1
PASS=0; FAIL=0
BODY="$(mktemp)"; trap 'rm -f "$BODY"' EXIT

ok()  { echo "  PASS  $1"; PASS=$((PASS+1)); }
bad() {
  echo "  FAIL  $1  -> $2"; FAIL=$((FAIL+1))
  # On GitHub, also post the failure as an annotation so it is visible on the run page without opening the log.
  if [ -n "${GITHUB_ACTIONS:-}" ]; then echo "::error title=Smoke check failed::$1 -> $2"; fi
}
eq()  { if [ "$2" = "$3" ]; then ok "$1"; else bad "$1" "expected '$2', got '$3'"; fi; }
ge()  { if [ "${3:-0}" -ge "$2" ] 2>/dev/null; then ok "$1"; else bad "$1" "expected >= $2, got '${3:-}'"; fi; }
has() { if printf '%s' "$3" | grep -qi -- "$2"; then ok "$1"; else bad "$1" "'$2' not found"; fi; }

# call METHOD PATH [TOKEN] [JSON]  -> prints HTTP status; body in $BODY
call() {
  local m="$1" p="$2" t="${3:-}" d="${4:-}"
  local args=(-sS -m 40 -o "$BODY" -w '%{http_code}' -X "$m")   # separate line: $m must exist before it is used here
  [ -n "$t" ] && args+=(-H "Authorization: Bearer $t")
  if [ -n "$d" ]; then args+=(-H 'Content-Type: application/json' -d "$d"); fi
  curl "${args[@]}" "$BASE$p" 2>/dev/null || echo "000"
}
field() { jq -r "$1" "$BODY" 2>/dev/null; }

STAMP="$(date +%s)"
EMAIL="smoke$STAMP@example.com"; USERN="smoke$STAMP"; PASSWORD="Smoke-Test-$STAMP!"
CURPW="$PASSWORD"      # the customer's current password (changes if the reset check runs)

echo "== Service =="
eq "health endpoint answers 200" 200 "$(call GET /health)"
has "health says ok" '"status":"ok"' "$(cat "$BODY")"
# /docs redirects to /docs/v1 (the interactive documentation page), so follow the redirect
eq "API documentation page (/docs) loads" 200 "$(curl -sS -m 30 -L -o /dev/null -w '%{http_code}' "$BASE/docs" 2>/dev/null || echo 000)"
eq "OpenAPI document answers 200" 200 "$(call GET /openapi/v1.json)"

echo "== Plans =="
eq "plans list answers 200" 200 "$(call GET /api/plans)"
eq "three plans are offered" 3 "$(field 'length')"

echo "== Accounts =="
eq "register a new customer" 200 "$(call POST /api/auth/register '' "{\"fullName\":\"Smoke Tester\",\"username\":\"$USERN\",\"email\":\"$EMAIL\",\"password\":\"$PASSWORD\"}")"
TOKEN="$(field '.token')"
eq "registering the same email again is refused" 409 "$(call POST /api/auth/register '' "{\"fullName\":\"Smoke Tester\",\"username\":\"${USERN}b\",\"email\":\"$EMAIL\",\"password\":\"$PASSWORD\"}")"
eq "a too-short password is refused" 400 "$(call POST /api/auth/register '' "{\"fullName\":\"Smoke Tester\",\"username\":\"${USERN}c\",\"email\":\"c$EMAIL\",\"password\":\"abc\"}")"
eq "wrong password is refused" 401 "$(call POST /api/auth/login '' "{\"identifier\":\"$USERN\",\"password\":\"wrong-password\"}")"
eq "login with the right password" 200 "$(call POST /api/auth/login '' "{\"identifier\":\"$USERN\",\"password\":\"$PASSWORD\"}")"
TOKEN="$(field '.token')"
eq "profile loads with the token" 200 "$(call GET /api/auth/me "$TOKEN")"
eq "protected route without a token is refused" 401 "$(call GET /api/lessons)"

echo "== Content and paywall =="
eq "lessons list answers 200" 200 "$(call GET /api/lessons "$TOKEN")"
LESSONS="$(cat "$BODY")"
eq "eight lessons are seeded" 8 "$(printf '%s' "$LESSONS" | jq 'length')"
LOCKED_ID="$(printf '%s' "$LESSONS" | jq -r '[.[] | select(.locked)][0].id')"
FREE_ID="$(printf '%s' "$LESSONS" | jq -r '[.[] | select(.locked | not)][0].id')"
ge "some lessons are locked for a customer with no subscription" 1 "$(printf '%s' "$LESSONS" | jq '[.[] | select(.locked)] | length')"
eq "a locked lesson cannot be played (403)" 403 "$(call GET "/api/lessons/$LOCKED_ID/playback" "$TOKEN")"
eq "a free preview lesson can be played" 200 "$(call GET "/api/lessons/$FREE_ID/playback" "$TOKEN")"
PLAY_URL="$(field '.url')"
if [ -n "$PLAY_URL" ] && [ "$PLAY_URL" != "null" ]; then
  # In the test stack the link is built from the request address; rewrite it to the address we are testing.
  PLAY_URL="${BASE}/${PLAY_URL#*://*/}"
  code="$(curl -sS -m 40 -o /dev/null -D /tmp/h.$$ -w '%{http_code}' -H 'Range: bytes=0-99' "$PLAY_URL" 2>/dev/null || echo 000)"
  eq "video streams through the signed link (206 partial content)" 206 "$code"
  has "video response carries a Content-Range" "content-range" "$(cat /tmp/h.$$ 2>/dev/null)"
  # change the last character of the signature (to a different one every time)
  if [ "${PLAY_URL: -1}" = "0" ]; then swap=1; else swap=0; fi
  tampered="${PLAY_URL%?}$swap"
  eq "a tampered video link is rejected (401)" 401 "$(curl -sS -m 20 -o /dev/null -w '%{http_code}' "$tampered" 2>/dev/null || echo 000)"
  rm -f /tmp/h.$$
else
  bad "playback link returned" "no url in response"
fi

echo "== Checkout =="
eq "checkout creates a PayFast link" 200 "$(call POST /api/subscriptions/checkout "$TOKEN" '{"planId":2}')"
SUB_ID="$(field '.subscriptionId')"
has "link points at PayFast" "payfast.co.za" "$(field '.paymentUrl')"

echo "== Payment history, push registration, legal links, data export =="
eq "payment history answers 200" 200 "$(call GET /api/subscriptions/payments "$TOKEN")"
eq "a new customer has no payments yet" 0 "$(field 'length')"
eq "a phone can be registered for push" 200 "$(call POST /api/notifications/device-token "$TOKEN" "{\"token\":\"smoke-device-token-$STAMP-0123456789\"}")"
eq "a too-short push token is refused" 400 "$(call POST /api/notifications/device-token "$TOKEN" '{"token":"short"}')"
eq "the phone can be removed again" 200 "$(call POST /api/notifications/device-token/remove "$TOKEN" "{\"token\":\"smoke-device-token-$STAMP-0123456789\"}")"
eq "/terms sends people to the website" 302 "$(curl -sS -m 20 -o /dev/null -w '%{http_code}' "$BASE/terms" 2>/dev/null || echo 000)"
has "/privacy points at rhythmandflow.co.za" "rhythmandflow.co.za" "$(curl -sS -m 20 -o /dev/null -D - "$BASE/privacy" 2>/dev/null | grep -i '^location:')"
eq "a customer can download their data" 200 "$(call GET /api/account/export "$TOKEN")"
has "the export contains their profile" "$EMAIL" "$(cat "$BODY")"

if [ "$DEV" = 1 ]; then
  echo "== Payment, progress, notifications, cancel (test stack only) =="
  eq "simulated payment is accepted" 204 "$(call POST "/api/dev/simulate-payment/$SUB_ID" "$TOKEN")"
  call GET /api/subscriptions "$TOKEN" >/dev/null
  eq "subscription is now ACTIVE" ACTIVE "$(field '.[0].status')"
  eq "a second plan is refused while one is renewing" 400 "$(call POST /api/subscriptions/checkout "$TOKEN" '{"planId":3}')"
  has "the refusal says to cancel first" "cancel it first" "$(cat "$BODY")"
  call GET /api/subscriptions/payments "$TOKEN" >/dev/null
  ge "the payment shows in the history" 1 "$(jq 'length' "$BODY")"
  has "the payment has a receipt number" "RF-" "$(cat "$BODY")"
  call GET /api/lessons "$TOKEN" >/dev/null
  eq "no lessons are locked for a tier-2 plan except the top tier" 2 "$(jq '[.[] | select(.locked)] | length' "$BODY")"
  eq "a locked lesson now plays" 200 "$(call GET "/api/lessons/$LOCKED_ID/playback" "$TOKEN")"
  eq "watch progress is saved" 200 "$(call POST /api/progress "$TOKEN" "{\"lessonId\":$FREE_ID,\"watchTimeSeconds\":9999}")"
  eq "progress is capped at 100%" 100 "$(field '.completionPercentage | floor')"
  call GET /api/notifications "$TOKEN" >/dev/null
  ge "a payment notification was created" 1 "$(jq 'length' "$BODY")"
  eq "mark all notifications read" 204 "$(call POST /api/notifications/read "$TOKEN" '{"ids":null}')"
  eq "cancelling the subscription works" 200 "$(call POST "/api/subscriptions/$SUB_ID/cancel" "$TOKEN")"
  has "cancel message mentions access until the end date" "keep access" "$(cat "$BODY")"
  eq "cancelling twice is refused" 409 "$(call POST "/api/subscriptions/$SUB_ID/cancel" "$TOKEN")"
fi

echo "== Classes and bookings =="
eq "class list answers 200" 200 "$(call GET /api/classes "$TOKEN")"
CLASS_ID="$(field '.[0].id')"
ge "classes are scheduled" 1 "$(field 'length')"
eq "book a class" 200 "$(call POST "/api/classes/$CLASS_ID/book" "$TOKEN")"
BOOKING_ID="$(field '.id')"
eq "booking the same class twice is refused" 409 "$(call POST "/api/classes/$CLASS_ID/book" "$TOKEN")"
eq "my bookings lists it" 1 "$(call GET /api/bookings "$TOKEN" >/dev/null; field 'length')"
eq "cancel the booking" 204 "$(call POST "/api/bookings/$BOOKING_ID/cancel" "$TOKEN")"

echo "== Journal =="
eq "add a journal entry" 200 "$(call POST /api/journal "$TOKEN" '{"kind":"REFLECTION","mood":"Calmer","text":"Smoke test"}')"
J_ID="$(field '.id')"
eq "journal lists the entry" 1 "$(call GET /api/journal "$TOKEN" >/dev/null; field 'length')"
eq "delete the entry" 204 "$(call DELETE "/api/journal/$J_ID" "$TOKEN")"

echo "== Password reset =="
eq "forgot-password gives the same answer for a known email" 200 "$(call POST /api/auth/forgot-password '' "{\"email\":\"$EMAIL\"}")"
eq "forgot-password gives the same answer for an unknown email" 200 "$(call POST /api/auth/forgot-password '' '{"email":"nobody-here@example.com"}')"
if [ -n "${RESET_CODE_CMD:-}" ]; then
  sleep 2
  CODE="$(bash -c "$RESET_CODE_CMD" 2>/dev/null | tail -n1)"
  eq "a wrong code is refused" 400 "$(call POST /api/auth/reset-password '' "{\"email\":\"$EMAIL\",\"code\":\"000000\",\"newPassword\":\"New-Password-$STAMP!\"}")"
  eq "the emailed code resets the password" 200 "$(call POST /api/auth/reset-password '' "{\"email\":\"$EMAIL\",\"code\":\"$CODE\",\"newPassword\":\"New-Password-$STAMP!\"}")"
  eq "the old password no longer works" 401 "$(call POST /api/auth/login '' "{\"identifier\":\"$USERN\",\"password\":\"$PASSWORD\"}")"
  eq "the old login token no longer works" 401 "$(call GET /api/auth/me "$TOKEN")"
  eq "the new password works" 200 "$(call POST /api/auth/login '' "{\"identifier\":\"$USERN\",\"password\":\"New-Password-$STAMP!\"}")"
  TOKEN="$(field '.token')"
  CURPW="New-Password-$STAMP!"
fi

echo "== Error reporting =="
eq "the app can report an error" 200 "$(call POST /api/telemetry/errors '' '{"message":"Smoke test: pretend app error","details":"at smoke.test(Test.kt:1)","route":"smoke","appVersion":"1.0","device":"CI","fatal":false}')"
has "the report gets a reference number" referenceId "$(cat "$BODY")"
if [ "$DEV" = 1 ]; then
  eq "a forced server error returns 500" 500 "$(call GET /api/dev/boom "$TOKEN")"
  has "the 500 reply carries a reference number and no stack trace" referenceId "$(cat "$BODY")"
  if grep -q "InvalidOperationException" "$BODY"; then bad "500 reply hides internals" "exception name leaked"; else ok "500 reply hides internals"; fi
fi

if [ -n "${ADMIN_EMAIL:-}" ] && [ -n "${ADMIN_PASSWORD:-}" ]; then
  echo "== Admin =="
  eq "admin can log in" 200 "$(call POST /api/auth/login '' "{\"identifier\":\"$ADMIN_EMAIL\",\"password\":\"$ADMIN_PASSWORD\"}")"
  ATOKEN="$(field '.token')"
  eq "admin summary loads" 200 "$(call GET /api/admin/summary "$ATOKEN")"
  eq "a customer cannot open admin pages" 403 "$(call GET /api/admin/summary "$TOKEN")"
  eq "admin sees the error log" 200 "$(call GET '/api/admin/errors?status=NEW' "$ATOKEN")"
  ge "the reported errors are listed" 1 "$(field 'length')"
  call GET /api/notifications "$ATOKEN" >/dev/null
  ge "admin received error notifications" 1 "$(jq 'length' "$BODY")"
  eq "admin can search customers" 200 "$(call GET "/api/admin/users?search=$USERN" "$ATOKEN")"
  SMOKE_ID="$(field '.[0].id')"
  eq "the new customer is found" "$USERN" "$(field '.[0].username')"
  eq "a customer cannot list customers" 403 "$(call GET /api/admin/users "$TOKEN")"
  eq "admin can switch the customer off" 204 "$(call POST "/api/admin/users/$SMOKE_ID/status" "$ATOKEN" '{"status":"DISABLED"}')"
  eq "a switched-off customer cannot log in" 401 "$(call POST /api/auth/login '' "{\"identifier\":\"$USERN\",\"password\":\"$CURPW\"}")"
  eq "and their old login token stops working" 401 "$(call GET /api/auth/me "$TOKEN")"
  eq "admin can switch them back on" 204 "$(call POST "/api/admin/users/$SMOKE_ID/status" "$ATOKEN" '{"status":"ACTIVE"}')"
  eq "they can log in again" 200 "$(call POST /api/auth/login '' "{\"identifier\":\"$USERN\",\"password\":\"$CURPW\"}")"
  TOKEN="$(field '.token')"
fi

echo "== Account deletion (the throw-away smoke customer removes itself) =="
eq "deleting with the wrong password is refused" 400 "$(call POST /api/account/delete "$TOKEN" '{"password":"definitely-wrong"}')"
eq "deleting the account works" 200 "$(call POST /api/account/delete "$TOKEN" "{\"password\":\"$CURPW\"}")"
eq "the deleted account cannot log in" 401 "$(call POST /api/auth/login '' "{\"identifier\":\"$USERN\",\"password\":\"$CURPW\"}")"
eq "the old login token stops working" 401 "$(call GET /api/auth/me "$TOKEN")"

echo
echo "Result: $PASS passed, $FAIL failed"
[ "$FAIL" -eq 0 ]
