# Tasks

Unless a task says otherwise, run tests from `Kvitteria` with
`dotnet test Backend.Tests/Backend.Tests.csproj`. Manual checks use `docker compose up -d --build`, with
a token from `scripts/token.sh`, against a database holding receipts in at least two months.

## 1. Month and percentage helpers

- [x] 1.1 Add `DanishTime.MonthRangeUtc(int year, int month)` and `DanishTime.MonthOf(DateTime utc)`, and verify `DanishTimeTests` cover a purchase at 00:30 on the first, the last instant of a month, and months either side of both daylight saving changes
- [x] 1.2 Add `MonthKey` with parsing, formatting, walking back and `TryValidateRun`, and verify `MonthKeyTests` cover valid values, `2026-13`, `2026-1`, garbage, a run across a year boundary, and run lengths of 0, 1, 24 and 25
- [x] 1.3 Add `Percent.Change`, and verify `PercentTests` cover a rise, a fall and a zero starting value

## 2. Summary and trend

- [x] 2.1 Add `SpendingRollup`, and verify `SpendingRollupTests` cover totals including pant, lines with no category, an empty month, a month in the run with no receipts, and the first month's change
- [x] 2.2 Add `SpendingSummaryDto` and `SpendingTrendMonthDto` to `Contracts`, and verify the backend and `MobileApp` still build
- [x] 2.3 Add `InsightsEndpoints` with `GET /summary`, mapped from `MapReceiptEndpoints`, with `.WithName()`, `.Produces` and `.ProducesProblem`, and verify by hand that a known month's figures match the sum of its receipts, that an omitted month names the current one, and the 400 on a bad month
- [x] 2.4 Add `GET /trend`, and verify by hand six months by default, zero months listed, and the 400 on 25 months

## 3. Category breakdown

- [x] 3.1 Add `CategoryRollup`, and verify `CategoryRollupTests` cover child lines rolling up, a line directly on a top-level category, a moved category, uncategorized lines, a comparison with a new and a vanished category, largest-first order, and that drill-down rows sum to the parent
- [x] 3.2 Add `CategoryBreakdownDto` to `Contracts`, and verify the backend builds
- [x] 3.3 Add `GET /categories`, and verify by hand that the top-level rows plus uncategorized equal `LinesTotal`, a comparison month, and 404 and 400 on an unknown and a child `parentId`

## 4. Prices and meal statistics

- [x] 4.1 Add `PriceHistory`, and verify `PriceHistoryTests` cover the weighed example from the spec, two merchants, excluded discount and zero-quantity lines, a gap, one priced month and none
- [x] 4.2 Add `PricedItemDto` and `PriceHistoryDto` to `Contracts`, and verify the backend builds
- [x] 4.3 Add `GET /prices` and `GET /prices/{itemId}`, and verify by hand the ordering, that an unbought item isn't listed, and the 404
- [x] 4.4 Add `MealCoverageStats`, and verify `MealCoverageStatsTests` cover spend with coverage, spend with none, a month with neither, and rounding
- [x] 4.5 Add `MealCoverageStatsDto` to `Contracts` and `GET /meal-coverage`, and verify by hand that only tracking meals appear and that a month's figures match its receipts

## 5. Receipt search

- [x] 5.1 Add `ReceiptSearch.TryParse`, and verify `ReceiptSearchTests` cover trimming, empty input, the length limit, escaping `\`, `%` and `_`, a comma and a point amount, and a whole number's range
- [x] 5.2 Add `search` to `GET /api/receipts`, and verify by hand a merchant, a member, a printed line, an item name, an exact amount, a whole-number amount, `%` matching literally, a date range combined with a search, and that a receipt with two matching lines is returned once with all its lines
- [x] 5.3 Verify by hand that searching `føtex` finds `FØTEX` on the running stack, and if it does not, handle the locale in `ReceiptSearch` and record why in `CLAUDE.md`

## 6. Documentation

- [x] 6.1 Update `CLAUDE.md`: add the rollup helpers to the pure logic lists, and record why rollups run in memory, why the total includes pant while categories don't, and that price is total over quantity, and verify the text matches the code
- [x] 6.2 Add the insights routes and receipt search to `README.md`, and verify the documented `curl` commands run as written

## Workflow follow-up

- `android-app` comes next and depends on this change.
- Archive this change after review.
