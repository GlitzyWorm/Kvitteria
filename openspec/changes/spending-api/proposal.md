# Proposal

## Why

The Overview and Insights screens are built on numbers the server does not produce: month totals,
category rollups, trends, price history and dinner cost per day. Computing them on the phone from
paged receipts would be slow and would give a second source of truth for what the household spent,
so the rollups belong on the server. The receipt list also needs the free-text search the mockup
shows.

## What Changes

- Add a monthly summary: total spend, receipt count, change against the previous month, uncategorized
  share, and the unassigned line count or a way to reach it.
- Add a category breakdown for a chosen month and comparison month, by top-level category with
  drill-down into children, including an uncategorized bucket.
- Add a multi-month spending trend.
- Add item price history per canonical item: price per month, current price, lowest price and change.
- Add meal-day statistics per month: spend on a meal, days covered and cost per day, for meals where
  `TracksDayCoverage` is true.
- Add search to `GET /api/receipts`, matching merchant, line text and amount.
- Remove nothing. The existing `ReceiptStatus` filter stays, but stored receipts are all `Completed`,
  so the app takes failed receipts from `pipeline-failure-tracking` instead.

## Capabilities

### New Capabilities

- `insights/spending-summary`: monthly totals and comparison with the previous month.
- `insights/category-breakdown`: category rollups, drill-down and month comparison.
- `insights/spending-trend`: spending across several months.
- `insights/price-history`: how a canonical item's price moves over time.
- `insights/meal-coverage-stats`: spend and cost per covered day for day-tracking meals.
- `receipts/search`: finding receipts by merchant, line text or amount.

### Modified Capabilities

None. No specs exist yet.

## Impact

- **Backend:** new read-only endpoints, with the calculations as pure helpers covered by
  `Backend.Tests` in the style of `Reconciliation` and `CategoryTree`.
- **Database:** no new tables expected. Indexes for month-range queries may be needed.
- **Contracts:** new DTOs. `GET /api/receipts` gains an optional query parameter, which is additive.
- **Authorization:** all reads, so ungated like the rest.
- **Behaviour to settle in design:** whether month boundaries follow Danish local time rather than
  UTC, since a late-night purchase must land in the right month; how pant counts towards totals;
  what happens to spend in a category that has since moved; and the unit a price is compared in,
  given weighed items with fractional quantities.
- **Depends on:** nothing. Failed receipts are never in totals, because they have no `Receipt` row
  under the proposed design for `pipeline-failure-tracking`.
