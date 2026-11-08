# Spec Delta

## Purpose

Gets a receipt that exists only on paper or on another app's screen into Kvitteria from the phone: scan it,
pick it, or share it in, and send it to the server in a form the server will accept.

## ADDED Requirements

### Requirement: Adding a receipt is offered where receipts are
With `AddReceipts`, Oversigt and Kvitteringer SHALL offer adding a receipt, which opens a choice of scanning a
paper receipt or choosing an existing image. Without it the action SHALL be hidden.

#### Scenario: Opening the choice
- **WHEN** a member with `AddReceipts` taps add receipt
- **THEN** a sheet offers scan and choose image

#### Scenario: No permission
- **WHEN** a member lacks `AddReceipts`
- **THEN** no add receipt action is shown

### Requirement: Paper receipts are scanned with cropping
Scan SHALL open a document scanner that finds, crops and straightens a single page, without the app holding
the camera permission. When the device cannot provide the scanner, scan SHALL be hidden and choose image
SHALL remain.

#### Scenario: Scanning
- **WHEN** a member scans a receipt and accepts the result
- **THEN** the cropped page is shown for confirmation

#### Scenario: Scanner unavailable
- **WHEN** the device has no scanner support
- **THEN** only choose image is offered

#### Scenario: Scan cancelled
- **WHEN** a member backs out of the scanner
- **THEN** nothing is uploaded and the previous screen is shown

### Requirement: Existing images can be chosen
Choose image SHALL open the system photo picker for one image, without a storage permission.

#### Scenario: Choosing a screenshot
- **WHEN** a member picks a screenshot of a store app's receipt
- **THEN** it is shown for confirmation

### Requirement: Images can be shared into the app
The app SHALL accept a single image shared from another app and show it for confirmation. A share that
arrives before a member is signed in SHALL be kept for this app session and shown once one is.

#### Scenario: Sharing while signed in
- **WHEN** a member shares an image to Kvitteria
- **THEN** the app opens on the confirmation of that image

#### Scenario: Sharing while signed out
- **WHEN** an image is shared while no member is signed in
- **THEN** the app goes through sign-in and then shows the confirmation

#### Scenario: Sharing without permission
- **WHEN** the signed-in member lacks `AddReceipts`
- **THEN** the share is discarded with a message that the permission is missing

### Requirement: Images are prepared on the phone before upload
Before upload the app SHALL rotate the image upright, scale it down to at most 1 000 000 pixels keeping its
aspect ratio, and encode it as JPEG without metadata. It SHALL send the time the image was taken when known.

#### Scenario: Large camera photo
- **WHEN** a 4000×3000 photo is confirmed
- **THEN** the uploaded JPEG is at most 1 000 000 pixels with the same aspect ratio

#### Scenario: Location metadata
- **WHEN** the chosen photo carries GPS coordinates
- **THEN** the uploaded file carries none

#### Scenario: Small screenshot
- **WHEN** an image is already within the pixel budget
- **THEN** it is not scaled up

### Requirement: Confirming uploads and reports the outcome
Confirmation SHALL show the prepared image with upload and discard. A successful upload SHALL say the receipt
is being read. A rejection SHALL show the server's message.

#### Scenario: Upload accepted
- **WHEN** a member confirms and the server answers 202
- **THEN** the app says the receipt is queued and returns to where adding started

#### Scenario: Already uploaded
- **WHEN** the server answers 409 for an identical image
- **THEN** its message is shown and the image can be discarded

#### Scenario: No connection
- **WHEN** the upload can't reach the server
- **THEN** a localized message is shown and the image stays on the confirmation for another try
