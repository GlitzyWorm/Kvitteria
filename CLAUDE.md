# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

Kvitteria ingests Danish grocery-receipt emails, extracts them into structured line items with a
local LLM, persists them to Postgres, and exposes a minimal-API surface for categorizing lines
(canonical items, categories, meals).

Single project: `Kvitteria/Backend/Backend.csproj` (ASP.NET Core, **.NET 10**), solution file
`Kvitteria/Kvitteria.slnx`.

## Commands

The backend runs in Docker, not via `dotnet run`. Run these from the repo root
(`Q:/Projects/Kvitteria`), where `compose.yaml` lives:

```bash
docker compose up -d --build ollama
```

```bash
docker compose up -d --build backend
```

Start `ollama` first — `backend` declares `depends_on: ollama`, and the ollama container pulls its
model on first boot (`entrypoint` runs `ollama pull qwen3.5:4b`), so the first start takes a while.

Ports from `compose.yaml`: backend on `http://localhost:8080`, Ollama on `http://localhost:11434`.
Scalar API reference at `http://localhost:8080/scalar`, OpenAPI doc at
`http://localhost:8080/openapi/v1.json` — both Development-only, and compose sets
`ASPNETCORE_ENVIRONMENT=Development`.

`compose.yaml` mounts `Kvitteria/Backend/appsettings.Local.json` read-only — the file must exist
before starting `backend`, or Docker creates a directory in its place. The backend container reaches
the host's Supabase Postgres via `host.docker.internal`, and Ollama via the service name
(`Ollama__BaseUrl=http://ollama:11434`).

Database is Supabase's local Postgres on port **54322**, running on the host rather than in this
compose project. Start it first, from the `Kvitteria` directory:

```bash
supabase start
```

Supabase Auth (`[auth] enabled = true`) signs tokens with the key in the gitignored
`supabase/signing_keys.json`, which `config.toml` points at via `signing_keys_path`. It must exist
before `supabase start`, or the CLI refuses the config; regenerate it with:

```bash
supabase gen signing-key --algorithm ES256
```

The CLI prints a single JWK but the config expects an **array**, so wrap it in `[...]` when writing the
file. Without a signing key the stack falls back to the legacy shared HS256 secret and serves an empty
JWKS, which the backend cannot validate against — see `Auth:SharedSecret` below for that path.

EF Core migrations are **not** applied at startup; apply them from the host, from `Kvitteria`:

```bash
dotnet ef database update --project Backend
```

```bash
dotnet ef migrations add <Name> --project Backend
```

`Kvitteria/Backend.Tests` (xUnit) covers the pure logic that has no other safety net: the
reconciliation tolerance, Danish purchase-date parsing including the DST gap, `NormalizeString`
culture-independence, the `CategoryTree` depth/height/descendant rules, the meal-coverage diff, the
reorder guard, paging validation, the HTML-to-text converter, the user-provisioning decision, effective
permissions and the last-admin guard, permission-name parsing, and the auth options rule. Run it from
`Kvitteria`:

```bash
dotnet test
```

There are no integration or endpoint tests. For anything touching HTTP or the database, verification
is still manual: bring the stack up against a running Supabase and watch the pipeline logs
(`docker compose logs -f backend`; Development log level is `Debug`).

## Configuration

Secrets and infrastructure addresses live in the gitignored `Backend/appsettings.Local.json`
(`.example` template checked in), loaded on top of the normal appsettings chain in `Program.cs`.
Options classes implement `Backend.Abstractions.IOptionsSection`, which declares a
`static abstract string SectionName` — the section name lives on the class, not at the registration
site. All four are registered with the `AddOptionsSection<T>(IConfiguration)` extension, which binds
the named section and adds `ValidateDataAnnotations().ValidateOnStart()`, so misconfiguration fails
at startup instead of inside the first poll. Sections: `Database` (`ConnectionString`), `Imap`
(`Folders` only), `Ollama` (`BaseUrl`, `RequestTimeoutSeconds`), `Auth`. These stay file-only on
purpose: each either breaks connectivity/security on a bad value, is baked into an object built once
at startup (the `DbContext` factory, the typed `HttpClient`, the JWT bearer scheme), or — for
`Imap:Folders` — carries plaintext mailbox credentials that aren't linked to an `AppUser` yet.

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

`ValidateDataAnnotations` does not recurse into collection elements, so per-entry IMAP completeness
is checked by `ImapFolderOptions.IsComplete()` and reported by `MailFetcher`, which can name the
member whose entry is incomplete.

`Imap:Folders` is a list — one entry per household member's receipt folder (host, credentials,
folder name, member name/email).

## Pipeline architecture

