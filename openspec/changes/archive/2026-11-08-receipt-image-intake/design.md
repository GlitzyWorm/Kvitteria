# Design

## Context

The pipeline is mail-shaped end to end:

- `ReceiptIntake` holds `SourceMessageId`, `From`, `Subject`, `MailDate`, `BodyText` and `BodySource`.
- `ReceiptRecord` mirrors the intake row in memory.
- `ExtractionWorker` sends `BodyText` to `OllamaClient.ExtractAsync`, with one system prompt tuned on
  Netto Scan&Go mail.
- `ReceiptIngestionService` deletes the intake in the transaction that inserts the receipt.

Everything after the intake row needs only text, so an image source plugs in at two places: recording,
and one extra step before extraction.

The benchmark behind the proposal (see proposal.md, Why) fixes several numbers:

- **Hardware.** `qwen3.5:4b` (vision capable, with a 333 M-parameter projector) on the LXC: 4 cores,
  5 GB RAM, about 6.5 tokens/s generation, `num_thread` 4.
- **Transcription speed.** About 1 000 image tokens per megapixel. Reading the image takes 10–40 s and
  writing the text about 30–40 s.
- **Memory.** With the model loaded, 4.8 of 5 GB is in use. The run at about 1.8 MP hung the guest.
- **Accuracy.**
  - About 0.8 MP read every character correctly. About 0.2 MP read every amount but misread product
    names.
  - The mail prompt, given correct transcriptions, dropped trailing-minus discounts and the first
    product.
  - It returned July 4 (from its own "4. juli 2026" example) for two receipts, and swapped day and
    month on `01 11 2026`.

## Goals / Non-Goals

**Goals:**
- The server cannot be made to hand Ollama an image larger than it can hold, whatever the client does.
- Mail extraction behaves exactly as before: same prompt, same code path.
- An upload is as durable as a mail: recorded before the 202, resumed after a restart, retried and
  dismissed like a failure.
- The image costs one vision call per upload, at most. Retries are text-only.

**Non-Goals:**
- Server-side image processing (resize, rotate, re-encode). The phone does it; the server only checks.
- A general attachment framework for mail. Upload is the only image path in this change.
- Tuning the duplicate rule beyond exact merchant, day and total.

## Decisions

### 1. Transcribe once, store the text, extract from the text

`ExtractionWorker` claims the intake as today. When the intake is an upload with no transcription, it:

1. loads the image;
2. calls a new `OllamaClient.TranscribeAsync`, which sends the image in the message's `images` field
   with no `format`, at temperature 0, using the configured thinking mode, `num_ctx` and `num_thread`;
3. stores the text through a new conditional transition on `ReceiptIntakeStore`
   (`SaveTranscriptionAsync`, valid only while `Extracting`);
4. continues into the unchanged retry loop with the photo prompt.

A stored transcription is reused by the retry loop, by a user retry and by restart recovery.

- *Alternative: one call that extracts JSON straight from the image.* Every reconciliation retry would
  pay for the image again, and there would be no text to inspect. Not benchmarked further.
- *Alternative: OCR with Tesseract.* A new native dependency in the image. It is weak on creased
  thermal paper and loses the label/amount column pairing that the vision model kept.

**No new `ReceiptStatus`.** Transcription happens inside `Extracting`, so the numeric wire enum and every
client stay unchanged. Status counts it as extracting, as the spec says.

**Transcription failures are item failures.** A transport error or timeout fails the intake with the
cause, through `OnFailedAsync` as today, with zero attempts recorded. The user retries it. Wrapping
transcription in the extraction retry budget would multiply the most expensive call.

### 2. "Not a receipt" is a sentinel, checked before extraction

The transcription prompt tells the model to answer exactly `NO_RECEIPT` when the image shows no receipt.
`ReceiptTranscription.IsNoReceipt(text)` is a pure helper: trimmed and case-insensitive, and tolerant of
a trailing full stop. When it matches, the intake fails with a fixed, readable error and no transcription
is stored, so a retry transcribes again. Without the sentinel, a photo of a cat becomes an unreconciled
receipt with invented lines, which is harder to clean up than a failure.

### 3. A second extraction prompt, same schema

`ReceiptExtractionPrompt` gains `PhotoSystemPrompt`, used with the same `Schema` and the same
`ReconciliationHint`. `OllamaClient.ExtractAsync` takes the prompt as a parameter, and `ExtractionWorker`
picks it from the intake's source. The photo prompt:

- **Discounts.** States the trailing-minus discount form (`14,95-`) and the leading-minus form (`-50,00`),
  each as a separate negative line.
