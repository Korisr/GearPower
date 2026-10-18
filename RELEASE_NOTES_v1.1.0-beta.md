# GearPower v1.1.0-beta

预发布测试版，重点更新主题强调色、设备卡片自适应和迈从 MCHOSE 支持。

## 亮点

- 新增绿色 / 粉色 / 黄色 / 蓝色强调色
- 设备卡片按窗口宽度自适应 2–3 列
- 支持 MCHOSE K7 Ultra（Realtek 方案，已实机验证）
- 设备清单底部新增「提交给开发者」入口

## 新增

- 四套强调色主题，浅色 / 深色分别适配
- 所有单选选项统一 1px 强调色描边
- 设备支持请求 Issue 模板
- MCHOSE L7 Pro 协议预适配（待实机验证）
- 应用内恢复大号 GearPower 标题，窗口标题栏保持无品牌
- 设备清单底部增加设备支持入口，可直接打开 GitHub Issue

## 修复

- 单实例：重复启动时唤回已运行窗口，而不是创建多个实例
- 最小化或隐藏到托盘时，再次启动也能唤回原窗口
- 管理员重启时会正确释放单实例锁
- 日志固定到 `%LOCALAPPDATA%\GearPower\logs\app.log`
- `--selftest` / `--dump` 输出统一到日志目录
- 启动日志记录版本、系统和 CLR 信息

## 下载

- `GearPower.exe`
- `SHA256SUMS.txt`

SHA256：

```text
a022c7eb551d34371cbfbd652cb669307cbbe12c34ff51b83f92b4c98769deb7
```

## 注意

- Windows 10/11 64 位，需要 .NET Framework 4.8
- exe 未签名，SmartScreen 可能提示未知发布者
- 任务栏电量条仍是实验性功能，主要面向 Windows 11 居中任务栏
- MCHOSE K7 Ultra 已实机验证；L7 Pro 和其他型号可能需要继续适配
