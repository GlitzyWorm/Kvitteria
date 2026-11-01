#Requires -Version 7
<#
.SYNOPSIS
	Prepares .env, brings the Kvitteria stack up, and creates the first admin before anything is reachable.
.DESCRIPTION
	Safe to re-run: an existing .env is left alone, so the database password and signing keys survive,
	an already-claimed household asks for nothing, and compose reconciles whatever is already running.

	On a fresh install it asks for the public address and, for an https:// one, whether a proxy you run
	already terminates TLS. For an unattended run set these environment variables instead:

	  KVITTERIA_PUBLIC_URL      the address clients use (default http://localhost:8080)
	  KVITTERIA_TLS             proxy | domain   (https:// addresses only; default domain)
	  KVITTERIA_ADMIN_EMAIL     the first admin, while the household is unclaimed
	  KVITTERIA_ADMIN_PASSWORD  its password. Never written to a file or put in a process's arguments.
#>
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Set-Location (Join-Path $PSScriptRoot '..')

foreach ($tool in 'docker', 'openssl', 'python') {
	if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) {
		throw "$tool is required but was not found on PATH."
	}
}

docker compose version *> $null
if ($LASTEXITCODE -ne 0) { throw "'docker compose' is required (Compose v2)." }

$defaultUrl = 'http://localhost:8080'
# compose.public.yaml replaces the base file's port list with `!override`.
$minCompose = [version]'2.24.4'
$interactive = [Environment]::UserInteractive -and -not [Console]::IsInputRedirected

# --- Fresh install: address and mode, chosen and checked before anything is written ----------------

$fresh = -not (Test-Path .env)
if (-not $fresh) {
	Write-Host '-> .env exists; leaving it as it is.'
}
else {
	$url = $env:KVITTERIA_PUBLIC_URL
	if (-not $url) {
		if ($interactive) {
			$url = Read-Host "Public address clients will use [$defaultUrl]"
		}
		if (-not $url) { $url = $defaultUrl }
	}

	# A scheme and a host, optionally a port. A path or a trailing slash would end up inside the token
	# issuer and every address derived from it.
	if ($url -notmatch '^https?://[A-Za-z0-9]([A-Za-z0-9.-]*[A-Za-z0-9])?(:[0-9]{1,5})?$') {
		throw "'$url' is not a usable address. Use http:// or https://, a host and optionally a port, with no path and no trailing slash."
	}

	$tls = ''
	if ($url.StartsWith('https://')) {
		$tls = $env:KVITTERIA_TLS
		if (-not $tls) {
			if ($interactive) {
				$answer = Read-Host 'Does a proxy you run (Tailscale serve, Cloudflare Tunnel, Traefik, ...) already terminate TLS for this address? [y/N]'
				$tls = if ($answer -match '^[yY]') { 'proxy' } else { 'domain' }
			}
			else {
				$tls = 'domain'
			}
		}
		if ($tls -notin 'proxy', 'domain') { throw "KVITTERIA_TLS must be 'proxy' or 'domain', not '$tls'." }
	}

	if ($tls -eq 'domain') {
		$have = (docker compose version --short 2>$null) -replace '^v', ''
		$parsed = $null
		if (-not [version]::TryParse(($have -replace '[^0-9.].*$', ''), [ref]$parsed) -or $parsed -lt $minCompose) {
			throw "Public-domain mode needs Docker Compose $minCompose or newer; this is $have."
		}
	}

	Write-Host '-> Writing .env'
	Copy-Item .env.example .env

	# Never published to the host, but still the only thing between a container on this machine and
	# the database.
	$password = [Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(24)) `
		-replace '[^A-Za-z0-9]', ''

	# GoTrue's config loader demands a symmetric secret whether or not asymmetric keys are set. With
	# the key set below present it goes unused, and it is never shared with the backend.
	$secret = [Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(36)) `
		-replace '[^A-Za-z0-9]', ''

	# The key GoTrue signs with. ES256, so the backend validates against the public half it fetches
	# from the JWKS endpoint and the private key never leaves this file.
	$keys = openssl ecparam -name prime256v1 -genkey -noout 2>$null | python scripts/jwk_from_pem.py
	if ($LASTEXITCODE -ne 0) { throw 'Failed to generate a signing key.' }

	# Both HTTPS modes name their files, which also leaves compose.override.yaml out. That file is the
	# development overlay: it would run the backend in Development and publish Postgres and GoTrue on
	# the host, and a published GoTrue port bypasses the client address the rate limit is keyed on.
	$separator = [IO.Path]::PathSeparator
	$composeFile = switch ($tls) {
		'domain' { "compose.yaml${separator}compose.public.yaml" }
		'proxy' { 'compose.yaml' }
		default { '' }
	}

	$env:KVITTERIA_ENV_PASSWORD = $password
	$env:KVITTERIA_ENV_SECRET = $secret
	$env:KVITTERIA_ENV_KEYS = $keys
	$env:KVITTERIA_ENV_URL = $url
	$env:KVITTERIA_ENV_COMPOSE_FILE = $composeFile

	python -c @'
import io, os, re
e = os.environ
s = io.open(".env", encoding="utf-8").read()

def put(s, key, value):
    return re.sub(r"^" + key + r"=.*$", lambda m: key + "=" + value, s, count=1, flags=re.M)

s = put(s, "POSTGRES_PASSWORD", e["KVITTERIA_ENV_PASSWORD"])
s = put(s, "GOTRUE_JWT_SECRET", e["KVITTERIA_ENV_SECRET"])
s = put(s, "GOTRUE_JWT_KEYS", e["KVITTERIA_ENV_KEYS"])
s = put(s, "PUBLIC_URL", e["KVITTERIA_ENV_URL"])

# A plain-HTTP address with a port is served on that port.
m = re.match(r"^http://[^/:]+:(\d+)$", e["KVITTERIA_ENV_URL"])
if m:
    s = put(s, "CADDY_HTTP_PORT", m.group(1))

if e["KVITTERIA_ENV_COMPOSE_FILE"]:
    s = s.rstrip("\n") + "\n\nCOMPOSE_FILE=" + e["KVITTERIA_ENV_COMPOSE_FILE"] + "\n"
io.open(".env", "w", encoding="utf-8", newline="\n").write(s)
'@
	$written = $LASTEXITCODE
	Remove-Item Env:KVITTERIA_ENV_PASSWORD, Env:KVITTERIA_ENV_SECRET, Env:KVITTERIA_ENV_KEYS, Env:KVITTERIA_ENV_URL, Env:KVITTERIA_ENV_COMPOSE_FILE
	if ($written -ne 0) { throw 'Failed to write .env.' }
}

# --- Credentials -----------------------------------------------------------------------------------

$adminEmail = $env:KVITTERIA_ADMIN_EMAIL
$adminPassword = $env:KVITTERIA_ADMIN_PASSWORD
Remove-Item Env:KVITTERIA_ADMIN_PASSWORD -ErrorAction SilentlyContinue

# Asked for once, and only when the household turns out to be unclaimed.
function Get-Credentials {
	if ($script:adminEmail -and $script:adminPassword) { return }
	if (-not $interactive) {
		throw 'The household has no admin yet and there is no terminal to ask on. Set KVITTERIA_ADMIN_EMAIL and KVITTERIA_ADMIN_PASSWORD and run again. Nothing is reachable meanwhile.'
	}
	Write-Host '-> Creating the first admin. This account claims the household.'
	if (-not $script:adminEmail) { $script:adminEmail = Read-Host 'Admin email' }
	if (-not $script:adminPassword) {
		$secure = Read-Host 'Admin password' -AsSecureString
		$script:adminPassword = [System.Net.NetworkCredential]::new('', $secure).Password
	}
	if (-not $script:adminEmail -or -not $script:adminPassword) { throw 'An email and a password are both needed.' }
}

# On a fresh install the model download below is the long part, so ask now rather than after it.
if ($fresh) { Get-Credentials }

# --- Start the services the claim needs, without the entrypoint -------------------------------------

# Caddy depends on a healthy backend, so a plain `up -d` would open the entrypoint on an unclaimed
# household for the seconds it takes to claim it. Bringing up only the backend and the identity provider
# (and what they depend on) keeps every port closed until the household has an admin.
Write-Host '-> Starting the backend and identity provider. The first run downloads the extraction model, which takes a while.'
docker compose stop caddy *> $null
docker compose up -d --wait backend gotrue
if ($LASTEXITCODE -ne 0) { throw 'docker compose up failed.' }

# Everything below goes through `exec` into the backend container, so the claim needs no DNS, no
# certificate and no proxy of yours to be in place.
function Get-HouseholdStatus {
	try {
		$raw = docker compose exec -T backend curl -sS http://127.0.0.1:8080/health/ready 2>$null
		$report = ($raw -join "`n") | ConvertFrom-Json
		$check = $report.checks | Where-Object { $_.name -eq 'household' } | Select-Object -First 1
		if ($check) { return $check.status }
	}
	catch { }
	return 'Unknown'
}

# Sends the credentials on stdin, so neither value is ever an argument of any process. Returns the HTTP
# status and the parsed body.
function Send-Credentials([string]$Path) {
	$json = @{ email = $script:adminEmail; password = $script:adminPassword } | ConvertTo-Json -Compress
	$raw = $json | docker compose exec -T backend curl -sS -X POST -H 'Content-Type: application/json' `
		--data-binary '@-' -w '\n%{http_code}' "http://gotrue:9999$Path"
	$text = $raw -join "`n"
	$split = $text.LastIndexOf("`n")
	$body = $null
	try { $body = $text.Substring(0, $split) | ConvertFrom-Json } catch { }
	[pscustomobject]@{ Status = $text.Substring($split + 1).Trim(); Body = $body }
}

function Get-Field($Body, [string]$Name) {
	if ($Body -and $Body.PSObject.Properties[$Name] -and $Body.$Name) { return [string]$Body.$Name }
	return ''
}

function Invoke-Claim {
	$result = Send-Credentials '/signup'
	$token = Get-Field $result.Body 'access_token'

	if (-not $token) {
		# An earlier run created the account and stopped before the membership request, or this is the
		# same person signing in again.
		if ((Get-Field $result.Body 'error_code') -eq 'user_already_exists' -or $result.Status -eq '200') {
			$result = Send-Credentials '/token?grant_type=password'
			$token = Get-Field $result.Body 'access_token'
		}
	}

	if (-not $token) {
		$reason = ((Get-Field $result.Body 'msg') + (Get-Field $result.Body 'error_description')).TrimEnd('.')
		throw "Could not create or sign in the admin account (HTTP $($result.Status)): $reason. The entrypoint stays stopped; run this script again."
	}

	# GET /api/users/me is what claims the household: the first authenticated caller is bootstrapped
	# into the admin by the same decision that handles every later request.
	$me = "Authorization: Bearer $token" | docker compose exec -T backend curl -sS -o /dev/null -w '%{http_code}' -H '@-' http://127.0.0.1:8080/api/users/me
	if ("$me".Trim() -ne '200') {
		throw "The account exists but the household was not claimed (GET /api/users/me answered $me). The entrypoint stays stopped."
	}
}

$state = Get-HouseholdStatus
if ($state -ne 'Healthy') {
	Get-Credentials
	Invoke-Claim
	$adminPassword = $null
	$state = Get-HouseholdStatus
}

if ($state -ne 'Healthy') {
	throw "The household still has no admin (it reports $state). The entrypoint stays stopped."
}

# --- Open the entrypoint ----------------------------------------------------------------------------

Write-Host '-> The household is claimed. Starting the entrypoint.'
docker compose up -d
if ($LASTEXITCODE -ne 0) { throw 'docker compose up failed.' }

$url = (Select-String -Path .env -Pattern '^PUBLIC_URL=(.*)$').Matches[0].Groups[1].Value
Write-Host ''
Write-Host 'Ready. The household is claimed, and clients connect to:'
Write-Host "  $url"
