# Design

## Context

See proposal.md for the motivation. Today `GET /insights/prices` pages canonical items by their priced-line
count in SQL. `GET /insights/prices/{itemId}` loads the item's lines in the run and hands them to
`PriceHistory.Build`, a pure helper covered by `PriceHistoryTests`. The app fetches one page of 100 into a flat
`PickerSheet` and draws `PriceChart` on a bare `Canvas` with cleared semantics and no input.

Three constraints shape the approach:

- **Raw names must be matched in memory.** `lower(btrim(...))` in SQL depends on the database locale for Æ, Ø
  and Å and could disagree with `NormalizeString`. This is the same reason the unassigned-lines queue matches
  in memory.
- **The contract has to stay additive.** An app and a server of different versions meet after every update,
  and `ContractTest` and `test.yml` hold the document honest.
- **The screen must hold only state.** Each screen is MVI, with `Screen` holding only state.

## Goals / Non-Goals

**Goals:**
- Reuse `PriceHistory.Build` unchanged in its price rule, so a raw-name price is computed exactly like an item
  price.
- Keep the new rules (grouping raw names, axis ticks, search filtering, nearest priced month) in pure functions
  with tests on both sides.

**Non-Goals:**
- Pricing raw names of unsorted lines, or across items. The user chose to scope raw names to their item.
- An adjustable run length. The API already accepts 1-24 months, and the app stays at six.
- Navigating from a point to the receipts behind it.
- A chart library. Charts stay plain `Canvas`.

## Decisions

### The list stays paged and gains a `Names` field

`PricedItemDto` gets `List<PricedNameDto> Names`, and `PricedNameDto(string Key, string Label, int
PurchaseCount)` is added. The handler pages items in SQL as today. It then loads `(CanonicalItemId, RawName)`
for the priced lines of that page's item IDs only, and groups them in memory with a new pure helper,
`RawNames.Group` in `Features/Insights`. That helper keys by `NormalizeString`, labels with the most frequent
spelling (ties broken by `string.CompareOrdinal`), and orders by count and then label.

*Alternative:* return the whole tree unpaged. That is simpler for the app but changes the shape of an existing
response, which an older app would fail to read.

### History narrows by an optional `name` query parameter

`GET /insights/prices/{itemId}?months=&name=` validates `name` against `KvitteriaDbContext.RawNameMaxLength`
(400 when longer). It loads the run's priced lines as today, now also projecting `RawName`. When `name` is
given, it keeps only lines whose `NormalizeString()` equals `name.NormalizeString()`, then calls
`PriceHistory.Build`. The label comes from a second narrow query of the item's priced `RawName`s across all
time, passed through `RawNames.Group`, so the heading always matches the label in the sheet. It falls back to
the key when nothing matches.

`PriceHistoryDto` gains `string? RawNameKey` and `string? RawNameLabel`, both null without `name`. Echoing the
key back also guards against an older server: it would ignore `name` and answer with the whole item, and the
app can tell because the key comes back null.

*Alternative:* a path segment per raw name (`/prices/{itemId}/names/{key}`). Keys contain spaces and Danish
letters, which a path makes awkward to encode, and it would be a second route repeating the first.

### Months carry a purchase count

`PriceMonthDto` gains `int PurchaseCount`, the number of priced lines in the month. `Build` already groups by
month, so it counts in the same pass and returns 0 for an empty month. The callout shows this count because a
price built from one purchase deserves less trust than one built from eight.

### The app loads every page

`InsightsRepository.pricedItems` stays paged. The ViewModel's loader fetches pages of 100 until it has
`totalCount` items, so search covers everything. A household's item count makes this one or two requests.

### Selection is a pair, and survives a refresh only when it still exists

`InsightsSelections.priceItemId` becomes a `PriceSelection(itemId, nameKey: String?)`. After a reload, a
`nameKey` that is no longer under its item falls back to the item, and an item that is gone falls back to the
first item, as happens today. The selected chart month, `priceMonth: YearMonth?`, also lives in selections, so
the ViewModel owns it and tests can cover it. Picking another item or name clears it.

### The search is a slot on `PickerSheet` and the filtering a pure function

`PickerSheet` gains an optional search: a query, a change callback and an empty-state label. When present it
draws a `KvField` above the list. The sheet doesn't filter. `PriceItemRows.filter(items, query)` in the
insights presentation module does, lower-casing with `Locale.ROOT` as `SuggestionRanker` does. Its rules: a
matching item keeps all its names, a matching name keeps its item as a section, and an item with one name drops
the name row. Sort's search field stays as it is.

*Alternative:* a dedicated sheet inside insights. That would copy `PickerList`, which is private in
`core:presentation`, and a later sheet that needs search would copy it again.

### Rows: the item as a section and option, names indented

Each item becomes a `PickerRow.Section` only when it has names to show beneath it. The item itself is always an
`Option`, so "the whole item" stays one tap away, and its names follow as indented `Option`s. This is the shape
`categoryRows` already uses for a two-level tree.

### Axis ticks come from a pure "nice numbers" function

`PriceAxis.ticks(low, high)` returns three to five values on a step from {1, 2, 2,5, 5} × 10ⁿ that covers
`[low, high]`. When `low == high` it widens to one step either side. Labels are amounts without the `kr` suffix,
and a `kr` unit sits above the axis. The gutter is as wide as the widest label, measured with
`rememberTextMeasurer`. Gridlines use a neutral token, so the accent remains the line.

### Points are tap targets in columns, as `TrendChart`'s bars already are

An overlay `Row` of `weight(1f)` columns sits over the canvas. Each column is `clickable` and carries
semantics: "juli: 14,95 kr" for a priced month, and no semantics for a gap. Tapping a gap selects the nearest
priced month, which another pure function resolves (`PriceAxis.nearestPriced(months, index)`, ties going to
the later month). The selected point draws larger with a vertical guide. The callout is a small surface above
the point, clamped inside the chart's width.

*Alternative:* `pointerInput` with hit-testing on the canvas. Columns give 48 dp targets and TalkBack nodes for
free, and match the trend chart.

## Risks / Trade-offs

- [One raw name, many spellings, such as OCR noise on photographed receipts, splits into several near-identical
  rows] → Normalization handles case and outer whitespace only, as aliases do. Fuzzy merging is out of scope,
  and search keeps the list usable.
- [The list's in-memory grouping loads every priced line of the page's items] → At most 100 items per page,
  with a household's purchase counts. If this ever grows, group per item with a count cap.
- [`ContractTest` checks nullability: a Kotlin default on `names` or `purchaseCount` may read as optional where
  the schema says required] → Declare them without defaults, as the server always sends them. An older server
  then fails to decode the list, which `Loader` reports as an error rather than misbehaving. Accepted, because
  the app is updated alongside the server.
- [The `name` filter is case-insensitive in C# but the key comes from the server] → The app only ever sends a
  key it received, and never builds one.

## Migration Plan

No schema change and no migration. Ship the backend first, regenerate `docs/openapi/kvitteria.json` in the same
commit, then the app. Rolling back the backend leaves a newer app unable to decode the list (see Risks), which
rolling back the app fixes.
