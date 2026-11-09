# Design

## Context

See proposal.md for why. What shapes the approach:

- `Receipt` stores `GrandTotal`, `PantTotal` and the lines' `LineTotal`s exactly as extraction produced them
  (`ReceiptIngestionService`), so the stored `Reconciled` bool has always equalled
  `Reconciliation.IsReconciled(GrandTotal - Σ LineTotal - PantTotal, tolerance)` computed from those figures. It holds
  no information of its own; it only goes stale.
- The tolerance is `PipelineSettings.OllamaReconciliationToleranceKr`, a single row an admin can change.
- `Reconciled` is read in three places: the `reconciled` filter on `GET /api/receipts`, the unreconciled count in
  `GET /api/pipeline/status`, and `ReceiptDto`. Insights don't read it.
- `ReceiptLine` has no position column; lines come back in key order.
- `MealCoverage.ClearOrphanedAsync` already drops coverage for a meal no line carries any more, using
  `ExecuteDeleteAsync`, which runs at once rather than in `SaveChanges`.
- Linking a line through `ItemAlias` lives in the private `ReceiptIngestionService.LinkCanonicalItemsAsync`, which
  applies `AliasLearning.FillGaps`.
- The app already computes `Receipt.difference` itself from the same figures, as exact `BigDecimal`s, and refreshes
  detail after every line save.

## Goals / Non-Goals

**Goals:**
- One definition of "reconciled", applied identically in the DTO, the filter and the count.
- An acceptance that can't outlive the figures it was given for, with no write path needing to know it exists.
- Hand-added and renamed lines resolve through aliases with the code ingestion uses.

**Non-Goals:**
- Re-running duplicate detection when a total changes. It stays an ingestion-time flag.
- Reordering lines, or giving added lines a position.
- Editing the merchant or purchase date.
- An audit trail of corrections. The acceptance records who and when; edits record nothing.

## Decisions

### Reconciliation is derived from the stored figures

The state is computed from the stored figures on every read, and the `Reconciled` column is dropped.

- *Alternative: recompute on write.* Every figure write would set the column. That's cheaper to read, but every
  current and future write path has to remember to do it, and a changed tolerance would never reach old receipts.
  Dropping the column also stops anyone reading a value that no longer means anything.
- The rule exists in two forms, as `GET /prices` already does for price: a pure C# classifier that
  `Backend.Tests` covers, and a translated EF predicate for the filter and the count. Both live in one file under
  `Features/Receipts` (`ReceiptReconciliation`), so they sit side by side:
  - `Classify(decimal delta, decimal tolerance, decimal? acceptedDifference) → ReconciliationState`, built on
    `Reconciliation.IsReconciled`, so extraction and stored receipts share the threshold rule.
  - `NeedsAttention(decimal tolerance) → Expression<Func<Receipt, bool>>`: `|delta| > tolerance` and
    `AcceptedDifference` null or not equal to delta, where delta is
    `GrandTotal - ReceiptLines.Sum(LineTotal) - PantTotal`. EF translates the sum to a correlated `COALESCE(SUM)`
    subquery. The tolerance is read once per request and passed in as a parameter.
- `ToDto` needs the tolerance, so it gains a parameter. Every receipt endpoint reads `PipelineSettings` once per
  request; on a list page that's one extra query, not one per row.
- Delta uses `Math.Abs` of the tolerance, as extraction does.

### An acceptance records the difference it was given for

Four nullable columns on `Receipt`: `AcceptedDifference` (money precision), `AcceptedAt`, `AcceptedById`
(`AppUser`, `Restrict`, like `AppUserId`) and `AcceptedNote` (200).

- `Accepted` is derived as "not reconciled, and delta equals `AcceptedDifference` exactly". Exact equality is
  sound because both are `numeric(…, 2)` values, never floats.
- A lapsed acceptance stays in the row and is ignored. Clearing it on every figure write is exactly the coupling
  this design avoids; restoring the accepted difference by later edits brings it back, which is what the spec says.
- *Alternative: a plain `Accepted` bool.* It would survive edits that open a new gap, and every write path would
  have to clear it.
- *Alternative: a separate acceptance table.* One acceptance per receipt and no history means it would only add a
  join.
- `POST /api/receipts/{id}/accept-difference` takes `AcceptDifferenceDto(decimal Difference, string? Note)`. The
  stated difference is compared with the current delta in the same request, so a figure someone else changed
  between the member reading the receipt and confirming gets a 409 rather than accepting an amount they never saw,
  the same shape as the sort endpoint's 409. It returns the updated `ReceiptDto`, because the app replaces its copy.
  The member comes from `CurrentUserService.ResolveAsync`.
- `DELETE` on the same path clears all four columns and returns 204, even when there's nothing to clear.

### The wire shape

