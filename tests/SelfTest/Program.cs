using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using PortManagerWin.Services;

// 子进程模式：监听指定端口，供结束进程测试使用
if (args.Length >= 2 && args[0] == "--listen")
{
    var listener = new TcpListener(IPAddress.Any, int.Parse(args[1]));
    listener.Start();
    Thread.Sleep(60000);
    return 0;
}

Console.OutputEncoding = System.Text.Encoding.UTF8;
int pass = 0, fail = 0;

void Check(string name, bool ok, string detail = "")
{
    Console.WriteLine((ok ? "  [PASS] " : "  [FAIL] ") + name + (detail.Length > 0 ? "  -> " + detail : ""));
    if (ok) pass++; else fail++;
}

Console.WriteLine("=== 1. 端口表扫描 ===");
var entries = PortScanner.Scan();
Check("能读到端口记录", entries.Count > 100, $"{entries.Count} 条");
Check("包含监听状态记录", entries.Any(e => e.State == "LISTENING"),
    $"{entries.Count(e => e.State == "LISTENING")} 个监听");
Check("进程名已解析", entries.Count(e => e.Name.EndsWith(".exe")) > 50,
    $"{entries.Count(e => e.Name.EndsWith(".exe"))} 条有进程名");
Check("地址格式正确", entries.All(e => e.Local.Contains(':')),
    entries.FirstOrDefault(e => e.Port == 8080)?.Local ?? "无 8080");

var sample = entries.FirstOrDefault(e => e.Port == 8080);
Console.WriteLine($"        8080 -> {sample?.Name} PID {sample?.Pid} {sample?.State}");
Check("IPv4/IPv6 均能识别", entries.Any(e => e.Local.StartsWith("[")),
    $"{entries.Count(e => e.Local.StartsWith("["))} 条 IPv6");

Console.WriteLine();
Console.WriteLine("=== 2. 结束进程（真实端口释放）===");
int testPort = 8899;
var child = Process.Start(new ProcessStartInfo(Environment.ProcessPath!, $"--listen {testPort}")
{
    UseShellExecute = false,
})!;
Thread.Sleep(1800);

var before = PortScanner.Scan().Where(e => e.Port == testPort && e.State == "LISTENING").ToList();
Check($"子进程占用 {testPort}", before.Count > 0, string.Join(",", before.Select(b => b.Pid)));

if (before.Count > 0)
{
    var (ok, msg) = ProcessActions.Kill(before[0].Pid, before[0].Name);
    Check("Kill 返回成功", ok, msg);
    Thread.Sleep(900);
    var after = PortScanner.Scan().Where(e => e.Port == testPort && e.State == "LISTENING").ToList();
    Check("端口已释放", after.Count == 0, $"剩余 {after.Count} 条");
    Check("子进程已退出", child.HasExited);
}

Console.WriteLine();
Console.WriteLine("=== 3. 安全防护 ===");
var (okSys, msgSys) = ProcessActions.Kill(4, "System");
Check("拒绝结束 System(PID 4)", !okSys, msgSys);
var (okSelf, msgSelf) = ProcessActions.Kill(Environment.ProcessId, "SelfTest.exe");
Check("拒绝结束自身进程", !okSelf, msgSelf);
Check("IsProtected 识别 svchost", ProcessActions.IsProtected(1234, "svchost.exe"));
Check("IsProtected 放行普通进程", !ProcessActions.IsProtected(1234, "node.exe"));

Console.WriteLine();
Console.WriteLine("=== 4. 进程详情 ===");
// 用自身进程判定：权限一定足够，结果不受运行环境影响（CI 上也稳定）
var self = ProcessActions.GetDetail(Environment.ProcessId);
Console.WriteLine($"        {self.Name} | 路径: {(self.Path.Length > 0 ? self.Path : "空")} | 父PID: {self.ParentPid}");
Console.WriteLine($"        命令行: {(self.CommandLine.Length > 60 ? self.CommandLine[..60] + "..." : self.CommandLine)}");
Check("能读到可执行路径", self.Path.Length > 0);
Check("能读到父进程 PID", self.ParentPid.Length > 0);

// 顺带打印一个外部进程的详情，仅作观察，不参与判定（可能因权限读不到）
var target = entries.FirstOrDefault(e => e.Name.Equals("explorer.exe", StringComparison.OrdinalIgnoreCase))
             ?? entries.FirstOrDefault(e => e.Pid > 100);
if (target != null && target.Pid != Environment.ProcessId)
{
    var d = ProcessActions.GetDetail(target.Pid);
    Console.WriteLine($"        [参考] {d.Name} | 路径: {(d.Path.Length > 0 ? d.Path : "空")} | 父PID: {d.ParentPid}");
}

Console.WriteLine();
Console.WriteLine($"结果：{pass} 通过 / {fail} 失败");
return fail == 0 ? 0 : 1;
