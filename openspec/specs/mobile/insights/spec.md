# mobile/insights Specification

## Purpose

Shows how the household's spending breaks down and changes over time, in four views.

## Requirements

### Requirement: Insights has four views
The Indsigt tab SHALL offer Kategorier, Udvikling, Priser and Aftensmad as a segmented control that keeps each
view's selections while the app is open.

#### Scenario: Switching views
- **WHEN** a member picks a month in Kategorier, switches view and returns
- **THEN** the same month is selected

### Requirement: Categories can be compared and drilled into
Kategorier SHALL show rows for a chosen month and comparison month, defaulting to the current and previous
months, sortable by size or change. Tapping a top-level row SHALL show its children, with a way back.

#### Scenario: Comparing
- **WHEN** both months are chosen
- **THEN** each row shows amount, a bar for each month and the change

#### Scenario: No comparison
- **WHEN** the comparison is set to none
- **THEN** comparison values are hidden and sorting by change is unavailable

#### Scenario: Drilling down
- **WHEN** a member taps a category
- **THEN** its children are shown and uncategorized is not

#### Scenario: Movers
- **WHEN** at least two rows changed by 1 % or more
- **THEN** a note names the biggest rise and the biggest fall

#### Scenario: Null change
- **WHEN** a row's change is null
- **THEN** it shows `—`

### Requirement: Categories can be managed from Insights
With `ManageTaxonomy`, Kategorier SHALL offer creating a category, and renaming or changing the parent of an
existing one. A category with children SHALL NOT be moved under another.

#### Scenario: Renaming
- **WHEN** a member saves a new name
- **THEN** the rows refresh with it

#### Scenario: Name taken
- **WHEN** the server answers 409
- **THEN** its message is shown and the dialog stays open

#### Scenario: Category with children
- **WHEN** the category has children
- **THEN** the parent field is locked with a stated reason

### Requirement: The trend shows months side by side
Udvikling SHALL show six months as bars, with the selected month highlighted, and a table of month, total,
change and receipt count.

#### Scenario: Opening the trend
- **WHEN** a member opens Udvikling
- **THEN** six months ending with the current one are shown

#### Scenario: Selecting a month
- **WHEN** a member taps a bar
- **THEN** that month is highlighted

### Requirement: Prices show an item's history
Priser SHALL offer a choice of items with purchases and show latest price, change, lowest price and a six-month
line chart, with gaps where a month has no price, and a note saying what is measured.

#### Scenario: Choosing an item
- **WHEN** a member picks an item
- **THEN** its figures and chart are shown

#### Scenario: Gap
- **WHEN** a month has no price
- **THEN** the line breaks there rather than dropping to zero

#### Scenario: No priced items
- **WHEN** no item has purchases
- **THEN** an empty state is shown

#### Scenario: Choosing a raw name
- **WHEN** a member picks a raw name under an item
- **THEN** the figures and chart cover that raw name's lines alone, and the heading names the item and the raw
  name

### Requirement: Aftensmad shows cost per day
Aftensmad SHALL show the first day-tracked meal's current cost per day, days covered and spend, and a table of
its months. If several meals track days, a member SHALL be able to choose among them.

#### Scenario: Current month
- **WHEN** a member opens Aftensmad
- **THEN** the hero shows the current month's figures

#### Scenario: Null cost per day
- **WHEN** no days are covered
- **THEN** the hero shows `—`

#### Scenario: No tracking meal
- **WHEN** no meal tracks days
- **THEN** an empty state is shown

### Requirement: The item choice offers raw names under their item
The item sheet SHALL list every item with priced purchases, including beyond the first page the server returns,
each followed by its raw names, indented. An item with only one raw name SHALL be listed without it.

#### Scenario: Item with several raw names
- **WHEN** an item has priced lines under two raw names
- **THEN** the sheet shows the item, then both raw names indented beneath it, each with its own row

#### Scenario: Item with one raw name
- **WHEN** an item's priced lines all share one raw name
- **THEN** the sheet shows the item alone

#### Scenario: More than one page
- **WHEN** the household has more priced items than fit on one page
- **THEN** every item is offered

### Requirement: The item choice can be searched
The item sheet SHALL have a search field that keeps the items and raw names containing the text, ignoring case
in a way that keeps Æ, Ø and Å distinct. A matching raw name SHALL keep its item visible, and a matching item
SHALL keep all its raw names.

#### Scenario: Matching a raw name
- **WHEN** a member types `arla`
- **THEN** `Mælk` is shown with `ARLA LETMÆLK 1L` beneath it, and its other raw names are hidden

#### Scenario: Matching an item
- **WHEN** a member types `mælk`
- **THEN** `Mælk` is shown with all of its raw names

#### Scenario: Nothing matches
- **WHEN** the text matches nothing
- **THEN** the sheet says so

### Requirement: The price chart has a labelled Y axis
The price chart SHALL label its Y axis in kroner with rounded ticks and a gridline at each, spanning every
price in the run. With one priced month, or prices that are all equal, the axis SHALL span a range around that
price rather than collapse.

#### Scenario: Several prices
- **WHEN** the run's prices lie between 14,45 and 14,95
- **THEN** the axis is labelled at rounded values that include both

#### Scenario: One price
- **WHEN** only one month is priced
- **THEN** the point sits inside a labelled range around its price

### Requirement: A price point can be selected
Tapping the chart SHALL select the nearest priced month and show its month, price and number of purchases
beside the point. Tapping the selected point again, or choosing another item, SHALL clear the selection. Each
priced point SHALL be announced to accessibility services with its month and price.

#### Scenario: Tapping a point
- **WHEN** a member taps near the July point
- **THEN** a callout shows July, its price and its number of purchases

#### Scenario: Tapping a gap
- **WHEN** a member taps a month with no price
- **THEN** the nearest priced month is selected

#### Scenario: Changing item
- **WHEN** a member picks another item
- **THEN** no point is selected
