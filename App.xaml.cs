using Microsoft.UI.Xaml;
using PortManagerWin.ViewModels;

namespace PortManagerWin;

public partial class App : Application
{
    public static MainWindow? MainWindowRef { get; private set; }

    /// <summary>主题设置：System / Light / Dark</summary>
    public static string ThemeSetting { get; private set; } = "System";

    public static ElementTheme CurrentTheme => ThemeSetting switch
    {
        "Light" => ElementTheme.Light,
        "Dark" => ElementTheme.Dark,
        _ => ElementTheme.Default,
    };

    public App()
    {
        InitializeComponent();

        if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "--diag-details") >= 0)
        {
            Services.Diag.Enabled = true;
            Services.Diag.Reset();
            Services.Diag.Log("app start (diag enabled)");
        }

        UnhandledException += (_, e) =>
        {
            Services.Diag.Log($"UnhandledException: {e.Exception}");
            LogCrash(e.Exception);
            e.Handled = false;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            Services.Diag.Log($"AppDomain unhandled: {e.ExceptionObject}");
            LogCrash(e.ExceptionObject as Exception);
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            LogCrash(e.Exception);
            e.SetObserved();
        };
    }

    private static void LogCrash(Exception? ex)
    {
        try
        {
            var dir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PortManagerWin");
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.AppendAllText(System.IO.Path.Combine(dir, "error.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\r\n\r\n");
        }
        catch { }
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var settings = SettingsStore.Load();
        ThemeSetting = settings.Theme;
        Services.Localizer.Current.SetLanguage(settings.Language);

        MainWindowRef = new MainWindow();
        MainWindowRef.Activate();

        ApplyTheme(ThemeSetting, persist: false);
    }

    public static void ApplyTheme(string setting, bool persist = true)
    {
        ThemeSetting = setting;
        MainWindowRef?.ApplyTheme(CurrentTheme);

        if (!persist) return;

        var settings = SettingsStore.Load();
        settings.Theme = setting;
        SettingsStore.Save(settings);
    }
}
