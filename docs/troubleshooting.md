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

Nobody has claimed the household. This is how every fresh install starts, and it resolves itself the
moment somebody signs in:

```bash
./scripts/token.sh you@example.com 'a-good-password' --signup
```

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

## Starting over

```bash
docker compose down -v
./scripts/setup.sh
```

This deletes the database: receipts, members, identities, and the keys protecting your stored mailbox
passwords. Take a `pg_dump` first if any of that matters.
