# Troubleshooting

Start here:

```bash
curl http://localhost:8080/health/ready
```

Each dependency reports separately and by name, which is the whole point — the alternative is a stack
that is silently doing nothing for one of five different reasons. This page is organized by what that
answer says.

`Degraded` is not broken. It means the API works but something downstream of it does not yet.

## `postgres` — Unhealthy

The backend cannot reach the database.

```bash
docker compose ps postgres
docker compose logs postgres --tail 50
```

Most likely causes:

- **`POSTGRES_PASSWORD` was changed after the first start.** The volume was initialized with the old
  one, and Postgres does not re-read it. Either put the old value back, or start over with
  `docker compose down -v` — which deletes everything.
- **`POSTGRES_PASSWORD` is empty.** `scripts/setup.sh` generates it; a hand-written `.env` needs it
  filled in.
- **The volume was initialized by a different major version.** Changing `POSTGRES_VERSION` after the
  fact needs a dump and restore, not a restart.

## `migrations` — Unhealthy

The schema is behind the code. Migrations run at startup, so this means that ran and did not finish.

```bash
docker compose logs backend --tail 100
```

If the failure mentions `CREATE EXTENSION`, the database role cannot create extensions. On the bundled
Postgres it can, and `postgres/initdb/10-roles-and-extensions.sql` creates `citext` and `pgcrypto`
up front regardless — but that script **only runs on a fresh volume**. A database restored from
elsewhere, or one that predates that file, may be missing them:

```bash
docker compose exec postgres psql -U postgres -d kvitteria \
  -c 'CREATE EXTENSION IF NOT EXISTS citext; CREATE EXTENSION IF NOT EXISTS pgcrypto;'
docker compose restart backend
```

## `ollama` — Unhealthy

The model server is not answering.

```bash
docker compose logs ollama --tail 50
```

On a first start this is normal for a while: the container pulls a multi-gigabyte model before it
serves anything. Give it time and watch the log.

## `ollama-model` — Degraded

Ollama is up, but the model the pipeline is configured to use has not been pulled. The API is fine;
extraction would fail. The response lists what *is* pulled.

This is the classic mismatch: `.env` says one thing and the database row says another. Remember
`OLLAMA_MODEL` **seeds only** — on an existing install it changes what Ollama pulls, not what the
pipeline asks for.

Fix it at whichever end is wrong. To change what the pipeline asks for:

```bash
curl -X PUT http://localhost:8080/api/settings/pipeline \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{"ollamaModel":"qwen3.5:4b", ...}'
```

That verifies the model is pulled before saving, so it cannot introduce this state. Or pull what the
database already wants:

```bash
docker compose exec ollama ollama pull <the model the health check named>
```

## `household` — Degraded

Nobody has claimed the household. `./scripts/setup.sh` creates the admin before it opens the entrypoint, so
this means you started the stack some other way, or setup stopped before the claim. Run the script again and it asks
for the admin's email and password. By hand:

```bash
./scripts/token.sh you@example.com 'a-good-password' --signup
```

Until that happens the first person to sign in becomes the admin, so keep the stack off the network.

## A certificate is not issued (public-domain mode)

Caddy obtains the certificate on first use, and a failure shows only in its log:

```bash
docker compose logs caddy | grep -iE "acme|certificate|error"
```

Look for `obtaining certificate` followed by an error. The usual causes are a DNS record that does not point at this
host (check with `dig +short your.domain`), port 80 closed or already used by another service (the challenge arrives
on 80 even though clients use 443), or hitting Let's Encrypt's rate limits after many failed attempts. Fix the cause
and `docker compose restart caddy`.

## Sign-in answers 429, or "rate limiting is not applied"

A burst of password attempts from one address gets 429 for a while; wait a few minutes. If everyone behind one proxy is
limited together, the proxy has a public address Caddy does not trust: set `TRUSTED_PROXIES` to its range.

