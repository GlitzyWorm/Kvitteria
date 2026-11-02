# Tasks

Hand checks in this list use a throwaway stack (`COMPOSE_PROJECT_NAME=kvitteria-check`, its own `.env` values and
port), so the development database is left alone. It must be a copy of the files outside any git checkout, because
`update.sh` refuses one. There are no endpoint or integration tests of the backend, so anything touching Caddy,
compose or the database is verified that way. `--bundle` makes the script testable without publishing a release.

## 0. Prerequisite

- [x] 0.1 Confirm `public-access` has landed on the base branch and verify `compose.public.yaml` and the
  `{$SITE_ADDRESS::8080}` Caddyfile are present before starting

## 1. Release bundle

- [x] 1.1 Add `scripts/bundle.sh`, which builds `kvitteria-stack-<version>.tar.gz` and `.sha256` from a checkout, and
  verify with `tar -tzf` that the files in the release-bundle spec are present and `compose.override.yaml` is not
- [x] 1.2 Add a step to `publish-backend.yml` after the image push that runs `bundle.sh` and uploads both files with
  `gh release upload --clobber`, set `contents: write`, and check out `backend-v<version>` on `workflow_dispatch`;
  verify the workflow parses with `actionlint` or a dry `workflow_dispatch` on a fork
- [x] 1.3 Add "new variables need a default in compose" to the Gotchas in `CLAUDE.md` and describe the bundle in the
  release notes section of `docs/development.md`; verify both read correctly beside their neighbours

## 2. Update script: happy path

- [x] 2.1 Add `update-state/` and `.update/` to `.gitignore`, and to `compose.yaml` mount `./update-state` on the
  backend and `./update-state/public` read-only on Caddy; verify `docker compose config` is valid in all three modes
- [x] 2.2 Add `handle /update-status` to the Caddyfile serving `public/update-status.json` as JSON, and verify on the
  throwaway stack that it answers 404 with no file, the file's contents with one, and keeps answering with the backend
  stopped
- [x] 2.3 Check whether `docker compose stop backend` also stops Caddy on the Compose version in use, and record the
  result in `design.md`; verify `/update-status` still answers after whichever stop command is chosen
- [x] 2.4 Write `scripts/update.sh` up to the first change: lock, status writes, target resolution via `grep -oE` and
  `sort -V`, `--bundle`, and the refusals (git checkout, older, Postgres major); verify each refusal on the throwaway
  stack leaves the stack and files unchanged
- [x] 2.5 Add download, checksum, image pull, backend stop, dump with retention of three, and migration-history capture;
  verify a corrupted bundle and an unpullable tag each leave the stack running, and a dump restores into a scratch
  database
- [x] 2.6 Add the file swap to `.update/previous/`, the `exec` hand-over with `--continue`, the `BACKEND_VERSION` pin,
  `up -d --no-deps backend`, verification through `/version` and the two readiness checks, the remaining services and
  the Caddy restart on a changed hash; verify a move between two locally tagged images end to end, with `.env` differing
  only on its pin
- [x] 2.7 Verify the update succeeds with Ollama's model missing, and that `ollama:latest` isn't moved by an update
- [x] 2.8 Rewrite the Upgrading section of `README.md` around `update.sh`, including the Windows note, and verify the
  documented commands run as written

## 3. Update script: failure handling

- [x] 3.1 Add rollback when migration history is unchanged and verify with an image that exits at startup: the old
  version runs again and status is `rolled-back`
- [x] 3.2 Add the `restore-required` stop when history changed, and verify with an image that applies a migration and
  then exits: the backend stays stopped, the dump is kept and `/update-status` reports it
- [x] 3.3 Add `--restore` with typed confirmation and the terminal check, and verify on that same stack: the database,
  including `auth`, the files and the pin return, the previous version runs, a sign-in works and a stored mailbox password
  still decrypts; verify it refuses with stdin closed
- [x] 3.4 Add a section on failed updates to `docs/troubleshooting.md` and verify each state in the rollback table has
  an entry

## 4. Host agent

