using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace PortManagerWin.Services;

public sealed class ProcessDetail
{
    /// <summary>进程是否仍然存在（扫描与点击之间可能已退出）</summary>
    public bool Exists { get; set; }
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public string CommandLine { get; set; } = "";
    public string StartTime { get; set; } = "";
    public string ParentPid { get; set; } = "";
    public string WorkingSet { get; set; } = "";
}

/// <summary>结束进程 / 读取进程详情</summary>
public static class ProcessActions
{
    /// <summary>系统关键进程，禁止结束</summary>
    private static readonly HashSet<string> ProtectedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "system", "system idle process", "idle", "registry", "memory compression",
        "smss.exe", "csrss.exe", "wininit.exe", "winlogon.exe", "services.exe",
        "lsass.exe", "svchost.exe", "fontdrvhost.exe", "dwm.exe", "spoolsv.exe",
        "msmpeng.exe", "securityhealthservice.exe", "audiodg.exe", "sihost.exe",
        "ctfmon.exe", "explorer.exe", "taskhostw.exe", "shellexperiencehost.exe",
    };

    /// <summary>内核伪进程：没有对应的可执行文件</summary>
    private static readonly HashSet<string> PseudoNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "System", "Idle", "Registry", "Memory Compression", "Secure System",
    };

    private static bool IsPseudoProcess(string processName) => PseudoNames.Contains(processName);

    /* ---------------- 父进程查询：Toolhelp 快照 ---------------- */

    private const uint TH32CS_SNAPPROCESS = 0x00000002;
    private static readonly IntPtr InvalidHandle = new(-1);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PROCESSENTRY32W
    {
        public uint dwSize;
        public uint cntUsage;
        public uint th32ProcessID;
        public IntPtr th32DefaultHeapID;
        public uint th32ModuleID;
        public uint cntThreads;
        public uint th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExeFile;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool Process32FirstW(IntPtr hSnapshot, ref PROCESSENTRY32W lppe);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool Process32NextW(IntPtr hSnapshot, ref PROCESSENTRY32W lppe);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    /// <summary>
    /// 取父进程 PID。走 Toolhelp 快照，毫秒级且不需要 WMI——
    /// 冷启动的 WMI 查询在干净机器上可能耗时数秒，不能拿它决定父 PID 是否有值。
    /// </summary>
    private static int GetParentPid(int pid)
    {
        IntPtr snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        if (snapshot == InvalidHandle) return 0;

        try
        {
            var entry = new PROCESSENTRY32W { dwSize = (uint)Marshal.SizeOf<PROCESSENTRY32W>() };
            if (!Process32FirstW(snapshot, ref entry)) return 0;

            do
            {
                if (entry.th32ProcessID == (uint)pid) return (int)entry.th32ParentProcessID;
                entry.dwSize = (uint)Marshal.SizeOf<PROCESSENTRY32W>();
            }
            while (Process32NextW(snapshot, ref entry));

            return 0;
        }
        finally
        {
            CloseHandle(snapshot);
        }
    }

    public static bool IsProtected(int pid, string name)
    {
        if (pid <= 0 || pid == 4) return true;
        if (string.IsNullOrWhiteSpace(name)) return false;
        var bare = name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? name[..^4]
            : name;
        return ProtectedNames.Contains(bare) || ProtectedNames.Contains(name);
    }

    public static (bool Ok, string Message) Kill(int pid, string name)
    {
        if (pid <= 0) return (false, Localizer.Current.InvalidPid);
        if (IsProtected(pid, name))
            return (false, Localizer.Current.ProtectedProcess(name));
        if (pid == Environment.ProcessId)
            return (false, Localizer.Current.CannotKillSelf);

        try
        {
            using var p = Process.GetProcessById(pid);
            p.Kill(entireProcessTree: true);
            p.WaitForExit(3000);
            return (true, Localizer.Current.KillDone(name, pid));
        }
        catch (ArgumentException)
        {
            return (true, Localizer.Current.AlreadyGone(pid));
        }
        catch (InvalidOperationException)
        {
            return (true, Localizer.Current.AlreadyGone(pid));
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 5)
        {
            return (false, Localizer.Current.AccessDenied);
        }
        catch (Win32Exception ex)
        {
            return (false, Localizer.Current.KillFailed(ex.Message));
        }
        catch (Exception ex)
        {
            return (false, Localizer.Current.KillFailed(ex.Message));
        }
    }

    /// <summary>读取进程详情；命令行通过 CIM 查询（按需调用，失败不影响其他字段）</summary>
    public static ProcessDetail GetDetail(int pid)
    {
        var detail = new ProcessDetail();
        try
        {
            using var p = Process.GetProcessById(pid);
            detail.Exists = true;
            // System / Idle / Registry 这类内核进程并没有可执行文件，不能硬加 .exe
            detail.Name = IsPseudoProcess(p.ProcessName) ? p.ProcessName : p.ProcessName + ".exe";
            try { detail.Path = p.MainModule?.FileName ?? ""; } catch { /* 权限不足 */ }
            try { detail.StartTime = p.StartTime.ToString("yyyy-MM-dd HH:mm:ss"); } catch { }
            try
            {
                var mb = p.WorkingSet64 / 1024.0 / 1024.0;
                detail.WorkingSet = mb >= 1024 ? $"{mb / 1024:F2} GB" : $"{mb:F1} MB";
            }
            catch { }
        }
        catch (ArgumentException)
        {
            return detail;
        }

        // 父进程走 Toolhelp 快照，不受 WMI 冷启动影响
        int ppid = GetParentPid(pid);
        if (ppid > 0) detail.ParentPid = ppid.ToString();

        // 命令行只能靠 CIM 查询（脚本内只用单引号，避免参数转义问题）
        var script = "$ErrorActionPreference='SilentlyContinue';" +
                     "[Console]::OutputEncoding=[Text.Encoding]::UTF8;" +
                     $"$p=Get-CimInstance Win32_Process -Filter 'ProcessId={pid}';" +
                     "if($p){[PSCustomObject]@{cmd=$p.CommandLine;ppid=$p.ParentProcessId}|ConvertTo-Json -Compress}";

        try
        {
            var psi = new ProcessStartInfo("powershell.exe")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
            };
            psi.ArgumentList.Add("-NoProfile");
            psi.ArgumentList.Add("-NonInteractive");
            psi.ArgumentList.Add("-ExecutionPolicy");
            psi.ArgumentList.Add("Bypass");
            psi.ArgumentList.Add("-Command");
            psi.ArgumentList.Add(script);

            using var ps = Process.Start(psi);
            if (ps != null)
            {
                // 必须同时消费 stdout / stderr，否则子进程写满管道缓冲区会永久阻塞
                var stdoutTask = ps.StandardOutput.ReadToEndAsync();
                var stderrTask = ps.StandardError.ReadToEndAsync();

                string output;
                // 冷启动的 WMI 查询在干净机器上可能要几秒，留足余量；超时只影响命令行
                if (ps.WaitForExit(6000))
                {
                    output = stdoutTask.GetAwaiter().GetResult();
                }
                else
                {
                    try { ps.Kill(entireProcessTree: true); } catch { }
                    output = "";
                }
                _ = stderrTask; // 丢弃，仅用于排空管道

                if (!string.IsNullOrWhiteSpace(output))
                {
                    using var doc = JsonDocument.Parse(output.Trim());
                    var root = doc.RootElement;
                    if (root.TryGetProperty("cmd", out var cmd) && cmd.ValueKind == JsonValueKind.String)
                        detail.CommandLine = cmd.GetString() ?? "";
                    if (detail.ParentPid.Length == 0 &&
                        root.TryGetProperty("ppid", out var parentFromCim) &&
                        parentFromCim.ValueKind == JsonValueKind.Number)
                        detail.ParentPid = parentFromCim.GetInt32().ToString();
                }
            }
        }
        catch { /* 查询失败不影响主流程 */ }

        return detail;
    }
}
