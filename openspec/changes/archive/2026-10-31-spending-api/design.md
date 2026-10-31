# Design

## Context

See `proposal.md` for motivation and `specs/` for required behaviour.

Facts that shape the design:

- `Reconciliation` defines `grand_total = sum(line_totals) + pant_total`, so `GrandTotal` already
  includes pant, while category figures come from lines and don't. The two only agree after allowing for
  pant, and not at all on an unreconciled receipt.
- `PurchaseDate` is stored in UTC and has an index. `DanishTime.MonthRangeUtc` works from an instant.
- `Category` is a fixed two-level tree, and a line can sit on either level.
- Every stored `Receipt` is `Completed`, and failed mail lives in `ReceiptIntake`, so no status filter is
  needed.
- There are no integration tests, so logic that matters must be in pure helpers to be tested at all.
- A household produces tens of receipts a month. The widest request here covers 25 months.

## Goals / Non-Goals

**Goals:**

- One definition of each number, tested, so the phone only formats.
- Month boundaries that are right across daylight saving changes.

**Non-Goals:**

- Caching or stored rollups.
- Per-merchant or per-member spending breakdowns.
- Income, budgets or forecasts.
- Fixing lines that have no category. They're reported as uncategorized.

## Decisions

### Routes

New `InsightsEndpoints`, under `/api/insights`, mapped from `MapReceiptEndpoints`. All are reads.

| Route | Result |
|---|---|
| `GET /summary?month=` | `SpendingSummaryDto` |
| `GET /categories?month=&compareTo=&parentId=` | `CategoryBreakdownDto` |
| `GET /trend?endMonth=&months=` | `List<SpendingTrendMonthDto>` |
| `GET /prices?page=&pageSize=` | `PagedResult<PricedItemDto>` |
| `GET /prices/{itemId}?months=` | `PriceHistoryDto` |
| `GET /meal-coverage?endMonth=&months=` | `List<MealCoverageStatsDto>` |

Overview combines `summary`, `categories`, `meal-coverage`, the existing unassigned count, and
`GET /receipts?pageSize=1` for the latest shop. That's five small calls, and it's preferable to a
screen-shaped endpoint that couples the API to one layout.

### Compute in memory from narrow projections

Handlers load only the needed columns for the date range with a translated `Select`, then pure helpers
in `Features/Insights` do the grouping.

- `MonthKey` parses and formats `yyyy-MM`, and walks months.
- `SpendingRollup` gives the summary and the trend.
- `CategoryRollup` gives the breakdown and drill-down from lines and the category tree.
- `PriceHistory` gives the per-unit price series and its summary.
- `MealCoverageStats` gives the per-month figures.
- `Percent.Change(from, to)` is the one definition of a change, null when `from` is zero.

The alternative is `GROUP BY` in SQL, which is faster but needs Copenhagen time in the query for
bucketing across months, and it would put every rule beyond the reach of `Backend.Tests`. At this scale
the rows fit comfortably in memory, and testability is worth more than the milliseconds.

Lines are bucketed by their receipt's month, so a helper takes a Danish month rather than a raw date.
`DanishTime` gains `MonthRangeUtc(int year, int month)` and `MonthOf(DateTime utc)`, tested for both
sides of the daylight saving changes.

### Total paid versus category sums

`Total` is the sum of `GrandTotal`, because that's what left the account and it's the figure printed on
the receipt. Category rows sum `LineTotal`. The summary also returns `PantTotal` and `LinesTotal`, so
the app can compute category shares against `LinesTotal` and have them add up to 100 %. The mockup
divides by the grand total, so its bars can't quite reach 100 % when there's pant.

Pant counts as paid. It's refundable, but subtracting it would make the headline disagree with the
receipts.

Negative discount lines stay in the sums, because a discount really does reduce what a category cost. A
row's amount can therefore be negative, and the client has to cope with that.

### Current taxonomy for all months

Lines are grouped through today's tree. A snapshot per line would need a schema change and would stop
months being comparable after a reorganisation. The mockup does the same.

### Drill-down keeps the sum

The mockup lists only children, which drops lines placed directly on the parent, so its drill-down
doesn't add up to the row you tapped. Here the parent appears as a row of its own when it has direct
lines. Uncategorized is a separate field and is null on a drill-down.

### Price per unit

`sum(LineTotal) / sum(Quantity)` over the month, rather than averaging `UnitPrice`. Extraction reads line
totals most reliably, as the prompt's rules show, and weighting by quantity makes weighed goods and
multi-buys come out right. Lines with zero or negative quantity or total are excluded so that a discount
can't look like a price drop.

Purchase counts on `GET /prices` use the same filter, so an item is only listed if it has at least one
price to show. That query is a translated `GroupBy` with `Count`, since it needs no month logic.

Comparing a generic item across pack sizes is out of scope. If "Mælk" covers 1 L and 2 L, its per-unit
figure mixes them, because `Quantity` counts packs. Canonical items in the mockup name their size, which
is the intended way to avoid this.

### Meal statistics

These follow the mockup: all spend on the meal, divided by days covered. Spend on receipts with no
coverage is still in the numerator, so a household that fills in coverage only sometimes sees a high
figure. Dividing only covered spend would be more accurate per day, but it would show a spend figure that
doesn't match the lines carrying the meal. The screen shows both numbers side by side, so they need to
stay consistent. Covered-only can be added later without changing this shape.

### Search

`GET /api/receipts` gains `string? search`. `ReceiptSearch.TryParse` is a pure helper that trims the
term, enforces the length limit, escapes `\`, `%` and `_` for `ILIKE`, and recognises a number as an
amount range: `284,50` is `[284.50, 284.50]`, and `284` is `[284, 285)`.

The filter is a translated `Where` over the merchant, member, `ReceiptLines.Any(...)` on raw name and
canonical item name, plus the amount range. `Any` means a receipt with several matching lines is
returned once and keeps its other lines, and the existing `Include`s stay as they are.

`EF.Functions.ILike` is used rather than `Contains`, because `RawName` isn't `citext`. `ILIKE` on
non-ASCII letters depends on the database's `LC_CTYPE`. The stock `postgres` image initialises as
`en_US.utf8`, which handles them, but it couldn't be confirmed on a running stack while planning, so a
task checks `Ø` by hand. Search is a sequential scan over a household's receipts, so it needs no index.

### Validation

`months` is bounded by a shared `MonthKey.TryValidateRun`, as `Paging.TryValidate` does for paging, and
a bad month or range answers 400 through `Results.Problem`.

## Risks / Trade-offs

- [In-memory rollups don't scale] → Fine for a household. If it ever becomes slow, helpers can be fed
  from pre-aggregated rows without changing the API.
- [Top-level sums disagree with the headline] → Expected because of pant and unreconciled receipts, and
  the summary exposes `PantTotal` and `LinesTotal` so the app can explain it.
- [Extraction errors reach the numbers] → An unreconciled receipt's lines may be wrong. They're included
  because leaving them out would understate spending, and the Pipeline screen lists them for correction.
- [Whole-number amount search also matches names] → `1000` finds `REMA 1000`. That's useful rather than
  wrong, and it matches the mockup's behaviour of searching everything.
- [Pack sizes in price history] → Described above, and the note under the chart states what's measured.

## Migration Plan

No migration is needed, and every change is additive. To roll back, redeploy the previous image.
