param(
    [ValidateSet('x86', 'x64', 'all')]
    [string]$Architecture = 'all'
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$dist = Join-Path $root 'dist'
$nsisVersion = '3.10'
$nsisCache = Join-Path $env:LOCALAPPDATA "OpenClipboardBuild\nsis-$nsisVersion"

$architectures = if ($Architecture -eq 'all') { @('x86', 'x64') } else { @($Architecture) }

# 检查应用是否已经构建
foreach ($arch in $architectures) {
    $appExe = Join-Path $dist "OpenClipboard-$arch\Clipboard.Desktop.exe"
    if (-not (Test-Path $appExe)) {
        throw "未找到 $appExe，请先运行 .\build.ps1 -Architecture $arch"
    }
}

# 定位 NSIS，缺失时自动下载
$makensis = Get-ChildItem $nsisCache -Recurse -Filter makensis.exe -ErrorAction SilentlyContinue |
    Select-Object -First 1 -ExpandProperty FullName

if (-not $makensis) {
    Write-Host "==> 下载 NSIS $nsisVersion" -ForegroundColor Cyan
    $zip = Join-Path $env:TEMP "nsis-$nsisVersion.zip"
    $url = "https://sourceforge.net/projects/nsis/files/NSIS%203/$nsisVersion/nsis-$nsisVersion.zip/download"

    $curl = Get-Command curl.exe -ErrorAction SilentlyContinue
    if ($curl) {
        & curl.exe -L --fail --retry 3 --max-time 300 --silent --show-error -o $zip $url 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0 -or -not (Test-Path $zip)) {
            throw "下载 NSIS 失败"
        }
    }
    else {
        Invoke-WebRequest -Uri $url -OutFile $zip -UseBasicParsing
    }

    $magic = [System.IO.File]::ReadAllBytes($zip)[0..1]
    if ($magic[0] -ne 0x50 -or $magic[1] -ne 0x4B) {
        throw "下载的 NSIS 包不是有效的 zip（可能是 SourceForge 的跳转页面）"
    }

    New-Item -ItemType Directory -Force $nsisCache | Out-Null
    Expand-Archive -Path $zip -DestinationPath $nsisCache -Force
    Remove-Item $zip -Force
    $makensis = Get-ChildItem $nsisCache -Recurse -Filter makensis.exe |
        Select-Object -First 1 -ExpandProperty FullName
}

if (-not $makensis) {
    throw "NSIS 安装失败：未找到 makensis.exe"
}

$results = @()

foreach ($arch in $architectures) {
    $appExe = Join-Path $dist "OpenClipboard-$arch\Clipboard.Desktop.exe"
    $version = (Get-Item $appExe).VersionInfo.FileVersion

    Write-Host ""
    Write-Host "==> 生成 $arch 安装包 (v$version)" -ForegroundColor Cyan
    & $makensis "/DARCH=$arch" "/DVERSION=$version" (Join-Path $root 'installer\OpenClipboard.nsi')
    if ($LASTEXITCODE -ne 0) {
        throw "makensis 执行失败，退出码 $LASTEXITCODE"
    }

    $setup = Join-Path $dist "OpenClipboard-Setup-$arch-$version.exe"
    if (-not (Test-Path $setup)) {
        throw "安装包未生成：$setup"
    }

    $results += [pscustomobject]@{
        Architecture = $arch
        Setup        = $setup
        SizeMB       = [math]::Round((Get-Item $setup).Length / 1MB, 1)
        Version      = $version
    }
}

Write-Host ""
Write-Host "完成。" -ForegroundColor Green
foreach ($r in $results) {
    Write-Host "  [$($r.Architecture)] $($r.Setup)  ($($r.SizeMB) MB)"
}
Write-Host ""
Write-Host "  安装位置 : %LOCALAPPDATA%\Programs\OpenClipboard(-x64)（当前用户，无需管理员）" -ForegroundColor DarkGray
