# receipts/duplicate-detection Specification

## Purpose
Points out a receipt that probably records a purchase already stored, such as a photographed slip and an
app screenshot of the same shop, without ever discarding data on a guess.

## Requirements

### Requirement: A likely duplicate is flagged when stored
When a receipt from any source is stored, the system SHALL flag it as a possible duplicate of an existing
receipt with the same merchant, the same purchase day in Danish local time and the same grand total. The
receipt SHALL still be stored.

#### Scenario: Screenshot and paper slip of one purchase
- **WHEN** a receipt is stored for SuperBrugsen on 31 October at 108.95 and one with those values already exists
- **THEN** the new receipt is stored and carries the existing receipt's ID as its possible duplicate

#### Scenario: Same shop, different total
- **WHEN** a receipt matches an existing one on merchant and day but not on total
- **THEN** it is not flagged

#### Scenario: Several matches
- **WHEN** more than one stored receipt matches
- **THEN** the flag names the earliest stored of them

#### Scenario: Day boundary
- **WHEN** two receipts with equal merchant and total were bought at 23:50 and 00:10 Danish time
- **THEN** neither is flagged against the other

### Requirement: A duplicate flag does not outlive its match
Deleting the receipt a flag points to SHALL clear the flag on every receipt that pointed to it.

#### Scenario: Original deleted
- **WHEN** the receipt another receipt is flagged against is deleted
- **THEN** the remaining receipt is no longer flagged
