# Contributing

欢迎提交设备协议、界面修复和文档改进。

## Build

需要 Windows 10/11 和 .NET Framework 4.8 自带的 `csc.exe`，不需要 Visual Studio 或 .NET SDK：

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

构建产物在 `dist\`，默认会运行 28 项自检。只编译不测试：

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1 -SkipTest
```

## Code style

- C# 5 语法（`/langversion:5`），不要引入 NuGet 依赖
- 界面 XAML 在 `ui.xaml`，运行时通过 `XamlReader.Parse` 加载
- 设备和协议相关逻辑放在 `src\Protocols.cs`、`src\Scanner.cs`
- 不要加入网络请求、遥测或自动更新

## Device / protocol reports

新增设备支持时，请附上：

- 设备型号、VID/PID、连接方式（2.4G / USB / 蓝牙）
- `GearPower.exe --dump` 生成的接口/字段摘要（先删除序列号）
- 能复现的读取结果，或厂商软件显示的电量作为对照

协议字段若来自公开项目，请同时更新 `THIRD_PARTY_NOTICES.md`，不要直接复制其他项目的源代码。

## Pull requests

- 一个 PR 尽量只做一件事
- 说明改动原因、测试方式和受影响的设备
- UI 改动请附截图，并确认窗口缩到最小宽度 `980 × 620` 时不裁切
- 不要提交 `dist\`、日志、缓存或生成的 exe
