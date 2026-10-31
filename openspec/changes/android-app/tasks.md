# Tasks

Gradle commands run from `android/`. Unless a task says otherwise, "tests pass" means
`./gradlew testDebugUnitTest`, and "by hand" means on the `pixel_7_-_api_36_0` AVD, started through `android emulator`,
against `docker compose up -d --build` at `http://10.0.2.2:8080`. Each group leaves a building, running app. Every new
Kotlin file carries the MPL header from group 1 onward.

## 1. Project and contract foundations

Skills: `android-cli`, `android-module-structure`, `agp-9-upgrade`.

- [x] 1.1 Copy `Kvitteria.html` to `docs/mockup.html` and `kvitteria-tokens.html` to `docs/design-tokens.html`, and verify both open correctly from the repo
- [x] 1.2 Create `android/` with the Gradle wrapper, version catalog and `build-logic` convention plugins, and verify `./gradlew help` succeeds
- [x] 1.3 Add every module from the design with its plugin, and verify `./gradlew projects` lists them and an empty Compose app installs and starts by hand
- [x] 1.4 Add `android/LICENSE`, Spotless with the MPL header, and verify `./gradlew spotlessCheck` fails on a file with no header and passes once it has one
- [x] 1.5 Add build-time OpenAPI generation to `docs/openapi/kvitteria.json`, with the `Program.cs` guard, and verify it generates with no database available, or adopt the fallback in `design.md`
- [x] 1.6 Add the generate-and-diff step to `test.yml`, and verify locally that a changed DTO produces a diff and that two runs produce identical files
- [ ] 1.7 Rewrite `mobile.yml` for Gradle with its new path filter, keeping the MAUI build in place until group 12, and verify the workflow passes on a pull request

## 2. Core domain and data

Skills: `android-data-layer`, `android-error-handling`, `android-di-koin`, `android-testing`.

- [ ] 2.1 Add `Result`, `DataError`, `ApiError` and helpers to `core:domain`, and verify tests cover map, success and failure paths
- [ ] 2.2 Add the money, instant and enum serializers, and verify tests cover exact decimals, a missing offset, numeric status and unknown enum values
- [ ] 2.3 Add `safeCall` with problem-details parsing, porting `ProblemDetailTests`, and verify tests cover each status mapping and a non-JSON body
- [ ] 2.4 Add the Keystore cipher and encrypted session store with one blob, and verify by hand that a session survives a restart, and with a test that a corrupt value reads as no session
- [ ] 2.5 Port `StoredSession` and its tests, add `SessionManager`, and verify a `MockEngine` test shows a burst of 401s sending one refresh, a rejected refresh clearing the session, and a network failure keeping it
- [ ] 2.6 Add the two `HttpClient`s and the server-address plugin, and verify tests show `/api` requests carry a bearer, identity requests do not, a session change clears cached tokens, and a changed server applies to the next request
- [ ] 2.7 Add shared domain models, DTOs, mappers and repositories for receipts, taxonomy, members and insights with Koin modules, and verify mapper tests pass
- [ ] 2.8 Add the contract test against `docs/openapi/kvitteria.json`, and verify it passes, and fails when a DTO field is temporarily renamed

## 3. Design system

Skills: `android-compose-ui`, `edge-to-edge`.

- [ ] 3.1 Add fonts, colour, type, spacing, radius and elevation tokens and the theme from `docs/design-tokens.html`, and verify values with a preview of the swatches and type scale against the reference
- [ ] 3.2 Add `KvIcon` and named Phosphor code points for every icon in the mockup, and verify a preview renders each glyph
- [ ] 3.3 Add buttons, tags, fields, segmented control, cards, panels, dialog and rules, with previews, and verify by hand 48dp touch targets on a small control
- [ ] 3.4 Add `KvHeroCard`, `KvAmount` and `KvTabBar`, and verify by hand that a stacked column of amounts aligns and the hero glow matches the mockup
- [ ] 3.5 Add `KrFormat`, Copenhagen date formatting and `UiText` to `core:presentation`, and verify tests cover the examples in `mobile/localization`, including a device in another time zone
- [ ] 3.6 Set up the dark splash, edge-to-edge and system bars, and verify by hand in device light mode that no light frame appears
- [ ] 3.7 Convert the `.resx` strings to `strings.xml`, set up `AppCompatActivity`, locale service and `generateLocaleConfig`, and verify by hand that the language setting changes the app and a German device falls back to Danish

