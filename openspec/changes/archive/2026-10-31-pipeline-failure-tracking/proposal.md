# Proposal

## Why

Receipts are lost silently in two ways. A failed extraction is only recorded on the in-memory
`ReceiptRecord`, with no database row, and the mail has already been flagged `Seen`, so it is never
picked up again. A restart loses more: mail is flagged `Seen` as soon as it enters an in-memory queue,
so everything still waiting is dropped, and extraction takes minutes per attempt. Nobody can see that
either happened. The Android app's Pipeline screen (stage counts, a Problems list with Retry, mailbox
status) has nothing to read until the server keeps this state, and this fix is worth shipping on its
own even if nothing else follows.

## What Changes

- Record every fetched mail durably before it is flagged `Seen`, and keep that record until the
  receipt is stored, so mail in the pipeline survives a restart and resumes by itself.
- Keep a mail that fails, with its error, attempts, source mailbox and text, until someone retries or
  dismisses it. Mail text is deleted as soon as the mail is resolved.
- Add retry, which sends a failed mail back through extraction, and dismiss, for mail that was never a
  receipt.
- Skip mail whose message ID is already stored or in progress, rather than extracting it again.
- Expose failures over `/api`, including the error text.
- Expose pipeline status: how many mails are in each stage, how many receipts are unreconciled, and
  when mail was last polled.
- Expose per-mailbox activity beyond the existing `LastPolledAt`/`LastError`: last mail received and
  receipts this month.
- Add `Error` to `ReceiptDto`, so the warning already stored for an unreconciled receipt reaches
  clients, and add a filter to `GET /api/receipts` for finding unreconciled receipts.
- Record which mailbox, and so which household member, each receipt came from, and return that member
  on `ReceiptDto`. Receipts ingested before this change have no known member.
- Unreconciled receipts are not retryable, unlike the mockup. They are already stored, may already be
  categorized, and their mail text is gone, so they are corrected by hand.

## Capabilities

### New Capabilities

- `pipeline/durable-intake`: how fetched mail is recorded, survives restarts and avoids duplicates.
- `pipeline/failure-tracking`: how failed mail is kept, listed, retried and dismissed.
- `pipeline/status`: what the server reports about stages, polling and per-mailbox activity.
- `receipts/attribution`: which mailbox and household member a receipt belongs to.

### Modified Capabilities

None. No specs exist yet.

## Impact

- **Backend:** `MailFetcher`, `MailWorker`, `ExtractionWorker`, `ReceiptFactoryWorker`, `QueueWorker`,
  `ReceiptIngestionService`, the work queues, and new endpoints under `/api`.
- **Database:** a migration for the intake table, and for the receipt's mailbox and member links. The
  migration must leave existing receipts valid.
- **Contracts:** new DTOs, plus additive fields on `ReceiptDto`. No existing field changes.
- **Authorization:** reading stays ungated like every other read. Retry and dismiss need
  `EditReceipts`.
- **Privacy and storage:** mail text is now held in the database, and so in backups, but only while
  the mail is unresolved.
- **Limits:** mail lost before this change is not recovered automatically. Marking it unread in the
  mail client sends it through the pipeline again, because nothing stored matches its message ID.
