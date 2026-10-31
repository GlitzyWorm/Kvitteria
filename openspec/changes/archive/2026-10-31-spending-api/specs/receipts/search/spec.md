# Spec Delta

## Purpose

Lets a member find a receipt from what they remember about it: the shop, who bought it, something on
it, or what it cost.

## ADDED Requirements

### Requirement: Receipts can be searched by text
The receipt list SHALL accept an optional search term matching, ignoring case, any part of the merchant
name, the member's display name, a line's printed name, or a line's canonical item name. It SHALL combine
with the existing filters and keep the existing order and paging.

#### Scenario: Matching a merchant
- **WHEN** a member searches for `rema`
- **THEN** receipts from `REMA 1000` are returned

#### Scenario: Matching a line
- **WHEN** a member searches for text printed on one line of a receipt
- **THEN** that receipt is returned once, with all its lines

#### Scenario: Danish letters
- **WHEN** a member searches for `føtex`
- **THEN** receipts from `FØTEX` are returned

#### Scenario: Wildcard characters
- **WHEN** a search term contains `%` or `_`
- **THEN** those characters match literally

#### Scenario: Combined with a filter
- **WHEN** a search is combined with a date range
- **THEN** only receipts meeting both are returned, and the total count reflects that

### Requirement: Receipts can be searched by amount
When the term is a number, using a comma or a point as the decimal separator, the search SHALL also match
receipts by grand total. A term with decimals SHALL match that exact amount. A whole number SHALL match
totals from that number up to, but not including, the next whole number.

#### Scenario: Exact amount
- **WHEN** a member searches for `284,50`
- **THEN** receipts with a grand total of 284.50 are returned

#### Scenario: Point as separator
- **WHEN** a member searches for `284.50`
- **THEN** the same receipts are returned

#### Scenario: Whole number
- **WHEN** a member searches for `284`
- **THEN** receipts with a grand total from 284.00 to 284.99 are returned

#### Scenario: Number in a name
- **WHEN** a member searches for `1000`
- **THEN** receipts from `REMA 1000` are also returned

### Requirement: An empty or missing search changes nothing
A missing, empty or whitespace-only search term SHALL leave the list as it is. A term longer than 100
characters SHALL be rejected.

#### Scenario: No search
- **WHEN** a member lists receipts without a search term
- **THEN** the results are the same as before this change

#### Scenario: Whitespace only
- **WHEN** the search term is only whitespace
- **THEN** the results are the same as with no term

#### Scenario: Term too long
- **WHEN** the search term is longer than 100 characters
- **THEN** the request is rejected with a 400 problem response
