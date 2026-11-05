#requires -Version 5.1
<#
.SYNOPSIS
    构建并打包 OpenClipboard（WPF 剪贴板管理器）。

.DESCRIPTION
    1) 还原 NuGet 依赖
    2) 按架构发布到 dist\OpenClipboard-<arch>
    3) 生成 dist\OpenClipboard-<arch>-<版本>.zip

    目标框架 net472，需要 .NET Framework 4.7.2+ 运行时。

.PARAMETER Architecture
    x86、x64 或 all（默认 all）

.EXAMPLE
    .\build.ps1
    .\build.ps1 -Architecture x64
    .\build.ps1 -Configuration Debug -Architecture x86
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [ValidateSet('x86', 'x64', 'all')]
    [string]$Architecture = 'all',

    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version = '1.0.0'
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$src = Join-Path $root 'src'
$dist = Join-Path $root 'dist'
$sln = Join-Path $src 'OpenClipboard.sln'
$project = Join-Path $src 'Clipboard.Desktop\Clipboard.Desktop.csproj'

$architectures = if ($Architecture -eq 'all') { @('x86', 'x64') } else { @($Architecture) }

Write-Host "==> 还原依赖" -ForegroundColor Cyan
dotnet restore $sln
if ($LASTEXITCODE -ne 0) { exit 1 }

$results = @()

foreach ($arch in $architectures) {
    $rid = "win-$arch"
    $out = Join-Path $dist "OpenClipboard-$arch"

    Write-Host ""
    Write-Host "==> 发布 $arch 到 $out" -ForegroundColor Cyan

    if (Test-Path $out) { Remove-Item $out -Recurse -Force }
    # 切换架构时强制重新编译，避免复用另一架构的中间产物
    Remove-Item (Join-Path $src 'Clipboard.Desktop\obj\Release'),
                (Join-Path $src 'Clipboard.Desktop\bin\Release') `
        -Recurse -Force -ErrorAction SilentlyContinue

    dotnet publish $project -c $Configuration -r $rid -o $out --no-restore "-p:Version=$Version"
    if ($LASTEXITCODE -ne 0) { exit 1 }

    # 只保留当前架构的原生库
    foreach ($other in @('win-arm', 'win-x86', 'win-x64') | Where-Object { $_ -ne $rid }) {
        $native = Join-Path $out "runtimes\$other"
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
        "runtimes\$rid\native\e_sqlite3.dll"
    )
    $missing = $required | Where-Object { -not (Test-Path (Join-Path $out $_)) }
    if ($missing) {
        Write-Host "!! 发布产物缺少必需文件:" -ForegroundColor Red
        $missing | ForEach-Object { Write-Host "   - $_" -ForegroundColor Red }
        exit 1
    }

    # WPF 资源容器必须嵌入进程序集（两个架构共用同一份编译产物结构，校验一次即可）
    if (-not $script:resourcesVerified) {
        $expectedResources = @('Clipboard.Desktop.g.resources')
        $actualResources = [System.Reflection.Assembly]::ReflectionOnlyLoadFrom(
            (Join-Path $out 'Clipboard.Desktop.exe')).GetManifestResourceNames()
        $badResources = $expectedResources | Where-Object { $actualResources -notcontains $_ }
        if ($badResources) {
            Write-Host "!! 程序集缺少嵌入资源:" -ForegroundColor Red
            $badResources | ForEach-Object { Write-Host "   - $_" -ForegroundColor Red }
            exit 1
        }
        $script:resourcesVerified = $true
    }

    Write-Host "==> 打包 $arch zip" -ForegroundColor Cyan
    $zip = Join-Path $dist "OpenClipboard-$arch-$Version.zip"
    if (Test-Path $zip) { Remove-Item $zip -Force }

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

    $results += [pscustomobject]@{
        Architecture = $arch
        Output       = $out
        Zip          = $zip
        SizeMB       = [math]::Round((Get-Item $zip).Length / 1MB, 1)
        Version      = $Version
    }
}

Write-Host ""
Write-Host "完成。" -ForegroundColor Green
foreach ($r in $results) {
    Write-Host "  [$($r.Architecture)] $($r.Output)\Clipboard.Desktop.exe"
    Write-Host "        zip: $($r.Zip)  ($($r.SizeMB) MB)"
}
Write-Host ""
Write-Host "  运行要求   : Windows + .NET Framework 4.7.2+" -ForegroundColor DarkGray
Write-Host "  数据位置   : %APPDATA%\OpenClipboard\database.db" -ForegroundColor DarkGray
