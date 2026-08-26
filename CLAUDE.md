# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

Kvitteria ingests Danish grocery-receipt emails, extracts them into structured line items with a
local LLM, persists them to Postgres, and exposes a minimal-API surface for categorizing lines
(canonical items, categories, meals).

Three projects in `Kvitteria/Kvitteria.slnx`, all **.NET 10**:

```
Backend        ASP.NET Core, the whole server           net10.0
Backend.Tests  xUnit over the pure logic                net10.0
Contracts      the wire types both sides share          net10.0
MobileApp      .NET MAUI, ships Android and Mac Catalyst  net10.0-android (+ -windows on Windows, -maccatalyst on macOS)
```

The reference graph is **`MobileApp → Contracts ← Backend`**, and the missing edge is deliberate:
`MobileApp` must never reference `Backend`. The app talks to the server over HTTP, so the two are
separate programs and `Contracts` is the only thing they share. An Angular app goes at the repo root
beside `caddy/`.

`Kvitteria/` is the .NET root and holds `Directory.Build.props`, `Directory.Packages.props` and
`global.json`; it is also the Docker build context. `TargetFramework` is **not** in
`Directory.Build.props` — it was hoisted there when the backend was the only project, but a
single-targeting `TargetFramework` silently wins over `TargetFrameworks`, so a MAUI project
inheriting it builds plain `net10.0` and fails without naming the cause. Each project declares its
own; everything else genuinely shared stays hoisted.

User-facing documentation lives in `README.md` and `docs/` (`configuration.md`, `troubleshooting.md`,
`development.md`). This file is the internal counterpart — keep the rationale here and the
instructions there rather than duplicating either.

## Licensing

Three licenses, one per subtree, and the arrangement constrains the code:

- **`LICENSE`, AGPL-3.0-or-later** — the backend and everything not listed below. §13 is the point:
  the backend is the part someone could run as a service, and that clause makes them publish changes.
- **`Kvitteria/Contracts/LICENSE`, MIT** — compiled into *both* other projects, so it has to be
  weaker than either. Anything stronger here pulls one license into the other.
