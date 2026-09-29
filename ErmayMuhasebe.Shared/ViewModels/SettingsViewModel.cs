using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ErmayMuhasebe.Services;
using ErmayMuhasebe.Repositories;
using System;
using System.Threading.Tasks;
using System.Collections.ObjectModel;

namespace ErmayMuhasebe.Shared.ViewModels;

public abstract partial class SettingsViewModel : ViewModelBase
{
    protected readonly IUnitOfWork _uow;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _cloudUrl = "";
    [ObservableProperty] private string _cloudSecret = "";
    [ObservableProperty] private string _cloudStatus = "Bekleniyor...";
    [ObservableProperty] private string _exchangeRateApiKey = "";
    [ObservableProperty] private string _positionStackApiKey = "";
    [ObservableProperty] private string _emailableApiKey = "";
    [ObservableProperty] private bool _isAutoScalingEnabled = true;
    
    // Logo görünürlük ayarları
    [ObservableProperty] private bool _logoFatura = true;
    [ObservableProperty] private bool _logoSiparis = true;
    [ObservableProperty] private bool _logoTeklif = true;
    [ObservableProperty] private bool _logoEkstre = true;
    [ObservableProperty] private bool _logoRaporlar = true;
    [ObservableProperty] private bool _logoTahsilat = true;
    [ObservableProperty] private bool _logoOdeme = true;
    [ObservableProperty] private bool _logoAcilisBakiye = true;

    // PDF Boyut ve Yönlendirme Ayarları
    [ObservableProperty] private string _faturaSize = "A4";
    [ObservableProperty] private string _faturaOrientation = "Dikey";
    [ObservableProperty] private string _siparisSize = "A5";
    [ObservableProperty] private string _siparisOrientation = "Dikey";
    [ObservableProperty] private string _teklifSize = "A4";
    [ObservableProperty] private string _teklifOrientation = "Dikey";
    [ObservableProperty] private string _ekstreSize = "A4";
    [ObservableProperty] private string _ekstreOrientation = "Dikey";
    [ObservableProperty] private string _raporSize = "A4";
    [ObservableProperty] private string _raporOrientation = "Dikey";
    [ObservableProperty] private string _tahsilatSize = "A5";
    [ObservableProperty] private string _tahsilatOrientation = "Dikey";
    [ObservableProperty] private string _odemeSize = "A5";
    [ObservableProperty] private string _odemeOrientation = "Dikey";
    [ObservableProperty] private string _acilisBakiyeSize = "A5";
    [ObservableProperty] private string _acilisBakiyeOrientation = "Dikey";

    // Yeni Özellikler (Test Checklist İçin)
    [ObservableProperty] private string _appVersion = "1.2.5-Stable";
    [ObservableProperty] private string _databasePath = "";
    [ObservableProperty] private string _lastBackupDate = "-";
    [ObservableProperty] private bool _isOfflineMode; // Checklist: İnternetsiz çalışma
    [ObservableProperty] private bool _isMaintenanceMode; // Checklist: Bakım modu
    [ObservableProperty] private ObservableCollection<string> _printerList = new();
    [ObservableProperty] private string _selectedPrinter = "Varsayılan";

    public ObservableCollection<string> SizeList { get; } = new() { "A3", "A4", "A5", "A6", "B5" };
    public ObservableCollection<string> OrientationList { get; } = new() { "Dikey", "Yatay" };
    public ObservableCollection<string> ShortcutKeysList { get; } = new() { "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10" };
    
    // Şifre ve Güvenlik
    [ObservableProperty] private string _oldPassword = "";
    [ObservableProperty] private string _newPassword = "";
    [ObservableProperty] private string _resetPassword = ""; // Fabrika ayarları için şifre sorma
    [ObservableProperty] private string _adminFactoryResetPassword = ""; // Admin tarafından belirlenen şifre
    
