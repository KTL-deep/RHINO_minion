[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

Push-Location $repoRoot
try {
    dotnet build RHINO_minion.sln --configuration $Configuration
    & "$repoRoot\.venv\Scripts\python.exe" -m ruff check src/backend tests/backend
    npm run build --prefix RHINO_minion_frontend
} finally {
    Pop-Location
}
