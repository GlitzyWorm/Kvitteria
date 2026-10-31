# Spec Delta

## Purpose

Keeps the server and the app in step now that they no longer share compiled types, so an API change is
noticed in review rather than at runtime.

## ADDED Requirements

### Requirement: The API description is checked in
The repository SHALL contain an OpenAPI document generated from the backend. CI SHALL fail when the generated
document differs from the checked-in one.

#### Scenario: API changed without the document
- **WHEN** a change alters an endpoint or DTO and leaves the document stale
- **THEN** CI fails

#### Scenario: Backend unchanged
- **WHEN** nothing in the API changes
- **THEN** the check passes, deterministically

### Requirement: App models match the document
A check SHALL compare each of the app's wire models with its schema in the document, and fail when a field the
app requires is missing, or when the app treats a nullable field as required.

#### Scenario: Field renamed on the server
- **WHEN** a server field the app reads is renamed in the document
- **THEN** the app's check fails, naming the model and field

#### Scenario: Server adds a field
- **WHEN** the server adds a field the app doesn't read
- **THEN** the check passes, and the app ignores the field at runtime

### Requirement: Wire types keep their meaning
The app SHALL decode amounts to an exact decimal type, read `ReceiptStatus` as a number, and read other enums as
names.

#### Scenario: Amount with many digits
- **WHEN** the server sends an amount such as 12345.67
- **THEN** the app holds exactly that value

#### Scenario: Unknown status
- **WHEN** a numeric status or enum name is unknown to the app
- **THEN** the response still decodes, and the value is treated as unknown

### Requirement: Removing shared code changes nothing on the wire
Folding the shared contract project into the backend SHALL leave the API unchanged.

#### Scenario: Comparing before and after
- **WHEN** the document is generated before and after the move
- **THEN** the two are identical
