using System;
using System.IO;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using Windows.UI;

namespace PortManagerWin;

public sealed partial class MainWindow : Window
{
    private const int DesignWidth = 1200;
    private const int DesignHeight = 840;

    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        var icon = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        if (File.Exists(icon))
        {
            try { AppWindow.SetIcon(icon); } catch { }
        }

        RootFrame.Navigate(typeof(MainPage));
        ApplyLanguage();

        RootGrid.Loaded += OnRootLoaded;
    }

    private void OnRootLoaded(object sender, RoutedEventArgs e)
    {
        RootGrid.Loaded -= OnRootLoaded;

        double scale = RootGrid.XamlRoot?.RasterizationScale ?? 1.0;
        int w = (int)(DesignWidth * scale);
        int h = (int)(DesignHeight * scale);

        try
        {
            AppWindow.Resize(new SizeInt32(w, h));

            var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
            if (area != null)
            {
                var work = area.WorkArea;
                AppWindow.Move(new PointInt32(
                    work.X + Math.Max(0, (work.Width - w) / 2),
                    work.Y + Math.Max(0, (work.Height - h) / 2)));
            }

            if (AppWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.PreferredMinimumWidth = (int)(940 * scale);
                presenter.PreferredMinimumHeight = (int)(620 * scale);
            }
        }
        catch { /* 个别环境不支持则保持默认尺寸 */ }
    }

    /// <summary>按当前语言刷新窗口标题（应用名固定为英文）</summary>
    public void ApplyLanguage()
    {
        Title = Services.Localizer.AppName;
        AppTitleBar.Title = Services.Localizer.AppName;
    }

    /// <summary>切换浅色/深色；跟随系统时透出 Mica 材质</summary>
    public void ApplyTheme(ElementTheme theme)
    {
        if (Content is FrameworkElement root)
            root.RequestedTheme = theme;

        // 跟随系统时用透明背景让 Mica 透出；强制主题时用不透明底色盖住 Mica，
        // 避免系统着色与界面主题不一致（不销毁 SystemBackdrop，否则窗口会失去渲染）
        RootGrid.Background = theme switch
        {
            ElementTheme.Dark => new SolidColorBrush(Color.FromArgb(255, 0x20, 0x20, 0x20)),
            ElementTheme.Light => new SolidColorBrush(Color.FromArgb(255, 0xF3, 0xF3, 0xF3)),
            _ => new SolidColorBrush(Colors.Transparent),
        };

        Services.Diag.Log($"ApplyTheme({theme}) rootReq={(Content as FrameworkElement)?.RequestedTheme}");

        UpdateCaptionButtons(theme);
    }

    private void UpdateCaptionButtons(ElementTheme theme)
    {
        try
        {
            bool dark = theme == ElementTheme.Dark ||
                        (theme == ElementTheme.Default && Application.Current.RequestedTheme == ApplicationTheme.Dark);

            var tb = AppWindow.TitleBar;
            var fg = dark ? Colors.White : Color.FromArgb(255, 0x1A, 0x1A, 0x1A);
            tb.ButtonForegroundColor = fg;
            tb.ButtonHoverForegroundColor = fg;
            tb.ButtonPressedForegroundColor = fg;
            tb.ButtonBackgroundColor = Colors.Transparent;
            tb.ButtonInactiveBackgroundColor = Colors.Transparent;
            tb.ButtonHoverBackgroundColor = dark
                ? Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF)
                : Color.FromArgb(0x14, 0x00, 0x00, 0x00);
        }
        catch { }
    }
}
