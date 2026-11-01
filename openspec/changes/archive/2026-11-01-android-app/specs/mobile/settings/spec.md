# Spec Delta

## Purpose

Gives members their account and language, and lets those with permission run the household: its members,
what they may do, its mailboxes and its pipeline.

## ADDED Requirements

### Requirement: Settings has five sections
Settings SHALL offer Konto, Husstand, Rettigheder, Postkasser and Pipeline, and SHALL reload the shown section
when opened and on a section change.

#### Scenario: Opening settings
- **WHEN** a member opens Settings
- **THEN** Konto is shown and every section is reachable

### Requirement: Konto shows the account
Konto SHALL show the member's initials, name, email, role and permissions, the language choice, the server
address with a change action, the app and server versions, a link to licences, and sign out.

#### Scenario: Signing out
- **WHEN** a member signs out
- **THEN** the session is cleared and the app returns to the gate

### Requirement: Husstand manages members
Husstand SHALL list every member with initials, name, email, role, status and last seen, marking the current
member. With `ManageUsers` it SHALL offer invite and manage.

#### Scenario: Inviting
- **WHEN** a member with `ManageUsers` invites an address
- **THEN** the member is pre-authorized, and the app explains that no email is sent

#### Scenario: Duplicate address
- **WHEN** the server answers 409
- **THEN** its message is shown in the dialog

#### Scenario: Managing a member
- **WHEN** an admin changes a member's name, role or status
- **THEN** the change is saved and the list refreshes

#### Scenario: Last admin
- **WHEN** a change would remove the last active admin
- **THEN** the server's message is shown in the dialog

#### Scenario: No permission
- **WHEN** a member lacks `ManageUsers`
- **THEN** the list is read-only

### Requirement: Rettigheder sets what members may do
Rettigheder SHALL list every permission with its label and description, showing which members hold. With
`ManageSettings` a change SHALL save at once and roll back on failure. Without it the list SHALL be read-only
with the reason.

#### Scenario: Toggling a permission
- **WHEN** a member with `ManageSettings` turns a permission on
- **THEN** the full set is saved

#### Scenario: Save fails
- **WHEN** the save fails
- **THEN** the toggle returns and the failure is shown

### Requirement: Postkasser manages mailboxes
Postkasser SHALL list the current member's mailboxes first and other members' after, each showing its last
error. With `ManageMailboxes` it SHALL offer add, edit, enable, and remove with confirmation.

#### Scenario: Several mailboxes of mine
- **WHEN** a member owns two mailboxes
- **THEN** both appear under their own mailboxes

#### Scenario: Validation
- **WHEN** a required field is missing or the port isn't between 1 and 65535
- **THEN** the field shows a localized error and nothing is sent

#### Scenario: Verification fails
- **WHEN** a save answers 502
- **THEN** the server's message is shown and the form stays open with its values

#### Scenario: Password on edit
- **WHEN** a member edits a mailbox and leaves the password empty
- **THEN** the stored password is kept

#### Scenario: Owner
- **WHEN** a member adds a mailbox
- **THEN** they can choose the owner from the household's members

### Requirement: Pipeline settings can be edited
The Pipeline section SHALL show every pipeline setting with the model chosen from Ollama's list. With
`ManageSettings` it SHALL validate locally with the server's ranges, and offer save and discard only while
something changed. Without it the form SHALL be read-only with the reason.

#### Scenario: Out of range
- **WHEN** a value is outside its range
- **THEN** the field shows the range and save is disabled

#### Scenario: Model unavailable
- **WHEN** the model list can't be loaded
- **THEN** the current model is still shown and the other settings can be saved

#### Scenario: Discarding
- **WHEN** a member discards changes
- **THEN** the fields return to the saved values
