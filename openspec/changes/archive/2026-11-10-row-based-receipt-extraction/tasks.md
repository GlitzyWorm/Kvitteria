# Tasks

## 1. Reading amounts, dates and chains

- [x] 1.1 Add `DanishAmount.TryParse` in `Features/Extraction`. It reads a decimal comma, an optional thousands
      dot, and a leading or trailing minus, and rejects anything else. Verify with `DanishAmountTests` covering
      `29,95`, `14,95-`, `-50,00`, `1.234,50`, `""`, `2 x 10,05` and `29`.
- [x] 1.2 Add `PrintedDate.Find(text)`. It recognizes the written-month form and the `DD<sep>MM<sep>YY(YY)`
      form with an optional time, scans from every starting position, checks ranges and calendar validity, and
      prefers the first match that has a time. Verify with `PrintedDateTests` covering
      `11 1 462 26 10 26 13:07`, `13 1 96 08 11 26 09:05`, `31.10.2026 11:40`, the mail pair
      `9. november 2026 - 13:33` / `09-11-2026 13:33:17`, `01 11 2026` without a time, `31 02 26`, and a text
      with no date.
- [x] 1.3 Add `MerchantChains.Match(text)` with the chain list and the letter-and-digit boundary rule from
      design decision 5. Verify with `MerchantChainsTests` covering:
      - `Netto Scan&Go` → `Netto`;
      - `Super Brugsen` and `SUPERBRUGSEN BRØNDBY` → `SuperBrugsen`;
      - `www.føtex.dk` above `SALLING RISNUDLER` → `Føtex`;
      - `COOP BONUSKONTO` alone → no match;
      - `Du har sparet` → no match;
      - an unknown shop → no match.
- [x] 1.4 Add the plausibility window to `PurchaseDate.Resolve`: fall back when the date is more than one day
      after the source time or more than 365 days before it. Verify with new cases in `PurchaseDateTests` (2024
      against a 2026 source, two weeks before, 30 hours after) while the existing cases still pass.
- [x] 1.5 Add the test classes to the `Backend.Tests` list in `CLAUDE.md`, and verify that
      `dotnet test Backend.Tests/Backend.Tests.csproj` passes.

## 2. Mapping labelled rows

- [x] 2.1 Add the model-answer records (`merchant`, `currency`, `rows[] {text, amount, quantity, kind}`,
      `grand_total`) and `ReceiptRows.ToExtracted(answer, text)`, following design decision 1. That covers kinds
      kept and dropped, forced-negative discounts, deposit before `deposit_total`, absolute grand total, unit
      price = amount ÷ quantity, quantity defaulting to 1, `PrintedDate` and `MerchantChains` with the model's
      merchant as fallback, and `OllamaResponseException` for an unparseable amount, an unknown kind or a
      quantity-only product or discount row.
- [x] 2.2 Verify 2.1 with `ReceiptRowsTests` built from the evaluated receipts' rows, rewritten without personal
      data:
      - the Netto photo with three RABAT and a PANT (lines 68.00, pant 1.50, total 69.50);
      - the SuperBrugsen kassebon (`I alt -108,95` stays a payment, total 108.95);
      - the mail totals block (pant from `deposit_total` 0,00);
      - a receipt with both `PANT` rows and `HERAF PANT IALT` (pant counted once);
      - the `2 x 12,00` row that carries a discount amount (throws).

## 3. One extraction prompt

- [x] 3.1 Replace `SystemPrompt`, `PhotoSystemPrompt` and the old schema in `ReceiptExtractionPrompt` with
      `ExtractionPrompt` and the row schema: patterned `amount` and `grand_total`, `kind` enum, `rows` before
      `grand_total`. Start from the evaluated prompt and add `deposit_total`, the merchant rule and the
      quantity-line rule. Reword `ReconciliationHint` as design decision 6 describes. Keep `TranscriptionPrompt`
      unchanged. Verify that the backend builds with `TreatWarningsAsErrors`.
- [x] 3.2 Have `OllamaClient.ExtractAsync` deserialize the row answer and return `ReceiptRows.ToExtracted`, so a
      mapping failure surfaces as `OllamaResponseException`. Remove the prompt choice from `ExtractionWorker`
      and keep transcription for uploads. Verify the build and that the retry loop is unchanged in the diff.
- [x] 3.3 Update `CLAUDE.md`:
      - Image intake: one prompt, transcription unchanged;
      - Pipeline architecture step 2, and the paragraph on tuned prompt behaviour, with what the evaluation
        learned: classify rather than filter, patterned amount strings, dates and chains in code;
      - `PurchaseDate`'s new fallback.
      Also update `docs/troubleshooting.md` if it describes extraction rules. Verify that no doc still names
      `SystemPrompt` or `PhotoSystemPrompt` (`grep`).

## 4. Evaluation tool

- [x] 4.1 Add `Kvitteria/Backend.Eval`, a console project in `Kvitteria.slnx` referencing `Backend`, with
      `InternalsVisibleTo` extended to it. Share the `/api/chat` request records with `OllamaClient` instead of
      copying them. Verify that `dotnet build Kvitteria.slnx` passes and `test.yml` is unchanged.
- [x] 4.2 Implement the sample loop from design decision 7:
      - `.txt`, `.eml`/`.html` through `HtmlToPlainText`, and `.jpg` transcribed once and cached, with the
        `ReceiptImageLimits` check;
      - extraction with the production defaults and retry loop;
      - comparison with `<name>.expected.json`;
      - options for runs, Ollama URL and `num_thread`.
      Verify against one synthetic `.txt` sample and its expected file.
- [x] 4.3 Document the tool in `docs/development.md`. Cover the sample layout and the expected-file format, why
      samples stay outside the repository, the GPU container for iterating, and the CPU container limited to
      4 cores (`--cpus=4 --cpuset-cpus=0-3`, `num_thread` 4) for acceptance. Verify the documented commands run
      as written.

## 5. Acceptance

- [ ] 5.1 Assemble the sample folder outside the repository: the five evaluated images with expected files, and
      real mails exported as `.eml`. The mails need several Netto Scan&Go (at least one with pant above 0) and
      any other chain available, which the household provides. Verify that every sample has an expected file.
- [ ] 5.2 Run the tool on the CPU container limited to 4 cores, three runs per sample. Accept when every image
      and every mail is fully right in at least two of three runs, and no mail sample does worse than today's
      `SystemPrompt` on the same text. Use the lab script or the previous release for that comparison. Record
      the results in this change's design.
- [ ] 5.3 Run one mail and one upload end to end on the throwaway e2e stack, not the dev database or the LXC.
      Verify on `GET /api/receipts/{id}` that the merchant, purchase date, total, pant and lines are as
      expected and the receipt is `Reconciled`.

## Workflow follow-up

- Update the memory notes on extraction findings with the evaluation's lessons.
- Archive the change once it has shipped in a backend release.