Three `BackgroundService`s chained by two bounded queues. `ExtractionQueue` and `PersistenceQueue`
are both three-line subclasses of `WorkQueue<T>` (capacity 100, single reader, single writer); they
are separate named types rather than one `WorkQueue<ReceiptRecord>` because both carry the same `T`
and could not otherwise be resolved unambiguously from the container. A single mutable
`ReceiptRecord` instance flows the whole way through and accumulates state (`Status`, `Extracted`,
`Attempts`, `Reconciled`, `Error`).

The two queue consumers derive from `QueueWorker<T>`, which owns the drain loop and the per-item
`try`/`catch`, so the "one bad receipt must never kill a worker" rule is structural rather than
remembered. `MailWorker` is a poller, not a consumer, and stands alone.

1. **`MailWorker`** — polls IMAP on a `PeriodicTimer` every `PipelineSettings.ImapPollIntervalSeconds`,
   backing off exponentially with jitter after consecutive failures. `MailFetcher` streams up to
   `ImapMaxMessagesPerPoll` unseen messages per cycle, converts HTML bodies to plain text, and
   invokes a callback that enqueues to `ExtractionQueue`. A message is flagged `Seen` **only after**
   that callback returns, so anything that throws first leaves it unread for the next poll. One
   `ImapClient` per configured folder, since each entry is a different member's account.
2. **`ExtractionWorker`** — calls `OllamaClient` (`/api/chat`, non-streaming, JSON-schema-constrained
   output from `ReceiptExtractionPrompt.Schema`). Retries up to `PipelineSettings.OllamaMaxExtractionAttempts`
   times: attempt 1 at temperature 0, retries at `OllamaRetryTemperature`. Success = **reconciliation**:
   `Reconciliation.IsReconciled`, i.e. `grand_total - sum(line_totals) - pant_total` within
   `OllamaReconciliationToleranceKr` of zero (default 0.01 — Danish receipts round to øre, and exact
   equality burned every retry on a 1-øre difference). Unreconciled attempts feed a natural-language `hint` back describing the
   delta. `OllamaClient` distinguishes `OllamaResponseException` (the model answered with something
   unusable — the only case where a "your JSON was malformed" hint is truthful) from
   `OllamaTransportException` (the model never saw the prompt, so retry unchanged and with no hint).
   If nothing reconciles, the best-delta result is kept with `Reconciled = false` and a warning.
   Enqueues to `PersistenceQueue`.
3. **`ReceiptFactoryWorker`** — delegates to the scoped `ReceiptIngestionService`, which maps
   `ExtractedReceipt` → `Receipt`/`ReceiptLine`, resolves (or inserts) the `Merchant` by name, and
   auto-links lines to `CanonicalItem`s by looking up normalized raw names in `ItemAlias` — all in
   one transaction, so a failure cannot leave an orphaned merchant. Purchase dates go through
   `PurchaseDate.Resolve`, which parses the documented formats with `TryParseExact`, falls back to
   the mail's own timestamp on unparseable model output, and nudges past the DST spring-forward gap
   rather than throwing.

Both workers resolve their scoped or transient dependencies through `IServiceScopeFactory` since the
hosted services are singletons. `ExtractionWorker` resolves `OllamaClient` per item deliberately: it
is a transient typed `HttpClient`, and holding one in a singleton would pin its pooled handler for
the process lifetime.

`ReceiptExtractionPrompt.SystemPrompt` encodes hard-won Danish-receipt rules (printed amounts are
line totals not unit prices, the `HERAF PANT IALT` / `HERAF MOMS` / `I ALT` label-then-amount
ordering, decimal commas). Treat it as tuned behavior: changing wording changes extraction accuracy.

## Authentication and authorization

Supabase Auth (GoTrue) owns **identity**; this backend owns **authorization**. Clients — the C# MAUI app,
later Angular — sign in against GoTrue directly and send the resulting access token as a bearer token.
The backend never sees a password, never sends a reset mail, never rotates a refresh token.

EF connects as the `postgres` superuser, so Postgres RLS cannot be part of the security model and the
Supabase Data API stays off. **The backend is the sole enforcement point.**

