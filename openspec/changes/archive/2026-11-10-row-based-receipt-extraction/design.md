# Design

## Context

What happens today:

- `ExtractionWorker` sends the recorded text to `OllamaClient.ExtractAsync` with one of two prompts:
  `SystemPrompt` for mail or `PhotoSystemPrompt` for a transcribed upload. The schema asks for the merchant, a
  formatted `purchase_date`, `grand_total`, `pant_total` and `line_items`, all as JSON numbers and strings
  the model fills in.
- `ExtractedReceipt` goes to the retry loop, which reconciles `grand_total − Σ line_total − pant_total`. It
  then goes to `ReceiptIngestionService`. That service resolves the merchant by name and calls
  `PurchaseDate.Resolve(extracted.PurchaseDate, record.Date, …)`, which falls back to the mail or capture time
  only when the string doesn't parse.
- `TranscriptionPrompt` turns an image into text once, and the text is stored.

A local evaluation of the current prompts motivated this change (see proposal.md, Why). It ran on the same
`qwen3.5:4b` model as the LXC, on GPU and on a 4-core CPU container, over five real photos and screenshots and
one Netto Scan&Go mail. The numbers behind the decisions below come from that run:

| Variant | 5 images | Mail |
|---|---|---|
| Today's prompts | 1 fully right; 4 wrong dates, 2 drop lines on every attempt | right |
| Classified rows, amounts as numbers | amounts right | every amount cut to whole kroner |
| Classified rows, amounts as free strings | 2 images return `2 x 10,05` or `""` as amounts | right |
| Classified rows, amounts as patterned strings | all right, first attempt (GPU); 11/12 runs (CPU) | right |

In every variant the model's own `purchase_date` was wrong whenever the receipt printed register numbers in
front of the date. A regex over the text found every printed date correctly.

## Goals / Non-Goals

**Goals:**
- One extraction path for every source, so receipts from other stores, by mail or by photo, are read by the
  same rules.
- Move everything a 4B model gets wrong but a few lines of code get right out of the model: arithmetic, signs,
  dates and the chain name.
- Keep `ExtractedReceipt`, the retry loop, reconciliation and ingestion exactly as they are.

**Non-Goals:**
- Changing transcription. Its output was nearly perfect on GPU and on CPU.
- Renaming existing merchant rows or merging receipts already stored under another spelling.
- Combining a printed time with the capture date when the receipt prints a time but no date (Føtex `10:13`).
  The capture time is used whole.
- A thinking mode, another model, or a bigger context. The LXC's 5 GB rules them out today.

## Decisions

### 1. The model labels every line that has an amount; code decides what counts

The model schema becomes:

```
merchant     string
currency     string
rows[]       { text, amount, quantity, kind }
grand_total  string
```

`kind` is one of `product`, `discount`, `deposit`, `deposit_total`, `total`, `payment`, `vat`, `loyalty`,
`other`. A pure mapper (`ReceiptRows`, in `Features/Extraction`) builds `ExtractedReceipt` from this answer:

- `product` and `discount` rows become line items in order. A discount's amount is forced negative. Quantity
  defaults to 1 when it is zero or missing, unit price is amount ÷ quantity rounded to two decimals, and the
  unit is `stk`. The unit is not stored anywhere today.
- `pant_total` is the sum of `deposit` rows. When there are none, it is the first `deposit_total` row
  (`HERAF PANT IALT`), and otherwise 0. The split stops a receipt that prints both `PANT` lines and a
  `HERAF PANT IALT` from counting the deposit twice.
- `grand_total` is the absolute value of the parsed paid amount, because `I alt -108,95 DKK` is printed
  negative after a payment.
- All other kinds are dropped.

`rows` comes before `grand_total` in the schema, so the model writes every line before it states the total.

*Why:* told to leave out what isn't a product, the model also left out discounts and the first product. Told
to label everything, it kept them. Labelling is something a small model does well. The rules about what
counts are then code, tested in `Backend.Tests`, instead of prompt wording that has proved fragile.

*Alternative:* tune `PhotoSystemPrompt` further. The baseline shows it failing on four of five images. The
history of the mail prompt shows that each wording fix tends to break another receipt.

### 2. Amounts are strings held to a printed-amount pattern

