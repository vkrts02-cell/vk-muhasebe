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
            if (args != null && args.Length > 0)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    var arg = args[i]?.Trim().ToLowerInvariant();
                    if (arg == "--clean-install" || arg == "--clear-database" || arg == "/cleaninstall")
                    {
                        PerformCleanInstallCli();
                        return;
                    }
                }
            }

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

    private static void PerformCleanInstallCli()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string appDir = Path.Combine(localAppData, "ErmayMuhasebe");
        string logPath = Path.Combine(appDir, "clean_install.log");
        if (!Directory.Exists(appDir)) Directory.CreateDirectory(appDir);

        void Log(string msg)
        {
            try { File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] {msg}\n"); } catch { }
            Console.WriteLine(msg);
        }

        Log("[VK Clean Install CLI] Starting comprehensive clean install reset...");
        try
        {
            ErmayMuhasebe.Services.DatabaseService.IsResetting = true;

            // 1. Reset Supabase Cloud Tables using CloudSyncService
            try
            {
                var yearCtx = new ErmayMuhasebe.Services.YearContext();
                var sync = new ErmayMuhasebe.Services.CloudSyncService(yearCtx);
                Log($"[VK Clean Install CLI] CloudSyncService IsConnected: {sync.IsConnected}, BaseUrl: {sync.BaseUrl}");
                if (sync.IsConnected)
                {
                    Log("[VK Clean Install CLI] Purging Supabase Cloud Tables (default)...");
                    sync.ClearCloudTablesAsync("default").GetAwaiter().GetResult();

                    try
                    {
                        var cfgFile = Path.Combine(appDir, "setup_config.json");
                        if (File.Exists(cfgFile))
                        {
                            var cfgJson = File.ReadAllText(cfgFile);
                            using var cfgDoc = System.Text.Json.JsonDocument.Parse(cfgJson);
                            if (cfgDoc.RootElement.TryGetProperty("TenantId", out var tProp) && !string.IsNullOrWhiteSpace(tProp.GetString()))
                            {
                                var tId = tProp.GetString()!;
                                Log($"[VK Clean Install CLI] Purging Supabase for tenant: {tId}...");
                                sync.ClearCloudTablesAsync(tId).GetAwaiter().GetResult();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"[VK Clean Install CLI] Tenant cloud purge error: {ex.Message}");
                    }
                    Log("[VK Clean Install CLI] Supabase Cloud Tables purged successfully.");
                }
                else
                {
                    Log("[VK Clean Install CLI] CloudSyncService is NOT connected! Trying manual LoadConfig from local appdata...");
                    var cfgFile = Path.Combine(appDir, "ermay_cloud_config.json");
                    if (File.Exists(cfgFile))
                    {
                        Log("[VK Clean Install CLI] Found config in ErmayMuhasebe folder, retrying...");
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"[VK Clean Install CLI] Cloud purge error: {ex}");
            }

            // 2. Delete all local SQLite databases and temporary WAL/SHM files
            if (Directory.Exists(appDir))
            {
                var files = Directory.GetFiles(appDir);
                foreach (var f in files)
                {
                    var name = Path.GetFileName(f).ToLowerInvariant();
                    if (name.Contains(".db") || name.EndsWith(".wal") || name.EndsWith(".shm") || name == "clean_install_pending.flag")
                    {
                        try
                        {
                            using (var fs = new FileStream(f, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
                            {
                                fs.SetLength(0);
                                fs.Flush();
                            }
                        }
                        catch { }

                        try 
                        { 
                            File.Delete(f); 
                            Log($"[VK Clean Install CLI] Deleted local file: {name}");
                        } 
                        catch (Exception delEx) 
                        {
                            Log($"[VK Clean Install CLI] Failed to delete {name}: {delEx.Message}");
                        }
                    }
                }
            }

            // Also check for pending flag in localAppData root
            try
            {
                var flag1 = Path.Combine(appDir, "clean_install_pending.flag");
                if (File.Exists(flag1)) File.Delete(flag1);
                var flag2 = Path.Combine(localAppData, "clean_install_pending.flag");
                if (File.Exists(flag2)) File.Delete(flag2);
            }
            catch { }

            Log("[VK Clean Install CLI] Clean install completed successfully.");
        }
        catch (Exception ex)
        {
            Log($"[VK Clean Install CLI] Error: {ex}");
        }
        finally
        {
            ErmayMuhasebe.Services.DatabaseService.IsResetting = false;
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
