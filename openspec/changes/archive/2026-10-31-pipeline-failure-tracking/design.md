# Design

## Context

See `proposal.md` for motivation and `specs/` for required behaviour.

The pipeline today is `MailWorker` → `ExtractionQueue` → `ExtractionWorker` → `PersistenceQueue` →
`ReceiptFactoryWorker`, with one mutable in-memory `ReceiptRecord` passing through. Four facts shape
the design:

- `MailFetcher` flags a message `Seen` once the enqueue callback returns, so from then on the only copy
  of the mail text is in memory.
- `QueueWorker.OnFailed` is a synchronous method that writes to that in-memory record and nothing else.
- `Receipt.MerchantId` is required, and every stored `Receipt` is `Completed`. The `Queued`,
  `Extracting`, `Extracted` and `Failed` values of `ReceiptStatus` never reach the database.
- `WorkQueue<T>` declares `SingleWriter = true`, and `MailWorker` is the one writer of
  `ExtractionQueue`. A retry from an endpoint would be a second writer.

The backend runs as a single process. There are no integration tests, so anything that touches the
database or HTTP is verified by hand against the compose stack.

## Goals / Non-Goals

**Goals:**

- After the mail is flagged `Seen`, the database always holds it or its receipt.
- Keep the channels as the way work moves, so the change stays small.
- Keep `Receipt` meaning "successfully parsed", so `spending-api` never has to filter failures.

**Non-Goals:**

- Multiple backend instances. Claiming a row is safe, but startup recovery assumes one process.
- Re-extracting stored receipts, including unreconciled ones.
- Reporting live attempt numbers, as the mockup's "attempt 2/3" does. Stage counts are enough for now.
- Client UI. That belongs to `android-app`.

## Decisions

### A durable intake table, not a failure table

Add `ReceiptIntake`: one row per mail from fetch until it is resolved. It holds `SourceMessageId`,
`From`, `Subject`, `MailDate`, `BodyText`, `BodySource`, `MailboxId`, `AppUserId`, `Status`, `Error`,
`Attempts`, `ReceivedAt` and `FailedAt`.

The row is written before the message is flagged `Seen`, and is deleted in the same transaction that
inserts the `Receipt`. A failed mail keeps its row. A row therefore means "unresolved mail", which is
why mail text is held only as long as it is needed.

*Alternatives:*

- **`ReceiptFailure`, written on failure**, the proposal's first idea. It leaves mail in the queues
  unprotected, so a restart still loses it. The row would also be created at the moment things are
  already going wrong.
- **Failed rows in `Receipt`** would need a nullable merchant, purchase date and totals, and every
  query for totals would have to exclude them.
- **Polling the table instead of using channels** is the cleaner long-term design, but it rewrites all
  three workers. Channels stay as the delivery mechanism, and the table is the source of truth.

### Reuse `ReceiptStatus` for intake state

`Status` uses `Queued`, `Extracting`, `Extracted` and `Failed`, stored as text like `Receipt.Status`.
`Completed` is never stored on a row, because a completed row is deleted. The mockup's "Persisting"
stage is `Extracted`. The status endpoint reports named counts, so the numeric wire format of
`ReceiptStatus` doesn't leak any further.

### Claim on dequeue

`ExtractionWorker` starts each item with a conditional update from `Queued` to `Extracting`. If no row
changes, it skips the item.

This makes a duplicate enqueue harmless, which matters during the short overlap where startup recovery
and a retry could both enqueue the same row. Every later transition also updates by ID and expected
status.

### Recovery runs in `MailWorker`, before the first poll

At the start of `ExecuteAsync`, `MailWorker` resets `Extracting` and `Extracted` rows to `Queued`, then
enqueues every `Queued` row, oldest first. A full queue simply waits, which delays the first poll and
is the right kind of backpressure. A separate hosted service would have to be ordered against
`MailWorker`, and this way the ordering is already there.

Extraction restarts from the mail text rather than resuming a half-stored result. The result from
`Extracted` isn't kept on the row, so a crash there costs one more extraction, which is rare and
harmless.

### Shutdown stops being a failure

`QueueWorker` no longer records anything on the shutdown path. The row stays as it is and recovery
picks it up on the next start.

`OnFailed` becomes `Task OnFailedAsync(TItem, string)`. It writes with `CancellationToken.None` inside
its own `try`/`catch`, so a database outage cannot end a worker, keeping the "one bad receipt must
never kill a worker" rule. If that write fails, the row stays in progress and is retried at next
startup rather than being lost.

### Duplicate check at fetch

