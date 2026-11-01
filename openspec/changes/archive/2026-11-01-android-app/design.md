# Design

## Context

See `proposal.md` for motivation and `specs/` for required behaviour.

- `Kvitteria/MobileApp` has real auth and settings screens, and stubs elsewhere. Its value is behaviour
  rather than code, and `CLAUDE.md` records why each piece works the way it does.
- The server API is complete for every screen. `docs/mockup.html` shows the intended result, including a
  mobile layout, and its `STRINGS` table holds both languages.
- `DataError.Network` in `android-error-handling` is an enum, while this API's 400, 409 and 502 `detail`
  text has to reach the screen verbatim.
- Montserrat has an OpenType `tnum` feature and Poppins doesn't (checked in the font files).
- `Program.cs` migrates the database before `Run`, and OpenAPI is mapped only in Development, so the document
  can't be fetched in CI without a running stack unless it's generated at build time.
- This machine has JDK 21, SDK 36, the `android` CLI, and an AVD named `pixel_7_-_api_36_0`.

## Goals / Non-Goals

**Goals:**

- Follow the Android skills, and record each departure with its reason.
- Keep every lesson the MAUI app paid for, as tests where possible.
- Leave the repository with one client and no MAUI residue.

**Non-Goals:**

- Offline support, a local database, or queued writes.
- Push notifications or background sync.
- iOS, Kotlin Multiplatform, tablets or foldables beyond working at larger widths.
- Play Store publishing and release signing.

## Skills

Apply these during implementation. Each task group in `tasks.md` names its own.

| Skill | Used for |
|---|---|
| `android-module-structure` | Modules, convention plugins, version catalog |
| `android-cli` | Project creation, SDK, running on the emulator, screenshots |
| `android-presentation-mvi` | State, Action, Event, ViewModel, Root/Screen split, `UiText` |
| `android-compose-ui` | Composables, stability, design system, previews, accessibility |
| `android-data-layer` | Ktor client, DTOs, mappers, data sources, token storage |
| `android-error-handling` | `Result`, `DataError`, helpers |
| `android-di-koin` | Koin modules per layer |
| `navigation-3` | Tabs, conditional navigation |
| `edge-to-edge` | Insets, keyboard, system bars |
| `android-testing` | ViewModel tests, fakes |
| `r8-analyzer` | Keep rules for the release build |
| `agp-9-upgrade` | Reference for AGP 9 and built-in Kotlin, as the project starts on it |

Departures from them are marked **Departure** below.

## Decisions

### Modules

```
android/
  build-logic/                 convention plugins
  app/                         assembles everything, root navigation
  core/domain                  shared models, Result, repository interfaces
  core/data                    Ktor, session, shared repositories, money and date serializers
  core/presentation            UiText, formatting, pickers, ObserveAsEvents
  core/design-system           tokens, theme, components, fonts
  feature/auth/{domain,data,presentation}
  feature/sort/{domain,data,presentation}
  feature/pipeline/{domain,data,presentation}
  feature/settings/{domain,data,presentation}
  feature/overview/presentation
  feature/receipts/presentation
  feature/insights/presentation
```

Receipts, taxonomy, insights figures and the member are used by several features, so by the skill's rule they
live in `core`. **Departure:** the three features that own no data of their own have no `domain` or `data`
module, rather than empty ones.

`android/` sits at the repo root beside `caddy/`, because `Kvitteria/` is the .NET root and Docker build context.

The build starts on the current stable AGP 9 with built-in Kotlin, compile SDK 37, target SDK 36 and min SDK 26, which
gives `java.time` without desugaring. Compile SDK is 37 because current stable AndroidX and OkHttp require it; target
stays 36 because compile SDK doesn't change runtime behaviour. Versions live in `gradle/libs.versions.toml` and are the latest stable at
implementation time.

### Navigation

**Departure:** this uses Navigation 3 rather than the Nav2 route graphs in `android-navigation`. It's a new
project with nothing to migrate, and both hard parts are published Navigation 3 recipes: multiple back stacks and
conditional navigation. The skill's principles still apply: `@Serializable` routes in each feature's presentation
module, one entry-provider extension per feature, cross-feature navigation by callbacks, and assembly in `:app`.

The root back stack holds `Gate`, `Server`, `Claim`, `Auth` or `Main`, and moving between these replaces the whole
stack, so back can't reach a signed-in screen after sign-out. It also holds `Settings` and `Licenses` above `Main`,
which is why they have no tab bar. `Main` hosts one back stack per tab.

Dialogs and sheets are screen state rather than navigation entries, because their results stay inside one MVI
screen. Routes carry IDs only.

### Session and network

`SessionManager` in `core:data` owns the stored session, exposes it as a `StateFlow`, and holds the refresh
`Mutex`. It re-checks the token after acquiring the lock, which is how a burst of 401s becomes one refresh. A
refresh rejected by GoTrue clears the session. One that fails on the network keeps it.

