using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PortManagerWin.Services;

namespace PortManagerWin.ViewModels;

/// <summary>应用设置，存于 %LOCALAPPDATA%\PortManagerWin\settings.json</summary>
public sealed class AppSettings
{
    public string Theme { get; set; } = "System";
    public string Language { get; set; } = "zh-CN";
    public List<int> Watch { get; set; } = new() { 8080, 3000, 5173, 8000 };
    public bool AutoRefresh { get; set; } = true;
}

public static class SettingsStore
{
    private static readonly string Root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    private static readonly string Dir = Path.Combine(Root, "PortManager");

    private static readonly string FilePath = Path.Combine(Dir, "settings.json");

    /// <summary>旧版本（中文名时期）的配置目录，首次启动时迁移</summary>
    private static readonly string LegacyPath = Path.Combine(Root, "PortManagerWin", "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath) && File.Exists(LegacyPath))
            {
                Directory.CreateDirectory(Dir);
                File.Copy(LegacyPath, FilePath);
            }

            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                var s = JsonSerializer.Deserialize<AppSettings>(json);
                if (s != null)
                {
                    if (s.Watch == null || s.Watch.Count == 0)
                        s.Watch = new List<int> { 8080, 3000, 5173, 8000 };
                    s.Watch = s.Watch.Where(p => p > 0 && p < 65536).Distinct().ToList();
                    return s;
                }
            }
        }
        catch { /* 配置损坏则用默认值 */ }
        return new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(settings,
                new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* 写入失败不影响使用 */ }
    }
}

/// <summary>表格中的一行</summary>
public sealed class PortRow : INotifyPropertyChanged
{
    public string Key { get; init; } = "";
    public int Port { get; init; }
    public string Proto { get; init; } = "";
    public string Local { get; init; } = "";
    public int Pid { get; init; }

    private string _remote = "";
    private string _state = "";
    private string _name = "";
    private bool _isWatched;
    private bool _canKill;

    public string Remote { get => _remote; private set => Set(ref _remote, value); }
    public string State { get => _state; private set => Set(ref _state, value); }
    public string Name { get => _name; private set => Set(ref _name, value); }
    public bool IsWatched { get => _isWatched; private set => Set(ref _isWatched, value); }
    public bool CanKill { get => _canKill; private set => Set(ref _canKill, value); }

    // 监听中的 TCP 和全部 UDP 都没有对端（扫描器里记为 *:*），统一显示成破折号
    public string RemoteDisplay =>
        string.IsNullOrEmpty(Remote) || Remote == "*:*" ? "—" : Remote;
    public bool IsListening => State == "LISTENING";
    public string PidText => Pid.ToString();
    public string DetailsText => Localizer.Current.Details;
    public string KillTooltip => Localizer.Current.ConfirmKillTitle;

    public void Update(PortEntry e, bool watched)
    {
        Remote = e.Remote;
        State = e.State;
        Name = string.IsNullOrEmpty(e.Name) ? Localizer.Current.UnknownProcess : e.Name;
        IsWatched = watched;
        CanKill = !ProcessActions.IsProtected(e.Pid, e.Name) && e.Pid != Environment.ProcessId;
        OnPropertyChanged(nameof(RemoteDisplay));
        OnPropertyChanged(nameof(IsListening));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? prop = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        OnPropertyChanged(prop);
    }

    private void OnPropertyChanged(string? prop) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
}

/// <summary>关注端口卡片</summary>
public sealed class WatchCardVm
{
    public int Port { get; init; }
    public bool IsBusy { get; init; }
    public string StatusText => IsBusy ? Localizer.Current.Busy : Localizer.Current.Free;
    public string InfoText { get; init; } = "";
    public int Pid { get; init; }
    public string ProcessName { get; init; } = "";
    public bool CanKill { get; init; }
    public string ReleaseText => Localizer.Current.Release;
    public string DetailsText => Localizer.Current.Details;
    public string UnwatchTooltip => Localizer.Current.UnwatchTooltip;
}

/// <summary>主视图模型</summary>
public sealed class MainViewModel : INotifyPropertyChanged
{
    public ObservableCollection<PortRow> Visible { get; } = new();
    public ObservableCollection<WatchCardVm> Watched { get; } = new();
    public List<int> WatchPorts { get; } = new();

    private readonly Dictionary<string, PortRow> _rows = new();
    private List<PortEntry> _all = new();
    private string _filter = "listen";
    private string _query = "";
    private int _totalCount;
    private int _visibleCount;
    private string _updateClock = "";
    private string _updateError = "";
    private bool _isRefreshing;
    private int _scanning;

    public MainViewModel()
    {
        var s = SettingsStore.Load();
        Localizer.Current.SetLanguage(s.Language);
        Localizer.Current.LanguageChanged += OnLanguageChanged;
        foreach (var p in s.Watch) WatchPorts.Add(p);
    }

