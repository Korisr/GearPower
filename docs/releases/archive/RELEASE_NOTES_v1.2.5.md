# GearPower v1.2.5

可选蓝牙设备电量显示。

## 新增

- 首页右上角新增「蓝牙」电量显示开关，默认开启
- 关闭后：
  - 不枚举 Windows 蓝牙电池属性
  - 不探测蓝牙 HID 设备电量（包括 Apple 键盘）
  - 历史蓝牙卡片暂时隐藏
- 重新开启后自动重新扫描，历史卡片恢复
- 开关状态保存在注册表：

```text
HKCU\Software\GearPower\BluetoothBattery
```

## 保持不变

- USB / 2.4G 接收器设备不受影响
- 蓝牙开关只控制电量读取和卡片显示，不修改蓝牙配对或驱动
- 设备清单仍可查看蓝牙接口原始信息

## 验证

- 自检：78 项通过
- 首页开关已在浅色主题下加载验证

## 下载

- `GearPower.exe`
- `SHA256SUMS.txt`

随附设备配置已内嵌，`profiles/` 为可选覆盖目录。

SHA256：

```text
2991ac8ef82acca3953872f197aba4615babdd2876c26780ee962146fae2ebaa
```

文件大小：`366,080` 字节

## 注意

- Windows 10/11 64 位，需要 .NET Framework 4.8
- exe 未签名，SmartScreen 可能提示未知发布者
