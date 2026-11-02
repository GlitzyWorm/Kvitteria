# deployment/server-update Specification

## Purpose

Defines how a server moves to a newer release from the console: one update path that backs up first, verifies the
result, undoes what it safely can, and leaves a clear way back when it cannot.

## Requirements

### Requirement: One script applies every update
`scripts/update.sh` SHALL be the only implementation of an update, whether started from the console or by the host
agent. Run with no arguments it SHALL move the stack to the newest available release, needing only bash, curl, tar
and Docker Compose.

#### Scenario: From the console
- **WHEN** a person runs `./scripts/update.sh` with a newer release available
- **THEN** the stack ends up on that release

#### Scenario: Already current
- **WHEN** the running version is the newest available
- **THEN** the script says so and changes nothing

#### Scenario: Offline bundle
- **WHEN** a person passes a bundle file with `--bundle`
- **THEN** the script applies that file and does not contact GitHub

### Requirement: Updates never go backwards
The script SHALL refuse a bundle older than the running version, because migrations only go forward.

#### Scenario: Older bundle
- **WHEN** the given bundle's version is below the running version
- **THEN** the script stops before changing anything

### Requirement: Only one update runs at a time
A second update SHALL be refused while one is running, whichever way either was started.

#### Scenario: Console during an app-started update
- **WHEN** a person runs the script while the agent's run is in progress
- **THEN** the second run exits saying an update is running, and the first is unaffected

### Requirement: A development checkout is refused
The script SHALL refuse to run in a git checkout, because it would overwrite tracked files, and would be working
against the development override.

#### Scenario: Run from the repository
- **WHEN** the script is run in a directory that is a git checkout
- **THEN** it stops and changes nothing

### Requirement: A Postgres major version change is refused
The script SHALL compare the Postgres major version the bundle would run with the one running, and SHALL stop before
changing anything if they differ.

#### Scenario: Bundle moves Postgres
- **WHEN** the bundle's compose file selects a different Postgres major than the running server
- **THEN** the update stops, names both versions, and the stack keeps running

#### Scenario: Deployer pinned Postgres
- **WHEN** `.env` pins a `POSTGRES_VERSION` matching the running major
- **THEN** the update proceeds

### Requirement: Download problems change nothing
The bundle's checksum and the new image SHALL both be in place before the running backend is touched.

#### Scenario: Corrupt bundle
- **WHEN** the checksum does not match
- **THEN** the update stops and the stack keeps running

#### Scenario: Image unavailable
- **WHEN** the image cannot be pulled
- **THEN** the update stops and the stack keeps running

### Requirement: A backup precedes every update
The script SHALL take a complete database dump after stopping the backend and before changing the stack, and SHALL
stop if the dump fails. It SHALL keep the three most recent update dumps.

#### Scenario: Backup fails
- **WHEN** the dump cannot be written
- **THEN** the backend is started again on the old version and the update is reported as failed

#### Scenario: Dump scope
- **WHEN** an update's dump is restored
- **THEN** receipts, identities and the keys protecting mailbox passwords all return together

### Requirement: The exact version is pinned
A successful update SHALL set `BACKEND_VERSION` in `.env` to the exact version installed and SHALL change no other
line of that file. The script SHALL keep what it replaced until the next update.

#### Scenario: Floating tag
- **WHEN** an install with `BACKEND_VERSION=latest` is updated to 1.2.0
- **THEN** `.env` holds `BACKEND_VERSION=1.2.0` and everything else in it is unchanged

### Requirement: Floating images stay where they are
An update SHALL pull the backend image and any image whose tag the install doesn't already have. It SHALL NOT move a
floating tag already present.

#### Scenario: Ollama on latest
- **WHEN** an update runs and `ollama:latest` is present locally
- **THEN** the same Ollama image keeps running

#### Scenario: New GoTrue pin
- **WHEN** the bundle selects a GoTrue version not present locally
- **THEN** that version is pulled and started

### Requirement: Changed configuration takes effect
After an update, every service SHALL be running the configuration from the bundle, including a changed Caddyfile.

#### Scenario: Caddyfile changed
- **WHEN** the bundle's Caddyfile differs from the previous one
- **THEN** Caddy serves the new routes after the update

### Requirement: Success means the new backend works
An update SHALL succeed only when `/version` reports the target version and the `postgres` and `migrations` readiness
checks are healthy, read directly from the backend. Other checks SHALL NOT affect the outcome.

#### Scenario: Ollama is unhealthy
- **WHEN** the new backend reports the target version, with postgres and migrations healthy and the model unavailable
- **THEN** the update succeeds

#### Scenario: Never comes up
- **WHEN** the conditions are not met within the time limit
- **THEN** the update has failed

### Requirement: A failure with an unchanged schema rolls back
When an update fails and the applied migrations match those from before it, the script SHALL restore the previous
files and pin, start the previous version and report that it rolled back.

#### Scenario: New backend crashes on startup
- **WHEN** the new backend never becomes healthy and no migration was applied
- **THEN** the previous version runs again and the outcome is `rolled-back`

#### Scenario: Migration fails and is undone
- **WHEN** a migration fails and the migration history is unchanged
- **THEN** the previous version runs again and the outcome is `rolled-back`

### Requirement: A failure with a changed schema stops
When an update fails and the applied migrations differ from those before it, the script SHALL stop the backend, leave
the database untouched and report that a restore is needed. It SHALL NOT restore a dump unattended.

#### Scenario: Unhealthy after migrating
- **WHEN** a migration was applied and the new backend does not become healthy
- **THEN** the backend stays stopped, the dump is kept and the outcome is `restore-required`

### Requirement: A person can restore the pre-update state
`./scripts/update.sh --restore` SHALL replace the whole database with the latest update dump and SHALL start the
version that dump came from. It SHALL ask for typed confirmation and SHALL NOT be reachable through the agent.

#### Scenario: Restoring
- **WHEN** a person confirms a restore after `restore-required`
- **THEN** the database, files and pin are back as they were before the update, and the previous version runs

#### Scenario: Declined
- **WHEN** the confirmation isn't given
- **THEN** nothing changes

#### Scenario: Not interactive
- **WHEN** `--restore` runs with no terminal
- **THEN** it refuses and changes nothing
