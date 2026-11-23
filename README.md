# OpenClipboard

[English](README.en.md) | **简体中文**

**你的剪贴板历史，随取随用。**

一款轻量、开源的剪贴板历史管理器。全局热键从桌面底部呼出全宽面板，横向卡片记录文本、图片、文件等内容，支持搜索、收藏、来源应用图标识别与本地隐私存储，让每一次复制都可追溯、可复用。

[官网](https://www.openclipboard.cc.cd) · [下载最新版](https://github.com/gzymomo/openclipboard/releases) · [反馈建议](https://github.com/gzymomo/openclipboard/issues)

[![Windows build](https://github.com/gzymomo/openclipboard/actions/workflows/win.yml/badge.svg)](https://github.com/gzymomo/openclipboard/actions/workflows/win.yml)
[![Linux build](https://github.com/gzymomo/openclipboard/actions/workflows/linux.yml/badge.svg)](https://github.com/gzymomo/openclipboard/actions/workflows/linux.yml)
[![macOS build](https://github.com/gzymomo/openclipboard/actions/workflows/macos.yml/badge.svg)](https://github.com/gzymomo/openclipboard/actions/workflows/macos.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

---

## 为什么选择 OpenClipboard

- **随手唤起，不卡工作流**：全局热键（默认 `F2`）从桌面底部呼出固定全宽面板，不打断当前操作
- **历史一目了然**：横向卡片列表按时间展示，双击即可复制回剪贴板
- **内容类型更清晰**：文本、图片、文件、颜色、URL、JSON、XML、Markdown 等类型化卡片与预览（含 JSON/XML 高亮、Markdown 渲染）
- **知道“从哪复制的”**：记录来源应用名称与图标（Windows/macOS 优先支持，Linux X11 尽力识别，Wayland 自动降级）
- **本地优先，隐私可控**：数据存储在本地 SQLite，不依赖云同步，可配置保留期限、清理与收藏策略
- **跨平台可用**：支持 Windows、macOS、Linux（x64 / ARM64）

> 预览与功能演示：**https://www.openclipboard.cc.cd**

![OpenClipboard 预览](docs/assets/demo.png)

## 核心功能

- **多类型剪贴板历史**：自动记录文本、图片、文件（及颜色/URL/JSON/XML/Markdown 等识别类型）
- **横向卡片面板**：桌面底部全宽、固定尺寸面板；类型着色卡片头、来源图标、收藏标记与内容预览
- **搜索与收藏**：快速筛选历史，收藏常用条目（收藏项可不受过期清理影响）
- **高效操作**：双击复制、多选与右键菜单（复制/收藏/删除）、一键跳转最新
- **系统集成**：托盘驻留、全局热键、开机自启（按平台能力支持）
- **个性化设置**：多语言界面、激活/快速复制热键、历史保留期限、透明度/毛玻璃效果、滚轮速度等
- **数据管理**：本地 SQLite 存储、清空历史、支持从旧版 Windows 数据库导入

## 下载与运行

前往 [**GitHub Releases**](https://github.com/gzymomo/openclipboard/releases) 下载对应平台的预编译包（Windows / macOS / Linux，x64 / ARM64）。

- **跨平台版（推荐，活跃维护）**：`OpenClipboard.App`（.NET 10 + Avalonia）
  - **Windows**：发布为自包含的 `.zip` 与 `.exe` 安装包（x64 / ARM64），**无需安装 .NET**；可免安装解压运行或使用安装程序安装
  - **Linux**：发布为自包含的 `.deb` 包（amd64 / arm64），**无需安装 .NET**；可通过 `dpkg` 安装
  - **macOS**：发布为自包含的 `.dmg`（内含 `OpenClipboard.app`），**无需安装 .NET**；双击挂载后拖入「应用程序」即可
- **Windows 经典版（WPF，维护较少）**：`Clipboard.Desktop`（.NET Framework 4.7.2）
  - Windows 10/11 通常已自带所需 .NET Framework；旧系统可能需安装 [.NET Framework 4.7.2+](https://dotnet.microsoft.com/download/dotnet-framework)

### 使用提示
- 启动后默认驻留托盘，按 `F2`（可在设置中修改）呼出面板
- 全局热键可用性受系统权限/桌面环境影响：macOS 需辅助功能权限；Linux Wayland 下热键需要读取原始输入设备，`.deb` 安装时会自动创建 `openclipboard` 系统组并配置 udev 规则，使本应用可读取 `/dev/input/event*`（安装后需**重新登录一次**生效）。这意味着应用能读取全部键盘输入，请知悉；若介意，可改用托盘图标呼出。来源应用识别在 Wayland 下仍会降级
- Linux 若使用免安装的 `.zip` 而非 `.deb`，Wayland 下没有上述授权，全局热键不可用（Xorg 会话不受影响），请通过托盘图标打开面板
- macOS 包为 ad-hoc 签名、未公证；从 dmg 拖出应用后首次打开如提示「无法验证开发者」，执行一次：
  ```sh
  xattr -dr com.apple.quarantine /Applications/OpenClipboard.app
  ```
- 跨平台版数据目录：各平台应用数据目录下 `OpenClipboard/cross-platform.db`（与旧版 WPF 数据库分开，可在设置/功能中导入旧库）

## 从源码构建

### 跨平台版（.NET 10）

```sh
dotnet build src/OpenClipboard.CrossPlatform.slnx -c Release
dotnet run --project src/OpenClipboard.App/OpenClipboard.App.csproj
```

发布示例（Windows x64，自包含）：

```sh
dotnet publish src/OpenClipboard.App/OpenClipboard.App.csproj -c Release -r win-x64 --self-contained true
```

Linux 使用自包含发布并打包为 `.deb`（需 `dpkg-deb`）：

```sh
dotnet publish src/OpenClipboard.App/OpenClipboard.App.csproj -c Release -r linux-x64 --self-contained true
chmod +x installer/linux/package-deb.sh
installer/linux/package-deb.sh \
  src/OpenClipboard.App/bin/Release/net10.0/linux-x64/publish \
  1.1.7 amd64 artifacts/openclipboard_1.1.7_amd64.deb
```

macOS 使用独立的自包含打包流程，产物是内含 `OpenClipboard.app` 的 `.dmg`：

```sh
dotnet publish src/OpenClipboard.App/OpenClipboard.App.csproj -c Release -r osx-x64 \
  --self-contained true
installer/macos/package.sh \
  src/OpenClipboard.App/bin/Release/net10.0/osx-x64/publish \
  1.1.7 artifacts/OpenClipboard-osx-x64-1.1.7.dmg
```

> 不要用 `-p:AssemblyName=OpenClipboard` 全局覆盖程序集名，它会被应用到所有 ProjectReference 并导致 `Ambiguous project name`。`package.sh` 只在 bundle 内把 apphost 重命名为 `OpenClipboard`，程序集名和 `avares://OpenClipboard.App/...` 资源 URI 保持不变。
>
> apphost 改名是安全的：宿主内部仍然查找 `OpenClipboard.App.dll`；但 macOS 会把以 `.App` 结尾的可执行文件当成应用包，Gatekeeper 会直接结束进程。

### Windows 经典版（WPF）

```powershell
.\build.ps1                    # 构建并打包
.\build-installer.ps1          # 生成安装包（NSIS）
```

## 项目结构

```text
src/
  OpenClipboard.App/             # 跨平台 Avalonia 主程序（net10.0，推荐）
  OpenClipboard.App.Tests/       # 跨平台测试
  OpenClipboard.CrossPlatform.slnx
  Clipboard.Desktop/             # Windows WPF 经典版（net472）
  Clipboard.Data/                # 数据模型与枚举
docs/                            # 官网/宣传页源码（GitHub Pages）
.github/workflows/               # CI（版本 tag 或手动触发）
```

## 隐私与说明

- 剪贴板历史默认仅保存在本地 SQLite 数据库，不上传云端
- 请在共享设备或敏感环境中结合自身需求配置保留与清理策略
- 来源应用识别、热键、透明效果等能力受操作系统权限与桌面环境限制，部分场景会自动降级

## 许可证与免责声明

- 本项目基于 [MIT License](LICENSE) 开源
- 本项目由反编译产物整理、重建而来，仅用于学习、研究与互操作性目的；与任何原软件厂商无关，也不包含其商标或品牌信息
- 请在使用、分发前自行确认你拥有相应权利并遵守当地法律；软件按“现状”提供，不附带任何明示或暗示担保

---

**官网：** https://www.openclipboard.cc.cd  
**下载：** https://github.com/gzymomo/openclipboard/releases
