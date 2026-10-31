# Tasks

Unless a task says otherwise, run tests from `Kvitteria` with
`dotnet test Backend.Tests/Backend.Tests.csproj`. Manual checks use `docker compose up -d --build` and
`docker compose logs -f backend`, with a token from `scripts/token.sh`.

## 1. Pure helpers

- [x] 1.1 Add `DanishTime` in `Features/Shared` with the month's UTC range, move `PurchaseDate` onto it, and verify `PurchaseDateTests` still pass and new `DanishTimeTests` cover month boundaries in winter and summer time
- [x] 1.2 Add `PipelineError.Describe(Exception)` and verify `PipelineErrorTests` cover a single exception, an inner exception and repeated messages

## 2. Data model

- [x] 2.1 Add the `ReceiptIntake` entity, its `DbSet` and configuration (status stored as text, filtered unique index on `SourceMessageId`, index on `Status`, `MailboxId` SetNull, `AppUserId` Restrict), and verify the backend builds
- [x] 2.2 Add nullable `AppUserId` (Restrict) and `MailboxId` (SetNull) to `Receipt`, and `LastMessageAt` to `Mailbox`, updating the comment in `DELETE /mailboxes/{id}` that says nothing references a mailbox, and verify the backend builds
- [x] 2.3 Scaffold the migration with `dotnet ef migrations add AddReceiptIntake --project Backend` and verify it only adds a table, columns and indexes, and that the backend applies it on an existing database with receipts intact

## 3. Durable intake

- [x] 3.1 Add a singleton `ReceiptIntakeStore` that uses `IServiceScopeFactory`, with claim, mark-extracted and mark-failed conditional updates, and verify the backend builds
- [x] 3.2 Add `IntakeId` and `MailboxId` to `ReceiptRecord`, plus building a record from a row, and verify the backend builds
- [x] 3.3 In `MailFetcher`, check for a known message ID, write the row and set `Mailbox.LastMessageAt` before enqueueing, and flag a known message `Seen` without enqueueing, and verify by hand that a new mail creates a row and that marking a stored receipt's mail unread creates no duplicate
- [x] 3.4 Change `WorkQueue<T>` to allow several writers and add `TryEnqueue`, and verify the backend builds
- [x] 3.5 Change `QueueWorker` to `OnFailedAsync` with a guarded write and no failure recorded on shutdown, and update `ExtractionWorker` and `ReceiptFactoryWorker`, including the claim and the `Extracted` transition, and verify the existing tests pass
- [x] 3.6 Make `ReceiptIngestionService` copy member and mailbox onto the receipt and delete the intake row in its transaction, and verify by hand that a processed mail leaves a receipt with its member and no intake row
- [x] 3.7 Add recovery at the start of `MailWorker.ExecuteAsync`, and verify by hand that restarting the backend during extraction with `docker compose restart backend` ends with the receipt stored and no failure reported

## 4. API

- [x] 4.1 Add `ReceiptFailureDto` and `PipelineStatusDto` to `Contracts` with `ToDto` mappings in `Features/Endpoints/Extensions`, and verify the backend builds
- [x] 4.2 Add `PipelineEndpoints` with `GET /status` and `GET /failures`, mapped from `MapReceiptEndpoints`, with `.WithName()`, `.Produces` and `.ProducesProblem`, and verify by hand that both answer on an idle stack and that the failure response has no mail text
- [x] 4.3 Add `POST /failures/{id}/retry` and verify by hand 202, 404, 403 without `EditReceipts`, and 409 on a mail that is not failed
- [x] 4.4 Add `DELETE /failures/{id}` and verify by hand 204, 404, 403 and 409
- [x] 4.5 Add `Error`, `MemberId` and `MemberDisplayName` to `ReceiptDto`, the member `Include` on both receipt queries and the `reconciled` filter, and verify by hand that the fields are populated on list and detail, that old receipts return null, and that `Kvitteria/MobileApp` still builds with `dotnet build MobileApp/MobileApp.csproj -f net10.0-android`

## 5. Failure path, end to end

- [x] 5.1 Stop the `ollama` container so extraction fails, deliver a mail, and verify it appears in `GET /failures` with the underlying cause and in the status failed count
- [x] 5.2 Start `ollama`, retry that failure, and verify it becomes a receipt with its original member and leaves the failure list
- [x] 5.3 Stop `ollama` again, deliver a non-receipt mail, dismiss it, and verify the row and its text are gone from `ReceiptIntakes`

## 6. Documentation

- [x] 6.1 Update the pipeline architecture, `WorkQueue` and permission sections of `CLAUDE.md`, including why rows are written before `Seen`, why shutdown is not a failure and why unreconciled receipts are not retryable, and verify no text still says the queues are single-writer
- [x] 6.2 Update `docs/troubleshooting.md` with how to find, retry and dismiss failed mail, and how to recover mail lost before this change by marking it unread, and verify the documented `curl` commands run as written

## Workflow follow-up

- `sorting-api` and `spending-api` can follow in either order. `android-app` depends on all three.
- Archive this change after review, which turns its specs into the first main specs.
