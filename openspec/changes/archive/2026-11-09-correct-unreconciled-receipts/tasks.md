# Tasks

## 1. Reconciliation rule

- [x] 1.1 Add `ReconciliationState` (`Reconciled`, `Accepted`, `Unreconciled`) and `ReceiptReconciliation.Classify(delta, tolerance, acceptedDifference)` in `Features/Receipts`, built on `Reconciliation.IsReconciled`; verify with `Backend.Tests` cases for within tolerance, just outside, a negative delta, accepted equal, accepted lapsed, accepted but within tolerance (reconciled wins), and a negative tolerance setting
- [x] 1.2 Add `ReceiptReconciliation.NeedsAttention(tolerance)` as an EF expression beside `Classify`, with a comment tying it to the classifier; verify the build passes under `TreatWarningsAsErrors`

## 2. Schema

- [x] 2.1 Add `AcceptedDifference`, `AcceptedAt`, `AcceptedById` (with `AcceptedBy`, `Restrict`, indexed) and `AcceptedNote` (200) to `Receipt` and configure them in `KvitteriaDbContext`, using money precision for the amount; remove `Receipt.Reconciled` and its assignment in `ReceiptIngestionService`; verify `dotnet build` passes
- [x] 2.2 Add the `ReceiptReconciliationAcceptance` migration with `dotnet ef migrations add`, editing `Down` to re-add `Reconciled` and fill it from the figures and the stored tolerance in one `UPDATE`; verify the generated `Up` adds the four columns and drops `Reconciled`, and that a fresh stack migrates at startup (`/health/ready` reports `migrations` healthy)

## 3. Receipt reads

- [x] 3.1 Add `ReceiptAcceptanceDto` and replace `ReceiptDto.Reconciled` with `Reconciliation` and `Acceptance`; give `ToDto` the tolerance and derive both from `Classify`; verify the build passes
- [x] 3.2 Read the tolerance once per request in `GET /receipts` and `GET /receipts/{id}`, add `Include(r => r.AcceptedBy)` to both, and make `reconciled=false`/`true` use `NeedsAttention` and its negation; verify against a running stack that an unreconciled receipt is listed with `reconciled=false` and that `totalCount` matches
- [x] 3.3 Count `NeedsAttention` in `GET /pipeline/status`; verify the count matches the length of `GET /receipts?reconciled=false` on the running stack
- [x] 3.4 Pass the tolerance to every other `ToDto` call on a receipt, and verify with a search for `ToDto` that none is left without it

## 4. Accepting a difference

- [x] 4.1 Add `AcceptDifferenceDto` and `POST /receipts/{id}/accept-difference` (`EditReceipts`): 404, 400 for a note over 200 characters, 409 when the receipt reconciles or the stated difference isn't the current one, otherwise record the amount, member, time and note and return the `ReceiptDto`; declare `WithName`, `Produces` and `ProducesProblem`; verify by hand that accepting moves a receipt out of `reconciled=false` and that a stale difference gets 409
- [x] 4.2 Add `DELETE /receipts/{id}/accept-difference` (`EditReceipts`), clearing the four columns and answering 204 whether or not there was one; verify by hand that the receipt returns to `reconciled=false`
- [x] 4.3 Verify by hand that editing a line on an accepted receipt makes it read `Unreconciled` with no acceptance, and that restoring the line makes it `Accepted` again

## 5. Correcting figures