`MailFetcher` looks for the message ID in `Receipts` and `ReceiptIntakes` before recording. If it finds
one, it flags the message `Seen` and moves on. A filtered unique index on non-null
`ReceiptIntake.SourceMessageId` backs this up.

Duplicates used to burn a full extraction and then fail on the unique index on `Receipt`. A message
with no `Message-ID` can't be matched, and behaves as it does today.

This also closes the crash window between recording and flagging: on the next poll the message is
found, flagged, and recovery handles the row.

### Retry and dismiss endpoints

New `PipelineEndpoints`, under `/api/pipeline`:

| Route | Result | Permission |
|---|---|---|
| `GET /status` | `PipelineStatusDto` | read |
| `GET /failures?page=&pageSize=` | `PagedResult<ReceiptFailureDto>` | read |
| `POST /failures/{id}/retry` | 202 | `EditReceipts` |
| `DELETE /failures/{id}` | 204 | `EditReceipts` |

Retry is a conditional update from `Failed` to `Queued`, clearing `Error` and `FailedAt`, followed by a
non-blocking `TryEnqueue`. If the queue is full, it restores `Failed` and answers 503. A blocking
enqueue would hold an HTTP request open for as long as extraction takes. Zero rows updated means 404
or 409, depending on whether the row exists. Dismiss works the same way with a conditional delete.

`WorkQueue<T>` drops `SingleWriter = true` and gains `TryEnqueue`. `EditReceipts` is the closest
existing permission, since this acts on receipt content. A new permission would be heavier than the
action deserves, and admins hold it implicitly.

### Attribution

`Receipt` gains nullable `AppUserId` (Restrict) and nullable `MailboxId` (SetNull), both copied from
the intake row. Member and mailbox are stored separately, because a mailbox can be deleted or
reassigned while the receipt must keep the person it belonged to when it arrived.

`ReceiptDto` gains `Error`, `MemberId` and `MemberDisplayName` at the end. `ToDto` is client-evaluated,
so both receipt queries need an explicit `Include` of the member (see the `CLAUDE.md` gotcha).
`GET /api/receipts` gains an optional `bool? reconciled`.

### Status is computed from the database

`GET /status` counts `ReceiptIntake` by status and unreconciled receipts, and reads the poll time and
interval. Per-mailbox activity combines a new `Mailbox.LastMessageAt`, written by the fetcher, with a
count of receipts per mailbox for the month.

Asking the channels would give numbers that disappear on restart. `MailboxDto` stays as it is, so the
per-mailbox count doesn't slow down `GET /api/mailboxes`.

The month range comes from a new pure `DanishTime` helper, extracted from the `DanishTimeZone` field in
`PurchaseDate`, which `spending-api` can reuse.

### Keep the cause of a failure

`ExtractWithRetriesAsync` puts the real cause in `InnerException`, and `QueueWorker` passes on only
`ex.Message`, so the stored error would read "Failed to extract any valid data after 3 attempt(s)". A
pure `PipelineError.Describe(Exception)` joins the chain of messages. `Attempts` is written on failure
too, so the row shows how many tries were spent.

## Risks / Trade-offs

- [Mail text now sits in the database and in backups] → It's held only while unresolved, and a failure
  can be dismissed. It's never returned by the API.
- [Failed rows pile up] → Mail in the folder that isn't a receipt stays until dismissed. That's
  deliberate, because silently expiring failures is the bug being fixed. The stage count makes it
  visible.
- [Extra database writes per mail] → A handful of single-row updates, against an extraction that takes
  minutes.
- [Mail without a `Message-ID`] → It can't be deduplicated, so a crash between recording and flagging
  could process it twice. This is no worse than today and very unlikely for store receipts.
- [Same receipt in two members' mailboxes] → It belongs to whichever mailbox was polled first.
  Previously the second copy failed. A forwarded receipt usually has a new `Message-ID` anyway.
- [Retrying right after a restart] → A row can be enqueued twice, which the claim handles.
- [No automated coverage of the transitions] → The conditional updates live in SQL, which `Backend.Tests`
  can't reach, so tasks include manual checks for each path.

## Migration Plan

1. Deploy the migration with the code. Migrations run at startup. It only adds a table and nullable
   columns, so existing rows stay valid and no backfill is needed.
2. Mail held in memory during that one upgrade restart is still lost, as it is today. Mark it unread to
   recover it. From then on restarts are safe.
3. Rollback: redeploy the previous image and reverse the migration. Any unresolved rows are dropped
   with the table, so check the failure list first and mark that mail unread once rolled back.