- `ReceiptDto.Reconciled` (bool) is replaced by `Reconciliation` (`ReconciliationState`: `Reconciled`, `Accepted`,
  `Unreconciled`, serialized as names like every enum newer than `ReceiptStatus`) and `Acceptance`
  (`ReceiptAcceptanceDto(decimal Difference, Guid? MemberId, string? MemberDisplayName, DateTime AcceptedAt,
  string? Note)`), null unless the state is `Accepted`. A lapsed acceptance is never sent.
- The `reconciled` query parameter keeps its name and type. `false` is the `NeedsAttention` predicate and `true` its
  negation. The app sends only `false`, and a renamed parameter would be churn for no reader.
- `MemberDisplayName` needs `AcceptedBy` included. `ToDto` is client-evaluated, so every receipt query adds the
  `Include` explicitly (the `GET /mailboxes` trap in CLAUDE.md).

### Figure corrections

- `PATCH /api/receipts/{id}` takes `PatchReceiptDto(decimal? GrandTotal, decimal? PantTotal)` and returns
  `ReceiptDto`. No sign validation: a return receipt can be negative, and nothing else validates totals.
- `POST /api/receipts/{id}/lines` takes `CreateReceiptLineDto(RawName, Quantity, UnitPrice, LineTotal)` and returns
  201 with `ReceiptLineDto`. It is mapped on the receipt group because it creates a child of the receipt.
- `DELETE /api/receipt-lines/{id}` runs in an explicit transaction, because `ClearOrphanedAsync` executes
  immediately. It counts the receipt's lines (409 when this is the only one), calls `ClearOrphanedAsync` when the
  line has a meal, removes the line, saves and commits.
- All of these, and accepting, are gated by `EditReceipts`. Correcting figures is exactly what that permission
  covers.

### Alias linking is shared

`LinkCanonicalItemsAsync` moves from `ReceiptIngestionService` to a static `AliasLinking.LinkAsync(dbContext,
IReadOnlyCollection<ReceiptLine>, ct)` in `Features/Receipts`. It does the same thing: one `ItemAliases` query for
the lines' normalized names, then `AliasLearning.FillGaps`. It only touches lines without a `CanonicalItemId`, which
is a no-op for ingestion, where every line starts empty, and is the rule the rename case needs. Callers:

- ingestion, unchanged in behaviour;
- adding a line;
- `PATCH /receipt-lines/{id}` when `RawName` changed and the line has no item.

### The app

- `core:domain` `Receipt` replaces `reconciled: Boolean` with `reconciliation: ReconciliationState` (with
  `Unknown`, per the enum rule) and `acceptance: Acceptance?`. `difference` stays local; it's what the accept
  request states, and it is computed from the same exact decimals the server uses.
- `ReceiptRepository` gains `correctTotals`, `addLine`, `deleteLine`, `acceptDifference` and `withdrawAcceptance`.
  Detail refreshes after each, as it already does after a line save, rather than patching state optimistically,
  since coverage and the state can both change on the server.
- Adding and deleting a line, and renaming one, can change the unassigned count, so detail calls
  `WorkCounts.refresh()` after them. The failed-mail badge is unaffected by any of this.
- The line editor gains an add mode (no line ID, empty fields) and a delete action with confirmation. Totals get a
  small editor with two fields. Accepting is a confirmation dialog showing the amount, with an optional note field.
- The stored `warning` is labelled as what extraction reported and shown only while `Unreconciled`.
- New DTOs go into `ContractTest`'s list.

## Risks / Trade-offs

- [Reconciliation states change on deploy if the tolerance was edited after some receipts were stored] → This is the
  intended behaviour; the release notes say so.
- [The rule exists in C# and as an EF expression and could drift] → Both are in one file, and the classifier's
  tests document the boundary cases the predicate must match. A by-hand check against a running stack covers the
  predicate, as for every other query here.
- [List reads now sum lines per receipt for the filter and the count] → A correlated sum over a few dozen lines per
  receipt, across a household's receipts, is negligible; `ReceiptLines.ReceiptId` is already indexed as a foreign
  key.
- [An acceptance can come back after being lapsed] → Only when the difference returns to exactly the accepted
  amount, which means the receipt is again in the state a member accepted. The spec states it.
- [Dropping `Reconciled` is a schema change, so a failed update rolls back to `restore-required`] → Same as any
  migration; the column is derivable, so the down migration can recompute it.

## Migration Plan

One EF migration, `ReceiptReconciliationAcceptance`: add the four acceptance columns with the `AppUser` foreign key
and its index, and drop `Reconciled`. Nothing needs backfilling. The down migration re-adds `Reconciled` and fills it
from the figures with the stored tolerance in a single `UPDATE`, so a downgrade gets correct values rather than
`false` everywhere.
