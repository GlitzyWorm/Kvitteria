# deployment/first-run-setup Specification

## Purpose

Makes the setup scripts the safe way to bring up a stack: they configure it for the chosen mode and create the first
administrator before anyone outside the host can reach it, closing the race to claim an empty household.

## Requirements

### Requirement: Setup configures the chosen mode on a fresh install
On a fresh install the setup scripts SHALL establish the public address and, for an `https://` address, whether a
proxy already terminates TLS, then write a `.env` that selects that mode.

#### Scenario: Accepting the default
- **WHEN** the person accepts the default address
- **THEN** the stack is configured for local mode

#### Scenario: Domain without a proxy
- **WHEN** the person gives an `https://` address and says nothing else terminates TLS
- **THEN** the `.env` enables the public overlay, excludes the development overlay and uses the path separator of
  that operating system

#### Scenario: Domain with a proxy
- **WHEN** the person gives an `https://` address and says a proxy terminates TLS
- **THEN** the `.env` sets the address, does not enable the overlay and excludes the development overlay

#### Scenario: Malformed address
- **WHEN** the address has no scheme, uses another scheme, or carries a path or trailing slash
- **THEN** the script refuses it and starts nothing

#### Scenario: Compose too old for the overlay
- **WHEN** public-domain mode is chosen and the installed Compose cannot parse the overlay
- **THEN** the script names the required version and starts nothing

### Requirement: A fresh configuration is complete
The setup scripts SHALL generate every secret the stack needs, on every supported platform, so the identity provider
starts on a fresh install.

#### Scenario: Fresh install on Linux
- **WHEN** `setup.sh` writes a new `.env`
- **THEN** the database password, the signing key set and the identity provider's symmetric secret are all set

### Requirement: The first admin exists before the entrypoint is reachable
The setup scripts SHALL create the first admin through the normal identity and membership path, and SHALL NOT start
the entrypoint on an unclaimed household.

#### Scenario: Fresh install
- **WHEN** the script runs on a fresh install
- **THEN** the backend reports the household as claimed before any port is published to the network

#### Scenario: Claim fails
- **WHEN** creating the account fails, for example because the password is too short
- **THEN** the entrypoint stays stopped, the script reports the reason and exits non-zero

#### Scenario: Address not reachable yet
- **WHEN** DNS or the deployer's proxy is not yet in place
- **THEN** the claim still succeeds, because it does not travel through the public address

### Requirement: Admin credentials are handled carefully
The setup scripts SHALL accept the admin's email and password from a hidden prompt or from environment variables for
unattended runs. They SHALL NOT pass the password as a process argument or write it to any file.

#### Scenario: Interactive run
- **WHEN** a fresh install runs in a terminal
- **THEN** the credentials are asked for before the model download begins and the password is not echoed

#### Scenario: Unattended run without credentials
- **WHEN** the household is unclaimed, no terminal is attached and the variables are unset
- **THEN** the script explains what is missing and exits non-zero with the entrypoint stopped

### Requirement: Re-running setup is safe
The setup scripts SHALL leave an existing `.env` alone and SHALL NOT ask for credentials when the household already
has an admin.

#### Scenario: Claimed household
- **WHEN** the script runs against a claimed household
- **THEN** it starts whatever is stopped, reports the stack ready and creates no account

#### Scenario: Interrupted first run
- **WHEN** an earlier run stopped before the claim
- **THEN** the next run asks for credentials and completes the claim

#### Scenario: Account exists but the claim did not complete
- **WHEN** the account was created by an earlier run that stopped before the membership request
- **THEN** the script signs in with the given credentials and completes the claim

### Requirement: Ready means claimed
The setup scripts SHALL report the stack ready only after the backend's `household` check reports an admin and the
entrypoint has started. The final output SHALL name the address clients use.

#### Scenario: Successful run
- **WHEN** the script finishes without error
- **THEN** its last lines state that the household is claimed and give `PUBLIC_URL`

### Requirement: The manual route warns about the race
Documentation of a deployment that skips the setup scripts SHALL state that the first caller to sign in claims the
household, and that such a stack must not be reachable from outside until claimed.

#### Scenario: Reading the manual route
- **WHEN** a deployer follows the documented route without the scripts
- **THEN** the warning appears beside those instructions
