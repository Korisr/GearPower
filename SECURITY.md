# Security policy

## Supported versions

只维护最新 Release 和 `main` 分支。

## Reporting a vulnerability

请通过 GitHub 的 **Security → Report a vulnerability** 私密报告，不要先公开提交 Issue。

报告时请尽量包含：

- 受影响的版本或提交
- 复现步骤
- 影响范围（例如任意文件写入、权限提升、HID 数据泄露）
- 是否需要在管理员权限下才能触发

## Scope

GearPower 会：

- 读取 HID 设备报告和 Windows 设备属性
- 在本机写入设置、缓存和日志
- 在用户主动确认后通过 UAC 以管理员权限重启

GearPower 不会：

- 发起网络请求
- 上传设备数据
- 修改 DPI、灯效、EQ 等设备设置

## Privacy of diagnostics

`hid-dump.txt`、`logs/app.log` 和「复制诊断信息」可能包含设备名称、VID/PID、接口路径，部分设备路径可能带有唯一 ID。公开粘贴前请先检查并删除敏感字段。
