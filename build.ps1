<#
.SYNOPSIS
    Сборка, тестирование и запуск решения RPG Character Manager.

.DESCRIPTION
    На этой машине .NET 8 SDK установлен в профиль пользователя и отсутствует в PATH,
    а системный каталог C:\Program Files\dotnet содержит только среду выполнения .NET 6.
    Скрипт находит нужный dotnet и задаёт DOTNET_ROOT, без которого запускающий модуль
    приложения не может найти .NET 8.

.PARAMETER Task
    Build   — собрать решение (по умолчанию);
    Test    — выполнить автоматические тесты;
    Run     — собрать и запустить приложение;
    Publish — создать автономную сборку, не требующую установленного .NET.

.PARAMETER Configuration
    Конфигурация сборки: Debug (по умолчанию) или Release.

.EXAMPLE
    .\build.ps1 Run
#>
[CmdletBinding()]
param(
    [ValidateSet('Build', 'Test', 'Run', 'Publish')]
    [string] $Task = 'Build',

    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'

$solutionRoot = $PSScriptRoot
$solution = Join-Path $solutionRoot 'RPGCharacterManager.sln'
$appProject = Join-Path $solutionRoot 'src\RPGCharacterManager.App\RPGCharacterManager.App.csproj'

function Resolve-DotNet {
    $userDotNet = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet\dotnet.exe'
    if (Test-Path $userDotNet) { return $userDotNet }

    $systemDotNet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
    if ($systemDotNet) { return $systemDotNet }

    throw '.NET 8 SDK не найден. Установите его: https://dotnet.microsoft.com/download/dotnet/8.0'
}

function Stop-RunningApplication {
    # Запущенное приложение удерживает свои сборки, и MSBuild не может их перезаписать.
    # Без остановки сборка «удаётся», но в каталоге остаются файлы предыдущей версии.
    $running = Get-Process -Name 'RPGCharacterManager' -ErrorAction SilentlyContinue
    if ($running) {
        Write-Host 'Остановка запущенного приложения перед сборкой…' -ForegroundColor Yellow
        $running | Stop-Process -Force
        Start-Sleep -Seconds 2
    }
}

$dotnet = Resolve-DotNet
$env:DOTNET_ROOT = Split-Path $dotnet -Parent
Write-Host "Используется dotnet: $dotnet" -ForegroundColor Cyan

Stop-RunningApplication

switch ($Task) {
    'Build' {
        & $dotnet build $solution -c $Configuration
    }
    'Test' {
        # Сначала собирается всё решение: «dotnet test» строит только тестовые проекты
        # и их зависимости, поэтому приложение осталось бы предыдущей версии.
        & $dotnet build $solution -c $Configuration
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

        & $dotnet test $solution -c $Configuration --no-build
    }
    'Run' {
        & $dotnet run --project $appProject -c $Configuration
    }
    'Publish' {
        $output = Join-Path $solutionRoot 'publish'

        # --self-contained включает среду выполнения .NET в состав файла, поэтому
        # приложение запускается двойным щелчком на компьютере без установленного .NET.
        # IncludeNativeLibrariesForSelfExtract требуется Avalonia: библиотеки отрисовки
        # являются native-кодом и должны попасть внутрь единого файла.
        # Каталог очищается: файлы предыдущей сборки не должны смешиваться с новой.
        if (Test-Path $output) { Remove-Item $output -Recurse -Force }

        & $dotnet publish $appProject -c Release -r win-x64 --self-contained true `
            -p:PublishSingleFile=true `
            -p:IncludeNativeLibrariesForSelfExtract=true `
            -p:EnableCompressionInSingleFile=true `
            -p:DebugType=none `
            -p:AllowedReferenceRelatedFileExtensions=none `
            -o $output

        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

        $executable = Join-Path $output 'RPGCharacterManager.exe'
        $sizeInMegabytes = [math]::Round((Get-Item $executable).Length / 1MB, 1)

        Write-Host ''
        Write-Host "Готово: $executable ($sizeInMegabytes МБ)" -ForegroundColor Green
        Write-Host 'Установка .NET на компьютере не требуется — запускайте двойным щелчком.' -ForegroundColor Green
    }
}

if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
