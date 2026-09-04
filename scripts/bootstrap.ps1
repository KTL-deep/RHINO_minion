[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw ".NET 8 SDK is required. Install it from https://dotnet.microsoft.com/download/dotnet/8.0"
}

$sdkList = dotnet --list-sdks
if (-not ($sdkList -match '^8\.0\.')) {
    throw ".NET 8 SDK is not installed. A runtime alone is not sufficient."
}

$pythonCommand = Get-Command py -ErrorAction SilentlyContinue
if ($null -eq $pythonCommand) {
    $pythonCommand = Get-Command python -ErrorAction SilentlyContinue
}
if ($null -eq $pythonCommand) {
    throw "Python 3.11 or newer is required."
}
if (-not (Get-Command npm -ErrorAction SilentlyContinue)) {
    throw "Node.js 20 or newer with npm is required for the frontend."
}

Push-Location $repoRoot
try {
    if ($pythonCommand.Name -eq "py.exe") {
        & $pythonCommand.Source -3.11 -m venv .venv
    } else {
        & $pythonCommand.Source -m venv .venv
    }

    & "$repoRoot\.venv\Scripts\python.exe" -m pip install --upgrade pip
    & "$repoRoot\.venv\Scripts\python.exe" -m pip install -e "src/backend[dev]"
    dotnet restore RHINO_minion.sln
    npm install --prefix RHINO_minion_frontend
} finally {
    Pop-Location
}
