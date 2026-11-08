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
mailbox (IMAP) ──────────────────────────▶ extraction (Ollama) ──▶ Postgres ──▶ API
photo or screenshot ──▶ transcription ──┘         │
                        (Ollama, vision)          └─ retries until the arithmetic reconciles
```

1. **Polls your mailbox.** Each household member registers their own IMAP folder. Unseen messages are
   read, converted to plain text, and queued — and only marked read once they are safely queued, so a
   crash never loses a receipt.
   **Or takes a picture.** A paper slip or a store app's receipt screen can be scanned, picked or shared
   into the Android app, which uploads it. The model writes the image out as text once, and that text
   goes through the same extraction. The image is kept with the receipt it became.
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

The script asks for the address clients will use (press Enter for `http://localhost:8080`) and, for an
`https://` address, whether a proxy you run already terminates TLS. It writes a `.env` with a generated database
password and signing keys, asks for the **first admin's email and password**, and starts the backend and identity
provider with the entrypoint still off. It creates the admin, confirms the household is claimed, and only then starts
the rest. The first run downloads a multi-gigabyte model, so give it time. For an unattended run, set
`KVITTERIA_PUBLIC_URL`, `KVITTERIA_TLS` (`proxy` or `domain`), `KVITTERIA_ADMIN_EMAIL` and
`KVITTERIA_ADMIN_PASSWORD` instead of answering.

Running it again is safe: it leaves `.env` alone and asks for nothing once the household has an admin.

### Modes

| You have | `PUBLIC_URL` | `COMPOSE_FILE` |
|---|---|---|
| Just this machine, or an emulator | `http://localhost:8080` | unset |
| A proxy that already does HTTPS (Tailscale serve, Cloudflare Tunnel, Traefik, ...) | `https://...` | `compose.yaml` |
| A domain pointing at this host, ports 80 and 443 open | `https://...` | `compose.yaml:compose.public.yaml` |

