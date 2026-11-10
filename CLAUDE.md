# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

Kvitteria ingests Danish grocery-receipt emails, extracts them into structured line items with a
local LLM, persists them to Postgres, and exposes a minimal-API surface for categorizing lines
(canonical items, categories, meals).

Two projects in `Kvitteria/Kvitteria.slnx`, both **.NET 10**, and the Android app beside them:

```
Kvitteria/Backend        ASP.NET Core, the whole server    net10.0
Kvitteria/Backend.Tests  xUnit over the pure logic         net10.0
android/                 Compose, the only client          Gradle, min SDK 26
```

The app and the server share **no code**. The app talks to the server over HTTP, and
`docs/openapi/kvitteria.json` is the contract between them, kept honest by a check on each side (see
[The contract](#the-contract)). An Angular app goes at the repo root beside `caddy/`.

`Kvitteria/` is the .NET root and holds `Directory.Build.props`, `Directory.Packages.props` and
`global.json`; it is also the Docker build context. `android/` sits at the repo root for that reason.
User-facing documentation lives in `README.md` and `docs/` (`configuration.md`, `troubleshooting.md`,
`development.md`). This file is the internal counterpart — keep the rationale here and the
instructions there rather than duplicating either.

## Licensing

Two licenses, one per subtree, and the arrangement constrains the code:

- **`LICENSE`, AGPL-3.0-or-later** — the backend and everything not listed below. §13 is the point:
  the backend is the part someone could run as a service, and that clause makes them publish changes.
- **`android/LICENSE`, MPL-2.0** — app store terms conflict with GPLv3 §6 (installation information) and
  §10 (no further restrictions). MPL is App Store compatible in practice: Firefox ships under it.

Two rules follow, and breaking either silently collapses the split:

1. **`android/` must never contain or depend on backend code.** They are separate programs over HTTP.
   The wire types are written again on the app's side for exactly this reason.
2. **Every Kotlin source file under `android/` carries the MPL Exhibit A header.** MPL's copyleft is per
   file and attaches through that notice, so a file without it is not clearly covered. Spotless enforces
   it: `./gradlew spotlessCheck` fails naming the file. Resource XML headers are manual.

The bundled typefaces are the third-party licences that need a notice to travel with the binary, because
someone installing the app receives a binary rather than this repository. Montserrat and Poppins are under
the SIL OFL 1.1, Phosphor under MIT. All three notices and the app's own MPL sit in
`feature/settings/presentation/src/main/assets/licenses`, and the licences screen renders them alongside
the AboutLibraries list, which picks up every dependency without a manual edit. No dependency may be GPL or
AGPL.

Relicensing is currently free: `git log` shows one human author. That stops being true the moment an
outside contribution lands without a CLA, or an AGPL/GPL third-party package enters the app.
## Commands

Everything runs in Docker, from the repo root where `compose.yaml` lives. There is no second
orchestration tool and no host-side database: Postgres, GoTrue, Ollama, the backend and a Caddy
entrypoint are all services in this compose project.

```bash
./scripts/setup.sh
```

That writes `.env` (generating the Postgres password, `GOTRUE_JWT_SECRET`, and the ES256
`GOTRUE_JWT_KEYS`), asks for the public address and mode, and creates the first admin **before the
entrypoint starts**. `scripts/setup.ps1` is the Windows equivalent. Re-running it leaves an existing `.env`
alone and asks for nothing once the household has an admin.

The order is the safety property, so keep it. Caddy `depends_on` a healthy backend, so a plain
`docker compose up -d` opens the entrypoint on an unclaimed household for the seconds a claim takes, and
that window is the whole first-caller race. The script therefore runs `up -d --wait backend gotrue` (Caddy
stays down, so no network port is open in an HTTPS mode), claims through `docker compose exec -T backend
curl`, re-reads the `household` check, and only then runs `up -d`. Going through `exec` means the claim
needs neither DNS, a certificate nor the deployer's proxy. The credentials reach `curl` on stdin
(`--data-binary @-`, and `-H @-` for the bearer), so the password is never an argument of any process;
a `user_already_exists` answer falls back to the password grant, which is how an interrupted run
completes. A deployer who skips the script keeps the race, and the README says so beside the manual route.

After that, plain compose:

```bash
docker compose up -d --build
```

A checkout picks up `compose.override.yaml` automatically, which builds the backend from source
rather than pulling from GHCR, sets `ASPNETCORE_ENVIRONMENT=Development`, and publishes the ports
host-side tooling needs — Postgres on 54322, GoTrue on 9999, the backend directly on 8081 bypassing
Caddy. `compose.gpu.yaml` is a third overlay for GPU passthrough, opted into via `COMPOSE_FILE`
in `.env`.

**Caddy is the single entrypoint**, on `http://localhost:8080` by default; see [Modes](#modes) for the
HTTPS alternatives. It routes `/auth/v1/*` to
GoTrue with the prefix stripped (standalone GoTrue serves at the root; in a Supabase project Kong is
what adds that prefix), `/api/*` and `/health/*` and `/version` to the backend, and `/` to a future
Angular build mounted at `/srv`. Scalar is at `/scalar` and the OpenAPI document at
`/openapi/v1.json`, both Development-only.

### Modes

`PUBLIC_URL` says where the stack is and `COMPOSE_FILE` says who terminates TLS, because a domain and a
proxy both have an `https://` address:

| Mode | `PUBLIC_URL` | `COMPOSE_FILE` |
|---|---|---|
| Local | `http://localhost:8080` | unset |
| Behind a proxy | `https://...` | `compose.yaml` |
| Public domain | `https://...` | `compose.yaml:compose.public.yaml` (`;` on Windows) |

The Caddyfile's site address is `{$SITE_ADDRESS::8080}`. A bare port does not qualify for a certificate, so
local and proxy modes attempt none and open no `:80` listener; `compose.public.yaml` sets `SITE_ADDRESS` to
the whole URL (compose interpolation cannot extract a host, and Caddy accepts a URL) and replaces the port
list with `ports: !override` (Compose 2.24.4+; without it compose appends and leaves 8080 published). Three
traps:

- **`auto_https off` is gone on purpose.** The option has no `on`, so a variable could never switch it.
- **`SITE_ADDRESS` must be unset, not empty, in the base file.** Caddy applies `{$VAR:default}` only to an
  unset variable; an empty one blanks the site address and the Caddyfile fails with "server block without any
  key". `compose.yaml` therefore does not mention it.
- **Both HTTPS modes set `COMPOSE_FILE`, which leaves `compose.override.yaml` out.** Otherwise a stack set up
  from a checkout runs in Development with Postgres and GoTrue published, and a published GoTrue port
  bypasses the client-address header and therefore the rate limit.

`GOTRUE_SITE_URL` and `GOTRUE_URI_ALLOW_LIST` default to `${PUBLIC_URL}` in `compose.yaml` and are no longer
in `.env.example`. **An existing `.env` that still pins them wins over `PUBLIC_URL`**, so moving off
localhost means deleting those two lines. Changing `PUBLIC_URL` changes the token issuer, so every client
signs in again.

Migrations are applied at startup by `DatabaseMigrator.MigrateDatabaseAsync`, called before the host
starts. To add one, from `Kvitteria`:

```bash
dotnet ef migrations add <Name> --project Backend
```

`DesignTimeDbContextFactory` keeps the tooling off the web host — otherwise scaffolding a migration
would apply one as a side effect, and the absent connection-string default would fail validation
first anyway. Commands that talk to a real database need `Database__ConnectionString` exported.

`Kvitteria/Backend.Tests` (xUnit) covers the pure logic that has no other safety net: the
reconciliation tolerance, the Ollama thread limit, Danish purchase-date parsing including the DST gap, `NormalizeString`
culture-independence, the `CategoryTree` depth/height/descendant rules, the meal-coverage diff, the
reorder guard, paging validation, the HTML-to-text converter, the user-provisioning decision,
effective permissions and the last-admin guard, permission-name parsing, the auth options rules,
mailbox write validation, the mailbox secret round-trip, the rate-limit partition key and trusted private ranges, the Danish month helpers and `MonthKey`, the
spending, category, price and meal-day rollups, receipt-search parsing, release selection and the update
status decisions, the JPEG header reader and image budget, the Danish day range, the no-receipt sentinel, the reconciliation states, Danish amounts, the printed purchase date, the chain match, the row mapper and the extraction schema. Run it from `Kvitteria`:

```bash
dotnet test Backend.Tests/Backend.Tests.csproj
```

The Android app has its own commands, from `android/`:

```bash
./gradlew spotlessCheck testDebugUnitTest test
./gradlew assembleDebug
```
There are no integration or endpoint tests of the backend. For anything touching HTTP or the database, verification
is still manual: bring the stack up and watch `docker compose logs -f backend` (Development log level
is `Debug`). `/health/ready` names each dependency separately, which is the fastest triage.

Manual API calls need a bearer token, and Scalar declares no security scheme so it cannot attach one:

```bash
./scripts/token.sh you@example.com 'password' --signup
```

## Configuration

Configuration is environment variables only, read from the gitignored `.env` beside `compose.yaml`
(`.env.example` is the checked-in reference). Nothing is layered on top of the chain
`WebApplication.CreateBuilder` assembles, deliberately: a JSON file added after it sits *above* the
environment provider and silently overrides whatever compose sets.

Options classes implement `Backend.Abstractions.IOptionsSection`, which declares a
`static abstract string SectionName` — the section name lives on the class, not at the registration
site. All three are registered with the `AddOptionsSection<T>(IConfiguration)` extension, which binds
the named section and adds `ValidateDataAnnotations().ValidateOnStart()`, so misconfiguration fails
at startup instead of inside the first poll. Sections: `Database` (`ConnectionString`), `Ollama`
(`BaseUrl`, `RequestTimeoutSeconds`, `DefaultModel`), `Auth`. These stay configuration on purpose:
each either breaks connectivity/security on a bad value, or is baked into an object built once at
startup (the `DbContext` factory, the typed `HttpClient`, the JWT bearer scheme).

**None of the addresses carry defaults.** A default pointing at localhost means a mistyped
`Database__ConnectionString` starts cleanly and dials 127.0.0.1, surfacing much later as a connection
error naming the wrong host; empty fails `ValidateOnStart` instead. `Auth:Audience` keeps its default
because `authenticated` is a constant of the identity provider rather than a deployment detail. The
cost is that a host-side `dotnet run` must export all of them — see `docs/development.md`.

`Ollama:DefaultModel` is the one value that spans configuration and settings. It seeds
`PipelineSettings.OllamaModel` when the row is created and is never read again; the row is
authoritative thereafter. Compose feeds the same `.env` variable to the Ollama container's `pull`, so
the pipeline cannot ask for a tag nobody pulled. On an existing install the seeder's emptiness guard
means editing it changes what Ollama pulls and nothing else.

Everything else that used to live in `Ollama`/`Imap` and is safe to change without a restart —
`Ollama:Model`, `ThinkingMode`, `NumCtx`, `KeepAlive`, `MaxExtractionAttempts`, `RetryTemperature`,
`ReconciliationToleranceKr`, and `Imap:PollIntervalSeconds`/`MaxMessagesPerPoll` — moved into
`PipelineSettings` (`Features/Persistence/Model/PipelineSettings.cs`), a single-row database table
in the same shape as `HouseholdSettings`, editable by an admin through
`GET`/`PUT /api/settings/pipeline` (`Features/Endpoints/SettingsEndpoints.cs`,
`PermissionPolicies.Name(Permission.ManageSettings)` on the `PUT`). There is no cache in front of it —
unlike `PermissionCache`, these values are read only a few times a minute (once per mail poll, once
per extraction attempt), so `MailWorker`, `MailFetcher`, `ExtractionWorker`, and `OllamaClient` each
query it fresh through a scoped `KvitteriaDbContext` (singletons go through `IServiceScopeFactory`).
Changing `Ollama:Model` validates the new value against Ollama's own `/api/tags`
(`OllamaClient.GetAvailableModelsAsync`) before saving, so a typo or an unpulled model can't silently
break every future extraction. `PipelineSettingsValidation.TryValidate` (`Features/Shared/`) enforces
the same numeric ranges the fields carried as `[Range]` attributes back when they were `Options`
classes.

### Mailboxes

Receipt folders are **not** configuration. `Mailbox`
(`Features/Persistence/Model/Mailbox.cs`) is one row per household member's IMAP folder — host, port,
TLS, credentials, folder name — owned by a real `AppUser` through a required FK, and managed over
`/api/mailboxes` (`Features/Endpoints/MailboxEndpoints.cs`) so a frontend can add one. This replaced
the `Imap:Folders` config list, whose entries carried plaintext credentials and a free-text member
name and email that linked to nothing.

Writes need the `ManageMailboxes` permission, deliberately separate from `ManageSettings` so a member
can register their own mailbox without also being able to retune the pipeline. Reads are ungated like
every other read, which is safe because the password is never in a DTO — `MailboxDto` carries only a
`HasPassword` flag.

The password is encrypted with ASP.NET Data Protection through `MailboxSecrets`
(`Features/Mail/MailboxSecrets.cs`), which owns the single purpose string. Deliberately **not** an EF
value converter: the plaintext should only materialize when writing the row and when authenticating,
not on every list query. `MailboxSecretException` wraps the `CryptographicException` a lost key ring
produces, so it surfaces as "re-enter the password" rather than a 500.

Every write verifies the settings against the real server before committing
(`ImapConnection.VerifyAsync`), the same way `PUT /settings/pipeline` verifies an Ollama model against
`/api/tags`, and answers 502 with the reason. A wrong folder name comes back with the server's own
folder list. The check is skipped when nothing connection-relevant changed, so toggling `IsEnabled`
costs no round-trip. `MailboxValidation.TryValidate` (`Features/Shared/`) does the completeness
checking that `ImapFolderOptions.IsComplete()` used to report from inside the poller — at the moment
of submission, rather than as a warning once a minute forever.

`ImapConnection` (`Features/Mail/`) holds the connect/authenticate/open sequence shared by the poller
and the verifying endpoints, including the `FolderNotFoundException` branch that enumerates
`PersonalNamespaces` into an "Available folders: ..." message.

## Pipeline architecture

Three `BackgroundService`s chained by two bounded queues. `ExtractionQueue` and `PersistenceQueue`
are both three-line subclasses of `WorkQueue<T>` (capacity 100, single reader, several writers: the poller, startup recovery and the retry endpoint all feed
`ExtractionQueue`, and `TryEnqueue` lets an HTTP handler refuse rather than wait); they
are separate named types rather than one `WorkQueue<ReceiptRecord>` because both carry the same `T`
and could not otherwise be resolved unambiguously from the container. A single mutable
`ReceiptRecord` instance flows the whole way through and accumulates state (`Status`, `Extracted`,
`Attempts`, `Reconciled`, `Error`).

**The queues are delivery, not storage.** `ReceiptIntake` is the source of truth: one row per mail from
fetch until it becomes a receipt, holding the mail text, with `Status` reusing `ReceiptStatus`
(`Queued`, `Extracting`, `Extracted`, `Failed`; `Completed` is never stored because a completed row is
deleted). `ReceiptIntakeStore` (singleton, own scope per call) owns every transition as a conditional
update on the expected status, so a duplicate enqueue, a retry racing recovery or a late write from a
worker that lost its row changes nothing. Three rules follow from it:

- **The row is written before the message is flagged `Seen`.** Previously `Seen` was set once the mail
  entered an in-memory queue, so a restart dropped everything still waiting, and extraction takes
  minutes per attempt. If recording throws, the mail stays unread and is fetched again.
- **Shutdown is not a failure.** `QueueWorker` records nothing on cancellation; the row stays in
  progress and `MailWorker` resets `Extracting`/`Extracted` to `Queued` and re-enqueues everything
  before its first poll. This assumes a single backend process.
- **Failure is recorded through `OnFailedAsync`**, with `CancellationToken.None` and its own
  `try`/`catch`, so a database outage cannot end a worker. `PipelineError.Describe` joins the
  `InnerException` chain, because the extraction loop's own message ("failed after N attempts") hides
  the cause.

### Image intake

An upload is a second source feeding the same pipeline. `POST /api/receipts/uploads`
(`Features/Endpoints/ReceiptUploadEndpoints.cs`) checks the JPEG against `ReceiptImageLimits`, asks Ollama
whether the configured model has `vision`, records image and intake in one `SaveChanges`
(`ReceiptIntakeStore.RecordUploadAsync`), and answers 202 once `TryEnqueue` takes it; a refused enqueue
deletes both rows and answers 503. `ReceiptIntake.Source` (`Mail`/`Upload`, stored as its name) is the only
thing that tells them apart downstream; an upload has empty `Subject`/`From`, no `SourceMessageId`,
`BodySource = "image"`, and `MailDate = CapturedAt ?? ReceivedAt`, which is how `PurchaseDate.Resolve`'s
existing fallback becomes "the capture time" with no code of its own. `ReceiptRecord.Label` is the
subject for mail and `upload <id>` for an upload, and every worker log line uses it.

`ExtractionWorker` transcribes an upload whose `BodyText` is empty (`OllamaClient.TranscribeAsync`, image in
the message's `images`, no `format`, temperature 0), stores the text with `SaveTranscriptionAsync` (only
while `Extracting`), then runs the unchanged retry loop with the same `ReceiptExtractionPrompt.ExtractionPrompt` a mail uses,
so the same printed receipt gives the same figures whichever way it arrived. So the image costs one vision call: a retry, a user retry and restart recovery
all reuse the stored text. Transcription is deliberately outside the extraction retry budget, and a
transport failure fails the intake with zero attempts. There is no new `ReceiptStatus`; transcribing is
`Extracting`. A model answering `NO_RECEIPT` (`ReceiptTranscription.IsNoReceipt`) fails the intake with a
fixed error and stores no text, so a retry reads the image again.

`ReceiptImage` is its own table, linked from the intake and then from the receipt (`Restrict` both ways;
ingestion copies the key in its transaction, dismiss and `DELETE /api/receipts/{id}` delete the row
explicitly). A table rather than a volume because files would be a second thing to back up and restore
in step with the database, which is the Data Protection key-ring trap again, and a table rather than a
column so the blob never rides along with a receipt or intake query. `Sha256` is uniquely indexed and the
409 for identical bytes comes from that index rejecting the insert, like `SourceMessageId`.

`ReceiptIngestionService` flags a possible duplicate for every source: the earliest receipt by
`CreatedAt`, then `Id`, with the same merchant, the same `GrandTotal` and a `PurchaseDate` inside
`DanishTime.DayRangeUtc`. `CreatedAt` exists because Npgsql's version 7 keys are random below the
millisecond, so `Id` alone is not insertion order. `PossibleDuplicateOfId` is a self-FK with `SetNull`,
which is what makes the flag disappear with the receipt it points to. The receipt is always stored.

`ExtractionPrompt` is tuned behaviour, evaluated on real receipts that
are **kept out of the repository**: they carry a loyalty number, store addresses and a cashier's name, and
the repository is public.

Unreconciled receipts are **not** failures and not retryable: they are already stored, may already be
categorized, and their mail text is gone. They are corrected by hand, and **the state is derived, not stored**:
`ReceiptReconciliation` (`Features/Receipts`) classifies the stored figures on every read as `Reconciled`,
`Accepted` or `Unreconciled`, with the tolerance currently in `PipelineSettings` (read once per request). The
rule exists as `Classify` (what the tests cover) and as the `NeedsAttention` EF expression behind
`GET /api/receipts?reconciled=false` and the pipeline status count, side by side in one file; the test
compiles the expression to hold them together. `Receipt.Reconciled` no longer exists, so a correction takes
effect by itself and a changed tolerance reaches old receipts. `Error` stays as what extraction reported.

A difference is accepted with `POST /receipts/{id}/accept-difference`, which states the difference the member
saw (409 when the figures moved since). The acceptance is four nullable columns and holds only while the
delta still equals `AcceptedDifference` exactly, so no write path has to clear it; a lapsed one stays in the
row, is never sent, and comes back if edits restore that exact difference. `DELETE` withdraws it. Figures are
corrected with `PATCH /receipts/{id}` (total and pant), `POST /receipts/{id}/lines` and `DELETE
/receipt-lines/{id}` (refused for the last line; clears orphaned meal coverage in a transaction), all
`EditReceipts`. `Receipt` carries `AppUserId` and `MailboxId` copied from the intake row (the
mailbox is `SetNull` on delete, the member `Restrict`), read at ingestion from the row rather than the
in-memory record so a mailbox deleted mid-flight cannot violate the foreign key.

The two queue consumers derive from `QueueWorker<T>`, which owns the drain loop and the per-item
`try`/`catch`, so the "one bad receipt must never kill a worker" rule is structural rather than
remembered. `MailWorker` is a poller, not a consumer, and stands alone.

1. **`MailWorker`** — polls IMAP on a `PeriodicTimer` every `PipelineSettings.ImapPollIntervalSeconds`,
   backing off exponentially with jitter after consecutive failures. `MailFetcher` reads the enabled
   `Mailbox` rows fresh each poll (a scoped `KvitteriaDbContext`, since it is a singleton), streams up
   to `ImapMaxMessagesPerPoll` unseen messages per cycle, converts HTML bodies to plain text, and
   records each message as a `ReceiptIntake` row, then invokes a callback that enqueues to
   `ExtractionQueue`. A message is flagged `Seen` **only after** both, so anything that throws first
   leaves it unread for the next poll. A message ID already in `Receipts` or `ReceiptIntakes` is
   flagged `Seen` without being enqueued. One
   `ImapClient` per mailbox, since each is a different member's account.

   Each mailbox is **isolated**: its own `try`/`catch`, and the outcome recorded on the row
   (`LastPolledAt`, `LastError`, `ConsecutiveFailures`) so a frontend can show which one is broken.
   Previously one bad password aborted the whole poll and starved every other mailbox. `MailFetcher`
   returns a `MailPollResult`, and `MailWorker` backs off only when **every** attempted mailbox failed
   — otherwise one broken mailbox would slow the healthy ones down. The per-poll message cap is a
   household budget: once spent, the remaining mailboxes are left for the next poll with their status
   untouched rather than recorded as polled. An empty table logs and returns; it is how every household
   starts, not a failure.
2. **`ExtractionWorker`** — calls `OllamaClient` (`/api/chat`, non-streaming, JSON-schema-constrained
   output from `ReceiptExtractionPrompt.Schema`: labelled `rows`, which `ReceiptRows.ToExtracted` maps to the
   `ExtractedReceipt` the rest of the pipeline uses, so an unmappable answer is an `OllamaResponseException`). Retries up to `PipelineSettings.OllamaMaxExtractionAttempts`
   times: attempt 1 at temperature 0, retries at `OllamaRetryTemperature`. Success = **reconciliation**:
   `Reconciliation.IsReconciled`, i.e. `grand_total - sum(line_totals) - pant_total` within
   `OllamaReconciliationToleranceKr` of zero (default 0.01 — Danish receipts round to øre, and exact
   equality burned every retry on a 1-øre difference). Unreconciled attempts feed a natural-language `hint` back describing the
   delta. `OllamaClient` distinguishes `OllamaResponseException` (the model answered with something
   unusable — the only case where a "your JSON was malformed" hint is truthful) from
   `OllamaTransportException` (the model never saw the prompt, so retry unchanged and with no hint).
   `OllamaThreads` sends `num_thread` when the process may run on fewer CPUs than `/proc/cpuinfo` lists. Docker
   shows a container the host's real cpuinfo, so in an LXC Ollama counted 6 cores on a 4-CPU cpuset: 0.19 tokens/s
   against 4.35, which reads as a timeout rather than as anything to do with CPUs. Ollama has no setting for it,
   and with no limit the option is left out, so its own choice stands. The check assumes the backend's cpuset is
   Ollama's, which holds for this compose file; a `cpus:` limit on one service alone would break that.
   If nothing reconciles, the best-delta result is kept with `Reconciled = false` and a warning.
   Enqueues to `PersistenceQueue`.
3. **`ReceiptFactoryWorker`** — delegates to the scoped `ReceiptIngestionService`, which maps
   `ExtractedReceipt` → `Receipt`/`ReceiptLine`, resolves (or inserts) the `Merchant` by name, and
   auto-links lines to `CanonicalItem`s by looking up normalized raw names in `ItemAlias`, copying
   the item's default category and meal onto each linked line through `AliasLearning.FillGaps` — all
   in one transaction, so a failure cannot leave an orphaned merchant. Purchase dates go through
   `PurchaseDate.Resolve`, which parses the documented formats with `TryParseExact`, falls back to
   the mail's own timestamp on unparseable model output, and nudges past the DST spring-forward gap
   rather than throwing.

Both workers resolve their scoped or transient dependencies through `IServiceScopeFactory` since the
hosted services are singletons. `ExtractionWorker` resolves `OllamaClient` per item deliberately: it
is a transient typed `HttpClient`, and holding one in a singleton would pin its pooled handler for
the process lifetime.

`ReceiptExtractionPrompt.ExtractionPrompt` encodes hard-won Danish-receipt rules (printed amounts are
line totals not unit prices, the `HERAF PANT IALT` / `HERAF MOMS` / `I ALT` label-then-amount
ordering, decimal commas). Treat it as tuned behavior: changing wording changes extraction accuracy. What the
evaluation of the earlier prompts taught:

- **Classify, don't filter.** Told to leave out what isn't a product, a 4B model also left out discounts and
  the first product. Told to label every line with an amount (`ReceiptRows`' `kind`), it kept them, and
  code decides which kinds count, forces discounts negative, sums deposits and takes the absolute paid amount.
- **Amounts are patterned strings.** As JSON numbers `29,95` came out as `29` (no decimal comma in JSON); as
  free strings the model returned `2 x 10,05` or `""`. The schema pattern (a grammar in Ollama) fixed both,
  and `DanishAmount.TryParse` rejecting anything else turns an ignored pattern into a retry.
- **Dates and chains are code.** The model's `purchase_date` was wrong whenever register numbers preceded the
  date. `PrintedDate.Find` reads it from the text and `MerchantChains.Match` names the chain (one spelling per
  chain; the model's name is only a fallback). `PurchaseDate.Resolve` falls back to the source time when the
  date is more than a day after it or more than 365 days before it.
- A row that is only a quantity (`2 x 12,00`) is an unusable answer, retried like malformed JSON.

## Authentication and authorization

GoTrue owns **identity**; this backend owns **authorization**. It runs as a service in this compose
project, sharing the database and owning the `auth` schema while EF owns `public` — so one `pg_dump` is
a complete backup. Clients sign in against GoTrue through Caddy and send the resulting access token as
a bearer token. The backend never sees a password, never sends a reset mail, never rotates a refresh
token.

EF connects as a superuser, so Postgres RLS cannot be part of the security model and no Data API is
exposed. **The backend is the sole enforcement point.**

`AuthOptions` (`Features/Auth/AuthOptions.cs`) needs at least one signing source, and `SharedSecret`
takes precedence over `JwksUrl` — which is why it is **rejected outside Development**: honouring it
where a working key set already exists would silently downgrade validation to HS256. Neither rule fits
data annotations, so `AuthOptions.Validate(bool isDevelopment)` reports both through an
`IValidateOptions<T>` and `ValidateOnStart`. It takes the environment rather than reading it, keeping
the rules a pure function and the tests free of hosting types.

`RequireHttpsMetadata` is secure by default, relaxed only in Development or when
`AllowInsecureJwksTransport` says so — whoever can answer the key fetch decides which tokens are
valid. The shipped compose sets that flag, because the fetch is a hop between containers with no
certificate to present; it is an explicit opt-out rather than an inference from the environment name,
so a deployment genuinely serving its JWKS in the clear has said so in one auditable place. Note the
failure mode: `ConfigurationManager` is constructed lazily on first authentication, so a rejected
transport presents as a 401 rather than a startup error.

`Issuer` and `JwksUrl` are **separate values on purpose**: the token's `iss` is the public URL the
*client* reached auth on through Caddy, while this process fetches keys at `http://gotrue:9999` on the
compose network. Conflating them presents as an unexplained 401. GoTrue serves a bare JWKS rather than
an OpenID discovery document, hence `JwksConfigurationRetriever`; wrapping the key set in an
`OpenIdConnectConfiguration` keeps `ConfigurationManager`'s caching and refresh-on-failure, so key
rotation is a non-event.

`GOTRUE_JWT_KEYS` is a JSON **array** of JWKs — a `{"keys": [...]}` wrapper fails to parse — and each
key needs `"key_ops": ["sign", "verify"]` or startup reports `no signing key detected`.
`GOTRUE_JWT_SECRET` is required by GoTrue's config loader regardless, goes unused when the key set is
present, and is deliberately never given to the backend. `scripts/jwk_from_pem.py` generates a
conforming key set from an openssl PEM using the standard library alone.

**Reachable from a phone.** A release build refuses cleartext, so a physical device needs one of the two HTTPS
[modes](#modes); a debug build on an emulator reaches a local stack at `http://10.0.2.2:8080`. Signup stays
open on purpose: members join by signing up after an admin pre-authorizes their email, and inviting needs a
`service_role` key this project avoids having. A stranger who signs up gets a row in `auth.users` and a 403
from the backend, so [request protection](#request-protection) is required rather than optional.
`GOTRUE_DISABLE_SIGNUP` stays a manual switch, and setting it stops new members joining. Nothing blocks a move
to hosted identity: `AppUser.AuthUserId` is deliberately **not** a foreign key to `auth.users`, so
identity can relocate without a migration. `docs/configuration.md` lists what changes; the trap there
is that a hosted Supabase project has its Data API **on** by default, which would bypass every policy
below.

### Request protection

**Caddy states the client address once.** `request_header X-Real-IP {client_ip}` at the top of the site block
overwrites whatever the client sent, and both GoTrue and the backend read that header alone. A dedicated
header, not `X-Forwarded-For`: that can hold a list, GoTrue uses the raw value as its limiter key, and a list
would make the key forgeable. The `servers` global option sets `trusted_proxies static
{$TRUSTED_PROXIES:private_ranges}` with `trusted_proxies_strict` (right-to-left, so an appended value cannot be
spoofed from the left). Compose defaults the variable to `private_ranges`, the peer a host-side proxy,
Tailscale serve, a Tunnel daemon or a sibling container appears as. `compose.public.yaml` pins it to
`127.0.0.1/32`, meaning nobody: on a dual-stack host an IPv6 client can reach Caddy from the Docker gateway,
a private address. Docker Desktop hides client addresses behind its gateway, so there all clients share one
limit; that fails towards limited, not unprotected.

**Sign-in and sign-up use GoTrue's own limits.** They are inactive unless `GOTRUE_RATE_LIMIT_HEADER` names a
header, which is why the stack once had none. `/signup` keeps GoTrue's default of 30 per 5 minutes
(`GOTRUE_RATE_LIMIT_OTP`); `/token` is lowered to 30 (`GOTRUE_RATE_LIMIT_TOKEN_REFRESH`, default 150) because
password and refresh grants share a limiter, so it is also the password-guessing budget: a burst of 30, then
about six a minute. If Caddy ever stopped sending `X-Real-IP`, GoTrue would log a warning and silently limit
nobody.

**The API is limited in the backend** (`Features/Shared/RequestProtection.cs`): a sliding window of 300
requests a minute per `Connection.RemoteIpAddress`, attached to the `/api` group with `RequireRateLimiting`
so `/health/*` and `/version` are outside it by construction. 300 is deliberately generous, because a
household shares one address and a screen load fires several requests. The limiter's default rejection is
503, so `RejectionStatusCode` is 429 and `OnRejected` writes problem details and `Retry-After` (the window
length, since a sliding window reports no retry time). `UseRateLimiter` runs before authentication so a flood
is turned away before any token is validated. It is a per-group policy and changes nothing in the OpenAPI
document. The backend does not depend on which proxy is in front.

**`UseForwardedHeaders` runs first**, with `ForwardedForHeaderName = "X-Real-IP"`, `ForwardLimit = 1` and
`KnownIPNetworks` set to the private ranges (`10/8`, `172.16/12`, `192.168/16`, `fc00::/7`). The compose
subnet is assigned by Docker and cannot be named statically, and pinning one risks colliding with the host's
own network; private ranges are safe because the backend publishes no port outside the development override,
so only compose peers can reach it. It is `KnownIPNetworks`, not `KnownNetworks`, which is obsolete in .NET
10 and fails the build under `TreatWarningsAsErrors`. An IPv4-mapped IPv6 address keys as its IPv4 form.

### Who gets in

`UserProvisioning.Decide` answers this on every authenticated request, because bootstrapping the first
admin and binding a pre-authorized email are the same question at different points in the household's
life — one decision rather than two flows that can disagree:

- **Existing** — a row already claimed by this Supabase user. `Disabled` is rejected.
- **Bind** — an unclaimed row pre-authorizes this email: attach the user and activate it.
- **Bootstrap** — no members exist at all, so this caller becomes the admin. There is no init endpoint
  and no bootstrap secret; nobody can be pre-authorized before an admin exists to do it. The branch
  takes a `pg_advisory_xact_lock`, because the empty-table check is otherwise racy.
- **Reject** — authenticated, but not a member. 403.

Admins pre-authorize with `POST /api/users` and tell the person to sign up; **nothing is emailed**. That
is why no `service_role` key and no SMTP configuration are needed anywhere in this project. Members are
`Disabled`, never deleted — rows reference them.

### Permissions

Reads are never gated: the household sees everything — `GET /api/users` and `GET /api/users/{id}`
included. Those two carried `ManageUsers` until the mobile app needed them: a member may hold
`ManageMailboxes` and register their own mailbox, and the owner picker on that screen cannot populate
itself without the member list, so the gate made a permission the household had granted unusable. The
writes on the same group keep theirs, so who may be *edited* is unchanged. The cost is that
`AppUserDto` shows every active member's email, role, status, `HasSignedIn` and `LastSeenAt` to every
other member — the same bargain every other read here already makes inside one household.

`Permission` covers the writes —
`CategorizeLines`, `ManageTaxonomy`, `EditReceipts` (also retry and dismiss on
`/api/pipeline/failures/{id}`, and `DELETE /api/receipts/{id}`), `ManageUsers`, `ManageSettings`,
`ManageMailboxes`, `AddReceipts` (uploading an image, separate from `EditReceipts` because adding your own
receipt is ordinary use while correcting figures is not, and in the seeded member set; the
`ReceiptImageIntake` migration appended it to existing households once). `Admin` holds all
of them implicitly, so a new enum value never locks the admin out of it; `Member` holds
`HouseholdSettings.MemberPermissions`, one row the admin edits through `PUT /api/settings/permissions`
(see [Configuration](#configuration) for the sibling `PUT /api/settings/pipeline`, gated by the same
`ManageSettings` permission).
`AppUser.PermissionOverrides` is honoured by `PermissionResolution.Resolve` but written by nothing yet —
the column exists so per-user overrides need no migration.

Enforcement is one policy per permission, registered mechanically in `PermissionPolicies`, and the `/api`
group requires `active-user`. Policies compose as an AND, so a write route declares only its own
permission. `HouseholdAuthorizationHandler` resolves the caller once per request and settles every
household requirement from that answer. Permissions live in the database, not in JWT `app_metadata`:
with a handful of users the lookup is free, and claims would need a token refresh every time the admin
flips a checkbox. `PermissionCache` keeps resolved members for 60 seconds, keyed by a version that every
user or settings write bumps, so a toggle takes effect without a new token.

One check cannot be a route policy because it depends on the body: `SetAsDefault` on the line category
and meal endpoints writes the *canonical item's* defaults, which is taxonomy reached through a
categorization endpoint. Those handlers call `CurrentUserService.HasAsync` and answer 403 without
`ManageTaxonomy`.

Enums cross the wire as **names**, not ordinals — `string.TryParseName<T>()` in
`Features/Shared/EnumNames.cs`, because `Enum.TryParse` also accepts numbers, which would make the API
contract depend on declaration order and let an out-of-range value through. Note this differs from
`ReceiptStatus`, which predates it and still serializes as a number.

## Domain model

`Receipt` → many `ReceiptLine`. Each line carries the raw printed name plus optional
`CanonicalItemId`, `CategoryId`, `MealId`. `ItemAlias` maps a **normalized** raw name
(`string.NormalizeString()` in `Features/Shared/Utils.cs` — trim + lowercase) to a `CanonicalItem`;
this is how manual categorization of one receipt teaches future ones. `CanonicalItem` holds the
defaults (`CategoryId`, `DefaultMealId`) that the `SetAsDefault` flags on the line endpoints write
back to, and that ingestion reads back onto a line it links through an alias.

**An automatic write only fills empty fields.** `AliasLearning.FillGaps` (`Features/Receipts`) is the
one place that rule lives, shared by ingestion and by `POST /receipt-lines/{id}/sort`. `AliasLinking.LinkAsync`
beside it links lines without an item through aliases for ingestion, a hand-added line and a renamed
unassigned line: a category or
meal a person already chose is never replaced by an item's default. That sort endpoint is the
atomic save behind the Sort screen — one transaction for the line, an optional new `CanonicalItem`,
its defaults and the alias. It rejects a line that already has an item (409), and when it remembers
an alias it assigns every other unassigned line with the same normalized name. Those waiting lines
(`GET /receipt-lines/unassigned`, plus `/count`) are matched **in memory** with
`AliasLearning.Matching` rather than in SQL, because `lower(btrim(...))` would depend on the
database locale for `Æ`, `Ø` and `Å` and could disagree with the C# `NormalizeString` that wrote the
alias. The candidate set is the queue itself, which is small. Lines linked before ingestion applied
defaults keep their empty category and meal.

`Category` is a two-level tree (`ParentCategoryId`, self-referencing) with unique-name indexes
filtered on root vs child. `ReceiptMealCoverage` (composite key `ReceiptId`+`MealId`) records how
many days a receipt covers for meals where `TracksDayCoverage` is true; it is only valid while some
line on that receipt still carries that meal. Both halves of that invariant — the write-time diff and
the orphan cleanup — live in `Features/Receipts/MealCoverage.cs`; they were previously split across
two endpoint files that disagreed about how coverage was reached.

`AppUser` is a household member as this application knows them, keyed by its own `Id` with
`AuthUserId` as the only link to identity. `HouseholdSettings` is a single row — the scope is one
household with no option to create more — so editing what members may do is one atomic update rather
than a fan-out. Both permission columns are Postgres `text[]` of enum names via `PermissionSetConverter`,
matching the "database values are self-describing" rule the status and role columns follow.

`DatabaseSeeder` seeds the Danish meal and category taxonomy when the tables are empty — which is now
every boot, since `MigrateDatabaseAsync` runs unconditionally; the four emptiness probes are what make
that a handful of `SELECT EXISTS` rather than repeated work. They guard whole tables, not rows, so a
deleted category is not restored and an existing `PipelineSettings` row keeps its model name. Registered
as both `UseSeeding` and `UseAsyncSeeding` in the `AddDbContext` callback in `Program.cs`
(not in `OnConfiguring`, which re-ran on every `DbContext` construction). EF fires these from
`Migrate()`, which includes the `dotnet ef database update` path above. Both inserts share one
transaction, so a partial seed cannot leave meals present and categories permanently blocked.

## Insights and receipt search

`InsightsEndpoints` (`Features/Endpoints`, under `/api/insights`) are all reads, so ungated like every other
read: `summary`, `categories`, `trend`, `prices`, `prices/{itemId}` and `meal-coverage`. `GET /api/receipts`
gained `search`. No schema change; the existing `PurchaseDate` index serves the month ranges.

**Rollups run in memory.** Each handler loads narrow projections for the date range (`ReceiptFigure`,
`LineFigure`, `PricedLine`, `MealLineFigure`, `CoverageFigure`) and hands them to a pure helper in
`Features/Insights`: `SpendingRollup`, `CategoryRollup`, `PriceHistory`, `MealCoverageStats`. A `GROUP BY`
would be faster, but bucketing by Danish month needs Copenhagen time inside the query, and every rule would
then sit out of reach of `Backend.Tests`. A household produces tens of receipts a month and the widest run is
24 months, so the rows fit comfortably. If that ever changes, feed the helpers pre-aggregated rows; the API
doesn't move.

**Months are Danish calendar months.** `MonthKey` is the `yyyy-MM` value on the wire (strict: no trimming, no
`2026-1`), `MonthKey.TryValidateRun` bounds `months` to 1-24 the way `Paging.TryValidate` bounds paging, and
`DanishTime.MonthRangeUtc(year, month)` / `MonthOf` turn it into UTC instants, so a purchase at 00:30 on the
first lands in the new month although it is still the last day of the old one in UTC. `Percent.Change` is the
one definition of a change: one decimal, null when the starting value is zero.

**The total includes pant and the categories don't.** `Total` is the sum of `GrandTotal`, which by
`Reconciliation` is lines plus pant, because that is what left the account. Category rows sum `LineTotal`.
`SpendingSummaryDto` therefore also carries `PantTotal` and `LinesTotal`, and category shares should divide by
`LinesTotal` to add up to 100 %. Negative discount lines stay in every sum, so a row can be negative. An
unreconciled receipt is included like any other: leaving it out would understate spending.

**Categories group through today's tree** (`CategoryRollup`), not a snapshot, so a moved category takes its
history with it. A line on a child counts towards its top-level ancestor; a drill-down lists the children and
shows lines placed directly on the parent as a row for the parent, so the rows always sum to the amount that
was tapped. Uncategorized is a separate field and null on a drill-down.

**Price is total over quantity**, not an average of printed unit prices: extraction reads line totals most
reliably, and weighting by quantity makes weighed goods and multi-buys come out right. Lines with a zero or
negative quantity or total are excluded so a discount cannot look like a price drop. `GET /prices` applies the
same rule in SQL, so an item is listed only if it has a price to show. `Quantity` counts packs, so a generic
item that mixes pack sizes mixes them here too.

**Raw names are grouped in memory** (`RawNames.Group`), never in SQL, for the same reason as the unassigned queue:
`lower(btrim(...))` would follow the database locale for `Æ`, `Ø` and `Å` and could disagree with the C#
`NormalizeString` that aliases are keyed by. `GET /prices` attaches each item's names (key = normalized form,
label = most common spelling) for the page's items only. `GET /prices/{itemId}?name=` filters the item's lines by
that key in memory, so the same printed name sorted to another item never counts, and answers `RawNameKey` and
`RawNameLabel` (null without `name`, which is also how an app spots an older server that ignored it). The label
is taken from the item's all-time lines so the heading matches the sheet. Each month carries `PurchaseCount`.

**Meal statistics divide all spend on the meal by the days covered.** Spend on receipts with no coverage stays
in the numerator, so the spend figure matches the lines that carry the meal; the cost per day is high for a
household that fills coverage in only sometimes.

**Search** (`ReceiptSearch.TryParse`) trims the term, rejects over 100 characters, escapes `\`, `%` and `_`,
and recognises a number as an amount range: `284,50` is exactly 284.50, `284` is `[284, 285)`. The filter uses
`EF.Functions.ILike` with `ReceiptSearch.EscapeChar` passed **explicitly**. The two-argument overload makes EF
emit `ESCAPE ''`, which switches escaping off, so `50%` would match nothing and the escapes would be treated as
literal backslashes; it was only found by searching for `50%` against a running database. A whole-number term
also matches names, so `1000` finds `REMA 1000`, on purpose. `ILIKE` on `Ø`/`Å`/`Æ` follows the database's
`LC_CTYPE`; the stock `postgres:17` image handles them (`føtex` finds `FØTEX`), so nothing special is done. When
testing by hand from a Windows shell, percent-encode the UTF-8 bytes (`f%C3%B8tex`): a non-ASCII argument can reach
`curl` in the console code page and look like a search that finds nothing.

## Updates

`scripts/update.sh` is the one implementation of an update; the console, the app and a later web interface
are only triggers. `docs/configuration.md` and `docs/troubleshooting.md` cover running it. This is why it's
shaped the way it is.

**The backend never gets the Docker socket.** That would make an API compromise root on the host, and a
sidecar holding it is no better. The backend creates `update-state/request`, a systemd path unit on the host
sees it, and the agent runs the script. The contents are never read and `POST /api/system/update` takes no
body, so the channel carries one meaning: update to the newest release. Nobody can ask for a downgrade across a
migration. The cost is that only Linux hosts with systemd get a button, and setup says so everywhere else.

**The stack only ever gets written to.** The backend owns `update-state/`, and the script may run as root,
so nothing there is an input. `.update/state`, which no container can see, is what the `restore-required`
refusal and the boot cleanup read. `status.json` and the heartbeat are built in `.update/` and renamed in,
because a rename replaces a planted symlink and a write follows it. `touch --no-dereference` looks like the
answer for the heartbeat, but it won't create a missing file.

**Two status files, because Caddy has to answer.** The backend is stopped for the riskiest part of an update
and stays stopped after `restore-required`, so `/update-status` comes from Caddy, from `update-state/public`.
That route is anonymous, so it holds a state, a version and a time; `detail` can name paths and stays in
`status.json`, read through the API. It's outside the OpenAPI document, so `ContractTest` can't cover it.
Caddy mounts the directory rather than the file because the rename replaces the inode.

**The order is the safety property.** Download, checksum and pull happen with nothing touched. The backend
stops before the dump, so no receipt lands after it. Five things in there are easy to undo by accident:

1. **`--no-deps` on every `up`.** Caddy depends on a healthy backend and the backend on a healthy Ollama, so a
   plain `up` during an Ollama problem fails an update that has nothing to do with it, and can take the status
   route down.
2. **Success is judged by the script**, from `/version` and the `postgres` and `migrations` checks read by `exec`.
   `up --wait` follows the healthcheck, where Ollama alone reads as a failed update and would trigger a rollback
   for nothing. The body of `/health/ready` is what counts, since it's a 503 with Ollama down.
3. **`pull --policy missing` still pulls a tag named `latest`.** Images other than the backend are checked one by
   one with `docker image inspect`, or every update would move `ollama:latest`.
4. **The script hands over to its replacement.** Bash reads a script as it runs, so `update.sh` is unlinked rather
   than overwritten, and the old one `exec`s the new one with `--continue`. `.update/previous/` is the interface
   between them, not the command line, so a change there has to work across one release boundary. This is also
   how a release fixes the process delivering it.
5. **Compose can't see a changed Caddyfile.** It compares configuration, not the contents of a mounted file, so
   the script restarts Caddy when the hash changed.

**Rollback turns on migration history.** Unchanged means the old version can run against this database, so
it comes back by itself. Changed means only the dump can undo it, so the backend stays stopped and the state is
`restore-required`. An unreadable history counts as changed. Restoring is never automatic: it would drop the
database, `auth` included, on the strength of a check that can fail for unrelated reasons. `--restore` demands a
terminal and a typed word, so the agent can't reach it.

**The backend side needs no database.** `UpdateChecker` (`Features/Update`) holds the last result in memory,
because a restart just checks again, and that keeps this feature out of its own rollback calculation.
`ReleaseSelection` is the same rule as the script's `grep`, kept twice because the script has to work with no
backend running. `UpdateRun.Parse` and `UpdateGate` are the decisions, pure and tested; `UpdateStateStore` is the
file access, where a missing or unwritable directory simply reads as no agent. "Has a bundle" stands for "has
an image" because `publish-backend` uploads it after the push. Reads follow the household rule, and `POST` reuses
`ManageSettings`, since updating is retuning the server.

## Android app

`android/` is a Gradle project with convention plugins in `build-logic/` and versions in
`gradle/libs.versions.toml`. It starts on AGP 9 with built-in Kotlin, compile SDK 37, target SDK 36 and
min SDK 26, which gives `java.time` without desugaring.

```
app/                 assembles everything: root navigation, tabs, Koin, the activity
core/domain          shared models, Result, repository interfaces
core/data            Ktor, session, shared repositories, money and date serializers
core/presentation    UiText, formatting, pickers, Loader, screen states
core/design-system   tokens, theme, components, fonts
feature/{auth,sort,pipeline,settings,capture}/{domain,data,presentation}
feature/{overview,receipts,insights}/presentation
```

Each screen is MVI: `State`, `Action`, a `Channel` of events, `Root` holding the ViewModel and `Screen`
holding only state, in one file. The three features that own no data have no `domain` or `data` module
rather than empty ones. `Loader<T>` (`core:presentation`) carries the rule every data screen shares: a
refresh supersedes the one before it, and a failed refresh keeps the content and reports through
`failures`. `LoadableContent` draws the matching loading, empty, error and content states inside
pull-to-refresh.

### Navigation

Navigation 3, for two reasons: multiple back stacks and conditional navigation are both published
recipes for it. `KvitteriaRoot` holds `GateRoute`, `ServerRoute`, `ClaimRoute`, `AuthRoute` or
`MainRoute`, and moving between those **replaces the whole stack**, so back can't reach a signed-in screen
after sign-out. `SettingsRoute` and `LicensesRoute` sit above `MainRoute`, which is why they have no tab
bar. `MainShell` holds one back stack per tab in `TabNavigation`, each with its own decorators so a tab
that isn't showing keeps its ViewModels. Dialogs and sheets are screen state rather than navigation
entries, and routes carry IDs only.

The root is what notices a session ending, so no screen handles that. It also sends a stack restored
after process death back to the gate, because nobody has been resolved then: `session.member` is null.

### Server connection and auth

The app knows nothing at startup: which server, and who you are on it. `GateViewModel` resolves it in
an order that is load-bearing:

1. **No stored server** → server entry.
2. **`GET /version`** — proves the origin is a Kvitteria backend rather than merely something that
   answers. Failure reads `/update-status` on the anonymous client: `running` waits and rechecks by itself until
   `/version` answers, `restore-required` explains the command to run, and anything else, a 404 or a `running`
   over thirty minutes old included, stays on the gate with a retry naming the address. None of them signs out.
3. **No session** → `GET /health/ready`, whose `household` check decides claim versus sign-in.
4. **An expiring session** → refresh before spending it on a request that would 401.
5. **`GET /api/users/me`** → 200 opens the tabs, **403** is the rejected state, **401** clears the session
   and starts over at step 3.

Four things about that are easy to undo by accident:

1. **Step 5 is what claims a household.** There is no claim endpoint. `UserProvisioning.Decide`
   bootstraps the first authenticated caller into the admin as a side effect of that request, under a
   `pg_advisory_xact_lock`. `ClaimScreen` therefore calls nothing: it exists so the person about to sign
   up learns beforehand what signing up will make them.
2. **`/health/ready` must be parsed whatever the status code is.** The health middleware answers 503 for
   an unhealthy report, and an unpulled Ollama model makes the whole report unhealthy while the
   `household` check beside it stays perfectly authoritative.
3. **A non-member gets 403, not 401.** GoTrue accepted the account and the household did not, which is a
   different thing from a failed sign-in. The session is kept so the rejected state can name the address.
4. **Refreshes are serialized.** GoTrue rotates the refresh token on every use and honours the previous
   one for only ten seconds, so two concurrent refreshes can log the household out.
   `GoTrueSessionManager` holds a `Mutex` and re-checks the token after acquiring it, so a burst of 401s
   produces one refresh. A refresh that fails for a *network* reason keeps the stored session, and so does one
   answered with 429 or a 5xx: rate limiting and a proxy's 502 during a restart say nothing about the refresh
   token, and treating them as a rejection would sign the household out. `GoTrueTokenClient.refresh` maps both to
   `RefreshOutcome.Unreachable`; any other non-2xx stays `Rejected`.

**Konto follows an update it started, and the gate covers everyone else.** After `POST /api/system/update` Konto
stays in charge and polls `/version` and `/update-status`, because the old server keeps answering `/version` while
the image downloads: the gate would open the tabs and the server would vanish from under them. Only
`restore-required` goes to the gate. It ignores any status whose `updatedAt` matches the one from before the `POST`,
or a `rolled-back` from an earlier try at the same version would read as an instant outcome. The reason comes from
`GET /api/system/update`, because `/update-status` is anonymous and has none. A 404 from that endpoint, on an older
server, simply leaves no update row. The gate's wait keeps the last status through a failed read, since Caddy restarts
at the end of an update.

There are two `HttpClient`s, wrapped in `AnonymousHttpClient` and `ApiHttpClient` so that confusing them
is a compile error rather than an endless refresh loop. GoTrue **must** use the anonymous one. Neither has
a base URL: a small plugin fills in the server per request, so a changed server needs no rebuilt client,
and an absolute URL is left alone, which is how a candidate address is probed before it's stored.

Ktor's bearer plugin caches what `loadTokens` returned, which would skip the expiry check and keep
serving a previous session's token. So `HttpClientFactory.api` installs a plugin in front of it that asks
the session on every request and calls `clearToken()` as soon as the token differs.

The address and the session both live in DataStore. The session is one AES-GCM value with the key in
Android Keystore (`KeystoreSessionCipher`), because per-key writes aren't atomic and a torn session with a
fresh access token and a spent refresh token is unrecoverable. A value that can't be decrypted reads as no
session. Backups are off, through `allowBackup` and `dataExtractionRules`, because a Keystore key doesn't
survive a restore.

`src/debug/res/xml/network_security_config.xml` overrides the main one to allow cleartext, so an emulator
can reach `http://10.0.2.2:8080`. Release builds refuse it, and report the address as unreachable.

### Errors

Alongside the skill's `DataError`, API calls return `Result<T, ApiError>`, where `ApiError` also carries
the problem+json `detail`. `ApiError.toUiText()` shows `detail` verbatim on 400, 409, 502 and 503,
because it names things a client can't reconstruct, like the folders that do exist. Network and permission
failures stay localized. A 403 on a write also refreshes the member (`refreshIfForbidden`), because what
the screen shows is out of date.

### Wire types

DTOs are hand-written `@Serializable` classes beside each data source, mapped to domain models.

- **Money** is `BigDecimal` through `MoneySerializer`, which reads the JSON number's own text so a value
  never passes through `Double`. .NET 10 describes `decimal` as number-or-string, and it takes both.
- **Instants** tolerate a missing offset by reading it as UTC.
- **`ReceiptStatus`** is a number. Other enums are names, with an `Unknown` fallback.

### The contract

The app can't compile the server's DTOs, so `docs/openapi/kvitteria.json` stands in. `dotnet build
Backend/Backend.csproj -p:GenerateOpenApi=true` regenerates it, and `test.yml` fails when it's stale.
`Program.cs` skips migrating when started by that tool, so generating needs no database.

`ContractTest` in `core:data` compares every wire model's serial descriptor with its schema. It checks
names and nullability only, because of how .NET 10 describes `decimal`. A new DTO has to be added to its
list, including ones owned by feature data modules, which `core:data` reaches as test dependencies.

### Design system

`docs/design-system.html` defines the components and `docs/design-tokens.html` the values, whose hex is
already resolved from oklch. Type sizes come from its spec column: the samples on that page render
smaller. `core:design-system` holds them as `KvColors`, `KvType`, `KvSpace` and `KvRadius`, and maps them
onto a Material dark scheme so that system-styled pieces come out dark. Screens use the `Kv*` components,
which are thin over `foundation` rather than Material restyled.

The palette is a green at oklch hue 158–170 on a dark ground, with Montserrat at weight 500 over Poppins.
The reference contradicts itself on that pairing in its prose while its tokens are unambiguous; the tokens
won. Five things follow that are easy to undo by accident:

1. **It is dark-only.** There is no light ramp designed. `KvitteriaApp` pins `MODE_NIGHT_YES` so the
   platform's own chrome comes up dark, `MainActivity` passes dark bar styles, and `themes.xml` mirrors
   the ground, because Android paints the window before any Compose exists.
2. **The accent is a line and a glow, never a flood.** So `KvButton` defaults to the quiet outline, and
   `KvButtonStyle.Primary` is opted into.
3. **Nothing hardcodes a value.** Colours, sizes, radii and spacing are all named tokens.
4. **Elevation is an edge plus ambient darkness.** `kvElevation` applies both halves, and the small level
   is an edge alone.
5. **Amounts go through `KvAmount`.** Montserrat has a `tnum` feature and Poppins doesn't, so an amount set
   in Poppins won't line up in a column.

Touch targets use `minimumInteractiveComponentSize`, so a 30dp stepper draws at 30dp and reacts at 48dp.
Icons are glyphs from `Phosphor-Regular.ttf`, drawn by `KvIcon` from code points named in `PhosphorIcons`
and transcribed from Phosphor's own `src/regular/style.css`. Charts are plain `Canvas`, because three
simple charts don't justify a library. A debug-only `DesignGalleryActivity` shows everything on a device:

```bash
adb shell am start -n dev.jelshoj.kvitteria/.DesignGalleryActivity
```

### Strings

`res/values/strings.xml` is **Danish** and `res/values-en/strings.xml` is English, the opposite of the
usual arrangement and the point: a device in an untranslated language falls back to Danish. Strings live
in the module that uses them, with shared ones in `core:presentation`.

The choice is kept by AppCompat. `AppCompatDelegate.setApplicationLocales` restarts the activity, so every
string updates with no language plumbing in composables, which needs `AppCompatActivity` and the locale
service in the manifest. `generateLocaleConfig` publishes the two languages to the system's per-app
setting, and `res/resources.properties` says the unqualified resources are Danish.

`KrFormat` formats amounts by hand rather than through `NumberFormat`, so the true minus sign and the
placement of `kr` are exact in every language. `DanishTime` shows dates in a fixed `Europe/Copenhagen`, so
a purchase at 00:30 on the first is in the new month on a phone in New York. Household data and the
server's `detail` are never translated.

### Screens

- **Instant detail.** `KtorReceiptRepository` keeps listed receipts in memory, and detail paints from
  `peek(id)` and then refreshes.
- **Badges.** `WorkCounts` holds the two counts. The shell refreshes it on `ON_START`, and saves update it
  directly.
- **Taxonomy.** Categories, meals and the item list are cached for the session and invalidated by writes.
- **Suggestions.** `SuggestionRanker` is a pure function ported from the mockup. It tokenizes with
  `[^a-zæøå0-9]+` on root-locale lower-casing, so `Æ`, `Ø` and `Å` stay inside a word.
- **Skips** are held in memory by `InMemorySkippedLines`, so they survive tab changes and end with the
  process.
- **A 409 on sort** means someone sorted the line, or a new item's name is taken. The queue tells them
  apart without reading the message: after a reload, the line is either gone or still there.
- **Optimistic line edits** snapshot the previous line and restore it on failure. Clearing a meal's last
  line refreshes the receipt, because its coverage went with it on the server.
- **Coverage steppers** debounce by 400 ms and send the full set, because the endpoint replaces it.
- **Search** uses `debounce(300)` with `flatMapLatest`, which is what discards stale results.
- **Mailbox state on Pipeline** joins the status report with `GET /mailboxes` by ID, because the report
  has no error or enabled flag.
- **Pickers** are bottom sheets, and the category sheet is grouped by parent, because a two-level tree
  needs no tree control.
- **Capture** (`feature/capture`) is the image source. Scan (ML Kit document scanner through Play services,
  no camera permission), pick (`PickVisualMedia`, no storage permission) and share (`ACTION_SEND image/*` on
  `MainActivity`) all end in `AndroidImagePreparer`, which applies EXIF rotation, scales to the server's
  1 MP budget (`PixelBudget`, the testable arithmetic) and re-encodes, which is what strips GPS. The result
  waits in `PendingCapture` because routes carry IDs only; a share is read at once, since its read grant ends
  with the activity, and `MainShell` opens the confirmation once a member is resolved, so a share made while
  signed out appears after sign-in. Overview and Receipts get the plus action through a slot the app fills,
  so neither feature depends on capture. `CaptureNotices` carries the outcome to the shell's toast, because
  adding starts on one screen and is confirmed on another. `adb shell am start -a SEND` can't grant a
  MediaStore read, so a by-hand share check goes through the Files app's share sheet.

### Tests

Run from `android/`. Pure logic and ViewModels are tested on the JVM with JUnit 5, AssertK, Turbine and
fakes, and the session and clients with Ktor's `MockEngine`:

```bash
./gradlew spotlessCheck testDebugUnitTest test
```

`testDebugUnitTest` covers Android modules and `test` the pure JVM ones. There are no instrumented tests:
end-to-end checks are by hand on an AVD against the compose stack.

## API and code conventions

Endpoints are static classes under `Features/Endpoints`, all mapped under the `/api` group from
`Program.cs` → `MapReceiptEndpoints()`, which fans out to the per-resource map methods. Endpoint
handlers inject `KvitteriaDbContext` directly and query with EF Core. There is no repository layer:
the ingestion pipeline uses the concrete `ReceiptIngestionService`, and everything else uses the
context. Structural rules worth testing live outside the endpoint files as pure helpers —
`CategoryTree`, `MealCoverage`, `Reorder`, `Paging`, `Reconciliation`, `PurchaseDate` — while the
endpoints keep the HTTP concerns.

C# 14 **extension blocks** (`extension(RouteGroupBuilder app) { ... }`) are the idiom for endpoint
registration and (`extension(string rawName)`) for string helpers.

DTOs are **split across two folders**. The records live in `Backend/Contracts`, in the
`Kvitteria.Contracts.DTO` namespace, and their `ToDto()` extensions in
`Backend/Features/Endpoints/Extensions/<Name>Extension.cs`, one file per DTO, mirroring the record it maps
to. Still no mapper layer; do not add one. A change to a record changes the API, so regenerate
`docs/openapi/kvitteria.json` with it.
Errors are returned as RFC 7807 problem details via `Results.Problem(statusCode:, detail:)`, and
every route declares `.WithName()`, `.Produces<T>()`, and `.ProducesProblem(...)` for the OpenAPI
document.

Style: tabs, Allman braces, explicit types over `var` where the type isn't obvious from the
right-hand side, `CancellationToken ct` as the parameter name.

## Gotchas

- **The image budget is 1 000 000 pixels and 2 MB, checked by reading the JPEG header** (`JpegHeader`),
  never by decoding. On the home LXC, with `qwen3.5:4b` loaded, about 1.8 MP ran the 5 GB guest out of
  memory and hung it, while 0.79 MP read every character. The app scales to the same numbers. Don't raise
  the limit without measuring memory on the deployment, and don't add server-side resizing: decoding a
  hostile image is its own memory risk.
- **Receipt sample images are never committed.** The benchmark photos carry personal data and the
  repository is public. Keep them in a folder outside the checkout.
- `POST /api/receipts/uploads` carries `.DisableAntiforgery()`. A form-bound minimal API endpoint demands an
  antiforgery token otherwise, and there is no cookie for one to protect: auth is a bearer token. Its
  `RequestSizeLimit` makes Kestrel cut off an oversize body before buffering it, which surfaces as a 413
  through `BadHttpRequestExceptionHandler`.

- The `citext` Postgres extension is required (declared via `HasPostgresExtension`) for
  case-insensitive category names, and for matching a token's email against a pre-authorized `AppUser`.
  `postgres/initdb/10-roles-and-extensions.sql` creates it up front along with `pgcrypto`, so EF's
  `CREATE EXTENSION` is a no-op even under a role without `CREATE` — but **that script only runs on a
  fresh volume**.
- GoTrue's own migrations `GRANT` to `anon`, `authenticated` and `service_role`, which a Supabase
  project provisions and a plain `postgres` image does not. The same initdb script creates them; without
  it the first `gotrue` boot fails with `role "..." does not exist`.
- `Backend/Dockerfile` must copy `Directory.Packages.props` **and** `Directory.Build.props` before
  `dotnet restore`. Missing the first fails with NU1015, since every `PackageReference` takes its
  version from there; missing the second fails on the absent `TargetFramework`.
- The Dockerfile's `VERSION`/`SOURCE_COMMIT` args default to **empty**, and each `-p:` override is
  applied only when its arg has a value (`${VERSION:+...}`). A literal default would override the
  `VersionPrefix` release-please maintains in `Backend.csproj`, so an ordinary `docker build` would
  report that placeholder on a released commit. `compose.override.yaml` passes `0.0.0-dev` deliberately:
  a build from a working tree is not a release and `GET /version` should not imply it is.
- `compose.override.yaml` must set `image:` explicitly. Compose merges rather than replaces, so without
  it a local build is tagged as the GHCR image and shadows a later pull.
- Manual API checks need a bearer token; the Scalar reference declares no security scheme, so it cannot
  attach one for you. `scripts/token.sh` signs in against GoTrue and prints one.
- The Data Protection key ring lives in this database (`DataProtectionKeys`, via
  `IDataProtectionKeyContext` on `KvitteriaDbContext`), so one backup covers both the keys and the
  mailbox ciphertext they protect. **Restoring one without the other means every mailbox password must
  be re-entered.** `SetApplicationName("Kvitteria")` in `Program.cs` and the purpose string in
  `MailboxSecrets` are both load-bearing: change either and every stored password becomes
  undecryptable.
- `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` pins the EF Core version for the whole
  graph. It is on the ASP.NET Core release cadence, so its patch number has to match the
  `Microsoft.EntityFrameworkCore.*` pins in `Directory.Packages.props` — a mismatch fails the build
  with CS1705, not a restore error.
- Tags pushed with the default `GITHUB_TOKEN` do **not** trigger other workflows, so image publishing
  runs as a job inside `release-please.yml` gated on its outputs. An `on: release` trigger would never
  fire, and every release would look successful with no image behind it.
- **A new environment variable needs a default in the compose file.** An update unpacks a bundle over an
  existing `.env`, which nothing edits except the `BACKEND_VERSION` pin, so a `${VAR}` with no default starts
  blank on every upgraded install. `.env.example` alone doesn't reach them.
- `publish-backend.yml` needs `contents: write` to attach the bundle, and so does the `permissions:` block on
  the job in `release-please.yml` that calls it: a caller caps what a reusable workflow can have.
- `mobile.yml` filters on paths, `test.yml` deliberately does not. A path-skipped run of a **required**
  check stays pending forever and nothing can merge, so only the workflow that is expensive and
  non-blocking gets a filter.
- **`.Select(m => m.ToDto())` is client-evaluated**, because `ToDto` is a method call EF cannot
  translate. Two consequences that pull in opposite directions from the obvious reading: reading a
  navigation inside it does **not** join that navigation in, and an `Include` before it is **not**
  discarded the way it would be ahead of a translated projection. `GET /mailboxes` had no `Include` and
  returned an empty `MemberDisplayName` and `MemberEmail` on every row; `GET /mailboxes/{id}` beside it
  always had one. Any endpoint projecting through `ToDto` needs the `Include` spelled out.
- `PUT /mailboxes/{id}` decrypts the stored password **inside** the `connectionChanged` branch, not
  before it. Unprotecting eagerly meant a lost Data Protection key ring turned "disable this mailbox"
  into a 409 demanding the password be re-entered — the one thing whoever has a broken mailbox cannot
  supply. Toggling `IsEnabled` or reassigning the owner must cost neither a round-trip nor a decryption.
- `test.yml` builds `Backend.Tests/Backend.Tests.csproj` and runs the OpenAPI check. It has no paths filter
  on purpose, and neither step needs a database.
- The app's `versionCode` is an integer that must only ever increase, which a semver string cannot supply.
  release-please owns `versionName`, through the `x-release-please-version` comment that must stay on the
  same line as the value. CI passes `-PversionCode=` from the run number.
- A file-backed DataStore can't replace its file on Windows while it's open, and fails with `Unable to
  rename ...`. It's a host problem and doesn't happen on a device, so `DataStoreSessionStoreTest` uses an
  in-memory store.
- Adaptive launcher icons need the `mipmap-anydpi-v26` folder. Lint calls the `-v26` qualifier obsolete at
  min SDK 26, and following that advice fails at `aapt2` with `resource mipmap/ic_launcher not found`.
- AboutLibraries can list two libraries with one display name, so anything keyed on a name crashes a
  `LazyColumn` with `Key "..." was already used`. Key on `uniqueId`.
- A per-app locale is resolved through the device's list as well, so `cmd locale set-app-locales --locales
  de` on an `en-US` emulator shows English, not the Danish fallback. Only a device with no English in its
  list falls back. Asking a `createConfigurationContext` with `LocaleList.forLanguageTags("de")` is the
  honest check on an image without root.
- `ANDROID_SDK_HOME` on this machine points at `C:\Android`, which `android emulator` doesn't read AVDs
  from. The `pixel_7_-_api_36_0` AVD needs `emulator -avd` from the SDK folder directly.
- Compose text nodes in `uiautomator dump` only exist while on screen, and `adb shell input text` can't type
  non-ASCII, so a scripted check has to scroll first and use ASCII input.