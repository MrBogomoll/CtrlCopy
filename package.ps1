param([Parameter(Mandatory=$true)][string]$Destination)
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'build.ps1') -Test -Preview -PolishTest
if ($LASTEXITCODE -ne 0) { throw 'Build/tests failed.' }
$taskOutput = [IO.Path]::GetFullPath($Destination)
New-Item -ItemType Directory -Path $taskOutput -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'bin\AutoCopy.exe') -Destination (Join-Path $taskOutput 'CtrlCopy.exe') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'bin\AutoCopy.ico') -Destination (Join-Path $taskOutput 'CtrlCopy.ico') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md') -Destination (Join-Path $taskOutput 'CtrlCopy-README.md') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'docs\THIRD-PARTY-NOTICES.txt') -Destination (Join-Path $taskOutput 'CtrlCopy-NOTICES.txt') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'docs\verification.md') -Destination (Join-Path $taskOutput 'CtrlCopy-verification.md') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'LICENSE') -Destination (Join-Path $taskOutput 'LICENSE') -Force
$taskArchive = Join-Path $taskOutput 'CtrlCopy-source.zip'
$taskSourceFiles = @('src','tests','tools','docs','assets','build.ps1','package.ps1','README.md','CHANGELOG.md','LICENSE','app.manifest','.gitignore') | ForEach-Object { Join-Path $PSScriptRoot $_ }
Compress-Archive -LiteralPath $taskSourceFiles -DestinationPath $taskArchive -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'bin\settings-dark.png') -Destination (Join-Path $taskOutput 'CtrlCopy-interface-dark.png') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'bin\settings-light.png') -Destination (Join-Path $taskOutput 'CtrlCopy-interface-light.png') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'bin\tray-menu-dark.png') -Destination (Join-Path $taskOutput 'CtrlCopy-tray-menu-dark.png') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'bin\tray-menu-light.png') -Destination (Join-Path $taskOutput 'CtrlCopy-tray-menu-light.png') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'assets\copy-click.wav') -Destination (Join-Path $taskOutput 'CtrlCopy-click.wav') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'bin\copy-indicator.png') -Destination (Join-Path $taskOutput 'CtrlCopy-copy-indicator.png') -Force
@('CtrlCopy.exe','CtrlCopy-source.zip') | ForEach-Object {
    $taskHash = Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $taskOutput $_)
    "$($taskHash.Hash.ToLowerInvariant())  $_"
} | Set-Content -LiteralPath (Join-Path $taskOutput 'SHA256SUMS.txt') -Encoding ascii
Get-Item -LiteralPath (Join-Path $taskOutput 'CtrlCopy.exe'),$taskArchive | Select-Object Name,Length
