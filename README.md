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

## 构建

需要 [.NET SDK 8.0+](https://dotnet.microsoft.com/download)：

```powershell
.\build.ps1
```

输出：

```
dist\OpenClipboard\              # 可直接运行的目录
dist\OpenClipboard-<版本>.zip    # 分发包
```

也可以直接构建解决方案：

```powershell
dotnet build src\OpenClipboard.sln -c Release
```

> 依赖通过 NuGet 还原，首次构建需要联网。

### GitHub Actions

仓库自带 CI（`.github/workflows/build.yml`）：

- 每次 push / PR 自动在 `windows-latest` 上构建并打包
- 产物 `OpenClipboard-<版本>.zip` 上传到该次 Actions 运行的 **Artifacts**
- 推送 `v*` 标签（如 `v1.0.0`）会自动创建 GitHub Release 并附带 zip

## 运行

1. 运行 `dist\OpenClipboard\Clipboard.Desktop.exe`
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
  Clipboard.Data/        # 数据模型与枚举（netstandard2.0）
  Clipboard.Desktop/     # WPF 主程序（net472）
    Clipboard.Desktop/            # App、MainWindow
    Clipboard.Desktop.Views.*     # 控件 / 页面 / 窗口
    Clipboard.Desktop.ViewModels/ # MVVM
    Common.*                      # 剪贴板、热键、Hook、SQLite、Interop
    resources/                    # 语言、样式、图片
build.ps1                # 构建与打包脚本
```

## 已知限制

- 仅支持 Windows（WPF）
- “记录天数”目前只在启动时按时间过滤显示，不会自动删除数据库中的旧记录
- 毛玻璃不透明度为 0% 时仅去掉着色，DWM 模糊仍然保留

## 第三方组件

| 组件 | 版本 | 许可证 |
|---|---|---|
| HandyControl | 3.0 | MIT |
| GalaSoft.MvvmLight | 5.4.1 | MIT |
| Microsoft.EntityFrameworkCore | 3.1 | Apache-2.0 |
| Newtonsoft.Json | 13.0.3 | MIT |
| SQLitePCLRaw | 2.x | Apache-2.0 |
| Microsoft.Data.Sqlite | 5.x | MIT |
| Microsoft.Extensions.* | 3.1 | MIT |

各组件版权归其原作者所有，使用时请遵守对应许可证。

## 许可证

本项目基于 [MIT License](LICENSE) 开源。

## 免责声明

- 本项目与任何原软件厂商无关，也不包含其商标或品牌信息
- 代码来源于对已发布程序的反编译与整理，请在使用、分发前自行确认你拥有相应权利，并遵守当地法律
- 软件按“现状”提供，不附带任何明示或暗示担保
