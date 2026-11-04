#requires -Version 5.1
<#
.SYNOPSIS
    构建并打包 OpenClipboard（WPF 剪贴板管理器）。

.DESCRIPTION
    1) Release 构建 src\OpenClipboard.sln
    2) 发布到 dist\OpenClipboard
    3) 生成可分发 zip：dist\OpenClipboard-<版本>.zip

    目标框架 net472，需要 .NET Framework 4.7.2+ 运行时。
    原生依赖 e_sqlite3.dll 通过 runtimes\win-x86\native\ 布局分发（32 位进程）。

.EXAMPLE
    .\build.ps1
    .\build.ps1 -Configuration Debug
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$src = Join-Path $root 'src'
$dist = Join-Path $root 'dist'
$out = Join-Path $dist 'OpenClipboard'
$sln = Join-Path $src 'OpenClipboard.sln'

Write-Host "==> 还原依赖" -ForegroundColor Cyan
dotnet restore $sln

Write-Host "==> 构建 ($Configuration)" -ForegroundColor Cyan
dotnet build $sln -c $Configuration --no-restore

Write-Host "==> 发布到 $out" -ForegroundColor Cyan
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
dotnet publish (Join-Path $src 'Clipboard.Desktop\Clipboard.Desktop.csproj') `
    -c $Configuration -o $out --no-restore

# 应用为 32 位，仅保留 x86 原生库，减小分发包体积
foreach ($rid in @('win-arm', 'win-x64')) {
    $native = Join-Path $out "runtimes\$rid"
    if (Test-Path $native) { Remove-Item $native -Recurse -Force }
}

# 关键运行期文件自检
$required = @(
    'Clipboard.Desktop.exe',
    'Clipboard.Desktop.exe.config',
    'Clipboard.Data.dll',
    'HandyControl.dll',
    'GalaSoft.MvvmLight.dll',
    'Microsoft.Data.Sqlite.dll',
    'SQLitePCLRaw.batteries_v2.dll',
    'runtimes\win-x86\native\e_sqlite3.dll'
)
$missing = $required | Where-Object { -not (Test-Path (Join-Path $out $_)) }
if ($missing) {
    Write-Host "!! 发布产物缺少必需文件:" -ForegroundColor Red
    $missing | ForEach-Object { Write-Host "   - $_" -ForegroundColor Red }
    exit 1
}

# WPF 资源容器必须嵌入进程序集（ILSpy 会把 .g.resources 拆散，导致运行期 IOException）
$expectedResources = @('Clipboard.Desktop.g.resources')
$actualResources = [System.Reflection.Assembly]::ReflectionOnlyLoadFrom((Join-Path $out 'Clipboard.Desktop.exe')).GetManifestResourceNames()
$badResources = $expectedResources | Where-Object { $actualResources -notcontains $_ }
if ($badResources) {
    Write-Host "!! 程序集缺少嵌入资源:" -ForegroundColor Red
    $badResources | ForEach-Object { Write-Host "   - $_" -ForegroundColor Red }
    exit 1
}
Write-Host "    嵌入资源校验通过: $($actualResources -join ', ')" -ForegroundColor DarkGray

Write-Host "==> 打包 zip" -ForegroundColor Cyan
$version = (Get-Item (Join-Path $out 'Clipboard.Desktop.exe')).VersionInfo.FileVersion
$zip = Join-Path $dist "OpenClipboard-$version.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
# 使用 .NET 的 ZipFile 生成标准 zip（条目直接位于根目录，不带 ./ 前缀）
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory(
    $out,
    $zip,
    [System.IO.Compression.CompressionLevel]::Optimal,
    $false
)
if (-not (Test-Path $zip)) {
    Write-Host "!! 打包失败" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "完成。" -ForegroundColor Green
Write-Host "  可执行文件 : $out\Clipboard.Desktop.exe"
Write-Host "  压缩包     : $zip  ($([math]::Round((Get-Item $zip).Length/1MB,1)) MB)"
Write-Host "  运行要求   : Windows x64 + .NET Framework 4.7.2+"
Write-Host ""
Write-Host "数据位置   : %APPDATA%\OpenClipboard\database.db" -ForegroundColor DarkGray

