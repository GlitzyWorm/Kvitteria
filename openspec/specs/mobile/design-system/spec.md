# mobile/design-system Specification

## Purpose

Keeps the app looking like the Kvitteria design: a compact dark interface with one green accent, drawn
from tokens rather than one-off values.

## Requirements

### Requirement: The reference defines the tokens
Colours, type sizes, spacing, radii and elevation SHALL be the values in `docs/design-tokens.html`, with
`docs/design-system.html` defining the components. Type sizes SHALL be 42/32/25/20/16/13 for H1 to H6.

#### Scenario: Conflicting sources
- **WHEN** the reference pages disagree on the font pairing
- **THEN** headings use Montserrat at weight 500 and body text uses Poppins

#### Scenario: A component needs a value
- **WHEN** a screen needs a colour, size, radius or spacing
- **THEN** it reads a named token, never a literal

### Requirement: The app is dark only
The app SHALL use the dark palette regardless of the system setting, including in system-drawn surfaces
such as pickers and the status and navigation bars, and SHALL paint the dark ground from the first frame.

#### Scenario: Device in light mode
- **WHEN** the device uses light mode
- **THEN** every surface, dialog and system bar stays dark

#### Scenario: Cold start
- **WHEN** the app launches
- **THEN** no light frame appears before the first screen

### Requirement: The accent is a line and a glow
Buttons SHALL default to the quiet outline style. Primary actions SHALL be an accent outline on transparent,
never a solid fill, and large surfaces SHALL NOT be filled with the accent.

#### Scenario: Primary action
- **WHEN** a primary button is shown
- **THEN** it has an accent outline and text, with press feedback as a tint

### Requirement: Amounts align
Amounts SHALL be set in Montserrat with tabular figures. Amounts SHALL NOT be set in Poppins, which has no
tabular figures.

#### Scenario: Column of amounts
- **WHEN** amounts are stacked in a list
- **THEN** their digits line up

### Requirement: Elevation is an edge plus ambient darkness
Each elevation level SHALL pair a neutral edge with its shadow. The small level SHALL be an edge alone.
Dialogs SHALL use the large level.

#### Scenario: Dialog
- **WHEN** a dialog is shown
- **THEN** it has the Neutral 500 edge and the large shadow

### Requirement: Icons are Phosphor glyphs
Icons SHALL come from the bundled Phosphor font and take the colour of their context.

#### Scenario: Tab icon
- **WHEN** a tab is selected
- **THEN** its icon is the accent colour with no change of glyph

### Requirement: The density stays usable
The design's compact visual sizes SHALL be kept, while every interactive element SHALL have a touch target
of at least 48dp. Text SHALL scale with the system font size without being clipped.

#### Scenario: Small icon button
- **WHEN** a control is drawn at 30dp, as the coverage steppers are
- **THEN** its touch target is still 48dp

#### Scenario: Large font setting
- **WHEN** the system font scale is 200 %
- **THEN** text wraps or truncates with an ellipsis rather than being cut off

### Requirement: The app draws edge to edge
Content SHALL extend behind the system bars, with interactive content kept clear of them and of the
keyboard.

#### Scenario: Keyboard open
- **WHEN** the keyboard opens on a form
- **THEN** the focused field and its submit action stay visible

### Requirement: Icons and images have text alternatives
Interactive icons SHALL carry a localized description. Decorative glyphs SHALL be hidden from screen readers.

#### Scenario: Screen reader on an icon button
- **WHEN** a screen reader focuses the settings button
- **THEN** it announces the localized label
