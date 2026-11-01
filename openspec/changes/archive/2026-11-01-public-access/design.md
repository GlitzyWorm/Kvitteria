# Design

## Context

See proposal.md for motivation. The relevant current state:

- Caddy is the only service with a published port in `compose.yaml`. Its Caddyfile has a bare `:8080` site and
  `auto_https off`.
- `compose.override.yaml` is picked up automatically in any checkout. It sets Development and publishes the
  backend, Postgres, GoTrue and Ollama on the host. The setup scripts run from a checkout, so anything they start
  gets it unless `COMPOSE_FILE` says otherwise.
- `PUBLIC_URL` already feeds `Auth__Issuer`, GoTrue's `API_EXTERNAL_URL` and `GOTRUE_JWT_ISSUER`.
  `GOTRUE_SITE_URL` and `GOTRUE_URI_ALLOW_LIST` have their own localhost defaults in `compose.yaml`, **and** are
  written into every `.env` by `.env.example`, so changing the compose default alone would do nothing.
- The first authenticated caller is bootstrapped into admin by `UserProvisioning.Decide`, under an advisory
  lock. There is no endpoint for it, and that is deliberate.
- Members join by signing up themselves after an admin pre-authorizes their email. GoTrue's invite and
  create-user calls need a `service_role` key, which the project avoids having anywhere.
- The `/auth/v1/*` routes go straight from Caddy to GoTrue, so the backend never sees them.
- GoTrue v2.177.0 (read from its source) rate limits `/signup` and `/token`, password grant and refresh alike, but
  `performRateLimiting` does nothing unless `GOTRUE_RATE_LIMIT_HEADER` names a header. It is unset here, so today
  there is no limit at all.
- Caddy replaces `X-Forwarded-For` from any peer that is not in `trusted_proxies`, which by default is none.
- `GoTrueTokenClient.refresh` maps every non-2xx answer to `RefreshOutcome.Rejected`, which clears the session.

## Goals / Non-Goals

**Goals:**
- One address to configure, and nothing else to edit to move between modes.
- A stack on a public domain is safe by default: no first-caller window when the script is used, and bounded
  request rates.
- A household without a domain loses nothing and is not asked to understand certificates.

**Non-Goals:**
- Closing signup automatically after the admin exists. That would break how members join, and a backend-mediated
  invite needs the `service_role` key (see Risks). `GOTRUE_DISABLE_SIGNUP` stays as a manual switch.
- Terminating TLS for a private CA or self-signed certificate. The app would not trust it.
- Hosted identity, SMTP, or password reset.
- A web app. Caddy's `/srv` route is untouched.
- Making the limits configurable in the backend. A constant is enough until someone needs otherwise.

## Decisions

### 1. The base file stays plain HTTP; a public overlay adds TLS

`compose.yaml` keeps today's behaviour. The Caddyfile drops the global `auto_https off` and reads its site address
as `{$SITE_ADDRESS::8080}`. A bare port is not a qualifying domain, so Caddy attempts no certificate and opens no
redirect listener for it. `auto_https off` must go rather than become a variable: the option has no `on` value, and
any value other than its four keywords fails at parse time, so `{$AUTO_HTTPS:off}` could never be switched.

`compose.public.yaml` sets `SITE_ADDRESS: ${PUBLIC_URL}`, using the whole URL because compose interpolation cannot
extract a host and Caddy accepts a URL as a site address. It replaces the port list with 80, 443 and 443/udp using
`ports: !override`, since compose otherwise appends and would leave the 8080 mapping behind. Certificates live in
the existing `caddy_data` volume.

Alternatives considered:
- *Compose profiles.* They cannot change an existing service's ports or environment, only whether a service
  runs, so they do not fit.
- *Always on with a domain required.* It breaks the localhost emulator workflow for everyone.

### 2. `COMPOSE_FILE` selects the mode, and HTTPS modes leave out the dev override

`PUBLIC_URL` says where the stack is. It cannot also say who terminates TLS, because domain and proxy modes both use
an `https://` value. The overlay carries that second fact, opted into through `COMPOSE_FILE` as `compose.gpu.yaml`
already is.

| Mode | `PUBLIC_URL` | `COMPOSE_FILE` |
|---|---|---|
| Local | `http://localhost:8080` | unset |
| Behind a proxy | `https://...` | `compose.yaml` |
| Public domain | `https://...` | `compose.yaml` + `compose.public.yaml` |

Both HTTPS modes set `COMPOSE_FILE` so that `compose.override.yaml` stays off. Without that, a stack set up from a
checkout would run in Development with Postgres and GoTrue published on the host, and GoTrue's published port
bypasses the client-address header and therefore the limit. The images then come from GHCR, like any deployment.

