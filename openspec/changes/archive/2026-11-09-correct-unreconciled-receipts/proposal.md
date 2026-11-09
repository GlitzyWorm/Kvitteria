# Proposal

## Why

A receipt that didn't reconcile can't be brought back to reconciled. `Receipt.Reconciled` is written once at
ingestion and never again, so a member can correct a line until the app's own difference reads 0,00 kr and the
receipt still says Unreconciled, beside a callout quoting a delta that no longer exists. The household also can't
correct most of what extraction gets wrong: only a line's name, quantity, unit price and total are editable, so a
misread pant or grand total, a missing line or an invented one can't be fixed at all. And a receipt that is
printed correctly but genuinely doesn't add up, such as one with cash rounding, has no way out of the
Unreconciled list.

## What Changes

- **Reconciliation is derived, not stored.** Whether a receipt reconciles is computed from its stored figures
  whenever it is read, with the tolerance currently in pipeline settings, using the same rule as extraction. The
  `Reconciled` column is dropped. Corrections therefore take effect by themselves, and a changed tolerance applies
  to every receipt.
- **A difference can be accepted as a last resort.** A member with `EditReceipts` can accept a receipt's current
  difference, with an optional note. The acceptance records the exact amount, who accepted it and when, and holds
  only while the receipt's difference still equals that amount, so any later edit to the figures cancels it
  without anything having to clear it. An acceptance can be withdrawn.
- **Three reconciliation states on the wire.** `ReceiptDto` carries `reconciliation` (`Reconciled`, `Accepted`,
  `Unreconciled`) and the live acceptance, replacing the `reconciled` bool. **BREAKING** for API clients; the app
  is the only one and changes with it.
- **"Unreconciled" means needs attention.** `GET /api/receipts?reconciled=false` and the pipeline status count
  return only receipts in the `Unreconciled` state; accepted receipts are left out.
- **Grand total and pant can be corrected** through `PATCH /api/receipts/{id}`.
- **Lines can be added and deleted.** An added line is linked through learned aliases and takes the item's
  defaults, exactly as at ingestion. Deleting a line removes meal coverage that no longer has a line, keeps
  learned aliases, and is refused for a receipt's last line, which is deleted with the receipt instead.
- **Renaming an unassigned line links it** through learned aliases, so correcting a garbled name sorts the line
  if the household already taught that name.
- **The app** shows the three states, offers accepting beside the difference behind a confirmation, edits total
  and pant, adds and deletes lines, and shows the stored extraction warning as history rather than as the current
  state.

## Capabilities

### New Capabilities

- `receipts/reconciliation`: how a stored receipt's reconciliation state is decided, how a difference is accepted
  and withdrawn, and what the unreconciled filter returns.
- `receipts/correction`: correcting a stored receipt's figures on the server: its grand total and pant, adding,
  editing and deleting lines.

### Modified Capabilities

- `receipts/alias-learning`: lines added by hand, and unassigned lines renamed by hand, are linked through aliases
  and take the item's defaults, like lines from ingestion.
- `pipeline/status`: the unreconciled count leaves out receipts whose difference has been accepted.
- `mobile/receipts`: the list and filter follow the new states; detail shows Accepted with who and when, offers
  accepting and withdrawing, edits total and pant, adds and deletes lines, and labels the extraction warning as
  history.
- `mobile/pipeline`: the unreconciled list leaves out accepted receipts.

## Impact

- **Backend:** `Receipt` model and a migration (drop `Reconciled`, add the acceptance columns with an `AppUser`
  foreign key); `Reconciliation` gains the three-state rule; `ReceiptEndpoints`, `ReceiptLineEndpoints` and
  `PipelineEndpoints`; new DTOs in `Contracts/DTO` with their `ToDto` extensions; the alias-linking code moves out
  of `ReceiptIngestionService` so ingestion and the line endpoints share it. `Backend.Tests` gains the state rule.
- **Contract:** `docs/openapi/kvitteria.json` regenerated; `reconciled` replaced on `ReceiptDto`.
- **Android:** `core:domain` and `core:data` receipt models and repository, `ContractTest`'s list,
  `feature/receipts` detail and list, `feature/pipeline`, Danish and English strings.
- **Updates:** the release carries a migration, so a rollback from it is `restore-required`, as for any schema
  change.
- **Docs:** `CLAUDE.md`'s paragraph on unreconciled receipts.
