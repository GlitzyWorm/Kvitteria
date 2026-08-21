#Requires -Version 7
<#
.SYNOPSIS
	Prepares .env and brings the Kvitteria stack up.
.DESCRIPTION
	Safe to re-run: an existing .env is left alone, so the database password and signing keys survive,
	and compose reconciles whatever is already running.
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

if (Test-Path .env) {
	Write-Host '-> .env exists; leaving it as it is.'
}
else {
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

	$env:KVITTERIA_PASSWORD = $password
	$env:KVITTERIA_SECRET = $secret
	$env:KVITTERIA_KEYS = $keys

	python -c @'
import io, os, re
s = io.open(".env", encoding="utf-8").read()
s = re.sub(r"^POSTGRES_PASSWORD=.*$", "POSTGRES_PASSWORD=" + os.environ["KVITTERIA_PASSWORD"], s, count=1, flags=re.M)
s = re.sub(r"^GOTRUE_JWT_SECRET=.*$", "GOTRUE_JWT_SECRET=" + os.environ["KVITTERIA_SECRET"], s, count=1, flags=re.M)
s = re.sub(r"^GOTRUE_JWT_KEYS=.*$", "GOTRUE_JWT_KEYS=" + os.environ["KVITTERIA_KEYS"], s, count=1, flags=re.M)
io.open(".env", "w", encoding="utf-8", newline="\n").write(s)
'@
	if ($LASTEXITCODE -ne 0) { throw 'Failed to write .env.' }

	Remove-Item Env:KVITTERIA_PASSWORD, Env:KVITTERIA_SECRET, Env:KVITTERIA_KEYS
}

Write-Host '-> Starting the stack. The first run downloads the extraction model, which takes a while.'
docker compose up -d
if ($LASTEXITCODE -ne 0) { throw 'docker compose up failed.' }

$url = (Select-String -Path .env -Pattern '^PUBLIC_URL=(.*)$').Matches[0].Groups[1].Value
Write-Host "-> Waiting for $url/health/ready"

foreach ($attempt in 1..120) {
	try {
		$ready = Invoke-RestMethod "$url/health/ready" -TimeoutSec 5
		Write-Host ''
		$ready | ConvertTo-Json -Depth 5
		Write-Host ''
		Write-Host '-> Ready. Create the first account, which claims the household as its admin:'
		Write-Host "   .\scripts\token.ps1 you@example.com 'your-password' -SignUp"
		exit 0
	}
	catch {
		Write-Host '.' -NoNewline
		Start-Sleep -Seconds 5
	}
}

Write-Host ''
Write-Error 'The stack did not become ready. What each dependency reports:'
docker compose ps
exit 1