    // Telegram Bot Ayarları
    [ObservableProperty] private string _telegramBotToken = "";
    [ObservableProperty] private string _telegramChatId = "";
    
    // Fabrika Ayarları Şifre Sıfırlama Doğrulama
    [ObservableProperty] private bool _showFactoryResetVerificationPanel;
    [ObservableProperty] private string _factoryResetVerificationCode = "";
    [ObservableProperty] private bool _isFactoryResetCodeSent;
    
    // Kullanıcı Şifre Değiştirme Doğrulama
    [ObservableProperty] private bool _showUserPasswordVerificationPanel;
    [ObservableProperty] private string _userPasswordVerificationCode = "";
    [ObservableProperty] private bool _isUserPasswordCodeSent;
    [ObservableProperty] private string _pendingPasswordUsername = ""; // Şifresi değiştirilecek kullanıcı adı
    
    [ObservableProperty] private bool _lowPerformanceMode;
    [ObservableProperty] private bool _cloudBackupIntegration;
    
    [ObservableProperty] private bool _enableLowStockAlert = true;
    [ObservableProperty] private bool _enableOverdueAlert = true;
    [ObservableProperty] private string _customExcelTemplatePath = "";

    // SMTP Ayarları
    [ObservableProperty] private string _smtpHost = "";
    [ObservableProperty] private int _smtpPort = 587;
    [ObservableProperty] private string _smtpUser = "";
    [ObservableProperty] private string _smtpPass = "";
    [ObservableProperty] private bool _smtpSsl = true;

    // IMAP Ayarları
    [ObservableProperty] private string _imapHost = "";
    [ObservableProperty] private int _imapPort = 993;
    [ObservableProperty] private bool _imapSsl = true;

    // Gelişmiş Bildirim Özellikleri
    [ObservableProperty] private int _lowStockThreshold = 5;
    [ObservableProperty] private int _overdueDaysThreshold = 3;
    [ObservableProperty] private bool _enableBackupAlert = true;
    [ObservableProperty] private bool _enableCloudSyncAlert = true;
    [ObservableProperty] private bool _enableDailySummaryAlert = true;
    [ObservableProperty] private bool _enableRiskLimitAlert = true;
    [ObservableProperty] private bool _enableWindowsToastAlert = true;
    [ObservableProperty] private bool _enableChequeSenetAlert = true;
    [ObservableProperty] private bool _enableAgingDebtAlert = true;
    [ObservableProperty] private bool _enableNegativeStockAlert = true;
    [ObservableProperty] private bool _enableDeadStockAlert = true;
    
    [ObservableProperty] private bool _showAlertManager;
    [ObservableProperty] private bool _showSmtpSettings;
    [ObservableProperty] private bool _showExcelTemplates;
    [ObservableProperty] private bool _showRecycleBin;
    [ObservableProperty] private bool _showCronManager;
    [ObservableProperty] private bool _showShortcutsManager;
    
    [ObservableProperty] private ObservableCollection<string> _deletedItems = new();
    
    [ObservableProperty] private string _shortcutNewInvoice = "F2";
    [ObservableProperty] private string _shortcutNewCari = "F3";
    [ObservableProperty] private string _shortcutNewStok = "F4";
    [ObservableProperty] private string _shortcutOpenRaporlar = "F10";
    [ObservableProperty] private string _shortcutOpenFaturalar = "F6";
    [ObservableProperty] private string _shortcutOpenCariler = "F7";
    [ObservableProperty] private string _backupFolderPath = "";
    
    // Destek ve Hata Bildirimi
    [ObservableProperty] private string _supportSubject = "";
    [ObservableProperty] private string _supportMessage = "";
    
    // Kullanıcı Ekleme
    [ObservableProperty] private string _newUsername = "";
    [ObservableProperty] private string _newUserPassword = "";
    [ObservableProperty] private string _newUserRole = "Operatör";
    [ObservableProperty] private string _newUserEmail = "";
    [ObservableProperty] private string _newUserTelegramChatId = "";

