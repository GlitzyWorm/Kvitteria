# pipeline/durable-intake Specification

## Purpose
Guarantees that a receipt mail, once fetched, cannot disappear between the mailbox and the database,
whether processing fails or the server restarts.

## Requirements

### Requirement: Fetched mail is recorded before it is marked read
The system SHALL durably record each fetched mail, including its text, before flagging it read on the
mail server.

#### Scenario: Recording fails
- **WHEN** a mail is fetched and the system cannot record it
- **THEN** the mail stays unread on the mail server and is fetched again on a later poll

#### Scenario: Recording succeeds
- **WHEN** a mail is fetched and recorded
- **THEN** the mail is flagged read and the record shows it as queued

### Requirement: Unfinished mail resumes after a restart
On startup the system SHALL resume every recorded mail and every recorded uploaded image that was neither
stored as a receipt nor marked failed, without any user action.

#### Scenario: Restart while mail is queued
- **WHEN** the server restarts with mail recorded as queued
- **THEN** that mail is processed after startup and becomes a receipt or a failure

#### Scenario: Restart during extraction
- **WHEN** the server restarts while a mail is being extracted or stored
- **THEN** that mail is extracted again from its recorded text

#### Scenario: Restart with an upload in progress
- **WHEN** the server restarts while an uploaded image is queued, being transcribed, extracted or stored
- **THEN** it is processed after startup, reusing its transcription when one was stored

#### Scenario: Shutdown is not a failure
- **WHEN** the server shuts down while a mail is in progress
- **THEN** the mail is not reported as failed

### Requirement: A mail is processed once
The system SHALL NOT start processing a mail whose message ID already belongs to a stored receipt or a
recorded mail. Such a mail SHALL still be flagged read.

#### Scenario: Same mail in two mailboxes
- **WHEN** a mail is fetched whose message ID is already recorded from another mailbox
- **THEN** no second record is created and the fetched copy is flagged read

#### Scenario: Mail marked unread after being stored
- **WHEN** a mail whose receipt is already stored is marked unread and fetched again
- **THEN** no duplicate receipt is created

#### Scenario: Previously lost mail marked unread
- **WHEN** a mail with no stored receipt and no record is marked unread and fetched
- **THEN** it is processed as new

### Requirement: Mail text is kept only while unresolved
The system SHALL delete a recorded mail, including its text, and a recorded upload, including its
transcription, in the same transaction that stores its receipt. An upload's image SHALL move to the receipt
rather than be deleted.

#### Scenario: Receipt stored
- **WHEN** a recorded mail is stored as a receipt
- **THEN** its record no longer exists

#### Scenario: Upload stored
- **WHEN** a recorded upload is stored as a receipt
- **THEN** its record and transcription no longer exist, and the receipt has the image

#### Scenario: Storing the receipt fails
- **WHEN** storing the receipt fails
- **THEN** no receipt exists and the recorded mail or upload is kept