- [x] 4.1 Add `scripts/systemd/` units for the path watch, the oneshot service and the heartbeat timer, with the
  service running `update.sh` and clearing a leftover `running` at boot; verify on the LXC that creating `request`
  runs the script once and removes the file
- [x] 4.2 Have `setup.sh` create `update-state/public` owned by `$APP_UID`, and install and enable the units when run as
  root under systemd, otherwise reporting the skip; verify re-running on an existing install adds the agent, leaves
  `.env` alone, and that a run as non-root still completes
- [x] 4.3 Have the agent run refuse while the state is `restore-required`, and verify a request in that state changes
  nothing
- [x] 4.4 Document the agent, its requirements and what it can and can't do in `docs/configuration.md`, and the design
  rationale in `CLAUDE.md`; verify `grep -r docker.sock compose*.yaml` finds nothing

## 5. Backend

- [x] 5.1 Add `UpdateOptions` (`Enabled`, `Repository`) with `AddOptionsSection`, and the compose variables; add
  `ReleaseSelection` and tests in `Backend.Tests` for the mobile-newest, no-bundle, numeric-comparison, prerelease and
  development-build cases; verify with `dotnet test Backend.Tests/Backend.Tests.csproj`
- [x] 5.2 Add the `UpdateChecker` background service with a typed `HttpClient`, and verify in the logs that a failing
  request keeps the last result, leaves `/health/ready` alone, and that `Update__Enabled=false` makes no request
- [x] 5.3 Add an `UpdateState` reader for `status.json` and the heartbeat, covering missing files, bad JSON, stale
  heartbeat and stale `running` as pure decisions with tests; verify with `dotnet test`
- [x] 5.4 Add `SystemEndpoints` with `GET` and `POST /api/system/update` and `UpdateStatusDto` with its `ToDto`, mapped
  under `/api`, `POST` on `ManageSettings` with 202, 409 and 503; regenerate `docs/openapi/kvitteria.json`, and verify by
  hand: 401 anonymous, 403 without permission, each 409 and 503 case, and `request` appearing on 202
- [x] 5.5 Add the endpoints and `Update:*` to `docs/configuration.md`, add `Features/Update` to `CLAUDE.md` and the test
  list there, and verify `test.yml`'s OpenAPI check passes locally

## 6. Android

- [x] 6.1 Add the wire types and `UpdateRepository` in `core`, including the anonymous `/update-status` read and the
  `Unknown` state fallback; add the DTO to `ContractTest` and parsing tests for `/update-status`, including a 404; verify
  with `./gradlew spotlessCheck testDebugUnitTest test` from `android/`
- [x] 6.2 Add the update row to Konto with its three permission cases, the console command with copy, and a load
  failure that hides the row, with strings in both languages; verify with ViewModel tests
- [x] 6.3 Add the confirmation and the Konto progress state polling both endpoints to an outcome, including the one
  minute start timeout; verify with ViewModel tests using a fake repository and a test clock
- [x] 6.4 Add the gate's `/update-status` read, the waiting state with automatic recheck and the restore explanation;
  extend `GateViewModelTest` for running, finished, stale `running`, `restore-required` and a 404; verify with the same
  Gradle command
- [x] 6.5 Add the new gate state and the Konto rule to the Android section of `CLAUDE.md`, and verify the gate's numbered
  steps there match the `mobile/session` spec

## 7. End-to-end

- [x] 7.1 On a throwaway copy on the LXC, verify from an AVD: Konto shows an update, the button runs it, progress ends
  on the new version with the session kept, and a second device opened mid-update waits on the gate
- [x] 7.2 Verify the same flow with a failing image for `rolled-back` and a migrating failing image for
  `restore-required`, with the gate showing the command
- [x] 7.3 Verify the bootstrap from a real 1.1.0 install and write it into the release notes draft: the manual fetch,
  the update, `setup.sh` as root, then a working button
- [x] 7.4 Run `openspec validate server-updates --strict`, `dotnet test`, and `./gradlew spotlessCheck testDebugUnitTest
  test`, and verify all pass

## Workflow follow-up

- Update the home server through the bootstrap once the release is out, since it pins `BACKEND_VERSION=1.1.0`.
- Archive the change after `public-access` is archived, so `deployment/` exists in the main specs.
