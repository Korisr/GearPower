# GearPower v1.2.0-beta

将 Cxxhh/GearPower v2.0.0 的界面功能移植回单文件 WPF 版本。

## 新增

- **设备适配页签**：从接口生成草稿、JSON 编辑、测试、导入启用、导出和移除
- **声明式配置引擎**：output / feature / featureQuery / input，多步查询、sum8/xor8 校验、前缀、字段 mask/shift、大小端和条件
- **用户配置**：`%LOCALAPPDATA%\GearPower\profiles\<id>.json`，同 id 覆盖随附配置，每次扫描热加载
- **示例配置**：HJSDZHI S1-V2、LEOBOG HI98PRO，位于 exe 旁 `profiles/`
- **设备清单分组**：按键盘 / 鼠标 / 耳机 / 麦克风 / 拓展坞等排序，同一物理设备多接口合并，可折叠
- **分类修正**：HM HUB / HM HubX 归为拓展坞，NINX NX60 归为键盘，Razer Seiren 归为麦克风
- **复制给 Agent / 复制诊断**：每个设备组提供两个按钮；点击单个接口行仍复制该接口诊断
- **实时日志**：设置页「实时日志」显示最近 1000 行，可刷新、清空、导出

## 未移植

- Rust 核心、`gearpower-cli`、macOS/Linux 支持
- 仍需 CLI 的跨平台设备扫描
- 复杂内置协议仍保留在本版本 C# 中

## 说明

- 这是 beta 版
- JSON 配置只描述 HID 查询和数值解析，不能执行脚本、DLL 或外部程序
- 没有协议资料或真实抓包时不要猜查询命令；保持 `enabled: false`
- 包含 1.1.5-beta 的 Hwnd 修复、Logitech 电量/设备名、ROG Falchion、Razer 等已有功能

## 下载

- `GearPower.exe`
- `profiles/` 目录
- `SHA256SUMS.txt`

SHA256：

```text
70dca156788e88a9d30a071bc4a60ebcde9f4ac651ffc6c6be3bb235240cd5ed
```

文件大小：`349,184` 字节

## 注意

- Windows 10/11 64 位，需要 .NET Framework 4.8
- exe 未签名，SmartScreen 可能提示未知发布者
