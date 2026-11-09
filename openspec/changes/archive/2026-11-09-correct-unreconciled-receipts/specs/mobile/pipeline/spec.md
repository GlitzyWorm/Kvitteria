# Spec Delta

## MODIFIED Requirements

### Requirement: Unreconciled receipts are listed for correction
Problems SHALL list receipts in the `Unreconciled` state with merchant, date and warning, opening the receipt on
tap. They SHALL NOT offer retry. Receipts whose difference was accepted SHALL NOT be listed.

#### Scenario: Opening an unreconciled receipt
- **WHEN** a member taps one
- **THEN** that receipt opens on the Kvitteringer tab

#### Scenario: Difference accepted
- **WHEN** a receipt's difference has been accepted
- **THEN** it is no longer listed, and the no-problems message shows if nothing else is
