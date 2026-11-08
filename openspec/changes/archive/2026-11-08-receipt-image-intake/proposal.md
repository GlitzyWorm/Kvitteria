# Proposal

## Why

Receipts only reach Kvitteria as receipt emails today, but many purchases leave nothing but a paper slip
or a screen in a store's app (the Coop app's "kassebon", for one). The household can't record those
without typing them in. The configured model, `qwen3.5:4b`, can read images. A benchmark on the home LXC
found it transcribes real Netto, Føtex and SuperBrugsen receipts and app screenshots accurately in 50–85 s
per image, faster than one text extraction attempt. Adding an image source now is cheap.

The same benchmark showed what must not be naive:

- At about 1.8 MP the LXC ran out of memory and hung, so image size has to be capped before Ollama
  sees it.
- The email-tuned extraction prompt reconciled only 2 of 4 transcriptions. It dropped discount lines
  with a trailing minus (`14,95-`), dropped the first product, and read dates wrong: `26 10 26`,
  `01 11 2026`, and July 4 taken from the prompt's own example. Photos need their own prompt so the
  email receipts that work today stay untouched.

## What Changes

- **Upload endpoint.** A member can upload a receipt image to the server. The server checks the image
  against a fixed size budget, records it durably, and answers as soon as it is queued.
- **Transcribe, then extract.** The pipeline gains a transcription step for images. The model writes
  the receipt out as plain text once, and that text is stored on the intake row. Extraction then runs
  on that text with its own prompt for photographed and screenshotted receipts, through the existing
  retry and reconciliation loop. A retry reuses the stored text instead of reading the image again.
- **Not a receipt.** An image the model says is not a receipt fails with a clear reason. It is not
  stored as a junk receipt.
- **Image kept.** The image stays with the receipt it became and can be fetched from the receipt.
  Unlike mail text, it is kept, because it is what someone checks when correcting an unreconciled
  receipt.
- **Duplicates flagged, not rejected.** An uploaded image has no Message-Id. A receipt from any source
  is flagged when a stored receipt already has the same merchant, the same Danish calendar day and the
  same total. The household decides, so photographing the paper slip and screenshotting the app for
  the same purchase gives one receipt plus a visible flag. A byte-identical image that is still in
  progress or already stored is rejected outright.
- **Receipt deletion.** A member with `EditReceipts` can delete a receipt. This is needed to act on a
  duplicate flag, and to remove a photo that never should have been sent.
- **New permission `AddReceipts`.** It gates uploads, and every household's members get it by default.
- **Android app.** The app gains three ways in:
  - photographing a paper receipt with the ML Kit document scanner, which crops and straightens it;
  - picking an existing image with the system photo picker;
  - sharing an image into Kvitteria from another app.

  Every image is downscaled to the budget and re-encoded as JPEG on the phone before upload, which
  also strips location metadata.
- **Pipeline and receipt screens.** Both show image-sourced work: failures name their source, receipt
  detail shows the image, and a possible duplicate is marked with a link to the receipt it matches.
- **Out of scope:**
  - image attachments on receipt emails;
  - PDF receipts;
  - a receipt spanning several photos;
  - extracting straight from the image in one call (the benchmark favoured transcribing first).

## Capabilities

### New Capabilities

- `pipeline/image-intake`: accepting a receipt image, the size budget, transcription, the "not a
  receipt" outcome, and the byte-identical duplicate rule.
- `receipts/receipt-images`: the image kept with the receipt it became, and how it is fetched.
- `receipts/duplicate-detection`: flagging a receipt that matches a stored one on merchant, day and
  total.
- `receipts/deletion`: deleting a receipt and everything that belongs only to it.
- `mobile/receipt-capture`: scanning, picking and sharing an image into the app, downscaling it, and
  uploading it.

### Modified Capabilities

- `pipeline/durable-intake`: recording, resuming and resolving cover uploaded images as well as mail,
  and the stored transcription follows the same delete-on-resolve rule as mail text.
- `pipeline/failure-tracking`: a failure says which source it came from, and an upload's entry carries
  upload details in place of subject and sender.
- `pipeline/status`: stage counts include uploads.
- `mobile/receipts`: detail shows the receipt's image and a possible-duplicate mark, and offers delete
  with `EditReceipts`.
- `mobile/pipeline`: failures from uploads are listed by their source rather than as mail.

## Impact

- **Backend.**
  - New code: an upload endpoint, `ReceiptImage` storage, a transcription call in `OllamaClient`, and a
    second extraction prompt.
  - Changed: `ExtractionWorker` gains the transcription branch, `ReceiptIngestionService` moves the
    image and flags duplicates, and `ReceiptIntakeStore` records uploads.
  - New endpoints: image download and receipt delete.
- **Database.** A migration adds:
  - a receipt-image table;
  - a source kind and an image link on `ReceiptIntake`;
  - an image link and a possible-duplicate link on `Receipt`;
  - `AddReceipts` on the existing `HouseholdSettings.MemberPermissions`.

  Existing rows stay valid, with mail as their source and no image.
- **Contracts.** New DTOs for the upload result. Additive fields on `ReceiptDto` and
  `ReceiptFailureDto`. `docs/openapi/kvitteria.json` is regenerated.
- **Android.**
  - A new `feature/capture` module.
  - An `ACTION_SEND image/*` intent filter.
  - ML Kit document scanner through Play services. It is closed source, which the licence rule
    allows, and the AboutLibraries screen picks it up.
  - Image display on receipt detail, and receipt delete.
- **Storage.** About 100 KB per image-sourced receipt, in Postgres, so one `pg_dump` remains a complete
  backup.
- **Operations.** Image work needs a model with vision. With a text-only model, uploads are refused up
  front and nothing else changes.
