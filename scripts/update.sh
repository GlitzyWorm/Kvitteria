#!/usr/bin/env bash
# Moves the stack to the newest release. The one implementation of an update: the console runs it
# directly, and the host agent runs it when the app asks.
#
#   ./scripts/update.sh                      update to the newest release
#   ./scripts/update.sh --bundle <file>      apply kvitteria-stack-<version>.tar.gz without contacting GitHub
#   ./scripts/update.sh --restore            put back the database, files and version from before the last
#                                            update. Asks first, and only ever runs at a terminal.
#
# Nothing is touched until the bundle and the image are both here, the backend is stopped before the dump
# so no receipt lands after it, and a failure undoes what it safely can: with the migrations unchanged the
# previous version comes back by itself, and once one has applied the backend stays down for a person to
# run --restore. Restoring is never automatic, because it replaces the database on the strength of a
# check that can fail for unrelated reasons.
#
# Needs bash, curl, tar and Docker Compose.
set -euo pipefail

BUNDLE_FILE=""
case "${1:-}" in
	--bundle)
		[ -n "${2:-}" ] || {
			echo "usage: $0 --bundle <file>" >&2
			exit 2
		}
		case "$2" in
			/*) BUNDLE_FILE="$2" ;;
			*) BUNDLE_FILE="$PWD/$2" ;;
		esac
		;;
esac

cd "$(dirname "$0")/.."

DEFAULT_REPOSITORY="GlitzyWorm/Kvitteria"

# Private to the host: nothing in the stack can see this directory. .update/state is therefore what the
# script trusts about the last outcome, and update-state/ is only ever written, never read.
WORK=.update
INCOMING="$WORK/incoming"
STACK="$INCOMING/stack"
PREVIOUS="$WORK/previous"
DUMPS="$WORK/dumps"
DUMPS_KEPT=3

STATE_DIR=update-state
PUBLIC_DIR="$STATE_DIR/public"

VERIFY_SECONDS="${KVITTERIA_UPDATE_VERIFY_SECONDS:-300}"

# How far a run got, which is what decides what a failure has to undo:
#   idle      nothing started
#   checking  nothing changed yet
#   stopped   the backend is down on its old version
#   swapped   the new files are in place
#   finishing the new backend works; only the other services are left
#   done      the outcome is recorded
STAGE=idle
HOLDS_LOCK=false
FAIL_REASON=""
TARGET=""
RUNNING=""
BUNDLE_URL=""

log() {
	echo "-> $*"
}

die() {
	echo "error: $*" >&2
	exit 1
}

# Records the reason, and the EXIT trap does whatever undoing the stage calls for.
fail() {
	FAIL_REASON="$*"
	echo "error: $*" >&2
	exit 1
}

require() {
	command -v "$1" >/dev/null 2>&1 || die "$1 is required but not installed."
}

sha256() {
	if command -v sha256sum >/dev/null 2>&1; then
		sha256sum "$1" | cut -d' ' -f1
	else
		shasum -a 256 "$1" | cut -d' ' -f1
	fi
}

# True when $1 is strictly below $2, comparing dotted numbers.
version_lt() {
	[ "$1" != "$2" ] && [ "$(printf '%s\n%s\n' "$1" "$2" | sort -V | head -n1)" = "$1" ]
}

env_value() {
	grep -E "^$1=" .env 2>/dev/null | tail -n1 | cut -d= -f2- | tr -d '\r' || true
}

# --- Reporting --------------------------------------------------------------------------------------

json_escape() {
	printf '%s' "$1" | tr '\n\t' '  ' | sed -e 's/\\/\\\\/g' -e 's/"/\\"/g'
}

# Renamed into place, so a reader never sees half a file. That replaces the inode, which is why Caddy
# mounts the directory rather than the file. The temporary file sits in the private directory because the
# backend owns update-state/, and this may run as root: a name there could already be a symlink it planted.
write_json() {
	local next
	next="$(mktemp "$WORK/status.XXXXXX")" &&
		printf '%s\n' "$2" >"$next" && chmod 644 "$next" && mv -f "$next" "$1"
}

# `detail` can name paths and errors, so it stays out of the copy Caddy serves to anyone. A status that
# can't be written costs a warning and nothing else: it must never decide how an update goes.
set_state() {
	local state="$1" target="$2" detail="${3:-}" at
	at="$(date -u +%Y-%m-%dT%H:%M:%SZ)"

	printf '%s\n%s\n' "$state" "$target" >"$WORK/state"

	{
		mkdir -p "$PUBLIC_DIR" && [ ! -L "$PUBLIC_DIR" ] &&
			write_json "$STATE_DIR/status.json" \
				"{\"state\":\"${state}\",\"targetVersion\":\"${target}\",\"detail\":\"$(json_escape "$detail")\",\"updatedAt\":\"${at}\"}" &&
			write_json "$PUBLIC_DIR/update-status.json" \
				"{\"state\":\"${state}\",\"targetVersion\":\"${target}\",\"updatedAt\":\"${at}\"}"
	} 2>/dev/null || echo "warning: could not write the update status to ${STATE_DIR}/." >&2
}

last_state() {
	head -n1 "$WORK/state" 2>/dev/null || true
}

# --- Locking ----------------------------------------------------------------------------------------

# mkdir rather than flock, which Git Bash and macOS don't have. The hand-over to the new script keeps the
# process ID, so the run that took the lock recognises it.
acquire_lock() {
	mkdir -p "$WORK"
	chmod 700 "$WORK"
	if ! mkdir "$WORK/lock" 2>/dev/null; then
		local holder
		holder="$(cat "$WORK/lock/pid" 2>/dev/null || true)"
		if [ "$holder" != "$$" ]; then
			if [ -n "$holder" ] && { kill -0 "$holder" 2>/dev/null || ps -p "$holder" >/dev/null 2>&1; }; then
				die "an update is already running (process ${holder})."
			fi
			log "Taking over the lock of a run that no longer exists."
		fi
	fi
	echo "$$" >"$WORK/lock/pid"
	HOLDS_LOCK=true
}

release_lock() {
	if [ "$HOLDS_LOCK" = true ]; then
		rm -rf "$WORK/lock"
		HOLDS_LOCK=false
	fi
}

# --- Reading the stack ------------------------------------------------------------------------------

# Compose against the unpacked bundle, reading the install's own .env. Running from inside the directory
# makes a relative COMPOSE_FILE name the bundle's files.
bundle_compose() {
	(cd "$STACK" && docker compose --env-file ../../../.env "$@")
}

psql_postgres() {
	docker compose exec -T postgres sh -c "psql -U \"\$POSTGRES_USER\" -d \"\$POSTGRES_DB\" -tA -c '$1'"
}

backend_version() {
	docker compose exec -T backend curl -fsS --max-time 5 http://127.0.0.1:8080/version 2>/dev/null |
		grep -oE '"version":"[^"]*"' | cut -d'"' -f4 || true
}

migration_history() {
	psql_postgres 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY 1'
}

# Stops one service and nothing that depends on it: Caddy has to stay up, because it serves the status.
stop_service() {
	local id
	id="$(docker compose ps -q "$1" 2>/dev/null || true)"
	if [ -n "$id" ]; then
		docker stop "$id" >/dev/null
	fi
}

# /health/ready is read for two checks only, directly from the backend. An unhealthy report answers 503,
# and Ollama alone can cause that, which is why `up --wait` can't judge an update. The body says which.
backend_is() {
	local want="$1" ready
	[ "$(backend_version)" = "$want" ] || return 1
	ready="$(docker compose exec -T backend curl -sS --max-time 30 http://127.0.0.1:8080/health/ready 2>/dev/null || true)"
	printf '%s' "$ready" | grep -q '"name":"postgres","status":"Healthy"' &&
		printf '%s' "$ready" | grep -q '"name":"migrations","status":"Healthy"'
}

wait_for_backend() {
	local want="$1" deadline=$((SECONDS + VERIFY_SECONDS))
	while [ "$SECONDS" -lt "$deadline" ]; do
		backend_is "$want" && return 0
		sleep 5
	done
	return 1
}

# --- Refusals ---------------------------------------------------------------------------------------

refuse_checkout() {
	if [ -e .git ] || { command -v git >/dev/null 2>&1 && git rev-parse --is-inside-work-tree >/dev/null 2>&1; }; then
		die "this is a git checkout. Updating would overwrite tracked files, and it runs against the development override. Rebuild with: docker compose up -d --build"
	fi
}

refuse_pending_restore() {
	if [ "$(last_state)" = "restore-required" ]; then
		die "the last update stopped after changing the database, and that has to be resolved first. Run: ./scripts/update.sh --restore"
	fi
}

refuse_postgres_major() {
	local num running bundled
	num="$(psql_postgres 'SHOW server_version_num' | tr -d '\r[:space:]')" || fail "Could not read the running Postgres version."
	running=$((num / 10000))

	bundled="$(bundle_compose config --images | grep -E '(^|/)postgres:' | head -n1 | sed 's/.*://' | grep -oE '^[0-9]+' || true)"
	[ -n "$bundled" ] || fail "Could not tell which Postgres version ${TARGET} would run."

	if [ "$bundled" != "$running" ]; then
		fail "${TARGET} would run Postgres ${bundled} on a server running Postgres ${running}. Moving between major versions needs a dump and restore by hand, so nothing was changed."
	fi
}

# A local-mode install copied with git archive has compose.override.yaml, which builds from source and
# would never run the version asked for.
refuse_unfollowed_version() {
	BACKEND_VERSION="$TARGET" docker compose config --images | tr -d '\r' | grep -qE "/kvitteria-backend:${TARGET}\$" ||
		fail "The backend image does not follow BACKEND_VERSION here, usually because compose.override.yaml is active. Remove it, or list the files in COMPOSE_FILE."
}

# --- Finding the release ----------------------------------------------------------------------------

resolve_target() {
	if [ -n "$BUNDLE_FILE" ]; then
		[ -f "$BUNDLE_FILE" ] || fail "${BUNDLE_FILE} does not exist."
		TARGET="$(basename "$BUNDLE_FILE")"
		TARGET="${TARGET#kvitteria-stack-}"
		TARGET="${TARGET%.tar.gz}"
		printf '%s' "$TARGET" | grep -Eq '^[0-9]+\.[0-9]+\.[0-9]+$' ||
			fail "$(basename "$BUNDLE_FILE") is not named kvitteria-stack-<version>.tar.gz."
		return
	fi

	# Only backend releases carry this asset, and a release gets it once its image is pushed, so the
	# newest match is the newest thing that can be installed. A fork names itself in .env.
	local repository
	repository="$(env_value UPDATE_REPOSITORY)"
	repository="${repository:-$DEFAULT_REPOSITORY}"

	BUNDLE_URL="$(curl -fsSL "https://api.github.com/repos/${repository}/releases?per_page=30" |
		grep -oE 'https://[^"]*/kvitteria-stack-[0-9]+\.[0-9]+\.[0-9]+\.tar\.gz' | sort -V | tail -n1 || true)"
	[ -n "$BUNDLE_URL" ] || fail "Could not find a release of ${repository} with a stack bundle."

	TARGET="$(basename "$BUNDLE_URL")"
	TARGET="${TARGET#kvitteria-stack-}"
	TARGET="${TARGET%.tar.gz}"
}

