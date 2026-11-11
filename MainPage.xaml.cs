using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using PortManagerWin.Services;
using PortManagerWin.ViewModels;
using VirtualKey = Windows.System.VirtualKey;

namespace PortManagerWin;

public sealed partial class MainPage : Page
{
    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _timer;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _infoTimer;
    private readonly List<Action> _settingsTextUpdaters = new();
    private StackPanel? _settingsPanel;

    public MainViewModel Vm { get; } = new();
    public Localizer Loc => Localizer.Current;

    /// <summary>
    /// 弹层（ContentDialog / Flyout）处在独立的视觉树里，RequestedTheme=Default 会回退到
    /// Application.RequestedTheme（默认浅色），导致深色界面下弹窗变成白底。
    /// 这里统一取页面实际解析出来的主题。
    /// </summary>
    private ElementTheme PopupTheme => ActualTheme == ElementTheme.Dark ? ElementTheme.Dark : ElementTheme.Light;

    private static Style? AccentButtonStyle =>
        Application.Current.Resources.TryGetValue("AccentButtonStyle", out var style) ? style as Style : null;

    public MainPage()
    {
        InitializeComponent();

        var settings = SettingsStore.Load();
        AutoSwitch.IsOn = settings.AutoRefresh;

        _timer = DispatcherQueue.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(3);
        _timer.Tick += async (_, _) =>
        {
            if (AutoSwitch.IsOn) await RefreshAsync();
        };

        Loaded += OnLoaded;
        Unloaded += (_, _) => _timer.Stop();

        var refreshAccelerator = new KeyboardAccelerator { Key = VirtualKey.F5 };
        refreshAccelerator.Invoked += async (_, args) =>
        {
            args.Handled = true;
            await RefreshAsync();
        };
        KeyboardAccelerators.Add(refreshAccelerator);

        BuildSettingsFlyout();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await RefreshAsync();
        _timer.Start();

        // 关注卡片总宽超过可视区时，ScrollViewer 首次布局会把偏移推到最右端，
        // 导致第一张卡片被裁掉半个；等布局稳定后显式复位到起点。
        await Task.Delay(260);
        WatchScroll.ChangeView(0, null, null, true);

        // 诊断入口：--diag-details <pid> 自动打开详情对话框，便于稳定复现问题
        var argv = Environment.GetCommandLineArgs();
        int at = Array.IndexOf(argv, "--diag-details");
        if (at >= 0 && at + 1 < argv.Length && int.TryParse(argv[at + 1], out int diagPid))
        {
            Diag.Log("diag hook: opening details");
            await ShowDetailAsync(diagPid);
            Diag.Log("diag hook: finished");
        }
    }

    private async Task RefreshAsync()
    {
        await Vm.RefreshAsync();
        UpdateEmptyState();

        // 首次填充数据时 ListView 可能尚未完成布局，强制触发一次，避免出现空白列表
        DispatcherQueue.TryEnqueue(() => PortList.UpdateLayout());
    }