- **Pant.** Treats a printed `PANT` line as deposit: it adds to `pant_total` and is not a product. This
  matches what the model already did on the Netto photo and keeps `Reconciliation`'s
  `grand_total = lines + pant` intact.
- **Totals.** Names the totals labels seen on photographed slips: `TOTAL`, `AT BETALE`, `I ALT`,
  `Betalt`.
- **Not products.** Lists what is never a product: payment lines (`DANKORT`, `BETALINGSKORT`,
  `COOP-BETALING`), `BYTTEPENGE`, VAT lines (`MOMS UDGØR`, `HERAF 25%`), loyalty lines (`BONUSKØB`,
  `BONUSKONTO`), bare `0,00` lines, and a summary "Rabat på køb" when per-line discounts are present.
- **Dates.** Spells out `DD MM YY`, `DD MM YYYY`, `DD.MM.YYYY` and `DD-MM-YYYY`, and says the date and
  the time may be printed apart. It uses no concrete example dates, since the mail prompt's example
  leaked into the output.
- **Wording.** Keeps the declarative style the memory notes found works. No worked examples.

The prompt is tuned against the four benchmark images, kept **outside the repository**. They carry a
loyalty-account number, store addresses and a cashier's name, and the repository is public. A
reconciliation is judged over several runs, because temperature-0 CPU inference is not deterministic.

- *Alternative: extend the mail prompt.* The memory notes record how fragile its wording is, and the
  mail receipts reconcile today. A shared prompt would put them at risk for no gain.

### 4. Storage: a `ReceiptImages` table, linked from intake and receipt

- **The table.** `ReceiptImage` has `Id`, `Content` (`bytea`), `Sha256` (32-byte `bytea`, unique index),
  `Width`, `Height` and `CreatedAt`.
- **The links.** `ReceiptIntake.ImageId` and `Receipt.ImageId` are nullable FKs with `Restrict`.
  Ingestion copies the ID from intake to receipt in its existing transaction. Dismiss and receipt
  delete remove the image row explicitly in their transactions.
- **Why a table.** A separate table keeps the blob out of every receipt and intake query (the
  `.Select(ToDto)` gotcha would otherwise drag it into lists). It also keeps one `pg_dump` a complete
  backup.

Identical bytes are rejected through the unique hash. The 409 comes from the insert failing on the unique
index, the way `RecordAsync` already treats `SourceMessageId`, not from a query beforehand. Once a
receipt and its image are deleted, the hash is free again.

- *Alternative: files on a volume.* A second thing to back up and restore in step with the database,
  which is exactly the Data Protection key-ring trap this project already documents.

### 5. `ReceiptIntake` gains a source, and stays one table

- **New columns.** `Source` is an `IntakeSource` enum (`Mail`, `Upload`) stored as its name, following
  the self-describing-values rule. `CapturedAt` is nullable. Existing rows migrate to `Mail`.
- **Upload rows.** An upload sets `Subject` and `From` to empty, `SourceMessageId` to null, and
  `BodySource` to `"image"`. `MailDate` is set to `CapturedAt ?? ReceivedAt`, so
  `PurchaseDate.Resolve`'s existing fallback gives the spec's capture-time fallback with no new code
  path. `BodyText` is empty until transcribed; "has a transcription" means `BodyText != ""` on an upload.
- **The record.** `ReceiptRecord` gains `Source` and `ImageId`. `Describe` in the workers uses the
  subject for mail and `upload <id>` for an upload, so logs stay readable.

- *Alternative: a separate upload table.* Every transition in `ReceiptIntakeStore`, recovery, the status
  counts and failure listing would be written twice.

### 6. Size budget enforced by header inspection, not decoding

`ReceiptImageLimits` holds the limits, which are the same numbers the app uses:

- at most 1 000 000 pixels;
- at most 2 MB;
- JPEG only.

`JpegHeader.TryRead` is a pure parser. It requires the SOI marker, walks segments to the first SOF0–SOF15
frame (excluding DHT, JPG and DAC) for width and height, and requires an EOI marker at the end. It is
enough to refuse oversize and truncated files without decoding pixels on a 5 GB machine.

The endpoint carries `RequestSizeLimit` just above 2 MB, so an oversize body is cut off by Kestrel
before it is buffered.

- *Alternative: ImageSharp to decode and resize server-side.* A new dependency under a split licence.
  Decoding a hostile image is itself a memory risk, and the only client already prepares the image.
- **Why 1 MP.** The tested 768×1024 (0.79 MP) was accurate and safe. 1 MP leaves headroom for tall
  screenshots: a 1080×2400 screen lands at about 670×1490, wider than the 502 px a 1024-px long-edge
  cap would give.

