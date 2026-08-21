#!/usr/bin/env bash
# Prints an access token for manual API calls. There is no web app yet, and the Scalar reference
# declares no security scheme, so this is how you get a bearer token to paste.
#
#   ./scripts/token.sh you@example.com 'password'            # sign in
#   ./scripts/token.sh you@example.com 'password' --signup   # create the account first
#
# The very first account to sign in claims the household as its admin.
set -euo pipefail

cd "$(dirname "$0")/.."

if [ $# -lt 2 ]; then
	sed -n '2,9p' "$0" | sed 's/^# \{0,1\}//'
	exit 64
fi

email="$1"
password="$2"
mode="${3:-}"

[ -f .env ] || { echo "error: no .env. Run scripts/setup.sh first." >&2; exit 1; }
url="$(grep -E '^PUBLIC_URL=' .env | cut -d= -f2-)"

# The endpoint differs, but both answer with the same token shape.
if [ "$mode" = "--signup" ]; then
	endpoint="${url}/auth/v1/signup"
else
	endpoint="${url}/auth/v1/token?grant_type=password"
fi

response="$(curl -fsS "$endpoint" \
	-H 'Content-Type: application/json' \
	-d "{\"email\":$(printf '%s' "$email" | python3 -c 'import json,sys; print(json.dumps(sys.stdin.read()))'),\"password\":$(printf '%s' "$password" | python3 -c 'import json,sys; print(json.dumps(sys.stdin.read()))')}")"

token="$(printf '%s' "$response" | python3 -c 'import json,sys; print(json.load(sys.stdin).get("access_token",""))')"

if [ -z "$token" ]; then
	echo "error: no access token in the response:" >&2
	printf '%s\n' "$response" >&2
	exit 1
fi

printf '%s\n' "$token"

# On stderr, so `TOKEN=$(./scripts/token.sh ...)` still captures only the token.
{
	echo
	echo "Use it as:"
	echo "  curl -H 'Authorization: Bearer <token>' ${url}/api/receipts"
} >&2
