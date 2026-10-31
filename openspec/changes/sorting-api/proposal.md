# Proposal

## Why

Sorting receipt lines is the app's daily job, and the server cannot support it yet. Nothing lists the
lines that still need a canonical item, saving one line takes three separate PUTs that can partly
fail, and a new alias only teaches future receipts, leaving identical lines already in the queue
unsolved. Teaching doesn't fully work either: ingestion links a line to its item but never reads the
item's default category or meal, so an alias-matched line stays uncategorized and never enters the
queue. The Android Sort screen and the spending numbers both need this fixed.

## What Changes

- Add a paged queue of receipt lines with no canonical item, each with the receipt context the screen
  shows (merchant, date, member, amounts).
- Add a count of those lines, cheap enough to drive the navigation badge and the Overview card.
- Add one atomic save for a line: canonical item, category, meal, whether to remember the alias, and
  whether to write the item's defaults. All of it applies or none of it does.
- Allow that save to create the canonical item in the same transaction.
- Reject a save on a line that already has an item, so two people sorting at once cannot overwrite
  each other.
- When a save remembers an alias, apply it to other unassigned lines with the same normalized raw
  name, and report how many lines changed so the app can show it. Those lines take the saved category
  and meal only where they have none, so a person's earlier choice is kept.
- When ingestion links a line through an alias, copy the item's default category and meal onto it.
- Keep the existing per-line `PUT` endpoints unchanged. Receipt detail still uses them.
- Item suggestions stay out of the server for now. The client ranks them from the canonical item
  list, as the mockup does. If that list proves too large to fetch, this is revisited.
- Skipping a line stays client-side and per-session, as the mockup does. It is not persisted.

## Capabilities

### New Capabilities

- `receipts/line-sorting`: the queue of unassigned lines, its count, and saving a line's assignment
  atomically.
- `receipts/alias-learning`: how remembering an alias affects future receipts and matching existing
  lines.

### Modified Capabilities

None. `receipts/attribution` is untouched, and the queue only reads the member it records.

## Impact

- **Backend:** new endpoints beside `ReceiptLineEndpoints`, `ReceiptIngestionService` linking, and pure
  helpers in the style of `MealCoverage`. The alias write is shared with the existing `PUT`.
- **Database:** no schema change. The existing foreign-key index on `ReceiptLine.CanonicalItemId`
  already serves the queue.
- **Contracts:** new DTOs only.
- **Authorization:** the save needs `CategorizeLines`. Creating an item or writing its defaults also
  needs `ManageTaxonomy`, the same rule `SetAsDefault` enforces today.
- **Existing data:** lines auto-linked before this change keep their empty category and meal.
- **Depends on:** `pipeline-failure-tracking`, now applied, for the receipt's member.
