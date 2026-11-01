$ErrorActionPreference = "Stop"

$version = $env:APP_VERSION
if ($version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Set APP_VERSION to the stable MAJOR.MINOR.PATCH version from the release tag (for example, 7.1.7)."
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$artifactRoot = Join-Path $repoRoot "artifacts"
$publishDir = Join-Path $artifactRoot "win-x64"
$zipPath = Join-Path $artifactRoot "MidFlow-win-x64.zip"
$setupPath = Join-Path $artifactRoot "MidFlow-Setup-x64.exe"
$compilerCandidates = @(
    (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
    (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe"),
    (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 7\ISCC.exe"),
    (Join-Path $env:ProgramFiles "Inno Setup 7\ISCC.exe")
)
$compiler = $compilerCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $compiler) {
    throw "Inno Setup 6 or 7 was not found. Install it before packaging the release."
}

New-Item -ItemType Directory -Force -Path $artifactRoot | Out-Null
foreach ($target in @($publishDir, $zipPath, $setupPath)) {
    $resolvedTarget = [System.IO.Path]::GetFullPath($target)
    if (-not $resolvedTarget.StartsWith($artifactRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean an output outside the artifacts directory: $resolvedTarget"
    }
    if (Test-Path -LiteralPath $resolvedTarget) {
        Remove-Item -LiteralPath $resolvedTarget -Recurse -Force
    }
}

$project = Join-Path $repoRoot "src\MidFlow.App\MidFlow.App.csproj"
dotnet publish $project -c Release -r win-x64 --self-contained true `
    -p:Version=$version -p:FileVersion="$version.0" `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true -p:DebugSymbols=false `
    -o $publishDir
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -CompressionLevel Optimal
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& $compiler "/DAppVersion=$version" "/O$artifactRoot" "/FMidFlow-Setup-x64" `
    (Join-Path $repoRoot "installer\MidFlow.iss")
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if (-not (Test-Path -LiteralPath $setupPath)) {
    throw "Inno Setup completed without creating $setupPath"
}
Write-Host "Created release assets:"
Write-Host "  $zipPath"
Write-Host "  $setupPath"
