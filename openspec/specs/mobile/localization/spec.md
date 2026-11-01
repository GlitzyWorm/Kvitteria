# mobile/localization Specification

## Purpose

Makes the app Danish first, with English as a choice, and keeps amounts and dates in one consistent form.

## Requirements

### Requirement: Danish is the default language
Danish SHALL be the app's default language and English the only translation, so a device in any other
language shows Danish.

#### Scenario: Device in German
- **WHEN** the device language is German
- **THEN** the app is in Danish

#### Scenario: Device in English
- **WHEN** the device language is English and no choice has been made
- **THEN** the app is in English

### Requirement: The language can be changed in the app
A person SHALL be able to choose Dansk or English in Settings. The change SHALL apply at once and persist
across restarts.

#### Scenario: Changing language
- **WHEN** a person chooses English
- **THEN** every visible string, including tab labels, changes without a restart

#### Scenario: Restart
- **WHEN** the app restarts
- **THEN** the chosen language is still in effect

### Requirement: Household data is not translated
Item names, categories, meals and server `detail` text SHALL be shown as stored.

#### Scenario: English interface
- **WHEN** the interface is in English
- **THEN** a category named `Frugt & Grønt` is shown unchanged

### Requirement: Amounts use one format
Amounts SHALL use the Danish form in every language: a point for thousands, a comma for decimals, a true
minus sign, and `kr`. A missing amount SHALL show `—`. Amounts SHALL keep their exact decimal value from the
server to the screen.

#### Scenario: Formatting
- **WHEN** an amount of 2438.27 is shown
- **THEN** it reads `2.438,27 kr`

#### Scenario: Negative
- **WHEN** an amount of -12.5 is shown
- **THEN** it reads `−12,50 kr`

#### Scenario: Exact value
- **WHEN** the server sends 0.1 + 0.2 style values such as 284.30
- **THEN** no rounding drift appears

### Requirement: Dates follow Danish local time
Purchase dates and months SHALL be shown in Danish local time, whatever the device's time zone.

#### Scenario: Device abroad
- **WHEN** a receipt was bought at 00:30 Danish time and the device is in New York
- **THEN** the date shown is the Danish date
