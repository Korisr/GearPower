GearPower 开机自启分支说明
本仓库是 GearPower 的非官方分支，在 v1.2.6 基础上新增「开机启动」功能，并修复了自启时的窗口闪烁问题。当前版本 v1.2.8。

基线版本：GearPower 1.2.6
当前版本：GearPower 1.2.8
改动范围：3 个源文件新增/修改 + 1 个 XAML + 1 个自测文件 + 版本号与文档
无新增第三方依赖，仍是单文件程序
为什么有这个分支
原版 GearPower 只能手动启动。对常驻托盘的电量监控工具来说，每次开机都要自己点一次图标才能开始刷新电量，等于功能少了一半——尤其是任务栏电量条，不开机自启就永远是空的。

本分支补上这个缺口，并把自启做成真正静默的：登录后只有托盘图标，不弹窗口、不抢焦点、不闪黑屏。

功能差异
新增：设置页「开机启动」开关
位置在设置页「系统托盘」与「任务栏电量条」之间。

项目	实现
写入位置	HKCU\Software\Microsoft\Windows\CurrentVersion\Run
值名	GearPower
值数据	"<exe 绝对路径>" --autostart
权限	仅当前用户，不需要管理员权限，不触发 UAC
选择 HKCU 而不是计划任务或服务的原因：无需提权、无需额外文件、随用户登录触发，卸载时删一个注册表值就干净。

新增：启动项状态自检
设置页开关下方会显示当前状态，共三种：

启动项已就绪 · <exe 路径> — 正常
启动项已被 Windows「设置 → 应用 → 启动」关闭，需在那里重新打开。 — 被系统禁用
启动项路径已失效，下次启动会自动修复。 — exe 被移动或改名
第三种的检测方式是读 HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run 里同名值的二进制 blob，(blob[0] & 0x01) == 0 即判定为被 Windows 关闭。开启开关时会顺带清掉这条禁用记录，避免出现「开关显示开着、实际不生效」。

新增：路径自愈
每次正常启动都会比对注册表里的命令行与当前 exe 路径，不一致就自动改写。所以移动目录、改名、覆盖升级之后都不需要重新打开开关。

新增：--autostart 启动参数
内部参数，由启动项调用，不是给用户手动输入的。

自启时的行为差异：

完全不创建主窗口，进程里连窗口句柄都不存在
直接初始化托盘图标、任务栏电量条、首次扫描与刷新定时器
跳过提权提示窗
跳过「托盘后台已启用」气泡
已有实例在运行时静默退出，不把旧窗口拉到前台
行为变更：单实例
场景	原版	本分支
普通启动遇到已有实例	把旧窗口拉到前台	同原版
自启遇到已有实例	把旧窗口拉到前台	静默退出
行为变更：关闭行为
自启进程在无窗口时使用 ShutdownMode.OnExplicitShutdown，否则 WPF 消息循环会因为「没有主窗口」立刻结束。从托盘打开窗口后自动切回 OnMainWindowClose，之后关闭窗口的行为与普通启动完全一致——这一点做了专门处理，否则用户从托盘打开窗口后点关闭，程序不会退出。

与原版的代码差异
新增文件
文件	行数	职责
src/AutoStart.cs	191	启动项读写、状态检测、路径自愈、纯函数命令行构造
AutoStart 的公开接口：

csharp


public static class AutoStart
{
    public const string StartupArgument = "--autostart";

    public static string ExecutablePath { get; }
    public static string BuildCommandLine(string exePath);  // 纯函数，可自测
    public static bool   IsStartupArgument(string arg);     // 纯函数，可自测
    public static string ReadCommand();
    public static bool   IsEnabled();
    public static bool   IsCurrent();
    public static bool   IsBlockedByWindows();
    public static bool   Apply(bool enabled, out string error);
    public static void   SyncIfEnabled();
}
BuildCommandLine 与 IsStartupArgument 刻意做成不碰注册表的纯函数，这样自测能在无副作用的前提下覆盖引号、大小写、null 等边界。

修改文件
文件	原行数	现行数	改动点
src/Program.cs	109	130	解析 --autostart；自启时不抢焦点；启动时调 SyncIfEnabled()；构造 UiController 传第三参；自启分支改走无参 app.Run()
src/MainWindow.cs	2168	2284	新增自启状态字段与控件引用；Window_Loaded 瘦身为一行；抽出 InitializeRuntime()；新增 StartInBackground()；新增 RestoreMainWindowShutdown()；设置读写加 AutoStart 键；新增 SetAutoStart() 与 RefreshAutoStartState()
ui.xaml	683	708	「系统托盘」与「任务栏电量条」之间插入「开机启动」卡片；设置页副标题调整
src/SelfTest.cs	270	281	新增 7 条 autostart 断言
src/AssemblyInfo.cs	10	10	版本 1.2.6.0 → 1.2.8.0
关键改动说明
src/MainWindow.cs:362 — 初始化时机从「窗口显示」改为「首次需要」

