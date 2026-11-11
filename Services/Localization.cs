using System;
using System.ComponentModel;
using System.Reflection;

namespace PortManagerWin.Services;

/// <summary>中英文文案，切换语言时通知所有绑定刷新</summary>
public sealed class Localizer : INotifyPropertyChanged
{
    public static Localizer Current { get; } = new();

    private string _language = "zh-CN";

    public string Language => _language;
    public bool IsChinese => _language.StartsWith("zh", StringComparison.OrdinalIgnoreCase);

    public event Action? LanguageChanged;

    public void SetLanguage(string language)
    {
        if (string.IsNullOrWhiteSpace(language) || language == _language) return;
        _language = language;
        NotifyAll();
        LanguageChanged?.Invoke();
    }

    private string T(string zh, string en) => IsChinese ? zh : en;

    /* ---------------- 静态文案 ---------------- */

    /// <summary>应用名固定使用英文，不随界面语言变化</summary>
    public const string AppName = "Port Manager";

    public string PageTitle => T("端口概览", "Ports overview");

    public string SearchPlaceholder => T("搜索端口 / PID / 进程名", "Search port / PID / process");
    public string FilterListening => T("监听中", "Listening");
    public string FilterAll => T("全部", "All");
    public string FilterEstablished => T("已连接", "Established");
    public string FilterWatched => T("关注的", "Watched");
    public string AutoRefresh => T("自动刷新", "Auto refresh");
    public string RefreshTooltip => T("立即刷新 (F5)", "Refresh now (F5)");
    public string Settings => T("设置", "Settings");
    public string Appearance => T("外观", "Appearance");
    public string ThemeSystem => T("跟随系统", "Use system setting");
    public string ThemeLight => T("浅色", "Light");
    public string ThemeDark => T("深色", "Dark");
    public string LanguageLabel => T("语言", "Language");

    public string AllPortsSection => T("全部端口", "All ports");
    public string WatchedSection => T("我关注的端口", "Watched ports");
    public string AddPortPlaceholder => T("端口号", "Port");
    public string Add => T("添加", "Add");
    public string UnwatchTooltip => T("取消关注", "Stop watching");

    public string Busy => T("占用中", "In use");
    public string Free => T("空闲", "Free");
    public string Available => T("可以使用", "Available");
    public string Release => T("释放", "Release");
    public string Details => T("详情", "Details");

    public string ColPort => T("端口", "Port");
    public string ColProto => T("协议", "Proto");
    public string ColLocal => T("本地地址", "Local address");
    public string ColRemote => T("远程地址", "Remote address");
    public string ColState => T("状态", "State");
    public string ColPid => T("PID", "PID");
    public string ColProcess => T("进程名", "Process");
    public string ColActions => T("操作", "Actions");
    public string EmptyState => T("没有匹配的端口记录", "No matching ports");

    public string ConfirmKillTitle => T("结束进程", "End process");
    public string ConfirmEndButton => T("确认结束", "End process");
    public string Cancel => T("取消", "Cancel");
    public string Close => T("关闭", "Close");
    public string ProcessDetailsTitle => T("进程详情", "Process details");
    public string FieldName => T("进程名", "Process name");
    /// <summary>拿不到进程名时的兜底文案（部分系统进程或已退出的进程）</summary>
    public string UnknownProcess => T("（未知进程）", "(unknown process)");
    public string FieldPath => T("可执行路径", "Executable path");
    public string FieldCommandLine => T("命令行", "Command line");
    public string FieldStartTime => T("启动时间", "Start time");
    public string FieldMemory => T("内存占用", "Memory usage");
    public string FieldParentPid => T("父进程 PID", "Parent PID");
    public string NoPermissionPath => T("—（无权限读取，可尝试以管理员身份运行）",
        "— (access denied, try running as administrator)");
    public string NoPermissionCommandLine => T("—（无权限读取）", "— (access denied)");
    public string LoadingDetails => T("正在读取进程信息…", "Reading process info…");
    public string ProcessGone => T("该进程已经退出，可能刚被结束或自行终止。",
        "This process has already exited.");
    public string DetailFailed(string message) => T($"无法读取进程详情：{message}",
        $"Could not read process details: {message}");

    /* ---------------- 带参文案 ---------------- */

    public string Summary(int total, int shown) =>
        T($"共 {total} 条端口记录 · 当前显示 {shown} 条",
          $"{total} ports · showing {shown}");

    public string UpdatedAt(string time) => T($"更新于 {time}", $"Updated {time}");
    public string RefreshFailed(string message) => T($"刷新失败：{message}", $"Refresh failed: {message}");
    public string InvalidPort => T("请输入 1 - 65535 之间的端口号", "Enter a port number between 1 and 65535");
    public string AlreadyWatched(int port) => T($"端口 {port} 已在关注列表中", $"Port {port} is already watched");
    public string PortIsFree(int port) => T($"端口 {port} 当前空闲", $"Port {port} is currently free");

    public string ConfirmKillBody(string name, int pid) =>
        T($"确定要结束 {name}（PID {pid}）吗？\n\n强制结束会立即中断该程序，未保存的数据可能丢失。",
          $"End {name} (PID {pid})?\n\nThe process will be terminated immediately. Unsaved data may be lost.");

    public string ConfirmReleaseTitle(int port) => T($"释放端口 {port}", $"Release port {port}");

    public string ConfirmReleaseBody(int port, string processes) =>
        T($"端口 {port} 正被 {processes} 占用，确定强制结束吗？\n\n强制结束会立即中断该程序，未保存的数据可能丢失。",
          $"Port {port} is held by {processes}. Terminate it?\n\nThe process will be terminated immediately. Unsaved data may be lost.");

    public string ProcessCount(int count) => T($"等 {count} 个进程", $" +{count - 1} more");

    /* ---------------- 进程操作返回文案 ---------------- */

    public string KillDone(string name, int pid) => T($"已结束 {name} (PID {pid})", $"Ended {name} (PID {pid})");
    public string AlreadyGone(int pid) => T($"PID {pid} 的进程已不存在（可能已自行退出）",
        $"Process {pid} no longer exists (it may have already exited)");
    public string AccessDenied => T("权限不足：该进程需要以管理员身份运行端口管家才能结束",
        "Access denied: run Port Manager as administrator to end this process");
    public string KillFailed(string message) => T($"结束失败：{message}", $"Failed to end process: {message}");
    public string InvalidPid => T("无效的 PID", "Invalid PID");
    public string ProtectedProcess(string name) => T($"{name} 属于系统关键进程，已阻止结束操作",
        $"{name} is a critical system process and cannot be ended");
    public string CannotKillSelf => T("不能结束端口管家自身进程", "Cannot end Port Manager itself");

    /* ---------------- 通知 ---------------- */

    public event PropertyChangedEventHandler? PropertyChanged;

    private void NotifyAll()
    {
        foreach (var prop in typeof(Localizer).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.Name == nameof(Language) || prop.Name == nameof(IsChinese)) continue;
            if (prop.GetIndexParameters().Length > 0) continue;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop.Name));
        }
    }
}
