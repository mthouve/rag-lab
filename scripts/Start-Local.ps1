<#
.SYNOPSIS
Starts the API using the existing local Compose database password.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path $PSScriptRoot -Parent
$envFile = Join-Path $repositoryRoot '.env'
if (-not (Test-Path -LiteralPath $envFile)) {
    throw 'The local .env file is missing. Follow the README fresh installation instructions.'
}

$passwordEntry = @(Get-Content -LiteralPath $envFile | Where-Object { $_ -match '^POSTGRES_PASSWORD=' })
if ($passwordEntry.Count -ne 1) {
    throw 'Expected exactly one POSTGRES_PASSWORD entry in .env.'
}
$databasePassword = $passwordEntry[0].Substring('POSTGRES_PASSWORD='.Length)
if ([string]::IsNullOrWhiteSpace($databasePassword) -or $databasePassword -match '[\x27"$#\s]') {
    throw 'This helper expects a plain, unquoted password without whitespace, quotes, $ or # in .env. For other passwords, use the README environment-variable instructions.'
}

$previousConnection = $env:ConnectionStrings__Postgres
$previousEnvironment = $env:DOTNET_ENVIRONMENT
try {
    $env:ConnectionStrings__Postgres = "Host=127.0.0.1;Port=5432;Database=raglab;Username=raglab;Password=`"$databasePassword`""
    $env:DOTNET_ENVIRONMENT = 'Development'
    Push-Location $repositoryRoot
    try {
        & dotnet restore src/RagLab.Api --configfile NuGet.Config
        if ($LASTEXITCODE -ne 0) {
            throw "Package restore exited with code $LASTEXITCODE."
        }
        & dotnet run --project src/RagLab.Api --launch-profile RagLab --no-restore
        if ($LASTEXITCODE -ne 0) {
            throw "API startup exited with code $LASTEXITCODE."
        }
    } finally {
        Pop-Location
    }
} finally {
    $env:ConnectionStrings__Postgres = $previousConnection
    $env:DOTNET_ENVIRONMENT = $previousEnvironment
}
