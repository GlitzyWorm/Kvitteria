# Spec Delta

## Purpose

Keeps the image a receipt was read from beside the receipt, so whoever corrects an unreconciled or doubtful
receipt can see what was actually printed.

## ADDED Requirements

### Requirement: The source image stays with its receipt
When an uploaded image becomes a receipt, the system SHALL keep the image with that receipt, while the stored
transcription is deleted with the intake.

#### Scenario: Image-sourced receipt stored
- **WHEN** an uploaded image is stored as a receipt
- **THEN** the receipt reports that it has an image, and the intake and its transcription no longer exist

#### Scenario: Mail-sourced receipt
- **WHEN** a receipt came from mail
- **THEN** it reports that it has no image

### Requirement: A receipt's image can be fetched
The system SHALL serve a receipt's image as `image/jpeg` to any active member, and SHALL answer 404 with a
problem response when the receipt does not exist or has no image.

#### Scenario: Fetching the image
- **WHEN** an active member requests the image of an image-sourced receipt
- **THEN** they receive the JPEG bytes that were uploaded

#### Scenario: Receipt without image
- **WHEN** the image of a mail-sourced receipt is requested
- **THEN** the request is rejected with a 404 problem response

#### Scenario: Not signed in
- **WHEN** the image is requested without a valid token
- **THEN** the request is rejected with a 401 response

### Requirement: A failed image's source is kept until resolved
A failed image intake SHALL keep its image until it is retried into a receipt or dismissed. Dismissing it
SHALL delete the image.

#### Scenario: Dismissing a failed upload
- **WHEN** a member with `EditReceipts` dismisses a failed image intake
- **THEN** its image and transcription are deleted
