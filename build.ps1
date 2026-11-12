param([switch]$Test, [switch]$Integration, [switch]$Preview, [switch]$UiTest, [switch]$PolishTest)
$ErrorActionPreference = 'Stop'
$taskRoot = $PSScriptRoot
$taskBin = Join-Path $taskRoot 'bin'
New-Item -ItemType Directory -Path $taskBin -Force | Out-Null
$taskSdkLine = (& dotnet --list-sdks | Select-Object -Last 1)
if (-not $taskSdkLine) { throw 'Install the .NET SDK to build AutoCopy.' }
$taskSdkVersion = $taskSdkLine.Split(' ')[0]
$taskSdkRoot = [regex]::Match($taskSdkLine, '\[(.*)\]').Groups[1].Value
$taskCompiler = Join-Path $taskSdkRoot "$taskSdkVersion\Roslyn\bincore\csc.dll"
$taskFramework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$taskRefs = @('mscorlib.dll','System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Runtime.Serialization.dll','System.Xml.dll','System.Security.dll','WPF\WindowsBase.dll','WPF\UIAutomationClient.dll','WPF\UIAutomationTypes.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll','WPF\WindowsFormsIntegration.dll','System.Xaml.dll') | ForEach-Object { '/reference:' + (Join-Path $taskFramework $_) }
function Invoke-Compile($Output, $Sources, $Target = 'exe', $Extra = @()) {
    & dotnet $taskCompiler /nologo /noconfig /nostdlib+ /langversion:latest /optimize+ /utf8output "/target:$Target" "/out:$Output" @taskRefs @Extra @Sources
    if ($LASTEXITCODE -ne 0) { throw "Compilation failed: $Output" }
}
$taskSources = @(Get-ChildItem -LiteralPath (Join-Path $taskRoot 'src') -Filter '*.cs' | ForEach-Object FullName)
$taskUiResource = '/resource:' + (Join-Path $taskRoot 'src\CompactSettings.xaml') + ',AutoCopy.CompactSettings.xaml'
$taskMenuResource = '/resource:' + (Join-Path $taskRoot 'src\TrayMenu.xaml') + ',AutoCopy.TrayMenu.xaml'
$taskNoticesResource = '/resource:' + (Join-Path $taskRoot 'docs\THIRD-PARTY-NOTICES.txt') + ',AutoCopy.Notices.txt'
$taskLicenseResource = '/resource:' + (Join-Path $taskRoot 'LICENSE') + ',AutoCopy.License.txt'
$taskAudioResource = @()
if (Test-Path -LiteralPath (Join-Path $taskRoot 'assets\copy-click.wav')) { $taskAudioResource = @('/resource:' + (Join-Path $taskRoot 'assets\copy-click.wav') + ',AutoCopy.CopyClick.wav') }
if (Test-Path -LiteralPath (Join-Path $taskRoot 'src\Program.cs')) {
    Invoke-Compile (Join-Path $taskBin 'IconWriter.exe') @((Join-Path $taskRoot 'src\AppIcon.cs'), (Join-Path $taskRoot 'src\Native.cs'), (Join-Path $taskRoot 'tools\IconWriter.cs'))
    & (Join-Path $taskBin 'IconWriter.exe') (Join-Path $taskBin 'AutoCopy.ico')
    if ($LASTEXITCODE -ne 0) { throw 'Icon generation failed.' }
    Invoke-Compile (Join-Path $taskBin 'AutoCopy.exe') $taskSources 'winexe' (@(('/win32manifest:' + (Join-Path $taskRoot 'app.manifest')), ('/win32icon:' + (Join-Path $taskBin 'AutoCopy.ico')), $taskUiResource, $taskMenuResource, $taskNoticesResource, $taskLicenseResource) + $taskAudioResource)
}
if ($Test) {
    $taskCore = @('Settings.cs','SelectionPolicy.cs') | ForEach-Object { Join-Path $taskRoot "src\$_" }
    Invoke-Compile (Join-Path $taskBin 'CoreTests.exe') ($taskCore + (Join-Path $taskRoot 'tests\CoreTests.cs'))
    & (Join-Path $taskBin 'CoreTests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Core tests failed.' }
}
if ($Integration) {
    Invoke-Compile (Join-Path $taskBin 'SelectionFixture.exe') @((Join-Path $taskRoot 'tests\SelectionFixture.cs')) 'winexe'
    Invoke-Compile (Join-Path $taskBin 'IntegrationTests.exe') ($taskSources + (Join-Path $taskRoot 'tests\IntegrationTests.cs')) 'exe' (@('/main:AutoCopy.Tests.IntegrationTests', $taskUiResource, $taskMenuResource) + $taskAudioResource)
    & (Join-Path $taskBin 'IntegrationTests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Integration tests failed.' }
}
if ($Preview) {
    Invoke-Compile (Join-Path $taskBin 'RenderSettings.exe') ($taskSources + (Join-Path $taskRoot 'tools\RenderSettings.cs')) 'exe' @('/main:AutoCopy.Tests.RenderSettings', $taskUiResource, $taskMenuResource)
    & (Join-Path $taskBin 'RenderSettings.exe') (Join-Path $taskBin 'settings-preview.png')
    if ($LASTEXITCODE -ne 0) { throw 'Settings render failed.' }
}
if ($UiTest) {
    Invoke-Compile (Join-Path $taskBin 'SelectionFixture.exe') @((Join-Path $taskRoot 'tests\SelectionFixture.cs')) 'winexe'
    Invoke-Compile (Join-Path $taskBin 'InterfaceTests.exe') @((Join-Path $taskRoot 'src\Settings.cs'),(Join-Path $taskRoot 'src\SelectionPolicy.cs'),(Join-Path $taskRoot 'tests\InterfaceTests.cs'))
    & (Join-Path $taskBin 'InterfaceTests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Interface tests failed.' }
}
if ($PolishTest) {
    Invoke-Compile (Join-Path $taskBin 'DesktopPolishTests.exe') ($taskSources + (Join-Path $taskRoot 'tests\DesktopPolishTests.cs')) 'exe' (@('/main:AutoCopy.Tests.DesktopPolishTests', $taskUiResource, $taskMenuResource) + $taskAudioResource)
    & (Join-Path $taskBin 'DesktopPolishTests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Desktop polish tests failed.' }
}
Write-Output 'Build complete.'
