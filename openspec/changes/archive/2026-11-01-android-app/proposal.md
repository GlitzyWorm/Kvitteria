# Proposal

## Why

Kvitteria is moving from a .NET MAUI client to a native Android app in Jetpack Compose, and Android
becomes the only client. MAUI has only its auth and settings screens built, with Sort, Receipts,
Insights and Pipeline still stubs, so this is the cheapest point to switch. The app implements the
product mockup and design system against the server, using the Android skills. The server work it
needed (`pipeline-failure-tracking`, `sorting-api`, `spending-api`) has landed.

## What Changes

- Add a native Android app under `android/` at the repo root, built with Compose and following the
  Android skills: feature-layered Gradle modules with convention plugins, MVI, Koin, Ktor,
  kotlinx.serialization and DataStore. Application ID stays `dev.jelshoj.kvitteria`.
- Carry over the existing connection and auth behaviour: server address entry, the gate sequence,
  household claim, sign in and sign up against GoTrue, the rejected state for non-members, encrypted
  session storage, and serialized token refresh.
- Implement Overview, Sort, Receipts and receipt detail, Insights and Pipeline as five bottom tabs, with
  Settings reached from Overview, all gated by the caller's permissions.
- Settings combines the mockup and the existing app: Konto, Husstand, Rettigheder, Postkasser and
  Pipeline.
- Add line correction to receipt detail for members with `EditReceipts`. The mockup has no screen for
  it, but unreconciled receipts are corrected by hand, and this is where that happens.
- Replace the mockup's receipt status filter with All and Unreconciled, because stored receipts are all
  completed and failed mail is shown on the Pipeline tab.
- Transcribe the design system into `core:design-system`: dark-only, Montserrat over Poppins, Phosphor
  icons, the accent as a line and a glow rather than a flood. Faded rules and tabular figures, which
  MAUI could not do, are used, with amounts always in Montserrat because Poppins has no tabular figures.
- Danish first with English as a runtime choice, reusing the existing string tables.
- Online only: no local database in this version. Stored state is the server address, the session and
  the language.
- Copy `kvitteria-tokens.html` and `Kvitteria.html` into `docs/`. They currently live outside the repo,
  and specs must not depend on files only one machine has.
- Generate an OpenAPI snapshot into the repo as the contract between server and app, since the app can
  no longer compile the server's DTOs, and fail CI when either side drifts.
- **BREAKING:** remove `Kvitteria/MobileApp` and `Kvitteria/MobileApp.Tests` once the Android app
  matches their auth and settings screens. This drops the Mac Catalyst target.
- **BREAKING:** fold `Kvitteria/Contracts` into `Backend`, keeping the namespace and wire format. Its
  licence moves from MIT to AGPL with the rest of the backend, because nothing else consumes it.
- `android/` stays under MPL-2.0. Every Kotlin source file carries the Exhibit A header, enforced by
  tooling. The licences screen stays, with the font, icon and dependency notices.
- Out of scope, with no UI: category archive and merge, per-member permission overrides, and the mockup's
  read-only "from the environment" settings list, because the server has none of them.

## Capabilities

### New Capabilities

- `mobile/server-connection`: choosing a server and proving it is a Kvitteria backend.
- `mobile/session`: the gate sequence, sign in and sign up, household claim, rejection, and token
  refresh.
- `mobile/app-shell`: tabs, the Sort badge, permission gating and how server errors are shown.
- `mobile/design-system`: tokens, typography, components and their rules.
- `mobile/localization`: Danish-first strings, runtime language change, and da-DK formatting.
- `mobile/overview`: the month hero, category bars and work cards.
- `mobile/sorting`: working through unassigned lines.
- `mobile/receipts`: the receipt list, search, detail, line assignment, correction and day coverage.
- `mobile/insights`: the category, trend, price and dinner tabs.
- `mobile/pipeline`: stage counts, problems with retry and dismiss, and mailbox status.
- `mobile/settings`: account, household members, permissions, mailboxes and pipeline settings.
- `mobile/licensing`: source headers and the notices that ship with the app.
- `api-contract`: how the published API and the app's models are kept in step.

### Modified Capabilities

None. This consumes the existing server capabilities without changing their requirements.

## Impact

- **Removed:** `Kvitteria/MobileApp`, `Kvitteria/MobileApp.Tests`, `Kvitteria/Contracts`, and
  `Directory.Packages.props` entries used only by them. `Kvitteria.slnx` is updated.
- **Backend:** `Dockerfile` and project references for `Contracts`, and a guard so the OpenAPI document
  can be generated at build time. No runtime behaviour change.
- **Before deleting MAUI:** the logic worth keeping must be ported, including `ServerUrl`,
  `HealthProbe`, `AuthErrorMapper`, `StoredSession`, `Initials` and the draft validators, along with
  their tests.
- **CI and release:** `mobile.yml` becomes a Gradle build without the Mac job, `test.yml` stops running
  `MobileApp.Tests`, and the release-please `Kvitteria/MobileApp` package moves to `android/`, with
  `versionCode` still increasing only.
- **Documentation:** `README.md`, `docs/development.md`, `docs/troubleshooting.md` and much of
  `CLAUDE.md`, including the licensing rules.
- **New toolchain:** JDK, Android SDK and Gradle wrapper. No .NET MAUI workload remains.
- **Wire details the app must handle:** `decimal` amounts must not pass through a floating-point type,
  and `ReceiptStatus` is a number while other enums are names.