    private void OnLanguageChanged()
    {
        _rows.Clear();
        Apply(_all);
        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(UpdateText));
    }

    public int TotalCount { get => _totalCount; private set => Set(ref _totalCount, value); }
    public int VisibleCount { get => _visibleCount; private set => Set(ref _visibleCount, value); }
    public string UpdateText => _updateError.Length > 0
        ? Localizer.Current.RefreshFailed(_updateError)
        : (_updateClock.Length == 0 ? "" : Localizer.Current.UpdatedAt(_updateClock));
    public bool IsRefreshing { get => _isRefreshing; private set => Set(ref _isRefreshing, value); }

    public string SummaryText => Localizer.Current.Summary(TotalCount, VisibleCount);

    public void SetFilter(string filter)
    {
        _filter = filter;
        RebuildVisible();
    }

    public void SetQuery(string query)
    {
        _query = query?.Trim() ?? "";
        RebuildVisible();
    }

    public bool AddWatch(int port)
    {
        if (port <= 0 || port > 65535 || WatchPorts.Contains(port)) return false;
        WatchPorts.Add(port);
        PersistWatch();
        RefreshWatched();
        RebuildVisible();
        return true;
    }

    public void RemoveWatch(int port)
    {
        WatchPorts.Remove(port);
        PersistWatch();
        RefreshWatched();
        RebuildVisible();
    }

    /// <summary>某端口上正在监听的进程</summary>
    public List<(int Pid, string Name)> ListeningOn(int port) =>
        _all.Where(e => e.Port == port && e.State == "LISTENING")
            .Select(e => (e.Pid, e.Name))
            .Distinct()
            .ToList();

    private void PersistWatch()
    {
        var s = SettingsStore.Load();
        s.Watch = WatchPorts.ToList();
        SettingsStore.Save(s);
    }

    public async Task RefreshAsync()
    {
        if (Interlocked.Exchange(ref _scanning, 1) == 1) return;
        IsRefreshing = true;
        try
        {
            var entries = await Task.Run(PortScanner.Scan);
            Apply(entries);
            _updateError = "";
            _updateClock = DateTime.Now.ToString("HH:mm:ss");
            OnPropertyChanged(nameof(UpdateText));
        }
        catch (Exception ex)
        {
            _updateClock = "";
            _updateError = ex.Message;
            OnPropertyChanged(nameof(UpdateText));
        }
        finally
        {
            IsRefreshing = false;
            Interlocked.Exchange(ref _scanning, 0);
        }
    }

    private bool Matches(PortEntry e)
    {
        if (_filter == "listen" && e.State != "LISTENING") return false;
        if (_filter == "established" && e.State != "ESTABLISHED") return false;
        if (_filter == "watched" && (!WatchPorts.Contains(e.Port) || e.State != "LISTENING")) return false;

        if (_query.Length == 0) return true;
        return e.Port.ToString().Contains(_query)
            || e.Pid.ToString().Contains(_query)
            || e.Name.Contains(_query, StringComparison.OrdinalIgnoreCase)
            || e.Local.Contains(_query, StringComparison.OrdinalIgnoreCase);
    }

    private void Apply(List<PortEntry> entries)
    {
        _all = entries;
        var target = new List<PortRow>(entries.Count);
        var alive = new HashSet<string>(StringComparer.Ordinal);

        foreach (var e in entries)
        {
            alive.Add(e.Key);
            if (!_rows.TryGetValue(e.Key, out var row))
            {
                row = new PortRow
                {
                    Key = e.Key,
                    Port = e.Port,
                    Proto = e.Proto,
                    Local = e.Local,
                    Pid = e.Pid,
                };
                _rows[e.Key] = row;
            }
            row.Update(e, WatchPorts.Contains(e.Port));
            target.Add(row);
        }

        foreach (var stale in _rows.Keys.Where(k => !alive.Contains(k)).ToList())
            _rows.Remove(stale);

        TotalCount = entries.Count;
        RebuildVisible();
        RefreshWatched();
    }

    private void RebuildVisible()
    {
        SyncVisible(_all.Where(Matches)
            .Select(e => _rows.TryGetValue(e.Key, out var r) ? r : null)
            .Where(r => r != null)
            .Cast<PortRow>()
            .ToList());
    }

    /// <summary>增量同步，保留滚动位置并避免整表重绘</summary>
    private void SyncVisible(List<PortRow> target)
    {
        var targetSet = new HashSet<PortRow>(target);
        for (int i = Visible.Count - 1; i >= 0; i--)
        {
            if (!targetSet.Contains(Visible[i])) Visible.RemoveAt(i);
        }

        for (int i = 0; i < target.Count; i++)
        {
            var want = target[i];
            if (i < Visible.Count && ReferenceEquals(Visible[i], want)) continue;
            int idx = Visible.IndexOf(want);
            if (idx >= 0) Visible.Move(idx, i);
            else Visible.Insert(i, want);
        }

        while (Visible.Count > target.Count)
            Visible.RemoveAt(Visible.Count - 1);

        VisibleCount = Visible.Count;
        OnPropertyChanged(nameof(SummaryText));
    }

    private void RefreshWatched()
    {
        Watched.Clear();
        foreach (var port in WatchPorts.OrderBy(p => p))
        {
            var listening = _all.Where(e => e.Port == port && e.State == "LISTENING").ToList();
            var first = listening.FirstOrDefault();
            var pids = listening.Select(e => e.Pid).Distinct().ToList();

            Watched.Add(new WatchCardVm
            {
                Port = port,
                IsBusy = listening.Count > 0,
                InfoText = first == null
                    ? Localizer.Current.Available
                    : first.Name + " · PID " + first.Pid +
                      (pids.Count > 1 ? Localizer.Current.ProcessCount(pids.Count) : ""),
                Pid = first?.Pid ?? 0,
                ProcessName = first?.Name ?? "",
                CanKill = first != null && !ProcessActions.IsProtected(first.Pid, first.Name),
            });
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? prop = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }

    private void OnPropertyChanged(string prop) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
}
