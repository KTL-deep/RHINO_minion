[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

Push-Location $repoRoot
try {
    dotnet test RHINO_minion.sln --configuration Debug --no-restore
    & "$repoRoot\.venv\Scripts\python.exe" -m pytest
} finally {
    Pop-Location
}
