# Spec Delta

## Purpose

Lets a request that begins inside the stack start an update that runs outside it, without giving any container
control of the host, and keeps the outcome readable while the backend is down.

## ADDED Requirements

### Requirement: Setup installs the agent where it can
`scripts/setup.sh` SHALL install and enable the host agent when run as root on a host with systemd, including on an
install that is already set up. Otherwise it SHALL say the agent was skipped and SHALL finish normally.

#### Scenario: Linux host as root
- **WHEN** setup runs as root on a systemd host
- **THEN** the agent is enabled and survives a reboot

#### Scenario: No systemd
- **WHEN** setup runs on a host without systemd
- **THEN** setup succeeds, and says the app can't start updates there

#### Scenario: Existing install
- **WHEN** setup is run again on an install without the agent
- **THEN** the agent is added and `.env` is left alone

### Requirement: The agent proves it is alive
The agent SHALL refresh a heartbeat in the shared state directory at least every five minutes, and an update SHALL be
startable from inside the stack only while that heartbeat is under fifteen minutes old.

#### Scenario: Agent stopped
- **WHEN** the heartbeat is older than fifteen minutes
- **THEN** the server reports that updates can't be started remotely

### Requirement: A request carries no instructions
A request to update SHALL be the existence of a file. The agent SHALL ignore its contents, and SHALL always run the
update to the newest available release.

#### Scenario: Request names a version
- **WHEN** the request file contains a version or a command
- **THEN** the content is ignored and the newest release is applied

### Requirement: No container controls the host
No service in the stack SHALL mount the Docker socket, and the agent SHALL NOT read any input from the stack other
than the request's existence.

#### Scenario: Backend compromised
- **WHEN** someone controls the backend process
- **THEN** the most they can cause through this channel is an update to the newest published release

### Requirement: Restore is not available to the agent
The agent SHALL NOT run a restore.

#### Scenario: Restore required
- **WHEN** the last outcome is `restore-required` and a new request arrives
- **THEN** the agent leaves the stack alone and the outcome stands

### Requirement: Update status is public while the backend is down
Caddy SHALL serve `GET /update-status` without authentication and without the backend, as JSON holding only the state,
the target version and the time of the last change. It SHALL answer 404 before any update has run.

#### Scenario: During an update
- **WHEN** the backend is stopped for an update
- **THEN** `/update-status` answers with state `running` and the target version

#### Scenario: Stack needs a restore
- **WHEN** the backend is stopped after `restore-required`
- **THEN** `/update-status` answers with that state

#### Scenario: Nothing sensitive
- **WHEN** an anonymous caller reads `/update-status`
- **THEN** the response has no error text, paths, hostnames or current version

#### Scenario: Fresh install
- **WHEN** no update has ever run
- **THEN** `/update-status` answers 404

### Requirement: A stale running state is not trusted
A `running` state whose last change is over thirty minutes old SHALL be read as interrupted, and the agent SHALL
replace a `running` state left over from before a reboot.

#### Scenario: Host lost power mid-update
- **WHEN** the host restarts while an update ran
- **THEN** the state stops being `running`
