# BamlDecompiler

把 `.baml`（WPF 编译后的 BAML）还原成可读的 `.xaml`。

底层使用 ILSpy 的 [`ICSharpCode.BamlDecompiler`](https://github.com/icsharpcode/ILSpy) 9.1.0，因此输出质量与 ILSpy 一致：保留 `x:Class`、`Grid.Row/Column`、`WindowChrome.WindowChrome`、`{x:Static}`、`{x:Null}`、事件处理器名等。

## 为什么不用 tools/wpfbaml2xaml

`tools/wpfbaml2xaml` 是第三方手写转换器，有三类问题：

- 附加属性丢失宿主类型，例如 `<Grid Row="0">`（应为 `Grid.Row`）、`WindowChrome="{WindowChrome ...}"`（应为属性元素）
- 标记扩展还原错误：`{Static Text}`（应为 `{x:Static ...}`）、`{Null}`（应为 `{x:Null}`）
- 把复杂对象写成 `Thumb="{Thumb ...}"` 这类非法语法

这些输出无法通过 WPF 的 XAML 编译。

另外 `tools/Baml2Xaml` 只是**扫描 PE 找 MSBAML 特征并切 2MB 大块**的提取器，切出来的不是单个资源，转出来的 XAML 只有 `null`（`baml_output/` 里那些文件就是这样来的）。

## 用法

```powershell
# 1) 从一个程序集的 .g.resources 批量还原（推荐）
dotnet run --project tools/BamlDecompiler -c Release -- `
  "<原版程序目录>\Clipboard.Desktop.exe" `
  "<原版程序目录>\Clipboard.Desktop.g.resources" `
  <输出目录>

# 2) 从单个 .baml 还原
dotnet run --project tools/BamlDecompiler -c Release -- `
  "<原版程序目录>\Clipboard.Desktop.exe" `
  --baml "<某个>.baml" `
  <输出目录>
```

第一个参数是程序集路径，用于解析 BAML 中引用的自定义类型/程序集（会从程序集所在目录找依赖 DLL）。

## 当前项目状态

`src` 下的 25 个 `.xaml` 已用本工具生成，并且 **Clipboard.Desktop 已改为从 XAML 编译**：

- `src/Clipboard.Desktop/**`：25 个（含 `app.xaml`、`mainwindow.xaml`、语言资源、样式、控件/页面/窗口）

Clipboard.Desktop 的改动：

1. csproj 删除 `Clipboard.Desktop.g.resources` 的 `EmbeddedResource`，改用 SDK 默认的 `Page`/`ApplicationDefinition`
2. 图片与图标改为 `<Resource>`：`resources/images/file.png`、`resources/images/logo.png`、`logo.ico`
3. 13 个代码后置类改为 `partial`，删除反编译产生的 `InitializeComponent`、`Connect`、`_CreateDelegate`、`_contentLoaded`、`Main`（App）以及 `x:Name` 字段
4. `app.xaml` 增加 `StartupUri="MainWindow.xaml"`
5. 所有 `Name=` 统一改成 `x:Name=`

验证结果：

- `build.ps1` 通过（0 错误），产物内 `g.resources` 共 28 项，与旧 BAML 集合完全一致
- `obj\Release\net472\**\*.g.cs` 生成 13 个代码后置文件，确认走的是 XAML 编译
- 运行 90 秒无异常，无 XamlParse 错误

`src\Clipboard.Desktop` 下的 `*.baml`、`Clipboard.Desktop.g.resources` 以及之前的 `baml_output\` 已清理（构建只依赖 `*.xaml`）。


