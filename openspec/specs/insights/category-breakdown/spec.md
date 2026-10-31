# insights/category-breakdown Specification

## Purpose
Shows where a month's money went by category, how that compares with another month, and what sits
inside each top-level category.

## Requirements

### Requirement: Spending is broken down by top-level category
The system SHALL give any active member a chosen month's line totals grouped by top-level category,
largest first. A line on a child category SHALL count towards its parent. Only categories with spend in
the month or the comparison month SHALL be listed.

#### Scenario: Lines on child categories
- **WHEN** a month has lines on two children of one top-level category
- **THEN** that category's amount is their sum

#### Scenario: Line directly on a top-level category
- **WHEN** a line is assigned to a top-level category
- **THEN** it counts towards that category

#### Scenario: Month with no lines
- **WHEN** the month has no receipt lines
- **THEN** the list is empty and the request succeeds

### Requirement: The current taxonomy decides the grouping
Grouping SHALL use each line's current category and that category's current parent, for every month.

#### Scenario: Category moved to another parent
- **WHEN** a category moved to a different parent after its lines were bought
- **THEN** those lines count towards the new parent in every month

### Requirement: Uncategorized spend is reported separately
At the top level, the response SHALL report line totals with no category separately from the category
list.

#### Scenario: Lines with no category
- **WHEN** a month has lines with no category
- **THEN** their sum is the uncategorized amount

#### Scenario: Everything is categorized
- **WHEN** every line has a category
- **THEN** the uncategorized amount is zero

### Requirement: A breakdown can be compared with another month
When given a comparison month, each row SHALL include that month's amount and the percentage change.
The change SHALL be null when the comparison amount is zero. Without one, both SHALL be null.

#### Scenario: Comparing months
- **WHEN** a member requests a breakdown with a comparison month
- **THEN** each row gives both amounts and the change, and the uncategorized amount is compared too

#### Scenario: Category new this month
- **WHEN** a category had no spend in the comparison month
- **THEN** its comparison amount is zero and its change is null

#### Scenario: Category gone this month
- **WHEN** a category had spend only in the comparison month
- **THEN** it is listed with an amount of zero

#### Scenario: No comparison month
- **WHEN** no comparison month is given
- **THEN** every comparison amount and change is null

### Requirement: A top-level category can be drilled into
Given a top-level category, the breakdown SHALL list its child categories instead, and lines placed
directly on the parent SHALL appear as their own row, so the rows sum to the parent's amount.

#### Scenario: Drilling down
- **WHEN** a member requests a breakdown for a top-level category
- **THEN** the rows are its children with spend, and there is no uncategorized amount

#### Scenario: Lines directly on the parent
- **WHEN** some lines sit directly on the parent
- **THEN** the parent appears as a row with their sum

#### Scenario: Unknown category
- **WHEN** the category does not exist
- **THEN** the request is rejected with a 404 problem response

#### Scenario: Not a top-level category
- **WHEN** the category is itself a child
- **THEN** the request is rejected with a 400 problem response