`AuthOptions` (`Features/Auth/AuthOptions.cs`) needs at least one signing source, and `SharedSecret`
takes precedence over `JwksUrl` when both are set. `JwksUrl` is preferred and carries a default pointing
at the local stack, which is why the rule is precedence rather than mutual exclusion: "both set" is the
normal state for anyone choosing the secret, not a mistake worth failing startup over. "At least one of
two" is beyond data annotations, so `AuthOptions.Validate()` reports it through an
`IValidateOptions<T>` and `ValidateOnStart` — the same gap `ImapFolderOptions.IsComplete()` works
around. `Issuer` and `JwksUrl` are **separate values on purpose**: the token's `iss` is the URL the
*client* reached Auth on (`127.0.0.1`), while the container fetches keys via `host.docker.internal`.
Conflating them presents as an unexplained 401. Supabase serves a bare JWKS rather than an OpenID
discovery document, hence `JwksConfigurationRetriever`; wrapping the key set in an
`OpenIdConnectConfiguration` keeps `ConfigurationManager`'s caching and refresh-on-failure, so key
rotation is a non-event.

**Local-only, for now.** iOS App Transport Security blocks plaintext HTTP, so physical devices cannot
reach the local stack — simulator and emulator only until a hosted Supabase project exists. Nothing
blocks that move: `AppUser.SupabaseUserId` is deliberately **not** a foreign key to `auth.users`, so
identity can relocate without a migration. When it does, revisit `enable_signup` and
`[auth.email] enable_confirmations` in `config.toml`, both of which are open locally because members
sign themselves up and no SMTP is configured.

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

Reads are never gated: the household sees everything. `Permission` covers the writes —
`CategorizeLines`, `ManageTaxonomy`, `EditReceipts`, `ManageUsers`, `ManageSettings`. `Admin` holds all
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
back to.

`Category` is a two-level tree (`ParentCategoryId`, self-referencing) with unique-name indexes
filtered on root vs child. `ReceiptMealCoverage` (composite key `ReceiptId`+`MealId`) records how
many days a receipt covers for meals where `TracksDayCoverage` is true; it is only valid while some
line on that receipt still carries that meal. Both halves of that invariant — the write-time diff and
the orphan cleanup — live in `Features/Receipts/MealCoverage.cs`; they were previously split across
two endpoint files that disagreed about how coverage was reached.

`AppUser` is a household member as this application knows them, keyed by its own `Id` with
`SupabaseUserId` as the only link to identity. `HouseholdSettings` is a single row — the scope is one
household with no option to create more — so editing what members may do is one atomic update rather
than a fan-out. Both permission columns are Postgres `text[]` of enum names via `PermissionSetConverter`,
matching the "database values are self-describing" rule the status and role columns follow.

`DatabaseSeeder` seeds the Danish meal and category taxonomy on first run when the tables are empty,
registered as both `UseSeeding` and `UseAsyncSeeding` in the `AddDbContext` callback in `Program.cs`
(not in `OnConfiguring`, which re-ran on every `DbContext` construction). EF fires these from
`Migrate()`, which includes the `dotnet ef database update` path above. Both inserts share one
transaction, so a partial seed cannot leave meals present and categories permanently blocked.

## API and code conventions

Endpoints are static classes under `Features/Endpoints`, all mapped under the `/api` group from
`Program.cs` → `MapReceiptEndpoints()`, which fans out to the per-resource map methods. Endpoint
handlers inject `KvitteriaDbContext` directly and query with EF Core. There is no repository layer:
the ingestion pipeline uses the concrete `ReceiptIngestionService`, and everything else uses the
context. Structural rules worth testing live outside the endpoint files as pure helpers —
`CategoryTree`, `MealCoverage`, `Reorder`, `Paging`, `Reconciliation`, `PurchaseDate` — while the
endpoints keep the HTTP concerns.

C# 14 **extension blocks** (`extension(RouteGroupBuilder app) { ... }`) are the idiom for endpoint
registration and (`extension(string rawName)`) for string helpers. DTOs live in
`Features/Endpoints/DTO` and each carries its own `ToDto()` extension in the same file — keep
mapping there rather than introducing a mapper layer.

Errors are returned as RFC 7807 problem details via `Results.Problem(statusCode:, detail:)`, and
every route declares `.WithName()`, `.Produces<T>()`, and `.ProducesProblem(...)` for the OpenAPI
document.

Style: tabs, Allman braces, explicit types over `var` where the type isn't obvious from the
right-hand side, `CancellationToken ct` as the parameter name.

## Gotchas

- `supabase/` (config, snippets) sits inside `Kvitteria/`, so `supabase` CLI commands must run from
  there.
- The `citext` Postgres extension is required (declared via `HasPostgresExtension`) for
  case-insensitive category names, and for matching a token's email against a pre-authorized `AppUser`.
- `Backend/Dockerfile` must copy `Directory.Packages.props` before `dotnet restore`, or restore fails
  with NU1015 — every `PackageReference` gets its version from there.
- Manual API checks need a bearer token. Sign up or in against GoTrue directly and use the
  `access_token`; the Scalar reference declares no security scheme, so it cannot attach one for you.
