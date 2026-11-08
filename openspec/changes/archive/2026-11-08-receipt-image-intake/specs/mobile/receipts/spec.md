# Spec Delta

## MODIFIED Requirements

### Requirement: Detail shows the whole receipt
Detail SHALL show date, merchant, member, line count, total, pant, reconciliation state with its delta or
warning, a possible-duplicate mark when flagged, the receipt's image when it has one, then the lines, then
coverage. It SHALL paint at once from the list's data and then refresh.

#### Scenario: Opening from the list
- **WHEN** a member opens a receipt
- **THEN** its content appears immediately and is replaced by the refreshed copy

#### Scenario: Line without an item
- **WHEN** a line has no canonical item
- **THEN** it says so, distinguishably from assigned lines

#### Scenario: Member unknown
- **WHEN** the receipt has no known member
- **THEN** the member part of the line is left out

#### Scenario: Opened by ID
- **WHEN** detail is opened from Overview or Pipeline with only an ID
- **THEN** it loads the receipt, with a loading state

#### Scenario: Receipt with an image
- **WHEN** a receipt came from an uploaded image
- **THEN** detail shows a thumbnail that opens the image full screen with zoom

#### Scenario: Image fails to load
- **WHEN** the image request fails
- **THEN** the thumbnail shows a retry, and the rest of detail is unaffected

#### Scenario: Possible duplicate
- **WHEN** a receipt is flagged as a possible duplicate
- **THEN** detail says so and opens the matching receipt on tap

## ADDED Requirements

### Requirement: Receipts can be deleted from detail
With `EditReceipts`, detail SHALL offer delete, which asks for confirmation because it can't be undone. A
successful delete SHALL return to the list without the receipt.

#### Scenario: Deleting a duplicate
- **WHEN** a member with `EditReceipts` confirms deleting a receipt
- **THEN** the app returns to the list and the receipt is gone from it

#### Scenario: Already deleted
- **WHEN** the delete answers 404
- **THEN** the server's message is shown and the app returns to the reloaded list

#### Scenario: No permission
- **WHEN** a member lacks `EditReceipts`
- **THEN** no delete is offered
