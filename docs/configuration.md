# Configuration

Everything is environment variables, read from `.env` next to `compose.yaml`. There is no
configuration file to mount: a file layered on top of the environment would silently override
whatever compose sets, which is a very hard problem to spot.

`.env.example` is the reference copy, and `scripts/setup.sh` generates the secrets. Do not quote
values — Compose keeps the quotes as part of the value, and a quoted JWK set then fails to parse.

## Configuration versus settings

Two different things, deliberately kept apart.

**Configuration** is what would break connectivity or security on a bad value, and is baked into
objects built once at startup — the database connection, the Ollama address, the JWT bearer scheme.
It lives here, and a bad value fails the process on boot rather than on first use.

**Settings** are what you might reasonably retune while running: how often to poll, how many retries
to allow, which model to use. Those live in the database and are edited through
`PUT /api/settings/pipeline`, with no restart. They are not in this file, and putting them here would
not work.

The one value that spans both is the model name — see [`OLLAMA_MODEL`](#extraction).

## Stack

| Variable | Default | Notes |
|---|---|---|
| `COMPOSE_PROJECT_NAME` | `kvitteria` | Container and volume name prefix. Change only to run two stacks on one host. |
| `IMAGE_REGISTRY` | `ghcr.io/glitzyworm` | Lowercase, as registries require. |
| `BACKEND_VERSION` | `latest` | `0.1.0`, `0.1`, `0`, or `latest`. While this is pre-1.0 the floating major is not a stability promise. |
| `GOTRUE_VERSION` | `v2.177.0` | |
| `POSTGRES_VERSION` | `17` | Changing the major after the first start needs a dump and restore, not just a restart. |
| `CADDY_VERSION` | `2-alpine` | |
| `OLLAMA_VERSION` | `latest` | |

## Networking

| Variable | Default | Notes |
|---|---|---|
| `PUBLIC_URL` | `http://localhost:8080` | The origin clients use. It becomes the token issuer that both GoTrue and the backend validate against, so it has to be the address a *client* reaches — not an internal one. |
| `CADDY_HTTP_PORT` | `8080` | Host port for plain HTTP. Change it and `PUBLIC_URL` together. In public-domain mode Caddy publishes 80 and 443 instead and this is unused. |
| `COMPOSE_FILE` | unset | Selects the mode below. `:` separates files on Linux and macOS, `;` on Windows. Setting it turns off the automatic pickup of `compose.override.yaml`. |
| `TRUSTED_PROXIES` | `private_ranges` | Whose forwarded client address Caddy believes, as comma-separated CIDR ranges. See [Client addresses](#client-addresses-and-rate-limits). |

Everything else is reachable only on the compose network. Nothing else publishes a port.

### Modes

`PUBLIC_URL` says *where* the stack is. It cannot also say who terminates TLS, because a stack with its own
domain and one behind someone else's proxy both have an `https://` address. `COMPOSE_FILE` carries that second
fact.

| Mode | `PUBLIC_URL` | `COMPOSE_FILE` | Who terminates TLS | Published ports |
|---|---|---|---|---|
| Local | `http://localhost:8080` | unset | nobody (plain HTTP) | 8080, plus the development overlay's |
| Behind a proxy | `https://...` | `compose.yaml` | your proxy | 8080 |
| Public domain | `https://...` | `compose.yaml:compose.public.yaml` | Caddy, with a Let's Encrypt certificate | 80, 443 |

Both HTTPS modes name their files, which leaves out `compose.override.yaml`. That file is the development
overlay: it would run the backend in Development and publish Postgres and GoTrue on the host. Public modes are
for deployment, so only Caddy publishes a port. `./scripts/setup.sh` writes all of this for you.

**Public domain** needs DNS pointing at the host, ports 80 and 443 reachable from the internet, and Docker Compose
2.24.4 or newer (the overlay replaces the base file's port list with `!override`). Caddy obtains the certificate
on first use, renews it, redirects port 80 to HTTPS, and keeps the certificate in the `caddy_data` volume, so
recreating the container does not request a new one.

**Behind a proxy** leaves Caddy on plain HTTP on `CADDY_HTTP_PORT`. Keep that port off the network: bind it to
loopback or firewall it, because a client that reaches it directly can forge the address it is rate limited
under. Recipes are in [Putting a proxy in front](#putting-a-proxy-in-front).

**Moving an existing install to a domain:**

1. Point DNS at the host and open ports 80 and 443.
2. Set `PUBLIC_URL=https://your.domain`, **delete any `GOTRUE_SITE_URL` and `GOTRUE_URI_ALLOW_LIST` lines** from
   `.env` (older `.env.example` files pinned them to localhost, and a pinned value wins over `PUBLIC_URL`), and set
   `COMPOSE_FILE=compose.yaml:compose.public.yaml`. Behind a proxy, set `COMPOSE_FILE=compose.yaml` instead.
3. `docker compose up -d`. Caddy requests the certificate on first use.
4. **Sign in again from every client.** Tokens already issued name the old issuer. Rolling back is the reverse:
   restore the old `PUBLIC_URL`, drop `COMPOSE_FILE`, restart. No data moves.

> **Without the setup script, the first caller claims the household.** Whoever signs in first on an empty
> household becomes its admin. `setup.sh` and `setup.ps1` create the admin *before* the entrypoint starts, so
> nobody outside the host can reach an unclaimed household. If you start the stack by hand, do not let it be
> reachable from outside until you have signed in yourself:
> `./scripts/token.sh you@example.com 'a-good-password' --signup`.

### Client addresses and rate limits

Caddy states the client address once, in `X-Real-IP`, overwriting whatever the client sent. GoTrue and the backend
both rate limit on that header alone, so a client cannot choose its own address.

Caddy believes a *forwarded* address (`X-Forwarded-For` from your proxy) only from a trusted proxy. The default is
`private_ranges`, which is what a proxy on the same host, Tailscale serve, a Cloudflare Tunnel daemon or a
sibling container looks like. In public-domain mode the overlay trusts nobody (`127.0.0.1/32`), and every client
is limited under the address of its own connection. If your proxy has a *public* address, set
`TRUSTED_PROXIES` to its range, or every client behind it shares one limit.

| Limit | Per client address | Where |
|---|---|---|
| Sign-in and token refresh | 30 per 5 minutes after a burst of 30 | GoTrue (`GOTRUE_RATE_LIMIT_TOKEN_REFRESH`) |
| Sign-up | 30 per 5 minutes after a burst of 30 | GoTrue (`GOTRUE_RATE_LIMIT_OTP`) |
| `/api/*` | 300 per minute | the backend |

Password and refresh grants share one limiter, so the sign-in figure is also the password-guessing budget. A
request over a limit gets 429 and is not evaluated. `/health/*` and `/version` are never limited.

### Putting a proxy in front

Each recipe leaves Caddy on plain HTTP. Set `PUBLIC_URL` to the address the proxy serves and
`COMPOSE_FILE=compose.yaml`. In each, Caddy sees the proxy as a private-network peer, so the default
`TRUSTED_PROXIES` already lets it name the client. Keep `CADDY_HTTP_PORT` off the network.

- **Tailscale serve** *(untested)*: `tailscale serve --bg --https=443 http://127.0.0.1:8080`, with
  `PUBLIC_URL=https://<machine>.<tailnet>.ts.net`. Tailscale terminates TLS with its own certificate and connects
  from the host, so Caddy sees a private address and honours the forwarded client. Not run with Tailscale itself;
  the same arrangement with a throwaway TLS-terminating Caddy in front was, and sign-in, the new issuer and the
  published ports all behaved.
- **Cloudflare Tunnel** *(untested)*: point a public hostname at `http://localhost:8080` (or `http://caddy:8080` if
  `cloudflared` is a container in this project). `PUBLIC_URL` is that hostname. The daemon connects from the host or
  the compose network, so the default holds.
- **Traefik** *(untested)*: route the host to `http://caddy:8080` on a shared network, with `PUBLIC_URL` as the
  router's host. Traefik connects from the Docker network, a private range. Make sure it overwrites client-sent
  forwarding headers rather than appending to them.
- **Nginx Proxy Manager** *(untested)*: add a proxy host for your domain forwarding to `caddy` port `8080` (or the
  host's `8080`) and request a certificate there. If it runs on another machine with a public address, set
  `TRUSTED_PROXIES` to that address.

## Database

| Variable | Default | Notes |
|---|---|---|
| `POSTGRES_DB` | `kvitteria` | |
| `POSTGRES_USER` | `postgres` | |
| `POSTGRES_PASSWORD` | — | Generated by setup. **Changing it after the first start requires `docker compose down -v`**: the volume was initialized with the old one. |

One database holds two schemas: `public` for the application, `auth` for identities. That is why one
`pg_dump` is a complete backup.

The backend connects as a superuser, so Postgres row-level security is not part of the security model
and the backend is the sole enforcement point. This is also why no Data API is exposed — there is
nothing here for one to front.

## Identity

| Variable | Default | Notes |
|---|---|---|
| `GOTRUE_JWT_KEYS` | — | The ES256 key set, as a JSON **array** of JWKs on one line. A `{"keys": [...]}` wrapper is rejected at startup, and each key needs `"key_ops": ["sign", "verify"]` or startup fails with `no signing key detected`. Generated by setup. |
| `GOTRUE_JWT_SECRET` | — | GoTrue's config loader requires a symmetric secret whether or not asymmetric keys are set. With the key set present it goes unused. It is never given to the backend. |
| `GOTRUE_SITE_URL` | `PUBLIC_URL` | Base for email links and the redirect allow-list. Leave it out of `.env`: a pinned value wins over `PUBLIC_URL` and goes stale when that changes. |
| `GOTRUE_URI_ALLOW_LIST` | `PUBLIC_URL` | Exact URLs auth may redirect to. Comma-separated. Leave it out for the same reason. |
| `GOTRUE_RATE_LIMIT_TOKEN_REFRESH` | `30` | Password and refresh grants allowed per client address per 5 minutes, after a burst of 30. |
| `GOTRUE_RATE_LIMIT_OTP` | `30` | Sign-ups allowed per client address per 5 minutes, after a burst of 30. |
| `GOTRUE_JWT_EXP` | `3600` | Access-token lifetime in seconds. Clients refresh; this is not the session length. |
| `GOTRUE_DISABLE_SIGNUP` | `false` | Members sign themselves up, and the backend rejects anyone unrecognised: a stranger gets an account and a 403, and the limits above bound how many they can try. `true` closes sign-up for good, which also stops new members joining, because they could not create the account an admin pre-authorized. Close it only once everyone has signed up. |
| `GOTRUE_MAILER_AUTOCONFIRM` | `true` | No SMTP is configured anywhere, so an unconfirmed address could never confirm itself. **Turning this off without configuring SMTP first locks everyone out.** |
| `GOTRUE_PASSWORD_MIN_LENGTH` | `8` | |
| `GOTRUE_LOG_LEVEL` | `info` | |

### Why the issuer and the JWKS URL are different values

The token's `iss` is the URL the *client* reached auth on — through the proxy, so
`http://localhost:8080/auth/v1`. The backend fetches signing keys by the container's own name,
`http://gotrue:9999`. They are the same service at two addresses, and making them equal presents as a
401 with no explanation.

Both are set for you in `compose.yaml` and derive from `PUBLIC_URL`.

## Extraction

| Variable | Default | Notes |
|---|---|---|
| `OLLAMA_MODEL` | `qwen3.5:4b` | Read twice: Ollama pulls this tag, and the backend seeds it into its pipeline settings. That is what stops the pipeline asking for a model nobody pulled. |
| `OLLAMA_REQUEST_TIMEOUT_SECONDS` | `1200` | Generous because CPU extraction takes minutes. |

`OLLAMA_MODEL` **seeds only.** Once the settings row exists, this variable no longer affects it —
change the model through `PUT /api/settings/pipeline`, which verifies it is pulled before saving. On
an existing install, editing `.env` changes what Ollama pulls and nothing else.

## Runtime

| Variable | Default | Notes |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` | `Development` additionally serves `/openapi/v1.json` and the Scalar reference at `/scalar`, and logs at Debug. |
| `TZ` | `Europe/Copenhagen` | Danish purchase dates are resolved against this, including across the spring-forward gap. |
| `AUTH_ALLOW_INSECURE_JWKS_TRANSPORT` | `true` | See below. |

### `AUTH_ALLOW_INSECURE_JWKS_TRANSPORT`

Whoever can answer the key fetch decides which tokens are valid, so fetching keys over plain HTTP is a
token-forgery vector and HTTPS is otherwise required.

It is `true` here because that fetch is a hop between two containers on a private network, where there
is no certificate to present. It is an explicit opt-out rather than something inferred from the
environment name, so a deployment genuinely serving its JWKS in the clear has said so in one
auditable place.

Set it to `false` if you move identity to a host that terminates TLS.

### `Auth__SharedSecret`

Not in `.env`, and should not be. The backend accepts a symmetric signing secret only in Development,
where it takes precedence over the key set. Anywhere else it is rejected at startup, because honouring
it would downgrade a deployment that already has working asymmetric keys to HS256 — silently.

## Hosted identity

`AppUser.AuthUserId` is deliberately not a foreign key to `auth.users`, so identity can relocate
without a schema change. To point at a hosted Supabase project instead of the bundled GoTrue:

- `Auth__Issuer` → `https://<ref>.supabase.co/auth/v1`
- `Auth__JwksUrl` → `https://<ref>.supabase.co/auth/v1/.well-known/jwks.json`
- `AUTH_ALLOW_INSECURE_JWKS_TRANSPORT` → `false`, since that fetch is now genuinely over TLS

Two things to get right if you also move the database there: **turn the Data API off**, because it is
on by default and would expose every table directly, bypassing this application's authorization
entirely; and connect as a role narrower than `postgres`.
