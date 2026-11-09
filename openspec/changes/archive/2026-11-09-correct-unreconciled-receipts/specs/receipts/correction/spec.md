# Spec Delta

## Purpose

Lets the household correct what extraction got wrong on a stored receipt: its grand total and pant, and its lines,
including lines that were missed or invented.

## ADDED Requirements

### Requirement: Grand total and pant can be corrected
A member with `EditReceipts` SHALL be able to change a receipt's grand total, its pant total, or both. A field left
out SHALL be unchanged. The response SHALL be the updated receipt.

#### Scenario: Correcting the pant
- **WHEN** a member with `EditReceipts` sends a new pant total only
- **THEN** the pant is changed, the grand total is kept, and the returned receipt shows the new state

#### Scenario: Nothing sent
- **WHEN** neither field is sent
- **THEN** the receipt is returned unchanged

#### Scenario: Duplicate flag
- **WHEN** a corrected grand total now matches another receipt from the same merchant and day
- **THEN** the possible-duplicate flag is not changed

#### Scenario: Missing permission
- **WHEN** a member without `EditReceipts` corrects a total
- **THEN** the request is rejected with a 403 response

#### Scenario: Unknown receipt
- **WHEN** the receipt does not exist
- **THEN** the request is rejected with a 404 problem response

### Requirement: A line can be added
A member with `EditReceipts` SHALL be able to add a line to a receipt with a raw name, quantity, unit price and line
total. The raw name SHALL be required, non-blank and at most 300 characters. The response SHALL be 201 with the
line.

#### Scenario: Adding a missed line
- **WHEN** a member with `EditReceipts` adds a line to a receipt
- **THEN** the response is 201, and the receipt has the line and a difference that includes it

#### Scenario: Blank name
- **WHEN** the raw name is missing or only whitespace
- **THEN** the request is rejected with a 400 problem response and nothing is added

#### Scenario: Missing permission
- **WHEN** a member without `EditReceipts` adds a line
- **THEN** the request is rejected with a 403 response

#### Scenario: Unknown receipt
- **WHEN** the receipt does not exist
- **THEN** the request is rejected with a 404 problem response

### Requirement: A line can be deleted
A member with `EditReceipts` SHALL be able to delete a line. Meal coverage that no remaining line on the receipt
carries SHALL be removed in the same transaction. Canonical items and learned aliases SHALL be kept.

#### Scenario: Deleting an invented line
- **WHEN** a member with `EditReceipts` deletes a line
- **THEN** the response is 204, and the line no longer exists or counts towards the difference

#### Scenario: Last line carrying a meal
- **WHEN** the deleted line was the receipt's only line with a meal that has coverage
- **THEN** that coverage is removed with it

#### Scenario: Another line carries the meal
- **WHEN** another line on the receipt carries the same meal
- **THEN** the coverage is kept

#### Scenario: What it taught is kept
- **WHEN** a line whose name has a learned alias is deleted
- **THEN** the alias and its canonical item still exist

#### Scenario: Missing permission
- **WHEN** a member without `EditReceipts` deletes a line
- **THEN** the request is rejected with a 403 response and the line is kept

#### Scenario: Unknown line
- **WHEN** the line does not exist
- **THEN** the request is rejected with a 404 problem response

### Requirement: A receipt keeps at least one line
Deleting a receipt's only line SHALL be refused, with a message saying to delete the receipt instead.

#### Scenario: Deleting the only line
- **WHEN** a member deletes the only line on a receipt
- **THEN** the request is rejected with a 409 problem response and the line is kept
