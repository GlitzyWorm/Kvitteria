# Development

## Layout

```
Kvitteria/                  repo root — compose, docs, CI
├── android/                the Android app, in Gradle
├── caddy/Caddyfile         the single entrypoint's routing
├── docs/openapi/           the API contract between the server and the app
├── postgres/initdb/        roles, schema and extensions, on a fresh volume only
├── scripts/                setup and a bearer-token helper
└── Kvitteria/              the .NET root
    ├── Backend/            the application
    ├── Backend.Tests/      unit tests
    ├── Directory.Build.props     shared MSBuild properties
    ├── Directory.Packages.props  every package version
    └── global.json               the pinned SDK
```

The app and the backend share no code. The app is an HTTP client of the server, not a part of it, and
`docs/openapi/kvitteria.json` is the contract between them.

That separation is also a licensing boundary. The backend is AGPL-3.0-or-later, and the app is MPL-2.0 so
it can ship through app stores. Every Kotlin source file under `android/` needs the MPL Exhibit A header,
and `./gradlew spotlessCheck` fails on a file without one.

The Docker build context is `Kvitteria/`, not the repo root, because that is where both props files
and `global.json` live. A future Angular app goes at the repo root beside `caddy/`.
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

## Working on the Android app

You need JDK 21 and the Android SDK with platform 37 and an emulator image. Gradle comes with the
wrapper. From `android/`:

```bash
./gradlew assembleDebug
```

```bash
adb install -r app/build/outputs/apk/debug/app-debug.apk
```

Modules are listed in `settings.gradle.kts`, shared build logic lives in `build-logic/`, and every version
is in `gradle/libs.versions.toml`. Every Kotlin file needs the MPL header, which this adds:

```bash
./gradlew spotlessApply
```

Point the app at the stack through Caddy, the same address a browser would use. An Android emulator
reaches the host at `10.0.2.2`, so that's `http://10.0.2.2:8080`. **Emulator only, for now.** A release
build refuses plain HTTP, so a physical device needs this served over TLS.

Plain HTTP works in a debug build because `src/debug/res/xml/network_security_config.xml` overrides the one
in `src/main`, which forbids it.

The design system can be looked at on its own, in a debug build:

```bash
adb shell am start -n dev.jelshoj.kvitteria/.DesignGalleryActivity
```

### Signing in

The app asks for a server address before anything else. What happens after that depends on the server:

- **No members yet** — the readiness report's `household` check says `Degraded`, and the app shows
  the claim screen before sending you to sign-up. Signing up then claims the household as admin.
  There is no claim endpoint; `UserProvisioning.Decide` settles it as a side effect of the first
  authenticated request, so the screen exists to say so beforehand.
- **Already claimed** — straight to sign in / sign up. An email an admin has not pre-authorized
  gets an account at GoTrue and a 403 from `/api/users/me`, which the app shows as "ingen adgang"
  with a sign-out button rather than as a failed sign-in.

To walk the unclaimed branch again on a stack that already has members, empty the table:

```bash
docker compose exec postgres psql -U postgres -c 'truncate "AppUsers" cascade'
```

```bash
adb shell pm clear dev.jelshoj.kvitteria
```

Clearing app data resets the stored server, session and language. In the running app, Settings is the
gear on Overview, and has sign-out, change server and the language under Konto.

### The API contract

The app has its own hand-written copy of each wire type, and `docs/openapi/kvitteria.json` keeps the two
in step. After changing an endpoint or DTO, from `Kvitteria`:

```bash
dotnet build Backend/Backend.csproj -p:GenerateOpenApi=true
```

`test.yml` fails when that file is stale, and `ContractTest` in the app fails, naming the model and field,
when a wire model no longer matches it. A new wire model goes in that test's list.

## Tests

```bash
cd Kvitteria
dotnet test Backend.Tests/Backend.Tests.csproj
```

`Backend.Tests` covers the pure logic that has no other safety net: the reconciliation tolerance,
Danish date parsing including the DST gap, the category tree rules, meal-coverage diffing, paging,
the HTML-to-text converter, the user-provisioning decision, permission resolution and the last-admin
guard, the auth options rules, mailbox validation, and the mailbox secret round-trip.

```bash
cd android
./gradlew spotlessCheck testDebugUnitTest test
```

The app's tests cover the same kind of logic plus its ViewModels: server-address normalization, reading
the household state out of a readiness report, mapping GoTrue's error codes, token refresh, amounts and
dates, suggestions, and the wire models against the OpenAPI document. `testDebugUnitTest` runs the Android
modules and `test` the pure JVM ones.

There are no integration or endpoint tests of the backend, and no instrumented tests of the app. Anything
touching HTTP or the database is verified by bringing the stack up and reading
`docker compose logs -f backend`, and on an emulator for the app.

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

Endpoints are static classes under `Features/Endpoints`, and errors are RFC 7807 problem details.
There is no repository layer and no mapper layer; do not add one. Structural rules that are worth
testing live outside the endpoint files as pure helpers.

DTOs are split: the records live in `Backend/Contracts` (`Kvitteria.Contracts.DTO`), and each one's
`ToDto()` lives in `Backend/Features/Endpoints/Extensions/` because it takes an EF entity.
Comments describe the code as it is, not the change that produced it.

## Releases

Commit messages are [Conventional Commits](https://www.conventionalcommits.org/), enforced on pull
requests. release-please derives the changelog and version from them, so a message that does not parse
is a change that silently never ships.

Components are versioned independently: `backend` and `mobile` today, with `web` reserved. Each owns
its own version — `VersionPrefix` in `Backend.csproj`, `versionName` in `android/app/build.gradle.kts` —
marked with an `x-release-please-version` comment that must stay on the same line as the value. That file
is the single source of truth: the Dockerfile overrides it only when CI supplies a version, so a plain
`docker build` reports whatever release-please last wrote rather than a placeholder.

The app's `versionCode` is a separate thing from its display version: an integer that must only ever
increase. CI passes it from the run number with `-PversionCode=`, and the default in the build file only
has to be valid.
Merging the release PR creates the tag and publishes the image. Publishing runs **inside** the
release-please workflow rather than on the tag, because a tag pushed with the default token does not
trigger other workflows — an `on: release` job would never fire and every release would appear
successful with no image behind it.

Two one-time repository settings:

- *Settings → Actions → General → Workflow permissions* → allow Actions to create pull requests.
  Without it release-please fails with a 403 that does not say why.
- After the first publish, make the GHCR package public, or nobody can pull it without a token.
