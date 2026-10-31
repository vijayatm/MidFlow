$ErrorActionPreference = "Stop"

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path.TrimEnd('\')
$projectPath = Join-Path $repositoryRoot "src\MidFlow.App\MidFlow.App.csproj"
$publishDirectory = Join-Path $repositoryRoot "artifacts\win-x64"
$ridOutputDirectory = Join-Path $repositoryRoot "src\MidFlow.App\bin\Release\net10.0-windows\win-x64"
$legacyPublishDirectory = Join-Path $ridOutputDirectory "publish"
$coreReleaseOutputDirectory = Join-Path $repositoryRoot "src\MidFlow.Core\bin\Release\net10.0"

New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null

& dotnet publish $projectPath --configuration Release --runtime win-x64 --self-contained false --output $publishDirectory
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$removedCopies = 0
foreach ($sourceDirectory in @($ridOutputDirectory, $legacyPublishDirectory, $coreReleaseOutputDirectory)) {
    if (-not (Test-Path -LiteralPath $sourceDirectory -PathType Container)) { continue }

    $resolvedSource = (Resolve-Path -LiteralPath $sourceDirectory).Path
    if (-not $resolvedSource.StartsWith($repositoryRoot + '\', [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean a path outside the repository: $resolvedSource"
    }

    foreach ($file in Get-ChildItem -LiteralPath $resolvedSource -File -Force) {
        $publishedCopy = Join-Path $publishDirectory $file.Name
        if (-not (Test-Path -LiteralPath $publishedCopy -PathType Leaf)) { continue }

        $sourceHash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
        $publishedHash = (Get-FileHash -LiteralPath $publishedCopy -Algorithm SHA256).Hash
        if ($sourceHash -eq $publishedHash) {
            Remove-Item -LiteralPath $file.FullName -Force
            $removedCopies++
        }
    }
}

if ((Test-Path -LiteralPath $legacyPublishDirectory -PathType Container) -and
    -not (Get-ChildItem -LiteralPath $legacyPublishDirectory -Force)) {
    Remove-Item -LiteralPath $legacyPublishDirectory -Force
}

Write-Host "Published framework-dependent win-x64 app to: $publishDirectory"
Write-Host "Removed $removedCopies identical build/publish copies from bin output."