    private void UpdateEmptyState() =>
        EmptyState.Visibility = Vm.Visible.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    /* ---------------- 工具栏 ---------------- */

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
        Vm.SetQuery(sender.Text);
        UpdateEmptyState();
    }

    private void FilterBar_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        var tag = sender.SelectedItem?.Tag as string ?? "listen";
        Vm.SetFilter(tag);
        UpdateEmptyState();
    }

    private void AutoSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        var settings = SettingsStore.Load();
        settings.AutoRefresh = AutoSwitch.IsOn;
        SettingsStore.Save(settings);
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

    /* ---------------- 设置面板 ---------------- */

    private sealed record SettingOption(string Tag, Func<string> Text, bool IsSelected);

    private void BuildSettingsFlyout()
    {
        _settingsTextUpdaters.Clear();
        var content = BuildSettingsContent();

        var flyout = new Flyout
        {
            Placement = FlyoutPlacementMode.BottomEdgeAlignedRight,
            Content = content,
        };
        flyout.Opening += (_, _) =>
        {
            if (_settingsPanel != null) _settingsPanel.RequestedTheme = PopupTheme;
            RefreshSettingsTexts();
        };
        SettingsButton.Flyout = flyout;
    }

    private FrameworkElement BuildSettingsContent()
    {
        var panel = new StackPanel
        {
            Width = 300,
            Spacing = 20,
            RequestedTheme = PopupTheme,
        };
        _settingsPanel = panel;

        var title = new TextBlock { FontSize = 20, FontWeight = FontWeights.SemiBold };
        _settingsTextUpdaters.Add(() => title.Text = Loc.Settings);
        title.Text = Loc.Settings;
        panel.Children.Add(title);

        // 外观
        panel.Children.Add(BuildSettingsGroup("appearance", () => Loc.Appearance, new[]
        {
            new SettingOption("System", () => Loc.ThemeSystem, App.ThemeSetting == "System"),
            new SettingOption("Light", () => Loc.ThemeLight, App.ThemeSetting == "Light"),
            new SettingOption("Dark", () => Loc.ThemeDark, App.ThemeSetting == "Dark"),
        }, tag =>
        {
            App.ApplyTheme(tag);
            panel.RequestedTheme = PopupTheme;
        }));

        // 语言
        panel.Children.Add(BuildSettingsGroup("language", () => Loc.LanguageLabel, new[]
        {
            new SettingOption("zh-CN", () => "中文（简体）", Localizer.Current.Language == "zh-CN"),
            new SettingOption("en-US", () => "English", Localizer.Current.Language == "en-US"),
        }, tag =>
        {
            var settings = SettingsStore.Load();
            settings.Language = tag;
            SettingsStore.Save(settings);

            Localizer.Current.SetLanguage(tag);
            App.MainWindowRef?.ApplyLanguage();
            RefreshSettingsTexts();
        }));

        return panel;
    }

    private StackPanel BuildSettingsGroup(string groupName, Func<string> header, SettingOption[] options,
        Action<string> onSelected)
    {
        var group = new StackPanel { Spacing = 6 };

        var headerBlock = new TextBlock { FontSize = 12.5, Opacity = 0.66 };
        _settingsTextUpdaters.Add(() => headerBlock.Text = header());
        headerBlock.Text = header();
        group.Children.Add(headerBlock);

        foreach (var option in options)
        {
            var radio = new RadioButton
            {
                GroupName = groupName,
                Tag = option.Tag,
                Content = option.Text(),
                IsChecked = option.IsSelected,
            };

            var captured = option;
            radio.Checked += (_, _) =>
            {
                if (radio.IsChecked == true) onSelected(captured.Tag);
            };
            _settingsTextUpdaters.Add(() => radio.Content = captured.Text());

            group.Children.Add(radio);
        }

        return group;
    }

    private void RefreshSettingsTexts()
    {
        foreach (var update in _settingsTextUpdaters) update();
    }

    /* ---------------- 关注端口 ---------------- */

    private void AddWatch_Click(object sender, RoutedEventArgs e) => AddWatch();

    private void WatchInput_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter) return;
        e.Handled = true;
        AddWatch();
    }

    private void AddWatch()
    {
        if (!int.TryParse(WatchInput.Text.Trim(), out int port) || port <= 0 || port > 65535)
        {
            ShowInfo(false, Loc.InvalidPort);
            return;
        }

        if (!Vm.AddWatch(port))
        {
            ShowInfo(false, Loc.AlreadyWatched(port));
            return;
        }

        WatchInput.Text = "";
        UpdateEmptyState();
    }

    private void Unwatch_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not int port) return;
        Vm.RemoveWatch(port);
        UpdateEmptyState();
    }

    private async void ReleasePort_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not int port) return;

        var targets = Vm.ListeningOn(port);
        if (targets.Count == 0)
        {
            ShowInfo(true, Loc.PortIsFree(port));
            await RefreshAsync();
            return;
        }

        var desc = string.Join(", ", targets.Select(t => $"{t.Name} (PID {t.Pid})"));
        if (!await ConfirmAsync(Loc.ConfirmReleaseTitle(port), Loc.ConfirmReleaseBody(port, desc)))
            return;

        var messages = new List<string>();
        bool allOk = true;
        foreach (var (pid, name) in targets)
        {
            var (ok, msg) = await Task.Run(() => ProcessActions.Kill(pid, name));
            allOk &= ok;
            messages.Add(msg);
        }

        ShowInfo(allOk, string.Join("; ", messages));
        await RefreshAsync();
    }

    private async void WatchDetail_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not int pid || pid <= 0) return;
        await ShowDetailAsync(pid);
    }

    /* ---------------- 端口列表 ---------------- */

    private async void Detail_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not int pid) return;
        await ShowDetailAsync(pid);
    }

    private async void Kill_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not PortRow row) return;
        await KillPidAsync(row.Pid, row.Name);
    }

    private async Task KillPidAsync(int pid, string name)
    {
        if (!await ConfirmAsync(Loc.ConfirmKillTitle, Loc.ConfirmKillBody(name, pid)))
            return;

        var (ok, msg) = await Task.Run(() => ProcessActions.Kill(pid, name));
        ShowInfo(ok, msg);
        await RefreshAsync();
    }

    /* ---------------- 对话框 ---------------- */

    private async Task<bool> ConfirmAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 22,
                FontSize = 13,
            },
            PrimaryButtonText = Loc.ConfirmEndButton,
            CloseButtonText = Loc.Cancel,
            // 主操作用强调色，默认键不绑定任何按钮，避免回车误触结束进程
            PrimaryButtonStyle = AccentButtonStyle,
            DefaultButton = ContentDialogButton.None,
            XamlRoot = XamlRoot,
            RequestedTheme = PopupTheme,
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private async Task ShowDetailAsync(int pid)
    {
        Diag.Log($"ShowDetailAsync enter pid={pid}");

        // 先弹对话框 + 加载态，避免等待 PowerShell 查询时界面像卡住一样
        var loadingPanel = new StackPanel
        {
            Spacing = 12,
            MinWidth = 440,
            MinHeight = 96,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        loadingPanel.Children.Add(new ProgressRing
        {
            IsActive = true,
            Width = 26,
            Height = 26,
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        loadingPanel.Children.Add(new TextBlock
        {
            Text = Loc.LoadingDetails,
            FontSize = 14.5,
            Opacity = 0.7,
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        var dialog = new ContentDialog
        {
            Title = Loc.ProcessDetailsTitle,
            Content = loadingPanel,
            PrimaryButtonText = Loc.ConfirmEndButton,
            CloseButtonText = Loc.Close,
            PrimaryButtonStyle = AccentButtonStyle,
            DefaultButton = ContentDialogButton.None,
            IsPrimaryButtonEnabled = false,
            XamlRoot = XamlRoot,
            RequestedTheme = PopupTheme,
        };

        try
        {
            var showOperation = dialog.ShowAsync();

            ProcessDetail detail;
            try
            {
                detail = await Task.Run(() => ProcessActions.GetDetail(pid));
                Diag.Log($"GetDetail returned name={detail.Name} cmdLen={detail.CommandLine.Length}");
            }
            catch (Exception ex)
            {
                Diag.Log($"GetDetail threw: {ex}");
                dialog.Content = new TextBlock
                {
                    Text = Loc.DetailFailed(ex.Message),
                    FontSize = 14.5,
                    TextWrapping = TextWrapping.Wrap,
                    MinWidth = 460,
                };
                await showOperation;
                return;
            }

            dialog.Content = new ScrollViewer
            {
                Content = BuildDetailPanel(detail, pid),
                MaxHeight = 620,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollMode = ScrollMode.Auto,
                // 只关滚动条不够，还必须关掉滚动模式，否则内容仍按更宽的尺寸测量，
                // 代码块右边缘会被对话框内容区裁掉（表现为右边框缺失）
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                HorizontalScrollMode = ScrollMode.Disabled,
            };
            dialog.IsPrimaryButtonEnabled = detail.Exists && !ProcessActions.IsProtected(pid, detail.Name);

            var result = await showOperation;
            Diag.Log($"ShowAsync returned {result}");
            if (result == ContentDialogResult.Primary)
                await KillPidAsync(pid, detail.Name);
        }
        catch (Exception ex)
        {
            // 详情属于辅助功能，任何异常都不应让整个应用退出
            Diag.Log($"ShowDetailAsync failed: {ex}");
            ShowInfo(false, Loc.DetailFailed(ex.Message));
        }
    }

    private FrameworkElement BuildDetailPanel(ProcessDetail detail, int pid)
    {
        if (!detail.Exists)
        {
            return new TextBlock
            {
                Text = Loc.ProcessGone,
                FontSize = 14.5,
                TextWrapping = TextWrapping.Wrap,
                MinWidth = 480,
            };
        }

        var panel = new StackPanel { Spacing = 16, MinWidth = 480 };

        static TextBlock Label(string text) => new()
        {
            Text = text,
            FontSize = 13,
            Opacity = 0.62,
        };

        void AddRow(string label, string value, bool mono = false)
        {
            var block = new StackPanel { Spacing = 5 };
            block.Children.Add(Label(label));

            var valueText = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(value) ? "—" : value,
                FontSize = 14.5,
                TextWrapping = TextWrapping.Wrap,
                IsTextSelectionEnabled = true,
            };
            // 注意：TextBlock.FontFamily 不能赋 null，WinUI 3 会在原生层直接崩溃（0xc000027b），
            // 且不产生托管异常。所以只在需要等宽字体时才设置。
            if (mono) valueText.FontFamily = new FontFamily("Consolas");
            block.Children.Add(valueText);

            panel.Children.Add(block);
        }

        // 命令行可能长达数千字符，单独放进带边框的可滚动代码块，避免把弹窗撑得过高
        void AddCodeBlock(string label, string value)
        {
            var block = new StackPanel { Spacing = 5 };
            block.Children.Add(Label(label));

            var code = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(value) ? "—" : value,
                FontSize = 14,
                FontFamily = new FontFamily("Consolas"),
                TextWrapping = TextWrapping.Wrap,
                IsTextSelectionEnabled = true,
                // 固定行高，让滚动区正好按整行截断，不会把某行字切成两半
                LineHeight = 20,
                LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
            };

            block.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 10, 12, 10),
                Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(1),
                Child = new ScrollViewer
                {
                    Content = code,
                    MaxHeight = 160,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    VerticalScrollMode = ScrollMode.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    HorizontalScrollMode = ScrollMode.Disabled,
                },
            });

            panel.Children.Add(block);
        }

        AddRow(Loc.FieldName, detail.Name);
        AddRow("PID", pid.ToString(), mono: true);
        AddRow(Loc.FieldPath, string.IsNullOrWhiteSpace(detail.Path) ? Loc.NoPermissionPath : detail.Path, mono: true);
        AddCodeBlock(Loc.FieldCommandLine,
            string.IsNullOrWhiteSpace(detail.CommandLine) ? Loc.NoPermissionCommandLine : detail.CommandLine);
        AddRow(Loc.FieldStartTime, detail.StartTime, mono: true);
        AddRow(Loc.FieldMemory, detail.WorkingSet, mono: true);
        AddRow(Loc.FieldParentPid, detail.ParentPid, mono: true);

        return panel;
    }

    private void ShowInfo(bool ok, string message)
    {
        Info.Severity = ok ? InfoBarSeverity.Success : InfoBarSeverity.Error;
        Info.Message = message;
        Info.IsOpen = true;

        _infoTimer?.Stop();
        _infoTimer = DispatcherQueue.CreateTimer();
        _infoTimer.Interval = TimeSpan.FromSeconds(6);
        _infoTimer.Tick += (_, _) =>
        {
            Info.IsOpen = false;
            _infoTimer?.Stop();
        };
        _infoTimer.Start();
    }
}