- [x] 5.1 Move `LinkCanonicalItemsAsync` from `ReceiptIngestionService` to `AliasLinking.LinkAsync` in `Features/Receipts`, touching only lines without an item, and call it from ingestion; verify ingestion of a mail with an aliased line still stores the item, category and meal
- [x] 5.2 Add `PatchReceiptDto` and `PATCH /receipts/{id}` (`EditReceipts`) for `GrandTotal` and `PantTotal`, returning `ReceiptDto`; verify by hand that correcting the pant on an unreconciled receipt makes it `Reconciled` when the figures add up
- [x] 5.3 Add `CreateReceiptLineDto` and `POST /receipts/{id}/lines` (`EditReceipts`): 404, 400 for a blank or over-300-character name, otherwise add the line, run `AliasLinking.LinkAsync` and return 201 with `ReceiptLineDto`; verify by hand that an aliased name comes back with its item and defaults and an unknown one appears in `GET /receipt-lines/unassigned`
- [x] 5.4 Add `DELETE /receipt-lines/{id}` (`EditReceipts`) in a transaction: 404, 409 for the receipt's only line, `MealCoverage.ClearOrphanedAsync` when the line has a meal, then remove and commit, answering 204; verify by hand that deleting the last line with a covered meal removes the coverage, that another line with the meal keeps it, and that the alias survives
- [x] 5.5 In `PATCH /receipt-lines/{id}`, run `AliasLinking.LinkAsync` when `RawName` changed and the line has no item; verify by hand that renaming an unassigned line to an aliased name assigns it and that an assigned line keeps its item

## 6. Contract and backend docs

- [x] 6.1 Regenerate `docs/openapi/kvitteria.json` with `dotnet build Backend/Backend.csproj -p:GenerateOpenApi=true`; verify the diff shows only this change's endpoints and DTO fields, and `dotnet test Backend.Tests/Backend.Tests.csproj` passes
- [x] 6.2 Update CLAUDE.md: the "Unreconciled receipts are not failures" paragraph (derived state, acceptance, corrections), the new `Backend.Tests` coverage in the test list, and `AliasLinking` beside `AliasLearning`; verify each named file and type exists

## 7. App data layer

- [x] 7.1 In `core:domain`, add `ReconciliationState` with `Unknown` and `Acceptance`, replace `Receipt.reconciled` with `reconciliation` and `acceptance`, and add `correctTotals`, `addLine`, `deleteLine`, `acceptDifference` and `withdrawAcceptance` to `ReceiptRepository`; verify `./gradlew :core:domain:test`
- [x] 7.2 In `core:data`, update `ReceiptDto`, add the new request and acceptance DTOs, implement the repository methods in `KtorReceiptRepository` (keeping its cache consistent with each result), and add every new DTO to `ContractTest`; verify with `./gradlew :core:data:testDebugUnitTest`, including `MockEngine` tests for the 409 paths and an unknown state name

## 8. App screens

- [x] 8.1 Update the receipts list and the Pipeline problems list to tag only `Unreconciled`; verify `./gradlew testDebugUnitTest` for both features
- [x] 8.2 In detail, show the three states with the acceptance's amount, member, time and note, and label the stored warning as what extraction reported, shown only while `Unreconciled`; add the strings to `values/strings.xml` (Danish) and `values-en/strings.xml`; verify on the AVD against the compose stack
- [x] 8.3 Add accept (confirmation with amount and optional note, sending `difference`) and withdraw to detail for `EditReceipts`, showing the server's message and refreshing on 409; verify with `ReceiptDetailViewModel` tests for success, 409 and no permission
- [x] 8.4 Add the totals editor to detail for `EditReceipts`, with decimal-comma parsing through `KrFormat.parse` and a refresh on success; verify with ViewModel tests for an invalid number and success
- [x] 8.5 Give the line editor an add mode and a delete action with confirmation, refreshing the receipt and calling `WorkCounts.refresh()` after add, delete and a rename, and showing the server's message on a 409; verify with ViewModel tests for blank name, successful add, delete and the only-line 409
- [x] 8.6 Verify `./gradlew spotlessCheck testDebugUnitTest test` passes, including MPL headers on any new Kotlin files

## 9. End to end

- [x] 9.1 On the AVD against a throwaway compose stack with an unreconciled receipt: correct a line until it reconciles, correct the pant, add and delete a line, accept a difference and then change a figure, and withdraw an acceptance; verify the list filter, the Pipeline problems list and the Sort badge follow each step

## Workflow follow-up

- Archive the change after review, then confirm `openspec/specs/receipts/reconciliation` and `receipts/correction` exist with their Purpose sections.
