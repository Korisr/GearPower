# GearPower v1.2.4

随附设备协议内嵌与型号命名正式版。

## 新增

- 随附配置内嵌进 exe：
  - HJSDZHI S1-V2
  - LEOBOG Hi75c Pro
  - LEOBOG HI98PRO
  - ASUS ROG PELTA 2.4GHz
- 不再依赖 exe 旁的 `profiles/` 目录；目录存在时仍可覆盖同 id 配置
- 新增 `productContains` 匹配，按设备型号名区分共用 VID/PID 的键盘
- 已知型号补充 LEOBOG Hi75c Pro、Logitech PRO X Wireless、ASUS ROG PELTA
- 配置列表明确显示完整设备名，不再把 Hi75c Pro 显示成 HI98PRO

## 优先级

```text
用户配置
> exe 旁的 profiles/
> exe 内嵌随附配置
```

用户配置位置：

```text
%LOCALAPPDATA%\GearPower\profiles\<id>.json
```

## 说明

- 这是正式版
- LEOBOG Hi75c Pro 仍建议实机确认电量与充电字段
- ASUS ROG PELTA、Razer DeathAdder V4 Pro、Logitech PRO X 按三设备补丁记录验证
- Apple Magic Keyboard 与蓝牙 HFP 支持包含在本版本

## 下载

- `GearPower.exe`
- `SHA256SUMS.txt`

`profiles/` 现在是可选的覆盖目录，不再是运行必需。

SHA256：

```text
4887132f79c4076c3d802290a969d0c85155a5fc89e3dda7ab84c0704783d8ac
```

文件大小：`364,032` 字节

## 注意

- Windows 10/11 64 位，需要 .NET Framework 4.8
- exe 未签名，SmartScreen 可能提示未知发布者
