#!/usr/bin/env bash
# Simulates PayTR's step-2 notification (Bildirim URL) against a local API.
#
# PayTR cannot reach localhost, so a local end-to-end run opens the real payment frame (step 1
# talks to www.paytr.com) but never receives the notification. This script posts one, signed with
# the same merchant key/salt the API reads from user-secrets, so the whole path after the frame —
# order paid, Pro extended, receipt queued, result page — can be exercised on a developer machine.
#
# Usage:
#   scripts/paytr-callback.sh <merchant_oid> success <total_amount_kurus>
#   scripts/paytr-callback.sh <merchant_oid> failed  <total_amount_kurus> [failed_reason_code] [failed_reason_msg]
#   PAYTR_BAD_HASH=1 scripts/paytr-callback.sh <merchant_oid> success <total>   # tamper test → 400
#
# Environment: API_BASE (default http://localhost:5151). The merchant key and salt come from
# `dotnet user-secrets list --project src/AfterApply.Api`, or from PAYTR_KEY / PAYTR_SALT.
set -euo pipefail

oid="${1:?merchant_oid}"
status="${2:?success|failed}"
total="${3:?total_amount in kuruş, e.g. 29900}"
code="${4:-}"
msg="${5:-}"
api="${API_BASE:-http://localhost:5151}"

root="$(cd "$(dirname "$0")/.." && pwd)"
secrets() { dotnet user-secrets list --project "$root/src/AfterApply.Api" 2>/dev/null | awk -F' = ' -v k="$1" '$1 == k { print $2 }'; }
key="${PAYTR_KEY:-$(secrets PayTr:MerchantKey)}"
salt="${PAYTR_SALT:-$(secrets PayTr:MerchantSalt)}"
if [[ -z "$key" || -z "$salt" ]]; then
  echo "PayTr:MerchantKey / PayTr:MerchantSalt not found in user-secrets; set PAYTR_KEY and PAYTR_SALT." >&2
  exit 1
fi

# hash = base64(HMAC-SHA256(merchant_oid + merchant_salt + status + total_amount, merchant_key))
hash="$(printf '%s%s%s%s' "$oid" "$salt" "$status" "$total" | openssl dgst -sha256 -hmac "$key" -binary | base64)"
if [[ "${PAYTR_BAD_HASH:-0}" == "1" ]]; then
  hash="bm90LWEtcmVhbC1oYXNo"
fi

args=(
  --data-urlencode "merchant_oid=$oid"
  --data-urlencode "status=$status"
  --data-urlencode "total_amount=$total"
  --data-urlencode "payment_amount=$total"
  --data-urlencode "hash=$hash"
  --data-urlencode "payment_type=card"
  --data-urlencode "currency=TL"
  --data-urlencode "test_mode=1"
)
if [[ -n "$code" ]]; then args+=(--data-urlencode "failed_reason_code=$code"); fi
if [[ -n "$msg" ]]; then args+=(--data-urlencode "failed_reason_msg=$msg"); fi

curl -sS -w '\nHTTP %{http_code}\n' "${args[@]}" "$api/api/payments/paytr/callback"
