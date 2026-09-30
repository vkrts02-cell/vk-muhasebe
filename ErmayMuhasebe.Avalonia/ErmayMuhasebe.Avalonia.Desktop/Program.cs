using System;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia;

namespace ErmayMuhasebe.Avalonia.Desktop;

sealed class Program
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Global Unhandled Exception Handlers
        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                HandleCrash(ex, "AppDomain Unhandled Exception");
            }
        };

        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (sender, e) =>
        {
            HandleCrash(e.Exception, "TaskScheduler Unobserved Task Exception", showDialog: false);
            e.SetObserved();
        };

        try
        {
            // Initialize SQLite SQLCipher native library at the very beginning
            try
            {
                SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_e_sqlcipher());
                SQLitePCL.Batteries_V2.Init();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CRITICAL PROGRAM SQLITE INIT ERROR]: {ex}");
            }

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            HandleCrash(ex, "Critical Startup Error");
            throw;
        }
    }

    private static void HandleCrash(Exception ex, string type, bool showDialog = true)
    {
        var logContent = $"=== ERMAY MUHASEBE CRASH REPORT ===\n" +
                         $"Time: {DateTime.Now}\n" +
                         $"Type: {type}\n" +
                         $"Message: {ex.Message}\n" +
                         $"Details:\n{ex}\n" +
                         $"==================================\n";

        // Log to local directory
        try
        {
            File.WriteAllText("crash_log.txt", logContent);
        }
        catch { }

        // Log to AppDomain Base Directory
        try
        {
            string baseDirLog = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ermay_crash_log.txt");
            File.WriteAllText(baseDirLog, logContent);
        }
        catch { }

        // Log to Desktop
        try
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            if (!string.IsNullOrEmpty(desktop))
            {
                File.WriteAllText(Path.Combine(desktop, "ermay_crash.txt"), logContent);
            }
        }
        catch { }

        // Show Native MessageBox on Windows only for fatal crashes
        if (showDialog && OperatingSystem.IsWindows())
        {
            string message = $"Program beklenmedik bir hata nedeniyle durduruldu.\n\n" +
                             $"Hata Türü: {type}\n" +
                             $"Hata Mesajı: {ex.Message}\n\n" +
                             $"Hata detayları masaüstünüze 'ermay_crash.txt' olarak kaydedilmiştir.\n\n" +
                             $"Lütfen bu dosyayı geliştiriciye iletin.";
            try
            {
                MessageBox(IntPtr.Zero, message, "Ermay Muhasebe - Kritik Hata", 0x10); // 0x10 is MB_ICONERROR
            }
            catch { }
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