    [ObservableProperty] private ObservableCollection<Models.User> _usersList = new();
    [ObservableProperty] private bool _isAdmin;

    partial void OnLogoFaturaChanged(bool value) => OnLogoFaturaChangedSideEffect(value);
    protected virtual void OnLogoFaturaChangedSideEffect(bool value) { }

    partial void OnLogoSiparisChanged(bool value) => OnLogoSiparisChangedSideEffect(value);
    protected virtual void OnLogoSiparisChangedSideEffect(bool value) { }

    partial void OnLogoTeklifChanged(bool value) => OnLogoTeklifChangedSideEffect(value);
    protected virtual void OnLogoTeklifChangedSideEffect(bool value) { }

    partial void OnLogoEkstreChanged(bool value) => OnLogoEkstreChangedSideEffect(value);
    protected virtual void OnLogoEkstreChangedSideEffect(bool value) { }

    partial void OnLogoRaporlarChanged(bool value) => OnLogoRaporlarChangedSideEffect(value);
    protected virtual void OnLogoRaporlarChangedSideEffect(bool value) { }

    partial void OnLogoTahsilatChanged(bool value) => OnLogoTahsilatChangedSideEffect(value);
    protected virtual void OnLogoTahsilatChangedSideEffect(bool value) { }

    partial void OnLogoOdemeChanged(bool value) => OnLogoOdemeChangedSideEffect(value);
    protected virtual void OnLogoOdemeChangedSideEffect(bool value) { }
    
    partial void OnLogoAcilisBakiyeChanged(bool value) => OnLogoAcilisBakiyeChangedSideEffect(value);
    protected virtual void OnLogoAcilisBakiyeChangedSideEffect(bool value) { }

    // Page Settings Hooks
    partial void OnFaturaSizeChanged(string value) => OnFaturaSizeChangedSideEffect(value);
    protected virtual void OnFaturaSizeChangedSideEffect(string value) { }
    partial void OnFaturaOrientationChanged(string value) => OnFaturaOrientationChangedSideEffect(value);
    protected virtual void OnFaturaOrientationChangedSideEffect(string value) { }
    
    partial void OnSiparisSizeChanged(string value) => OnSiparisSizeChangedSideEffect(value);
    protected virtual void OnSiparisSizeChangedSideEffect(string value) { }
    partial void OnSiparisOrientationChanged(string value) => OnSiparisOrientationChangedSideEffect(value);
    protected virtual void OnSiparisOrientationChangedSideEffect(string value) { }

    partial void OnTeklifSizeChanged(string value) => OnTeklifSizeChangedSideEffect(value);
    protected virtual void OnTeklifSizeChangedSideEffect(string value) { }
    partial void OnTeklifOrientationChanged(string value) => OnTeklifOrientationChangedSideEffect(value);
    protected virtual void OnTeklifOrientationChangedSideEffect(string value) { }

    partial void OnEkstreSizeChanged(string value) => OnEkstreSizeChangedSideEffect(value);
    protected virtual void OnEkstreSizeChangedSideEffect(string value) { }
    partial void OnEkstreOrientationChanged(string value) => OnEkstreOrientationChangedSideEffect(value);
    protected virtual void OnEkstreOrientationChangedSideEffect(string value) { }

    partial void OnRaporSizeChanged(string value) => OnRaporSizeChangedSideEffect(value);
    protected virtual void OnRaporSizeChangedSideEffect(string value) { }
    partial void OnRaporOrientationChanged(string value) => OnRaporOrientationChangedSideEffect(value);
    protected virtual void OnRaporOrientationChangedSideEffect(string value) { }

    partial void OnTahsilatSizeChanged(string value) => OnTahsilatSizeChangedSideEffect(value);
    protected virtual void OnTahsilatSizeChangedSideEffect(string value) { }
    partial void OnTahsilatOrientationChanged(string value) => OnTahsilatOrientationChangedSideEffect(value);
    protected virtual void OnTahsilatOrientationChangedSideEffect(string value) { }

