# mobile/session Specification

## Purpose

Decides where the app starts and keeps a person signed in, using the identity provider for accounts and
the server for household membership.

## Requirements

### Requirement: The gate resolves the start screen
Whenever the app starts or a session ends, the gate SHALL decide in this order: no stored server goes to
server entry; a failed `/version` reads `/update-status`, showing an update in progress or a needed restore if it says
so and otherwise staying on the gate with a retry naming the address; no session goes to
claim or sign-in; an expiring session is refreshed first; `GET /api/users/me` decides the rest.

#### Scenario: First launch
- **WHEN** no server is stored
- **THEN** the app shows server entry

#### Scenario: Server unreachable
- **WHEN** `/version` fails and `/update-status` does not report an update
- **THEN** the gate shows the address with retry and change-server actions, and does not sign out

#### Scenario: Member
- **WHEN** `/api/users/me` answers 200
- **THEN** the app caches the member and opens the tabs

#### Scenario: Back navigation
- **WHEN** a person signs out and presses back
- **THEN** no signed-in screen is reachable

### Requirement: An unclaimed household is announced
With no session, the app SHALL read the `household` check in `GET /health/ready` whatever the HTTP status,
and SHALL show the claim screen when the household has no admin, explaining that the first account
becomes its administrator. Otherwise it SHALL show sign-in.

#### Scenario: Fresh install with Ollama down
- **WHEN** the readiness report answers 503 and its `household` check is Degraded
- **THEN** the claim screen is shown

#### Scenario: Claimed household
- **WHEN** the `household` check is Healthy
- **THEN** sign-in is shown

#### Scenario: Unreadable report
- **WHEN** the report can't be read or doesn't name the check
- **THEN** sign-in is shown

### Requirement: People sign in and sign up against the identity provider
The app SHALL offer sign-in and sign-up on one screen, validate email format and an 8-character password
locally, and show identity-provider failures as localized messages, never its English text.

#### Scenario: Wrong password
- **WHEN** the identity provider rejects the credentials
- **THEN** a localized invalid-credentials message is shown

#### Scenario: Account already exists
- **WHEN** sign-up reports the address is taken
- **THEN** a localized message says so

#### Scenario: Network failure
- **WHEN** the request never completes
- **THEN** a localized network message is shown and the entered email is kept

#### Scenario: First account
- **WHEN** the first account signs in to an unclaimed household
- **THEN** the gate's request to `/api/users/me` makes it the administrator and the tabs open

### Requirement: A non-member is told so
A 403 from `/api/users/me` SHALL show the rejected state, naming the refused address and offering to sign
out. It SHALL NOT be presented as a failed sign-in.

#### Scenario: Authenticated but not a member
- **WHEN** `/api/users/me` answers 403
- **THEN** the rejected state names the address and the session is kept until the person signs out

### Requirement: The session is stored encrypted and whole
The session SHALL be stored encrypted with a device-bound key, as one value, so an access token and a
refresh token can never be stored from different sessions. It SHALL NOT be included in device backups.

#### Scenario: Restart with a valid session
- **WHEN** the app restarts with a stored session
- **THEN** the tabs open without a sign-in prompt

#### Scenario: Stored session unreadable
- **WHEN** the stored value can't be decrypted
- **THEN** it is discarded and the person is asked to sign in, without a crash

### Requirement: Token refresh is serialized
Only one refresh SHALL run at a time. A request SHALL re-check the token after waiting, so a burst of 401
responses causes one refresh, and each failed request SHALL be retried at most once.

#### Scenario: Burst of expired requests
- **WHEN** several requests get 401 at once
- **THEN** exactly one refresh is sent and each request is retried once with the new token

#### Scenario: Token about to expire
- **WHEN** the access token expires within 60 seconds
- **THEN** it is refreshed before the request is sent

#### Scenario: Refresh rejected
- **WHEN** the identity provider rejects the refresh token
- **THEN** the session is cleared and the app returns to the gate

#### Scenario: Refresh fails for a network reason
- **WHEN** a refresh can't reach the server
- **THEN** the stored session is kept and the request reports a network error

#### Scenario: Refresh is rate limited
- **WHEN** a refresh answers 429
- **THEN** the stored session is kept and the request reports a network error

#### Scenario: Refresh meets a server failure
- **WHEN** a refresh answers with a 5xx status, including a gateway error from a proxy
- **THEN** the stored session is kept and the request reports a network error

### Requirement: Identity requests carry no bearer handling
Requests to the identity provider SHALL NOT pass through the token refresh path.

#### Scenario: Refresh returns 401
- **WHEN** a refresh request itself answers 401
- **THEN** it is treated as a rejected refresh and does not trigger another refresh

### Requirement: The gate waits out an update
While `/update-status` reports `running`, the gate SHALL show that the server is updating to the target version, and
SHALL recheck by itself until `/version` answers, then continue resolving. The session SHALL be kept.

#### Scenario: Update finishes
- **WHEN** `/version` starts answering during the wait
- **THEN** the gate continues to the tabs without a sign-in

#### Scenario: Interrupted
- **WHEN** the `running` state's last change is over thirty minutes old
- **THEN** the gate shows the ordinary retry

### Requirement: A needed restore is explained
When `/update-status` reports `restore-required` and `/version` fails, the gate SHALL say the update failed after
changing the database, and SHALL show `./scripts/update.sh --restore` to run on the server with a copy action.

#### Scenario: Update stopped
- **WHEN** the server is down and the state is `restore-required`
- **THEN** the explanation and the command are shown, with a retry, and the session is kept
