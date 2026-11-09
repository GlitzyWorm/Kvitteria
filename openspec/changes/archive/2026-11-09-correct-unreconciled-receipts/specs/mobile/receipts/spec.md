# Spec Delta

## MODIFIED Requirements

### Requirement: Receipts are listed newest first
The Kvitteringer tab SHALL show receipts newest first with date, merchant and total, loading more as the
list scrolls, and SHALL mark receipts in the `Unreconciled` state.

#### Scenario: Scrolling
- **WHEN** a member scrolls near the end of the list
- **THEN** the next page loads and is appended

#### Scenario: Unreconciled receipt
- **WHEN** a receipt did not reconcile
- **THEN** its row shows an unreconciled tag

#### Scenario: Accepted receipt
- **WHEN** a receipt's difference has been accepted
- **THEN** its row shows no unreconciled tag

### Requirement: Receipts can be searched and filtered
A search field SHALL query the server after typing pauses for 300 ms, and a segmented filter SHALL choose All
or Unreconciled. Both SHALL combine. Unreconciled SHALL show only receipts that still need attention.

#### Scenario: Searching
- **WHEN** a member types and pauses
- **THEN** one request is sent, and the list shows its results

#### Scenario: No matches
- **WHEN** nothing matches
- **THEN** the no-results message is shown

#### Scenario: Out-of-order responses
- **WHEN** an older search answers after a newer one
- **THEN** only the newest result is shown

#### Scenario: Accepted receipts filtered
- **WHEN** a member chooses Unreconciled
- **THEN** receipts whose difference was accepted are not shown

### Requirement: Detail shows the whole receipt
Detail SHALL show date, merchant, member, line count, total, pant, reconciliation state, a possible-duplicate mark
when flagged, the receipt's image when it has one, then the lines, then coverage. The state SHALL be Reconciled,
Accepted with its amount, member and time, or Unreconciled with the current difference. It SHALL paint at once from
the list's data and then refresh.

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

#### Scenario: Accepted receipt
- **WHEN** a receipt's difference has been accepted
- **THEN** detail shows an Accepted tag distinct from Reconciled, with the amount, who accepted it, when, and the
  note if there is one

#### Scenario: Extraction warning
- **WHEN** an unreconciled receipt has a stored extraction warning
- **THEN** it is shown labelled as what extraction reported, not as the current difference

### Requirement: Extracted figures can be corrected
With `EditReceipts`, a line SHALL open an editor for raw name, quantity, unit price and line total, accepting
a decimal comma. It SHALL also show the receipt's remaining difference once saved.

#### Scenario: Correcting a line
- **WHEN** a member corrects a line total on an unreconciled receipt
- **THEN** the line and the shown difference update

#### Scenario: Correction makes it add up
- **WHEN** a correction brings the difference within the tolerance
- **THEN** the refreshed receipt shows Reconciled

#### Scenario: Renaming links the line
- **WHEN** a member renames an unassigned line to a name the household has an alias for
- **THEN** the refreshed line shows its item, and the Sort badge updates

#### Scenario: Invalid number
- **WHEN** a field isn't a valid number
- **THEN** the field shows a localized error and nothing is sent

#### Scenario: No permission
- **WHEN** a member lacks `EditReceipts`
- **THEN** no editor is offered

## ADDED Requirements

### Requirement: Total and pant can be corrected from detail
With `EditReceipts`, detail SHALL offer an editor for the grand total and pant, accepting a decimal comma. A
successful save SHALL refresh the receipt.

#### Scenario: Correcting the pant
- **WHEN** a member corrects the pant on an unreconciled receipt
- **THEN** the receipt refreshes with the new pant and state

#### Scenario: Invalid number
- **WHEN** a field isn't a valid number
- **THEN** the field shows a localized error and nothing is sent

#### Scenario: No permission
- **WHEN** a member lacks `EditReceipts`
- **THEN** no editor is offered

### Requirement: Lines can be added from detail
With `EditReceipts`, detail SHALL offer adding a line with the line editor's fields. A successful add SHALL refresh
the receipt and the Sort badge.

#### Scenario: Adding a missed line
- **WHEN** a member adds a line
- **THEN** the receipt refreshes with the line, and the badge counts it if it is unassigned

#### Scenario: Blank name
- **WHEN** the name is empty
- **THEN** the field shows a localized error and nothing is sent

#### Scenario: No permission
- **WHEN** a member lacks `EditReceipts`
- **THEN** no add is offered

### Requirement: Lines can be deleted from detail
With `EditReceipts`, the line editor SHALL offer delete, which asks for confirmation because it can't be undone. A
successful delete SHALL refresh the receipt and the Sort badge.

#### Scenario: Deleting an invented line
- **WHEN** a member confirms deleting a line
- **THEN** the receipt refreshes without the line, with its coverage as the server holds it

#### Scenario: Only line
- **WHEN** the delete answers 409
- **THEN** the server's message is shown and the line stays

#### Scenario: No permission
- **WHEN** a member lacks `EditReceipts`
- **THEN** no delete is offered

### Requirement: A difference can be accepted from detail
With `EditReceipts`, an unreconciled receipt SHALL offer accepting its difference beside the difference, behind a
confirmation that names the amount and takes an optional note. An accepted receipt SHALL offer withdrawing it.

#### Scenario: Accepting
- **WHEN** a member confirms accepting a difference of 0,25 kr
- **THEN** the receipt shows Accepted, with the amount, the member and the time

#### Scenario: Difference changed meanwhile
- **WHEN** accepting answers 409
- **THEN** the server's message is shown and the receipt refreshes

#### Scenario: Withdrawing
- **WHEN** a member withdraws an acceptance
- **THEN** the receipt shows Unreconciled with its difference

#### Scenario: No permission
- **WHEN** a member lacks `EditReceipts`
- **THEN** neither action is offered
