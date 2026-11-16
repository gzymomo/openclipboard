# macOS / Linux 来源应用图标实现方案

本文档基于当前 `OpenClipboard.App`（Avalonia 跨平台版）的实现现状，给出在 macOS 与 Linux 平台支持“剪贴板来源应用图标（卡片右上角）”的落地方案。

## 当前现状（基线）

- 已在 Windows 平台实现来源应用识别与图标提取/缓存，并在卡片中显示来源图标。
- 核心接入点：
  - `src/OpenClipboard.App/Services/Source/ISourceAppProvider.cs`
  - `src/OpenClipboard.App/Services/Source/SourceAppProvider.cs`
  - `src/OpenClipboard.App/Services/Source/SourceAppInfo.cs`
  - `src/OpenClipboard.App/MainWindow.axaml.cs`（`ClipboardTimer_Tick` 采集来源并写入条目）
  - `src/OpenClipboard.App/Models/ClipboardHistoryItem.cs`（`SourceAppName` / `SourceAppId` / `SourceIconPath` / `SourceIconBitmap`）
  - `src/OpenClipboard.App/Services/ClipboardStore.cs`（来源字段持久化）
  - `src/OpenClipboard.App/MainWindow.axaml`（卡片头部显示来源图标）
- 当前 `SourceAppProvider` 仅在 `OperatingSystem.IsWindows()` 时返回来源信息；macOS/Linux 返回 `null`（功能降级，不影响其他剪贴板功能）。
- 图标缓存目录（Windows 现状）：`%APPDATA%\OpenClipboard\Icons\{appId}.png`（按 app 标识哈希命名）。

> 建议实施基线：以包含来源图标/新类型识别/历史保留刻度等改动的当前分支代码为准（实施前确认本地提交与目标分支一致）。

## 目标

- 在 **macOS** 上尽量达到与 Windows 接近的体验：可识别来源应用并显示应用图标。
- 在 **Linux** 上提供分层支持：优先 **X11** 可用，**Wayland** 在通用场景下优先保证功能可降级（无图标/未知来源），后续按桌面环境扩展。

## 总体设计

### 1. 保持数据模型与 UI 不变
继续复用现有字段与展示链路：
- 采集：`SourceAppInfo(AppName, AppId, IconPath)`
- 存储：`ClipboardHistoryItem` / `ClipboardStore` 已有来源字段
- 展示：`MainWindow` 加载 `SourceIconPath` 为 `SourceIconBitmap`，卡片头部显示

### 2. 重构来源采集为平台分发
将 `SourceAppProvider` 从“Windows 单实现”改为“统一入口 + 平台实现”：

- `SourceAppProvider`：仅负责按平台分发与通用兜底
- `ISourceAppProvider`（或新增 `IPlatformSourceAppProvider`）：统一接口
- 平台实现建议：
  - `WindowsSourceProvider`（迁移现有逻辑）
  - `MacSourceProvider`
  - `LinuxSourceProvider`（内部可再拆 X11/Wayland 策略）

建议目录结构（示例）：
- `Services/Source/SourceAppProvider.cs`（分发）
- `Services/Source/Windows/WindowsSourceProvider.cs`
- `Services/Source/Mac/MacSourceProvider.cs`
- `Services/Source/Linux/LinuxSourceProvider.cs`（或 `X11SourceProvider` + `WaylandSourceProvider`）

### 3. 统一图标缓存策略
- 统一缓存根目录逻辑（复用/抽取现有按 OS 选择数据目录的逻辑）：
  - Windows: `%APPDATA%\OpenClipboard\Icons`
  - macOS: `~/Library/Application Support/OpenClipboard/Icons`
  - Linux: `$XDG_DATA_HOME/OpenClipboard/Icons` 或 `~/.local/share/OpenClipboard/Icons`
- 文件命名：`{StableAppIdHash}.png`（与现有 Windows 一致）
- 缓存命中直接返回路径；提取失败返回 `IconPath = null`（仅显示名称或无图标）
- 过滤自身应用（避免把 OpenClipboard 识别为来源）

## macOS 实现方案（优先）

### 目标策略
在检测到剪贴板变化时，采用与当前 Windows 类似的启发式：**获取前台应用（frontmost app）作为来源**，提取应用图标并缓存为 PNG。

### 来源应用识别
- 通过 `NSWorkspace.sharedWorkspace.frontmostApplication` 获取：
  - `localizedName` -> `AppName`
  - `bundleIdentifier`（优先）/ `bundleURL.path` / `processIdentifier` -> 生成 `AppId`
  - `bundleURL` / 应用路径（用于取图标）
- `AppId` 建议优先级：
  1. `bundleIdentifier`（稳定，推荐）
  2. bundle 路径
  3. 进程名 + 路径哈希（兜底）

### 图标提取与缓存
- 获取图标来源：
  - `NSRunningApplication.icon`
  - 或 `NSWorkspace.iconForFile(bundlePath)`
- 将 `NSImage` 转换为 PNG（建议 64x64 或 128x128）并写入统一缓存目录
- 若图标获取失败，仍可返回 `AppName/AppId`（`IconPath` 为空）

### 与 .NET 的互操作方式（可选）
- **方案 A：P/Invoke + Objective-C Runtime**（推荐先评估）
  - 通过 `libobjc` / `objc_msgSend` 调用 AppKit/Foundation API
  - 优点：无需额外原生工程产物；缺点：实现细节较多、签名/内存管理需谨慎
