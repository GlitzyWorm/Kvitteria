# Spec Delta

## Purpose

Defines how one public address and one overlay choose how the stack is reached, so a household with a domain, one
behind its own proxy and one on localhost all run the same compose project without hand-editing services.

## ADDED Requirements

### Requirement: Identity addresses derive from the public URL
The token issuer, the identity provider's external URL, its site URL and its redirect allow list SHALL all derive
from `PUBLIC_URL` unless a deployer explicitly overrides the site URL or the allow list.

#### Scenario: Changing the address
- **WHEN** `PUBLIC_URL` is set to `https://kvitteria.example.dk` and the stack is recreated
- **THEN** a newly issued token names `https://kvitteria.example.dk/auth/v1` as its issuer and the backend accepts it

#### Scenario: Fresh configuration
- **WHEN** a `.env` is created from the checked-in example
- **THEN** it sets neither the site URL nor the allow list, so both follow `PUBLIC_URL`

#### Scenario: Existing configuration that pins them
- **WHEN** an existing `.env` still sets the site URL or the allow list
- **THEN** those values keep applying and the documentation says to remove them when moving off localhost

### Requirement: Local mode is the default
With the base compose files and a `PUBLIC_URL` on `http://localhost`, the stack SHALL serve plain HTTP on the
configured host port and SHALL NOT attempt to obtain a certificate.

#### Scenario: Existing local install
- **WHEN** an install that has never set `PUBLIC_URL` to anything else is upgraded and restarted
- **THEN** the API, auth and health routes answer on `http://localhost:8080` as they did before

### Requirement: Public-domain mode terminates TLS at the entrypoint
With the public overlay enabled and an `https://` `PUBLIC_URL`, the entrypoint SHALL obtain and renew a certificate
for that address itself, serving HTTPS on port 443 and redirecting port 80 to it.

#### Scenario: First start on a domain
- **WHEN** DNS points at the host, ports 80 and 443 are open and the stack starts with the overlay
- **THEN** a client reaching `PUBLIC_URL` gets a publicly trusted certificate without further configuration

#### Scenario: Restart keeps the certificate
- **WHEN** the entrypoint container is recreated
- **THEN** the certificate already issued is reused rather than requested again

#### Scenario: Plain HTTP request
- **WHEN** a client requests `http://` on the domain
- **THEN** it is redirected to the `https://` address

#### Scenario: Plain port is closed
- **WHEN** the overlay is enabled
- **THEN** the local HTTP host port is not published

### Requirement: Behind-a-proxy mode leaves TLS to the proxy
With the base compose files and an `https://` `PUBLIC_URL`, the entrypoint SHALL keep serving plain HTTP on the
configured host port and SHALL NOT attempt to obtain a certificate.

#### Scenario: Proxy in front
- **WHEN** a TLS-terminating proxy forwards `PUBLIC_URL` to the host port
- **THEN** sign-in and API calls through `PUBLIC_URL` succeed with no further stack configuration

### Requirement: HTTPS modes publish only the entrypoint
A stack configured by the setup scripts for either HTTPS mode SHALL run the production configuration and SHALL publish
no host port other than the entrypoint's, including when it was started from a source checkout.

#### Scenario: Set up from a checkout
- **WHEN** the setup script configures an HTTPS mode in a checkout that contains the development overlay
- **THEN** the backend runs in Production, and Postgres, the identity provider, the backend and Ollama have no host port

### Requirement: A release app can use both HTTPS modes
Release builds of the app SHALL connect to a stack in public-domain or behind-a-proxy mode with no app change. The
cleartext restriction on release builds SHALL remain.

#### Scenario: Physical phone on a domain
- **WHEN** a release build is given the `https://` address of a stack in either HTTPS mode
- **THEN** the gate completes and sign-in works

#### Scenario: Release build against plain HTTP
- **WHEN** a release build is given an `http://` address
- **THEN** it reports the address as unreachable, as it does today
