-- Runs once, on an empty data directory, as POSTGRES_USER against POSTGRES_DB. Re-running it means
-- destroying the volume: `docker compose down -v`.
--
-- GoTrue migrates its own `auth` schema on first boot, but its migrations were written against a
-- cluster a Supabase project had already provisioned. They GRANT to roles that exist there and
-- nowhere else, and they call functions from pgcrypto. On a plain postgres image the first boot
-- otherwise fails with `role "..." does not exist`, which reads like a bug in this stack rather than
-- a missing prerequisite.

-- Grant targets only. NOLOGIN, because nothing authenticates as them: this backend is the sole
-- enforcement point and there is no PostgREST here for them to front.
DO $$
BEGIN
	IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'anon') THEN
		CREATE ROLE anon NOLOGIN NOINHERIT;
	END IF;

	IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'authenticated') THEN
		CREATE ROLE authenticated NOLOGIN NOINHERIT;
	END IF;

	IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'service_role') THEN
		CREATE ROLE service_role NOLOGIN NOINHERIT BYPASSRLS;
	END IF;
END
$$;

-- GoTrue owns `auth`; Entity Framework owns `public`. One database, two schemas, so a single backup
-- covers the members, their receipts, and the identities they sign in with.
CREATE SCHEMA IF NOT EXISTS auth;
GRANT USAGE ON SCHEMA auth TO anon, authenticated, service_role;

-- pgcrypto: used by GoTrue's own migrations.
-- citext: required by the backend for case-insensitive names and for matching a token's email
-- against a pre-authorized member. EF's first migration creates it too; doing it here means that
-- statement is a no-op even if the application role is ever narrowed to something without CREATE.
CREATE EXTENSION IF NOT EXISTS pgcrypto;
CREATE EXTENSION IF NOT EXISTS citext;
