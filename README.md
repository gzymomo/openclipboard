# OpenClipboard

[![Build](https://github.com/gzymomo/openclipboard/actions/workflows/build.yml/badge.svg)](https://github.com/gzymomo/openclipboard/actions/workflows/build.yml)

一个轻量的 Windows 剪贴板历史管理器，使用 **WPF / .NET Framework 4.7.2** 编写。

> 本项目由反编译产物整理、重建而来，仅用于学习、研究与互操作性目的。

## 功能

- 剪贴板历史：文本、图片、文件
- 历史记录列表、收藏、搜索、多选
- 全局热键呼出（默认 `F2`，可在设置里修改）
- 托盘图标与右键菜单
- 开机自启
- 多语言（简体中文、英、德、法、日、韩、俄、西、葡、泰、越）
- 毛玻璃（Acrylic/Frosted）窗口效果，透明度可调
- 清除历史（真正删除并 `VACUUM` 数据库）
- 数据本地存储，SQLite

## 系统要求

- Windows 10 1607+ / Windows 11
- .NET Framework 4.7.2+（Windows 10/11 已自带）
- 毛玻璃效果需要 Windows 10 1803+；圆角需要 Windows 11

## 跨平台版本

跨平台入口位于 `src/OpenClipboard.App`，使用 .NET 10 和 Avalonia，可面向 Windows、Linux、macOS 构建，并支持 x64/ARM64 运行时标识。支持文本/图片/文件剪贴板历史、搜索、收藏、删除/清理、托盘、登录启动，以及从旧版 SQLite 数据库导入。推送 `v*` 发布标签时，GitHub Release 会附带 Windows、Linux、macOS 的 x64/ARM64 六种跨平台 ZIP 包。原 Windows/WPF 版本保持不变。

跨平台版设置中可更改 11 种界面语言、激活热键、快速复制热键、历史保留期限和透明度。快速复制默认禁用，启用后会把最近一条历史记录复制到系统剪贴板；激活热键默认为 `F2`。历史期限支持不保存、7 天、30 天和永久；收藏记录不会因期限清理而删除，过期记录会在启动、修改期限时及运行期间定期清理。

版本号以发布标签为准：推送 `vMAJOR.MINOR.PATCH`（例如 `v1.2.3`）会将 `1.2.3` 写入程序集版本信息、WPF 设置窗口及跨平台窗口标题，并用于 Windows 发布包文件名；不符合该格式的 `v*` 标签会在发布构建中失败。

窗口使用 Avalonia 的 Acrylic 透明级别提示和可调透明度；实际模糊、圆角样式取决于操作系统和桌面合成器，不支持 Acrylic 的环境会退化为普通透明背景。

需要 [.NET SDK 10.0+](https://dotnet.microsoft.com/download)：

```sh
dotnet test src/OpenClipboard.CrossPlatform.slnx -c Release
dotnet build src/OpenClipboard.CrossPlatform.slnx -c Release
dotnet run --project src/OpenClipboard.App/OpenClipboard.App.csproj
```

跨平台版本将数据保存在当前平台的应用数据目录下 `OpenClipboard/cross-platform.db`，与现有 Windows 版本的数据库分开；可通过“Import old database...”手动导入旧版数据库。

> 全局热键受系统权限和桌面环境限制：macOS 需要授予辅助功能权限，Linux Wayland 环境可能需要额外权限。热键不可用时可通过托盘图标打开程序。

## 构建

需要 [.NET SDK 8.0+](https://dotnet.microsoft.com/download)：

```powershell
.\build.ps1
```

输出：

```
默认同时构建 x86 和 x64：

```
dist\OpenClipboard-x86\              # 32 位，可直接运行
dist\OpenClipboard-x64\              # 64 位，可直接运行
dist\OpenClipboard-x86-<版本>.zip    # 32 位免安装包
dist\OpenClipboard-x64-<版本>.zip    # 64 位免安装包
```

只构建其中一个架构：

```powershell
.\build.ps1 -Architecture x64
```

### 生成 EXE 安装包

```powershell
.\build-installer.ps1
```

输出：

```
dist\OpenClipboard-Setup-x86-<版本>.exe
dist\OpenClipboard-Setup-x64-<版本>.exe
```

- 当前用户安装（`%LOCALAPPDATA%\Programs\OpenClipboard`），无需管理员权限
- 首次运行会自动下载 NSIS 3.10 到 `%LOCALAPPDATA%\OpenClipboardBuild`
- 安装包未签名，首次运行可能触发 SmartScreen 提示

也可以直接构建解决方案：

```powershell
dotnet build src\OpenClipboard.sln -c Release
```

> 依赖通过 NuGet 还原，首次构建需要联网。

### GitHub Actions

仓库自带 CI（`.github/workflows/build.yml`）：

- 每次 push / PR 自动在 `windows-latest` 上构建并打包
- 产物（x86/x64 的 zip 与安装包，共 4 个）上传到该次 Actions 运行的 **Artifacts**
- 推送 `v*` 标签（如 `v1.0.0`）会自动创建 GitHub Release 并附带全部 4 个包

## 运行

1. 运行 `dist\OpenClipboard-x64\Clipboard.Desktop.exe`（或 x86 目录）
2. 程序启动后驻留托盘，主窗口默认隐藏
3. 按热键（默认 `F2`）或点击托盘图标呼出
4. 设置页可修改热键、语言、记录天数、毛玻璃不透明度

数据位置：

```
%APPDATA%\OpenClipboard\database.db
```

## 项目结构

```
src/
  Clipboard.Data/        # 数据模型与枚举（netstandard2.0 / net10.0）
  Clipboard.Desktop/     # WPF 主程序（net472）
  OpenClipboard.App/     # 跨平台 Avalonia 主程序（net10.0）
  OpenClipboard.App.Tests/ # 跨平台数据存储测试
  OpenClipboard.CrossPlatform.slnx # 跨平台解决方案
  OpenClipboard.sln               # Windows/WPF 解决方案
build.ps1                # 构建与打包脚本
build-installer.ps1      # 生成 EXE 安装包
installer/               # NSIS 安装包脚本
```

## 已知限制

- 各平台的全局热键可用性和 Acrylic/圆角视觉效果由系统权限及桌面合成器决定，无法保证与 Windows DWM 完全一致
- 跨平台版数据库与 WPF 版分开保存，需手动导入旧版历史记录

## 第三方组件

| 组件 | 版本 | 许可证 |
|---|---|---|
| HandyControl | 3.0 | MIT |
| GalaSoft.MvvmLight | 5.4.1 | MIT |
| Microsoft.EntityFrameworkCore | 3.1 | Apache-2.0 |
| Newtonsoft.Json | 13.0.3 | MIT |
| SQLitePCLRaw | 2.x | Apache-2.0 |
| Microsoft.Data.Sqlite | 5.x（WPF）/ 10.0（跨平台） | MIT |
| Microsoft.Extensions.* | 3.1 | MIT |
| Avalonia | 12.1.5 | MIT |
| SharpHook | 8.0.1 | MIT |

各组件版权归其原作者所有，使用时请遵守对应许可证。

## 许可证

本项目基于 [MIT License](LICENSE) 开源。

## 免责声明

- 本项目与任何原软件厂商无关，也不包含其商标或品牌信息
- 代码来源于对已发布程序的反编译与整理，请在使用、分发前自行确认你拥有相应权利，并遵守当地法律
- 软件按“现状”提供，不附带任何明示或暗示担保
