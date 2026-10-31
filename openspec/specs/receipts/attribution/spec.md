# receipts/attribution Specification

## Purpose
Records which mailbox and household member a receipt came from, so clients can show whose shopping it
was and the pipeline can report activity per mailbox.

## Requirements

### Requirement: Receipts record their source
A receipt stored from mail SHALL record the mailbox and household member the mail arrived through.

#### Scenario: New receipt
- **WHEN** a mail fetched from a member's mailbox is stored as a receipt
- **THEN** the receipt records that mailbox and member

#### Scenario: Retried receipt
- **WHEN** a failed mail is retried and stored
- **THEN** the receipt records the mailbox and member of the original mail

#### Scenario: Mailbox deleted while mail is failed
- **WHEN** a mailbox is deleted before its failed mail is retried and stored
- **THEN** the receipt has no mailbox but still records the member

### Requirement: Clients receive the member
Receipt responses SHALL include the member's ID and display name, on both the receipt list and a
single receipt. Both SHALL be null when the member is unknown.

#### Scenario: Attributed receipt
- **WHEN** a client reads a receipt with a known member
- **THEN** the response includes that member's ID and display name

#### Scenario: Receipt from before this change
- **WHEN** a client reads a receipt stored before attribution existed
- **THEN** both fields are null and the rest of the receipt is unchanged

### Requirement: Attribution survives mailbox removal
Deleting a mailbox SHALL NOT delete or change the member of any receipt.

#### Scenario: Deleting a mailbox
- **WHEN** a mailbox with stored receipts is deleted
- **THEN** its receipts remain, keep their member, and no longer reference the mailbox
