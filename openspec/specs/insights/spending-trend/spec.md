# insights/spending-trend Specification

## Purpose
Shows how the household's spending moves from month to month, for the Insights trend chart and table.

## Requirements

### Requirement: Spending is reported across months
The system SHALL give any active member a run of consecutive months, oldest first, each with total
paid, receipt count and percentage change from the month before. The run SHALL end at a chosen month,
by default the current one, and SHALL cover 6 months by default.

#### Scenario: Default trend
- **WHEN** a member requests the trend with no parameters
- **THEN** they receive six months ending with the current Danish month

#### Scenario: Month with no receipts
- **WHEN** a month in the run has no receipts
- **THEN** it is still listed with a total and count of zero

#### Scenario: First month in the run
- **WHEN** the month before the run has spending
- **THEN** the first month's change is calculated from it

#### Scenario: Previous month had no spending
- **WHEN** the month before has a total of zero
- **THEN** the change is null

### Requirement: The trend's length is bounded
The number of months SHALL be between 1 and 24.

#### Scenario: Out of range
- **WHEN** the number of months is outside that range
- **THEN** the request is rejected with a 400 problem response

#### Scenario: Invalid end month
- **WHEN** the end month is not a valid `yyyy-MM` value
- **THEN** the request is rejected with a 400 problem response