- **`Kvitteria/MobileApp/LICENSE`, MPL-2.0** — app store terms conflict with GPLv3 §6 (installation
  information, which iOS code signing makes impossible) and §10 (no further restrictions, which
  Apple's EULA imposes). MPL is App Store compatible in practice — Firefox for iOS ships under it.

Three rules follow, and breaking any of them silently collapses the split:

1. **`MobileApp` must never reference `Backend`.** They are separate programs over HTTP; a project
   reference would compile AGPL code into an MPL app.
2. **`Contracts` stays permissive.** If it ever needs to be relicensed, MIT or Apache-2.0 only.
3. **Every source file under `MobileApp/` carries the MPL Exhibit A header.** MPL's copyleft is per
   file and attaches through that notice, so a file without it is not clearly covered.
   `MobileApp/.editorconfig` sets `file_header_template` and promotes IDE0073 to an **error**, so a
   new `.cs` file fails the build. XAML is not covered by that analyzer — those headers are manual.

Relicensing is currently free: `git log` shows one human author. That stops being true the moment an
outside contribution lands without a CLA, or an AGPL/GPL third-party package enters the app.

Both license texts ship as `MauiAsset`s (`licenses/MPL-2.0.txt`, `licenses/MIT.txt`) and are rendered
by `LicensesPage`. MIT requires its notice to accompany the software, and someone installing the app
receives a binary rather than this repository. They are referenced in place rather than copied into
`Resources/Raw`, so there is one copy of each to keep current.

## Commands

Everything runs in Docker, from the repo root where `compose.yaml` lives. There is no second
orchestration tool and no host-side database: Postgres, GoTrue, Ollama, the backend and a Caddy
entrypoint are all services in this compose project.

```bash
./scripts/setup.sh
```

That writes `.env` (generating the Postgres password, `GOTRUE_JWT_SECRET`, and the ES256
`GOTRUE_JWT_KEYS`), starts everything, and waits on `/health/ready`. `scripts/setup.ps1` is the
Windows equivalent. Re-running it leaves an existing `.env` alone.

After that, plain compose:

```bash
docker compose up -d --build
```

A checkout picks up `compose.override.yaml` automatically, which builds the backend from source
rather than pulling from GHCR, sets `ASPNETCORE_ENVIRONMENT=Development`, and publishes the ports
host-side tooling needs — Postgres on 54322, GoTrue on 9999, the backend directly on 8081 bypassing
Caddy. `compose.gpu.yaml` is a third overlay for GPU passthrough, opted into via `COMPOSE_FILE`
in `.env`.

**Caddy is the single entrypoint**, on `http://localhost:8080` by default. It routes `/auth/v1/*` to
GoTrue with the prefix stripped (standalone GoTrue serves at the root; in a Supabase project Kong is
what adds that prefix), `/api/*` and `/health/*` and `/version` to the backend, and `/` to a future
Angular build mounted at `/srv`. Scalar is at `/scalar` and the OpenAPI document at
`/openapi/v1.json`, both Development-only.

Migrations are applied at startup by `DatabaseMigrator.MigrateDatabaseAsync`, called before the host
starts. To add one, from `Kvitteria`:

```bash
dotnet ef migrations add <Name> --project Backend
```

`DesignTimeDbContextFactory` keeps the tooling off the web host — otherwise scaffolding a migration
would apply one as a side effect, and the absent connection-string default would fail validation
first anyway. Commands that talk to a real database need `Database__ConnectionString` exported.

`Kvitteria/Backend.Tests` (xUnit) covers the pure logic that has no other safety net: the
reconciliation tolerance, Danish purchase-date parsing including the DST gap, `NormalizeString`
culture-independence, the `CategoryTree` depth/height/descendant rules, the meal-coverage diff, the
reorder guard, paging validation, the HTML-to-text converter, the user-provisioning decision,
effective permissions and the last-admin guard, permission-name parsing, the auth options rules,
mailbox write validation, and the mailbox secret round-trip. Run it from `Kvitteria`, naming the
project — a bare `dotnet test` resolves to the solution and now builds MobileApp on the way past:

```bash
dotnet test Backend.Tests/Backend.Tests.csproj
```

The mobile app builds on its own. Android works on any host; Mac Catalyst needs macOS and is built
only in CI:

```bash
dotnet build MobileApp/MobileApp.csproj -f net10.0-android
```

There are no integration or endpoint tests. For anything touching HTTP or the database, verification
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
are both three-line subclasses of `WorkQueue<T>` (capacity 100, single reader, single writer); they
are separate named types rather than one `WorkQueue<ReceiptRecord>` because both carry the same `T`
and could not otherwise be resolved unambiguously from the container. A single mutable
`ReceiptRecord` instance flows the whole way through and accumulates state (`Status`, `Extracted`,
`Attempts`, `Reconciled`, `Error`).

The two queue consumers derive from `QueueWorker<T>`, which owns the drain loop and the per-item
`try`/`catch`, so the "one bad receipt must never kill a worker" rule is structural rather than
remembered. `MailWorker` is a poller, not a consumer, and stands alone.

1. **`MailWorker`** — polls IMAP on a `PeriodicTimer` every `PipelineSettings.ImapPollIntervalSeconds`,
   backing off exponentially with jitter after consecutive failures. `MailFetcher` reads the enabled
   `Mailbox` rows fresh each poll (a scoped `KvitteriaDbContext`, since it is a singleton), streams up
   to `ImapMaxMessagesPerPoll` unseen messages per cycle, converts HTML bodies to plain text, and
   invokes a callback that enqueues to `ExtractionQueue`. A message is flagged `Seen` **only after**
   that callback returns, so anything that throws first leaves it unread for the next poll. One
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

**Local-only, for now.** iOS App Transport Security blocks plaintext HTTP, so physical devices cannot
reach a local stack — simulator and emulator only until this is served over TLS. Nothing blocks a move
to hosted identity: `AppUser.AuthUserId` is deliberately **not** a foreign key to `auth.users`, so
identity can relocate without a migration. `docs/configuration.md` lists what changes; the trap there
is that a hosted Supabase project has its Data API **on** by default, which would bypass every policy
below.

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
`CategorizeLines`, `ManageTaxonomy`, `EditReceipts`, `ManageUsers`, `ManageSettings`,
`ManageMailboxes`. `Admin` holds all
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

DTOs are **split across two projects**, and the split is forced rather than chosen. The records live
in `Contracts` (`Kvitteria.Contracts.DTO`), because the app compiles against them. Their `ToDto()`
extensions cannot follow: they take EF entities from `Features/Persistence/Model`, and `Contracts`
must stay referenceable from MAUI. So the mapping lives in
`Backend/Features/Endpoints/Extensions/<Name>Extension.cs` — one file per DTO, mirroring the record
it maps to. Still no mapper layer; do not add one.

What may go in `Contracts`: records, enums that cross the wire, and nothing else. It has zero
`PackageReference`s and no `FrameworkReference` — see Gotchas. `Permission` deliberately stayed in
`Features/Auth`: permissions cross the wire as `List<string>` in `MemberPermissionsDto` and
`CurrentUserDto`, so the enum itself is a server concern.

Errors are returned as RFC 7807 problem details via `Results.Problem(statusCode:, detail:)`, and
every route declares `.WithName()`, `.Produces<T>()`, and `.ProducesProblem(...)` for the OpenAPI
document.

Style: tabs, Allman braces, explicit types over `var` where the type isn't obvious from the
right-hand side, `CancellationToken ct` as the parameter name.

## Gotchas

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
- `Contracts` must never gain a `FrameworkReference` to `Microsoft.AspNetCore.App`. A MAUI project
  cannot consume the ASP.NET Core shared framework, so one `FrameworkReference` there breaks the app
  build — and it is the natural thing to reach for the first time a DTO wants `IResult` or a
  `[FromBody]` attribute. If a type in `Contracts` needs a package, it belongs in `Backend` instead.
- The MAUI template pins its two `PackageReference` versions inline, which central package management
  rejects with **NU1008** — not the NU1010 you would get from an omitted version. The fix is the
  `Label="Mobile"` group in `Directory.Packages.props`; everything else MAUI needs is an implicit
  reference from the workload, which sets its own versions and is exempt. `$(MauiVersion)` does
  resolve inside `Directory.Packages.props` despite how early that file is evaluated, so the pin
  tracks the installed workload rather than a literal that would drift from `global.json`.
  Transitive pinning was **not** a problem and stays enabled.
- `MobileApp.csproj` gates each platform TFM on the host that can build it: `net10.0-android`
  unconditionally, `net10.0-windows10.0.19041.0` under `IsOSPlatform('windows')`,
  `net10.0-maccatalyst` under `IsOSPlatform('osx')`. Listing Mac Catalyst unconditionally makes
  `dotnet build` on the solution fail on Windows and on every Linux runner. The template also matched
  it twice on macOS, since `!linux` and `osx` both applied.
- **The Windows TFM is a development target, and removing it breaks F5 in a way that names nothing.**
  `Properties/launchSettings.json` contains exactly one profile, `Windows Machine`. With no
  `net10.0-windows` framework behind it, Visual Studio still offers it, launches `dotnet.exe` against
  a project it cannot run, and reports `The target process exited without raising a CoreCLR started
  event` with exit code `0x80008083`. Shipping targets are Android and Mac Catalyst; that is not a
  reason to drop the target the developer actually presses F5 on.
- **Visual Studio caches the TFM list**, so editing `TargetFrameworks` needs a project reload before
  the debug-target dropdown is truthful. Until then it offers targets the project no longer has and
  omits ones it just gained — indistinguishable from the project being misconfigured. Deleting
  `MobileApp/obj` forces it.
- `test.yml` builds `Backend.Tests/Backend.Tests.csproj`, not the solution, because the solution now
  contains MobileApp. The project reference chain still covers `Backend` and `Contracts`, so nothing
  is lost and a backend pull request never installs a MAUI workload. Do not "simplify" it back to a
  bare `dotnet build`.
- `mobile.yml` filters on paths, `test.yml` deliberately does not. A path-skipped run of a **required**
  check stays pending forever and nothing can merge, so only the workflow that is expensive and
  non-blocking gets a filter.
- `Contracts` is deliberately not a release-please component: nothing distributes it, so its own tag
  and changelog would be noise. release-please attributes a commit to every package whose path it
  touches, so a contracts change committed together with its consumer already lands in that
  consumer's changelog — which is the shape you want anyway, since a DTO change with no consumer
  change is dead code. Revisit only if it is ever published as a NuGet package.
- `MobileApp`'s `ApplicationVersion` is Android's `versionCode`: an integer that must only ever
  increase, which a semver string cannot supply. release-please owns `ApplicationDisplayVersion`;
  CI passes `-p:ApplicationVersion=` from the run number. Getting this wrong is only discovered at
  store-upload time.
