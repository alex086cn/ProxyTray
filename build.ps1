<#
    ProxyTray 构建脚本
    用法： powershell -ExecutionPolicy Bypass -File .\build.ps1
    产物： dist\ProxyTray.exe
#>
[CmdletBinding()]
param(
    [string]$OutputDir = "dist"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Definition
Set-Location $root

$csc = Join-Path $env:SystemRoot "Microsoft.NET\Framework\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) {
    throw "未找到 .NET Framework 4.0 编译器：$csc`n请先安装 .NET Framework 4.0 或更高版本。"
}

$src   = Join-Path $root "src\ProxyTray.cs"
$icon  = Join-Path $root "assets\proxytray.ico"
$outDir = Join-Path $root $OutputDir
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir | Out-Null }

$exe = Join-Path $outDir "ProxyTray.exe"

& $csc /nologo /target:winexe /codepage:65001 /optimize+ /platform:anycpu `
    "/win32icon:$icon" `
    "/out:$exe" `
    /reference:System.dll `
    /reference:System.Drawing.dll `
    /reference:System.Windows.Forms.dll `
    $src

if ($LASTEXITCODE -ne 0) { throw "编译失败，退出码 $LASTEXITCODE" }

$size = (Get-Item $exe).Length
Write-Host ("构建成功：{0}  ({1} 字节)" -f $exe, $size) -ForegroundColor Green
