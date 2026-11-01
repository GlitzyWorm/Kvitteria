# mobile/sorting Specification

## Purpose

Makes working through unassigned receipt lines fast on a phone, one line at a time, in as few taps as
possible.

## Requirements

### Requirement: Lines are presented one at a time
Sort SHALL show the first unassigned line with its raw name in a monospaced face, merchant, date, member,
total and quantity times unit price, plus progress and the next few lines in the queue.

#### Scenario: Line shown
- **WHEN** Sort opens with unassigned lines
- **THEN** the first line's details, progress and up-next list are shown

#### Scenario: Queue empty
- **WHEN** no lines are unassigned
- **THEN** the empty state appears with a button to the receipts

### Requirement: Suggestions are ranked locally
The app SHALL fetch the canonical item list and suggest up to four items by matching words of three or more
letters in the raw name against item names: an exact word scores its length plus 4, and shared first four
letters on words of four or more letters score 3. The top item SHALL be marked when it scores 7 or more.

#### Scenario: Confident match
- **WHEN** `HAKKET OKSEKØD 8-12%` is shown and `Hakket oksekød 8-12%` exists
- **THEN** that item is first and marked

#### Scenario: Danish letters
- **WHEN** a word contains `Æ`, `Ø` or `Å`
- **THEN** it is matched as one word

#### Scenario: No match
- **WHEN** no item scores above zero
- **THEN** the app says so and offers search and create

### Requirement: Items can be searched
A search field SHALL match item names from two characters, ignoring case, and show up to eight results.

#### Scenario: Searching
- **WHEN** a member types `ost`
- **THEN** matching items are listed and can be picked

### Requirement: Picking an item prepares the save
Picking an item SHALL fill category and meal from its defaults, turn remember-alias on and write-defaults
off. Creating an item SHALL prefill a tidied name from the raw line and turn both on.

#### Scenario: Existing item
- **WHEN** a member picks an item with defaults
- **THEN** its category and meal are selected and can still be changed

#### Scenario: New item
- **WHEN** a member chooses create for `RØDVINSEDDIKE 500ML`
- **THEN** the name field starts as `Rødvinseddike 500ml` and can be edited

#### Scenario: No taxonomy permission
- **WHEN** a member lacks `ManageTaxonomy`
- **THEN** create and write-defaults are disabled with a stated reason

### Requirement: Category can be created in place
With `ManageTaxonomy`, the category picker SHALL offer a new category and select it once created.

#### Scenario: New category
- **WHEN** a member creates a category from the picker
- **THEN** it exists, is selected, and the draft is otherwise unchanged

### Requirement: A save sends one request
Save SHALL send the item, category, meal, alias choice and defaults choice as one atomic request and advance
to the next line, showing how many other lines were assigned.

#### Scenario: Alias teaches other lines
- **WHEN** a save reports other lines assigned
- **THEN** a message says how many, and those lines leave the queue

#### Scenario: Line already sorted
- **WHEN** a save answers 409 because the line has an item
- **THEN** the queue reloads and the next line is shown without losing the member's place

#### Scenario: Name taken
- **WHEN** creating an item answers 409 for its name
- **THEN** the server's message is shown and the draft is kept

#### Scenario: Offline
- **WHEN** a save can't reach the server
- **THEN** the draft is kept and the failure is shown

### Requirement: Lines can be skipped
Skip SHALL move past a line for the rest of the app session without changing anything on the server.

#### Scenario: Skipping
- **WHEN** a member skips a line
- **THEN** the next line is shown, and the skipped line returns after the app restarts

#### Scenario: Everything skipped
- **WHEN** every remaining line has been skipped
- **THEN** the app says so and offers to show them again

### Requirement: Sorting needs permission
Without `CategorizeLines`, Sort SHALL show the queue read-only and say why.

#### Scenario: Member without the permission
- **WHEN** a member without `CategorizeLines` opens Sort
- **THEN** no save control is available