The separator is `:` on Linux and macOS and `;` on Windows, so `setup.ps1` writes `;`.

### 3. Identity URLs derive from `PUBLIC_URL`, and `.env.example` stops pinning them

`GOTRUE_SITE_URL` and `GOTRUE_URI_ALLOW_LIST` default to `${PUBLIC_URL:-http://localhost:8080}` in `compose.yaml`
and both lines leave `.env.example`, which is what actually makes them derive. An existing `.env` keeps its pinned
values, so the migration steps say to delete them.

### 4. The setup script claims before Caddy starts

The first-run order is the safety property:

1. Collect the address and mode (fresh install) and the admin credentials, before the model download.
2. `docker compose up -d --wait backend gotrue`. Nothing depends on Caddy, so it stays down and no network port is
   open in an HTTPS mode.
3. Read the `household` check through `docker compose exec -T backend curl .../health/ready`. If it is Healthy, skip
   the claim.
4. Sign up against `http://gotrue:9999/signup` and call `GET /api/users/me` on the backend, both through `exec -T`
   with the body on stdin so the password is never in an argument list. A `user_already_exists` answer falls back to
   the password grant, which is how an interrupted run completes.
5. Re-read `household`; only when Healthy run `docker compose up -d`, which starts Caddy.

Compose's own ordering would not do this: Caddy `depends_on` a healthy backend, so a plain `up -d` opens the
entrypoint with the household unclaimed and leaves a window of seconds. Going through `exec` also means the claim
needs neither DNS, a certificate, nor the deployer's proxy to be in place. The backend image already has `curl` for
its healthcheck. JSON is handled on the host by `python3`, which the scripts already require.

No new endpoint is needed and the backend's decision logic is untouched. A deployer who skips the script keeps the
first-caller race, and the README says so next to the manual route. The script's existing health loop against
`PUBLIC_URL` goes away, since that address may not work yet.

`setup.sh` also gains the `GOTRUE_JWT_SECRET` generation that `setup.ps1` has. GoTrue marks it required, so a fresh
`setup.sh` install cannot currently start.

Alternatives considered:
- *A one-time claim code.* Stronger for hand deployers, but it needs a backend change and an app field on the
  claim screen. Deferred.
- *Closing signup afterwards.* Rejected, see Risks.

### 5. Caddy states the client address once, in `X-Real-IP`

A `request_header X-Real-IP {client_ip}` at the top of the site block overwrites anything a client sent, and both
GoTrue and the backend read that one header. A dedicated header is used rather than `X-Forwarded-For` because that
can hold a list, GoTrue uses the raw header value as its limiter key, and a list would make the key forgeable.

Caddy's `servers` global option takes `trusted_proxies static {$TRUSTED_PROXIES:private_ranges}` and
`trusted_proxies_strict`, which reads the forwarded list right to left so an appended value cannot be spoofed from
the left. Compose defaults the variable to `private_ranges`, the peer a host-side proxy, Tailscale serve, a Tunnel
daemon or a sibling container appears as, so proxy mode needs no second setting. `compose.public.yaml` pins it to
`127.0.0.1/32`, meaning nobody: nothing connects to Caddy from its own loopback, and on a dual-stack host an IPv6
client can arrive from the Docker gateway, which is a private address.

This replaces any need to tune `ForwardLimit` per deployment. The trust decision lives in one place, and everything
behind Caddy sees exactly one hop.

### 6. Sign-in and sign-up use GoTrue's own limits

`compose.yaml` sets `GOTRUE_RATE_LIMIT_HEADER: X-Real-IP`, which activates limits GoTrue already has. No custom
Caddy build or module is needed, so the stock image stays and there is nothing new to publish.

Starting values, per address: `/signup` keeps GoTrue's default of 30 per 5 minutes through `GOTRUE_RATE_LIMIT_OTP`.
`/token` lowers `GOTRUE_RATE_LIMIT_TOKEN_REFRESH` from 150 to 30 per 5 minutes, because password and refresh grants
share that limiter and a household with hour-long access tokens refreshes rarely. GoTrue's burst of 30 is fixed in
its source. So password guessing starts at 30 tries and then runs at about 6 a minute per address. Both are compose
variables with these defaults.

Alternatives considered:
- *A Caddy rate-limit module.* It needs a custom image built with `xcaddy` and published, and does nothing GoTrue
  cannot do once the header is set.

### 7. The API is limited in the backend

