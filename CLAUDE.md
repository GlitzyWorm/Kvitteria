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
reorder guard, paging validation, and the HTML-to-text converter. Run it from `Kvitteria`:

```bash
dotnet test
```

There are no integration or endpoint tests. For anything touching HTTP or the database, verification
is still manual: bring the stack up against a running Supabase and watch the pipeline logs
(`docker compose logs -f backend`; Development log level is `Debug`).

## Configuration

Secrets live in the gitignored `Backend/appsettings.Local.json`, loaded on top of the normal
appsettings chain in `Program.cs`. Options classes implement `Backend.Abstractions.IOptionsSection`,
which declares a `static abstract string SectionName` — the section name lives on the class, not at
the registration site. Register them with the `AddOptionsSection<T>(IConfiguration)` extension, which
binds the named section and adds `ValidateDataAnnotations().ValidateOnStart()`, so misconfiguration
fails at startup instead of inside the first poll. Sections: `Database`, `Imap`, `Ollama`. All are
bound with `IOptionsMonitor<T>` and read via `.CurrentValue`, so `reloadOnChange` edits take effect
live.

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

1. **`MailWorker`** — polls IMAP on a `PeriodicTimer` every `Imap:PollIntervalSeconds`, backing off
   exponentially with jitter after consecutive failures. `MailFetcher` streams up to
   `Imap:MaxMessagesPerPoll` unseen messages per cycle, converts HTML bodies to plain text, and
   invokes a callback that enqueues to `ExtractionQueue`. A message is flagged `Seen` **only after**
   that callback returns, so anything that throws first leaves it unread for the next poll. One
   `ImapClient` per configured folder, since each entry is a different member's account.
2. **`ExtractionWorker`** — calls `OllamaClient` (`/api/chat`, non-streaming, JSON-schema-constrained
   output from `ReceiptExtractionPrompt.Schema`). Retries up to 3 times: attempt 1 at temperature 0,
   retries at `Ollama:RetryTemperature`. Success = **reconciliation**: `Reconciliation.IsReconciled`,
   i.e. `grand_total - sum(line_totals) - pant_total` within `Ollama:ReconciliationToleranceKr` of
   zero (default 0.01 — Danish receipts round to øre, and exact equality burned every retry on a
   1-øre difference). Unreconciled attempts feed a natural-language `hint` back describing the
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
  case-insensitive category names.
