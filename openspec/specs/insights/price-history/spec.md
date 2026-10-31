# insights/price-history Specification

## Purpose
Shows how much a canonical item costs over time, across all merchants, so the household can see prices
moving.

## Requirements

### Requirement: Items with purchases can be listed
The system SHALL give any active member a paged list of canonical items that have priced purchases,
most purchased first, then by name.

#### Scenario: Listing items
- **WHEN** a member requests the list
- **THEN** each entry gives the item's ID, name and number of priced purchases

#### Scenario: Item never bought
- **WHEN** a canonical item has no priced purchases
- **THEN** it is not listed

#### Scenario: Invalid paging
- **WHEN** the page or page size is out of range
- **THEN** the request is rejected with a 400 problem response

### Requirement: An item's price is its average per unit
A month's price SHALL be the sum of the item's line totals divided by the sum of their quantities, across
all merchants, rounded to two decimals. Lines with a quantity or total of zero or less SHALL be excluded.

#### Scenario: Weighed item
- **WHEN** an item was bought as 1.24 for 19.72 and 0.76 for 12.08
- **THEN** that month's price is 15.90

#### Scenario: Several merchants
- **WHEN** an item was bought at two merchants in one month
- **THEN** the price averages both, weighted by quantity

#### Scenario: Discount line
- **WHEN** a linked line has a negative total
- **THEN** it is excluded from the price

### Requirement: Price history covers a run of months
For one item the system SHALL return a run of consecutive months, oldest first, ending at the current
Danish month, with the price or null for months without a purchase. The run SHALL default to 6 months
and SHALL be between 1 and 24.

#### Scenario: Gap in purchases
- **WHEN** an item was not bought in a month of the run
- **THEN** that month is listed with a null price

#### Scenario: Unknown item
- **WHEN** the item does not exist
- **THEN** the request is rejected with a 404 problem response

#### Scenario: Out of range
- **WHEN** the number of months is outside that range
- **THEN** the request is rejected with a 400 problem response

### Requirement: Price history is summarized
The response SHALL give the latest price, lowest price, and percentage change from the first to the
latest priced month in the run.

#### Scenario: Price rose
- **WHEN** the first priced month is 10.00 and the latest is 11.00
- **THEN** latest is 11.00, lowest is 10.00 and change is 10

#### Scenario: One priced month
- **WHEN** the run has one priced month
- **THEN** latest and lowest are that price and change is null

#### Scenario: No priced months
- **WHEN** the run has no priced months
- **THEN** latest, lowest and change are null and the request succeeds
