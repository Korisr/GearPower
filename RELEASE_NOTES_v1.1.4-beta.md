# GearPower v1.1.4-beta

新增 ASUS ROG Falchion 电量支持。

## 新增

- ASUS ROG Falchion（`0B05:193E`）
- 支持 65 字节 OUT/IN 报告回退，适配只有输出/输入报告、没有特性报告的 ROG 键盘
- Razer DeathAdder V3 Pro（`1532:00B7`）继续使用既有 Razer 90 字节协议

## 说明

- 这是 beta 版
- ROG Falchion 协议按 G-Helper 的 `0x12 0x07` 命令布局实现，等待实机验证
- 包含 v1.1.3-beta 的 Logitech 电量/设备名、管理员重启和任务栏修复

## 下载

- `GearPower.exe`
- `SHA256SUMS.txt`

SHA256：

```text
6aa5e6a544d0952d03645b3711f9c7b8adeb5687b9aafbae9184ddd8b915ddc0
```

文件大小：`297,984` 字节

## 注意

- Windows 10/11 64 位，需要 .NET Framework 4.8
- exe 未签名，SmartScreen 可能提示未知发布者
