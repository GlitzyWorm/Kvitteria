# pipeline/receipt-extraction Specification

## Purpose
Turns recorded receipt text, a mail's body or a transcribed image, into the figures a receipt is stored
with: merchant, purchase date, total, pant and lines. Every source follows the same rules.

## Requirements

### Requirement: Every source is extracted by the same rules
The system SHALL extract a mail's text and an image's transcription with the same instructions and the same
rules, so the same printed receipt gives the same figures whichever way it arrived.

#### Scenario: Same receipt from two sources
- **WHEN** a mail's text and an image's transcription print the same lines and amounts
- **THEN** both give the same merchant, purchase date, total, pant and lines

### Requirement: Only products and discounts become lines
The system SHALL store every printed product and every printed discount as a line, in printed order. It
SHALL NOT store totals, subtotals, payment, change, VAT, loyalty, savings summaries or deposit as lines.

#### Scenario: Payment and VAT lines
- **WHEN** a receipt prints `TOTAL 69,50`, `BETALINGSKORT 69,50` and `MOMS UDGØR 13,90` after its products
- **THEN** none of those three is stored as a line

#### Scenario: Discount printed after its product
- **WHEN** a receipt prints `VALSØLILLE VIT 0,85L 39,95` followed by `RABAT 14,95-`
- **THEN** both are stored as lines, the second with total −14.95

#### Scenario: Savings summary beside per-line discounts
- **WHEN** an app receipt prints `Rabat på køb 50,00` above its items and `Rabat -50,00` among them
- **THEN** only the discount among the items is stored as a line

#### Scenario: Labels printed before their amounts
- **WHEN** a mail prints `HERAF PANT IALT`, `HERAF MOMS` and `I ALT`, followed by `0,00`, `61,61` and `308,05`
- **THEN** the total is 308.05, the pant is 0, and none of the three is stored as a line

#### Scenario: First and last products
- **WHEN** a receipt's first product follows the store address and its last product precedes the totals
- **THEN** both are stored as lines

### Requirement: Pant is the sum of the printed deposit lines
The system SHALL set a receipt's pant to the sum of its printed deposit lines, or to a printed deposit total
when the receipt prints one instead, and to 0 when it prints neither.

#### Scenario: Deposit line among the products
- **WHEN** a receipt prints `PANT 1,50` under a bottled product
- **THEN** the pant is 1.50 and no line is stored for it

#### Scenario: No deposit
- **WHEN** a receipt prints no deposit
- **THEN** the pant is 0

### Requirement: Amounts are read exactly as printed
The system SHALL read every amount as a printed Danish amount with a decimal comma, where a minus before or
after it makes it negative, and a discount is always negative. A line's total SHALL be its printed amount,
never multiplied by its quantity. The unit price SHALL be the total divided by the quantity.

#### Scenario: Trailing minus
- **WHEN** a line prints `14,95-`
- **THEN** its total is −14.95

#### Scenario: Leading minus
- **WHEN** a line prints `-50,00`
- **THEN** its total is −50.00

#### Scenario: Quantity line
- **WHEN** a receipt prints `BACON I SKIVER`, then `2 x 12,00` with the amount `24,00`
- **THEN** the line has quantity 2, total 24.00 and unit price 12.00

#### Scenario: Size in the product name
- **WHEN** a line prints `FAXE KONDI 24X33 CL 115,00`
- **THEN** its quantity is 1 and its total is 115.00

#### Scenario: Øre kept
- **WHEN** a mail prints `29,95` as a product's amount
- **THEN** the line's total is 29.95, not 29

### Requirement: An unreadable amount is an unusable answer
When the model's answer contains an amount that cannot be read as a printed Danish amount, the system SHALL
count the attempt as an unusable answer and retry it the way it retries malformed output.

#### Scenario: Amount is not a number
- **WHEN** an attempt returns `2 x 10,05` or an empty value as an amount
- **THEN** the attempt counts towards the attempt limit and the next attempt is told its answer could not be used

### Requirement: The purchase date is read from the printed text
The system SHALL take the purchase date from the receipt text itself, not from the model, in Danish local
time. Day comes before month and a two-digit year means 20YY. When several dates are printed, the first one
with a time SHALL be used, otherwise the first one.

#### Scenario: Date among register numbers
- **WHEN** a receipt prints `11 1 462 26 10 26 13:07`
- **THEN** the purchase date is 26 October 2026 13:07

#### Scenario: Dotted date
- **WHEN** a receipt prints `31.10.2026 11:40`
- **THEN** the purchase date is 31 October 2026 11:40

#### Scenario: Written month
- **WHEN** a mail prints `9. november 2026 - 13:33` and later `Tidspunkt: 09-11-2026 13:33:17`
- **THEN** the purchase date is 9 November 2026 13:33

#### Scenario: Date without a time
- **WHEN** a receipt prints `01 11 2026` and no time beside it
- **THEN** the purchase date is 1 November 2026 at midnight

### Requirement: A missing or implausible date falls back to the source's time
When the text prints no date, or the date found lies more than one day after the mail's time or the image's
capture or upload time, or more than 365 days before it, the system SHALL use that time instead.

#### Scenario: No date printed
- **WHEN** a screenshot of an app receipt prints no date
- **THEN** the purchase date is the capture time, or the upload time when none was sent

#### Scenario: Date far in the past
- **WHEN** a mail from November 2026 yields a date in 2024
- **THEN** the purchase date is the mail's time

#### Scenario: Old receipt photographed later
- **WHEN** a receipt printed 26 October 2026 is photographed on 10 November 2026
- **THEN** the purchase date is 26 October 2026

### Requirement: The merchant is a known chain where one is printed
When the text names a known Danish grocery chain, the system SHALL use that chain's single spelling as the
merchant, whatever the receipt adds or how it spaces or cases the name. Otherwise it SHALL use the shop name
the model read, without address, CVR or branch number.

#### Scenario: Self-scanning mail
- **WHEN** a mail's header reads `Netto Scan&Go`
- **THEN** the merchant is `Netto`

#### Scenario: Spaced chain name
- **WHEN** a receipt prints `Super Brugsen` in one view and `SuperBrugsen Brøndby` in another
- **THEN** the merchant is `SuperBrugsen` for both

#### Scenario: Header cut off
- **WHEN** a photo shows no store header but prints `www.føtex.dk`
- **THEN** the merchant is `Føtex`

#### Scenario: Unknown shop
- **WHEN** no known chain appears in the text
- **THEN** the merchant is the shop name the model read
