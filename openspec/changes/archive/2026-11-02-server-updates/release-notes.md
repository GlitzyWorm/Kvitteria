# Release notes draft

For the first backend release carrying a stack bundle. Replace `<version>` with that release.

## Updating from 1.1.0 or older

Updates now go through `./scripts/update.sh`, which an existing install doesn't have yet, so this one step is
manual. From the directory holding `compose.yaml`:

```bash
curl -fsSL https://github.com/GlitzyWorm/Kvitteria/releases/download/backend-v<version>/kvitteria-stack-<version>.tar.gz | tar -xz scripts/update.sh
```

```bash
bash scripts/update.sh
```

That backs up the database, replaces the stack's files, pins `BACKEND_VERSION` and checks the new backend. From
then on, `./scripts/update.sh` is all an update takes.

To start updates from the app on a Linux host with systemd, run setup once more as root. It adds the update agent
and leaves `.env` alone:

```bash
sudo ./scripts/setup.sh
```

Without that, the app still tells you when a release is out and shows the command to run.

The backend now asks GitHub once a day whether a newer release exists. To turn that off, set
`UPDATE_CHECK_ENABLED=false` in `.env`.