- **方案 B：原生辅助组件（dylib / 小型 helper）**
  - 用 ObjC/Swift 封装“获取 frontmost app + 导出 PNG”接口，.NET 调用
  - 优点：可维护性更好、API 更清晰；缺点：构建与分发链路增加

### macOS 注意事项
- 前台应用归因在少数场景下可能与“实际写入剪贴板的应用”不完全一致（与当前 Windows 前台窗口启发式一致的取舍）
- 需在 `osx-arm64` / `osx-x64` 目标上验证
- 全局快捷键相关权限（辅助功能/输入监控）与本特性不同，但建议一并做平台验证

## Linux 实现方案（分层）

Linux 需按显示协议/桌面环境分层支持，核心原则是：**能识别就识别并缓存图标，不能识别则优雅降级**。

### A. X11（优先实现）

#### 来源应用识别
- 获取活动窗口：`_NET_ACTIVE_WINDOW`（EWMH）
- 读取窗口属性：
  - `_NET_WM_PID`（进程 ID）
  - `WM_CLASS`（应用识别的重要线索）
  - `_NET_WM_NAME` / `WM_NAME`（可选，用于调试或兜底）
- 通过 PID 解析进程信息：
  - `/proc/<pid>/exe`
  - `/proc/<pid>/cmdline`
- `AppId` 建议优先级：
  1. `WM_CLASS`（规范化后）
  2. `.desktop` 文件标识（若能匹配）
  3. 可执行文件路径哈希/进程名哈希

#### 图标提取
推荐按以下优先级尝试：
1. **基于 `WM_CLASS` / desktop entry 查找图标**
   - 查找 `.desktop` 文件（如 `/usr/share/applications`, `~/.local/share/applications`）
   - 读取 `Icon=`
   - 在 icon theme 目录中解析图标文件（PNG/SVG/XPM 等，优先 PNG；SVG 可视依赖决定是否转换）
2. **窗口图标属性 `_NET_WM_ICON`**（若存在）
   - 解析原始像素数据并编码为 PNG 缓存
3. **兜底**
   - 无法获取图标则仅返回名称/ID（`IconPath = null`）

#### 实现注意点
- 需要处理 icon theme 查找（尺寸、主题继承、fallback hicolor）
- X11 环境下可使用 P/Invoke 调用 libX11/Xatom 等，或封装原生辅助逻辑

### B. Wayland（通用场景降级优先）

#### 限制
- Wayland 安全模型默认不允许普通应用查询全局活动窗口/其他应用窗口信息
- 因此**通用、稳定、跨 compositor 的来源应用识别方案不可用**

#### 降级策略（建议默认）
- 剪贴板内容采集与类型识别照常工作
- 来源应用信息返回 `null` 或仅返回可安全获得的最小信息（通常不可得）
- UI 保持无来源图标显示（与当前 macOS/Linux 未实现时一致）

#### 可选增强（后续按需）
仅在明确目标桌面环境时考虑，且需标注为“环境特定”：
- 特定 compositor / 桌面扩展 / DBus 接口（可移植性差）
- 原生 helper/插件机制由具备权限的组件上报来源信息（部署复杂）

## 实施步骤建议

1. **重构分发层（不改变 Windows 行为）**
   - 抽出 Windows 逻辑到平台类
   - `SourceAppProvider` 改为平台分发 + 统一异常兜底
2. **抽取图标缓存公共逻辑**
   - 目录选择、文件命名、命中复用、写入 PNG 的公共方法
3. **实现 macOS Provider**
   - frontmost app 识别 + 图标提取 + 缓存
4. **实现 Linux Provider（X11 优先）**
   - X11 活动窗口/进程识别 + icon theme/窗口图标解析
   - Wayland 分支返回降级结果
5. **测试与验证**
   - Windows 回归（确保重构后行为不变）
   - macOS (`osx-arm64`/`osx-x64`) 实测常见应用
   - Linux X11/Wayland 分别实测并记录降级行为

## 验证清单

### 功能
- 从不同应用复制文本/图片/文件时，新增记录是否写入正确来源信息
- 来源图标缓存是否命中复用（同一应用重复复制不重复提取）
- OpenClipboard 自身复制是否被正确过滤
- 来源获取失败时是否仅无图标，不影响剪贴板记录功能

### 平台
- **Windows**：重构后回归通过
- **macOS**：主流应用（浏览器、编辑器、终端、Finder 等）图标与名称正确
- **Linux X11**：主流应用可识别；图标匹配准确率可接受
- **Linux Wayland**：确认按设计降级（无崩溃、无阻塞）

## 风险与边界

- **归因准确性**：当前采用“剪贴板变化时的前台应用”启发式，少数场景可能与实际来源进程不一致
- **Linux 图标匹配复杂度**：`WM_CLASS` 与 desktop entry/icon theme 映射存在环境差异
- **Wayland 平台限制**：通用来源识别/取图标能力受限，需接受降级
- **性能**：图标提取应仅在缓存未命中时执行，避免在轮询线程做重复重活

## 后续可选增强

- 增加来源识别调试日志（受配置开关控制）
- 提供“显示来源图标”设置开关（便于排查平台兼容性问题）
- 增加来源信息兜底展示（无图标时显示应用名缩写徽标）
- 针对特定 Linux 桌面环境做可选适配（明确标注非通用能力）
