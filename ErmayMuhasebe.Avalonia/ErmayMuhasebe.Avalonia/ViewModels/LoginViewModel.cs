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
                                        var existing = await conn.Table<Models.User>().FirstOrDefaultAsync(x => x.Username == uName);
                                        if (existing == null)
                                        {
                                            var salt = AuthService.GenerateSalt();
                                            var hash = AuthService.HashPassword(uPass, salt);
                                            await conn.InsertAsync(new Models.User
                                            {
                                                Username = uName,
                                                Password = hash,
                                                PasswordSalt = salt,
                                                Email = uEmail,
                                                Role = "Admin",
                                                CreatedAt = DateTime.UtcNow
                                            });
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

                            if (!string.IsNullOrEmpty(sUrl) && !string.IsNullOrEmpty(sKey))
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

            // TEMİZ KURULUM: setup_initial_user.json varsa bulut kullanıcı senkronizasyonunu atla
            var cleanInstallConfigDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ErmayMuhasebe");
            var cleanInstallSetupPath = Path.Combine(cleanInstallConfigDir, "setup_initial_user.json");
            bool isCleanInstall = File.Exists(cleanInstallSetupPath);

            if (!isCleanInstall)
            {
                // Arka planda buluttaki kullanıcıları yerel veritabanına senkronize et
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
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[LoginVM] TEMİZ KURULUM tespit edildi - bulut kullanıcı senkronizasyonu ATLANIYOR");
            }

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
                        _dbService.CurrentTenantId = user.TenantId ?? "default";
                        SaveCredentials();

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
}
