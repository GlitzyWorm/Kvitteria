# Spec Delta

## Purpose

Lets a household member hand the server a photo or screenshot of a receipt and have it become a receipt the
same way a receipt mail does, without the image ever being large enough to exhaust the server.

## ADDED Requirements

### Requirement: A receipt image can be uploaded
The system SHALL let a member with `AddReceipts` upload one JPEG image as a new receipt, optionally with the
time it was captured. The request SHALL return as soon as the image is durably recorded and queued, with the
recorded intake's ID, and the receipt SHALL belong to the uploading member.

#### Scenario: Upload accepted
- **WHEN** a member with `AddReceipts` uploads a JPEG within the size budget
- **THEN** the response is 202 with the intake ID, and the pipeline's queued count includes it

#### Scenario: Attribution
- **WHEN** an uploaded image becomes a receipt
- **THEN** the receipt's member is the uploader and it has no mailbox

#### Scenario: Missing permission
- **WHEN** a member without `AddReceipts` uploads an image
- **THEN** the request is rejected with a 403 response and nothing is recorded

#### Scenario: Queue is full
- **WHEN** an image is uploaded while the extraction queue is full
- **THEN** the request is rejected with a 503 problem response and nothing is kept

### Requirement: Images are limited to a fixed size budget
The system SHALL reject an upload that is not a complete JPEG, exceeds 1 000 000 pixels, or exceeds 2 MB,
with a 400 problem response naming the limit. It SHALL NOT resize or reinterpret the image itself.

#### Scenario: Too many pixels
- **WHEN** a 1536×2048 JPEG is uploaded
- **THEN** the request is rejected with a 400 problem response stating the pixel budget, and nothing is recorded

#### Scenario: Not a JPEG
- **WHEN** a PNG, a PDF or a truncated JPEG is uploaded
- **THEN** the request is rejected with a 400 problem response and nothing is recorded

#### Scenario: Within budget
- **WHEN** an 864×1152 JPEG of 150 KB is uploaded
- **THEN** it is accepted

### Requirement: Uploads need a model that can read images
The system SHALL refuse an upload with a 409 problem response when the configured extraction model cannot
read images, naming the model.

#### Scenario: Text-only model configured
- **WHEN** the pipeline model has no vision capability and a member uploads an image
- **THEN** the request is rejected with a 409 problem response naming the model, and nothing is recorded

### Requirement: An identical image is processed once
The system SHALL reject an upload whose bytes are identical to an image still in progress, failed, or kept
with a stored receipt, with a 409 problem response.

#### Scenario: Same file uploaded twice
- **WHEN** a member uploads the same JPEG bytes a second time
- **THEN** the request is rejected with a 409 problem response and no second intake exists

#### Scenario: Image of a deleted receipt
- **WHEN** the receipt an image became has been deleted and the same bytes are uploaded again
- **THEN** the upload is accepted as new

### Requirement: An image is transcribed once, then extracted from its text
The system SHALL transcribe an uploaded image to plain text and store it before extracting. Extraction SHALL
use the reconciliation and retry rules mail uses, but with instructions suited to photographed and
screenshotted receipts, and SHALL NOT change how mail is extracted.

#### Scenario: Transcription stored
- **WHEN** an uploaded image has been transcribed
- **THEN** the intake holds the text, and extraction attempts read that text rather than the image

#### Scenario: Retry after an extraction failure
- **WHEN** a failed image intake that already has a transcription is retried
- **THEN** extraction runs from the stored text without reading the image again

#### Scenario: Restart during transcription
- **WHEN** the server restarts before the transcription is stored
- **THEN** the image is transcribed again after startup

#### Scenario: Discount printed with a trailing minus
- **WHEN** a transcription has a line `RABAT 14,95-`
- **THEN** the extracted receipt has a line with total −14.95

#### Scenario: Danish date with spaces
- **WHEN** a transcription prints the date as `26 10 26 13:07` or `01 11 2026` with the time elsewhere
- **THEN** the receipt's purchase date is 26 October 2026 13:07 or 1 November 2026 respectively, in Danish time

### Requirement: An image that is not a receipt fails with that reason
When the transcription step reports that the image shows no receipt, the system SHALL mark the intake failed
with an error saying so, without attempting extraction.

#### Scenario: Photo of something else
- **WHEN** a member uploads a photo that contains no receipt
- **THEN** the intake is failed with an error stating that no receipt was found, and no receipt is stored

### Requirement: The purchase date falls back to the capture time
When the extracted purchase date cannot be parsed, an image-sourced receipt SHALL use the capture time sent
with the upload, or the upload time when none was sent.

#### Scenario: Unreadable date with capture time
- **WHEN** the date on an uploaded receipt cannot be read and the upload carried a capture time
- **THEN** the receipt's purchase date is that capture time

#### Scenario: Unreadable date without capture time
- **WHEN** the date cannot be read and no capture time was sent
- **THEN** the receipt's purchase date is the upload time