## 4. Connection and session

Skills: `android-presentation-mvi`, `navigation-3`, `android-compose-ui`, `android-testing`.

- [ ] 4.1 Port `ServerUrl`, `HealthProbe`, `AuthErrorMapper` and `Initials` with their test cases, and verify tests pass
- [ ] 4.2 Add root navigation, with `Gate`, `Server`, `Claim`, `Auth` and `Main` replacing the stack, and verify by hand that back can't reach `Main` after sign-out
- [ ] 4.3 Add the gate ViewModel in the documented order, and verify tests cover each branch, including 401 restarting at the claim check and 403 going to rejected
- [ ] 4.4 Add the server screen and debug-only cleartext through a debug-source-set network config, and verify by hand a good address, a wrong service, and an unreachable one
- [ ] 4.5 Add claim, sign-in, sign-up and rejected states, and verify by hand a first account claiming a fresh household, a wrong password, and a non-member seeing the rejected state

## 5. App shell

Skills: `navigation-3`, `android-presentation-mvi`.

- [ ] 5.1 Add five tabs with a back stack each, and verify by hand that tab history survives switching and back behaves as `mobile/app-shell` says
- [ ] 5.2 Add `WorkCounts` and badges, refreshed on `ON_START`, and verify by hand that both counts appear and hide at zero
- [ ] 5.3 Add the shared loading, empty, error and permission-gating components, and verify with a ViewModel test that a failed refresh keeps content

## 6. Settings

Skills: `android-presentation-mvi`, `android-data-layer`, `android-compose-ui`, `android-testing`.

- [ ] 6.1 Add the Settings screen with its sections and Konto, and verify by hand language, change server and sign out
- [ ] 6.2 Add Postkasser, porting `MailboxDraftValidator` and its tests, and verify by hand add, edit keeping the password, a 502 keeping the form, remove, and two mailboxes under one owner
- [ ] 6.3 Add the Pipeline section, porting `PipelineSettingsDraftValidator` and its tests, and verify by hand a model change, an out-of-range value and discard
- [ ] 6.4 Add Husstand with invite and manage, and verify by hand an invite, the 409, and the last-admin message
- [ ] 6.5 Add Rettigheder, and verify by hand a saved toggle, and the read-only state as a member without `ManageSettings`
- [ ] 6.6 Add the licences screen with AboutLibraries and the MPL, OFL and Phosphor notices, and verify by hand offline that every notice opens in full

## 7. Receipts

Skills: `android-presentation-mvi`, `android-compose-ui`, `android-testing`.

- [ ] 7.1 Add the shared category and meal picker sheets in `core:presentation`, and verify with a preview that the category sheet is grouped by parent
- [ ] 7.2 Add the list with paging, debounced search and the All and Unreconciled filter, and verify tests show stale results discarded, and by hand a list of more than one page
- [ ] 7.3 Add detail with instant paint, status, lines and refresh, and verify by hand with a 40-line receipt that scrolling is smooth and unassigned lines stand out
- [ ] 7.4 Add line category and meal changes with rollback, and verify tests cover rollback, and by hand that clearing a covered meal's last line refreshes the coverage
- [ ] 7.5 Add coverage steppers and blocked meals, and verify tests show rapid taps sending one final request
- [ ] 7.6 Add the line editor with decimal comma input, and verify by hand a corrected total on an unreconciled receipt, and that it's absent without `EditReceipts`

## 8. Sorting

Skills: `android-presentation-mvi`, `android-compose-ui`, `android-testing`.

- [ ] 8.1 Add `SuggestionRanker` and name tidying, and verify tests cover the scoring, the confident threshold, `Æ`, `Ø` and `Å`, and `Rødvinseddike 500ml`
- [ ] 8.2 Add the queue data source, item cache and skip set, and verify tests cover paging through more than 100 items
- [ ] 8.3 Add the sort screen with line card, suggestions, search and draft, and verify by hand that sorting a line with suggestions takes three taps or fewer
- [ ] 8.4 Add save, the learned message, 409 reload, skip, and in-place category creation, and verify tests cover each outcome, including that a failed save keeps the draft
- [ ] 8.5 Add the permission states and queue-empty state, and verify by hand as a member without `ManageTaxonomy` and as one without `CategorizeLines`

