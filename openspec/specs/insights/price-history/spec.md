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

#### Scenario: Raw names of an item
- **WHEN** a member requests the list
- **THEN** each entry also lists the raw names of the item's priced lines, each with its key, its label and
  its number of priced purchases, most purchased first, then by label

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
Danish month, with the price or null for months without a purchase, and the number of priced purchases in
each month. The run SHALL default to 6 months and SHALL be between 1 and 24.

#### Scenario: Gap in purchases
- **WHEN** an item was not bought in a month of the run
- **THEN** that month is listed with a null price and a purchase count of 0

#### Scenario: Purchases in a month
- **WHEN** an item was bought on three priced lines in a month and on one discount line
- **THEN** that month's purchase count is 3

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

### Requirement: A raw name is identified by its normalized form
Lines SHALL share a raw name when their printed names are equal after trimming and lower-casing independently
of the server's culture and the database's locale. That normalized form SHALL be the raw name's key, and its
label SHALL be the printed spelling most of those lines carry, ties broken by ordinal order.

#### Scenario: Different spelling of one name
- **WHEN** an item has lines printed `ARLA LETMÆLK 1L` twice and ` arla letmælk 1l` once
- **THEN** one raw name is listed, with key `arla letmælk 1l`, label `ARLA LETMÆLK 1L` and 3 purchases

#### Scenario: Danish letters
- **WHEN** lines are printed `SMØR` and `smør`
- **THEN** they share one raw name

#### Scenario: Excluded lines
- **WHEN** a raw name appears on the item only on discount or zero-quantity lines
- **THEN** it is not listed under the item

### Requirement: Price history can be narrowed to one raw name
Price history SHALL accept an optional raw-name key. With a key, only the item's lines with that raw name SHALL
count towards the months and the summary, and the response SHALL give the key and its label. Lines with that
raw name sorted to another item, or not sorted, SHALL NOT count.

#### Scenario: Narrowed history
- **WHEN** a member requests an item's history with the key of one of its raw names
- **THEN** the months, latest, lowest and change are computed from that raw name's lines alone

#### Scenario: No key
- **WHEN** a member requests an item's history without a key
- **THEN** every line of the item counts and the raw name is null

#### Scenario: Same name on another item
- **WHEN** lines with the same raw name are sorted to two items
- **THEN** each item's narrowed history counts only its own lines

#### Scenario: Unknown raw name
- **WHEN** the key matches no priced line of the item in the run
- **THEN** every month has a null price, the summary is null, the label is the key, and the request succeeds

#### Scenario: Key too long
- **WHEN** the key is longer than a line's name may be
- **THEN** the request is rejected with a 400 problem response