原版把托盘、任务栏电量条、首次扫描、刷新定时器全放在 Window_Loaded 里，意味着必须先显示窗口才能跑起来。本分支把这些整体搬进 InitializeRuntime()，并加 runtimeInitialized 守卫：

csharp


private void InitializeRuntime()
{
    if (runtimeInitialized) return;
    runtimeInitialized = true;
    // 原 Window_Loaded 的全部逻辑
}
Window_Loaded 只剩一行 InitializeRuntime();。自启路径由 dispatcher 直接调用 StartInBackground() → InitializeRuntime()，窗口始终没有 HWND。

src/Program.cs:111 — 自启走无参消息循环

csharp


app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
window.Dispatcher.BeginInvoke(new Action(controller.StartInBackground),
    DispatcherPriority.ApplicationIdle);
app.Run();   // 无参，不碰 window
这一点是整个修复的核心。WPF 的 Run(Window) 会强制调用 Show()，即使把 Opacity 设为 0，窗口仍会被创建并参与 DWM 合成，登录瞬间就会闪出一块黑屏。只有完全不调用 Show()、改跑裸消息循环，才能做到进程里没有任何可渲染对象。

src/MainWindow.cs:570 — 从托盘打开窗口时恢复关闭模式

csharp


private void RestoreMainWindowShutdown()
{
    Application app = Application.Current;
    if (app == null) return;
    if (app.ShutdownMode == ShutdownMode.OnMainWindowClose && app.MainWindow == window) return;
    app.MainWindow = window;
    app.ShutdownMode = ShutdownMode.OnMainWindowClose;
}
在 ShowMainWindow() 首行调用。缺了这段，自启进程被用户从托盘唤起后，点关闭窗口程序不会退出，只能去任务管理器结束。

兜底：自启时若托盘图标创建失败（例如低权限运行），回退为显示窗口，避免进程既无窗口也无托盘、用户没有任何入口可操作。

版本号说明
原版 1.2.6 → 本分支 1.2.8，跳过了 1.2.7。原因是本分支内部迭代过两版：

1.2.7 — 首次实现开机自启，自启时用 Opacity = 0 隐藏窗口
1.2.8 — 修复 1.2.7 的自启黑屏问题，改为完全不创建窗口
对外只发布 1.2.8。1.2.7 的产物未分发，但 CHANGELOG.md 里保留了完整记录，方便追溯。

升级与兼容
从原版 1.2.6 覆盖安装：设置项自动继承，注册表里没有 AutoStart 键时按「关闭」处理，不会自己打开自启
从本分支 1.2.7 升级到 1.2.8：已开启自启的用户无需任何操作，下次登录自动生效
卸载：删除 exe 即可；如需清理，删 HKCU\Software\GearPower 与 HKCU\Software\Microsoft\Windows\CurrentVersion\Run 下的 GearPower 值
构建
与原版一致，不引入新依赖，仍用系统自带 csc.exe：

powershell


powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
需要 Windows 10/11 自带的 .NET Framework 4.8 与 csc.exe（无需 Visual Studio / .NET SDK）。

产物为单文件 dist\GearPower.exe，ui.xaml、logo.png、app.ico、app.manifest 与 profiles/*.json 全部以资源形式内嵌，外部无任何 DLL 依赖，可单独拷走运行。

验证
自检 87 项全部通过，其中 7 项为新增的 autostart 断言
自启路径实测：后台启动后 IsVisible=False、HWND=0，首次扫描正常完成
普通启动回归正常，窗口照常创建
powershell


.\dist\GearPower.exe --selftest
已知限制
exe 未签名，SmartScreen 与部分杀毒软件会提示未知发布者。这是所有无代码签名证书的 .NET 小工具的通病，非本分支引入
开机自启依赖 HKCU 写入权限；受组策略或安全软件限制时，设置页会提示失败原因
低权限运行时托盘不可用，自启会回退为显示窗口
仅支持 Windows 10/11 64 位
许可
沿用原版许可，见 LICENSE。本分支的所有改动同样适用该许可。
