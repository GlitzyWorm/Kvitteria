# Spec Delta

## Purpose

Lets the household work through receipt lines that have no canonical item, saving each line's item,
category and meal in one step.

## ADDED Requirements

### Requirement: Unassigned lines can be listed
The system SHALL provide any active member a paged list of receipt lines with no canonical item,
newest purchase first, with lines from one receipt kept together. Each entry SHALL carry the line's
raw name, quantity, unit price and total, and its receipt's ID, merchant, purchase date and member.

#### Scenario: Listing the queue
- **WHEN** an active member requests the queue
- **THEN** they receive only lines with no canonical item, with those fields, in that order

#### Scenario: Member unknown
- **WHEN** a line's receipt has no known member
- **THEN** the entry's member is null and the line is still listed

#### Scenario: Empty queue
- **WHEN** every line has a canonical item
- **THEN** the list is empty and the request succeeds

#### Scenario: Invalid paging
- **WHEN** the page or page size is out of range
- **THEN** the request is rejected with a 400 problem response

### Requirement: Unassigned lines can be counted
The system SHALL report to any active member how many lines have no canonical item, without returning
the lines.

#### Scenario: Counting the queue
- **WHEN** an active member requests the count
- **THEN** the response gives the same number as the queue's total

### Requirement: A line's assignment is saved atomically
The system SHALL let a member with `CategorizeLines` save a line's canonical item, category and meal,
alias choice and defaults choice in one request. Either every part applies or none does.

#### Scenario: Saving a line
- **WHEN** a member saves a line with an existing item, a category and a meal
- **THEN** the line has all three and the response returns the updated line

#### Scenario: Clearing category or meal
- **WHEN** a save leaves category or meal empty
- **THEN** that field is empty on the line

#### Scenario: Part of the save fails
- **WHEN** any part of a save fails
- **THEN** the line, item and aliases are all unchanged

#### Scenario: Unknown line, item, category or meal
- **WHEN** a save names a line, item, category or meal that does not exist
- **THEN** the request is rejected with a 404 problem response and nothing changes

#### Scenario: Missing permission
- **WHEN** a member without `CategorizeLines` saves a line
- **THEN** the request is rejected with a 403 response

### Requirement: A save names exactly one item
A save SHALL name either an existing canonical item or a new item's name, not both and not neither.

#### Scenario: Neither given
- **WHEN** a save names no item
- **THEN** the request is rejected with a 400 problem response

#### Scenario: Both given
- **WHEN** a save names an existing item and a new item's name
- **THEN** the request is rejected with a 400 problem response

### Requirement: A save can create the item
A save with a new item's name SHALL create that canonical item in the same transaction, and SHALL
require `ManageTaxonomy` as well as `CategorizeLines`.

#### Scenario: Creating an item
- **WHEN** a member with both permissions saves a line with a new item's name
- **THEN** the item exists and the line is assigned to it

#### Scenario: Name already taken
- **WHEN** a new item's name matches an existing item, ignoring case
- **THEN** the request is rejected with a 409 problem response naming it, and nothing changes

#### Scenario: Invalid name
- **WHEN** the new name is blank or longer than 200 characters
- **THEN** the request is rejected with a 400 problem response

#### Scenario: Missing permission
- **WHEN** a member without `ManageTaxonomy` saves a line with a new item's name
- **THEN** the request is rejected with a 403 problem response explaining why, and nothing changes

### Requirement: A save can write the item's defaults
When asked, a save SHALL write the saved category and meal onto the canonical item as its defaults, and
SHALL require `ManageTaxonomy` to do so.

#### Scenario: Writing defaults
- **WHEN** a member with `ManageTaxonomy` saves a line with defaults requested
- **THEN** the item's default category and meal equal the saved values, including empty ones

#### Scenario: Defaults not requested
- **WHEN** a save does not request defaults
- **THEN** the item's defaults are unchanged

#### Scenario: Missing permission
- **WHEN** a member without `ManageTaxonomy` requests defaults
- **THEN** the request is rejected with a 403 problem response explaining why, and nothing changes

### Requirement: A save only applies to unassigned lines
A save SHALL be rejected when the line already has a canonical item, so that concurrent sorting never
overwrites an earlier choice. Lines that already have an item are changed through the existing per-line
operations.

#### Scenario: Line sorted by someone else
- **WHEN** a save targets a line that gained an item after the client loaded it
- **THEN** the request is rejected with a 409 problem response and nothing changes

#### Scenario: Line assigned by alias learning
- **WHEN** an earlier save assigned this line through its alias, and a client saves it from a stale
  queue
- **THEN** the request is rejected with a 409 problem response and nothing changes

### Requirement: Saving keeps day coverage valid
When a save removes the last line on a receipt carrying a meal, the system SHALL remove that receipt's
coverage for that meal, in the same transaction.

#### Scenario: Last line of a meal changes
- **WHEN** a save changes the meal on the only line carrying a covered meal
- **THEN** the receipt's coverage for that meal is removed

#### Scenario: Other lines keep the meal
- **WHEN** another line on the receipt still carries the meal
- **THEN** the coverage is kept

### Requirement: A save reports its effect
The save response SHALL include how many other lines it assigned and how many lines remain unassigned.

#### Scenario: Reporting the effect
- **WHEN** a save assigns two other lines and five unassigned lines remain
- **THEN** the response reports two and five