`amount` and `grand_total` are JSON strings with `"pattern": "^-?[0-9]{1,3}(\\.?[0-9]{3})?,[0-9]{2}-?$"`.
That covers `29,95`, `14,95-`, `-50,00` and `1.234,50`. Ollama turns the schema into a grammar, so the model
cannot write anything else. `DanishAmount.TryParse` in code then strips the thousands dot, reads the comma as
the decimal point and applies a leading or trailing minus.

*Why:* with numbers, the mail's `29,95` came out as `29`. JSON doesn't allow a comma in a number, so the model
stopped at it. With free strings, the model copied quantity lines or left amounts empty. Only the pattern was
right in every case. The thousands group was not in the evaluated pattern; the evaluation reruns with it
(see tasks).

*Fallback:* an Ollama that ignores `pattern` still returns strings. Anything `TryParse` rejects throws
`OllamaResponseException`, which the retry loop already handles: the attempt counts, and the next attempt gets
the malformed-answer hint.

### 3. A row that is only a quantity is an unusable answer

On CPU, one transcription put `2 x 12,00` on a line of its own. The model then attached the next `RABAT`'s
amount to it as a discount. The prompt says the quantity line belongs to the product above it. The mapper also
rejects any `product` or `discount` row whose text is nothing but a quantity expression (`2 x 12,00`,
`3 stk`), throwing `OllamaResponseException` so the attempt is retried. A retry at the retry temperature fixed
the equivalent case in the evaluation.

*Alternative:* rewrite the text before the model sees it, joining a lone `N x price` line to its neighbour.
Which neighbour is right depends on the layout: the amount sits on the name's line in one transcription and on
the quantity's line in another. That is a guess in code that's hard to test against layouts nobody has seen yet.

### 4. The purchase date comes from the text, not the model

`PrintedDate.Find(text)` scans the recorded text, mail body or transcription, for these forms:

- `D. <Danish month name> YYYY`, with an optional time after up to a few separator characters;
- `DD<sep>MM<sep>YY(YY)`, where the separator is `.`, `-` or a space and is the same both times, with an
  optional `HH:MM(:SS)` after up to four spaces.

The scan tries every starting position, not only the next one after a rejected match. Otherwise the register
numbers in `13 1 96 08 11 26 09:05` swallow the real date. Day 1–31 and month 1–12 are checked, and an invalid
calendar date is skipped. The first match that has a time wins, otherwise the first match. The mapper writes
it into `ExtractedReceipt.PurchaseDate` as `yyyy-MM-dd HH:mm:ss`, or `""` when nothing was found. `""` already
makes `Resolve` fall back.

`PurchaseDate.Resolve` gains the plausibility window from the spec: it falls back when the parsed date is more
than one day after `record.Date` or more than 365 days before it. That catches a wrong date from any future
code path, not only this one.

*Why not have the model copy the date:* in the evaluation, a `date_as_printed` field was copied correctly but
once also took in the address line above it. A regex over the text is never worse than a regex over the
model's copy of the text, and it makes no model call.

*Why the window is wide:* a photo can be taken weeks after the purchase. Wrong dates showed up as 2024 and 1996
against a 2026 source.

### 5. The merchant is matched against known chains, then falls back to the model

`MerchantChains.Match(text)` holds one spelling per chain, each with the forms it is printed in:

- `Netto`; `Rema 1000` (`rema 1000`, `rema1000`); `Føtex`; `Bilka`; `Lidl`; `Coop 365` (`coop 365`,
  `365discount`); `Kvickly`; `SuperBrugsen` (`superbrugsen`, `super brugsen`);
- `Dagli'Brugsen`; `Lokal Brugsen`; `Meny`; `Spar`; `Løvbjerg`.

Matching lowercases with the invariant culture. A form matches only with no letter (`a–z`, `æ`, `ø`, `å`) or
digit right before or after it, so `sparet` never reads as `Spar`. The form earliest in the text wins.
`Salling` is deliberately absent: it is printed as a product brand (`SALLING RISNUDLER`) on Føtex receipts.
`Coop` alone is absent because `COOP BONUSKONTO` is printed on SuperBrugsen receipts.

With no match, the model's `merchant` is used. The prompt tells the model to leave out the address, CVR and
words such as `Scan&Go`, which the old mail prompt removed.

*Why:* the one merchant-wording change tried in the evaluation knocked a product out of another receipt.
Matching on the chain list left the prompt alone and got all six texts right. It also fixes the
`SuperBrugsen` / `Super Brugsen` split that the image-intake design flagged as a duplicate-detection risk.

