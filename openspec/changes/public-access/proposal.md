# Proposal

## Why

A physical phone cannot use Kvitteria today. Caddy is pinned to plain HTTP on `:8080` with `auto_https off`,
and release builds of the app refuse cleartext, so the stack works only on an emulator. The project is also
meant to be run by other households, some on a public domain and some without one, so the defaults have to be
safe when the stack is reachable by strangers. Right now the first caller to sign in claims the household as
admin, signup is open, and nothing limits request rates: GoTrue's built-in limits are inactive unless it is told
which header carries the client address, and that is not set.

## What Changes

- `PUBLIC_URL` is the one address the stack is configured with. The issuer, GoTrue's external URL, its site URL
  and its redirect allow list all derive from it, so they cannot drift apart.
- Three modes, chosen by that value and by whether the overlay is on. An `http://localhost` value keeps today's
  behaviour. An `https://` value behind a proxy someone already runs leaves Caddy on plain `:8080`. An `https://`
  value with the new `compose.public.yaml` overlay makes Caddy obtain and renew certificates itself and publish
  ports 80 and 443.
- The setup scripts ask for the address and the mode on a fresh install, and sign up the first admin **before the
  entrypoint starts**, so nobody outside the host can reach an unclaimed household. Signup stays open afterwards:
  the backend answers 403 to anyone who is not a member, and members still join by signing up after an admin
  pre-authorizes their email.
- Caddy states the real client address to the services behind it, and only a configured proxy can influence that
  address.
- Sign-in and sign-up are rate limited per client address by turning on GoTrue's own limits. API requests are
  rate limited per client address in the backend. Neither limit depends on which proxy is in front.
- **The app keeps its session when a token refresh is rate limited or the server is failing.** Today any non-2xx
  answer ends the session, so a 429 on refresh would sign the person out.
- Documentation covers each mode, including recipes for putting Tailscale serve, Cloudflare Tunnel, Traefik or
  Nginx Proxy Manager in front, and says plainly that skipping the setup script leaves the first-caller race.
- `setup.sh` starts generating `GOTRUE_JWT_SECRET`, which GoTrue requires and only `setup.ps1` writes today.
- The Android release build stays HTTPS-only. The debug-only cleartext exception is unchanged.

## Capabilities

### New Capabilities
- `deployment/public-url`: how `PUBLIC_URL` and the overlay select between local, public-domain and behind-a-proxy
  modes, and which values derive from `PUBLIC_URL`.
- `deployment/first-run-setup`: the setup scripts create the first admin before the entrypoint is reachable, and
  report ready only when that has happened.
- `deployment/request-protection`: rate limiting on the auth and API routes, and how the client address they key
  on is established.

### Modified Capabilities
- `mobile/session`: a refresh answered by rate limiting or a server failure no longer ends the session. The
  household-claim behaviour in the backend and the app's claim screen are unchanged; the scripts use the former,
  and a deployer who skips the script still sees the latter.

## Impact

- `compose.yaml`, new `compose.public.yaml`, `caddy/Caddyfile`, `.env.example`. The stock Caddy image is kept.
- `scripts/setup.sh` and `scripts/setup.ps1`.
- `Kvitteria/Backend/Program.cs` and a small new class under `Features/` for forwarded headers and rate limiting.
  Both are in the shared framework, so no package is added.
- `android/core/data` `GoTrueTokenClient` and its test.
- `README.md`, `CLAUDE.md`, `docs/configuration.md` and `docs/troubleshooting.md`.
- Compose 2.24.4 or newer is needed for the public overlay, which replaces the base file's port list.
- No database migration and no change to the OpenAPI document.