fetch_bundle() {
	rm -rf "$INCOMING"
	mkdir -p "$STACK"

	local name="kvitteria-stack-${TARGET}" bundle sums
	if [ -n "$BUNDLE_FILE" ]; then
		bundle="$BUNDLE_FILE"
		sums="${BUNDLE_FILE%.tar.gz}.sha256"
	else
		bundle="$INCOMING/${name}.tar.gz"
		sums="$INCOMING/${name}.sha256"
		log "Downloading ${name}"
		curl -fsSL -o "$bundle" "$BUNDLE_URL" || fail "Could not download the bundle for ${TARGET}."
		curl -fsSL -o "$sums" "${BUNDLE_URL%.tar.gz}.sha256" || fail "Could not download the checksum for ${TARGET}."
	fi

	# A bundle file handed over by a person may come without one. A download never does.
	if [ -f "$sums" ]; then
		[ "$(sha256 "$bundle")" = "$(cut -d' ' -f1 "$sums" | tr -d '\r')" ] || fail "The bundle for ${TARGET} does not match its checksum."
	fi

	tar -xzf "$bundle" -C "$STACK" || fail "The bundle for ${TARGET} could not be unpacked."
	[ -f "$STACK/scripts/update.sh" ] && [ -f "$STACK/compose.yaml" ] || fail "The bundle for ${TARGET} is incomplete."
}

