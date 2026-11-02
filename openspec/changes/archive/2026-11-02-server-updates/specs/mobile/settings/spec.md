# Spec Delta

## MODIFIED Requirements

### Requirement: Konto shows the account
Konto SHALL show the member's initials, name, email, role and permissions, the language choice, the server
address with a change action, the app and server versions, the server's update state, a link to licences, and
sign out.

#### Scenario: Signing out
- **WHEN** a member signs out
- **THEN** the session is cleared and the app returns to the gate

#### Scenario: Update state unavailable
- **WHEN** the update status can't be loaded
- **THEN** Konto shows everything else, and no update row

## ADDED Requirements

### Requirement: Konto announces an available update
When a newer server version is available, Konto SHALL show that version with a link to its release notes to every
member. Starting it SHALL be offered according to the member's permission and whether the server can start updates.

#### Scenario: Can start it
- **WHEN** a member with `ManageSettings` views an available update and the server can start one
- **THEN** an update action is offered

#### Scenario: No agent
- **WHEN** a member with `ManageSettings` views an available update and the server can't start one
- **THEN** the console command is shown with a copy action, and no update action

#### Scenario: No permission
- **WHEN** a member without `ManageSettings` views an available update
- **THEN** the version is shown with the reason it can't be started, and no action

### Requirement: Starting an update is confirmed
The update action SHALL ask for confirmation, saying the server goes offline for a few minutes and a backup is taken
first. A refusal SHALL show the server's message.

#### Scenario: Refused
- **WHEN** the server answers 409 or 503
- **THEN** its message is shown and nothing else changes

### Requirement: Konto follows a started update
After confirming, Konto SHALL show progress and SHALL recheck `/update-status` and `/version` by itself until the update
reaches an outcome, because the old server keeps answering while the image downloads.

#### Scenario: Succeeds
- **WHEN** `/version` reports the target version
- **THEN** Konto says the update finished and shows the new server version

#### Scenario: Rolls back
- **WHEN** the state becomes `rolled-back` or `failed`
- **THEN** progress ends and Konto shows the outcome

#### Scenario: Needs a restore
- **WHEN** the state becomes `restore-required`
- **THEN** the app returns to the gate, which explains it

#### Scenario: Agent never starts
- **WHEN** the state hasn't reached `running` after a minute
- **THEN** progress ends and Konto says the update didn't start

### Requirement: Konto reports the last update's outcome
Konto SHALL show when the last update rolled back, with the reason.

#### Scenario: Rolled back
- **WHEN** the last state is `rolled-back`
- **THEN** Konto says the server stayed on its version, with the reason