With a domain, Caddy gets and renews a certificate itself. A physical phone needs one of the two HTTPS modes, because a
release build of the Android app refuses plain HTTP. Rate limits, proxy recipes and moving an existing install are in
[docs/configuration.md](docs/configuration.md#modes).

Prefer to do it yourself? `cp .env.example .env`, fill in `POSTGRES_PASSWORD`, `GOTRUE_JWT_SECRET` and
`GOTRUE_JWT_KEYS`, then `docker compose up -d`. Every variable is documented in
[docs/configuration.md](docs/configuration.md).

> **Skipping the script leaves the first-caller race.** On an empty household the first person to sign in becomes its
> admin, so a stack you start by hand must not be reachable from outside until you have signed in yourself (below).

Everything is served from one origin, `http://localhost:8080` by default:

| Path | What |
|---|---|
| `/api/*` | the application |
| `/auth/v1/*` | sign-up and sign-in |
| `/health/live`, `/health/ready` | is it working, and if not, which part |
| `/version` | which build is running |
| `/` | reserved for the web app |

## First run

The setup script has already created the first admin, and **the first account to sign in claims the household as its
admin**. If you started the stack by hand, do this now, before anything outside your machine can reach it: whoever signs
in first gets the household. Afterwards nobody new gets in unless an admin pre-authorizes their email.

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
| `AddReceipts` | uploading a photo or screenshot of a receipt; members have it by default |

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

Sorting a line that has no canonical item yet takes one request. The queue lists what is waiting, and
the save assigns the item, category and meal together; `rememberAlias` also resolves the same name on
other waiting lines, and `setAsDefaults` writes the item's defaults. Creating an item or writing
defaults needs `ManageTaxonomy` too.

```bash
# How many lines are waiting, and the first page of them
curl -H "Authorization: Bearer $TOKEN" http://localhost:8080/api/receipt-lines/unassigned/count
curl -H "Authorization: Bearer $TOKEN" 'http://localhost:8080/api/receipt-lines/unassigned?page=1&pageSize=20'

# Sort one: a new item, remembered for next time, with its defaults
curl -X POST http://localhost:8080/api/receipt-lines/$LINE_ID/sort \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{"newItemName":"Mælk","categoryId":"'$CATEGORY_ID'","mealId":null,"rememberAlias":true,"setAsDefaults":true}'
```

Month figures come from `/api/insights`, which are plain reads. Months are Danish calendar months written
`yyyy-MM`, and leaving `month` out means the current one. A request with a bad month or more than 24 months
answers 400.

```bash
# What was spent in a month, against the month before. total includes pant; linesTotal does not.
curl -H "Authorization: Bearer $TOKEN" 'http://localhost:8080/api/insights/summary?month=2026-08'

# Where it went, by top-level category, compared with another month; add parentId to drill into one
curl -H "Authorization: Bearer $TOKEN" 'http://localhost:8080/api/insights/categories?month=2026-08&compareTo=2026-07'
curl -H "Authorization: Bearer $TOKEN" "http://localhost:8080/api/insights/categories?month=2026-08&parentId=$CATEGORY_ID"

# Six months by default, up to 24
curl -H "Authorization: Bearer $TOKEN" 'http://localhost:8080/api/insights/trend?endMonth=2026-08&months=6'

# Items you have bought, and what one cost per unit month by month
curl -H "Authorization: Bearer $TOKEN" 'http://localhost:8080/api/insights/prices?page=1&pageSize=20'
curl -H "Authorization: Bearer $TOKEN" "http://localhost:8080/api/insights/prices/$ITEM_ID?months=12"

# Spend, days covered and cost per day for meals that track days
curl -H "Authorization: Bearer $TOKEN" 'http://localhost:8080/api/insights/meal-coverage?months=6'
```

The receipt list can be searched. `search` matches, ignoring case, part of the merchant, the member, a
printed line or a line's item name, and a number also matches by total: `284,50` (or `284.50`) is that exact
amount and `284` is anything from 284.00 to 284.99. It combines with the other filters.

```bash
curl -H "Authorization: Bearer $TOKEN" 'http://localhost:8080/api/receipts?search=f%C3%B8tex&from=2026-07-01T00:00:00Z'
curl -H "Authorization: Bearer $TOKEN" 'http://localhost:8080/api/receipts?search=284,50'
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
./scripts/update.sh
```

That moves the stack to the newest release: it backs up the database, replaces the compose files, the
Caddyfile and the scripts, pins the exact version in `.env` and checks that the new backend works. The
server is offline for a minute or two. Migrations only go forward, so it won't install anything older
than what's running, and it stops if a release would change the Postgres major version. Read the release
notes first.

If the new version doesn't come up, it goes back to the old one by itself. If a migration had already
changed the database by then, it stops and leaves the backend down, because only the backup can undo that:

```bash
./scripts/update.sh --restore
```

That replaces the whole database with the dump taken before the update, so it asks first and needs a
terminal. See [troubleshooting](docs/troubleshooting.md#a-failed-update) for what each outcome means. The
last three dumps are kept in `.update/dumps`.

Without internet access on the server, download `kvitteria-stack-<version>.tar.gz` from the release and
pass it with `--bundle <file>`.

On a Linux host with systemd, running `./scripts/setup.sh` as root also installs a small agent, so someone
with `ManageSettings` can start the update from the app. See
[configuration](docs/configuration.md#updates).

**On Windows** the script needs bash, so run it from Git Bash or WSL. There's no agent on Docker Desktop:
the app tells you a release exists and shows the command.

A git checkout isn't updated this way. Pull and rebuild with `docker compose up -d --build`.

## Backups

One database holds everything: your receipts and the images they were read from, your household, the
identities you sign in with, **and the keys that encrypt your stored mailbox passwords**.

```bash
docker compose exec -T postgres pg_dump -U postgres kvitteria > kvitteria.sql
```

That single dump covers all of it. Restoring anything less than the whole thing means every mailbox
password has to be entered again.

## Android app

The client is a native Android app in `android/`, built with Jetpack Compose. It covers the month's
overview, sorting lines, receipts, insights and the pipeline, along with the household's members,
permissions, mailboxes and settings. It is Danish first, with English as a choice. Nothing about
installing or using the stack depends on it.

It is a plain HTTP client of the backend, pointed at the same Caddy entrypoint a browser would use.
A release build refuses plain HTTP, so a physical device needs one of the two HTTPS [modes](#modes); a debug build on an
emulator reaches a local stack at `http://10.0.2.2:8080`. Building it is covered in
[docs/development.md](docs/development.md).
## Contributing

`docker compose` in a checkout picks up `compose.override.yaml` automatically, which builds the
backend from source instead of pulling a published image. See
[docs/development.md](docs/development.md).

Commit messages follow [Conventional Commits](https://www.conventionalcommits.org/) — CI enforces it,
and the changelog and version bumps are derived from them.

## License

Two licenses, by directory:

| Path | License |
|---|---|
| Everything not listed below, including the backend | [AGPL-3.0-or-later](LICENSE) |
| `android/` | [MPL-2.0](android/LICENSE) |

GitHub reads the root `LICENSE` and labels the whole repository AGPL-3.0, so this table is the only
place the split is visible.

The backend is AGPL because it is the part someone could run as a hosted service, and §13 is the
clause that makes them publish their changes. The app is MPL-2.0 because app store terms conflict
with GPLv3 §6 and §10 — VLC was pulled from the iOS App Store over exactly that. The two share no code.
Releases up to and including `backend-v1.0.0` were AGPL-3.0-or-later in full.