The two `HttpClient`s from MAUI stay, using the OkHttp engine: anonymous for GoTrue and the probes, and authenticated
for `/api`, using Ktor's bearer `Auth` plugin as the skill shows, with `loadTokens` and `refreshTokens` delegating to
`SessionManager`. The plugin caches tokens, so a session change calls `clearToken()`. GoTrue must use the anonymous
client, or a refresh would recurse.

The server address is read per request by a small plugin that composes the absolute URL, so changing server needs
no rebuilt client. `ignoreUnknownKeys` is on, so the server can add fields.

The app root observes the session and returns to the gate when it's cleared, so no screen handles that itself.

### Storage

The server address goes in DataStore. The session goes in DataStore as one AES-GCM encrypted JSON value, with the key
in Android Keystore, so a torn session can't exist. `security-crypto` is deprecated, so the cipher is a small class
of our own. `android:allowBackup` is false, because a Keystore key doesn't survive restore. A value that can't be
decrypted is treated as no session.

Language is stored by AppCompat's locale service. There's no Room in this version.

### Errors

**Departure:** alongside the skill's `DataError`, API calls return `Result<T, ApiError>`, where
`ApiError(kind: DataError.Network, detail: String?)` keeps the problem-details text. `safeCall` reads `detail` from
any problem+json body. `ApiError.toUiText()` uses `detail` verbatim when it's present on 400, 409, 502 and 503, and
a localized string otherwise. Network and permission failures are always localized. This preserves the MAUI
`ApiOutcome` convention in the skill's shape.

### Wire types

DTOs are hand-written `@Serializable` classes beside each data source, mapped to domain models as the skill shows.
Generating them was rejected, because the DTO count is small, and a generator is another toolchain to own.

- **Money** is `java.math.BigDecimal` through a serializer that reads the raw JSON number's text, so a value never
  passes through `Double`. Domain modules are JVM-only, so this is safe to use throughout.
- **Instants** are `java.time.Instant`, and a parser tolerates a missing offset by reading it as UTC.
- **`ReceiptStatus`** reads a number, with an `Unknown` fallback. Other enums read names, also with `Unknown`.

### Keeping the contract

`Microsoft.Extensions.ApiDescription.Server` generates the document during a build, to `docs/openapi/kvitteria.json`.
It runs the entry point up to `Run`, so `Program.cs` skips `MigrateDatabaseAsync` when started by that tool. Generation
is off by default and enabled in one step of `test.yml`, followed by `git diff --exit-code`. If the tool can't run
without a database, the fallback is a `scripts/` command that fetches `/openapi/v1.json` from the Development stack,
with CI running Postgres as a service.

A JVM test in `core:data` compares each DTO's serial descriptor with its schema in that file, using an explicit map of
DTO to schema name. It checks names and nullability only, because .NET 10 describes `decimal` as number-or-string.
That's about 100 lines, and it catches what a shared project used to catch at compile time.

`Contracts` moves to `Kvitteria/Backend/Contracts/`, keeping its namespaces, so no `using` changes. The document is
generated before and after to prove nothing moved.

### Design system

`core:design-system` holds tokens as `@Immutable` objects read through `KvitteriaTheme`, and maps them onto a Material 3
dark `ColorScheme` so that system-styled pieces come out dark. Screens use the design-system components, which are thin
over `foundation` rather than Material components restyled: `KvButton`, `KvTag`, `KvCard`, `KvPanel`, `KvField`,
`KvSegmented`, `KvDialog`, `KvHeroCard`, `KvRule`, `KvIcon`, `KvTabBar`, `KvAmount`.

Values come from `docs/design-tokens.html`, whose hex values are already resolved from oklch. Type sizes come from its
spec column and `Tokens.xaml`, which agree. The samples on that page render smaller, and the spec column wins. Fractional
spacing is kept in `dp`.

`KvAmount` is the one place amounts are drawn: Montserrat with `fontFeatureSettings = "tnum"`. The faded rule is a
horizontal gradient brush, and the hero glow is a radial brush plus a `Shadow` on the text style. Both replace MAUI
approximations.

Touch targets use `minimumInteractiveComponentSize`, so a 30dp stepper draws at 30dp and reacts at 48dp. The reference's
44 is raised to Android's 48. Charts are plain `Canvas`. Three simple charts don't justify a chart library.

`Phosphor-Regular.ttf` is drawn through `KvIcon` from named code points, moved from `PhosphorIcons.cs` and extended from
Phosphor's own `style.css` for the icons the mockup uses.

### Localization

`res/values/strings.xml` is Danish and `res/values-en/strings.xml` is English, mirroring the MAUI arrangement so an
untranslated device gets Danish. The 153 existing keys are converted from the `.resx` files, then the mockup's `STRINGS`
fill the gaps. Strings live in the module that uses them, with shared ones in `core:presentation`.

