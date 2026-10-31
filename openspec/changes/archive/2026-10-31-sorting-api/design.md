# Design

## Context

See `proposal.md` for motivation and `specs/` for required behaviour.

Current state of the code this builds on:

- `PUT /receipt-lines/{id}/canonical-item`, `/category` and `/meal` each save on their own. A client
  doing all three can leave a line half-sorted.
- The alias write sits inline in the `canonical-item` handler. It replaces an existing alias, so its 409
  only fires when two writers race on the unique index.
- `ItemAlias.RawName` is `citext` and holds `NormalizeString()` output: `Trim().ToLowerInvariant()`.
  `ReceiptLine.RawName` is stored as printed.
- `LinkCanonicalItemsAsync` sets only `CanonicalItemId`. No code reads `CanonicalItem.CategoryId` or
  `DefaultMealId` for a line, so `SetAsDefault` writes values nothing uses.
- `MealCoverage.ClearOrphanedAsync` runs `ExecuteDeleteAsync`, so it takes effect immediately rather
  than at `SaveChanges`.

## Goals / Non-Goals

**Goals:**

- One round trip per sorted line, with no partial result.
- The same normalization for alias matching at save time as at ingestion.
- A person's earlier choice is never overwritten by something automatic.

**Non-Goals:**

- Server-side suggestions.
- Backfilling category and meal on lines auto-linked before this change.
- Changing the existing per-line `PUT`s beyond sharing the alias write. Receipt detail keeps them, and
  they still don't resolve waiting lines.
- Excluding discount or deposit-style lines from the queue. They need an item like any other line for
  now.

## Decisions

### Routes

Beside the existing routes in `ReceiptLineEndpoints`:

| Route | Result | Permission |
|---|---|---|
| `GET /receipt-lines/unassigned?page=&pageSize=` | `PagedResult<UnassignedLineDto>` | read |
| `GET /receipt-lines/unassigned/count` | `UnassignedLineCountDto` | read |
| `POST /receipt-lines/{id}/sort` | `SortReceiptLineResultDto` | `CategorizeLines` |

The `{id:guid}` constraint keeps `unassigned` from colliding with the single-line route.

The count is a separate route because `?pageSize=1` would still pay for the joins, and it will back a
badge. `spending-api` can call the same query.

`POST` rather than `PUT`, because this is a command with side effects on other lines and an item,
rather than a replacement of one resource.

### Request shape

```text
SortReceiptLineDto(CanonicalItemId?, NewItemName?, CategoryId?, MealId?, RememberAlias, SetAsDefaults)
```

Category and meal are the full desired state, so null means empty, matching "Ingen" in the mockup.

`LineSorting.TryValidate` checks the exactly-one-item rule and name length without touching the
database, in the style of `MealCoverage.TryValidate`, and is unit tested. It uses a limit of 200,
matching `NameLength` in the context. `POST /canonical-items` has no length check, and that gap is left
alone.

`ManageTaxonomy` is checked in the handler through `CurrentUserService.HasAsync`, because whether it's
needed depends on the body. This is the same reason `SetAsDefault` does it, and the route policy
declares only `CategorizeLines`.

### One explicit transaction

The handler opens a transaction, as `ReceiptIngestionService` does. `SaveChanges` alone isn't enough,
for two reasons: `ClearOrphanedAsync` executes immediately, and a new item must be saved to get its ID
before the alias can point at it.

Order inside it:

1. Load the line. 404 if missing, 409 if it already has an item.
2. Resolve the category and meal (404), then the item: find it (404) or add it.
3. Set the line's fields and, if asked, the item's defaults.
4. Clear orphaned coverage for the line's previous meal.
5. If asked, upsert the alias and resolve waiting lines.
6. `TrySaveChangesAsync`, then commit. `false` means a unique index rejected the write, which is the
   name clash (409) or an alias race (409). The transaction is disposed uncommitted either way.

Checking the item's permission and shape comes first of all, so a 403 or 400 does no database work.

The "already has an item" check reads then writes, so two simultaneous saves on one line could both
pass it. A conditional update would close that, but the loser's only effect is last-write-wins on a
line two people sorted in the same instant, and this is a household app. Accepted.

### Matching waiting lines in memory, not SQL

`AliasLearning.Matching(lines, normalizedRawName)` is a pure helper. The handler loads lines where
`CanonicalItemId == null` and filters them with `NormalizeString()`.

Translating it to SQL (`lower(btrim(...))`) would depend on the database's locale for `Æ`, `Ø` and `Å`
and could quietly disagree with the C# normalization that wrote the alias. The candidate set is the
queue itself, which is small because it's work a person is getting through.

`AliasLearning.FillGaps(line, categoryId, mealId)` sets a field only when it's empty. It is shared with
ingestion, so "an automatic write never replaces a value" lives in one tested place. The mockup
overwrites instead, and this is a deliberate difference.

Filling a gap can't orphan coverage, because no meal is removed from any line.

### Ingestion applies defaults

`LinkCanonicalItemsAsync` changes its alias lookup to project the item's `CategoryId` and
`DefaultMealId`, and calls `FillGaps` for each linked line. New lines have nothing set, so it always
applies, but one rule stays in one place.

This goes in this change rather than `spending-api` because it's the other half of alias learning:
without it, a line resolved by alias is out of the queue but never categorized, and shows as
uncategorized spend for good.

### Sharing the alias write

The inline upsert moves to `ItemAliases.UpsertAsync(dbContext, rawName, canonicalItemId, ct)` in
`Features/Receipts`, and the existing `PUT` calls it. Its behaviour doesn't change.

### Response

`SortReceiptLineResultDto(ReceiptLineDto Line, int OtherLinesAssigned, int RemainingCount)`. The count
is queried after saving, inside the transaction, so it reflects this save. The line needs its
navigations populated for `ToDto`, so the handler sets them when loading, as the existing `PUT`s do.

`UnassignedLineDto` is built with a translated `Select` rather than `ToDto`, so it needs no `Include`s
and pulls only the columns it uses.

The queue orders by `PurchaseDate` descending, then `ReceiptId`, then line `Id`. Lines have no printed
position, so `Id` is only there to make paging stable.

## Risks / Trade-offs

- [A remembered alias with no item defaults] → Future lines link to the item but stay uncategorized,
  and they aren't in the queue. The Android app defaults to writing defaults for new items, as the
  mockup does, and the person can still set category on the receipt.
- [Paged queue shifts while sorting] → A save can remove several lines, so a cached page goes stale.
  The 409 is the signal, and the client refetches.
- [Unique-index 409 can't tell name from alias] → The detail text covers both cases. A race on the
  alias is rare enough that a clear combined message is fine.
- [A large backlog on first use] → Loading all unassigned lines per save is linear in the queue. That's
  hundreds of rows at most and only when remembering an alias.
- [No automated coverage of the transaction] → `Backend.Tests` covers the pure helpers. The endpoint
  paths are verified by hand, in tasks.

## Migration Plan

No migration is needed. The endpoints are additive, and `Contracts` gains new records only. To roll
back, redeploy the previous image; data written meanwhile stays valid.

Lines already auto-linked keep their empty category and meal. They can be set on the receipt, and a
one-off SQL update from each item's defaults could fill them later if that matters.
