# Сборка и установка Snapkit одной командой:
#   powershell -ExecutionPolicy Bypass -File build.ps1
# Порядок: тесты → публикация → остановка работающей программы → замена файлов → ярлык → запуск.

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'

# dotnet может ещё не быть в PATH, если SDK поставили только что
$dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
if (-not $dotnet) { $dotnet = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe' }
if (-not (Test-Path $dotnet)) { throw 'Не найден .NET SDK. Установите его: winget install Microsoft.DotNet.SDK.10' }

$installDir = Join-Path $env:LOCALAPPDATA 'Programs\Snapkit'
$publishDir = Join-Path $PSScriptRoot 'artifacts\publish'

Write-Host '1/5 Запускаю тесты...' -ForegroundColor Cyan
# Тесты с настоящим буфером обмена и окнами на экране здесь не запускаются — см. README
& $dotnet test Snapkit.slnx --filter 'Category!=Clipboard&Category!=Desktop' --nologo -v q
if ($LASTEXITCODE -ne 0) { Write-Host 'Тесты не прошли, сборка остановлена.' -ForegroundColor Red; exit 1 }

Write-Host '2/5 Собираю программу...' -ForegroundColor Cyan
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
& $dotnet publish src/Snapkit.App -c Release -r win-x64 --self-contained true -o $publishDir --nologo -v q
if ($LASTEXITCODE -ne 0) { Write-Host 'Сборка не удалась.' -ForegroundColor Red; exit 1 }

Write-Host '3/5 Останавливаю работающий Snapkit...' -ForegroundColor Cyan
$running = Get-Process -Name Snapkit -ErrorAction SilentlyContinue
if ($running) {
    $running | Stop-Process -Force
    $running | Wait-Process -Timeout 10 -ErrorAction SilentlyContinue
}

Write-Host "4/5 Устанавливаю в $installDir..." -ForegroundColor Cyan
# Папка принадлежит только Snapkit: старые файлы убираем, чтобы не осталось лишних.
# Windows отпускает файлы остановленной программы не сразу — пробуем несколько раз.
if ((Test-Path $installDir) -and ($installDir -like '*\Programs\Snapkit')) {
    for ($attempt = 1; $attempt -le 10; $attempt++) {
        try { Remove-Item $installDir -Recurse -Force -ErrorAction Stop; break }
        catch {
            if ($attempt -eq 10) { Write-Host 'Не удалось заменить файлы: программа ещё работает.' -ForegroundColor Red; exit 1 }
            Start-Sleep -Milliseconds 500
        }
    }
}
New-Item -ItemType Directory -Force -Path $installDir | Out-Null
Copy-Item -Path (Join-Path $publishDir '*') -Destination $installDir -Recurse -Force
$exe = Join-Path $installDir 'Snapkit.exe'

$shortcutPath = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Snapkit.lnk'
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $exe
$shortcut.WorkingDirectory = $installDir
$shortcut.Description = 'Snapkit — снимки экрана с комментариями для ИИ'
$shortcut.Save()

Write-Host '5/5 Запускаю Snapkit...' -ForegroundColor Cyan
# Через Проводник, а не напрямую: если сборку запустили из упакованного приложения (например,
# из терминала Claude Desktop), Windows подменяет AppData для всех его дочерних процессов,
# и Snapkit работал бы со своими отдельными лентой, настройками и журналом
Start-Process -FilePath explorer.exe -ArgumentList "`"$exe`""
Write-Host 'Готово.' -ForegroundColor Green
