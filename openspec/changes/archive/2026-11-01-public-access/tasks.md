# Tasks

Hand checks in this list use a throwaway stack (`COMPOSE_PROJECT_NAME=kvitteria-check`, its own `.env` values and
port), so the development database is left alone. There are no endpoint or integration tests of the backend, so
anything touching Caddy, compose or the database is verified that way.

## 1. App keeps its session through a limited refresh

- [x] 1.1 In `GoTrueTokenClient.refresh`, return `RefreshOutcome.Unreachable` for 429 and any 5xx, keeping other
  non-2xx answers as `Rejected`; add `SessionRefreshTest` cases for 429, 502 and 401 and verify with
  `./gradlew spotlessCheck testDebugUnitTest test` from `android/`
- [x] 1.2 Add the rule to the Server connection and auth section of `CLAUDE.md` and verify it sits beside the existing
  note on network failures keeping the stored session

## 2. Client address at the entrypoint

- [x] 2.1 In `caddy/Caddyfile`, remove `auto_https off`, read the site as `{$SITE_ADDRESS::8080}`, add the `servers`
  option with `trusted_proxies static {$TRUSTED_PROXIES:private_ranges}` and `trusted_proxies_strict`, and add
  `request_header X-Real-IP {client_ip}` at the top of the site block; verify with `caddy validate` in the stock image
  and that a bare `:8080` site adapts with no certificate policy and no `:80` listener
- [x] 2.2 In `compose.yaml`, pass `SITE_ADDRESS` (unset by default) and `TRUSTED_PROXIES`
  (`${TRUSTED_PROXIES:-private_ranges}`) to Caddy; verify with `docker compose config` and, on a running stack, that a
  client-sent `X-Real-IP` is replaced by checking what the backend logs

## 3. Rate limits on sign-in and sign-up

- [x] 3.1 In `compose.yaml`, set `GOTRUE_RATE_LIMIT_HEADER: X-Real-IP` and
  `GOTRUE_RATE_LIMIT_TOKEN_REFRESH: ${GOTRUE_RATE_LIMIT_TOKEN_REFRESH:-30}`, and expose `GOTRUE_RATE_LIMIT_OTP` with
  GoTrue's default of 30; verify by sending password sign-ins through Caddy until they answer 429, then that a request
  with a different trusted forwarded address succeeds
- [x] 3.2 Document both variables in `.env.example` and `docs/configuration.md`, and add a `docs/troubleshooting.md`
  entry naming GoTrue's "rate limiting is not applied" warning; verify each variable name matches `compose.yaml`

## 4. Rate limits on the API

- [x] 4.1 Add a small class under `Kvitteria/Backend/Features/` holding the policy name, the 300-a-minute sliding window
  and the partition key, and cover the key (including a null address) in `Backend.Tests`; verify with
  `dotnet test Backend.Tests/Backend.Tests.csproj`
- [x] 4.2 In `Program.cs`, register forwarded headers with `ForwardedForHeaderName = "X-Real-IP"`, `ForwardLimit = 1` and
  the private ranges in `KnownIPNetworks`, and call `UseForwardedHeaders` first in the pipeline; verify
  `dotnet build Backend/Backend.csproj` passes under `TreatWarningsAsErrors`
- [x] 4.3 Register the rate limiter with `RejectionStatusCode` 429 and an `OnRejected` that writes problem details and
  `Retry-After`, call `UseRateLimiter`, and attach the policy to the `/api` group only; verify on a running stack that a
  flood gets problem+json 429s while `/health/ready` and `/version` keep answering, and that two addresses do not share
  a window
- [x] 4.4 Regenerate with `dotnet build Backend/Backend.csproj -p:GenerateOpenApi=true` and verify
  `docs/openapi/kvitteria.json` has no diff
- [x] 4.5 Add the limiter and the forwarded-header trust to `CLAUDE.md`, including why `KnownIPNetworks` is private ranges
  rather than a compose subnet; verify the Auth section's "Local-only, for now" paragraph is rewritten to match

## 5. Modes

- [x] 5.1 In `compose.yaml`, default `GOTRUE_SITE_URL` and `GOTRUE_URI_ALLOW_LIST` to
  `${PUBLIC_URL:-http://localhost:8080}`, and remove both lines from `.env.example`; verify with `docker compose config`
  that both follow a changed `PUBLIC_URL` when a fresh `.env` is used
