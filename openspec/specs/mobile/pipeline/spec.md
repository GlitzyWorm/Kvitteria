# mobile/pipeline Specification

## Purpose

Shows whether receipts are flowing, and gives the household a way to deal with mail that didn't become a
clean receipt.

## Requirements

### Requirement: Stages are shown
The Pipeline tab SHALL show the last poll time and interval, and cards for queued, extracting, persisting and
failed counts.

#### Scenario: Status shown
- **WHEN** a member opens Pipeline
- **THEN** each stage shows its count and the last poll reads relative to now

#### Scenario: Never polled
- **WHEN** no mailbox has polled
- **THEN** the screen says so rather than showing a time

### Requirement: Failed mail is listed with its reason
Problems SHALL list failed mail with subject, sender, member, date, attempts and the error in a monospaced
face, in the server's words.

#### Scenario: Failed mail
- **WHEN** mail has failed
- **THEN** each failure is listed with its error

#### Scenario: No problems
- **WHEN** nothing failed and nothing is unreconciled
- **THEN** the no-problems message is shown

### Requirement: Failed mail can be retried or dismissed
With `EditReceipts`, each failure SHALL offer retry and dismiss. Dismiss SHALL ask for confirmation first,
because it can't be undone.

#### Scenario: Retry
- **WHEN** a member retries a failure
- **THEN** it leaves the list, the queued count rises, and the Pipeline badge updates

#### Scenario: Already handled
- **WHEN** a retry or dismiss answers 404 or 409
- **THEN** the server's message is shown and the list reloads

#### Scenario: Queue full
- **WHEN** a retry answers 503
- **THEN** the failure stays and the message is shown

#### Scenario: No permission
- **WHEN** a member lacks `EditReceipts`
- **THEN** failures are read-only

### Requirement: Unreconciled receipts are listed for correction
Problems SHALL list unreconciled receipts with merchant, date and warning, opening the receipt on tap. They
SHALL NOT offer retry.

#### Scenario: Opening an unreconciled receipt
- **WHEN** a member taps one
- **THEN** that receipt opens on the Kvitteringer tab

### Requirement: Mailboxes show their activity
Pipeline SHALL list each mailbox with member, folder, last mail, receipts this month and a state derived from
the mailbox's last error.

#### Scenario: Healthy mailbox
- **WHEN** a mailbox has no last error
- **THEN** it shows as connected

#### Scenario: Broken mailbox
- **WHEN** a mailbox has a last error
- **THEN** it shows the failure

#### Scenario: Paused mailbox
- **WHEN** a mailbox is disabled
- **THEN** it shows as paused
