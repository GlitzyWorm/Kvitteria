# Spec Delta

## Purpose

Tells a household that a newer server release exists, and gives clients one API to read that and to start an update,
so the app and a later web interface behave the same.

## ADDED Requirements

### Requirement: The backend looks for releases daily
The backend SHALL check the configured repository's releases shortly after starting and then once a day. A failed
check SHALL be logged, SHALL keep the last known result, and SHALL NOT affect readiness.

#### Scenario: GitHub unreachable
- **WHEN** a check fails
- **THEN** the previous result stands and `/health/ready` is unchanged

### Requirement: Only installable backend releases count
The newest available release SHALL be the highest semantic version among published backend releases that carry a
bundle. Releases of other components, drafts and prereleases SHALL be ignored.

#### Scenario: A mobile release is newest
- **WHEN** the newest release in the repository is `mobile-v2.0.0` and the newest backend one is 1.2.0
- **THEN** 1.2.0 is reported

#### Scenario: No bundle yet
- **WHEN** backend 1.3.0 has no bundle and 1.2.0 has
- **THEN** 1.2.0 is reported

#### Scenario: Comparison is numeric
- **WHEN** 1.10.0 and 1.9.0 are both available
- **THEN** 1.10.0 is reported

### Requirement: The check can be turned off and pointed elsewhere
Configuration SHALL be able to disable the check and SHALL be able to name the repository, so a fork can follow
itself. The default SHALL be enabled and following this project.

#### Scenario: Disabled
- **WHEN** checking is off
- **THEN** the backend makes no request to GitHub and reports no available update

#### Scenario: A fork
- **WHEN** a repository is configured
- **THEN** the backend reads that repository's releases

### Requirement: Development builds report no update
A running version that isn't a plain release version SHALL never have an update available.

#### Scenario: Local build
- **WHEN** the backend reports `0.0.0-dev`
- **THEN** no update is available

### Requirement: Any member can read the update status
`GET /api/system/update` SHALL report to any active member the running version, the newest available version, whether
an update is available, a link to its release notes, when it was last checked, whether an update can be started from
here, and the last update's state and detail.

#### Scenario: Update available
- **WHEN** a member reads the status and a newer release is known
- **THEN** the response names it with its release link

#### Scenario: Up to date
- **WHEN** the running version is the newest known
- **THEN** no update is available

#### Scenario: Not signed in
- **WHEN** an anonymous caller reads it
- **THEN** the answer is 401, and `/version` reveals nothing about updates

#### Scenario: After a rollback
- **WHEN** the last update rolled back
- **THEN** the state is `rolled-back` with the script's reason

### Requirement: Starting an update needs `ManageSettings`
`POST /api/system/update` SHALL take no body, SHALL require `ManageSettings`, and on success SHALL record the request
and answer 202 before anything restarts.

#### Scenario: Started
- **WHEN** a member with `ManageSettings` starts an available update and the agent is alive
- **THEN** the answer is 202 and the agent runs the update

#### Scenario: No permission
- **WHEN** a member without `ManageSettings` starts one
- **THEN** the answer is 403

### Requirement: An update that can't start is refused with a reason
`POST /api/system/update` SHALL answer 409 when no update is available or one is running, and 503 when no live agent
is present, each as problem details naming the reason.

#### Scenario: Already running
- **WHEN** an update is in progress
- **THEN** the answer is 409

#### Scenario: Nothing to install
- **WHEN** no newer release is known
- **THEN** the answer is 409

#### Scenario: No agent
- **WHEN** the heartbeat is missing or stale
- **THEN** the answer is 503 and the detail names the console command
