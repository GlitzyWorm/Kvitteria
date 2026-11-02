# Design

## Context

See proposal.md for motivation. The constraints that shape this:

- The backend has no Docker socket and publishes nothing but what Caddy proxies. That property is deliberate:
  "Caddy is the single entrypoint" is the project's attack-surface statement.
- `MigrateDatabaseAsync` runs before the host starts, forward only. A failed migration crash-loops under
  `restart: unless-stopped`.
- Durable intake makes a restart cheap: `MailWorker` re-queues whatever was in progress, so an update costs at most
  one extraction attempt.
- One `pg_dump` is a complete backup, including the Data Protection key ring.
- `/version` is anonymous and deliberately reveals only the build.
- An install is a directory of files with no git history; the home server's came from `git archive`.
- `/health/ready` answers 503 for an unhealthy report, and an unpulled Ollama model is enough to cause that.
- Release-please creates the GitHub release before `publish-backend` pushes the image, in the same workflow.

## Goals / Non-Goals

**Goals:**
- One update implementation, with the console, the app and a later web interface as triggers.
- Nothing destructive happens unattended.
- An update started from a phone can be followed from that phone, including when it fails.

**Non-Goals:**
- Zero downtime. A household can tolerate minutes.
- Authenticating bundles beyond TLS and a checksum. The trust root is the GitHub repository, as it already is for
  the image.

## Decisions

### Update path

```
                 GitHub Releases (backend-v*, with bundle)
                        |
                        v  daily
   +----- backend: UpdateChecker --> GET /api/system/update
   |                                     |           |
   |                                    app      web (later)
   |                                     |           |
   |                  POST /api/system/update <------+
   |                                     |
   |                                     v
   |                     update-state/request
   |   stack                             |
---+-------------------------------------+---------------------
       host                              v
                          kvitteria-update.path (systemd)
                                         |
  ./scripts/update.sh ------------> update.sh
                                         |
                                         v
       update-state/status.json, public/update-status.json
              |                                |
           backend                      Caddy /update-status
```

### A host agent rather than anything with the socket

A systemd path unit watches a bind-mounted directory and starts a oneshot service that runs `update.sh`.

Alternatives: the socket in the backend makes an API compromise root on the host. A sidecar with the socket is still
root-equivalent, is part of the stack it replaces, and has to run compose against paths that exist on the host rather
than in the container. Watchtower-style tools swap images and would miss the compose and Caddy files.

The cost is that only Linux hosts with systemd get the button, which is accepted. Others get the notice and the
command.

The file's contents are ignored, so a compromised backend can trigger only "update to the newest release", which
an admin could do anyway. It cannot choose a version, so it cannot force a downgrade across a migration.

### The state directory

`./update-state/` is a gitignored bind mount, owned by `$APP_UID` by `setup.sh`:

```
update-state/
  request             created by the backend, removed by the agent
  heartbeat           touched by a systemd timer
  status.json         state, target, detail, updated-at
  public/
    update-status.json     state, target, updated-at
```

The script only ever writes here. The backend owns this directory, so it trusts nothing it could find in it: the last
outcome also goes to `.update/state`, which no container can see, and that is what the `restore-required` refusal and
the boot cleanup read. Temporary files are built in `.update/` and renamed in, so root never follows a planted symlink.

Only `public/` is mounted into Caddy, read-only. The two status files exist because `detail` can name paths and
errors, and `/update-status` is anonymous. A directory mount rather than a file mount, because atomic rename replaces
the inode and a file bind mount would keep showing the old one.

If the directory is missing Docker creates it as root, and the backend simply can't write there. No heartbeat means
no button, so that failure is the same as having no agent.

### Caddy serves status

The backend is stopped for the riskiest part of an update, and stays stopped after `restore-required`. So status has
to come from something that's still up. Caddy stays up throughout. The route is anonymous, so it holds `state`,
`targetVersion` and `updatedAt` and nothing else. It sits outside the OpenAPI document, so `ContractTest` can't cover
it, and the client gets its own parsing test.

### "Ready" means the bundle exists

Rather than draft releases, which complicate tag creation under release-please, the bundle is uploaded in
`publish-backend` after the push. "Has a bundle" then answers "has an image" for every client with no extra state.
This requires `contents: write` on that workflow, and `workflow_dispatch` runs must check out the version's tag so a
re-publish bundles the right files.

### Discovery without `jq`

`setup.sh` doesn't require `jq`, and adding it for one lookup isn't worth it. The asset names carry everything needed:

```bash
curl -fsSL ".../releases?per_page=30" \
  | grep -oE 'https://[^"]*/kvitteria-stack-[0-9]+\.[0-9]+\.[0-9]+\.tar\.gz' | sort -V | tail -n 1
```

Only backend releases have this asset, and anonymous callers never see drafts. The backend parses the same response
properly, with the selection as a pure function in `Features/Update/ReleaseSelection.cs` so `Backend.Tests` covers
it. The two implementations are small enough to accept, because the script has to work with no backend running.

### Update order

