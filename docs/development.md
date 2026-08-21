# Development

## Layout

```
Kvitteria/                  repo root — compose, docs, CI
├── caddy/Caddyfile         the single entrypoint's routing
├── postgres/initdb/        roles, schema and extensions, on a fresh volume only
├── scripts/                setup and a bearer-token helper
└── Kvitteria/              the .NET root
    ├── Backend/            the application
    ├── Backend.Tests/      unit tests
    ├── Directory.Build.props     shared MSBuild properties
    ├── Directory.Packages.props  every package version
    └── global.json               the pinned SDK
```

The Docker build context is `Kvitteria/`, not the repo root, because that is where both props files
and `global.json` live. A future MAUI app goes beside `Backend/`; a future Angular app goes at the
repo root beside `caddy/`.

## Running from source

`docker compose` in a checkout picks up `compose.override.yaml` automatically, which builds the
backend instead of pulling it and switches to `Development`:

```bash
docker compose up -d --build
```

That also publishes what host-side tooling needs — Postgres on 54322, GoTrue on 9999, and the backend
directly on 8081, bypassing Caddy. That last one is the quickest way to tell a proxy problem from an
application one.

In `Development` you additionally get `/openapi/v1.json` and the Scalar reference at `/scalar`. Note
Scalar declares no security scheme, so it cannot attach a bearer token for you — use
`scripts/token.sh` and paste.

To test the *published* image from inside a checkout, rename `compose.override.yaml`.

## Working on the backend without rebuilding an image

The backend runs fine on the host against the containerized dependencies:

```bash
cd Kvitteria
export Database__ConnectionString='Host=localhost;Port=54322;Database=kvitteria;Username=postgres;Password=<from .env>'
export Ollama__BaseUrl='http://localhost:11434'
export Ollama__DefaultModel='qwen3.5:4b'
export Auth__Issuer='http://localhost:8080/auth/v1'
export Auth__JwksUrl='http://localhost:9999/.well-known/jwks.json'
dotnet watch --project Backend
```

None of these have defaults, deliberately: a value that fell back to localhost would let a typo start
cleanly and fail later against the wrong host. So the tradeoff is that a host-side run needs all of
them set. `Auth__SharedSecret` is accepted here — but only because this is `Development`.

## Tests

```bash
cd Kvitteria
dotnet test
```

`Backend.Tests` covers the pure logic that has no other safety net: the reconciliation tolerance,
Danish date parsing including the DST gap, the category tree rules, meal-coverage diffing, paging,
the HTML-to-text converter, the user-provisioning decision, permission resolution and the last-admin
guard, the auth options rules, mailbox validation, and the mailbox secret round-trip.

There are no integration or endpoint tests. Anything touching HTTP or the database is verified by
bringing the stack up and reading `docker compose logs -f backend`.

Note `Backend.csproj` sets `TreatWarningsAsErrors`, and `Backend.Tests.csproj` deliberately does not:
promoting the first xUnit analyzer suggestion to an error would break `dotnet test` for a reason that
has nothing to do with the tests. Do not hoist it into `Directory.Build.props`.

## Migrations

Applied at startup, so there is normally nothing to run. To add one:

```bash
cd Kvitteria
dotnet ef migrations add <Name> --project Backend
```

`DesignTimeDbContextFactory` means the tooling never builds the web host, which matters for two
reasons: the connection string has no default, and starting the host would *apply* migrations as a
side effect of scaffolding one. For commands that need a real database, set
`Database__ConnectionString` in the environment first.

Two things EF will not do for you:

- **A rename needs checking.** EF paired `SupabaseUserId` → `AuthUserId` into a `RenameColumn` plus a
  `RenameIndex`, which is correct. It does not always. A scaffolded `DropColumn` + `AddColumn` for
  what is really a rename destroys the data in that column, so read the generated migration.
- **Postgres does not rename an index with its column.** If you hand-write a `RenameColumn`, the
  matching `RenameIndex` is required or the next model comparison tries to create a duplicate.

Verify before committing:

```bash
dotnet ef migrations has-pending-model-changes --project Backend
```

## Conventions

Tabs, Allman braces, explicit types where the right-hand side does not make the type obvious,
`CancellationToken ct`. C# 14 extension blocks are the idiom for endpoint registration and for string
helpers, which is why `LangVersion` is pinned and why `global.json` exists — an older SDK fails with a
language-version error that does not name the cause.

Endpoints are static classes under `Features/Endpoints`, DTOs carry their own `ToDto()` in the same
file, and errors are RFC 7807 problem details. There is no repository layer and no mapper layer; do
not add one. Structural rules that are worth testing live outside the endpoint files as pure helpers.

Comments describe the code as it is, not the change that produced it.

## Releases

Commit messages are [Conventional Commits](https://www.conventionalcommits.org/), enforced on pull
requests. release-please derives the changelog and version from them, so a message that does not parse
is a change that silently never ships.

Components are versioned independently: `Kvitteria/Backend` today, with `mobile` and `web` reserved.
Each owns its own `VersionPrefix`, marked with an `x-release-please-version` comment that must stay on
the same line as the value.

Merging the release PR creates the tag and publishes the image. Publishing runs **inside** the
release-please workflow rather than on the tag, because a tag pushed with the default token does not
trigger other workflows — an `on: release` job would never fire and every release would appear
successful with no image behind it.

Two one-time repository settings:

- *Settings → Actions → General → Workflow permissions* → allow Actions to create pull requests.
  Without it release-please fails with a 403 that does not say why.
- After the first publish, make the GHCR package public, or nobody can pull it without a token.
