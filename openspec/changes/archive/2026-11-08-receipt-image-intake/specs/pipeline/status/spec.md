# Spec Delta

## MODIFIED Requirements

### Requirement: Stage counts are reported
The system SHALL report to any active member how many recorded mails and uploads are queued, extracting
(including transcribing), being stored and failed, and how many stored receipts are unreconciled. Counts SHALL
come from durable records.

#### Scenario: Mail in several stages
- **WHEN** a member requests pipeline status while mail is in progress
- **THEN** each stage reports its own count

#### Scenario: Upload being transcribed
- **WHEN** an uploaded image is being transcribed
- **THEN** it is counted as extracting

#### Scenario: Idle pipeline
- **WHEN** nothing is in progress and nothing has failed
- **THEN** every stage count is zero and the request succeeds

#### Scenario: After a restart
- **WHEN** status is requested after a restart and before resumed mail has been picked up
- **THEN** that mail is still counted
