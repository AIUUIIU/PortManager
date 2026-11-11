using System;
using System.IO;

namespace PortManagerWin.Services;

/// <summary>轻量诊断日志：定位崩溃点用，正常运行时几乎无开销</summary>
public static class Diag
{
    private static readonly object Gate = new();
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PortManager", "diag.log");

    public static bool Enabled { get; set; }

    public static void Reset()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, "");
        }
        catch { }
    }

    public static void Log(string message)
    {
        if (!Enabled) return;
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                File.AppendAllText(FilePath, $"[{DateTime.Now:HH:mm:ss.fff}] {message}\r\n");
            }
        }
        catch { }
    }
}
