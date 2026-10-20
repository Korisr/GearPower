# GearPower v1.1.3-beta

Logitech Lightspeed 电量读取修复版。

## 修复

- Logitech C539 Lightspeed 接收器上的 G502 / G Pro Wireless 现在可显示电量
- 读取 HID++ Device Name，显示 "G Pro Wireless Gaming Mouse" 等真实型号，不再只显示 "Logitech 罗技 无线设备"
- HID++ 2.0 特性查询改用 20 字节长报告
- 增加设备唤醒等待与重试
- 兼容旧款 BatteryStatus `0x1000`、BatteryVoltage `0x1001` 和 HID++ 1.0 BatteryMileage `0x0D`
- 鼠标休眠时保留历史卡片，唤醒后自动恢复当前电量
- 修复低权限下点击「以管理员身份重启」时可能出现的「零的 Hwnd 无效」错误

## 说明

- 这是 beta 版
- 如果鼠标刚唤醒仍然读不到，等下一次自动刷新即可
- 包含 v1.1.2-beta 的 Razer BlackShark V2 Pro 2.4 支持
- 包含 v1.1.1 的全屏隐藏、任务栏置顶防闪烁和充电设备优先级修复

## 下载

- `GearPower.exe`
- `SHA256SUMS.txt`

SHA256：

```text
492636910ae49f25340bb864480436716808a92a895119c1416ddd2a417527bc
```

文件大小：`297,472` 字节

## 注意

- Windows 10/11 64 位，需要 .NET Framework 4.8
- exe 未签名，SmartScreen 可能提示未知发布者
- 任务栏电量条仍是实验性功能，主要面向 Windows 11 居中任务栏
