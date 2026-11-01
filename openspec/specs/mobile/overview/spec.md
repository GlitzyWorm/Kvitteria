# mobile/overview Specification

## Purpose

Gives the household its month at a glance and a direct route to the work waiting for it.

## Requirements

### Requirement: The profile bar greets the member
Overview SHALL show a time-of-day greeting with the member's name, the household's member count, and a
settings button.

#### Scenario: Morning
- **WHEN** a member opens Overview before 10:00
- **THEN** the greeting says good morning with their name

#### Scenario: Opening settings
- **WHEN** a member presses the settings button
- **THEN** Settings opens

### Requirement: The hero shows the month's spending
Overview SHALL show the selected month's total paid, the change from the previous month, and the receipt
count, opening on the current month. A control SHALL step to the previous month, and back again.

#### Scenario: Current month
- **WHEN** Overview opens
- **THEN** the hero shows the current month's total and change

#### Scenario: Stepping back
- **WHEN** a member steps to the previous month
- **THEN** the hero and category bars show that month

#### Scenario: No comparison
- **WHEN** the change is null
- **THEN** the change tag is hidden

### Requirement: Category bars show where money went
Overview SHALL show up to the six largest top-level categories with amount, share of the month's line total
and a bar, and SHALL show uncategorized spend as the last bar when it isn't zero. A link SHALL open Insights.

#### Scenario: Many categories
- **WHEN** the month has more than six categories
- **THEN** the six largest are shown

#### Scenario: Uncategorized spend
- **WHEN** a month has uncategorized lines
- **THEN** they appear in a neutral colour

#### Scenario: Negative amount
- **WHEN** a category's amount is negative
- **THEN** its bar is empty and the amount is shown as it is

### Requirement: Work cards lead to the next step
Overview SHALL show cards for unassigned lines, with a button opening Sort; uncategorized share and amount;
dinner cost per day from the first day-tracked meal; and the latest receipt, with a button opening it.

#### Scenario: Work waiting
- **WHEN** lines are unassigned
- **THEN** the card shows the count and opens the Sortér tab

#### Scenario: Latest receipt
- **WHEN** a member presses open on the latest receipt card
- **THEN** that receipt opens on the Kvitteringer tab

#### Scenario: No coverage
- **WHEN** the dinner cost per day is null
- **THEN** the card shows `—`

#### Scenario: No receipts yet
- **WHEN** the household has no receipts
- **THEN** the latest receipt card is hidden and the screen shows an empty state
