# Kvitteria

Self-hosted grocery-receipt bookkeeping for a Danish household.

Kvitteria watches a mailbox for the receipts Danish supermarkets email you, reads each one with a
local language model, and turns it into structured line items you can categorize. Over time it learns
your household's names for things, so the second receipt from the same shop mostly categorizes
itself.

Everything runs on your own machine. No receipt data, and no mailbox password, leaves it.

> **Pre-1.0.** The schema and the API still move. Version `0.x` means a minor bump can break things —
> read the release notes before upgrading, and pin an exact version if that matters to you.

## What it actually does

```
mailbox (IMAP) ──▶ extraction (Ollama) ──▶ Postgres ──▶ API
                        │
                        └─ retries until the arithmetic reconciles
```

1. **Polls your mailbox.** Each household member registers their own IMAP folder. Unseen messages are
   read, converted to plain text, and queued — and only marked read once they are safely queued, so a
   crash never loses a receipt.
2. **Extracts line items** with a local model, constrained to a JSON schema. The prompt encodes rules
   specific to Danish receipts: printed amounts are line totals rather than unit prices, `HERAF PANT
   IALT` and `HERAF MOMS` order their labels before their amounts, decimals use commas.
3. **Checks its own work.** An extraction counts as successful only when
   `grand total − sum(lines) − pant` comes to zero within a øre. If it does not, the model is asked
   again and told how far off it was. This is the difference between "plausible-looking" and "right".
4. **Stores and links.** Line items are matched against canonical items by their normalized printed
   name, so categorizing *Arla Letmælk 1L* once teaches every future receipt that mentions it.

## What you need

**Docker.** That is the whole list.

