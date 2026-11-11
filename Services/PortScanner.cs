using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;

namespace PortManagerWin.Services;

/// <summary>一条端口占用记录</summary>
public sealed class PortEntry
{
    public string Proto { get; init; } = "TCP";
    public string Local { get; init; } = "";
    public string Remote { get; init; } = "";
    public int Port { get; init; }
    public string State { get; init; } = "";
    public int Pid { get; init; }
    public string Name { get; init; } = "";

    public bool IsListening => State == "LISTENING";
    public string Key => $"{Proto}|{Local}|{Remote}|{Pid}";
}

/// <summary>通过 iphlpapi 直接读取系统端口表，无需解析 netstat 文本</summary>
public static class PortScanner
{
    private const int AF_INET = 2;
    private const int AF_INET6 = 23;
    private const int TCP_TABLE_OWNER_PID_ALL = 5;
    private const int UDP_TABLE_OWNER_PID = 1;
    private const uint ERROR_INSUFFICIENT_BUFFER = 122;

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedTcpTable(IntPtr pTcpTable, ref int pdwSize, bool bOrder,
        int ulAf, int tableClass, uint reserved);

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedUdpTable(IntPtr pUdpTable, ref int pdwSize, bool bOrder,
        int ulAf, int tableClass, uint reserved);