Runtime change uses `AppCompatDelegate.setApplicationLocales`, which needs `AppCompatActivity` and the locale service in
the manifest. It restarts the activity, so every string updates with no language plumbing in composables. That removes
the MAUI `LocalizationProvider` entirely. `generateLocaleConfig` publishes the two languages to the system's per-app
language setting.

`KrFormat` formats amounts by hand rather than through `NumberFormat`, so the true minus sign and the placement of `kr` are
exact, and it's tested. Dates go through a fixed `Europe/Copenhagen` zone.

### Screens and state

Each screen follows `android-presentation-mvi`. Details that aren't obvious from it:

- **Instant detail.** `ReceiptRepository` keeps listed receipts in a memory cache, and detail paints from `peek(id)`
  and then refreshes.
- **Badges.** A singleton `WorkCounts` flow in `core` holds the two counts. The shell refreshes it on `ON_START`, and
  saves update it directly.
- **Taxonomy.** Categories, meals and the item list are cached in memory for the session, with a manual refresh and
  invalidated by writes in the app. The item list pages through `GET /canonical-items` at 100 per page.
- **Suggestions.** `SuggestionRanker` in `feature:sort:domain` ports the mockup's scoring as a tested pure function.
  Tokenizing uses `[^a-zæøå0-9]+` on lower-cased text with the root locale.
- **Skips** are a process-lifetime set in `feature:sort:data`, so they survive tab changes and end with the process.
- **Optimistic line edits** snapshot the previous value in the ViewModel and restore it on failure.
- **Coverage steppers** debounce by 400 ms and send the receipt's full coverage set, because the endpoint replaces it.
- **Search** uses `debounce(300)` with `flatMapLatest`, which is what discards stale results.
- **Unreconciled list** comes from `GET /receipts?reconciled=false`, and each row shows the stored warning.
- **Mailbox state on Pipeline** joins `PipelineStatusDto.Mailboxes` with `GET /mailboxes` by ID, because the status DTO
  has no error or enabled flag.
- **Pickers** are bottom sheets. The category sheet is grouped by parent, because a two-level tree needs no generic tree
  control.

### Testing

Pure logic is ported with its MAUI test cases, as JUnit 5 and AssertK: `ServerUrl`, `HealthProbe`, `AuthErrorMapper`,
`StoredSession`, `Initials`, `ProblemDetail`, both draft validators, plus `KrFormat`, `SuggestionRanker` and the
serializers. ViewModels are tested with Turbine and fakes as `android-testing` describes. `SessionManager` and the HTTP
client are tested with Ktor's `MockEngine`, especially the refresh burst. End-to-end checks are by hand on the AVD
against the compose stack, using `android run` and `android screen`.

### Repository cleanup

`MobileApp` and `MobileApp.Tests` go in one commit once the Android app reaches auth and settings parity, followed by
`Contracts`. Removing them also removes the Mobile group in `Directory.Packages.props`, the second half of `test.yml`,
and `Kvitteria.slnx` entries.

`mobile.yml` keeps its path filter, now on `android/**` and `docs/openapi/**`, with `spotlessCheck`, unit tests, lint and
`assembleRelease`. Release builds run R8, so CI catches serialization keep-rule problems that only appear when minified.
`versionCode` comes from `-PversionCode=${{ github.run_number }}`.

release-please moves its package to `android`, keeping the `mobile` component so tags stay continuous, with
`x-release-please-version` on `versionName` in `app/build.gradle.kts`. Its manifest entry moves with it.

## Risks / Trade-offs

- [Navigation 3 differs from most of the skills' examples] → Its bundled recipes cover what's needed, and routes and
  feature entries stay simple enough to move if it disappoints.
- [Hand-written DTOs drift] → The contract test and snapshot check catch names and nullability, but not value semantics.
- [Build-time OpenAPI may need more guarding] → A fallback is defined, and the first task proves which one applies.
- [Ktor's bearer plugin caches tokens] → `clearToken()` on every session change, covered by a test.
- [Language change restarts the activity] → Screen state is in ViewModels and saved state, so it survives.
- [Compact design against 48dp targets] → Visual sizes stay, and hit areas grow. Dense rows may need a little extra gap,
  which is checked on the AVD.
- [Online only] → Nothing works without the server. Failed loads are explicit, and a draft survives a failed save.
- [Large change] → Task groups build in order and each leaves a running app, so the work can pause at any group.

## Migration Plan

1. Build the app beside MAUI, with both on `main`. Nothing is released yet.
2. At auth and settings parity, delete MAUI, then fold `Contracts`.
3. No server deployment is involved, apart from the additive `Program.cs` guard.
4. Rollback before release is a revert of the deletion commits. `dev.jelshoj.kvitteria` is reused, and no installed
   version exists to upgrade from.
