# Spec Delta

## Purpose

Decides whether a stored receipt's figures add up, lets the household accept a difference that is printed
correctly but doesn't fit the rule, and says which receipts still need a person.

## ADDED Requirements

### Requirement: Reconciliation state follows the stored figures
A receipt's difference SHALL be its grand total less the sum of its line totals and its pant. The receipt SHALL be
`Reconciled` when the difference is within the reconciliation tolerance currently in pipeline settings, whenever
it is read. Nothing stored SHALL decide it.

#### Scenario: Correction makes it add up
- **WHEN** a line total is corrected so the difference falls within the tolerance
- **THEN** the receipt reads as `Reconciled` on its next read

#### Scenario: Edit breaks a reconciled receipt
- **WHEN** a figure on a reconciled receipt is changed so the difference leaves the tolerance
- **THEN** the receipt reads as `Unreconciled`

#### Scenario: Tolerance raised
- **WHEN** an admin raises the tolerance above an unreconciled receipt's difference
- **THEN** that receipt reads as `Reconciled` without being edited

#### Scenario: Stored before this change
- **WHEN** a receipt stored before this change is read
- **THEN** its state follows from its figures and the current tolerance

### Requirement: A difference can be accepted
A member with `EditReceipts` SHALL be able to accept the current difference of a receipt that is not `Reconciled`, with an
optional note of at most 200 characters. The request SHALL state the difference the member saw. The acceptance
SHALL record that amount, the member and the time.

#### Scenario: Accepting
- **WHEN** a member with `EditReceipts` accepts a receipt's difference, stating the current difference
- **THEN** the receipt reads as `Accepted`, with the amount, the member, the time and the note

#### Scenario: Difference changed meanwhile
- **WHEN** the stated difference is not the receipt's current difference
- **THEN** the request is rejected with a 409 problem response and nothing is recorded

#### Scenario: Already reconciled
- **WHEN** the receipt is `Reconciled`
- **THEN** the request is rejected with a 409 problem response

#### Scenario: Accepting again
- **WHEN** a receipt with an acceptance is accepted again at its current difference
- **THEN** the new acceptance replaces the old one

#### Scenario: Note too long
- **WHEN** the note is longer than 200 characters
- **THEN** the request is rejected with a 400 problem response

#### Scenario: Missing permission
- **WHEN** a member without `EditReceipts` accepts a difference
- **THEN** the request is rejected with a 403 response

#### Scenario: Unknown receipt
- **WHEN** the receipt does not exist
- **THEN** the request is rejected with a 404 problem response

### Requirement: An acceptance holds only for the accepted difference
A receipt SHALL be `Accepted` only while it is not `Reconciled` and its current difference equals the accepted
amount exactly. Otherwise the acceptance SHALL have no effect and SHALL NOT be reported.

#### Scenario: Figures edited after accepting
- **WHEN** a line on an accepted receipt is edited and the difference changes
- **THEN** the receipt reads as `Unreconciled` and no acceptance is reported

#### Scenario: Edit restores the accepted difference
- **WHEN** later edits bring the difference back to exactly the accepted amount
- **THEN** the receipt reads as `Accepted` again

#### Scenario: Edit makes it add up
- **WHEN** an accepted receipt is corrected so its difference is within the tolerance
- **THEN** it reads as `Reconciled`

### Requirement: An acceptance can be withdrawn
A member with `EditReceipts` SHALL be able to withdraw a receipt's acceptance. Withdrawing a receipt that has none
SHALL succeed and change nothing.

#### Scenario: Withdrawing
- **WHEN** a member withdraws an accepted receipt's acceptance
- **THEN** the response is 204 and the receipt reads as `Unreconciled`

#### Scenario: Nothing to withdraw
- **WHEN** the receipt has no acceptance
- **THEN** the response is 204

#### Scenario: Missing permission
- **WHEN** a member without `EditReceipts` withdraws an acceptance
- **THEN** the request is rejected with a 403 response and the acceptance is kept

### Requirement: Receipts report their reconciliation state
Every receipt in an API response SHALL carry its state by name, `Reconciled`, `Accepted` or `Unreconciled`, and,
when `Accepted`, the accepted amount, the member's ID and display name, the time and the note.

#### Scenario: Unreconciled receipt
- **WHEN** an unreconciled receipt is returned
- **THEN** its state is `Unreconciled` and no acceptance is included

#### Scenario: Accepted receipt
- **WHEN** an accepted receipt is returned
- **THEN** its state is `Accepted` and the acceptance is included

### Requirement: The unreconciled filter returns receipts needing attention
Listing receipts with `reconciled=false` SHALL return only `Unreconciled` receipts. `reconciled=true` SHALL return
`Reconciled` and `Accepted` receipts. Paging and the total count SHALL follow the filter.

#### Scenario: Accepted receipt filtered
- **WHEN** a member lists receipts with `reconciled=false`
- **THEN** accepted receipts are not returned

#### Scenario: Corrected receipt filtered
- **WHEN** a receipt has been corrected until it reconciles
- **THEN** it is not returned with `reconciled=false`
