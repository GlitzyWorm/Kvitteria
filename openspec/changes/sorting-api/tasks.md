# Tasks

Unless a task says otherwise, run tests from `Kvitteria` with
`dotnet test Backend.Tests/Backend.Tests.csproj`. Manual checks use `docker compose up -d --build`, with
a token from `scripts/token.sh`. Check permissions with a second, pre-authorized member.

## 1. Pure helpers

- [ ] 1.1 Add `AliasLearning.Matching` and `AliasLearning.FillGaps` in `Features/Receipts`, and verify `AliasLearningTests` cover case, surrounding whitespace, `Æ`/`Ø`/`Å`, a non-match, and that `FillGaps` never replaces a set value
- [ ] 1.2 Add `LineSorting.TryValidate`, and verify `LineSortingTests` cover neither item, both, a blank name, 200 and 201 characters, and a valid request of each kind

## 2. Teaching at ingestion

- [ ] 2.1 Move the alias upsert into `ItemAliases.UpsertAsync`, call it from `PUT /receipt-lines/{id}/canonical-item`, and verify by hand that the `PUT` still creates and replaces an alias
- [ ] 2.2 Change `LinkCanonicalItemsAsync` to apply item defaults through `FillGaps`, and verify by hand that a new receipt's alias-matched line has the item's category and meal, and that an item without defaults leaves them empty

## 3. Queue

- [ ] 3.1 Add `UnassignedLineDto` and `UnassignedLineCountDto` to `Contracts`, and verify the backend and `MobileApp` still build
- [ ] 3.2 Add `GET /receipt-lines/unassigned` with `.WithName()`, `.Produces` and `.ProducesProblem`, and verify by hand the fields, ordering, that a null member still lists, and the 400 for bad paging
- [ ] 3.3 Add `GET /receipt-lines/unassigned/count`, and verify by hand that it equals `totalCount` from the queue

## 4. Atomic save

- [ ] 4.1 Add `SortReceiptLineDto` and `SortReceiptLineResultDto` to `Contracts`, and verify the backend builds
- [ ] 4.2 Add `POST /receipt-lines/{id}/sort` for an existing item, covering the transaction, category, meal, defaults and coverage cleanup, and verify by hand a full save, clearing a field, 404 for each unknown ID, 409 on a line with an item, and that a covered meal's coverage goes when its last line changes
- [ ] 4.3 Add new-item creation to the save, and verify by hand creating an item, the 409 on a name differing only by case, and that a failed save leaves no item behind
- [ ] 4.4 Add the `ManageTaxonomy` checks, and verify by hand as a member without it that a plain save succeeds, and that a new item or defaults answers 403 with nothing changed
- [ ] 4.5 Add alias remembering, waiting-line resolution and the response counts, and verify by hand that a matching line on another receipt is assigned with gaps filled, a line with a category keeps it, a line with another item is untouched, and the reported numbers match the queue afterwards
- [ ] 4.6 Verify atomicity by hand: make the save fail after the line changes, such as with an item name that collides on commit, and confirm the line, item and aliases are all unchanged

## 5. Documentation

- [ ] 5.1 Update the domain model and pipeline sections of `CLAUDE.md` to say ingestion applies item defaults, automatic writes only fill empty fields, and why waiting lines are matched in memory, and verify the text matches the code
- [ ] 5.2 Add the sort flow to `README.md` beside the existing `receipt-lines` example, and verify the documented `curl` commands run as written

## Workflow follow-up

- `spending-api` comes next. It can reuse the unassigned count.
- Archive this change after review.
