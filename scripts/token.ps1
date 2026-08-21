#Requires -Version 7
<#
.SYNOPSIS
	Prints an access token for manual API calls.
.DESCRIPTION
	There is no web app yet, and the Scalar reference declares no security scheme, so this is how you
	get a bearer token to paste. The very first account to sign in claims the household as its admin.
.EXAMPLE
	.\scripts\token.ps1 you@example.com 'password'
.EXAMPLE
	.\scripts\token.ps1 you@example.com 'password' -SignUp
#>
[CmdletBinding()]
param(
	[Parameter(Mandatory)][string] $Email,
	[Parameter(Mandatory)][string] $Password,
	[switch] $SignUp
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Set-Location (Join-Path $PSScriptRoot '..')

if (-not (Test-Path .env)) { throw 'No .env. Run scripts/setup.ps1 first.' }

$url = (Select-String -Path .env -Pattern '^PUBLIC_URL=(.*)$').Matches[0].Groups[1].Value

# Different endpoints, same token shape in the response.
$endpoint = if ($SignUp) { "$url/auth/v1/signup" } else { "$url/auth/v1/token?grant_type=password" }
$body = @{ email = $Email; password = $Password } | ConvertTo-Json -Compress

$response = Invoke-RestMethod $endpoint -Method Post -ContentType 'application/json' -Body $body

if (-not $response.access_token) {
	throw "No access token in the response: $($response | ConvertTo-Json -Compress)"
}

# The token alone on stdout, so it can be captured; the hint goes to the host.
$response.access_token
Write-Host ''
Write-Host 'Use it as:'
Write-Host "  Invoke-RestMethod $url/api/receipts -Headers @{ Authorization = ""Bearer <token>"" }"
