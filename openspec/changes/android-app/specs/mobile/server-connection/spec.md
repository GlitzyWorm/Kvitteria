# Spec Delta

## Purpose

Lets a household point the app at its own Kvitteria server, and proves that the address really is one
before anyone tries to sign in.

## ADDED Requirements

### Requirement: The server address is normalized
The app SHALL reduce what a person types to a bare origin: trimmed, with `https` when no scheme is given,
and with any path, query or fragment removed.

#### Scenario: No scheme
- **WHEN** a person enters `kvitteria.example.dk`
- **THEN** the address becomes `https://kvitteria.example.dk`

#### Scenario: Pasted route
- **WHEN** a person enters `http://10.0.2.2:8080/auth/v1`
- **THEN** the address becomes `http://10.0.2.2:8080`, and the app says the route was removed

#### Scenario: Invalid input
- **WHEN** the field is empty, unparseable, or uses a scheme other than http or https
- **THEN** the app shows a localized reason and does not continue

### Requirement: A server is verified before it is stored
The app SHALL call `GET /version` on the normalized address, and SHALL store the address only when the
response identifies a Kvitteria backend.

#### Scenario: Kvitteria backend
- **WHEN** the address answers `/version` with a valid version
- **THEN** the address is stored and the gate continues

#### Scenario: Something else answers
- **WHEN** the address answers with anything other than a valid version
- **THEN** the app says it is not a Kvitteria server and stores nothing

#### Scenario: Unreachable
- **WHEN** the address cannot be reached
- **THEN** the app names the address and stores nothing

### Requirement: The server can be changed
A person SHALL be able to change the server from the gate's failure state and from Settings. Changing it
SHALL end the current session.

#### Scenario: Changing from Settings
- **WHEN** a signed-in person changes the server
- **THEN** the session is cleared and the app returns to the gate with the new address

### Requirement: Release builds forbid cleartext traffic
Debug builds SHALL allow cleartext HTTP so an emulator can reach a local stack at
`http://10.0.2.2:8080`. Release builds SHALL refuse it.

#### Scenario: Release build
- **WHEN** a release build is pointed at an `http` address
- **THEN** the connection is refused and the app reports the address as unreachable
