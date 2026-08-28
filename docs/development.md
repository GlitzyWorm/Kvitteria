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
    ├── Contracts/          the wire types the backend and the app share
    ├── MobileApp/          .NET MAUI, Android and Mac Catalyst
    ├── Directory.Build.props     shared MSBuild properties
    ├── Directory.Packages.props  every package version
    └── global.json               the pinned SDK
```

`MobileApp` references `Contracts`, `Backend` references `Contracts`, and `MobileApp` does **not**
reference `Backend`. The app is an HTTP client of the server, not a part of it, and `Contracts` is the
only code they share.

That missing reference is also a licensing boundary. The backend is AGPL-3.0-or-later, the app is
MPL-2.0 so it can ship through app stores, and `Contracts` is MIT so neither license reaches the other
through the code both compile. A `ProjectReference` from the app to the backend would undo it. Every
source file under `MobileApp/` needs the MPL Exhibit A header; `MobileApp/.editorconfig` promotes
IDE0073 to an error so a new `.cs` file without one fails the build, and XAML headers are manual.

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

## Working on the mobile app

One-time, plus a JDK and an Android SDK with an emulator image:

```bash
dotnet workload install maui-android
```

Then, from `Kvitteria`:

```bash
dotnet build MobileApp/MobileApp.csproj -f net10.0-android
```

Each platform target is gated on the host that can build it: Android everywhere, Windows on Windows,
Mac Catalyst on macOS. Mac Catalyst needs a macOS host with Xcode, so on Windows or Linux it is not in
the target list at all and the only place it gets built is the `maccatalyst` job in `mobile.yml`. iOS
is absent; add it the same way when there is a Mac to build on.

The shipping targets are Android and Mac Catalyst, but **Windows is the one to develop against** —
it is much the fastest way to see a change, and `Properties/launchSettings.json` has exactly one
profile, `Windows Machine`, which is what F5 selects. If that profile ever runs with no
`net10.0-windows` target in the project, Visual Studio fails with `The target process exited without
raising a CoreCLR started event` and exit code `0x80008083`, which points at nothing useful.

**Visual Studio caches the target-framework list per project.** After editing `TargetFrameworks`,
unload and reload the project (or restart VS) before trusting the debug-target dropdown — a stale list
shows targets the project no longer has and hides ones it just gained, which reads as the project
being broken rather than the IDE being behind. Deleting `MobileApp/obj` forces the issue.

Android emulator entries come from the Android SDK's own AVDs, managed in *Tools → Android → Android
Device Manager*. The legacy `avdmanager` in `Sdk/tools/bin` throws `NoClassDefFoundError:
javax/xml/bind/annotation/XmlSchema` on any JDK 11 or newer, so use the Device Manager or install
`cmdline-tools` if a scriptable path is ever needed.

Point the app at the stack through Caddy on `http://localhost:8080`, the same base URL a browser
would use. **Emulator and simulator only.** iOS App Transport Security blocks plaintext HTTP, and an
Android device on the same network cannot reach a `localhost` stack either, so a physical device needs
this served over TLS first. An Android emulator reaches the host at `10.0.2.2`.

### Signing in

The app asks for a server address before anything else. A Debug build prefills
`http://10.0.2.2:8080` — the emulator's route to the host, on the port `.env.example` gives Caddy — so
F5 to signed-in needs no typing. A Release build starts empty.

Plain HTTP works there **only because it is a Debug build**. `AndroidManifest.xml` points
`networkSecurityConfig` at `@xml/network_security_config`, and `MobileApp.csproj` supplies that
resource from one of two files depending on configuration: the Debug one permits cleartext, the
Release one forbids it. A shipped app therefore needs the stack behind TLS. Both files exist so the
manifest's reference always resolves — excluding a single file in Release would fail at `aapt2` with
a message about the manifest instead.

What happens after the address is accepted depends on the server:

- **No members yet** — the readiness report's `household` check says `Degraded`, and the app shows
  the claim screen before sending you to sign-up. Signing up then claims the household as admin.
  There is no claim endpoint; `UserProvisioning.Decide` settles it as a side effect of the first
  authenticated request, so the screen exists to say so beforehand.
- **Already claimed** — straight to sign in / sign up. An email an admin has not pre-authorized
  gets an account at GoTrue and a 403 from `/api/users/me`, which the app shows as "ingen adgang"
  with a sign-out button rather than as a failed sign-in.

