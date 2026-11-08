# Spec Delta

## MODIFIED Requirements

### Requirement: Failed mail is listed with its reason
Problems SHALL list failed intakes with their member, date, attempts and the error in a monospaced face, in
the server's words. Failed mail SHALL show subject and sender; a failed upload SHALL be labelled as an
uploaded image with its upload time.

#### Scenario: Failed mail
- **WHEN** mail has failed
- **THEN** each failure is listed with its error

#### Scenario: Failed upload
- **WHEN** an uploaded image has failed
- **THEN** it is listed as an uploaded image with its member, upload time and error, and no empty subject
  or sender is shown

#### Scenario: No problems
- **WHEN** nothing failed and nothing is unreconciled
- **THEN** the no-problems message is shown