```
 lock -> status running -> resolve target
   -> refuse (git checkout, older, Postgres major)
   -> download, verify checksum -> pull backend image        [no downtime yet]
   -> stop backend -> pg_dump -> record migration history
   -> keep previous files in .update/previous/
   -> unpack bundle -> hand over to the new update.sh -> pin BACKEND_VERSION
   -> up -d --no-deps backend -> verify
   -> up -d --no-deps (the rest) -> restart caddy if its config changed
   -> status succeeded
```

Stopping the backend before dumping means no receipt can land between the dump and the stop, so a restore loses
nothing the update didn't already pause. Pulling first keeps the downtime short.

**`--no-deps` is load-bearing.** Caddy `depends_on` a healthy backend, so a plain `up -d` while Ollama is unhealthy
would fail or leave Caddy down, and the status route with it.

**Handing over.** Bash reads a script as it runs, so replacing `update.sh` mid-run corrupts it. Once the bundle is
verified, the running script `exec`s the new copy with a `--continue` flag. This also means a release can fix the
update process that delivers it.

**A Caddyfile change is invisible to compose.** `up -d` compares configuration, not bind-mounted file contents, so
the script restarts Caddy itself when the file's hash changed. That restart can happen last, once the backend works.

### Success is judged by the script

`up --wait` isn't used, because it follows the healthcheck and therefore `/health/ready`, where an Ollama problem would
read as a failed update and could trigger a rollback for nothing. The script execs into the backend and checks that
`/version` equals the target and the `postgres` and `migrations` checks are healthy, for up to five minutes.

### Rollback (option A)

```
 fetch/pull/refusal fails           --> nothing changed; failed
 dump fails                         --> restart old backend; failed
 new backend fails, history same    --> restore files + pin; rolled-back
 new backend fails, history changed --> stop backend; restore-required
```

History is `SELECT "MigrationId" FROM "__EFMigrationsHistory"`, compared before and after. A migration that fails in
its own transaction leaves the history unchanged, so that case rolls back. A release with several migrations can leave
some applied, and that case stops.

Restoring automatically was rejected. It would drop the database, `auth` included, on the strength of a health check
that can fail for unrelated reasons, and it would lose anything ingested since.

`--restore` uses `DROP DATABASE ... WITH (FORCE)`, reloads the dump, puts back the files and pin from
`.update/previous/`, and starts the stack. It demands a typed confirmation and a terminal, so the agent can't reach it.

### Backend pieces need no database

The latest result is held in memory by a singleton `UpdateChecker` (`BackgroundService`), so a restart simply checks
again. `Update:Enabled` and `Update:Repository` are an options section with defaults, like `Auth:Audience`: the
repository is a project identity rather than a deployment address. They aren't in `PipelineSettings` because no client
needs to change them. No migration is needed, which also keeps this feature out of its own rollback calculation.

Reads follow the household rule and are open to members. The `POST` reuses `ManageSettings` rather than adding a
permission: updating is retuning the server, and an admin holds it implicitly either way. `/api` gives both routes the
rate limit and `active-user`. The `POST` takes no body for the same reason the agent ignores the file.

### The app

`UpdateState` is parsed by name with an `Unknown` fallback, like other enums. Konto loads the status alongside the
version. After the `POST` Konto stays in charge and polls, rather than handing over to the gate: the old server keeps
answering `/version` while the image downloads, so the gate would open the tabs and the server would vanish from under
them. Konto watches for an outcome, and only `restore-required` goes to the gate. The gate covers everyone else: a
person opening the app mid-update lands on a failed `/version`, and step 2 gains the `/update-status` read, on the
anonymous client since sessions are irrelevant there. The session is kept, as it already is for a failed `/version`.

## Risks / Trade-offs

- [`docker compose stop backend` may also stop Caddy as a dependent] → Checked on Compose 5.4.0: it stops the backend
  alone, and `/update-status` keeps answering. The script stops the container directly anyway, with `docker stop` on
  the ID from `compose ps -q`, so the guarantee doesn't rest on what one Compose version does.
- [The first update after this ships can't use any of it] → The release notes carry a one-line bootstrap, and it
  is exercised against a 1.1.0 install before release.
- [A release adds a required variable] → The release-bundle spec requires defaults, and review enforces that.
- [Old apps against a new server] → Out of scope. API changes stay additive until the app has a release channel.
- [Daily call to GitHub reveals an install's address to GitHub] → It's one anonymous request, and it can be disabled.
- [GitHub's anonymous rate limit of 60 an hour] → Daily polling is nowhere near it; the script runs once per update.
- [A stale `running` state strands the gate] → A thirty-minute limit on reads, and the agent replaces it at boot.
- [Disk fills with dumps] → The script keeps three, and a failed dump stops the update.
- [Windows hosts] → `update.sh` needs bash, so a Windows install uses WSL or Git Bash. There is no `update.ps1`.

## Migration Plan

1. Land `public-access` first, since this edits the same files.
2. Ship the bundle and `update.sh`. On an existing install, the bootstrap fetches the first bundle's `update.sh`, runs
   it, then re-runs `setup.sh` as root to add the agent and state directory.
3. Ship the check and the app together, since the app tolerates a 404 from an older server by showing no update row.

Rolling this feature back needs no data work: there is no schema, and the state directory is inert without the agent.