### 7. Refusing uploads when the model can't see

`OllamaClient.GetCapabilitiesAsync(model)` reads `capabilities` from `/api/show`. The upload endpoint
checks it per request: one cheap local call, with no cache to go stale when an admin changes the model.

- **The model answers without `vision`:** 409 naming the model.
- **Ollama doesn't answer:** the upload is accepted. The intake is durable and the pipeline already
  handles an unavailable Ollama, while refusing would lose the upload.

### 8. Upload endpoint shape

`POST /api/receipts/uploads` takes `multipart/form-data` with:

- `image`, the JPEG file;
- `capturedAt`, an optional ISO-8601 instant.

Gated by `PermissionPolicies.Name(Permission.AddReceipts)`, with `.DisableAntiforgery()`: auth is a
bearer token, so there is no cookie to forge.

The order is:

1. validate;
2. check vision;
3. insert image and intake in one transaction;
4. `ExtractionQueue.TryEnqueue`.

If the queue refuses, the handler deletes both rows and answers 503, mirroring the retry endpoint, so a
refused upload leaves nothing behind. Success answers 202 with `ReceiptUploadDto(Guid IntakeId)`.

`GET /api/receipts/{id}/image` returns `Results.File(bytes, "image/jpeg")` with
`Cache-Control: private, max-age=31536000, immutable`, since an image never changes under its receipt.
It's an ungated read like every other read.

### 9. Duplicate flag at ingestion

`Receipt.PossibleDuplicateOfId` is a nullable self-FK with `SetNull`, which is what makes "the flag does
not outlive its match" structural.

`ReceiptIngestionService` resolves the merchant, computes the purchase date, then queries for receipts
with:

- the same `MerchantId`;
- the same `GrandTotal`;
- a `PurchaseDate` inside `DanishTime.DayRangeUtc(purchaseDate)`, a new pure helper beside
  `MonthRangeUtc`.

It takes the earliest by `CreatedAt`, then `Id`. The keys are version 7 GUIDs, but Npgsql generates
them with `Guid.CreateVersion7`, whose bits below the millisecond are random: fifty keys generated in a
loop came out of order. So `Receipt.CreatedAt` was added in this migration, set at ingestion. Receipts
stored before it share the migration's `now()` and fall back to the key, which is ordered across
ingestions minutes apart. The rule applies to every source, so a mail receipt and a photo of the same
purchase are caught as well.

### 10. Receipt delete

`DELETE /api/receipts/{id}`, gated by `EditReceipts`, runs one transaction:

- load the receipt;
- remove its image row if any;
- remove the receipt (lines and meal coverage already cascade; duplicate flags are nulled by the FK).

Aliases, canonical items and merchants are untouched.

A deleted mail receipt's message ID is no longer stored anywhere, so marking the mail unread re-imports
it. That is a usable way to re-extract a mail receipt, and the spec records it rather than adding a
tombstone table.

### 11. Permission `AddReceipts`

The value is appended to `Permission`. The migration adds `AddReceipts` to the existing
`HouseholdSettings.MemberPermissions` array when absent, and `DatabaseSeeder.DefaultMemberPermissions`
includes it for new households. Admins hold it implicitly, as with every permission. It is separate from
`EditReceipts` because adding a receipt from your own phone is ordinary household use, while editing
extracted figures is correction work.

### 12. Contracts

Two DTOs gain fields:

- **`ReceiptDto`:** `bool HasImage`, `Guid? PossibleDuplicateOfId`.
- **`ReceiptFailureDto`:** `string Source` (`Mail`/`Upload`), `DateTime ReceivedAt`,
  `bool HasTranscription`. For uploads `Subject` and `From` are empty and `MailDate` is the capture time.

`ReceiptUploadDto` is new. All changes are additive: the OpenAPI document is regenerated, and the app's
wire models and `ContractTest` list are updated with it.

### 13. Android: `feature/capture`, preparation in data, pending capture in memory

- **Modules.** `feature/capture/{domain,data,presentation}`, following the convention for a feature that
  owns data.
  - **domain:** `PreparedImage(bytes, width, height, capturedAt)`, a `ReceiptUploader` interface, and
    an `ImagePreparer` interface.
  - **data:**
    - `KtorReceiptUploader`, which uses `submitFormWithBinaryData` on `ApiHttpClient`.
    - `AndroidImagePreparer`, which reads the URI once and decodes bounds first. It decodes with an
      `inSampleSize` that keeps the result above the budget, applies `ExifInterface` orientation,
      scales to ≤ 1 MP with `Bitmap.createScaledBitmap`, and compresses to JPEG at quality 85.
      `Bitmap.compress` writes no EXIF, which is what strips GPS. `capturedAt` comes from EXIF
      `DateTimeOriginal` when present.
  - **presentation:**
    - The add-receipt sheet.
    - The confirm screen, which is MVI like every screen and has upload and discard.
    - `PendingCapture`, an app-scoped in-memory holder like `InMemorySkippedLines`. Routes carry IDs
      only, so the prepared image never travels in a route.
