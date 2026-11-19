using System;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Avalonia;
using ErmayMuhasebe.Services;

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
                        PerformCleanInstallCli(args);
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

    private static void PerformCleanInstallCli(string[]? args = null)
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

            string? cliUrl = null;
            string? cliKey = null;
            string? cliMode = null; // "first_install" or "existing_company"
            string? cliTenant = null;

            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    var a = args[i]?.Trim();
                    if (string.Equals(a, "--url", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                        cliUrl = args[++i]?.Trim();
                    else if (string.Equals(a, "--key", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                        cliKey = args[++i]?.Trim();
                    else if (string.Equals(a, "--mode", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                        cliMode = args[++i]?.Trim()?.ToLowerInvariant();
                    else if (string.Equals(a, "--tenant", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                        cliTenant = args[++i]?.Trim();
                }
            }

            // Fallback: Read configuration from disk if not provided on CLI
            if (string.IsNullOrEmpty(cliUrl) || string.IsNullOrEmpty(cliKey))
            {
                var candidateFiles = new[]
                {
                    Path.Combine(appDir, "setup_config.json"),
                    Path.Combine(appDir, "setup_initial_user.json"),
                    Path.Combine(appDir, "ermay_cloud_config.json"),
                    Path.Combine(localAppData, "ermay_cloud_config.json")
                };

                foreach (var cf in candidateFiles)
                {
                    if (File.Exists(cf))
                    {
                        try
                        {
                            var json = File.ReadAllText(cf);
                            using var doc = JsonDocument.Parse(json);
                            var root = doc.RootElement;
                            if (string.IsNullOrEmpty(cliUrl))
                            {
                                if (root.TryGetProperty("SupabaseUrl", out var su) && su.ValueKind == JsonValueKind.String)
                                    cliUrl = su.GetString()?.Trim();
                                else if (root.TryGetProperty("BaseUrl", out var bu) && bu.ValueKind == JsonValueKind.String)
                                    cliUrl = bu.GetString()?.Trim();
                            }
                            if (string.IsNullOrEmpty(cliKey))
                            {
                                if (root.TryGetProperty("SupabaseKey", out var sk) && sk.ValueKind == JsonValueKind.String)
                                    cliKey = sk.GetString()?.Trim();
                                else if (root.TryGetProperty("AuthSecret", out var asProp) && asProp.ValueKind == JsonValueKind.String)
                                    cliKey = asProp.GetString()?.Trim();
                            }
                            if (string.IsNullOrEmpty(cliTenant) && root.TryGetProperty("TenantId", out var ti) && ti.ValueKind == JsonValueKind.String)
                            {
                                cliTenant = ti.GetString()?.Trim();
                            }
                        }
                        catch { }
                    }
                    if (!string.IsNullOrEmpty(cliUrl) && !string.IsNullOrEmpty(cliKey)) break;
                }
            }

            // Automatically decrypt cliKey if it was stored with AES encryption
            if (!string.IsNullOrEmpty(cliKey) && cliKey.StartsWith("ENC::AES::"))
            {
                try
                {
                    cliKey = AuthService.DecryptOrPlain(cliKey);
                }
                catch (Exception decEx)
                {
                    Log($"[VK Clean Install CLI] DecryptKey warning: {decEx.Message}");
                }
            }

            // 1. Purge Supabase Cloud Tables (Unconditional across all tenants)
            if (!string.IsNullOrEmpty(cliUrl) && !string.IsNullOrEmpty(cliKey))
            {
                try
                {
                    Log($"[VK Clean Install CLI] Connecting to Supabase: {cliUrl}...");
                    var yearCtx = new YearContext();
                    var sync = new CloudSyncService(yearCtx);
                    sync.SaveConfig(cliUrl, cliKey);

                    // 1.a: Call PostgreSQL SECURITY DEFINER RPC `factory_reset_all_tables` (atomic server-side wipe)
                    Log("[VK Clean Install CLI] Invoking factory_reset_all_tables RPC on Supabase...");
                    try
                    {
                        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                        string cleanUrl = CloudSyncService.CleanSupabaseUrl(cliUrl);
                        var rpcReq = new HttpRequestMessage(HttpMethod.Post, $"{cleanUrl}/rest/v1/rpc/factory_reset_all_tables");
                        rpcReq.Headers.Add("apikey", cliKey);
                        rpcReq.Headers.Add("Authorization", $"Bearer {cliKey}");
                        rpcReq.Content = new StringContent("{\"p_tenant_id\":null}", Encoding.UTF8, "application/json");
                        var rpcResp = httpClient.Send(rpcReq);
                        Log($"[VK Clean Install CLI] factory_reset_all_tables RPC status: {rpcResp.StatusCode}");
                    }
                    catch (Exception rpcEx)
                    {
                        Log($"[VK Clean Install CLI] factory_reset_all_tables RPC note: {rpcEx.Message}");
                    }

                    // 1.b: Execute comprehensive REST deletion wave across all operational tables
                    Log("[VK Clean Install CLI] Executing comprehensive REST table purge on Supabase...");
                    sync.ClearCloudTablesAsync("all").GetAwaiter().GetResult();

                    if (!string.IsNullOrEmpty(cliTenant) && cliTenant != "default" && cliTenant != "all")
                    {
                        sync.ClearCloudTablesAsync(cliTenant).GetAwaiter().GetResult();
                    }

                    // 1.c: Mode check for kullanicilar (user accounts)
                    if (string.Equals(cliMode, "first_install", StringComparison.OrdinalIgnoreCase))
                    {
                        Log("[VK Clean Install CLI] Mode is first_install: cleaning old test users from kullanicilar...");
                        try
                        {
                            var oldUsers = new[] { "vkrts1992@gmail.com", "v.karatas3458@gmail.com", "volkan", "volkan1", "vkrts02@gmail.com" };
                            foreach (var u in oldUsers)
                            {
                                try
                                {
                                    using var delReq = new HttpRequestMessage(HttpMethod.Delete, $"{CloudSyncService.CleanSupabaseUrl(cliUrl)}/rest/v1/kullanicilar?username=eq.{Uri.EscapeDataString(u)}");
                                    delReq.Headers.Add("apikey", cliKey);
                                    delReq.Headers.Add("Authorization", $"Bearer {cliKey}");
                                    delReq.Headers.TryAddWithoutValidation("Prefer", "return=minimal");
                                    using var delClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                                    delClient.Send(delReq);
                                }
                                catch { }
                            }
                        }
                        catch { }
                    }
                    else
                    {
                        Log("[VK Clean Install CLI] Mode is existing_company: registered users in kullanicilar preserved.");
                    }

                    Log("[VK Clean Install CLI] Supabase tables completely purged.");
                }
                catch (Exception ex)
                {
                    Log($"[VK Clean Install CLI] Supabase purge error: {ex.Message}");
                }
            }
            else
            {
                Log("[VK Clean Install CLI] No Supabase credentials found to purge cloud tables.");
            }

            // 2. Delete all local SQLite databases and temporary WAL/SHM files
            try { SQLite.SQLiteAsyncConnection.ResetPool(); } catch { }
            GC.Collect();
            GC.WaitForPendingFinalizers();

            if (Directory.Exists(appDir))
            {
                var files = Directory.GetFiles(appDir);
                foreach (var f in files)
                {
                    var name = Path.GetFileName(f).ToLowerInvariant();
                    // Match .db, .db3, .wal, .shm, and session cache files
                    if (name.Contains(".db") || name.EndsWith(".wal") || name.EndsWith(".shm") || 
                        name == "login_settings.txt" || name.StartsWith("notifications_") || 
                        name.StartsWith("dismissed_alerts_"))
                    {
                        try 
                        {
                            // Truncate file first in case file handles are lingering
                            try
                            {
                                using (var fs = new FileStream(f, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
                                {
                                    fs.SetLength(0);
                                    fs.Flush();
                                }
                            }
                            catch { }

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

            // Leave clean_install_pending.flag so the desktop app doesn't pull stale cloud data on first launch
            try
            {
                var flag1 = Path.Combine(appDir, "clean_install_pending.flag");
                File.WriteAllText(flag1, "PENDING");
                var flag2 = Path.Combine(localAppData, "clean_install_pending.flag");
                File.WriteAllText(flag2, "PENDING");
                Log("[VK Clean Install CLI] Written clean_install_pending.flag for first launch protection.");
            }
            catch { }

            // 3. Re-create empty SQLite schema (all tables intact, 0 rows)
            try
            {
                SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_e_sqlcipher());
                SQLitePCL.Batteries_V2.Init();
                ErmayMuhasebe.Services.DatabaseService.IsResetting = false;
                CloudSyncService.DisableCloudSync = true;

                var dbPath = Path.Combine(appDir, $"ermay_{DateTime.Now.Year}_tenant_ermay_master.db");
                var dbSvc = new DatabaseService(new YearContext(), dbPath) { IsTestMode = true };
                dbSvc.InitializeAsync(dbPath).GetAwaiter().GetResult();

                var globalConn = dbSvc.GetGlobalConnection().GetConnection();
                globalConn.CreateTable<ErmayMuhasebe.Models.User>();
                globalConn.CreateTable<ErmayMuhasebe.Models.FirmaProfili>();
                globalConn.Execute("DELETE FROM [User];");
                globalConn.Execute("DELETE FROM [FirmaProfili];");

                dbSvc.WipeAllLocalTablesAsync().GetAwaiter().GetResult();
                var syncConn = new SQLite.SQLiteConnection(new SQLite.SQLiteConnectionString(dbPath, SQLite.SQLiteOpenFlags.ReadWrite | SQLite.SQLiteOpenFlags.FullMutex, true, key: ErmayMuhasebe.Data.Constants.DatabasePassword));
                syncConn.Execute("DELETE FROM [FirmaProfili];");
                var tableCount = syncConn.ExecuteScalar<int>("SELECT count(*) FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%';");
                syncConn.Close();
                dbSvc.CloseConnectionAsync().GetAwaiter().GetResult();
                Log($"[VK Clean Install CLI] Initialized empty SQLite schema with {tableCount} tables (0 rows).");
            }
            catch (Exception schemaInitEx)
            {
                Log($"[VK Clean Install CLI] SQLite schema init note: {schemaInitEx.Message}");
            }

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