- [x] 5.2 Add `compose.public.yaml` with `SITE_ADDRESS: ${PUBLIC_URL}`, `TRUSTED_PROXIES: 127.0.0.1/32` and
  `ports: !override` for 80, 443 and 443/udp; verify with `docker compose -f compose.yaml -f compose.public.yaml config`
  that 8080 is gone
- [x] 5.3 Update the `COMPOSE_FILE` and `GOTRUE_DISABLE_SIGNUP` comments in `.env.example` so they cover the HTTPS modes,
  the Windows separator and what closing signup costs (new members can't join); verify against `docs/configuration.md`
- [x] 5.4 Verify local mode: an existing `.env` and `http://localhost:8080` serve API, auth and health as before, with no
  certificate attempts in Caddy's log
- [x] 5.5 Verify behind-a-proxy mode: with `COMPOSE_FILE=compose.yaml` and an `https://` `PUBLIC_URL` behind a local TLS
  proxy, sign-in succeeds with the new issuer and `docker compose ps` shows Caddy as the only published port

## 6. Setup scripts

- [x] 6.1 In `setup.sh`, generate `GOTRUE_JWT_SECRET`, and check that a `.env` from a fresh run brings GoTrue up
- [x] 6.2 Add the address and mode prompts to both scripts, plus flags or variables for unattended runs, with address
  validation and the Compose 2.24.4 check for domain mode, writing `COMPOSE_FILE` per mode (`;` in `setup.ps1`); verify
  each mode's resulting `.env`, and that a bad address starts nothing
- [x] 6.3 Add the claim to both scripts in the order of design decision 4: credentials first, `up -d --wait backend gotrue`,
  read `household` through `exec`, sign up with the password-grant fallback, `GET /api/users/me`, re-read `household`,
  then `up -d`; verify with `docker compose ps` during the claim that Caddy is not running
- [x] 6.4 Remove the health loop against `PUBLIC_URL` and the "create the first account" hint, ending with a line that
  says the household is claimed and names the address; verify a successful run prints it
- [x] 6.5 Verify the failure paths in both scripts: a too-short password leaves Caddy stopped with a non-zero exit, an
  unattended run with no credentials does the same, an interrupted run completes on the next one, and re-running on a
  claimed household asks for nothing and creates no account
- [x] 6.6 Verify the password stays out of process arguments and files: inspect the command lines used, and search
  `.env` and the shell history after a run

## 7. Documentation

- [x] 7.1 Add a modes section to `README.md` and `docs/configuration.md` with the table of `PUBLIC_URL` and `COMPOSE_FILE`
  values, the migration steps including deleting pinned identity URLs and signing in again, and the warning beside the
  manual route that it leaves the first-caller race; verify the steps run as written on the throwaway stack
- [ ] 7.2 Add proxy recipes for Tailscale serve, Cloudflare Tunnel, Traefik and Nginx Proxy Manager, each stating what
  address Caddy sees, when `TRUSTED_PROXIES` needs setting, and to keep `CADDY_HTTP_PORT` off the network; verify at
  least the Tailscale serve recipe end to end and mark any recipe not run as untested in the doc
- [ ] 7.3 Add `docs/troubleshooting.md` entries for certificate failures (DNS, closed port 80, where the error appears)
  and for a changed `PUBLIC_URL`; verify they match the messages seen during the checks
- [x] 7.4 Update the Commands, Configuration and Gotchas sections of `CLAUDE.md` for the overlay, the setup order and the
  `.env`-pinned identity URLs; verify nothing there still says Caddy is HTTP-only or that the first person signs up by hand

## 8. End-to-end check

- [ ] 8.1 On a host with a real domain, run the setup script in domain mode and verify a publicly trusted certificate,
  the port 80 redirect, that 8080 and every internal port are closed from outside, and that a certificate survives
  recreating Caddy. If no such host is available, stop here and tell the user rather than ticking this
- [ ] 8.2 On a physical phone with a release build, connect to that address and verify the gate, sign-in and receipts
  work, that a second account gets the rejected state, and that a 429 on refresh leaves the person signed in