- **Scanner.** `GmsDocumentScanning` with `RESULT_FORMAT_JPEG`, page limit 1, full mode, launched
  through `StartIntentSenderForResult`. No camera permission is declared, which the scanner doesn't
  need. "Unavailable" is `GoogleApiAvailability` not reporting success, or `getStartScanIntent`
  failing. Either hides scan.
- **Picker.** `PickVisualMedia(ImageOnly)`: no storage permission.
- **Share.**
  - An `ACTION_SEND` / `image/*` intent filter on `MainActivity`. `onCreate` and `onNewIntent` hand the
    URI to preparation at once, because the read grant lasts only as long as the activity, and put the
    result in `PendingCapture`.
  - `KvitteriaRoot` opens the confirm route on the current tab once it reaches `MainRoute`. That covers
    the signed-out case without the gate knowing about captures.
  - Without `AddReceipts` it clears the capture with a message.
- **Entry points.** A plus action on Oversigt's profile bar and on Kvitteringer's top bar, using
  Phosphor `Plus`, `Scan` and `Image` glyphs and `Kv*` components only.
- **Detail image.** `ReceiptRepository.image(id)` fetches bytes through `ApiHttpClient` and decodes them
  off the main thread; this is the path that carries the bearer token. A thumbnail opens a full-screen
  viewer using `Modifier.transformable` for zoom and pan. No image library: one JPEG per screen doesn't
  justify one, the same reasoning as the charts.
- **Delete** sits on detail behind a confirmation dialog, and invalidates the in-memory receipt cache.
- **Strings** are added in Danish (`values/`) and English (`values-en/`), including the `AddReceipts`
  label and description that Rettigheder lists generically.
- **The ML Kit dependency** comes from Google Play services under Google's terms. It is not GPL/AGPL,
  so the licensing rule holds, and AboutLibraries lists it with no manual edit.

## Risks / Trade-offs

- **[Memory headroom on the LXC is thin: 4.8 of 5 GB with the model loaded]** → Hard 1 MP limit on the
  server, and one worker means one image in flight. `docs/configuration.md` states the RAM the vision
  path needs.
- **[The photo prompt is tuned on four receipts and may overfit]** → It is kept declarative with no
  examples. Unreconciled results stay usable receipts, and the image is now kept, so hand correction is
  practical. More samples can be added to the local evaluation set over time.
- **[Merchant names differ by source ("SuperBrugsen" by mail, "Super Brugsen" by photo), so duplicates
  slip through]** → Accepted for now. The rule is exact on purpose: a false flag is noise, a missed flag
  is the status quo.
- **[The model is switched to a text-only one while uploads are queued]** → Those intakes fail at
  transcription with a cause naming the model's missing capability, and can be retried after switching
  back.
- **[Devices without Play services have no scanner]** → The photo picker and share still work. The
  system camera saves into the gallery, so the picker can reach a camera photo.
- **[The upload stores the image before anyone checks it is a receipt]** → The `NO_RECEIPT` failure
  keeps it visible in Problems, where dismiss deletes it.
- **[Database growth of about 100 KB per uploaded receipt]** → Negligible at household scale. Noted in
  `docs/configuration.md` beside backups.

## Migration Plan

- **One EF migration, applied at startup as usual.** It adds:
  - the `ReceiptImages` table;
  - `ReceiptIntakes.Source` (default `'Mail'`), `CapturedAt` and `ImageId`;
  - `Receipts.ImageId` and `PossibleDuplicateOfId`;
  - `AddReceipts` on `HouseholdSettings`.
- **Existing receipts** are not back-filled with duplicate flags; the rule applies from the next
  ingestion.
- **Rollback.** Because the migration history changes, `scripts/update.sh` treats a failed update as
  `restore-required`, as it does for any schema change. Rolling back means restoring the dump taken
  before the update.
- **Clients.** The app release that adds capture needs this backend:
  - older servers answer 404 on the upload route, and the app shows the server's message;
  - an older app ignores the new DTO fields.

## Open Questions

- **JPEG quality.** 85 is a guess. A lower value would shrink storage, and whether it costs accuracy can
  be measured with the local evaluation set later without changing anything else.
