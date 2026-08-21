#!/usr/bin/env bash
# Prepares .env and brings the stack up. Safe to re-run: an existing .env is left alone, so your
# password and signing keys survive, and compose reconciles whatever is already running.
set -euo pipefail

cd "$(dirname "$0")/.."

require() {
	command -v "$1" >/dev/null 2>&1 || { echo "error: $1 is required but not installed." >&2; exit 1; }
}

require docker
docker compose version >/dev/null 2>&1 || { echo "error: 'docker compose' is required (Compose v2)." >&2; exit 1; }
require openssl
require python3

if [ -f .env ]; then
	echo "-> .env exists; leaving it as it is."
else
	echo "-> Writing .env"
	cp .env.example .env

	# Never published to the host, but it is still the only thing between a container on this machine
	# and the database.
	password="$(openssl rand -base64 32 | tr -d '/+=' | cut -c1-32)"

	# The key GoTrue signs tokens with. ES256 because the backend validates against the public half
	# fetched from the JWKS endpoint, so the private key never leaves this file.
	keys="$(openssl ecparam -name prime256v1 -genkey -noout 2>/dev/null | python3 scripts/jwk_from_pem.py)"

	python3 - "$password" "$keys" <<-'PY'
		import io, sys
		password, keys = sys.argv[1], sys.argv[2]
		s = io.open('.env', encoding='utf-8').read()
		s = s.replace('POSTGRES_PASSWORD=', 'POSTGRES_PASSWORD=' + password, 1)
		s = s.replace('GOTRUE_JWT_KEYS=', 'GOTRUE_JWT_KEYS=' + keys, 1)
		io.open('.env', 'w', encoding='utf-8', newline='\n').write(s)
	PY
fi

echo "-> Starting the stack. The first run downloads the extraction model, which takes a while."
docker compose up -d

url="$(grep -E '^PUBLIC_URL=' .env | cut -d= -f2-)"
echo "-> Waiting for ${url}/health/ready"

for _ in $(seq 1 120); do
	if curl -fsS "${url}/health/ready" >/dev/null 2>&1; then
		echo
		curl -fsS "${url}/health/ready"
		echo
		echo "-> Ready. Create the first account, which claims the household as its admin:"
		echo "   ./scripts/token.sh you@example.com 'your-password' --signup"
		exit 0
	fi
	printf '.'
	sleep 5
done

echo
echo "error: the stack did not become ready. What each dependency reports:" >&2
curl -fsS "${url}/health/ready" || docker compose ps
exit 1
