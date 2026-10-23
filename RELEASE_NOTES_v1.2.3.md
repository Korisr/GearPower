# GearPower v1.2.3

Apple 键盘、蓝牙 HFP 耳机与三台外设适配合并正式版。

## 新增

- **Apple Magic Keyboard**（`004C/05AC:0267/026C`）：只读 Input `0x90`，实机 85%
- **蓝牙 HFP 耳机**：扫描 System 类 BTHENUM 节点，HUAWEI FreeBuds Pro 2 实机 90%
- **Razer DeathAdder V4 Pro**（`1532:00BF`）：零访问特性报告，实机 58%
- **Logitech PRO X Wireless（C547）**：接收器在线但耳机休眠时保留缓存卡片，实机 90%
- **ASUS ROG PELTA 2.4GHz**（`0B05:1B84`）：`CC 12 07` 配置，实机 86%

## 修复

- HID Feature 读写和配置执行增加零访问句柄回退
- 蓝牙属性按 DEVPROPTYPE 解析 BYTE / UINT16 / INT32 / UINT32 / STRING，保留 0%
- 未读到电量但已识别的设备保留卡片和诊断信息
- 设备清单显示「已识别 · 未读到电量」

## 设备配置

- 随附 `profiles/asus-rog-pelta-2-4g.json`
- 原有 HJSDZHI S1-V2、LEOBOG HI98PRO 示例保留
- 用户配置目录：`%LOCALAPPDATA%\GearPower\profiles`

## 验证

- 自检：**78 项通过**
- 三台外设均已按补丁记录实机验证
- Apple 键盘、HUAWEI FreeBuds Pro 2 已实机验证
- 复杂边界和厂商软件共存仍需继续复核，详见 `DEVICE_ADAPTERS.md`

## 下载

- `GearPower.exe`
- `profiles/`
- `DEVICE_ADAPTERS.md`
- `DEVICE_PROFILES.md`
- `SHA256SUMS.txt`

SHA256：

```text
03855c8c340195db0aae32ffa07b734049b31f1b61c406aade845f1de9e67b99
```

文件大小：`356,352` 字节

## 注意

- Windows 10/11 64 位，需要 .NET Framework 4.8
- exe 未签名，SmartScreen 可能提示未知发布者
