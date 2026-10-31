# Spec Delta

## Purpose

Makes one sorting decision teach the system: a remembered raw name resolves matching lines that are
already waiting, and every future receipt line with that name, including its category and meal.

## ADDED Requirements

### Requirement: A save can remember an alias
When asked, a save SHALL make the line's normalized raw name resolve to the saved canonical item,
replacing any alias that name already had. This SHALL need no permission beyond `CategorizeLines`.

#### Scenario: New alias
- **WHEN** a save with remember requested targets a raw name with no alias
- **THEN** an alias from that name to the item exists

#### Scenario: Alias already exists
- **WHEN** a save with remember requested targets a raw name whose alias points at another item
- **THEN** the alias points at the saved item

#### Scenario: Not requested
- **WHEN** a save does not request remembering
- **THEN** no alias is created or changed, and no other line is touched

### Requirement: Remembering resolves waiting lines
A save that remembers an alias SHALL assign the saved item to every other line with no canonical item
whose normalized raw name matches, in the same transaction.

#### Scenario: Same name on other receipts
- **WHEN** a save remembers an alias and two other unassigned lines print the same name
- **THEN** both are assigned the item

#### Scenario: Case and surrounding whitespace differ
- **WHEN** another unassigned line differs only by case or surrounding whitespace, including in the
  letters Æ, Ø and Å
- **THEN** it is assigned the item

#### Scenario: Line already has an item
- **WHEN** a matching line already has a different canonical item
- **THEN** that line is unchanged

### Requirement: Resolved lines keep existing choices
A line assigned by remembering SHALL take the saved category and meal only for fields that are empty.

#### Scenario: Empty line
- **WHEN** a matching line has no category and no meal
- **THEN** it takes the saved category and meal

#### Scenario: Category chosen by hand
- **WHEN** a matching line already has a category
- **THEN** its category is kept and only an empty meal is filled

#### Scenario: Save leaves a field empty
- **WHEN** the save has no meal
- **THEN** matching lines keep their current meal

### Requirement: New lines take the item's defaults
When ingestion links a new line to a canonical item through an alias, the line SHALL take the item's
default category and meal.

#### Scenario: Item has defaults
- **WHEN** a new receipt has a line matching an alias whose item has a default category and meal
- **THEN** the stored line has the item, category and meal

#### Scenario: Item has no defaults
- **WHEN** the item has no default category or meal
- **THEN** the stored line has the item and those fields are empty

#### Scenario: No alias
- **WHEN** a new line matches no alias
- **THEN** it has no item, category or meal and appears in the unassigned queue