    [StructLayout(LayoutKind.Sequential)]
    private struct MIB_TCPROW_OWNER_PID
    {
        public uint State;
        public uint LocalAddr;
        public uint LocalPort;
        public uint RemoteAddr;
        public uint RemotePort;
        public uint OwningPid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MIB_TCP6ROW_OWNER_PID
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)] public byte[] LocalAddr;
        public uint LocalScopeId;
        public uint LocalPort;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)] public byte[] RemoteAddr;
        public uint RemoteScopeId;
        public uint RemotePort;
        public uint State;
        public uint OwningPid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MIB_UDPROW_OWNER_PID
    {
        public uint LocalAddr;
        public uint LocalPort;
        public uint OwningPid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MIB_UDP6ROW_OWNER_PID
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)] public byte[] LocalAddr;
        public uint LocalScopeId;
        public uint LocalPort;
        public uint OwningPid;
    }

    private static readonly Dictionary<int, string> StateNames = new()
    {
        [1] = "CLOSED", [2] = "LISTENING", [3] = "SYN_SENT", [4] = "SYN_RECEIVED",
        [5] = "ESTABLISHED", [6] = "FIN_WAIT_1", [7] = "FIN_WAIT_2", [8] = "CLOSE_WAIT",
        [9] = "CLOSING", [10] = "LAST_ACK", [11] = "TIME_WAIT", [12] = "DELETE_TCB",
    };

    private static ushort DecodePort(uint dword) =>
        (ushort)(((dword & 0xFF) << 8) | ((dword >> 8) & 0xFF));

    private static string FormatV4(uint addr, ushort port) =>
        $"{new IPAddress(BitConverter.GetBytes(addr))}:{port}";

    private static string FormatV6(byte[] addr, uint scope, ushort port)
    {
        var ip = new IPAddress(addr, scope);
        var text = ip.ToString();
        return text.Contains(':') ? $"[{text}]:{port}" : $"{text}:{port}";
    }

    private static byte[]? FetchTable(bool tcp, int af, int tableClass, out int count)
    {
        count = 0;
        int size = 0;
        IntPtr buffer = IntPtr.Zero;
        try
        {
            uint ret = tcp
                ? GetExtendedTcpTable(IntPtr.Zero, ref size, false, af, tableClass, 0)
                : GetExtendedUdpTable(IntPtr.Zero, ref size, false, af, tableClass, 0);

            if (ret != ERROR_INSUFFICIENT_BUFFER || size <= 0)
                return null;

            buffer = Marshal.AllocHGlobal(size);
            ret = tcp
                ? GetExtendedTcpTable(buffer, ref size, false, af, tableClass, 0)
                : GetExtendedUdpTable(buffer, ref size, false, af, tableClass, 0);

            if (ret != 0)
                return null;

            count = Marshal.ReadInt32(buffer);
            var bytes = new byte[size];
            Marshal.Copy(buffer, bytes, 0, size);
            return bytes;
        }
        finally
        {
            if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer);
        }
    }

    /// <summary>取 PID -> 进程名（含 .exe）映射</summary>
    private static Dictionary<int, string> ProcessNames()
    {
        var map = new Dictionary<int, string>(512);
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                var name = p.ProcessName;
                map[p.Id] = name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name : name + ".exe";
            }
            catch { /* 个别进程可能拒绝访问 */ }
            finally { p.Dispose(); }
        }
        map[0] = "System Idle Process";
        map[4] = "System";
        return map;
    }

    public static List<PortEntry> Scan()
    {
        var names = ProcessNames();
        var result = new List<PortEntry>(512);

        void Add(string proto, string local, string remote, string state, int pid)
        {
            result.Add(new PortEntry
            {
                Proto = proto,
                Local = local,
                Remote = remote,
                Port = int.Parse(local.Substring(local.LastIndexOf(':') + 1)),
                State = state,
                Pid = pid,
                // 留空，由界面层按当前语言填「未知进程」，避免把中文文案固化进扫描结果
                Name = names.TryGetValue(pid, out var n) ? n : "",
            });
        }

        // 逐行读取端口表；v6 结构含定长数组，故统一用 PtrToStructure
        void ReadTable<T>(bool tcp, int af, int tableClass, Action<T, int> onRow) where T : struct
        {
            var bytes = FetchTable(tcp, af, tableClass, out int count);
            if (bytes == null || count <= 0) return;

            var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                IntPtr basePtr = handle.AddrOfPinnedObject();
                int rowSize = Marshal.SizeOf<T>();
                for (int i = 0; i < count; i++)
                {
                    IntPtr rowPtr = basePtr + 4 + i * rowSize;
                    if (4 + (i + 1) * rowSize > bytes.Length) break;
                    var row = Marshal.PtrToStructure<T>(rowPtr);
                    onRow(row, i);
                }
            }
            finally
            {
                handle.Free();
            }
        }

        // ---- IPv4 TCP ----
        ReadTable<MIB_TCPROW_OWNER_PID>(true, AF_INET, TCP_TABLE_OWNER_PID_ALL, (row, _) =>
        {
            ushort lport = DecodePort(row.LocalPort);
            ushort rport = DecodePort(row.RemotePort);
            var state = StateNames.TryGetValue((int)row.State, out var s) ? s : row.State.ToString();
            var remote = state == "LISTENING" ? "*:*" : FormatV4(row.RemoteAddr, rport);
            Add("TCP", FormatV4(row.LocalAddr, lport), remote, state, (int)row.OwningPid);
        });

        // ---- IPv6 TCP ----
        ReadTable<MIB_TCP6ROW_OWNER_PID>(true, AF_INET6, TCP_TABLE_OWNER_PID_ALL, (row, _) =>
        {
            ushort lport = DecodePort(row.LocalPort);
            ushort rport = DecodePort(row.RemotePort);
            var state = StateNames.TryGetValue((int)row.State, out var s) ? s : row.State.ToString();
            var remote = state == "LISTENING" ? "*:*" : FormatV6(row.RemoteAddr, row.RemoteScopeId, rport);
            Add("TCP", FormatV6(row.LocalAddr, row.LocalScopeId, lport), remote, state, (int)row.OwningPid);
        });

        // ---- IPv4 UDP ----
        ReadTable<MIB_UDPROW_OWNER_PID>(false, AF_INET, UDP_TABLE_OWNER_PID, (row, _) =>
        {
            Add("UDP", FormatV4(row.LocalAddr, DecodePort(row.LocalPort)), "*:*", "-", (int)row.OwningPid);
        });

        // ---- IPv6 UDP ----
        ReadTable<MIB_UDP6ROW_OWNER_PID>(false, AF_INET6, UDP_TABLE_OWNER_PID, (row, _) =>
        {
            Add("UDP", FormatV6(row.LocalAddr, row.LocalScopeId, DecodePort(row.LocalPort)), "*:*", "-", (int)row.OwningPid);
        });

        // 同一 (协议, 本地, 远端, PID) 去重
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var unique = new List<PortEntry>(result.Count);
        foreach (var e in result)
        {
            if (seen.Add(e.Key)) unique.Add(e);
        }

        unique.Sort((a, b) =>
        {
            int c = a.Port.CompareTo(b.Port);
            if (c != 0) return c;
            c = (a.IsListening ? 0 : 1).CompareTo(b.IsListening ? 0 : 1);
            if (c != 0) return c;
            return a.Pid.CompareTo(b.Pid);
        });

        return unique;
    }
}