    partial void OnOdemeSizeChanged(string value) => OnOdemeSizeChangedSideEffect(value);
    protected virtual void OnOdemeSizeChangedSideEffect(string value) { }
    partial void OnOdemeOrientationChanged(string value) => OnOdemeOrientationChangedSideEffect(value);
    protected virtual void OnOdemeOrientationChangedSideEffect(string value) { }
    
    partial void OnAcilisBakiyeSizeChanged(string value) => OnAcilisBakiyeSizeChangedSideEffect(value);
    protected virtual void OnAcilisBakiyeSizeChangedSideEffect(string value) { }
    partial void OnAcilisBakiyeOrientationChanged(string value) => OnAcilisBakiyeOrientationChangedSideEffect(value);
    protected virtual void OnAcilisBakiyeOrientationChangedSideEffect(string value) { }

    protected readonly ExternalApiService _externalApi;

    public SettingsViewModel(IUnitOfWork uow, ExternalApiService externalApi)
    {
        _uow = uow;
        _externalApi = externalApi;
        
        ExchangeRateApiKey = _externalApi.ExchangeRateApiKey;
        PositionStackApiKey = _externalApi.PositionStackApiKey;
        EmailableApiKey = _externalApi.EmailableApiKey;
    }

    [RelayCommand]
    public async Task SaveLogoSettingsAsync()
    {
        try 
        {
            var profil = await _uow.GetFirmaProfiliAsync();
            profil.LogoFatura = LogoFatura;
            profil.LogoSiparis = LogoSiparis;
            profil.LogoTeklif = LogoTeklif;
            profil.LogoEkstre = LogoEkstre;
            profil.LogoRaporlar = LogoRaporlar;
            profil.LogoTahsilat = LogoTahsilat;
            profil.LogoOdeme = LogoOdeme;
            profil.LogoAcilisBakiye = LogoAcilisBakiye;
            
            await _uow.SaveFirmaProfiliAsync(profil);
            SuccessMessage = "Logo görünürlük ayarları kaydedildi.";
        }
        catch(Exception ex)
        {
            ErrorMessage = $"Logo ayarları kaydedilemedi: {ex.Message}";
        }
    }

