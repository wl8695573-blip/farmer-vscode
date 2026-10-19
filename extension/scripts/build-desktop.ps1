$ErrorActionPreference = 'Stop'
$sourceRoot = Split-Path -Parent $PSScriptRoot
$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compilerPath)) {
    $compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $compilerPath)) { throw 'Install .NET Framework 4.x to build the desktop component.' }
$exePath = Join-Path $sourceRoot 'desktop\CodeWhip.Desktop.exe'
$csPath = Join-Path $sourceRoot 'desktop\Overlay.cs'
$graphicsPath = Join-Path $sourceRoot 'desktop\DesktopGraphics.cs'
& $compilerPath /nologo /target:exe /platform:anycpu /optimize+ /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll "/out:$exePath" $csPath $graphicsPath
if ($LASTEXITCODE -ne 0) { throw 'Desktop build failed.' }
Write-Output "Built $exePath"
