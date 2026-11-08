# receipts/deletion Specification

## Purpose
Lets the household remove a receipt that should not exist, such as a duplicate or an upload of the wrong
image, together with everything that belongs only to it.

## Requirements

### Requirement: A receipt can be deleted
The system SHALL let a member with `EditReceipts` permanently delete a receipt, with its lines, meal coverage
and image, in one transaction. Merchants, canonical items and learned aliases SHALL be kept.

#### Scenario: Deleting a receipt
- **WHEN** a member with `EditReceipts` deletes a receipt
- **THEN** the response is 204, and the receipt, its lines, its coverage and its image no longer exist

#### Scenario: What it taught is kept
- **WHEN** a receipt whose lines created an alias is deleted
- **THEN** the alias and its canonical item still exist

#### Scenario: Unknown receipt
- **WHEN** a delete targets a receipt that does not exist
- **THEN** the request is rejected with a 404 problem response

#### Scenario: Missing permission
- **WHEN** a member without `EditReceipts` deletes a receipt
- **THEN** the request is rejected with a 403 response and the receipt is kept

#### Scenario: Mail marked unread after deletion
- **WHEN** the mail of a deleted receipt is marked unread and fetched again
- **THEN** it is processed as new, the same as mail whose receipt was never stored
