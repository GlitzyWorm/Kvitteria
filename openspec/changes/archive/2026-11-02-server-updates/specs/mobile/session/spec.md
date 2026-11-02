# Spec Delta

## MODIFIED Requirements

### Requirement: The gate resolves the start screen
Whenever the app starts or a session ends, the gate SHALL decide in this order: no stored server goes to
server entry; a failed `/version` reads `/update-status`, showing an update in progress or a needed restore if it says
so and otherwise staying on the gate with a retry naming the address; no session goes to
claim or sign-in; an expiring session is refreshed first; `GET /api/users/me` decides the rest.

#### Scenario: First launch
- **WHEN** no server is stored
- **THEN** the app shows server entry

#### Scenario: Server unreachable
- **WHEN** `/version` fails and `/update-status` does not report an update
- **THEN** the gate shows the address with retry and change-server actions, and does not sign out

#### Scenario: Member
- **WHEN** `/api/users/me` answers 200
- **THEN** the app caches the member and opens the tabs

#### Scenario: Back navigation
- **WHEN** a person signs out and presses back
- **THEN** no signed-in screen is reachable

## ADDED Requirements

### Requirement: The gate waits out an update
While `/update-status` reports `running`, the gate SHALL show that the server is updating to the target version, and
SHALL recheck by itself until `/version` answers, then continue resolving. The session SHALL be kept.

#### Scenario: Update finishes
- **WHEN** `/version` starts answering during the wait
- **THEN** the gate continues to the tabs without a sign-in

#### Scenario: Interrupted
- **WHEN** the `running` state's last change is over thirty minutes old
- **THEN** the gate shows the ordinary retry

### Requirement: A needed restore is explained
When `/update-status` reports `restore-required` and `/version` fails, the gate SHALL say the update failed after
changing the database, and SHALL show `./scripts/update.sh --restore` to run on the server with a copy action.

#### Scenario: Update stopped
- **WHEN** the server is down and the state is `restore-required`
- **THEN** the explanation and the command are shown, with a retry, and the session is kept
