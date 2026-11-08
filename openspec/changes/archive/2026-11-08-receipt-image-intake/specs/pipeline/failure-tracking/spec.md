# Spec Delta

## MODIFIED Requirements

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
