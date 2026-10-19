# GearPower v1.1.2-beta

预发布版，新增 Razer BlackShark V2 Pro 2.4 耳机电量支持。

## 新增

- Razer BlackShark V2 Pro 2.4（`1532:0555`）
- 使用 64 字节中断 OUT/IN 报告，报告 ID `0x02`
- 响应第 15 字节为电量百分比
- 设备清单中显示为耳机类型，使用 2.4G 接收器

## 说明

- 这是 beta 版，协议等待对应型号实机验证
- Razer Synapse 可能占用设备；读不到时先退出 Synapse 再试
- 包含 v1.1.1 的全屏隐藏、任务栏置顶防闪烁和充电设备优先级修复

## 下载

- `GearPower.exe`
- `SHA256SUMS.txt`

SHA256：

```text
fc66501256b0c48a73c212916816a231209ee6b5c17a9310a5bf00ffb1a8af33
```

文件大小：`292,352` 字节

## 注意

- Windows 10/11 64 位，需要 .NET Framework 4.8
- exe 未签名，SmartScreen 可能提示未知发布者
- 任务栏电量条仍是实验性功能，主要面向 Windows 11 居中任务栏
