# Proposal

## Why

Updating a Kvitteria server takes more than the documented `docker compose pull && docker compose up -d`. That
command does nothing on an install that pins `BACKEND_VERSION`, moves floating images nobody meant to move, and
leaves `compose.yaml`, the Caddyfile and the scripts behind, although all of them changed between 1.0.0 and
1.1.0. It takes no backup, and migrations only go forward. Nobody is told a release exists. The stack is meant
to be run by households that manage it from a phone, so updating has to be safe enough to start from there.

## What Changes

- Each backend release carries a **stack bundle**: the compose files, Caddyfile, initdb scripts and update
  tooling for that version. It is uploaded after the image is pushed, so a release with a bundle is one that
  can be installed.
- A new `scripts/update.sh` is the one implementation of an update. It backs up, swaps the files, pins the exact
  version in `.env`, starts the new backend and verifies it. It refuses a development checkout and a change of
  Postgres major version.
- A failed update rolls itself back when the schema is unchanged. Once a migration has applied, it stops and leaves
  the backend down, because only the pre-update dump can undo that. `./scripts/update.sh --restore` is that step,
  and it is never run unattended.
- A host agent, a systemd path unit that `setup.sh` installs, runs the script when asked. The backend asks by
  writing a file whose contents the agent ignores, so no container gets the Docker socket and no caller can
  choose a version. Hosts without systemd or root have no agent.
- The backend checks GitHub daily for a newer installable release, unless configured not to. `GET /api/system/update`
  reports it to every member, and `POST /api/system/update` with `ManageSettings` starts an update.
- Caddy serves a small anonymous `/update-status` file, because the backend is down during an update and is the
  thing that failed after a bad one.
- The app shows an available update in Konto with a button, or the command when there is no agent, and shows
  update progress and failure on the gate.
- Docker Desktop and other hosts without an agent get the notice and the console command, but no button.

Out of scope: automatic nightly updates, a minimum app version, the web interface, signature verification of
bundles, and updating Postgres across major versions.

## Capabilities

### New Capabilities
- `deployment/release-bundle`: what a backend release publishes, and what makes a release installable.
- `deployment/server-update`: how `update.sh` applies an update, verifies it, rolls it back and restores from the
  pre-update dump.
- `deployment/update-agent`: how the host agent is installed, how it is asked to run, and how it reports back
  while the backend is down.
- `deployment/update-check`: how the backend finds a newer release and reports and starts updates over the API.

### Modified Capabilities
- `mobile/settings`: Konto shows an available update and can start one.
- `mobile/session`: the gate reads update status when `/version` fails, rather than only offering a retry.

## Impact

- `.github/workflows/publish-backend.yml`, which gains `contents: write` to upload the bundle.
- `compose.yaml` and `caddy/Caddyfile`, for the state directory and the status route. `.gitignore` and `.env.example`.
- `scripts/setup.sh` and new `scripts/update.sh` and `scripts/systemd/`.
- `Kvitteria/Backend`: a new `Update` options section, a background checker, endpoints, a DTO. No migration and
  no new package. `docs/openapi/kvitteria.json` is regenerated.
- `android/`: `core:data`, `feature:settings` and `feature:auth`, with strings in both languages.
- `README.md`, `CLAUDE.md`, `docs/configuration.md` and `docs/troubleshooting.md`.
- The backend makes its first outbound call that serves neither mail nor models, to the GitHub API.
- An existing install has neither the script nor the state directory, so reaching this release takes one manual
  step, given in its release notes.
- Builds on `public-access`, which is unarchived and edits the same compose, Caddy and setup files. That change
  should land first.