## 9. Pipeline

Skills: `android-presentation-mvi`, `android-compose-ui`, `android-testing`.

- [ ] 9.1 Add stage cards, poll line and mailbox rows joined with their last error, and verify by hand against a mailbox with a wrong password
- [ ] 9.2 Add failures with retry and a confirmed dismiss, and verify tests cover 404, 409 and 503, and by hand with `ollama` stopped to force a failure
- [ ] 9.3 Add the unreconciled list opening the receipt on the Kvitteringer tab, and verify by hand

## 10. Overview

Skills: `android-presentation-mvi`, `android-compose-ui`, `android-testing`.

- [ ] 10.1 Add the profile bar, hero and month stepping, and verify ViewModel tests cover a null change and stepping months
- [ ] 10.2 Add category bars and work cards, and verify by hand each card's navigation, negative amounts, and an empty household

## 11. Insights

Skills: `android-presentation-mvi`, `android-compose-ui`, `android-testing`.

- [ ] 11.1 Add the view switcher and Kategorier with comparison, sorting, drill-down and movers, and verify tests cover the movers note and by hand that drill-down rows add up
- [ ] 11.2 Add category create and edit dialogs with the parent lock, and verify by hand the 409 keeping the dialog open
- [ ] 11.3 Add Udvikling with its chart and table, and verify by hand six months with a zero month
- [ ] 11.4 Add Priser with its line chart and gaps, and verify by hand a gap breaks the line
- [ ] 11.5 Add Aftensmad, and verify by hand null cost per day and a choice between tracking meals

## 12. Retire MAUI and Contracts

Skills: `r8-analyzer`.

- [ ] 12.1 Check parity of auth and settings against the `MobileApp` screens and tests, and verify every `MobileApp.Tests` case has a Kotlin counterpart
- [ ] 12.2 Delete `Kvitteria/MobileApp` and `Kvitteria/MobileApp.Tests`, remove them from `Kvitteria.slnx`, `Directory.Packages.props` and `test.yml`, drop the MAUI jobs from `mobile.yml`, and verify `dotnet test Backend.Tests/Backend.Tests.csproj` passes
- [ ] 12.3 Move `Contracts` to `Kvitteria/Backend/Contracts`, remove its project, `LICENSE`, and `Dockerfile` lines, and verify `git diff` on `docs/openapi/kvitteria.json` is empty and `docker compose build backend` succeeds
- [ ] 12.4 Move the release-please package and manifest entry to `android` with `x-release-please-version` on `versionName`, and verify with a release-please dry run that it reads the version
- [ ] 12.5 Enable R8 for release with any needed keep rules, and verify `./gradlew assembleRelease` succeeds and a release build installs, signs in and loads receipts on the AVD

## 13. Documentation

- [ ] 13.1 Rewrite `CLAUDE.md`: project list and reference graph, licensing, commands, the mobile sections for Android, and the gotchas that no longer apply, and verify no text refers to MAUI, `Contracts` as a project, or removed paths
- [ ] 13.2 Update `README.md` and `docs/development.md` for building and running the Android app, and verify the documented commands run as written from a clean checkout
- [ ] 13.3 Update `docs/troubleshooting.md` and repoint `docs/design-system.html` and `docs/design-tokens.html` wording from XAML to the Kotlin tokens, and verify a search for `MobileApp` and `XAML` in `docs/` finds nothing that still applies

## 14. Whole-app verification

- [ ] 14.1 On a fresh database, go from first launch through server entry, claiming the household, adding a mailbox, a receipt arriving, sorting it, and seeing it in Overview and Insights, and verify the numbers match the receipt
- [ ] 14.2 Repeat key flows as a second member with default permissions, in English, at 200 % font scale, and verify nothing is clipped or offered that fails with 403

## Workflow follow-up

- Archive this change after review, which adds the `mobile/*` and `api-contract` specs to the main specs.
