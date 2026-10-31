# Spec Delta

## Purpose

Lets the household find any receipt, see what was bought, assign lines, record day coverage and correct
what extraction got wrong.

## ADDED Requirements

### Requirement: Receipts are listed newest first
The Kvitteringer tab SHALL show receipts newest first with date, merchant and total, loading more as the
list scrolls, and SHALL mark unreconciled receipts.

#### Scenario: Scrolling
- **WHEN** a member scrolls near the end of the list
- **THEN** the next page loads and is appended

#### Scenario: Unreconciled receipt
- **WHEN** a receipt did not reconcile
- **THEN** its row shows an unreconciled tag

### Requirement: Receipts can be searched and filtered
A search field SHALL query the server after typing pauses for 300 ms, and a segmented filter SHALL choose All
or Unreconciled. Both SHALL combine.

#### Scenario: Searching
- **WHEN** a member types and pauses
- **THEN** one request is sent, and the list shows its results

#### Scenario: No matches
- **WHEN** nothing matches
- **THEN** the no-results message is shown

#### Scenario: Out-of-order responses
- **WHEN** an older search answers after a newer one
- **THEN** only the newest result is shown

### Requirement: Detail shows the whole receipt
Detail SHALL show date, merchant, member, line count, total, pant, reconciliation state with its delta or
warning, then the lines, then coverage. It SHALL paint at once from the list's data and then refresh.

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

### Requirement: Lines can be assigned from detail
With `CategorizeLines`, each line SHALL have category and meal pickers, each with an explicit clear option.
A change SHALL show at once and roll back with a message if it fails.

#### Scenario: Assigning a category
- **WHEN** a member picks a category
- **THEN** the line shows it before the response arrives

#### Scenario: Request fails
- **WHEN** the request fails
- **THEN** the previous value returns and the failure is shown

#### Scenario: Meal cleared
- **WHEN** clearing a meal removes the last line carrying it
- **THEN** the receipt refreshes, so its coverage matches the server

#### Scenario: No permission
- **WHEN** a member lacks `CategorizeLines`
- **THEN** line values are shown read-only

### Requirement: Day coverage can be set
For meals that track days and appear on a line, coverage SHALL offer steppers from 0 to 31 and show the cost
per day. Tracking meals with no line SHALL be listed as blocked with the reason.

#### Scenario: Increasing days
- **WHEN** a member increases a meal's days
- **THEN** the new set is saved and the receipt refreshes

#### Scenario: Blocked meal
- **WHEN** a tracking meal has no line on the receipt
- **THEN** it has no stepper and the reason is shown

#### Scenario: Rapid taps
- **WHEN** a member taps a stepper several times quickly
- **THEN** the final value is what the server holds

### Requirement: Extracted figures can be corrected
With `EditReceipts`, a line SHALL open an editor for raw name, quantity, unit price and line total, accepting
a decimal comma. It SHALL also show the receipt's remaining difference once saved.

#### Scenario: Correcting a line
- **WHEN** a member corrects a line total on an unreconciled receipt
- **THEN** the line and the shown difference update

#### Scenario: Invalid number
- **WHEN** a field isn't a valid number
- **THEN** the field shows a localized error and nothing is sent

#### Scenario: No permission
- **WHEN** a member lacks `EditReceipts`
- **THEN** no editor is offered
