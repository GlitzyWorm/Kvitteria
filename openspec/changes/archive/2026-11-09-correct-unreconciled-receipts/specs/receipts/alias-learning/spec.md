# Spec Delta

## MODIFIED Requirements

### Requirement: New lines take the item's defaults
When ingestion, or a member adding a line to a stored receipt, links a new line to a canonical item through an
alias, the line SHALL take the item's default category and meal.

#### Scenario: Item has defaults
- **WHEN** a new receipt has a line matching an alias whose item has a default category and meal
- **THEN** the stored line has the item, category and meal

#### Scenario: Item has no defaults
- **WHEN** the item has no default category or meal
- **THEN** the stored line has the item and those fields are empty

#### Scenario: No alias
- **WHEN** a new line matches no alias
- **THEN** it has no item, category or meal and appears in the unassigned queue

#### Scenario: Line added by hand
- **WHEN** a member adds a line to a stored receipt whose name matches an alias, ignoring case and surrounding
  whitespace
- **THEN** the added line has the item and its default category and meal

## ADDED Requirements

### Requirement: Renaming an unassigned line links it
When a line with no canonical item is renamed, and the new name matches an alias, the line SHALL take that item
and fill only its empty category and meal from the item's defaults. A line that already has an item SHALL keep it.

#### Scenario: Garbled name corrected
- **WHEN** a member renames an unassigned line to a name the household has an alias for
- **THEN** the line has that item and leaves the unassigned queue

#### Scenario: Line already has an item
- **WHEN** a line that has an item is renamed to a name with an alias for another item
- **THEN** it keeps its item

#### Scenario: Category chosen by hand
- **WHEN** an unassigned line with a category is renamed to a name with an alias
- **THEN** it takes the item, keeps its category and fills only an empty meal

#### Scenario: No alias for the new name
- **WHEN** the new name matches no alias
- **THEN** the line stays unassigned
