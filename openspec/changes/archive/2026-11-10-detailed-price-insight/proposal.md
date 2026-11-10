# Proposal

## Why

Priser can only answer "what does this generic item cost". A canonical item such as "Mælk" mixes every
product sorted to it, so pack sizes and brands blur into one price. The chart has no Y axis, and its scale
runs from the lowest price to the highest, so a 45-øre change fills the whole height and reads like a jump.
No point can be read off exactly, and the item list is a flat sheet with no search, which stops working once
raw names make it longer.

## What Changes

- The item list also gives, for each canonical item, the raw names (`ReceiptLine.RawName`, normalized) of its
  priced lines, each with its most common printed spelling and its number of priced purchases. The list stays
  paged and the field is additive.
- Price history takes an optional `name`, which narrows the item's lines to those with that normalized raw
  name. Only lines sorted to the item count; unsorted lines are not priced by name. Without `name` the
  response is unchanged.
- Each month of a price history also gives its number of priced purchases. This is additive.
- The app's item sheet groups raw names under their item, hides the raw-name row when an item has only one,
  and gets a search field that matches either level. It loads every page of the list, no longer only the
  first 100.
- The price chart gets a labelled Y axis with rounded ticks and gridlines. Tapping a point selects it and shows
  that month, its price and its number of purchases.
- The heading and figures name the selection: the item, or the item and the raw name.

Nothing is **BREAKING**: an older app ignores the new fields and never sends `name`.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `insights/price-history`: the list carries raw names, history can be narrowed to one raw name of the item,
  and each month carries its purchase count.
- `mobile/insights`: Priser offers raw names under items, a searchable item sheet, a labelled Y axis and
  selectable points.

## Impact

- Backend: `InsightsEndpoints` (`GET /insights/prices`, `GET /insights/prices/{itemId}`),
  `Features/Insights/PriceHistory.cs` and a new pure helper for grouping raw names, `PricedItemDto`,
  `PriceMonthDto`, and new tests in `Backend.Tests`.
- Contract: `docs/openapi/kvitteria.json` regenerated. `ContractTest` gains the new raw-name DTO.
- Android: `core:domain` insights models and repository, `core:data` `KtorInsightsRepository` and its DTOs,
  `feature:insights:presentation` (`InsightsViewModel`, `InsightsScreen`, `Charts.kt`) and its strings in both
  languages.
- No migration, and no new dependency.
