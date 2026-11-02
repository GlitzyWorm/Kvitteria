#!/usr/bin/env bash
# Prepares .env, brings the stack up, and creates the first admin before anything is reachable.
# Safe to re-run: an existing .env is left alone, so your password and signing keys survive, an
# already-claimed household asks for nothing, and compose reconciles whatever is already running.
#
# On a fresh install it asks for the public address and, for an https:// one, whether a proxy you run
# already terminates TLS. For an unattended run set these instead:
#
#   KVITTERIA_PUBLIC_URL      the address clients use (default http://localhost:8080)
#   KVITTERIA_TLS             proxy | domain   (https:// addresses only; default domain)
#   KVITTERIA_ADMIN_EMAIL     the first admin, while the household is unclaimed
#   KVITTERIA_ADMIN_PASSWORD  its password. Never written to a file or put in a process's arguments.
set -euo pipefail

cd "$(dirname "$0")/.."

die() {
	echo "error: $*" >&2
	exit 1
}

require() {
	command -v "$1" >/dev/null 2>&1 || die "$1 is required but not installed."
}

require docker
docker compose version >/dev/null 2>&1 || die "'docker compose' is required (Compose v2)."
require openssl
require python3

DEFAULT_URL="http://localhost:8080"
# compose.public.yaml replaces the base file's port list with `!override`.
MIN_COMPOSE="2.24.4"

# True when $1 >= $2, comparing dotted numbers.
version_at_least() {
	[ "$(printf '%s\n%s\n' "$2" "$1" | sort -V | head -n1)" = "$2" ]
}

# --- Fresh install: address and mode, chosen and checked before anything is written ----------------

fresh=false
if [ -f .env ]; then
	echo "-> .env exists; leaving it as it is."
else
	fresh=true

	url="${KVITTERIA_PUBLIC_URL:-}"
	if [ -z "$url" ]; then
		if [ -t 0 ]; then
			read -r -p "Public address clients will use [${DEFAULT_URL}]: " url
		fi
		url="${url:-$DEFAULT_URL}"
	fi

	# A scheme and a host, optionally a port. A path or a trailing slash would end up inside the token
	# issuer and every address derived from it.
	if ! printf '%s' "$url" | grep -Eq '^https?://[A-Za-z0-9]([A-Za-z0-9.-]*[A-Za-z0-9])?(:[0-9]{1,5})?$'; then
		die "'${url}' is not a usable address. Use http:// or https://, a host and optionally a port, with no path and no trailing slash."
	fi

	tls=""
	case "$url" in
		https://*)
			tls="${KVITTERIA_TLS:-}"
			if [ -z "$tls" ]; then
				if [ -t 0 ]; then
					read -r -p "Does a proxy you run (Tailscale serve, Cloudflare Tunnel, Traefik, ...) already terminate TLS for this address? [y/N] " answer
					case "$answer" in [yY]*) tls=proxy ;; *) tls=domain ;; esac
				else
					tls=domain
				fi
			fi
			case "$tls" in
				proxy | domain) ;;
				*) die "KVITTERIA_TLS must be 'proxy' or 'domain', not '${tls}'." ;;
			esac
			;;
	esac

	if [ "$tls" = "domain" ]; then
		have="$(docker compose version --short 2>/dev/null | sed 's/^v//')"
		version_at_least "${have:-0}" "$MIN_COMPOSE" ||
			die "Public-domain mode needs Docker Compose ${MIN_COMPOSE} or newer; this is ${have:-unknown}."
	fi

	echo "-> Writing .env"
	cp .env.example .env

	# Never published to the host, but it is still the only thing between a container on this machine
	# and the database.
	password="$(openssl rand -base64 32 | tr -d '/+=' | cut -c1-32)"

	# GoTrue's config loader demands a symmetric secret whether or not asymmetric keys are set. With the
	# key set below present it goes unused, and it is never shared with the backend.
	secret="$(openssl rand -base64 48 | tr -d '/+=' | cut -c1-48)"

	# The key GoTrue signs tokens with. ES256 because the backend validates against the public half
	# fetched from the JWKS endpoint, so the private key never leaves this file.
	keys="$(openssl ecparam -name prime256v1 -genkey -noout 2>/dev/null | python3 scripts/jwk_from_pem.py)"

	# Both HTTPS modes name their files, which also leaves compose.override.yaml out. That file is the
	# development overlay: it would run the backend in Development and publish Postgres and GoTrue on
	# the host, and a published GoTrue port bypasses the client address the rate limit is keyed on.
	case "$tls" in
		domain) compose_file="compose.yaml:compose.public.yaml" ;;
		proxy) compose_file="compose.yaml" ;;
		*) compose_file="" ;;
	esac

	KVITTERIA_ENV_PASSWORD="$password" KVITTERIA_ENV_SECRET="$secret" KVITTERIA_ENV_KEYS="$keys" \
		KVITTERIA_ENV_URL="$url" KVITTERIA_ENV_COMPOSE_FILE="$compose_file" python3 - <<-'PY'
		import io, os, re
		e = os.environ
		s = io.open('.env', encoding='utf-8').read()

		def put(s, key, value):
		    return re.sub(r'^' + key + r'=.*$', lambda m: key + '=' + value, s, count=1, flags=re.M)

		s = put(s, 'POSTGRES_PASSWORD', e['KVITTERIA_ENV_PASSWORD'])
		s = put(s, 'GOTRUE_JWT_SECRET', e['KVITTERIA_ENV_SECRET'])
		s = put(s, 'GOTRUE_JWT_KEYS', e['KVITTERIA_ENV_KEYS'])
		s = put(s, 'PUBLIC_URL', e['KVITTERIA_ENV_URL'])

		# A plain-HTTP address with a port is served on that port.
		m = re.match(r'^http://[^/:]+:(\d+)$', e['KVITTERIA_ENV_URL'])
		if m:
		    s = put(s, 'CADDY_HTTP_PORT', m.group(1))

		if e['KVITTERIA_ENV_COMPOSE_FILE']:
		    s = s.rstrip('\n') + '\n\nCOMPOSE_FILE=' + e['KVITTERIA_ENV_COMPOSE_FILE'] + '\n'
		io.open('.env', 'w', encoding='utf-8', newline='\n').write(s)
		PY
