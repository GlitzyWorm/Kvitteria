# Proposal

## Why

Extraction is tuned for Netto Scan&Go mail, and photos and screenshots of other stores' receipts often come out
wrong. A local run of the current pipeline on five real photos and screenshots (Netto, Føtex, SuperBrugsen) got a
wrong purchase date on four of them. Each wrong date was a well-formed date two years off, which is accepted
silently and files the receipt under the wrong month. Two of the five also dropped discount lines or a product
on every attempt.

The vision transcriptions themselves were nearly perfect, so all of these failures happen when the text becomes
JSON. On the same texts, a different schema got every figure right on the first attempt. In it the model labels
every line that has an amount and copies each amount exactly as printed. Code then decides which labelled
lines count, does the sums, and finds the date and the store.

## What Changes

- **BREAKING (internal)**: mail and uploads share one extraction prompt and one model schema. `SystemPrompt`
  and `PhotoSystemPrompt` are replaced by a single prompt. Transcribing an upload's image is unchanged.
- The model lists every line that carries an amount, with its text, the amount as printed, its quantity and a
  kind: `product`, `discount`, `deposit`, `total`, `payment`, `vat`, `loyalty` or `other`. It also gives the
  amount paid as printed. A schema pattern restricts each amount to a printed Danish amount such as `29,95`,
  `14,95-` or `-50,00`.
- Code turns the labelled lines into the existing extracted receipt:
  - product and discount lines become receipt lines, and a discount is always negative;
  - deposit lines sum to the pant;
  - unit price is the total divided by the quantity.
  An amount that does not parse counts as an unusable answer and is retried the same way malformed JSON is.
- The purchase date is read by code from the receipt text, not by the model. Code recognizes the printed
  Danish forms (`26 10 26 13:07`, `31.10.2026 11:40`, `09-11-2026 13:33:17`, `9. november 2026 - 13:33`). A
  date that is missing, or implausible against the mail or capture time, falls back to that time.
- The merchant is matched by code against a list of Danish grocery chains found in the text, with one spelling
  per chain (`SuperBrugsen`, never `Super Brugsen`). The model's answer is used only when no chain matches.
- The reconciliation hint sent on a retry is reworded for the new schema. The rules for reconciliation,
  retrying and keeping the best attempt are unchanged.
- A prompt evaluation script is added, which reads receipts from a folder outside the repository. The sample
  receipts stay out of the public repository.

## Capabilities

### New Capabilities
- `pipeline/receipt-extraction`: how recorded receipt text, from a mail or a transcribed image, becomes a
  merchant, purchase date, total, pant and lines. This includes which printed lines count as lines and pant,
  how printed amounts are read, and where the date and merchant come from.

### Modified Capabilities
- `pipeline/image-intake`: images are no longer extracted with their own instructions. After transcription
  they go through the shared extraction. The date fallback also covers a date that is missing or implausible,
  not only one that cannot be parsed.

## Impact

- `Backend/Features/Extraction/LLM/ReceiptExtractionPrompt.cs`: one prompt and the new schema replace
  `SystemPrompt`, `PhotoSystemPrompt` and the old schema; `TranscriptionPrompt` is kept.
- `ExtractionWorker` and `OllamaClient.ExtractAsync`: no prompt choice by source; the model's answer is mapped
  to `ExtractedReceipt` before reconciliation.
- New pure helpers in `Features/Extraction`, covered by `Backend.Tests`: reading Danish amounts, mapping
  labelled lines, finding the printed date, and matching the chain.
- `PurchaseDate.Resolve`: plausibility check against the mail or capture time.
- `ExtractedReceipt`, `ReceiptIngestionService`, the database and the HTTP API: unchanged. No migration and no
  OpenAPI change.
- Existing merchants spelled differently from the chain list (such as `Super Brugsen`) are left as they are.
  New receipts attach to the chain's spelling.
- `CLAUDE.md`, the memory of what the old prompt learned, and `docs/troubleshooting.md` where they describe
  the prompts.