ASP.NET Core's built-in rate limiter, attached to the `/api` group with `RequireRateLimiting`, so `/health/*` and
`/version` are outside it by construction. A sliding window of 300 requests a minute, partitioned by
`Connection.RemoteIpAddress`. That is deliberately generous, because a household shares one address and a screen
load fires several requests.

The limiter's default rejection status is 503, so `RejectionStatusCode` is set to 429, and `OnRejected` writes
problem details and `Retry-After`. Being a per-group policy, it changes nothing in the OpenAPI document. The
partition key lives in a small class under `Features/` so `Backend.Tests` can reach it.

Alternatives considered:
- *Limiting everything in Caddy.* One mechanism, but it ties the backend's protection to a particular proxy
  and needs the custom build.

### 8. The backend trusts `X-Real-IP` from private networks only

`UseForwardedHeaders` runs first in the pipeline with `ForwardedForHeaderName = "X-Real-IP"`, `ForwardLimit = 1` and
`KnownIPNetworks` set to the private ranges (`10/8`, `172.16/12`, `192.168/16`, `fc00::/7`). The compose subnet is
assigned by Docker and can't be named statically, and pinning one risks colliding with the host's own network. The
private ranges are safe because the backend has no published port outside the development override, so only compose
peers can reach it.

`KnownIPNetworks` is required rather than `KnownNetworks`: the latter is obsolete in .NET 10 and `Backend.csproj` has
`TreatWarningsAsErrors`.

### 9. The app treats 429 and 5xx on refresh as transient

`GoTrueTokenClient.refresh` returns `RefreshOutcome.Unreachable` for 429 and any 5xx. Other client errors stay
`Rejected`. Rate limiting makes this necessary rather than a nicety: without it, one limited refresh signs a person
out, and a proxy's 502 during a restart already does. Sign-in needs no change, since `AuthError` already maps 429 to
`RATE_LIMITED`, and `SafeCall` already maps API 429s.

## Risks / Trade-offs

- **Signup stays open** → The backend answers 403 to non-members, so strangers get a row in `auth.users` and
  nothing else. Closing it would need the `service_role` key and a backend-mediated invite, a larger design change
  that is out of scope. Rate limiting on `/auth/v1/*` is therefore required, not optional.
- **The limit depends on a header** → If Caddy stopped sending `X-Real-IP`, GoTrue would log a warning and silently
  not limit. The manual verification covers it, and `docs/troubleshooting.md` names the log line.
- **Shared addresses** → A home connection shares a limit, and so does everyone behind a proxy with a public address
  until `TRUSTED_PROXIES` is set. Docker Desktop also hides client addresses behind its gateway, so all clients share
  one limit there. That fails towards being limited, not towards being unprotected.
- **`trusted_proxies` default in proxy mode** → A LAN client that can reach `CADDY_HTTP_PORT` directly can forge its
  rate-limit address. Bind the port to loopback or firewall it when a proxy is in front; the recipes say so.
- **`COMPOSE_FILE` hides the dev override** → Intended here, and already true of the GPU overlay. Public modes are for
  deployment, not development.
- **Compose version** → `!override` needs 2.24.4. An older Compose fails with a parse error on the overlay, and the
  script checks the version before enabling it.
- **Let's Encrypt limits and DNS mistakes** → A wrong DNS record or closed port 80 shows as a certificate error
  in Caddy's logs. `docs/troubleshooting.md` gets an entry saying where to look.
- **Wrong `PUBLIC_URL` after the first start** → Tokens already issued name the old issuer. Changing it means
  clients sign in again; the documentation says so.
- **Hand deployers keep the first-caller race** → Documented, and an option for later (claim code).
- **The app's claim screen** → After a scripted setup it is unreachable, which is correct. It stays for hand
  deployers and local use.

## Migration Plan

For an existing local install nothing changes: `PUBLIC_URL`'s default is the same, the site still binds `:8080`, and
the identity URLs still resolve to localhost. The new limits apply, and the app's refresh fix should ship first or
together with them.

To move to a domain:

1. Point DNS at the host and open ports 80 and 443.
2. Set `PUBLIC_URL=https://...`, delete any `GOTRUE_SITE_URL` and `GOTRUE_URI_ALLOW_LIST` lines from `.env`, and set
   `COMPOSE_FILE` to include the overlay. Behind a proxy, set `COMPOSE_FILE=compose.yaml` instead.
3. `docker compose up -d`. Caddy requests the certificate on first use.
4. Sign in again from each client, because the issuer changed.

Rollback is the reverse: restore the old `PUBLIC_URL`, drop `COMPOSE_FILE`, restart. No data migrates.

## Open Questions

None block the work. The limit values in decisions 6 and 7 are starting points to tune after the first deployment.
