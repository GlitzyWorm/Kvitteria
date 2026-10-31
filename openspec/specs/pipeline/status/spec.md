# pipeline/status Specification

## Purpose
Tells the household what the pipeline is doing right now and whether mail is arriving, so a stalled or
broken pipeline can be noticed without reading server logs.

## Requirements

### Requirement: Stage counts are reported
The system SHALL report to any active member how many recorded mails are queued, extracting, being
stored and failed, and how many stored receipts are unreconciled. Counts SHALL come from durable
records.

#### Scenario: Mail in several stages
- **WHEN** a member requests pipeline status while mail is in progress
- **THEN** each stage reports its own count

#### Scenario: Idle pipeline
- **WHEN** nothing is in progress and nothing has failed
- **THEN** every stage count is zero and the request succeeds

#### Scenario: After a restart
- **WHEN** status is requested after a restart and before resumed mail has been picked up
- **THEN** that mail is still counted

### Requirement: Polling activity is reported
Pipeline status SHALL include the most recent successful poll across all mailboxes and the configured
poll interval.

#### Scenario: Mailboxes have polled
- **WHEN** at least one mailbox has polled successfully
- **THEN** status gives the latest poll time and the interval in seconds

#### Scenario: No mailbox has polled
- **WHEN** no mailbox has ever polled successfully
- **THEN** the latest poll time is null and the request succeeds

### Requirement: Mailbox activity is reported
Pipeline status SHALL include, per mailbox, when mail was last received and how many stored receipts
came from it in the current calendar month in Danish local time.

#### Scenario: Mailbox with receipts this month
- **WHEN** a mailbox has receipts purchased in the current month
- **THEN** its entry gives their count and the time mail last arrived

#### Scenario: Month boundary
- **WHEN** a receipt was purchased just after midnight Danish time on the first of the month
- **THEN** it counts towards the new month, even though it is still the previous month in UTC

#### Scenario: Mailbox with no activity
- **WHEN** a mailbox has never received mail
- **THEN** its last-received time is null and its count is zero

#### Scenario: Receipts from before attribution
- **WHEN** a receipt has no known mailbox
- **THEN** it is not counted towards any mailbox