# Everything an update can need, before anything stops. The backend always, as the exact version; the
# rest only when this install lacks the tag, so a floating ollama:latest stays where it is. Checked image
# by image, because `pull --policy missing` still pulls a tag named latest.
pull_images() {
	log "Pulling images"
	BACKEND_VERSION="$TARGET" bundle_compose pull --quiet backend || fail "Could not pull the backend image for ${TARGET}."

	local images image
	images="$(BACKEND_VERSION="$TARGET" bundle_compose config --images | tr -d '\r')" || fail "Could not read the images ${TARGET} needs."
	for image in $images; do
		if ! docker image inspect "$image" >/dev/null 2>&1; then
			docker pull --quiet "$image" >/dev/null || fail "Could not pull ${image}."
		fi
	done
}

# --- The backup -------------------------------------------------------------------------------------

take_dump() {
	mkdir -p "$DUMPS"
	local dump
	dump="$DUMPS/update-$(date -u +%Y%m%dT%H%M%SZ)-${RUNNING}.dump"

	log "Backing up the database"
	# One database holds receipts, identities and the keys protecting mailbox passwords.
	if ! (umask 077 && docker compose exec -T postgres sh -c 'pg_dump -U "$POSTGRES_USER" -Fc "$POSTGRES_DB"' >"${dump}.part") ||
		[ ! -s "${dump}.part" ]; then
		rm -f "${dump}.part"
		fail "The database backup failed, so the update stopped."
	fi
	mv "${dump}.part" "$dump"

	# Names sort by time.
	ls -1 "$DUMPS"/*.dump | sort -r | tail -n +$((DUMPS_KEPT + 1)) | while read -r old; do
		rm -f "$old"
	done

	DUMP="$dump"
}

# --- Replacing the files ----------------------------------------------------------------------------

# Everything the new script needs travels here rather than on its command line, so this is the contract
# between one release's script and the next one's.
record_previous() {
	local next="${PREVIOUS}.new" path
	rm -rf "$next"
	mkdir -p "$next/files"

	migration_history >"$next/history" || fail "Could not read the migration history."
	printf '%s\n' "$RUNNING" >"$next/from-version"
	printf '%s\n' "$TARGET" >"$next/target-version"
	printf '%s\n' "$DUMP" >"$next/dump"
	grep -E '^BACKEND_VERSION=' .env | tr -d '\r' >"$next/pin" || true
	sha256 caddy/Caddyfile >"$next/caddyfile-hash"

	: >"$next/added"
	(cd "$STACK" && find . -type f) | sed 's|^\./||' | while read -r path; do
		if [ -f "$path" ]; then
			mkdir -p "$next/files/$(dirname "$path")"
			cp -p "$path" "$next/files/$path"
		else
			printf '%s\n' "$path" >>"$next/added"
		fi
	done

	rm -rf "$PREVIOUS"
	mv "$next" "$PREVIOUS"
}

swap_files() {
	log "Replacing the stack's files"
	# Bash reads a script as it runs, so this file is unlinked rather than written over. The running copy
	# keeps its inode until the exec below.
	rm -f scripts/update.sh
	cp -R "$STACK/." .
}

restore_files() {
	cp -R "$PREVIOUS/files/." .
	local path
	while read -r path; do
		rm -f "$path"
	done <"$PREVIOUS/added"
}

# Sets or, for an empty argument, removes the BACKEND_VERSION line. Every other line stays as it was, and
# writing through `cat` keeps the file's owner and mode.
write_pin() {
	local line="$1" next="$WORK/env.next"
	if grep -qE '^BACKEND_VERSION=' .env; then
		if [ -n "$line" ]; then
			sed "s|^BACKEND_VERSION=.*|${line}|" .env >"$next"
		else
			sed '/^BACKEND_VERSION=/d' .env >"$next"
		fi
	else
		cp .env "$next"
		[ -z "$line" ] || printf '%s\n' "$line" >>"$next"
	fi
	cat "$next" >.env
	rm -f "$next"
}

# --- When an update fails ---------------------------------------------------------------------------

roll_back() {
	local reason="$1" from
	from="$(cat "$PREVIOUS/from-version")"

	log "Rolling back to ${from}"
	restore_files
	write_pin "$(cat "$PREVIOUS/pin")"

	if docker compose up -d --no-deps backend && wait_for_backend "$from"; then
		set_state rolled-back "$TARGET" "$reason"
		echo "Rolled back to ${from}. Why: ${reason}" >&2
	else
		set_state failed "$TARGET" "${reason} Rolling back to ${from} did not bring the backend up either."
		echo "error: rolling back to ${from} did not bring the backend up. See: docker compose logs backend" >&2
	fi
}

# History is what separates the two ways back. Unchanged means the old version can run against this
# database. Anything else, an unreadable history included, means only the dump can undo it.
recover_from_failed_update() {
	local reason="$1" after="$WORK/history.after"

	# Under `restart: unless-stopped` a crashing backend keeps retrying its migrations until stopped.
	stop_service backend

	if migration_history >"$after" 2>/dev/null && cmp -s "$after" "$PREVIOUS/history"; then
		rm -f "$after"
		roll_back "$reason"
		return
	fi

	rm -f "$after"
	set_state restore-required "$TARGET" "${reason} The database had already changed, so the backend is stopped. Run ./scripts/update.sh --restore on the server."
	echo "error: ${reason}" >&2
	echo "The database had already changed, so the backend is stopped and nothing was restored." >&2
	echo "To go back to the state before the update, run: ./scripts/update.sh --restore" >&2
}

on_exit() {
	local code=$?
	trap - EXIT
	set +e

	local reason="${FAIL_REASON:-The update stopped unexpectedly. See the output of ./scripts/update.sh.}"
	case "$STAGE" in
		checking)
			set_state failed "$TARGET" "$reason"
			;;
		stopped)
			log "Starting the backend again on ${RUNNING}"
			docker compose up -d --no-deps backend
			set_state failed "$TARGET" "$reason"
			;;
		swapped)
			recover_from_failed_update "$reason"
			;;
		finishing)
			set_state failed "$TARGET" "${reason} The backend is on ${TARGET}. Run docker compose up -d on the server."
			;;
	esac

	rm -rf "$INCOMING"
	release_lock
	exit "$code"
}

# --- The update -------------------------------------------------------------------------------------

update() {
	refuse_checkout
	require curl
	require tar
	require docker
	docker compose version >/dev/null 2>&1 || die "'docker compose' is required (Compose v2)."
	[ -f .env ] || die "there is no .env here. Run ./scripts/setup.sh first."

	acquire_lock
	trap on_exit EXIT
	trap 'exit 143' TERM INT

	refuse_pending_restore

	RUNNING="$(backend_version)"
	[ -n "$RUNNING" ] || die "the backend isn't answering, so its version is unknown. Start it with: docker compose up -d"

	STAGE=checking
	resolve_target

	if [ "$TARGET" = "$RUNNING" ]; then
		STAGE=idle
		echo "Already on ${RUNNING}, the newest available."
		return
	fi
	if version_lt "$TARGET" "$RUNNING"; then
		if [ -n "$BUNDLE_FILE" ]; then
			fail "${TARGET} is older than the running ${RUNNING}, and migrations only go forward."
		fi
		STAGE=idle
		echo "Running ${RUNNING}, which is newer than the newest available release, ${TARGET}."
		return
	fi

	log "Updating ${RUNNING} to ${TARGET}"
	set_state running "$TARGET" "Updating from ${RUNNING}."

	fetch_bundle
	refuse_postgres_major
	refuse_unfollowed_version
	pull_images

	log "Stopping the backend"
	STAGE=stopped
	stop_service backend
	take_dump
	record_previous

	STAGE=swapped
	swap_files

	# From here the new release's own script carries on, so a release can fix the process delivering it.
	# The lock stays, since exec keeps the process.
	trap - EXIT TERM INT
	exec bash scripts/update.sh --continue
}

continue_update() {
	[ -f "$PREVIOUS/target-version" ] || die "--continue is only for the update script's own hand-over."
	TARGET="$(cat "$PREVIOUS/target-version")"
	RUNNING="$(cat "$PREVIOUS/from-version")"

	acquire_lock
	trap on_exit EXIT
	trap 'exit 143' TERM INT
	STAGE=swapped

	write_pin "BACKEND_VERSION=${TARGET}"

	log "Starting ${TARGET}"
	# --no-deps, because a plain `up` waits on everything the backend depends on, and an Ollama still
	# fetching its model would fail an update that has nothing to do with it.
	docker compose up -d --no-deps backend || fail "${TARGET} could not be started."

	log "Waiting for ${TARGET} to report in"
	wait_for_backend "$TARGET" || fail "${TARGET} did not become healthy within ${VERIFY_SECONDS} seconds."

	STAGE=finishing
	log "Starting the other services"
	local services
	services="$(docker compose config --services | tr -d '\r' | grep -vx backend)"
	# Word splitting wanted: one argument per service.
	# shellcheck disable=SC2086
	docker compose up -d --no-deps $services || fail "The other services could not be started."

	# Compose compares configuration rather than the contents of a mounted file, so it can't see this.
	if [ "$(sha256 caddy/Caddyfile)" != "$(cat "$PREVIOUS/caddyfile-hash")" ]; then
		log "Restarting Caddy for its new configuration"
		docker compose restart caddy >/dev/null || fail "Caddy could not be restarted."
	fi

	set_state succeeded "$TARGET" "Updated from ${RUNNING}."
	STAGE=done
	echo "Updated from ${RUNNING} to ${TARGET}."
}

# --- Restoring --------------------------------------------------------------------------------------

restore() {
	refuse_checkout
	[ -t 0 ] || die "--restore replaces the whole database, so it only runs at a terminal."
	[ -f "$PREVIOUS/dump" ] || die "no update has been recorded here, so there is nothing to restore."

	local dump from
	dump="$(cat "$PREVIOUS/dump")"
	from="$(cat "$PREVIOUS/from-version")"
	TARGET="$(cat "$PREVIOUS/target-version")"
	[ -s "$dump" ] || die "the dump ${dump} is missing or empty."

	acquire_lock
	trap release_lock EXIT

	echo "This replaces the WHOLE database with the dump taken before the update to ${TARGET}:"
	echo "  ${dump}"
	echo "Receipts, sign-ins and settings changed since then are lost, and the server returns to ${from}."
	local answer=""
	read -r -p "Type RESTORE to continue: " answer
	if [ "$answer" != "RESTORE" ]; then
		echo "Nothing was changed."
		return
	fi

	log "Stopping the backend and identity provider"
	stop_service backend
	stop_service gotrue

	log "Reloading the database"
	# shellcheck disable=SC2016
	docker compose exec -T postgres sh -c '
		set -e
		psql -U "$POSTGRES_USER" -d postgres -v ON_ERROR_STOP=1 -c "DROP DATABASE IF EXISTS \"$POSTGRES_DB\" WITH (FORCE)"
		psql -U "$POSTGRES_USER" -d postgres -v ON_ERROR_STOP=1 -c "CREATE DATABASE \"$POSTGRES_DB\""
	' || die "the database could not be recreated. Nothing was reloaded; run --restore again."
	# shellcheck disable=SC2016
	docker compose exec -T postgres sh -c 'pg_restore -U "$POSTGRES_USER" -d "$POSTGRES_DB" --exit-on-error' <"$dump" ||
		die "the dump could not be reloaded. It is untouched; run --restore again."

	restore_files
	write_pin "$(cat "$PREVIOUS/pin")"

	log "Starting ${from}"
	docker compose up -d --no-deps backend gotrue || die "the database and files are restored, but the services could not be started."
	wait_for_backend "$from" || die "the database and files are restored, but ${from} did not become healthy. See: docker compose logs backend"

	local services
	services="$(docker compose config --services | tr -d '\r' | grep -vxE 'backend|gotrue')"
	# shellcheck disable=SC2086
	docker compose up -d --no-deps $services
	docker compose restart caddy >/dev/null

	set_state rolled-back "$TARGET" "Restored by hand from the dump taken before the update."
	echo "Restored. The server is on ${from} again."
}

# --- After a reboot ---------------------------------------------------------------------------------

# Run by the agent at boot. A process that was running before the reboot can't still be, so a `running`
# left behind would otherwise strand every client for half an hour.
after_boot() {
	[ -d "$WORK" ] || return 0
	rm -rf "$WORK/lock"
	if [ "$(last_state)" = "running" ]; then
		set_state failed "$(sed -n 2p "$WORK/state")" "The host restarted during the update. Check the stack with docker compose ps, then run ./scripts/update.sh again."
	fi
}

case "${1:-}" in
	"" | --bundle) update ;;
	--continue) continue_update ;;
	--restore) restore ;;
	--after-boot) after_boot ;;
	*)
		echo "usage: $0 [--bundle <file> | --restore]" >&2
		exit 2
		;;
esac
