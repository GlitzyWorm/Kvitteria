# mobile/licensing Specification

## Purpose

Keeps the app under MPL-2.0 and meets the notice obligations of everything it ships with.

## Requirements

### Requirement: Source files carry the MPL header
Every Kotlin and Kotlin script source file under `android/` SHALL carry the MPL-2.0 Exhibit A header, and the
build check SHALL fail when one is missing.

#### Scenario: New file without a header
- **WHEN** a Kotlin file without the header is added
- **THEN** the check fails, naming the file

### Requirement: The app has no server code
`android/` SHALL NOT contain or depend on code from the AGPL backend. The app and server share only the HTTP
API.

#### Scenario: Reviewing dependencies
- **WHEN** the app's dependencies are listed
- **THEN** none is GPL or AGPL licensed

### Requirement: Licences are readable in the app
A screen reachable from Settings SHALL show MPL-2.0, the OFL notices for Montserrat and Poppins, the MIT notice
for Phosphor, and the licence of each bundled library.

#### Scenario: Opening licences
- **WHEN** a member opens Licences
- **THEN** every notice can be read in full, offline

#### Scenario: New dependency
- **WHEN** a dependency is added
- **THEN** it appears on the screen without a manual edit