fi

# --- Credentials -----------------------------------------------------------------------------------

admin_email="${KVITTERIA_ADMIN_EMAIL:-}"
admin_password="${KVITTERIA_ADMIN_PASSWORD:-}"
unset KVITTERIA_ADMIN_PASSWORD

# Asked for once, and only when the household turns out to be unclaimed.
collect_credentials() {
	[ -n "$admin_email" ] && [ -n "$admin_password" ] && return 0
	if [ ! -t 0 ]; then
		die "the household has no admin yet and there is no terminal to ask on. Set KVITTERIA_ADMIN_EMAIL and KVITTERIA_ADMIN_PASSWORD and run again. Nothing is reachable meanwhile."
	fi
	echo "-> Creating the first admin. This account claims the household."
	if [ -z "$admin_email" ]; then
		read -r -p "Admin email: " admin_email
	fi
	if [ -z "$admin_password" ]; then
		read -r -s -p "Admin password: " admin_password
		echo
	fi
	[ -n "$admin_email" ] && [ -n "$admin_password" ] || die "an email and a password are both needed."
}

# On a fresh install the model download below is the long part, so ask now rather than after it.
if [ "$fresh" = true ]; then
	collect_credentials
fi

# --- The directory updates report through ------------------------------------------------------------

# Before anything starts, or Docker creates it as root for the bind mount. The backend runs as this UID,
# the .NET image's `app` user, and has to be able to write the request file here.
APP_UID=1654
mkdir -p update-state/public
if [ "$(id -u)" = "0" ]; then
	chown "$APP_UID" update-state update-state/public
fi

# --- Start the services the claim needs, without the entrypoint -------------------------------------

# Caddy depends on a healthy backend, so a plain `up -d` would open the entrypoint on an unclaimed
# household for the seconds it takes to claim it. Bringing up only the backend and the identity provider
# (and what they depend on) keeps every port closed until the household has an admin.
echo "-> Starting the backend and identity provider. The first run downloads the extraction model, which takes a while."
docker compose stop caddy >/dev/null 2>&1 || true
docker compose up -d --wait backend gotrue

# Everything below goes through `exec` into the backend container, so the claim needs no DNS, no
# certificate and no proxy of yours to be in place.
backend_curl() {
	docker compose exec -T backend curl -sS "$@"
}

