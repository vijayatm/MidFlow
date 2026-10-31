$ErrorActionPreference = "Stop"

$solution = Join-Path $PSScriptRoot "..\MidFlow.sln"
dotnet build $solution -m:1 @args
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
