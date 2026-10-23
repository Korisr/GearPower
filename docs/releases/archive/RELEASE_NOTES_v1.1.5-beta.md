# GearPower v1.1.5-beta

管理员重启「零的 Hwnd 无效」修复版。

## 修复

- 窗口 `SourceInitialized` 事件在 HWND 已失效时不再调用 `HwndSource.FromHwnd(IntPtr.Zero)`
- 显式捕获 `HwndSource` 初始化异常，避免弹出「出现未处理的错误」
- 窗口关闭时移除 `WndProc` 钩子并清空 `HwndSource`

## 原因

WPF 在窗口句柄为零时调用：

```text
System.Windows.Interop.HwndSource.CriticalFromHwnd(IntPtr hwnd)
```

会抛出：

```text
ArgumentException: 零的 Hwnd 无效。
```

之前的保护只覆盖了窗口激活和 Taskbar Overlay，没有覆盖主窗口的 `SourceInitialized` 初始化路径。

## 说明

- 这是 beta 版
- 包含 v1.1.4-beta 的 ROG Falchion 支持
- 包含 v1.1.3-beta 的 Logitech 电量/设备名、任务栏修复
- 包含 Razer BlackShark V2 Pro 2.4 beta 支持

## 下载

- `GearPower.exe`
- `SHA256SUMS.txt`

SHA256：

```text
6e668e117b540a704636675877889561ba992a1eb83c69e0f705bfa16359f3e2
```

文件大小：`298,496` 字节

## 注意

- Windows 10/11 64 位，需要 .NET Framework 4.8
- exe 未签名，SmartScreen 可能提示未知发布者
