# Spec Delta

## MODIFIED Requirements

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