To walk the unclaimed branch again on a stack that already has members, empty the table and
reinstall the app so its stored session goes with it:

```bash
docker compose exec postgres psql -U postgres -c 'truncate "AppUsers" cascade'
```

The session lives in `SecureStorage` and the server address in `Preferences`, so clearing app data
(or reinstalling) is what resets the flow. In the running app, the control in the top-right corner of
the overview offers **Log ud**, **Skift server** and the language toggle.

## Tests

```bash
cd Kvitteria
dotnet test Backend.Tests/Backend.Tests.csproj
dotnet test MobileApp.Tests/MobileApp.Tests.csproj
```

Name the project. A bare `dotnet test` resolves to `Kvitteria.slnx`, which now contains the MAUI app,
so it builds that too on the way to running 132 assertions. CI is scoped the same way, for the same
reason plus a larger one: Mac Catalyst cannot build on a Linux runner at all.

`Backend.Tests` covers the pure logic that has no other safety net: the reconciliation tolerance,
Danish date parsing including the DST gap, the category tree rules, meal-coverage diffing, paging,
the HTML-to-text converter, the user-provisioning decision, permission resolution and the last-admin
guard, the auth options rules, mailbox validation, and the mailbox secret round-trip.

`MobileApp.Tests` covers the mobile app's pure logic on the same principle: server-address
normalization, reading the household state out of a readiness report, mapping GoTrue's error codes,
and token-expiry arithmetic.

It **links those four files as source** rather than referencing `MobileApp`, because it cannot
reference it: `MobileApp` targets `net10.0-android` (plus Windows or Mac Catalyst per host) and a
plain `net10.0` project cannot reference any of those. The cost is one `<Compile Include>` line per
helper; the benefit is that a helper reaching for `Preferences`, `SecureStorage` or anything else out
of `Microsoft.Maui` stops compiling here immediately. Anything that needs a MAUI type belongs in
`MobileApp/Services`, which is deliberately not linked in.

That is also why these run in `test.yml` beside the backend tests rather than in `mobile.yml`: they
install no MAUI workload, so a backend pull request pays nothing for them, and `test.yml` is the
workflow without a paths filter.

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

Endpoints are static classes under `Features/Endpoints`, and errors are RFC 7807 problem details.
There is no repository layer and no mapper layer; do not add one. Structural rules that are worth
testing live outside the endpoint files as pure helpers.

DTOs are split: the records live in `Contracts` (`Kvitteria.Contracts.DTO`) because the app compiles
against them, and each one's `ToDto()` lives in `Backend/Features/Endpoints/Extensions/` because it
takes an EF entity and `Contracts` has to stay referenceable from MAUI. `Contracts` carries records
and wire enums, has no package references at all, and must never gain a `FrameworkReference` to
`Microsoft.AspNetCore.App` — that alone would break the app build.

Comments describe the code as it is, not the change that produced it.

## Releases

Commit messages are [Conventional Commits](https://www.conventionalcommits.org/), enforced on pull
requests. release-please derives the changelog and version from them, so a message that does not parse
is a change that silently never ships.

Components are versioned independently: `backend` and `mobile` today, with `web` reserved. Each owns
its own version — `VersionPrefix` in `Backend.csproj`, `ApplicationDisplayVersion` in
`MobileApp.csproj` — marked with an `x-release-please-version` comment that must stay on the same line
as the value. That project file is the single source of truth: the Dockerfile overrides it only when
CI supplies a version, so a plain `docker build` reports whatever release-please last wrote rather
than a placeholder.

`Contracts` is not a component. Nothing distributes it, so its own tag would be noise; a contracts
change committed alongside the consumer it serves already lands in that consumer's changelog, because
release-please attributes a commit to every package path it touches.

The app's `ApplicationVersion` is a separate thing from its display version: Android's `versionCode`,
an integer that must only ever increase. CI passes it from the run number, and the value in the
project file only has to be valid.

Merging the release PR creates the tag and publishes the image. Publishing runs **inside** the
release-please workflow rather than on the tag, because a tag pushed with the default token does not
trigger other workflows — an `on: release` job would never fire and every release would appear
successful with no image behind it.

Two one-time repository settings:

- *Settings → Actions → General → Workflow permissions* → allow Actions to create pull requests.
  Without it release-please fails with a 403 that does not say why.
- After the first publish, make the GHCR package public, or nobody can pull it without a token.