    [RelayCommand]
    public virtual async Task SaveAppearanceSettingsAsync()
    {
        try 
        {
            var profil = await _uow.GetFirmaProfiliAsync();
            profil.FaturaSize = FaturaSize;
            profil.FaturaOrientation = FaturaOrientation;
            profil.SiparisSize = SiparisSize;
            profil.SiparisOrientation = SiparisOrientation;
            profil.TeklifSize = TeklifSize;
            profil.TeklifOrientation = TeklifOrientation;
            profil.EkstreSize = EkstreSize;
            profil.EkstreOrientation = EkstreOrientation;
            profil.RaporSize = RaporSize;
            profil.RaporOrientation = RaporOrientation;
            profil.TahsilatSize = TahsilatSize;
            profil.TahsilatOrientation = TahsilatOrientation;
            profil.OdemeSize = OdemeSize;
            profil.OdemeOrientation = OdemeOrientation;
            profil.LogoFatura = LogoFatura;
            profil.LogoSiparis = LogoSiparis;
            profil.LogoTeklif = LogoTeklif;
            profil.LogoEkstre = LogoEkstre;
            profil.LogoRaporlar = LogoRaporlar;
            profil.LogoTahsilat = LogoTahsilat;
            profil.LogoOdeme = LogoOdeme;
            profil.LogoAcilisBakiye = LogoAcilisBakiye;

            await _uow.SaveFirmaProfiliAsync(profil);
            SuccessMessage = "Görünüm ve logo ayarları başarıyla kaydedildi.";
        }
        catch(Exception ex)
        {
            ErrorMessage = $"Ayarlar kaydedilemedi: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task SaveAdminFactoryResetPasswordAsync()
    {
        try 
        {
            if (string.IsNullOrWhiteSpace(AdminFactoryResetPassword))
            {
                ErrorMessage = "Şifre boş olamaz.";
                return;
            }
            var profil = await _uow.GetFirmaProfiliAsync();
            profil.FactoryResetPassword = AdminFactoryResetPassword.Trim();
            await _uow.SaveFirmaProfiliAsync(profil);

            try
            {
                var configDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ErmayMuhasebe");
                var setupPath = System.IO.Path.Combine(configDir, "setup_initial_user.json");
                var permPath = System.IO.Path.Combine(configDir, "setup_config.json");
                foreach (var path in new[] { setupPath, permPath })
                {
                    if (System.IO.File.Exists(path))
                    {
                        var json = await System.IO.File.ReadAllTextAsync(path);
                        var node = System.Text.Json.Nodes.JsonNode.Parse(json);
                        if (node != null)
                        {
                            node["FactoryResetPassword"] = profil.FactoryResetPassword;
                            await System.IO.File.WriteAllTextAsync(path, node.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                        }
                    }
                }
            }
            catch { }

            SuccessMessage = "Fabrika ayarları sıfırlama onay şifresi başarıyla güncellendi.";
            ErrorMessage = "";
        }
        catch(Exception ex)
        {
            ErrorMessage = $"Şifre güncellenemedi: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task SaveTelegramSettingsAsync()
    {
        try
        {
            var profil = await _uow.GetFirmaProfiliAsync();
            profil.TelegramBotToken = TelegramBotToken;
            profil.TelegramChatId = TelegramChatId;
            await _uow.SaveFirmaProfiliAsync(profil);
            SuccessMessage = "Telegram Bot ayarları kaydedildi.";
            ErrorMessage = "";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Telegram ayarları kaydedilemedi: {ex.Message}";
        }
    }

    [RelayCommand]
    public void ToggleFactoryResetVerificationPanel()
    {
        ShowFactoryResetVerificationPanel = !ShowFactoryResetVerificationPanel;
        if (ShowFactoryResetVerificationPanel)
        {
            FactoryResetVerificationCode = "";
            IsFactoryResetCodeSent = false;
            ErrorMessage = "";
            SuccessMessage = "";
        }
    }

    [RelayCommand]
    public void ToggleUserPasswordVerificationPanel()
    {
        ShowUserPasswordVerificationPanel = !ShowUserPasswordVerificationPanel;
        if (ShowUserPasswordVerificationPanel)
        {
            UserPasswordVerificationCode = "";
            IsUserPasswordCodeSent = false;
            PendingPasswordUsername = "";
            ErrorMessage = "";
            SuccessMessage = "";
        }
    }

    partial void OnLowPerformanceModeChanged(bool value)
    {
        _ = SaveAdvancedFlagsAsync();
    }
    
    partial void OnCloudBackupIntegrationChanged(bool value)
    {
        _ = SaveAdvancedFlagsAsync();
    }

    partial void OnEnableLowStockAlertChanged(bool value)
    {
        _ = SaveAdvancedFlagsAsync();
    }

    partial void OnEnableOverdueAlertChanged(bool value)
    {
        _ = SaveAdvancedFlagsAsync();
    }
    
    partial void OnEnableBackupAlertChanged(bool value)
    {
        _ = SaveAdvancedFlagsAsync();
    }

    partial void OnEnableCloudSyncAlertChanged(bool value)
    {
        _ = SaveAdvancedFlagsAsync();
    }

    partial void OnEnableDailySummaryAlertChanged(bool value)
    {
        _ = SaveAdvancedFlagsAsync();
    }

    partial void OnEnableRiskLimitAlertChanged(bool value)
    {
        _ = SaveAdvancedFlagsAsync();
    }

    partial void OnEnableWindowsToastAlertChanged(bool value)
    {
        _ = SaveAdvancedFlagsAsync();
    }

    partial void OnEnableChequeSenetAlertChanged(bool value)
    {
        _ = SaveAdvancedFlagsAsync();
    }

    partial void OnEnableAgingDebtAlertChanged(bool value)
    {
        _ = SaveAdvancedFlagsAsync();
    }

    partial void OnEnableNegativeStockAlertChanged(bool value)
    {
        _ = SaveAdvancedFlagsAsync();
    }

    partial void OnOverdueDaysThresholdChanged(int value)
    {
        _ = SaveAdvancedFlagsAsync();
    }
    
    partial void OnShortcutNewInvoiceChanged(string value)
    {
        _ = SaveAdvancedFlagsAsync();
    }
    
    partial void OnShortcutNewCariChanged(string value)
    {
        _ = SaveAdvancedFlagsAsync();
    }

    partial void OnShortcutNewStokChanged(string value)
    {
        _ = SaveAdvancedFlagsAsync();
    }

    partial void OnShortcutOpenRaporlarChanged(string value)
    {
        _ = SaveAdvancedFlagsAsync();
    }

    partial void OnShortcutOpenFaturalarChanged(string value)
    {
        _ = SaveAdvancedFlagsAsync();
    }

    partial void OnShortcutOpenCarilerChanged(string value)
    {
        _ = SaveAdvancedFlagsAsync();
    }

    partial void OnBackupFolderPathChanged(string value)
    {
        _ = SaveAdvancedFlagsAsync();
    }

    partial void OnSmtpHostChanged(string value)
    {
        _ = SaveAdvancedFlagsAsync();
    }

    partial void OnSmtpPortChanged(int value)
    {
        _ = SaveAdvancedFlagsAsync();
    }

    partial void OnSmtpUserChanged(string value)
    {
        _ = SaveAdvancedFlagsAsync();
    }

    partial void OnSmtpPassChanged(string value)
    {
        _ = SaveAdvancedFlagsAsync();
    }

    partial void OnSmtpSslChanged(bool value)
    {
        _ = SaveAdvancedFlagsAsync();
    }

    partial void OnImapHostChanged(string value)
    {
        _ = SaveAdvancedFlagsAsync();
    }

    partial void OnImapPortChanged(int value)
    {
        _ = SaveAdvancedFlagsAsync();
    }

    partial void OnImapSslChanged(bool value)
    {
        _ = SaveAdvancedFlagsAsync();
    }

    protected async Task SaveAdvancedFlagsAsync()
    {
        try 
        {
            var profil = await _uow.GetFirmaProfiliAsync();
            profil.LowPerformanceMode = LowPerformanceMode;
            profil.CloudBackupIntegration = CloudBackupIntegration;
            profil.EnableLowStockAlert = EnableLowStockAlert;
            profil.EnableOverdueAlert = EnableOverdueAlert;
            profil.CustomExcelTemplatePath = CustomExcelTemplatePath;
            profil.ShortcutNewInvoice = ShortcutNewInvoice;
            profil.ShortcutNewCari = ShortcutNewCari;
            profil.ShortcutNewStok = ShortcutNewStok;
            profil.ShortcutOpenRaporlar = ShortcutOpenRaporlar;
            profil.ShortcutOpenFaturalar = ShortcutOpenFaturalar;
            profil.ShortcutOpenCariler = ShortcutOpenCariler;
            profil.BackupFolderPath = BackupFolderPath;

            profil.SmtpHost = SmtpHost;
            profil.SmtpPort = SmtpPort;
            profil.SmtpUser = SmtpUser;
            profil.SmtpPass = SmtpPass;
            profil.SmtpSsl = SmtpSsl;

            profil.ImapHost = ImapHost;
            profil.ImapPort = ImapPort;
            profil.ImapSsl = ImapSsl;
            profil.SmtpUser = SmtpUser;
            profil.SmtpPass = SmtpPass;
            profil.SmtpSsl = SmtpSsl;

            // Gelişmiş Bildirim Ayarları
            profil.LowStockThreshold = LowStockThreshold;
            profil.OverdueDaysThreshold = OverdueDaysThreshold;
            profil.EnableBackupAlert = EnableBackupAlert;
            profil.EnableCloudSyncAlert = EnableCloudSyncAlert;
            profil.EnableDailySummaryAlert = EnableDailySummaryAlert;
            profil.EnableRiskLimitAlert = EnableRiskLimitAlert;
            profil.EnableWindowsToastAlert = EnableWindowsToastAlert;
            profil.EnableChequeSenetAlert = EnableChequeSenetAlert;
            profil.EnableAgingDebtAlert = EnableAgingDebtAlert;
            profil.EnableNegativeStockAlert = EnableNegativeStockAlert;
            profil.EnableDeadStockAlert = EnableDeadStockAlert;

            await _uow.SaveFirmaProfiliAsync(profil);
        }
        catch { }
    }

    [RelayCommand]
    public void SaveSettings()
    {
        try 
        {
            if (!string.IsNullOrWhiteSpace(CloudUrl))
                _uow.SetCloudConfig(CloudUrl, CloudSecret);

            _externalApi.ExchangeRateApiKey = ExchangeRateApiKey;
            _externalApi.PositionStackApiKey = PositionStackApiKey;
            _externalApi.EmailableApiKey = EmailableApiKey;
            
            // Ayrıca gelişmiş bayrakları da kaydet
            _ = SaveAdvancedFlagsAsync();

            CloudStatus = "Tüm ayarlar kaydedildi.";
            SuccessMessage = "Yapılandırma başarıyla güncellendi.";
        }
        catch(Exception ex)
        {
            CloudStatus = $"Hata: {ex.Message}";
            ErrorMessage = CloudStatus;
        }
    }

    [RelayCommand]
    public async Task SyncCloudAsync()
    {
        IsBusy = true;
        CloudStatus = "Senkronizasyon başlatılıyor...";
        try
        {
             await _uow.SyncToCloudAsync();
             CloudStatus = "Senkronizasyon Başarılı! (Otomatik Eşitleme Aktif)";
             SuccessMessage = "Veriler bulut ile senkronize edildi. Otomatik eşitleme başlatıldı.";
             _uow.EnableAutoSync(true);
        }
        catch (Exception ex)
        {
             CloudStatus = $"Senkronizasyon Hatası: {ex.Message}";
             ErrorMessage = CloudStatus;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public abstract Task CreateBackupAsync();
    public abstract Task RestoreBackupAsync();
    public abstract void ChangeTheme(string themeName);
    
    // Yeni Komut Taslakları
    public abstract Task UpdateUserInfoAsync(Models.User user);
    public abstract Task UpdateUserPasswordAsync(Models.User user);
    public abstract Task LoadUsersAsync();
    public abstract Task DeleteUserAsync(Models.User user);
    public abstract Task ChangePasswordAsync();
    public abstract Task AddUserAsync();
    public abstract Task FactoryResetAsync();
    public abstract Task SendErrorReportAsync();
    public abstract Task ChangeDatabasePathAsync();
    
    // Gelişmiş Teşhis ve Optimizasyon Araçları (14 Özellik)
    public abstract Task RunTableAnalyzerAsync();
    public abstract Task RunDiagnosticAsync();
    public abstract Task OpenRecycleBinAsync();
    public abstract Task OpenAutoBackupAsync();
    public abstract Task OpenAlertManagerAsync();
    public abstract Task OpenCronManagerAsync();
    public abstract Task RunYearEndSimulatorAsync();
    public abstract Task RunDocNumberDiagnosticsAsync();
    public abstract Task RunNetworkDiagnosticAsync();
    public abstract Task RunSmtpDiagnosticAsync();
    public abstract Task OpenShortcutsManagerAsync();
    public abstract Task OpenExcelTemplatesAsync();
}

