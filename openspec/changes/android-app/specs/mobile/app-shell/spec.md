# Spec Delta

## Purpose

Defines the signed-in app's structure and the behaviour every screen shares: tabs, badges, permissions,
loading, and how failures are shown.

## ADDED Requirements

### Requirement: Five tabs organize the app
The signed-in app SHALL show bottom tabs for Oversigt, Sortér, Kvitteringer, Indsigt and Pipeline, each
keeping its own navigation history. Settings SHALL open from Overview, without the tab bar.

#### Scenario: Switching tabs
- **WHEN** a person opens a receipt, switches to Insights and back
- **THEN** the receipt is still open on the Kvitteringer tab

#### Scenario: Back from a tab root
- **WHEN** a person presses back at the root of a tab other than Oversigt
- **THEN** the app goes to Oversigt

### Requirement: Tabs show outstanding work
The Sortér tab SHALL show the unassigned line count and the Pipeline tab SHALL show the failed mail count,
each hidden at zero. Both SHALL refresh when the app returns to the foreground and after an action that
changes them.

#### Scenario: Line sorted
- **WHEN** a save reports the remaining count
- **THEN** the Sortér badge shows that count

#### Scenario: Nothing outstanding
- **WHEN** a count is zero
- **THEN** that badge is hidden

### Requirement: Writes follow the member's permissions
Controls for an action the member lacks permission for SHALL be hidden, or disabled with a stated reason
where the mockup keeps them visible. The server remains the authority.

#### Scenario: Permission revoked meanwhile
- **WHEN** an action answers 403
- **THEN** the app says the permission is missing and refreshes the member

### Requirement: Data is refreshed explicitly
Every list and dashboard SHALL offer pull-to-refresh and SHALL reload when its screen returns to the
foreground. The app SHALL NOT poll in the background.

#### Scenario: Returning to the app
- **WHEN** the app returns to the foreground on the Pipeline tab
- **THEN** its data reloads

### Requirement: Failures are shown in the right voice
Server `detail` text on 400, 409 and 502 responses SHALL be shown verbatim. Failures the app can categorize
itself, such as no connection or a missing permission, SHALL be localized.

#### Scenario: Mailbox verification fails
- **WHEN** a mailbox save answers 502 with a detail naming the available folders
- **THEN** that text is shown unchanged

#### Scenario: No connection
- **WHEN** a request can't reach the server
- **THEN** a localized message is shown with a retry

### Requirement: Every screen has its states
Each data screen SHALL have distinct loading, empty, error and content states, and a failed refresh SHALL
keep showing the content already loaded.

#### Scenario: Refresh fails
- **WHEN** a refresh fails while content is showing
- **THEN** the content stays and a message reports the failure

#### Scenario: Empty household
- **WHEN** a household has no receipts
- **THEN** screens show their empty state rather than zeros that look like errors
