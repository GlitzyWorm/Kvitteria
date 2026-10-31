# insights/meal-coverage-stats Specification

## Purpose
Shows what a day-tracked meal such as dinner costs per covered day, month by month.

## Requirements

### Requirement: Day-tracked meals are reported by month
The system SHALL give any active member, for every meal that tracks day coverage, a run of consecutive
months, oldest first, ending at a chosen month, by default the current one. The run SHALL default to 6
months and SHALL be between 1 and 24.

#### Scenario: Meals that track days
- **WHEN** a member requests the statistics
- **THEN** only meals that track day coverage are returned, in their sort order

#### Scenario: Month with no activity
- **WHEN** a month has no lines or coverage for the meal
- **THEN** it is listed with zero spend, zero days and a null cost per day

#### Scenario: No meal tracks days
- **WHEN** no meal tracks day coverage
- **THEN** the list is empty and the request succeeds

#### Scenario: Out of range
- **WHEN** the number of months is outside that range, or the end month is invalid
- **THEN** the request is rejected with a 400 problem response

### Requirement: Cost per day divides spend by covered days
Each month SHALL give the sum of line totals carrying the meal, the sum of days covered on that month's
receipts, and spend divided by days rounded to two decimals, or null when no days are covered.

#### Scenario: Spend and coverage
- **WHEN** a month has 900.00 of dinner lines and 12 days covered
- **THEN** spend is 900.00, days are 12 and the cost per day is 75.00

#### Scenario: Spend with no coverage
- **WHEN** a month has lines carrying the meal but no days covered
- **THEN** the spend is reported and the cost per day is null

#### Scenario: Receipt with lines but no coverage
- **WHEN** one receipt carries the meal with no coverage while another has coverage
- **THEN** the spend includes both receipts and the days come only from the covered one
