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

There is no test project. Verification is manual: bring the stack up against a running Supabase and
watch the pipeline logs (`docker compose logs -f backend`; Development log level is `Debug`).

## Configuration

Secrets live in the gitignored `Backend/appsettings.Local.json`, loaded on top of the normal
appsettings chain in `Program.cs`. Options classes implement `Backend.Abstractions.IOptions`, which
declares a `static abstract string SectionName` — the section name lives on the class, not at the
registration site. Sections: `Database`, `Imap`, `Ollama`. All are bound with
`IOptionsMonitor<T>` and read via `.CurrentValue`, so `reloadOnChange` edits take effect live.

`Imap:Folders` is a list — one entry per household member's receipt folder (host, credentials,
folder name, member name/email).

## Pipeline architecture

Three `BackgroundService`s chained by two bounded `Channel`s (capacity 100, single reader). A
single mutable `ReceiptRecord` instance flows the whole way through and accumulates state
(`Status`, `Extracted`, `Attempts`, `Reconciled`, `Error`):

1. **`MailWorker`** — polls IMAP every `Imap:PollIntervalSeconds` via `MailFetcher`, which fetches
   unseen messages, flags them `Seen`, converts HTML bodies to plain text, and enqueues to
   `ExtractionQueue`.
2. **`ExtractionWorker`** — calls `OllamaClient` (`/api/chat`, non-streaming, JSON-schema-constrained
   output from `ReceiptExtractionPrompt.Schema`). Retries up to 3 times: attempt 1 at temperature 0,
   retries at 0.3. Success = **reconciliation**: `grand_total - sum(line_totals) - pant_total == 0`.
   Failed attempts feed a natural-language `hint` back as an extra user message describing the
   delta. If nothing reconciles, the best-delta result is kept with `Reconciled = false` and an
   `Error` message. Enqueues to `PersistenceQueue`.
3. **`ReceiptFactoryWorker`** — maps `ExtractedReceipt` → `Receipt`/`ReceiptLine` entities, resolves
   (or inserts) the `Merchant` by name, and auto-links lines to `CanonicalItem`s by looking up
   normalized raw names in `ItemAlias`. Purchase dates are parsed as Europe/Copenhagen local time
   and converted to UTC.

Both workers resolve scoped services (`OllamaClient`, `IReceiptRepository`) through
`IServiceScopeFactory` since the hosted services are singletons. One bad receipt must never kill a
worker — catch per-item and mark the record `Failed`.

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
line on that receipt still carries that meal — see `ClearOrphanedCoverage` in
`ReceiptLineEndpoints`.

`KvitteriaDbContext.OnConfiguring` uses `UseSeeding` to seed the Danish meal and category taxonomy
on first run when the tables are empty.

## API and code conventions

Endpoints are static classes under `Features/Endpoints`, all mapped under the `/api` group from
`Program.cs` → `MapReceiptEndpoints()`, which fans out to the per-resource map methods. Endpoint
handlers inject `KvitteriaDbContext` directly and query with EF Core; `IReceiptRepository` exists
only for the ingestion pipeline, not for endpoints.

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
