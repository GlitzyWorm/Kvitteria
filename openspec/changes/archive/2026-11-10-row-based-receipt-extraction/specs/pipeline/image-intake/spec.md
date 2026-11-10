# Spec Delta

## MODIFIED Requirements

### Requirement: An image is transcribed once, then extracted from its text
The system SHALL transcribe an uploaded image to plain text and store it before extracting. Extraction SHALL
use the same instructions, reconciliation and retry rules as mail.

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

### Requirement: The purchase date falls back to the capture time
When no plausible purchase date can be read from the transcription, an image-sourced receipt SHALL use the
capture time sent with the upload, or the upload time when none was sent.

#### Scenario: Unreadable date with capture time
- **WHEN** the date on an uploaded receipt cannot be read and the upload carried a capture time
- **THEN** the receipt's purchase date is that capture time

#### Scenario: Unreadable date without capture time
- **WHEN** the date cannot be read and no capture time was sent
- **THEN** the receipt's purchase date is the upload time

#### Scenario: No date printed
- **WHEN** an uploaded screenshot prints no date and the upload carried a capture time
- **THEN** the receipt's purchase date is that capture time
