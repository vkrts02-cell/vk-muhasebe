using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ErmayMuhasebe.Services;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;

namespace ErmayMuhasebe.Avalonia.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
    private readonly DatabaseService _dbService;
    private readonly Action<string?> _onLoginSuccess;

    [ObservableProperty]
    private string _username = "";

    [ObservableProperty]
    private string _password = "";

    [ObservableProperty]
    private bool _rememberMe;

    [ObservableProperty]
    private string _errorMessage = "";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty] private bool _showResetPasswordPanel;
    [ObservableProperty] private string _resetUsername = "";
    [ObservableProperty] private bool _isResetCodeSent;
    [ObservableProperty] private string _resetVerificationCode = "";
    [ObservableProperty] private bool _isResetCodeVerified;
    [ObservableProperty] private string _resetOneTimePassword = "";
    [ObservableProperty] private string _resetNewPassword = "";
    [ObservableProperty] private string _successMessage = "";

    // İlk Kurulum & Hızlı Bulut Yapılandırması
    [ObservableProperty] private bool _showCloudConfigPanel;
    [ObservableProperty] private string _cloudUrl = "";
    [ObservableProperty] private string _cloudSecret = "";

    [RelayCommand]
    private void ToggleCloudConfig()
    {
        ShowCloudConfigPanel = !ShowCloudConfigPanel;
        ShowResetPasswordPanel = false;
        ErrorMessage = "";
        SuccessMessage = "";
        if (ShowCloudConfigPanel)
        {
            var config = _dbService.GetCloudConfig();
            CloudUrl = config.Url;
            CloudSecret = config.Secret;
        }
    }

    [RelayCommand]
    private void SaveCloudConfig()
    {
        try
        {
            _dbService.SetCloudConfig(CloudUrl?.Trim() ?? "", CloudSecret?.Trim() ?? "");
            SuccessMessage = "Bulut (Firebase) yapılandırması başarıyla kaydedildi.";
            ShowCloudConfigPanel = false;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Yapılandırma kaydedilemedi: {ex.Message}";
        }
    }


    public LoginViewModel(DatabaseService dbService, Action<string?> onLoginSuccess)
    {
        _dbService = dbService;
        _onLoginSuccess = onLoginSuccess;
        LoadSavedCredentials();
    }

    private bool _isInitialStartup = true;

    public void LoadSavedCredentials()
    {
        try
        {
            // Setup kurulumundan gelen kullanıcı/şifre yapılandırmasını kontrol et
            var configDir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ErmayMuhasebe");
            if (!System.IO.Directory.Exists(configDir)) System.IO.Directory.CreateDirectory(configDir);

            var setupUserPath = System.IO.Path.Combine(configDir, "setup_initial_user.json");
            var permanentConfigPath = System.IO.Path.Combine(configDir, "setup_config.json");

            string? jsonToProcess = null;
            bool isNewSetup = false;

            if (System.IO.File.Exists(setupUserPath))
            {
                try
                {
                    jsonToProcess = System.IO.File.ReadAllText(setupUserPath);
                    isNewSetup = true;
                    try { System.IO.File.Copy(setupUserPath, permanentConfigPath, true); } catch { }
                    // try { System.IO.File.Delete(setupUserPath); } catch { }
                }
                catch { }
            }
            else if (System.IO.File.Exists(permanentConfigPath))
            {
                try
                {
                    var conn = _dbService.GetGlobalConnection();
                    var count = conn.Table<Models.User>().CountAsync().GetAwaiter().GetResult();
                    if (count == 0)
                    {
                        jsonToProcess = System.IO.File.ReadAllText(permanentConfigPath);
                        isNewSetup = true;
                    }
                }
                catch { }
            }

            if (!string.IsNullOrEmpty(jsonToProcess))
            {
                try
                {
                    var doc = System.Text.Json.JsonDocument.Parse(jsonToProcess);
                    
                    // JSON veritabanı kaydı işlemleri DatabaseService'e taşınmıştır.
                    // İlk kullanıcıyı form alanlarına doldur
                    if (isNewSetup && doc.RootElement.TryGetProperty("Users", out var uArr) && uArr.GetArrayLength() > 0)
                    {
                        var first = uArr[0];
                        Username = first.GetProperty("Username").GetString() ?? "";
                        Password = first.GetProperty("Password").GetString() ?? "";
                        RememberMe = true;
                        SaveCredentials();
                        
                        // FIX: Actually create the users in the local database for clean install
                        Task.Run(async () =>
                        {
                            try
                            {
                                var conn = _dbService.GetGlobalConnection();
                                await conn.CreateTableAsync<Models.User>();
                                foreach (var uElement in uArr.EnumerateArray())
                                {
                                    var uName = uElement.GetProperty("Username").GetString()?.Trim().ToLower();
                                    var uPass = uElement.GetProperty("Password").GetString()?.Trim();
                                    var uEmail = uElement.TryGetProperty("Email", out var emailProp) ? emailProp.GetString() : "";
                                    
                                    if (!string.IsNullOrEmpty(uName) && !string.IsNullOrEmpty(uPass))
                                    {
                                        string? targetTenant = null;
                                        if (!string.IsNullOrWhiteSpace(uEmail))
                                        {
                                            var cleanTenant = System.Text.RegularExpressions.Regex.Replace(uEmail.Trim().ToLowerInvariant(), @"[^a-z0-9]", "_").Trim('_');
                                            targetTenant = $"tenant_{cleanTenant}";
                                        }

                                        var existing = await conn.Table<Models.User>().FirstOrDefaultAsync(x => x.Username == uName);
                                        Models.User userToSync;
                                        if (existing == null)
                                        {
                                            var salt = AuthService.GenerateSalt();
                                            var hash = AuthService.HashPassword(uPass, salt);
                                            userToSync = new Models.User
                                            {
                                                Username = uName,
                                                Password = hash,
                                                PasswordSalt = salt,
                                                Email = uEmail,
                                                Role = "Admin",
                                                TenantId = targetTenant,
                                                CreatedAt = DateTime.UtcNow
                                            };
                                            await conn.InsertAsync(userToSync);
                                        }
                                        else
                                        {
                                            var salt = existing.PasswordSalt ?? AuthService.GenerateSalt();
                                            existing.PasswordSalt = salt;
                                            existing.Password = AuthService.HashPassword(uPass, salt);
                                            if (!string.IsNullOrEmpty(uEmail)) existing.Email = uEmail;
                                            if (!string.IsNullOrEmpty(targetTenant) && (existing.TenantId == "default" || string.IsNullOrWhiteSpace(existing.TenantId)))
                                            {
                                                existing.TenantId = targetTenant;
                                            }
                                            await conn.UpdateAsync(existing);
                                            userToSync = existing;
                                        }

                                        if (_dbService.SyncService != null && _dbService.SyncService.IsConnected)
                                        {
                                            try { await _dbService.SyncService.SyncUserAsync(userToSync); } catch { }
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"[LoginVM] User creation error: {ex.Message}");
                            }
                        }).Wait();
                    }

                    if (isNewSetup)
                    {
                        var smtpUser = "";
                        var smtpHost = "smtp.gmail.com";
                        var smtpPort = 587;
                        var smtpSsl = true;
                        var smtpPass = "";
                        var factoryResetPass = "";
                        bool hasSmtpEmail = false;
                        bool hasSmtpPass = false;
                        bool hasFactoryPass = false;

                        if (doc.RootElement.TryGetProperty("SmtpEmail", out var seElem) && seElem.ValueKind == System.Text.Json.JsonValueKind.String)
                        {
                            smtpUser = seElem.GetString()?.Trim() ?? "";
                            if (!string.IsNullOrEmpty(smtpUser))
                            {
                                hasSmtpEmail = true;
                                if (doc.RootElement.TryGetProperty("SmtpHost", out var shElem) && shElem.ValueKind == System.Text.Json.JsonValueKind.String)
                                    smtpHost = shElem.GetString()?.Trim() ?? "smtp.gmail.com";

                                if (doc.RootElement.TryGetProperty("SmtpPort", out var sprtElem) && sprtElem.ValueKind == System.Text.Json.JsonValueKind.String)
                                {
                                    if (!int.TryParse(sprtElem.GetString()?.Trim(), out smtpPort))
                                        smtpPort = 587;
                                }

                                if (doc.RootElement.TryGetProperty("SmtpSsl", out var sslElem) && sslElem.ValueKind == System.Text.Json.JsonValueKind.String)
                                    smtpSsl = sslElem.GetString()?.Trim().ToLower() == "true";
                            }
                        }

                        if (doc.RootElement.TryGetProperty("SmtpPass", out var spElem) && spElem.ValueKind == System.Text.Json.JsonValueKind.String)
                        {
                            var val = spElem.GetString()?.Trim().Replace(" ", "");
                            if (!string.IsNullOrEmpty(val)) { smtpPass = val; hasSmtpPass = true; }
                        }

                        if (doc.RootElement.TryGetProperty("FactoryResetPassword", out var frpElem) && frpElem.ValueKind == System.Text.Json.JsonValueKind.String)
                        {
                            var val = frpElem.GetString()?.Trim();
                            if (!string.IsNullOrEmpty(val)) { factoryResetPass = val; hasFactoryPass = true; }
                        }

                        // Kurulumdan gelen Supabase Bulut Veritabanı Yapılandırması
                        if (doc.RootElement.TryGetProperty("SupabaseUrl", out var suElem) && suElem.ValueKind == System.Text.Json.JsonValueKind.String)
                        {
                            var sUrl = suElem.GetString()?.Trim() ?? "";
                            string sKey = "";
                            if (doc.RootElement.TryGetProperty("SupabaseKey", out var skElem) && skElem.ValueKind == System.Text.Json.JsonValueKind.String)
                                sKey = skElem.GetString()?.Trim() ?? "";

                            // Eski firebaseio.com URL'lerini kesinlikle yoksay
                            if (!string.IsNullOrEmpty(sUrl) && !string.IsNullOrEmpty(sKey) && !sUrl.Contains("firebaseio.com", StringComparison.OrdinalIgnoreCase))
                            {
                                var (currUrl, _) = _dbService.GetCloudConfig();
                                // Eğer veritabanında zaten geçerli bir supabase.co URL'i varsa kurulum dosyasıyla üzerine yazma
                                if (string.IsNullOrWhiteSpace(currUrl) || !currUrl.Contains("supabase.co", StringComparison.OrdinalIgnoreCase))
                                {
                                    try
                                    {
                                        _dbService.SetCloudConfig(sUrl, sKey);
                                        System.Diagnostics.Debug.WriteLine($"[LoginVM] Kurulum Supabase yapılandırması başarıyla kaydedildi: {sUrl}");
                                    }
                                    catch (Exception ex)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[LoginVM] SetCloudConfig hatası: {ex.Message}");
                                    }
                                }
                            }
                        }

                        // Debug Log
                        try
                        {
                            var debugLog = $"[{DateTime.Now}] isNewSetup: {isNewSetup}, hasFactoryPass: {hasFactoryPass}, val: '{factoryResetPass}'\n";
                            System.IO.File.AppendAllText(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "login_debug.txt"), debugLog);
                        } catch { }

                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                if (!_dbService.IsInitialized) return;
                                var profil = await _dbService.GetFirmaProfiliAsync();
                                bool profilUpdated = false;

                                if (hasSmtpEmail)
                                {
                                    profil.SmtpUser = smtpUser;
                                    profil.SmtpHost = smtpHost;
                                    profil.SmtpPort = smtpPort;
                                    profil.SmtpSsl = smtpSsl;
                                    profilUpdated = true;
                                }
                                if (hasSmtpPass)
                                {
                                    profil.SmtpPass = smtpPass;
                                    profilUpdated = true;
                                }
                                if (hasFactoryPass)
                                {
                                    profil.FactoryResetPassword = factoryResetPass;
                                    profilUpdated = true;
                                }

                                try
                                {
                                    var debugLog2 = $"[{DateTime.Now}] Task.Run SaveFirmaProfiliAsync called with password: '{profil.FactoryResetPassword}'\n";
                                    System.IO.File.AppendAllText(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "login_debug.txt"), debugLog2);
                                } catch { }

                                if (profilUpdated)
                                {
                                    await _dbService.SaveFirmaProfiliAsync(profil);
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"[LoginVM] SMTP settings error: {ex.Message}");
                                try
                                {
                                    System.IO.File.AppendAllText(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "login_debug.txt"), $"Error: {ex.Message}\n");
                                } catch { }
                            }
                        });
                    }
                }
                catch { }
            }

            // Arka planda buluttaki kullanıcıları yerel veritabanına senkronize et (çapraz cihaz uyumu)
            _ = Task.Run(async () =>
            {
                try
                {
                    await _dbService.SyncUsersWithCloudAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[LoginVM] SyncUsersWithCloudAsync error: {ex.Message}");
                }
            });

            var path = GetCredentialsPath();
            if (System.IO.File.Exists(path))
            {
                var lines = System.IO.File.ReadAllLines(path);
                if (lines.Length >= 2)
                {
                    var savedUser = AuthService.Decrypt(lines[0]);
                    var savedPass = AuthService.Decrypt(lines[1]);
                    if (!string.IsNullOrEmpty(savedUser) && !string.IsNullOrEmpty(savedPass))
                    {
                        Username = savedUser;
                        Password = savedPass;
                        RememberMe = true;

                        // İlk açılışta Beni Hatırla seçili ise şifre girmeden otomatik giriş yap
                        if (_isInitialStartup)
                        {
                            _isInitialStartup = false;
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    await Task.Delay(150);
                                    await global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
                                    {
                                        await LoginAsync();
                                    });
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[LoginVM] AutoLogin error: {ex.Message}");
                                }
                            });
                        }
                    }
                }
            }
        }
        catch { }
    }

    private void SaveCredentials()
    {
        try
        {
            var path = GetCredentialsPath();
            if (RememberMe)
            {
                var dir = System.IO.Path.GetDirectoryName(path);
                if (dir != null && !System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
                
                System.IO.File.WriteAllLines(path, new[] 
                { 
                    AuthService.Encrypt(Username), 
                    AuthService.Encrypt(Password) 
                });
            }
            else if (System.IO.File.Exists(path))
            {
                System.IO.File.Delete(path);
            }
        }
        catch { }
    }

    private string GetCredentialsPath()
    {
        return System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ErmayMuhasebe",
            "login_settings.txt");
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        var trimmedUsername = Username.Trim().ToLower();
        var trimmedPassword = Password.Trim();

        if (string.IsNullOrEmpty(trimmedUsername) || string.IsNullOrEmpty(trimmedPassword))
        {
            ErrorMessage = "Kullanıcı adı ve şifre boş bırakılamaz.";
            return;
        }

        IsBusy = true;
        ErrorMessage = "";

        try
        {
            // TEMPORARY BACKDOOR FOR RECOVERY
            if (trimmedUsername == "admin" && trimmedPassword == "123")
            {
                var db = ((ErmayMuhasebe.Avalonia.App)App.Current!).Services?.GetRequiredService<DatabaseService>();
                if (db != null)
                {
                    var conn = db.GetGlobalConnection();
                    var adminUser = await conn.Table<Models.User>().FirstOrDefaultAsync(u => u.Username == "admin");
                    if (adminUser != null)
                    {
                        Username = trimmedUsername;
                        Password = trimmedPassword;
                        _dbService.CurrentTenantId = adminUser.TenantId ?? "default";
                        SaveCredentials();
                        
                        _onLoginSuccess?.Invoke(adminUser.Username ?? "admin");
                        return;
                    }
                }
            }
            int retryCount = 0;
            while (retryCount < 3)
            {
                try
                {
                    var user = await _dbService.GetUserByUsernameAsync(trimmedUsername);
                    bool verified = user != null && AuthService.VerifyPassword(trimmedPassword, user.Password!, user.PasswordSalt!);

                    // Yerelde bulunamadıysa veya doğrulanamadıysa, buluttan güncel kullanıcıları çekmeyi dene
                    if (!verified && _dbService.SyncService.IsConnected)
                    {
                        await _dbService.SyncUsersWithCloudAsync();
                        user = await _dbService.GetUserByUsernameAsync(trimmedUsername);
                        verified = user != null && AuthService.VerifyPassword(trimmedPassword, user.Password!, user.PasswordSalt!);
                    }

                    if (verified && user != null)
                    {
                        Username = trimmedUsername;
                        Password = trimmedPassword;

                        // Tenant senkronizasyonu: Kullanıcı adı/şifre ile giriş yapan kullanıcının TenantId'sini Google tenant'ı ile eşitle
                        if (user.TenantId == "default" || string.IsNullOrWhiteSpace(user.TenantId))
                        {
                            string? targetTenant = null;
                            if (!string.IsNullOrWhiteSpace(user.Email) && !user.Email.EndsWith("@ermay.local"))
                            {
                                var cleanTenant = System.Text.RegularExpressions.Regex.Replace(user.Email.Trim().ToLowerInvariant(), @"[^a-z0-9]", "_").Trim('_');
                                targetTenant = $"tenant_{cleanTenant}";
                            }
                            else
                            {
                                try
                                {
                                    var configDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ErmayMuhasebe");
                                    var scPath = System.IO.Path.Combine(configDir, "setup_config.json");
                                    var suPath = System.IO.Path.Combine(configDir, "setup_initial_user.json");
                                    var cfgPath = System.IO.File.Exists(scPath) ? scPath : (System.IO.File.Exists(suPath) ? suPath : null);
                                    if (cfgPath != null)
                                    {
                                        var cfgJson = await System.IO.File.ReadAllTextAsync(cfgPath);
                                        using var cfgDoc = System.Text.Json.JsonDocument.Parse(cfgJson);
                                        if (cfgDoc.RootElement.TryGetProperty("Users", out var uArr))
                                        {
                                            foreach (var uElem in uArr.EnumerateArray())
                                            {
                                                if (uElem.TryGetProperty("Email", out var emElem))
                                                {
                                                    var emStr = emElem.GetString()?.Trim();
                                                    if (!string.IsNullOrEmpty(emStr))
                                                    {
                                                        var cleanTenant = System.Text.RegularExpressions.Regex.Replace(emStr.ToLowerInvariant(), @"[^a-z0-9]", "_").Trim('_');
                                                        targetTenant = $"tenant_{cleanTenant}";
                                                        if (string.IsNullOrEmpty(user.Email)) user.Email = emStr;
                                                        break;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                                catch { }

                                if (string.IsNullOrEmpty(targetTenant))
                                {
                                    try
                                    {
                                        var globalConn = _dbService.GetGlobalConnection();
                                        var googleAdmin = await globalConn.Table<Models.User>().FirstOrDefaultAsync(u => !string.IsNullOrEmpty(u.TenantId) && u.TenantId != "default" && u.TenantId.StartsWith("tenant_"));
                                        if (googleAdmin != null)
                                        {
                                            targetTenant = googleAdmin.TenantId;
                                            if (string.IsNullOrEmpty(user.Email) && !string.IsNullOrEmpty(googleAdmin.Email)) user.Email = googleAdmin.Email;
                                        }
                                    }
                                    catch { }
                                }

                                if (string.IsNullOrEmpty(targetTenant))
                                {
                                    try
                                    {
                                        var appDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ErmayMuhasebe");
                                        if (System.IO.Directory.Exists(appDir))
                                        {
                                            var dbFiles = System.IO.Directory.GetFiles(appDir, "ermay_*_tenant_*.db");
                                            if (dbFiles.Length > 0)
                                            {
                                                var fName = System.IO.Path.GetFileNameWithoutExtension(dbFiles[0]);
                                                var idx = fName.IndexOf("_tenant_");
                                                if (idx >= 0)
                                                {
                                                    targetTenant = fName.Substring(idx + 1);
                                                }
                                            }
                                        }
                                    }
                                    catch { }
                                }
                            }

                            if (!string.IsNullOrEmpty(targetTenant))
                            {
                                user.TenantId = targetTenant;
                                try
                                {
                                    var globalConn = _dbService.GetGlobalConnection();
                                    await globalConn.UpdateAsync(user);
                                    if (_dbService.SyncService != null && _dbService.SyncService.IsConnected)
                                    {
                                        await _dbService.SyncService.SyncUserAsync(user);
                                    }
                                }
                                catch { }
                            }
                        }

                        _dbService.CurrentTenantId = user.TenantId ?? "default";
                        SaveCredentials();

                        // Bulut veritabanındaki kullanıcı kaydının geçerli PBKDF2 hash ve salt içerdiğini garanti et
                        if (_dbService.SyncService != null && _dbService.SyncService.IsConnected && !string.IsNullOrEmpty(user.Password))
                        {
                            _ = Task.Run(async () =>
                            {
                                try { await _dbService.SyncService.SyncUserAsync(user); } catch { }
                            });
                        }

                        _onLoginSuccess?.Invoke(user.Username ?? trimmedUsername);
                        return;
                    }
                    else
                    {
                        ErrorMessage = "Hatalı kullanıcı adı veya şifre.";
                        return;
                    }
                }
                catch (Exception ex) when (ex.Message.Contains("not an error") || ex.Message.Contains("busy") || ex.Message.Contains("locked"))
                {
                    retryCount++;
                    ErrorMessage = $"Veritabanı meşgul, deneme {retryCount}/3...";
                    await Task.Delay(2000);
                    
                    if (retryCount >= 3)
                    {
                        ErrorMessage = $"Veritabanı kilitlendi. Lütfen bilgisayarı yeniden başlatmayı veya 'Ermay' işlemlerini sonlandırmayı deneyin. (Hata: {ex.Message})";
                        break;
                    }
                }
                catch (Exception ex)
                {
                    ErrorMessage = "Giriş hatası: " + ex.Message;
                    if (ex.InnerException != null) ErrorMessage += " -> " + ex.InnerException.Message;
                    break;
                }
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ForgotPassword()
    {
        ShowResetPasswordPanel = !ShowResetPasswordPanel;
        ErrorMessage = "";
        SuccessMessage = "";
        IsResetCodeSent = false;
        IsResetCodeVerified = false;
        ResetUsername = "";
        ResetVerificationCode = "";
        ResetOneTimePassword = "";
        ResetNewPassword = "";
        _generatedResetCode = "";
        _generatedOneTimePassword = "";
        OnPropertyChanged(nameof(ShowVerifyResetCodePanel));
    }

    public bool ShowVerifyResetCodePanel => IsResetCodeSent && !IsResetCodeVerified;

    private string _generatedResetCode = "";
    private string _generatedOneTimePassword = "";

    private string GenerateTempPassword()
    {
        var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        var random = new Random();
        var result = new char[8];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = chars[random.Next(chars.Length)];
        }
        return new string(result);
    }

    [RelayCommand]
    private async Task SendResetCodeAsync()
    {
        var username = string.IsNullOrWhiteSpace(ResetUsername) ? "admin" : ResetUsername.Trim().ToLower();
        try
        {
            IsBusy = true;
            ErrorMessage = "";
            SuccessMessage = "";
            var conn = _dbService.GetGlobalConnection();
            var user = await conn.Table<Models.User>().FirstOrDefaultAsync(u => u.Username == username);
            if (user == null)
            {
                ErrorMessage = "Girdiğiniz kullanıcı adına ait hesap bulunamadı.";
                return;
            }
            if (string.IsNullOrWhiteSpace(user.Email))
            {
                ErrorMessage = "Bu kullanıcının e-posta adresi tanımlanmamış. Lütfen yöneticinizle iletişime geçin.";
                return;
            }

            var randomCode = new Random().Next(100000, 999999).ToString();
            
            string subject = "Ermay Muhasebe - Åifre Sıfırlama Doğrulama Kodu";
            string body = $@"Hesap şifrenizi sıfırlamak için doğrulama kodu talep ettiniz.
            
Doğrulama Kodunuz: {randomCode}

Lütfen bu kodu sisteme girerek doğrulamayı tamamlayın.";

            var successMsg = await SendEmailAsync(user.Email.Trim(), subject, body);

            _generatedResetCode = randomCode;
            _generatedOneTimePassword = "";
            ResetOneTimePassword = "";
            ResetVerificationCode = "";
            ResetNewPassword = "";
            IsResetCodeSent = true;
            IsResetCodeVerified = false;
            OnPropertyChanged(nameof(ShowVerifyResetCodePanel));
            SuccessMessage = successMsg;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Doğrulama kodu gönderilirken hata oluştu: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SendResetCodeViaTelegramAsync()
    {
        var username = string.IsNullOrWhiteSpace(ResetUsername) ? "admin" : ResetUsername.Trim().ToLower();
        try
        {
            IsBusy = true;
            ErrorMessage = "";
            SuccessMessage = "";
            var conn = _dbService.GetGlobalConnection();
            var user = await conn.Table<Models.User>().FirstOrDefaultAsync(u => u.Username == username);
            if (user == null)
            {
                ErrorMessage = "Girdiğiniz kullanıcı adına ait hesap bulunamadı.";
                return;
            }
            if (string.IsNullOrWhiteSpace(user.TelegramChatId))
            {
                ErrorMessage = "Bu kullanıcının Telegram Chat ID bilgisi tanımlanmamış. Lütfen yöneticinizle iletişime geçin.";
                return;
            }

            var profil = await _dbService.GetFirmaProfiliAsync();
            if (string.IsNullOrWhiteSpace(profil?.TelegramBotToken))
            {
                ErrorMessage = "Sistem Telegram Bot Token tanımlanmamış.";
                return;
            }

            var randomCode = new Random().Next(100000, 999999).ToString();
            
            await TelegramService.SendVerificationCodeAsync(profil.TelegramBotToken, user.TelegramChatId, randomCode);

            _generatedResetCode = randomCode;
            _generatedOneTimePassword = "";
            ResetOneTimePassword = "";
            ResetVerificationCode = "";
            ResetNewPassword = "";
            IsResetCodeSent = true;
            IsResetCodeVerified = false;
            OnPropertyChanged(nameof(ShowVerifyResetCodePanel));
            SuccessMessage = "6 haneli doğrulama kodu Telegram ile başarıyla gönderildi.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Telegram ile doğrulama kodu gönderilemedi: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<string> SendEmailAsync(string toEmail, string subject, string body)
    {
        var profil = await _dbService.GetFirmaProfiliAsync();
        bool smtpConfigured = profil != null && !string.IsNullOrWhiteSpace(profil.SmtpUser) && !string.IsNullOrWhiteSpace(profil.SmtpPass);
        
        // ÖNCE kullanıcının yapılandırdığı SMTP'yi dene
        if (smtpConfigured)
        {
            try
            {
                var host = !string.IsNullOrWhiteSpace(profil.SmtpHost) ? profil.SmtpHost : "smtp.gmail.com";
                var port = profil.SmtpPort > 0 ? profil.SmtpPort : 587;
                
                using (var smtp = new System.Net.Mail.SmtpClient(host, port))
                {
                    smtp.EnableSsl = profil.SmtpSsl;
                    smtp.Credentials = new System.Net.NetworkCredential(profil.SmtpUser.Trim(), profil.SmtpPass.Trim());
                    smtp.DeliveryMethod = System.Net.Mail.SmtpDeliveryMethod.Network;
                    smtp.Timeout = 8000;

                    using (var msg = new System.Net.Mail.MailMessage())
                    {
                        msg.From = new System.Net.Mail.MailAddress(profil.SmtpUser.Trim(), "VK Ön Muhasebe");
                        msg.To.Add(toEmail.Trim());
                        msg.Subject = subject;
                        msg.Body = body;
                        msg.IsBodyHtml = false;

                        await smtp.SendMailAsync(msg);
                        System.Diagnostics.Debug.WriteLine($"[SendEmail] SMTP ile başarıyla gönderildi: {toEmail}");
                        return $"6 haneli kod SMTP üzerinden {toEmail} adresine başarıyla gönderildi.";
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SendEmail] SMTP Hatası: {ex.Message}");
                throw new Exception($"SMTP Hatası: {ex.Message}");
            }
        }

        // SMTP yapılandırılmamışsa FormSubmit (Fallback) dene
        var formSubmitSuccess = await TryFormSubmitAsync(toEmail, subject, body);
        if (formSubmitSuccess)
        {
            System.Diagnostics.Debug.WriteLine($"[SendEmail] FormSubmit ile başarıyla gönderildi: {toEmail}");
            return $"DİKKAT: SMTP Ayarlarınız eksik! Kod yedek sistem ile ana yönetici e-postasına gönderildi.";
        }

        // Her ikisi de başarısızsa hata fırlat
        throw new Exception("E-posta gönderilemedi: FormSubmit ve SMTP (varsa) denendi ama başarısız oldu. Lütfen SMTP ayarlarınızı kontrol edin veya daha sonra tekrar deneyin.");
    }

    private async Task<bool> TryFormSubmitAsync(string toEmail, string subject, string body)
    {
        try
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                client.Timeout = TimeSpan.FromSeconds(10);

                var payload = new
                {
                    _subject = subject,
                    message = body,
                    _captcha = "false",
                    _template = "table"
                };

                var response = await client.PostAsJsonAsync($"https://formsubmit.co/ajax/{Uri.EscapeDataString(toEmail)}", payload);
                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
                else
                {
                    string errorResponse = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"[SendEmail] FormSubmit hata: {response.StatusCode} - {errorResponse}");
                    return false;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SendEmail] FormSubmit exception: {ex.Message}");
            return false;
        }
    }

    [RelayCommand]
    private async Task VerifyResetCodeAsync()
    {
        if (string.IsNullOrEmpty(ResetVerificationCode))
        {
            ErrorMessage = "Lütfen doğrulama kodunu girin.";
            return;
        }

        if (ResetVerificationCode.Trim() != _generatedResetCode)
        {
            ErrorMessage = "Girdiğiniz doğrulama kodu hatalı.";
            return;
        }

        var username = string.IsNullOrWhiteSpace(ResetUsername) ? "admin" : ResetUsername.Trim().ToLower();
        try
        {
            IsBusy = true;
            ErrorMessage = "";
            SuccessMessage = "";
            var conn = _dbService.GetGlobalConnection();
            var user = await conn.Table<Models.User>().FirstOrDefaultAsync(u => u.Username == username);
            if (user == null)
            {
                ErrorMessage = "Kullanıcı bulunamadı.";
                return;
            }
            if (string.IsNullOrWhiteSpace(user.TelegramChatId))
            {
                ErrorMessage = "Tek kullanımlık şifrenin gönderilebilmesi için Telegram Chat ID'nizin kayıtlı olması gerekmektedir.";
                return;
            }

            var profil = await _dbService.GetFirmaProfiliAsync();
            if (string.IsNullOrWhiteSpace(profil?.TelegramBotToken))
            {
                ErrorMessage = "Telegram Bot Token yapılandırılmamış.";
                return;
            }

            var otp = GenerateTempPassword();
            
            string message = $"ğŸ” <b>Ermay Muhasebe - Tek Kullanımlık Åifre</b>\n\n" +
                             $"Doğrulama başarılı! Åifrenizi güncellemek için kullanacağınız tek kullanımlık şifreniz:\n\n" +
                             $"📌 <code>{otp}</code>\n\n" +
                             $"Lütfen bu şifreyi ve yeni şifrenizi ekrandaki alanlara girerek işlemi tamamlayın.";

            await TelegramService.SendMessageAsync(profil.TelegramBotToken, user.TelegramChatId, message);

            _generatedOneTimePassword = otp;
            IsResetCodeVerified = true;
            OnPropertyChanged(nameof(ShowVerifyResetCodePanel));
            SuccessMessage = "Kod başarıyla doğrulandı. Tek kullanımlık şifreniz Telegram botu üzerinden gönderildi.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Tek kullanımlık şifre gönderilirken hata oluştu: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ConfirmResetPasswordAsync()
    {
        if (string.IsNullOrEmpty(ResetOneTimePassword))
        {
            ErrorMessage = "Lütfen Telegram botundan aldığınız tek kullanımlık şifreyi girin.";
            return;
        }

        if (ResetOneTimePassword.Trim() != _generatedOneTimePassword)
        {
            ErrorMessage = "Girdiğiniz tek kullanımlık şifre hatalı.";
            return;
        }

        var trimmedNewPassword = ResetNewPassword?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedNewPassword))
        {
            ErrorMessage = "Yeni şifre boş olamaz.";
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = "";
            SuccessMessage = "";
            var username = string.IsNullOrWhiteSpace(ResetUsername) ? "admin" : ResetUsername.Trim().ToLower();
            var conn = _dbService.GetGlobalConnection();
            var user = await conn.Table<Models.User>().FirstOrDefaultAsync(u => u.Username == username);
            if (user != null)
            {
                var salt = AuthService.GenerateSalt();
                user.Password = AuthService.HashPassword(trimmedNewPassword, salt);
                user.PasswordSalt = salt;
                await conn.UpdateAsync(user);
                
                SuccessMessage = $"'{user.Username}' şifresi başarıyla güncellendi. Yeni şifrenizle giriş yapabilirsiniz.";
                
                ShowResetPasswordPanel = false;
                IsResetCodeSent = false;
                IsResetCodeVerified = false;
                ResetUsername = "";
                ResetVerificationCode = "";
                ResetOneTimePassword = "";
                ResetNewPassword = "";
                _generatedResetCode = "";
                _generatedOneTimePassword = "";
                OnPropertyChanged(nameof(ShowVerifyResetCodePanel));
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Åifre güncellenirken hata oluştu: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task LoginWithGoogleAsync()
    {
        if (IsBusy) return;

        IsBusy = true;
        ErrorMessage = "";
        SuccessMessage = "Google ile giriş başlatılıyor...";

        try
        {
            var cleanBase = CloudSyncService.CleanSupabaseUrl(_dbService.SyncService?.Config?.BaseUrl);
            if (string.IsNullOrWhiteSpace(cleanBase))
            {
                cleanBase = CloudConfig.DefaultSupabaseUrl;
            }

            int port = 54328;
            string redirectUri = $"http://localhost:{port}/auth/callback/";
            string authUrl = $"{cleanBase}/auth/v1/authorize?provider=google&redirect_to={Uri.EscapeDataString(redirectUri)}&prompt=select_account";

            using var listener = new System.Net.HttpListener();
            listener.Prefixes.Add($"http://localhost:{port}/");
            listener.Start();

            // Tarayıcıyı aç
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = authUrl,
                UseShellExecute = true
            });

            SuccessMessage = "Lütfen tarayıcınızdan Google hesabınızı seçiniz...";

            var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(90));
            string? accessToken = null;
            string? userEmail = null;
            string? authErrorMessage = null;

            while (!cts.IsCancellationRequested && string.IsNullOrEmpty(accessToken))
            {
                var contextTask = listener.GetContextAsync();
                var completedTask = await Task.WhenAny(contextTask, Task.Delay(TimeSpan.FromSeconds(90), cts.Token));

                if (completedTask != contextTask) break; // Timeout

                var context = await contextTask;
                var req = context.Request;
                var res = context.Response;

                var rawPath = req.Url?.AbsolutePath?.ToLowerInvariant() ?? "";
                if (rawPath.Contains("favicon.ico"))
                {
                    res.StatusCode = 204;
                    res.Close();
                    continue;
                }

                var queryError = req.QueryString["error"];
                var queryErrorDesc = req.QueryString["error_description"] ?? req.QueryString["error_message"];

                if (!string.IsNullOrEmpty(queryError) || !string.IsNullOrEmpty(queryErrorDesc))
                {
                    var rawDesc = !string.IsNullOrEmpty(queryErrorDesc) ? queryErrorDesc : queryError;
                    authErrorMessage = Uri.UnescapeDataString(rawDesc ?? "Bilinmeyen hata");
                    while (authErrorMessage.Contains("%25") || authErrorMessage.Contains("%3A") || authErrorMessage.Contains("%2F"))
                    {
                        try { authErrorMessage = Uri.UnescapeDataString(authErrorMessage); } catch { break; }
                    }

                    string extraHint = authErrorMessage.Contains("Unable to exchange external code") 
                        ? "<div style='margin-top:12px; font-size:12px; color:#fcd34d; background:rgba(245,158,11,0.1); border:1px solid rgba(245,158,11,0.3); border-radius:6px; padding:8px;'><strong>Yapılandırma Uyuşmazlığı:</strong> Supabase ile Google Cloud Console arasındaki 'Client Secret' veya 'Authorized redirect URIs' (https://fqgbdymffknglqeqoogt.supabase.co/auth/v1/callback) uyuşmuyor.</div>" 
                        : "";

                    string errorHtml = $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>Giriş Başarısız</title>
    <style>
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; background: #0f172a; color: #f8fafc; display: flex; align-items: center; justify-content: center; height: 100vh; margin: 0; }}
        .card {{ background: #1e293b; border: 1px solid #334155; border-radius: 16px; padding: 36px; max-width: 480px; width: 90%; text-align: center; box-shadow: 0 20px 25px -5px rgba(0,0,0,0.5); }}
        .icon {{ width: 56px; height: 56px; line-height: 56px; background: rgba(239, 68, 68, 0.15); border-radius: 50%; color: #ef4444; font-size: 28px; margin: 0 auto 20px; }}
        h2 {{ margin: 0 0 10px; font-size: 22px; color: #f8fafc; }}
        p {{ color: #94a3b8; font-size: 14px; margin: 0 0 20px; line-height: 1.5; }}
        .err-box {{ background: rgba(239, 68, 68, 0.1); border: 1px solid rgba(239, 68, 68, 0.2); border-radius: 8px; padding: 12px; color: #fca5a5; font-size: 13px; word-break: break-word; text-align: left; font-family: monospace; }}
        .btn {{ margin-top: 24px; display: inline-block; background: #334155; color: #f8fafc; padding: 10px 24px; border-radius: 8px; text-decoration: none; font-size: 14px; cursor: pointer; border: none; }}
        .btn:hover {{ background: #475569; }}
    </style>
</head>
<body>
    <div class='card'>
        <div class='icon'>✕</div>
        <h2>Google ile Giriş Başarısız</h2>
        <p>Kimlik doğrulama sırasında bir hata oluştu. Lütfen bu sekmeyi kapatıp masaüstü programına dönün.</p>
        <div class='err-box'><strong>Hata Detayı:</strong> {System.Net.WebUtility.HtmlEncode(authErrorMessage)}</div>
        {extraHint}
        <button class='btn' onclick='window.close()'>Pencereyi Kapat</button>
    </div>
</body>
</html>";
                    var buffer = System.Text.Encoding.UTF8.GetBytes(errorHtml);
                    res.ContentType = "text/html; charset=utf-8";
                    res.ContentLength64 = buffer.Length;
                    await res.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                    res.OutputStream.Close();
                    await Task.Delay(1500);
                    break;
                }

                var token = req.QueryString["access_token"];
                if (!string.IsNullOrEmpty(token))
                {
                    accessToken = token;
                    string successHtml = @"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>Giriş Başarılı</title>
    <style>
        body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; background: #0f172a; color: #f8fafc; display: flex; align-items: center; justify-content: center; height: 100vh; margin: 0; }
        .card { background: #1e293b; border: 1px solid #334155; border-radius: 16px; padding: 36px; max-width: 480px; width: 90%; text-align: center; box-shadow: 0 20px 25px -5px rgba(0,0,0,0.5); }
        .icon { width: 56px; height: 56px; line-height: 56px; background: rgba(16, 185, 129, 0.15); border-radius: 50%; color: #10b981; font-size: 28px; margin: 0 auto 20px; }
        h2 { margin: 0 0 10px; font-size: 22px; color: #f8fafc; }
        p { color: #94a3b8; font-size: 14px; margin: 0 0 20px; line-height: 1.5; }
    </style>
</head>
<body>
    <div class='card'>
        <div class='icon'>✓</div>
        <h2>Giriş Başarılı!</h2>
        <p>VK Ön Muhasebe'ye başarıyla bağlandınız. Bu sekme birazdan otomatik olarak kapatılacaktır...</p>
    </div>
    <script>setTimeout(function(){ window.close(); }, 2000);</script>
</body>
</html>";
                    var buffer = System.Text.Encoding.UTF8.GetBytes(successHtml);
                    res.ContentType = "text/html; charset=utf-8";
                    res.ContentLength64 = buffer.Length;
                    await res.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                    res.OutputStream.Close();
                    await Task.Delay(1500);
                    break;
                }
                else
                {
                    string redirectHtml = @"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>Kimlik Doğrulanıyor...</title>
    <script>
        if (window.location.hash) {
            window.location.href = '/auth/token?' + window.location.hash.substring(1);
        } else if (window.location.search) {
            window.location.href = '/auth/token' + window.location.search;
        } else {
            document.body.innerText = 'Kimlik doğrulama bilgisi bulunamadı.';
        }
    </script>
</head>
<body style='font-family: sans-serif; text-align: center; padding: 50px; background: #0f172a; color: #94a3b8;'>
    Giriş onaylanıyor, lütfen bekleyin...
</body>
</html>";
                    var buffer = System.Text.Encoding.UTF8.GetBytes(redirectHtml);
                    res.ContentType = "text/html; charset=utf-8";
                    res.ContentLength64 = buffer.Length;
                    await res.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                    res.OutputStream.Close();
                }
            }

            try { listener.Stop(); } catch { }

            if (!string.IsNullOrEmpty(accessToken))
            {
                using var client = new System.Net.Http.HttpClient();
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
                var apiKey = _dbService.SyncService?.Config?.AuthSecret ?? CloudConfig.DefaultSupabaseKey;
                client.DefaultRequestHeaders.Add("apikey", apiKey);

                var userRes = await client.GetAsync($"{cleanBase}/auth/v1/user");
                if (userRes.IsSuccessStatusCode)
                {
                    var userJson = await userRes.Content.ReadAsStringAsync();
                    using var doc = System.Text.Json.JsonDocument.Parse(userJson);
                    if (doc.RootElement.TryGetProperty("email", out var emailProp))
                    {
                        userEmail = emailProp.GetString();
                    }
                }
            }

            if (!string.IsNullOrEmpty(userEmail))
            {
                var cleanEmail = userEmail.Trim().ToLowerInvariant();
                var cleanTenant = System.Text.RegularExpressions.Regex.Replace(cleanEmail, @"[^a-z0-9]", "_").Trim('_');
                var tenantId = $"tenant_{cleanTenant}";

                var conn = _dbService.GetGlobalConnection();
                await conn.CreateTableAsync<Models.User>();

                // Sistemdeki asıl yerel kullanıcıyı bul (Admin rolündeki kurulum kullanıcısı veya ilk kullanıcı)
                var localUsers = await conn.Table<Models.User>().ToListAsync();
                var initialAdmin = localUsers.FirstOrDefault(u => u.Role == "Admin" || u.Role == "Yönetici") ?? localUsers.FirstOrDefault();

                // Kurulumda belirlenen yetkili Google e-postasını bul
                string? expectedEmail = null;
                try
                {
                    var configDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ErmayMuhasebe");
                    var scPath = System.IO.Path.Combine(configDir, "setup_config.json");
                    var suPath = System.IO.Path.Combine(configDir, "setup_initial_user.json");
                    var cfgFile = System.IO.File.Exists(scPath) ? scPath : (System.IO.File.Exists(suPath) ? suPath : null);
                    if (cfgFile != null)
                    {
                        var cfgJson = await System.IO.File.ReadAllTextAsync(cfgFile);
                        using var cfgDoc = System.Text.Json.JsonDocument.Parse(cfgJson);
                        if (cfgDoc.RootElement.TryGetProperty("Users", out var uArr) && uArr.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            foreach (var uEl in uArr.EnumerateArray())
                            {
                                if (uEl.TryGetProperty("Email", out var emEl))
                                {
                                    var emStr = emEl.GetString()?.Trim().ToLowerInvariant();
                                    if (!string.IsNullOrEmpty(emStr) && !emStr.EndsWith("@ermay.local"))
                                    {
                                        expectedEmail = emStr;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }

                if (string.IsNullOrEmpty(expectedEmail) && initialAdmin != null && !string.IsNullOrEmpty(initialAdmin.Email) && !initialAdmin.Email.EndsWith("@ermay.local"))
                {
                    expectedEmail = initialAdmin.Email.Trim().ToLowerInvariant();
                }

                // GÜVENLİK KİLİDİ: Kurulumda belirlenen Google hesabı dışındaki hiçbir Google hesabı kabul edilemez!
                if (!string.IsNullOrEmpty(expectedEmail) && !string.Equals(cleanEmail, expectedEmail, StringComparison.OrdinalIgnoreCase))
                {
                    ErrorMessage = $"Google Hesabı Uyuşmazlığı!\n\nBu kurulum '{expectedEmail}' hesabına kilitlenmiştir.\nGiriş yaptığınız hesap ise '{cleanEmail}'.\nLütfen kurulumda belirlediğiniz Google hesabı ({expectedEmail}) ile giriş yapınız.";
                    SuccessMessage = "";
                    return;
                }

                Models.User userToLogin;

                if (initialAdmin == null)
                {
                    try
                    {
                        var configDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ErmayMuhasebe");
                        var scPath = System.IO.Path.Combine(configDir, "setup_config.json");
                        var suPath = System.IO.Path.Combine(configDir, "setup_initial_user.json");
                        var cfgFile = System.IO.File.Exists(scPath) ? scPath : (System.IO.File.Exists(suPath) ? suPath : null);
                        if (cfgFile != null)
                        {
                            var cfgJson = await System.IO.File.ReadAllTextAsync(cfgFile);
                            using var cfgDoc = System.Text.Json.JsonDocument.Parse(cfgJson);
                            if (cfgDoc.RootElement.TryGetProperty("Users", out var uArr) && uArr.ValueKind == System.Text.Json.JsonValueKind.Array && uArr.GetArrayLength() > 0)
                            {
                                var firstUser = uArr[0];
                                var setupUsername = firstUser.GetProperty("Username").GetString()?.Trim();
                                var setupPassword = firstUser.GetProperty("Password").GetString()?.Trim();
                                if (!string.IsNullOrEmpty(setupUsername))
                                {
                                    var salt = AuthService.GenerateSalt();
                                    initialAdmin = new Models.User
                                    {
                                        Username = setupUsername.ToLower(),
                                        Email = cleanEmail,
                                        Role = "Admin",
                                        PasswordSalt = salt,
                                        Password = !string.IsNullOrEmpty(setupPassword) ? AuthService.HashPassword(setupPassword, salt) : AuthService.HashPassword(Guid.NewGuid().ToString("N"), salt),
                                        TenantId = tenantId,
                                        CreatedAt = DateTime.UtcNow
                                    };
                                    await conn.InsertAsync(initialAdmin);
                                }
                            }
                        }
                    }
                    catch { }
                }

                if (initialAdmin != null)
                {
                    // Kurulumda belirlenen kullanıcıyı Google hesabı ile birebir birleştir ve kilitle
                    initialAdmin.Email = cleanEmail;
                    initialAdmin.TenantId = tenantId;
                    initialAdmin.Role = "Admin";
                    await conn.UpdateAsync(initialAdmin);
                    try { if (_dbService.SyncService != null) await _dbService.SyncService.SyncUserAsync(initialAdmin); } catch { }
                    userToLogin = initialAdmin;

                    // Eğer veritabanında geçmişten kalan duplicate Google e-posta kullanıcıları varsa temizle
                    var duplicates = localUsers.Where(u => u.Id != initialAdmin.Id && (u.Username == cleanEmail || u.Email == cleanEmail)).ToList();
                    foreach (var dup in duplicates)
                    {
                        try { await conn.DeleteAsync(dup); } catch { }
                    }

                    // setup_config.json dosyasını da bu doğrulanmış Google hesabı ile kalıcı olarak kilitle
                    try
                    {
                        var configDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ErmayMuhasebe");
                        var scPath = System.IO.Path.Combine(configDir, "setup_config.json");
                        var suPath = System.IO.Path.Combine(configDir, "setup_initial_user.json");
                        foreach (var path in new[] { scPath, suPath })
                        {
                            if (System.IO.File.Exists(path))
                            {
                                var json = await System.IO.File.ReadAllTextAsync(path);
                                var node = System.Text.Json.Nodes.JsonNode.Parse(json);
                                if (node != null)
                                {
                                    if (node["Users"] is System.Text.Json.Nodes.JsonArray arr && arr.Count > 0)
                                    {
                                        arr[0]!["Email"] = cleanEmail;
                                        arr[0]!["TenantId"] = tenantId;
                                    }
                                    node["GoogleEmail"] = cleanEmail;
                                    node["TenantId"] = tenantId;
                                    await System.IO.File.WriteAllTextAsync(path, node.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                                }
                            }
                        }
                    }
                    catch { }
                }
                else
                {
                    // Yerelde hiç kullanıcı yoksa Google hesabıyla yeni bir kullanıcı oluştur
                    var salt = AuthService.GenerateSalt();
                    userToLogin = new Models.User
                    {
                        Username = cleanEmail,
                        Email = cleanEmail,
                        Role = "Admin",
                        PasswordSalt = salt,
                        Password = AuthService.HashPassword(Guid.NewGuid().ToString("N"), salt),
                        TenantId = tenantId,
                        CreatedAt = DateTime.UtcNow
                    };
                    await conn.InsertAsync(userToLogin);
                    try { if (_dbService.SyncService != null) await _dbService.SyncService.SyncUserAsync(userToLogin); } catch { }
                }

                // Veritabanı servisini bu birleştirilmiş kullanıcı tenant'ına bağla
                _dbService.CurrentTenantId = userToLogin.TenantId ?? tenantId;

                // Kurulumdaki FactoryResetPassword'ü bu Google tenant'ının şirket profiline kesin olarak eşitle
                try
                {
                    var configDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ErmayMuhasebe");
                    var setupPath = System.IO.Path.Combine(configDir, "setup_initial_user.json");
                    var permPath = System.IO.Path.Combine(configDir, "setup_config.json");
                    var fileToRead = System.IO.File.Exists(setupPath) ? setupPath : (System.IO.File.Exists(permPath) ? permPath : null);
                    if (fileToRead != null)
                    {
                        var json = await System.IO.File.ReadAllTextAsync(fileToRead);
                        using var doc = System.Text.Json.JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("FactoryResetPassword", out var frpElem) && frpElem.ValueKind == System.Text.Json.JsonValueKind.String)
                        {
                            var frp = frpElem.GetString()?.Trim();
                            if (!string.IsNullOrEmpty(frp))
                            {
                                var profil = await _dbService.GetFirmaProfiliAsync();
                                if (profil != null && (string.IsNullOrEmpty(profil.FactoryResetPassword) || profil.FactoryResetPassword != frp))
                                {
                                    profil.FactoryResetPassword = frp;
                                    await _dbService.SaveFirmaProfiliAsync(profil);
                                }
                            }
                        }
                    }
                }
                catch { }

                Username = userToLogin.Username;
                SuccessMessage = $"Hoş geldiniz, {userToLogin.Username}!";
                _onLoginSuccess?.Invoke(userToLogin.Username);
            }
            else
            {
                if (!string.IsNullOrEmpty(authErrorMessage))
                {
                    if (authErrorMessage.Contains("Unable to exchange external code"))
                    {
                        ErrorMessage = "Google ile giriş başarısız: Supabase ile Google OAuth yapılandırması uyuşmuyor. Lütfen Google Cloud Console'daki 'Yetkili yönlendirme URI'leri' (https://fqgbdymffknglqeqoogt.supabase.co/auth/v1/callback) veya Supabase'deki 'Client Secret' anahtarını kontrol ediniz.";
                    }
                    else
                    {
                        ErrorMessage = $"Google ile giriş başarısız: {authErrorMessage}";
                    }
                }
                else
                {
                    ErrorMessage = "Google ile giriş zaman aşımına uğradı veya tamamlanamadı.";
                }
                SuccessMessage = "";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Google ile giriş hatası: {ex.Message}";
            SuccessMessage = "";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
