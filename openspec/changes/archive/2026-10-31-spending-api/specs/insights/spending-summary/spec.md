# Spec Delta

## Purpose

Tells the household what it spent in a month and how that compares with the month before, as the
figures behind the Overview screen.

## ADDED Requirements

### Requirement: A month's spending is summarized
The system SHALL give any active member, for a chosen month, the total paid (sum of receipt grand
totals, including pant), receipt count, pant total, sum of line totals, and the sum of line totals with
no category.

#### Scenario: Month with receipts
- **WHEN** a member requests the summary for a month with receipts
- **THEN** the response gives those figures for that month's receipts only

#### Scenario: Month with no receipts
- **WHEN** a member requests a month with no receipts
- **THEN** every figure is zero and the request succeeds

#### Scenario: Unreconciled receipt
- **WHEN** a month contains a receipt that did not reconcile
- **THEN** its grand total and its lines are included like any other receipt

#### Scenario: Failed mail
- **WHEN** a mail failed and has no receipt
- **THEN** it contributes nothing to the summary

### Requirement: Months follow Danish local time
A month SHALL be a `yyyy-MM` value meaning the calendar month in Danish local time. When omitted, it
SHALL be the current Danish month. Every insight that takes a month SHALL follow this rule.

#### Scenario: Month omitted
- **WHEN** a member requests the summary without a month
- **THEN** the response is for the current Danish month and names it

#### Scenario: Purchase just after midnight on the first
- **WHEN** a receipt was purchased at 00:30 Danish time on 1 August, which is still 31 July in UTC
- **THEN** it counts towards August

#### Scenario: Invalid month
- **WHEN** the month is not a valid `yyyy-MM` value
- **THEN** the request is rejected with a 400 problem response

### Requirement: The summary compares with the previous month
The summary SHALL include the previous calendar month's total paid and the percentage change from it.
The change SHALL be null when the previous total is zero.

#### Scenario: Spending rose
- **WHEN** the previous month's total was 1000 and this month's is 1100
- **THEN** the previous total is 1000 and the change is 10

#### Scenario: No previous spending
- **WHEN** the previous month has no receipts
- **THEN** the previous total is zero and the change is null