If GoTrue logs `request does not have a value for the rate limiting header, rate limiting is not applied`, that request
arrived without `X-Real-IP` and was not limited. Something is reaching GoTrue without going through Caddy, usually its
published development port (the backend's own claim during setup does this once, by design):

```bash
docker compose logs gotrue --tail 100 | grep -i "rate limit"
```

## Everything stopped working after changing `PUBLIC_URL`

Tokens already issued name the old issuer, so the backend answers 401 to every one of them. Sign in again from each
client. Also check that `.env` does not still pin `GOTRUE_SITE_URL` or `GOTRUE_URI_ALLOW_LIST` to the old address: a
pinned value wins over `PUBLIC_URL`. See [Modes](configuration.md#modes).

## Everything reports healthy, but no receipts appear

The pipeline is fine; the mailbox is the question.

```bash
docker compose logs -f backend
```

Each mailbox records its own outcome, so ask it directly:

```bash
curl -H "Authorization: Bearer $TOKEN" http://localhost:8080/api/mailboxes
```

`lastPolledAt`, `lastError` and `consecutiveFailures` are per mailbox, and one broken mailbox does not
stop the others. Things to check:

- **`isEnabled` is false.** Disabled mailboxes are not polled.
- **Messages are already flagged read.** Only unseen messages are picked up. Mark a receipt unread to
  reprocess it.
- **Wrong folder.** A mailbox write is verified against the real server, and a bad folder name comes
  back with the list of folders that do exist — so this fails at the point you save it, not silently
  later.
- **`lastError` mentions the password.** If the Data Protection keys and the stored ciphertext have
  come apart — restoring one without the other — the error asks you to re-enter it. Do that; it cannot
  be recovered.

## A receipt mail or photo never became a receipt

Mail and uploaded images that fail are kept, with the reason, until someone deals with it. Look at the
stages and the failures:

```bash
curl -H "Authorization: Bearer $TOKEN" http://localhost:8080/api/pipeline/status
curl -H "Authorization: Bearer $TOKEN" http://localhost:8080/api/pipeline/failures
```

`status` counts mail and uploads per stage (`queued`, `extracting`, `persisting`, `failed`) plus
`unreconciled` receipts, and gives per-mailbox `lastMessageAt` and `receiptsThisMonth`. An image being
transcribed counts as `extracting`. Each failure names the underlying cause in `error` (an unreachable
Ollama, an unpulled model, unusable output), and `source` says whether it was `Mail` or an `Upload`. The
mail text, transcription and image are never returned. In the backend log an upload is named
`upload <id>` where a mail is named by its subject.

An upload that fails with `No receipt was found in the image.` was read and judged not to be a receipt.
Retrying reads it again; dismissing deletes the image.

Once the cause is fixed, retry it, which needs `EditReceipts`:

```bash
curl -X POST -H "Authorization: Bearer $TOKEN" http://localhost:8080/api/pipeline/failures/<id>/retry
```

A mail that was never a receipt can be dismissed, which deletes it and its text, or for an upload its
image and transcription. Nothing changes on the mail server:

```bash
curl -X DELETE -H "Authorization: Bearer $TOKEN" http://localhost:8080/api/pipeline/failures/<id>
```

Restarting the backend is safe: unfinished mail resumes by itself. Receipts that extracted but did not
reconcile are not failures and cannot be retried; list them with
`/api/receipts?reconciled=false` and correct them by hand.

**A failure ending in `Ollama request timed out`** means generation was too slow, or stuck. A 20-line
receipt takes about four minutes on four CPU cores. Check the thread count against the cores Ollama may
actually use:

```bash
docker compose logs ollama | grep n_threads
docker compose exec ollama nproc
```

The backend limits the threads itself when it runs on fewer CPUs than the machine has, so these should
match. More threads than CPUs slows generation about twentyfold, and raising
`OLLAMA_REQUEST_TIMEOUT_SECONDS` only hides that.

**Mail lost before this feature existed** left no record anywhere. Mark it unread in the mail client
and the next poll processes it as new, because nothing stored matches its message ID. The same mail
marked unread after its receipt exists is skipped, not duplicated.

## 401 on every API call

Two likely causes, and the health checks cannot see either.

**The token is for a different issuer.** `PUBLIC_URL` becomes the `iss` claim that both GoTrue stamps
and the backend validates. If you changed it after signing in, existing tokens no longer match — get a
new one. Confirm what the backend expects:

```bash
docker compose exec backend printenv Auth__Issuer
docker compose exec gotrue printenv GOTRUE_JWT_ISSUER
```

Those two must be byte-identical.

**The key fetch is failing.** It happens lazily on the first authentication, so it presents as a 401
rather than a startup error.

```bash
docker compose logs backend --tail 50 | grep -i jwks
curl http://localhost:8080/auth/v1/.well-known/jwks.json
```

If that serves keys but authentication still fails, check `AUTH_ALLOW_INSECURE_JWKS_TRANSPORT` — the
internal fetch is plain HTTP and needs it to be `true`.

## 403 with a valid token

Authenticated, but not a member of this household. Somebody has to pre-authorize the address:

```bash
curl -X POST http://localhost:8080/api/users \
  -H "Authorization: Bearer $ADMIN_TOKEN" -H 'Content-Type: application/json' \
  -d '{"email":"them@example.com","role":"Member"}'
```

If it is a *write* that 403s while reads work, it is a permission rather than membership — reads are
never restricted. Check what you hold at `GET /api/users/me`, and what members are granted at
`GET /api/settings/permissions`. Permission changes take effect within a minute without a new token.

## A failed update

`./scripts/update.sh` says what happened as it stops, and the same reason is in
`update-state/status.json`. The state there is one of these:

**`failed`, the stack kept running.** A download, a checksum, a pulled image or a refusal stopped the update
before anything changed, or the database backup failed and the backend was started again on its old
version. Nothing needs undoing. Fix the cause, which the message names, and run it again.

**`failed`, naming the new version.** The new backend works but another service wouldn't start. `.env` is
already pinned to the new version. Run `docker compose up -d` and read what it reports.

**`failed`, "the host restarted during the update".** The update was interrupted and may have stopped part way.
Look at `docker compose ps`, start anything that's down with `docker compose up -d`, and run the update
again. A backend stopped by the update stays stopped after a reboot.

**`failed`, "rolling back did not bring the backend up".** The new version failed, the files and pin were put
back, and the old version didn't return either. That points at something other than the release, so start
with `docker compose logs backend`.

**`rolled-back`.** The new version didn't become healthy within five minutes, no migration had applied, and the
previous version is running again. Nothing is lost. `docker compose logs backend` shows why the new version
failed only until the containers are replaced, so look before retrying. After `--restore` this is also the state,
with a detail saying so.

**`restore-required`.** A migration applied and the new version still didn't become healthy, so the old version
can't run against this database. The backend is stopped, the entrypoint stays up and the app explains this on
its opening screen. No further update will run until this is resolved. Normally:

```bash
./scripts/update.sh --restore
```

That replaces the whole database, sign-ins included, with the dump taken before the update, and puts back the
files and the version pin. Anything that arrived after the dump is gone, though the backend was stopped before it
was taken, so that's only what happened during the failed update. It asks you to type `RESTORE` and refuses to
run without a terminal. The dumps are in `.update/dumps` and what was replaced is in `.update/previous`.

**The app says the update didn't start.** The agent didn't pick the request up within a minute. Check it with
`systemctl status kvitteria-update.path` and `journalctl -u kvitteria-update.service`, or run `./scripts/update.sh`
on the server.

**The app offers the command rather than a button.** The server has no live agent: it's not a Linux host with
systemd, `setup.sh` wasn't run as root, or `update-state/heartbeat` is over fifteen minutes old.

## Starting over

```bash
docker compose down -v
./scripts/setup.sh
```

This deletes the database: receipts, members, identities, and the keys protecting your stored mailbox
passwords. Take a `pg_dump` first if any of that matters.

## The Android app

**"Nothing answered at that address."** The app couldn't reach `/version`. From an emulator the host is
`10.0.2.2`, not `localhost`. A release build also lands here for any `http` address, because it refuses
cleartext traffic, so point it at the stack over TLS (a public domain or a proxy; see
[Modes](configuration.md#modes)) or use a debug build.

**"Something answered at that address, but it was not a Kvitteria server."** `/version` answered with
something else, which usually means the port belongs to another service. Use the Caddy port, which is
8080 by default.

**The app shows the sign-in screen after a restart.** The stored session couldn't be decrypted or the
identity provider rejected its refresh token, and both are treated as signed out. Losing connection is
not: the gate offers a retry and keeps the session.

**A signed-in account sees "Ingen adgang".** The account exists at the identity provider, and no household
member has that address. An admin invites that exact address under Settings, and the same login then works.

**A build fails in `spotlessCheck`.** A Kotlin file is missing its MPL header. `./gradlew spotlessApply`
adds it.