### 6. One prompt, no source-specific path

`ReceiptExtractionPrompt.SystemPrompt` and `PhotoSystemPrompt` are replaced by `ExtractionPrompt`, and
`Schema` by the row schema. The evaluated prompt (`kinds4` in the lab) is the starting text, plus three
things it lacked: the `deposit_total` kind, the merchant rule from decision 5, and the quantity-line rule from
decision 3. `ExtractionWorker` loses its prompt choice; uploads still transcribe first. `OllamaClient.ExtractAsync`
keeps its prompt parameter, and maps the answer through `ReceiptRows` before returning, so the worker still
receives an `ExtractedReceipt`. The mapper needs the recorded text for the date and the chain, so
`ExtractAsync` already has it.

`ReconciliationHint` is reworded. It no longer talks about the first product after the header. Instead it asks
the model to list every line that has an amount exactly once and to copy each amount as printed. Its numbers
are the same.

*Alternative:* keep the mail prompt for mail and use rows only for uploads. The evaluation shows the row
prompt right on the mail. Two prompts are the reason a receipt from another store's mail would meet rules
tuned for Netto Scan&Go.

*Alternative:* fall back to the old mail prompt when rows don't reconcile. That's two paths and double the
attempts on the receipts that are already hardest. No.

### 7. An evaluation tool that runs the real code

`Kvitteria/Backend.Eval`, a console project in the solution and not in `test.yml`, takes a folder and an
Ollama URL. For each sample it reads the text:

- `.txt` as is;
- `.eml` and `.html` through `HtmlToPlainText`;
- `.jpg` through `TranscriptionPrompt` once, caching the text beside the image and refusing a JPEG over the
  `ReceiptImageLimits` budget.

It then runs the extraction prompt, schema, mapper, date and chain code with the production defaults
(temperature 0, then the retry temperature, three attempts, `num_ctx` 8192, no thinking). It compares the
result with an `<name>.expected.json` beside the sample (merchant, purchase date, total, pant, line count) and
prints one line per run, with an option for several runs and `num_thread`.

It sends its own `/api/chat` requests from the same constants, because `OllamaClient` reads its settings from
the database. The request record and options are made `internal` and shared, so the two can't drift.
`InternalsVisibleTo` is extended to the eval project.

The samples stay outside the repository, and the README of the tool says so.

*Alternative:* the Python lab script used for the evaluation. It reimplements the mapper and the date regex,
so it would measure a copy of the code instead of the code.

## Risks / Trade-offs

- **[The mail evidence is one Netto Scan&Go receipt]** → Before the prompt switch merges, the evaluation must
  cover a handful of real mails: several Netto, one with pant, and any other chain available, exported as
  `.eml` into the sample folder. They must do at least as well as with today's `SystemPrompt`. A task gates
  on it.
- **[The `deposit_total` kind is new and unevaluated]** → It is the only way a pant shows up on a Netto mail
  (`HERAF PANT IALT`), so the mail samples must include one with pant above 0.
- **[A printed date that isn't the purchase date]**, such as an expiry date on a voucher printed after the
  receipt → First-with-time keeps the receipt's own timestamp ahead of a bare date, and the plausibility
  window catches a far-off one. A wrong date within the window is no worse than today, and can be corrected
  by hand.
- **[The chain list matches a product name]**, such as a chain name used as a brand → Earliest-in-text puts
  the header first. `Salling` and `Coop` alone are left out for this reason. An unlisted chain just falls back
  to the model.
- **[Ollama ignores `pattern` in another version]** → Decision 2's fallback turns it into retries rather than
  wrong figures. `OLLAMA_VERSION` defaults to `latest`, which is what was evaluated.
- **[GPU and CPU inference differ]** → The acceptance run is the CPU container limited to 4 cores
  (`--cpus=4 --cpuset-cpus=0-3`, `num_thread` 4), matching the LXC. GPU is for iterating.
- **[Merchant rows multiply]** → An install that stored `Super Brugsen` from a photo now gets `SuperBrugsen`
  rows too. They can be fixed by hand. A merge tool is not part of this change.

## Migration Plan

No schema, data or API migration. The change ships as a backend release:

- A receipt already queued or extracting when the new version starts is extracted with the new prompt from its
  stored text, which is the existing restart behaviour.
- Rolling back restores the old prompts with nothing to undo, because nothing new is stored.
