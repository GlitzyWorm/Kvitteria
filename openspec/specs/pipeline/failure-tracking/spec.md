# pipeline/failure-tracking Specification

## Purpose
Makes mail that could not become a receipt visible to the household, and gives it a way forward: try
again, or dismiss it when it was never a receipt.

## Requirements

### Requirement: Failed mail is kept with its reason
When extraction or storing fails, the system SHALL mark the recorded mail as failed and keep its error
text, number of extraction attempts, and time of failure. A receipt that extracts but does not
reconcile is not a failure.

#### Scenario: Extraction produces nothing usable
- **WHEN** every extraction attempt for a mail fails
- **THEN** the mail is marked failed with an error that includes the underlying cause

#### Scenario: Storing fails
- **WHEN** an extracted mail cannot be stored as a receipt
- **THEN** the mail is marked failed and no partial receipt exists

#### Scenario: Unreconciled extraction
- **WHEN** extraction succeeds but the totals never reconcile
- **THEN** a receipt is stored with its warning and no failure is recorded

### Requirement: Failures can be listed
The system SHALL provide a paged list of failed intakes, newest failure first, to any active household
member. Each entry SHALL carry its source, failure time, error, attempts, and the member it came from when
known. A mail entry SHALL carry subject, sender and mail date; an upload entry SHALL carry its upload time
and whether it has a transcription. Entries SHALL NOT include mail text, transcriptions or images.

#### Scenario: Listing failures
- **WHEN** an active member requests the failure list
- **THEN** they receive the failed intakes with those fields, in that order

#### Scenario: Failed upload listed
- **WHEN** an uploaded image has failed
- **THEN** its entry names upload as the source, its member and upload time, and has an empty subject and
  sender

#### Scenario: Nothing has failed
- **WHEN** no mail is marked failed
- **THEN** the list is empty and the request succeeds

#### Scenario: Mail still in progress
- **WHEN** a mail is queued or being processed
- **THEN** it does not appear in the failure list

#### Scenario: Invalid paging
- **WHEN** the page or page size is out of range
- **THEN** the request is rejected with a 400 problem response

### Requirement: Failed mail can be retried
The system SHALL let a member with `EditReceipts` send a failed mail or upload back through extraction from
its recorded text, using the current pipeline settings. An upload with no stored transcription SHALL be
transcribed again first. The request SHALL return as soon as the intake is queued.

#### Scenario: Retry accepted
- **WHEN** a member with `EditReceipts` retries a failed mail
- **THEN** the mail becomes queued, leaves the failure list, and the response confirms it was accepted

#### Scenario: Retry succeeds
- **WHEN** a retried mail is extracted and stored
- **THEN** a receipt exists and the failure is gone

#### Scenario: Retry fails again
- **WHEN** a retried mail fails again
- **THEN** it returns to the failure list with the new error

#### Scenario: Retrying an upload that was not a receipt
- **WHEN** an upload that failed because no receipt was found is retried
- **THEN** it is transcribed again, since it has no stored transcription

#### Scenario: Mail is not failed
- **WHEN** a retry targets a mail that is queued or already being processed, including a second retry
  sent at the same time
- **THEN** the request is rejected with a 409 problem response and the mail is processed once

#### Scenario: Unknown mail
- **WHEN** a retry targets a mail that does not exist
- **THEN** the request is rejected with a 404 problem response

#### Scenario: Queue is full
- **WHEN** a retry arrives while the extraction queue is full
- **THEN** the mail stays failed and the request is rejected with a 503 problem response

#### Scenario: Missing permission
- **WHEN** a member without `EditReceipts` retries a mail
- **THEN** the request is rejected with a 403 response

### Requirement: Failed mail can be dismissed
The system SHALL let a member with `EditReceipts` permanently delete a failed mail, including its text.
Dismissing SHALL NOT change anything on the mail server.

#### Scenario: Dismissing mail that is not a receipt
- **WHEN** a member with `EditReceipts` dismisses a failed mail
- **THEN** its record and text are deleted and it leaves the failure list

#### Scenario: Mail is not failed
- **WHEN** a dismiss targets a mail that is queued or being processed
- **THEN** the request is rejected with a 409 problem response and the mail is kept

#### Scenario: Missing permission
- **WHEN** a member without `EditReceipts` dismisses a mail
- **THEN** the request is rejected with a 403 response

### Requirement: Unreconciled receipts can be found
The receipt list SHALL accept an optional filter on reconciled state, and each receipt SHALL carry its
stored warning. Unreconciled receipts SHALL NOT be retryable.

#### Scenario: Filtering unreconciled receipts
- **WHEN** a member lists receipts with the filter set to unreconciled
- **THEN** only receipts with reconciled false are returned, each with its warning

#### Scenario: Filter omitted
- **WHEN** a member lists receipts without the filter
- **THEN** the results are the same as before this change

#### Scenario: Receipt with no warning
- **WHEN** a reconciled receipt is returned
- **THEN** its warning is null