Extraction is heavy: roughly two to three minutes per receipt on CPU. It works, but if you plan to use
this properly, a GPU is worth setting up — see [GPU](#gpu) below.

## Install

```bash
git clone https://github.com/GlitzyWorm/Kvitteria.git
cd Kvitteria
./scripts/setup.sh
```

On Windows, `.\scripts\setup.ps1` instead.

The script writes a `.env` with a generated database password and signing key, then starts everything
and waits until it reports ready. The first run downloads a multi-gigabyte model, so give it time.

Prefer to do it yourself? `cp .env.example .env`, fill in `POSTGRES_PASSWORD`, `GOTRUE_JWT_SECRET` and
`GOTRUE_JWT_KEYS`, then `docker compose up -d`. Every variable is documented in
[docs/configuration.md](docs/configuration.md).

Everything is served from one origin, `http://localhost:8080` by default:

| Path | What |
|---|---|
| `/api/*` | the application |
| `/auth/v1/*` | sign-up and sign-in |
| `/health/live`, `/health/ready` | is it working, and if not, which part |
| `/version` | which build is running |
| `/` | reserved for the web app |

## First run

**The first account to sign in claims the household as its admin.** There is no setup wizard and no
bootstrap password — until somebody signs in, there is nothing to protect, and afterwards nobody new
gets in unless an admin pre-authorizes their email.

```bash
./scripts/token.sh you@example.com 'a-good-password' --signup
```

That prints an access token. Keep it — there is no web app yet, so it is how you talk to the API.

Then tell Kvitteria where your receipts are:

```bash
curl -X POST http://localhost:8080/api/mailboxes \
  -H "Authorization: Bearer $TOKEN" \
  -H 'Content-Type: application/json' \
  -d '{
    "host": "imap.example.com", "port": 993, "useSsl": true,
    "username": "you@example.com", "password": "your-imap-password",
    "folder": "INBOX/Kvitteringer", "isEnabled": true
  }'
```

The settings are verified against the real server before anything is saved, so a wrong password fails
immediately with the reason rather than silently once a minute forever. Name a folder that does not
exist and the answer lists the folders that do.

Watch it work:

```bash
docker compose logs -f backend
```

### Adding other people

An admin pre-authorizes an email address, and tells that person to sign up. **Nothing is emailed** —
there is no SMTP anywhere in this project, which is also why no mail configuration is needed.

```bash
curl -X POST http://localhost:8080/api/users \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{"email":"partner@example.com","displayName":"Partner","role":"Member"}'
```

They sign up with that address and are bound to the row waiting for them. Anybody else who signs up
is authenticated but not a member, and gets a 403.

## Using it

Reading is never restricted: the household sees everything. Writes need a permission, and admins hold
all of them. What members may do is one setting an admin edits at
`PUT /api/settings/permissions`.

| Permission | Covers |
|---|---|
| `CategorizeLines` | assigning a category, meal or canonical item to a receipt line |
| `ManageTaxonomy` | editing the categories, meals and canonical items themselves |
| `EditReceipts` | correcting a receipt or its lines |
| `ManageUsers` | pre-authorizing and disabling members |
| `ManageSettings` | retuning the extraction pipeline |
| `ManageMailboxes` | registering mailboxes — separate from the above, so somebody can add their own without also retuning extraction |

The everyday loop is categorization:

```bash
# What came in
curl -H "Authorization: Bearer $TOKEN" http://localhost:8080/api/receipts

# Teach it what a line is. setAsDefault also writes it back to the canonical item, so the next
# receipt mentioning the same product needs no help.
curl -X PUT http://localhost:8080/api/receipt-lines/$LINE_ID/category \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{"categoryId":"'$CATEGORY_ID'","setAsDefault":true}'
```

Categories are a two-level tree, meals are seeded with a Danish taxonomy, and receipts can record how
many days they cover for a given meal. Full surface: run with
`ASPNETCORE_ENVIRONMENT=Development` and open `http://localhost:8080/scalar`.

### Tuning extraction

Poll interval, retry count, reconciliation tolerance and which model to use are all live settings at
`GET`/`PUT /api/settings/pipeline` — no restart. Changing the model verifies it is actually pulled
first, so a typo cannot quietly break every future extraction.

## Diagnosing

`/health/ready` names each dependency separately, which is the fastest way to find out what is wrong:

```bash
curl http://localhost:8080/health/ready
```

```json
{
  "status": "Degraded",
  "checks": [
    { "name": "postgres", "status": "Healthy" },
    { "name": "migrations", "status": "Healthy" },
    { "name": "ollama", "status": "Healthy" },
    { "name": "ollama-model", "status": "Healthy" },
    { "name": "household", "status": "Degraded",
      "description": "No active admin yet; the first person to sign in claims the household." }
  ]
}
```

`Degraded` is not broken. An unclaimed household is how every install starts, and an unpulled model
leaves the API perfectly usable — it just cannot extract anything yet. See
[docs/troubleshooting.md](docs/troubleshooting.md) for what to do about each one.

## GPU

Extraction on CPU is slow enough to be annoying. With an NVIDIA GPU and the Container Toolkit
installed, uncomment `COMPOSE_FILE` in `.env` and restart:

```bash
docker compose up -d
```

Check the host can do it at all with `docker run --rm --gpus all ollama/ollama nvidia-smi`.

## Upgrading

```bash
docker compose pull && docker compose up -d
```

Migrations are applied on start, so there is no separate step. Read the release notes first while this
is pre-1.0 — and note that `BACKEND_VERSION=latest` moves across minor versions, so pin an exact
version in `.env` if you would rather choose when that happens.

## Backups

One database holds everything: your receipts, your household, the identities you sign in with, **and
the keys that encrypt your stored mailbox passwords**.

```bash
docker compose exec -T postgres pg_dump -U postgres kvitteria > kvitteria.sql
```

That single dump covers all of it. Restoring anything less than the whole thing means every mailbox
password has to be entered again.

## Mobile app

There is a .NET MAUI app in `Kvitteria/MobileApp/`, targeting **Android** and **macOS** (Mac
Catalyst). It is scaffolding at this point — it builds and runs, and does nothing useful yet. Nothing
about installing or using the stack depends on it.

It is a plain HTTP client of the backend, pointed at the same Caddy entrypoint a browser would use.
Emulator and simulator only for now: a physical device needs the stack served over TLS, which it
currently is not. Building it is covered in [docs/development.md](docs/development.md).

## Contributing

`docker compose` in a checkout picks up `compose.override.yaml` automatically, which builds the
backend from source instead of pulling a published image. See
[docs/development.md](docs/development.md).

Commit messages follow [Conventional Commits](https://www.conventionalcommits.org/) — CI enforces it,
and the changelog and version bumps are derived from them.

## License

Three licenses, by directory:

| Path | License |
|---|---|
| Everything not listed below, including the backend | [AGPL-3.0-or-later](LICENSE) |
| `Kvitteria/Contracts/` | [MIT](Kvitteria/Contracts/LICENSE) |
| `Kvitteria/MobileApp/` | [MPL-2.0](Kvitteria/MobileApp/LICENSE) |

GitHub reads the root `LICENSE` and labels the whole repository AGPL-3.0, so this table is the only
place the split is visible.

The backend is AGPL because it is the part someone could run as a hosted service, and §13 is the
clause that makes them publish their changes. The app is MPL-2.0 because app store terms conflict
with GPLv3 §6 and §10 — VLC was pulled from the iOS App Store over exactly that. The shared contracts
are MIT so that neither license reaches the other through the code both sides compile.

Releases up to and including `backend-v1.0.0` were AGPL-3.0-or-later in full.
