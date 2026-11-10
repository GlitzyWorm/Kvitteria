# Tasks

## 1. Backend: raw names and purchase counts

- [x] 1.1 Add `RawNames.Group` in `Features/Insights` (key by `NormalizeString`, label by most frequent spelling with ordinal tie-break, priced lines only, ordered by count then label) and verify new `RawNamesTests` cover spelling variants, Æ/Ø/Å, ties and excluded lines with `dotnet test Backend.Tests/Backend.Tests.csproj`
- [x] 1.2 Add `PurchaseCount` to `PriceMonthDto` and count priced lines per month in `PriceHistory.Build`, and verify `PriceHistoryTests` gain cases for a count of 3 beside a discount line and 0 for a gap
- [x] 1.3 Add `PricedNameDto` and `Names` to `PricedItemDto` (records in `Backend/Contracts`), and fill `GET /insights/prices` by loading the page's items' priced `(CanonicalItemId, RawName)` and grouping with `RawNames.Group`; verify by hand against the compose stack that each item lists its names
- [x] 1.4 Add the optional `name` parameter to `GET /insights/prices/{itemId}` (400 over `RawNameMaxLength`, in-memory filter with `NormalizeString`, label from the item's all-time priced names, key fallback) and `RawNameKey`/`RawNameLabel` on `PriceHistoryDto`; verify by hand that a narrowed, an unknown and an over-long name answer as the `insights/price-history` delta specifies
- [x] 1.5 Regenerate `docs/openapi/kvitteria.json` with `dotnet build Backend/Backend.csproj -p:GenerateOpenApi=true` and verify the diff shows only the added fields and parameter
- [x] 1.6 Update the Insights section of `CLAUDE.md` (raw names grouped in memory, the `name` filter scoped to the item) and verify it names `RawNames.Group` and states why the filter is not in SQL

## 2. Android: data and domain

- [x] 2.1 Add `PricedName` and `names` to `PricedItem`, `purchaseCount` to `PriceMonth`, and `nameKey`/`nameLabel` to `PriceHistory` in `core:domain`, and the matching DTOs and mappers in `KtorInsightsRepository`, with no defaults on required fields; verify `MappersTest` covers the new fields
- [x] 2.2 Add a `name` argument to `InsightsRepository.priceHistory` that sends the parameter only when not null, and verify with a `MockEngine` test that the query carries it
- [x] 2.3 Add `PricedNameDto` to `ContractTest`'s list and verify `./gradlew :core:data:test` passes against the regenerated document

## 3. Android: item sheet and selection

- [x] 3.1 Add an optional search slot (query, change callback, empty label) to `PickerSheet` in `core:presentation`, leaving existing callers unchanged, and verify with `./gradlew :core:presentation:testDebugUnitTest assembleDebug`
- [x] 3.2 Add `PriceItemRows` (rows with a section for items that show names, one-name items without a name row, and `filter` with `Locale.ROOT` lower-casing, keeping parents and siblings as the `mobile/insights` delta specifies) and verify `PriceItemRowsTest` covers each search scenario and `mælk`/`MÆLK`
- [x] 3.3 Replace `priceItemId` with `PriceSelection(itemId, nameKey)` plus `priceMonth` in `InsightsSelections`, load all pages of priced items in the loader, pass the name to history, clear `priceMonth` on a new pick, fall back when a selection disappears after reload, and verify with ViewModel tests using a fake repository and Turbine
- [x] 3.4 Wire the sheet in `InsightsScreen` (search field, rows, heading `Mælk · ARLA LETMÆLK 1L`) with Danish and English strings, and verify `./gradlew spotlessCheck testDebugUnitTest test` passes

## 4. Android: chart

- [x] 4.1 Add `PriceAxis.ticks` (nice steps, 3-5 ticks covering the range, widening an equal or single price by one step) and `PriceAxis.nearestPriced` (ties to the later month), and verify `PriceAxisTest` covers 14,45-14,95, one price, equal prices and a gap between two priced months
- [x] 4.2 Draw the Y axis in `PriceChart` (measured label gutter, `kr` unit, neutral gridlines from tokens) and verify on the design gallery or an AVD that labels don't clip at 0,99 and 1 249,00
- [x] 4.3 Add the column overlay with tap targets and semantics, the selected point and guide, and the clamped callout (month, price, `n køb` / `n purchases` plurals), and verify on an AVD that tapping a point, a gap and the edge months behaves as specified and that TalkBack reads each priced month

## 5. Integration

- [x] 5.1 Run the end-to-end check by hand against a compose stack per the Android e2e recipe: an item with two raw names shows both in the sheet, search narrows them, a narrowed chart differs from the whole item, and the callout count matches the receipts
