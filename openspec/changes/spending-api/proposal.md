# Proposal

## Why

The Overview and Insights screens are built on numbers the server does not produce: month totals,
category rollups, trends, price history and dinner cost per day. Computing them on the phone from
paged receipts would be slow and would give a second source of truth for what the household spent,
so the rollups belong on the server. The receipt list also needs the free-text search the mockup
shows.

## What Changes

- Add a monthly summary: total paid, receipt count, change against the previous month, pant, and the
  amount with no category.
- Add a category breakdown for a chosen month and an optional comparison month, by top-level category
  with drill-down into children, including an uncategorized bucket.
- Add a multi-month spending trend.
- Add item price history: which items have purchases, and for one item its price per month, current
  price, lowest price and change.
- Add meal-day statistics per month: spend on a meal, days covered and cost per day, for meals where
  `TracksDayCoverage` is true.
- Add search to `GET /api/receipts`, matching merchant, member, printed line name, item name and amount.
- Months follow Danish local time everywhere, using `DanishTime`.
- The queue count is not repeated here. `GET /receipt-lines/unassigned/count` from `sorting-api` already
  provides it.

## Capabilities

### New Capabilities

- `insights/spending-summary`: monthly totals and comparison with the previous month.
- `insights/category-breakdown`: category rollups, drill-down and month comparison.
- `insights/spending-trend`: spending across several months.
- `insights/price-history`: how a canonical item's price moves over time.
- `insights/meal-coverage-stats`: spend and cost per covered day for day-tracking meals.
- `receipts/search`: finding receipts by merchant, member, line, item or amount.

### Modified Capabilities

None. Search is an additive parameter, and the existing receipt list behaviour is unchanged without it.

## Impact

- **Backend:** new read-only `InsightsEndpoints`, with the calculations as pure helpers covered by
  `Backend.Tests` in the style of `Reconciliation` and `CategoryTree`. `DanishTime` gains a month
  range by year and month.
- **Database:** no schema change. The existing `PurchaseDate` index serves the month ranges.
- **Contracts:** new DTOs. `GET /api/receipts` gains an optional query parameter, which is additive.
- **Authorization:** all reads, so ungated like the rest.
- **Accuracy:** totals are only as good as the extracted data. Lines linked before `sorting-api` keep
  an empty category and count as uncategorized.
- **Depends on:** `pipeline-failure-tracking` for `DanishTime` and the receipt's member, and
  `sorting-api` for item defaults reaching lines. Both are applied.