household_status() {
	backend_curl http://127.0.0.1:8080/health/ready 2>/dev/null |
		python3 -c 'import json,sys
try:
	r = json.load(sys.stdin)
	print(next((c["status"] for c in r["checks"] if c["name"] == "household"), "Unknown"))
except Exception:
	print("Unknown")' || echo "Unknown"
}

# Reads "email\npassword\n" on stdin and prints the JSON body GoTrue expects, so neither value is ever
# an argument of any process.
credentials_json() {
	python3 -c 'import json,sys
email, password = sys.stdin.read().split("\n")[:2]
print(json.dumps({"email": email, "password": password}))'
}

# Prints "<http status>" on the first line and the body after it.
gotrue_post() {
	local path="$1"
	credentials_json < <(printf '%s\n%s\n' "$admin_email" "$admin_password") |
		docker compose exec -T backend curl -sS -X POST -H 'Content-Type: application/json' \
			--data-binary @- -w '\n%{http_code}' "http://gotrue:9999${path}" |
		python3 -c 'import sys
raw = sys.stdin.read()
body, _, status = raw.rpartition("\n")
print(status)
print(body)'
}

json_field() {
	python3 -c 'import json,sys
try:
	print(json.load(sys.stdin).get(sys.argv[1]) or "")
except Exception:
	print("")' "$1"
}

claim_household() {
	local response status body token error reason
	response="$(gotrue_post /signup)"
	status="$(printf '%s\n' "$response" | head -n1)"
	body="$(printf '%s\n' "$response" | tail -n +2)"
	token="$(printf '%s' "$body" | json_field access_token)"

	if [ -z "$token" ]; then
		error="$(printf '%s' "$body" | json_field error_code)"
		# An earlier run created the account and stopped before the membership request, or this is the
		# same person signing in again.
		if [ "$error" = "user_already_exists" ] || [ "$status" = "200" ]; then
			response="$(gotrue_post '/token?grant_type=password')"
			status="$(printf '%s\n' "$response" | head -n1)"
			body="$(printf '%s\n' "$response" | tail -n +2)"
			token="$(printf '%s' "$body" | json_field access_token)"
		fi
	fi

	if [ -z "$token" ]; then
		reason="$(printf '%s' "$body" | json_field msg)$(printf '%s' "$body" | json_field error_description)"
		die "could not create or sign in the admin account (HTTP ${status}): ${reason%.} The entrypoint stays stopped; run this script again."
	fi

	# GET /api/users/me is what claims the household: the first authenticated caller is bootstrapped
	# into the admin by the same decision that handles every later request.
	local me
	me="$(printf 'Authorization: Bearer %s\n' "$token" |
		docker compose exec -T backend curl -sS -o /dev/null -w '%{http_code}' -H @- http://127.0.0.1:8080/api/users/me)"
	[ "$me" = "200" ] || die "the account exists but the household was not claimed (GET /api/users/me answered ${me}). The entrypoint stays stopped."
}

state="$(household_status)"
if [ "$state" != "Healthy" ]; then
	collect_credentials
	claim_household
	unset admin_password
	state="$(household_status)"
fi

[ "$state" = "Healthy" ] || die "the household still has no admin (it reports ${state}). The entrypoint stays stopped."

# --- Open the entrypoint ----------------------------------------------------------------------------

echo "-> The household is claimed. Starting the entrypoint."
docker compose up -d

# --- The update agent ---------------------------------------------------------------------------------

# Lets the app start an update without any container holding the Docker socket. Everywhere else the app
# still shows that a release exists, with the command to run.
install_agent() {
	if [ "$(id -u)" != "0" ] || [ ! -d /run/systemd/system ] || [ -e .git ]; then
		echo "-> Skipped the update agent: it needs root on a systemd host, outside a git checkout. The app can't start updates here; run ./scripts/update.sh on the server instead."
		return 0
	fi

	echo "-> Installing the update agent"
	local dir unit
	dir="$(pwd)"
	for unit in scripts/systemd/*; do
		sed "s|@STACK_DIR@|${dir}|g" "$unit" >"/etc/systemd/system/$(basename "$unit")"
	done
	systemctl daemon-reload
	systemctl enable --now kvitteria-update.path kvitteria-update-heartbeat.timer
	systemctl enable kvitteria-update-boot.service
}

install_agent

url="$(grep -E '^PUBLIC_URL=' .env | cut -d= -f2-)"
echo
echo "Ready. The household is claimed, and clients connect to:"
echo "  ${url}"
