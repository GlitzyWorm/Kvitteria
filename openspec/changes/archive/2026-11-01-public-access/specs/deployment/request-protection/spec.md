# Spec Delta

## Purpose

Bounds what a stranger can do to a reachable stack: repeated sign-in and sign-up attempts and API request floods are
limited per client address, and that address cannot be forged by the client.

## ADDED Requirements

### Requirement: The client address is established at the entrypoint
The entrypoint SHALL determine each request's client address and pass that single value to the services behind it,
replacing whatever the client sent. The services SHALL use it for rate limiting.

#### Scenario: Spoofed header on a direct connection
- **WHEN** a client connected directly to the entrypoint sends its own forwarding or client-address headers
- **THEN** the headers are ignored and the address of the connection is used

### Requirement: Only trusted proxies can name a client
The entrypoint SHALL honour a forwarded client address only from a trusted proxy. By default, private-network peers
are trusted in the base configuration and no peer is trusted in public-domain mode. A deployer MAY set the trusted list.

#### Scenario: Behind a proxy on the host
- **WHEN** a proxy on a private network forwards requests from two different clients
- **THEN** each is limited under its own address, not the proxy's

#### Scenario: Public-domain mode
- **WHEN** a client reaches a stack in public-domain mode and sends a forwarded address
- **THEN** it is ignored

#### Scenario: Proxy on a public address
- **WHEN** a deployer's proxy has a public address and the trusted list is not changed
- **THEN** all its clients share one limit, and the documentation says how to set the list

### Requirement: Sign-in and sign-up are rate limited
Password sign-in, token refresh and sign-up SHALL be limited per client address. A request over the limit SHALL get
429 and SHALL NOT be evaluated.

#### Scenario: Password guessing
- **WHEN** one address keeps sending password sign-ins past the limit
- **THEN** further attempts get 429 whether or not the password is right

#### Scenario: Limit holds on a default install
- **WHEN** a stack started from the shipped compose files receives requests through the entrypoint
- **THEN** the limit is enforced with no extra configuration

#### Scenario: Another client
- **WHEN** one address is over the limit
- **THEN** a client at a different address signs in normally

### Requirement: API requests are rate limited
Requests under `/api` SHALL be limited per client address, generously enough that a household sharing one address does
not reach the limit in normal use. A request over the limit SHALL get 429 as problem details with a `Retry-After` header.

#### Scenario: Flood
- **WHEN** one address exceeds the limit
- **THEN** further requests get 429 until the window passes, without reaching an endpoint

#### Scenario: Normal household use
- **WHEN** two people use the app from one home connection
- **THEN** no request is limited

#### Scenario: Works with any proxy
- **WHEN** the deployer replaces or adds a proxy
- **THEN** the API limit still applies, because it does not depend on the proxy

### Requirement: Probes are not rate limited
`/health/*` and `/version` SHALL NOT be limited, so orchestrators and the app's connection check keep working while a
client is being limited.

#### Scenario: Probe during a flood
- **WHEN** an address is over the API limit
- **THEN** `GET /health/ready` and `GET /version` from that address still answer

### Requirement: Limits do not change the API contract
Rate limiting SHALL NOT change the routes, request shapes or response schemas of the OpenAPI document.

#### Scenario: Contract check
- **WHEN** the OpenAPI document is regenerated
- **THEN** it is identical to the committed one
