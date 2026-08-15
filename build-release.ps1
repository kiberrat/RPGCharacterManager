<#
.SYNOPSIS
  Собирает установщик и пакет автообновления RPG Character Manager.

.EXAMPLE
  .\build-release.ps1 -Version 1.1.0
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^\d+\.\d+\.\d+([-.][0-9A-Za-z.-]+)?$')]
    [string] $Version,

    [string] $UpdateSource,

    [string] $FeedbackEndpoint = '',

    [string] $ReleaseNotes
)

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath($PSScriptRoot)
$workRoot = [IO.Path]::GetFullPath((Join-Path $root '.release-work'))
$publishDir = [IO.Path]::GetFullPath((Join-Path $workRoot 'app'))
$releaseDir = [IO.Path]::GetFullPath((Join-Path $root 'releases-local'))
$project = Join-Path $root 'src\RPGCharacterManager.App\RPGCharacterManager.App.csproj'
$vpk = Join-Path $root '.tools\velopack\vpk.exe'

if (-not $workRoot.StartsWith($root, [StringComparison]::OrdinalIgnoreCase) -or
    -not $publishDir.StartsWith($workRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Небезопасный путь временного каталога выпуска.'
}

if (Test-Path -LiteralPath $publishDir) {
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $publishDir, $releaseDir | Out-Null

if (-not (Test-Path -LiteralPath $vpk)) {
    New-Item -ItemType Directory -Force -Path (Split-Path $vpk -Parent) | Out-Null
    dotnet tool install vpk --tool-path (Split-Path $vpk -Parent) --version 1.2.0
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

if ([string]::IsNullOrWhiteSpace($UpdateSource)) {
    $UpdateSource = $releaseDir
}

Write-Host "Публикация версии $Version…" -ForegroundColor Cyan
dotnet publish $project -c Release -r win-x64 --self-contained true `
    -p:Version=$Version `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none `
    -p:AllowedReferenceRelatedFileExtensions=none `
    -o $publishDir
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$configPath = Join-Path $publishDir 'appsettings.json'
$config = Get-Content -LiteralPath $configPath -Raw -Encoding UTF8 | ConvertFrom-Json
$config.Distribution.UpdateSource = $UpdateSource
$config.Distribution.FeedbackEndpoint = $FeedbackEndpoint
$config | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $configPath -Encoding UTF8

$arguments = @(
    'pack',
    '--packId', 'RPGCharacterManager',
    '--packVersion', $Version,
    '--packDir', $publishDir,
    '--mainExe', 'RPGCharacterManager.exe',
    '--packTitle', 'RPG Character Manager',
    '--packAuthors', 'RPG Character Manager',
    '--runtime', 'win-x64',
    '--channel', 'win',
    '--outputDir', $releaseDir,
    '--shortcuts', 'Desktop,StartMenuRoot'
)

if (-not [string]::IsNullOrWhiteSpace($ReleaseNotes)) {
    $notesPath = [IO.Path]::GetFullPath($ReleaseNotes)
    if (-not (Test-Path -LiteralPath $notesPath)) { throw "Файл описания не найден: $notesPath" }
    $arguments += @('--releaseNotes', $notesPath)
}

& $vpk @arguments
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$setup = Get-ChildItem -LiteralPath $releaseDir -Filter '*Setup.exe' |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1
Write-Host "Готово: $($setup.FullName)" -ForegroundColor Green
Write-Host "Лента локальной проверки: $releaseDir" -ForegroundColor Green