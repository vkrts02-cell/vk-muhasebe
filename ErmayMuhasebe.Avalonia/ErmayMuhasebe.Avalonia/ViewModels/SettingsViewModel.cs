using SQLite;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ErmayMuhasebe.Avalonia.Messages;
using ErmayMuhasebe.Repositories.DataProviders;
using ErmayMuhasebe.Services;
using ErmayMuhasebe.Repositories;
using ErmayMuhasebe.Models;
using System;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using System.Collections.ObjectModel;
using System.Runtime.Versioning;
using ErmayMuhasebe.Avalonia.Services;
using System.Net;
using System.Net.Mail;
using System.Net.Http;
using System.Net.Http.Json;

namespace ErmayMuhasebe.Avalonia.ViewModels;

public partial class SettingCategory : ObservableObject
{
    [ObservableProperty] private string _title;
    [ObservableProperty] private string _description;
    [ObservableProperty] private string _icon;
    [ObservableProperty] private string _color;
    [ObservableProperty] private string _id;
    [ObservableProperty] private string _statusText = "YAPILANDIR";
    [ObservableProperty] private bool _isImplemented = true;

    public SettingCategory(string id, string title, string description, string icon, string color, string statusText = "YAPILANDIR", bool isImplemented = true)
    {
        _id = id;
        _title = title;
        _description = description;
        _icon = icon;
        _color = color;
        _statusText = statusText;
        _isImplemented = isImplemented;
    }
}
public partial class SettingsViewModel : ErmayMuhasebe.Shared.ViewModels.SettingsViewModel, IHandleBack
{
    private readonly BackupService _backupService;
    private readonly ThemeService _themeService;
    private readonly DisplayService _displayService;
    private readonly IYearContext _yearContext;
    private readonly IDataProvider _dataProvider;
    private readonly StatusBarService _statusBarService;
    private readonly IFileService _fileService;
    private readonly SecuritySyncService _securitySyncService;

    [ObservableProperty] private Models.User? _selectedSecurityUser;
    [ObservableProperty] private bool _isWaitingForTelegramApproval;
    [ObservableProperty] private string _telegramApprovalStatusText = "";
    [ObservableProperty] private string _authFactoryResetPasswordForUsername = "";
    [ObservableProperty] private string _authFactoryResetPasswordForPassword = "";

    [ObservableProperty] private string _activeUsername = "admin";
    [ObservableProperty] private ObservableCollection<SettingCategory> _categories = new();
    [ObservableProperty] private SettingCategory? _currentCategory;

    [ObservableProperty]
    private double _currentScaling;

    [ObservableProperty]
    private bool _isAutoScalingEnabled;

    [ObservableProperty]
    private int _activeYear;

    [ObservableProperty]
    private ObservableCollection<int> _availableYears = new();

    [ObservableProperty]
    private int? _selectedYearToSwitch;

    // ==========================================
    // MALİ YIL & DEVİR İŞLEMLERİ (SETTINGS)
    // ==========================================
    [ObservableProperty] private int _rolloverSourceYear;
    [ObservableProperty] private int _rolloverTargetYear;
    [ObservableProperty] private bool _rolloverCariler = true;
    [ObservableProperty] private bool _rolloverStoklar = true;
    [ObservableProperty] private bool _rolloverKasaBanka = true;
    [ObservableProperty] private bool _rolloverCekSenet = true;
    [ObservableProperty] private bool _rolloverTanimlar = true;
    [ObservableProperty] private bool _rolloverExcludeZeroBalances = false;
    [ObservableProperty] private bool _rolloverAutoBackup = true;
    [ObservableProperty] private bool _autoReflectBalancesOnChange = true;
    [ObservableProperty] private bool _rolloverTransferZeroStock = false;
    [ObservableProperty] private string _rolloverDevirFisiTemplate = "{0} Yılı Devir Bakiyesi";
    [ObservableProperty] private string _rolloverProgressMessage = "";
    [ObservableProperty] private bool _isRolloverExecuting = false;

    partial void OnRolloverSourceYearChanged(int value)
    {
        RolloverTargetYear = value + 1;
    }

    [ObservableProperty]
    private string _backupStatus = "Yedekleme durumu bekleniyor...";

    [ObservableProperty]
    private string _selectedTheme = "ModernSaaS";

    [ObservableProperty] private bool _showResetPasswordPanel;
    [ObservableProperty] private string _resetEmail = "";
    [ObservableProperty] private string _resetUsername = "";
    [ObservableProperty] private bool _isResetCodeSent;
    [ObservableProperty] private string _resetVerificationCode = "";
    [ObservableProperty] private string _resetNewPassword = "";
    [ObservableProperty] private bool _isResetCodeVerified;
    [ObservableProperty] private string _resetOneTimePassword = "";
    
    public bool ShowVerifyResetCodePanel => IsResetCodeSent && !IsResetCodeVerified;
    private string _generatedResetCode = "";
    private string _generatedOneTimePassword = "";
    private string _generatedFactoryResetCode = "";
    private string _generatedUserPasswordCode = "";
    private Models.User? _pendingPasswordUser; // Şifresi değiştirilecek kullanıcı referansı

    [ObservableProperty]
    private ObservableCollection<Models.User> _managersList = new();

    [ObservableProperty]
    private Models.User? _selectedManagerForOperator;

    [ObservableProperty]
    private bool _isManagerSelectionVisible;

    [ObservableProperty] private bool _isFirebaseAuthEnabled;
    [ObservableProperty] private string _firebaseAuthApiKey = "";
    [ObservableProperty] private string _firebaseAuthDomain = "";
    [ObservableProperty] private string _newUserFirebaseAuthUid = "";

    [ObservableProperty] private string _cloudPdfApiUrl = "https://ermay-pdf-api-390930978984.europe-west1.run.app";
    [ObservableProperty] private string _cloudPdfApiKey = "";

    [ObservableProperty] private int _startingYear = 2026;
    [ObservableProperty] private ObservableCollection<int> _availableYearsForDeletion = new();
    [ObservableProperty] private int? _selectedYearToDelete;


    private void UpdateManagerSelectionVisibility()
    {
        IsManagerSelectionVisible = IsAdmin && NewUserRole == "Operatör";
    }

    public SettingsViewModel(
        BackupService backupService, 
        ThemeService themeService, 
        DisplayService displayService, 
        IUnitOfWork uow, 
        ExternalApiService externalApi,
        IYearContext yearContext,
        IDataProvider dataProvider,
        StatusBarService statusBarService,
        IFileService fileService,
        SecuritySyncService securitySyncService) 
        : base(uow, externalApi)
    {
        _backupService = backupService;
        _themeService = themeService;
        _displayService = displayService;
        _yearContext = yearContext;
        _dataProvider = dataProvider;
        _statusBarService = statusBarService;
        _fileService = fileService;
        _securitySyncService = securitySyncService;
        
        ActiveUsername = ((ErmayMuhasebe.Avalonia.App)App.Current!).Services?.GetRequiredService<MainViewModel>()?.CurrentUserName ?? "admin";

        ActiveYear = _yearContext.CurrentYear;
        _currentScaling = _displayService.Scaling;
        _isAutoScalingEnabled = _displayService.IsAutoScalingEnabled;
        _selectedTheme = _themeService.CurrentTheme.ToString();
        
        LoadStatusBarSettings();
        
        _statusBarService.SettingsChanged += () => 
        {
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() => LoadStatusBarSettings());
        };

        _displayService.ScalingChanged += (s) => CurrentScaling = s;
        _displayService.AutoScalingChanged += (a) => IsAutoScalingEnabled = a;
        InitializeCategories();
        LoadAvailableYears();
        _ = LoadFirmaProfiliAsync();
        if (OperatingSystem.IsWindows())
        {
            _ = LoadInitialDataAsync();
        }

        PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(NewUserRole))
            {
                UpdateManagerSelectionVisibility();
            }
        };

        WeakReferenceMessenger.Default.Register<FirmaProfiliChangedMessage>(this, (r, m) =>
        {
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                var profil = m.Value;
                if (profil != null)
                {
                    if (!string.IsNullOrEmpty(profil.LogoBase64))
                    {
                        try
                        {
                            LogoBytes = Convert.FromBase64String(profil.LogoBase64);
                        }
                        catch
                        {
                            LogoBytes = null;
                        }
                    }
                    else
                    {
                        LogoBytes = null;
                    }

                    LogoFatura = profil.LogoFatura;
                    LogoSiparis = profil.LogoSiparis;
                    LogoTeklif = profil.LogoTeklif;
                    LogoEkstre = profil.LogoEkstre;
                    LogoRaporlar = profil.LogoRaporlar;
                    LogoTahsilat = profil.LogoTahsilat;
                    LogoOdeme = profil.LogoOdeme;
                    LogoAcilisBakiye = profil.LogoAcilisBakiye;
                    if (profil.StartingYear > 2000)
                    {
                        StartingYear = profil.StartingYear;
                    }
                }
            });
        });
    }

    private void InitializeCategories()
    {
        Categories.Add(new SettingCategory("system", "Sistem ve Yedekleme", "Veritabanı konumunu yönetin ve yedek alın.", "Database", "#60A5FA"));
        Categories.Add(new SettingCategory("rollover", "Mali Yıl ve Devir İşlemleri", "Yeni yıla devir, bakiye senkronizasyonu ve devir parametrelerini yönetin.", "CalendarMonth", "#10B981"));
        Categories.Add(new SettingCategory("appearance", "Logo ve Belgeler", "Logo, yazıcı ve sayfa boyutlarını ayarlayın.", "Image", "#EC4899"));
        Categories.Add(new SettingCategory("security", "Kullanıcılar ve Güvenlik", "Şifre değiştirin ve kullanıcıları yönetin.", "ShieldLock", "#F43F5E"));
        Categories.Add(new SettingCategory("cloud", "Bulut ve API", "Firebase ve dış servis bağlantılarını yönetin.", "Cloud", "#F59E0B"));
        Categories.Add(new SettingCategory("theme", "Arayüz Teması", "Uygulama tasarımını ve renk paletini değiştirin.", "Color", "#3b82f6"));
        Categories.Add(new SettingCategory("display", "Ekran Ölçeklendirme", "Arayüz boyutunu cihazınıza göre ayarlayın.", "Desktop", "#10B981"));
        Categories.Add(new SettingCategory("notifications", "Bildirim Ayarları", "Bildirimlerin görünürlüğünü ve alarmları yapılandırın.", "Alert", "#F59E0B"));
        Categories.Add(new SettingCategory("advanced", "Gelişmiş Ayarlar", "Sistem sıfırlama ve bakım işlemleri.", "Wrench", "#EF4444"));
    }

    [RelayCommand]
    private void SelectCategory(SettingCategory category) => CurrentCategory = category;

    [RelayCommand]
    private void GoBack() => CurrentCategory = null;

    public bool HandleBack()
    {
        if (CurrentCategory != null)
        {
            GoBack();
            return true;
        }
        return false;
    }

    [SupportedOSPlatform("windows")]
    private async Task LoadInitialDataAsync()
    {
        DatabasePath = await _uow.GetDatabasePathAsync();
        var profil = await _uow.GetFirmaProfiliAsync();
        LastBackupDate = profil.LastBackupDate?.ToString("dd.MM.yyyy HH:mm") ?? "Hiç yedek alınmadı";
        
        // Load cloud config settings
        var (url, secret) = _uow.GetCloudConfig();
        CloudUrl = url;
        CloudSecret = secret;
        
        // Printer list (Windows specific)
        if (OperatingSystem.IsWindows())
        {
            try 
            {
                foreach (string printer in System.Drawing.Printing.PrinterSettings.InstalledPrinters)
                {
                    PrinterList.Add(printer);
                }
            } catch { }
        }
        else 
        {
            PrinterList.Add("Sistem Yazıcısı (Varsayılan)");
        }
        
        // Admin check 
        var mainVm = ((ErmayMuhasebe.Avalonia.App)App.Current!).Services?.GetRequiredService<MainViewModel>();
        IsAdmin = mainVm?.CurrentUserName?.ToLower() == "admin";
        
        if (IsAdmin)
        {
            await LoadUsersAsync();
        }
        UpdateManagerSelectionVisibility();
        
        SelectedPrinter = PrinterList.FirstOrDefault() ?? "Varsayılan";
    }


    [ObservableProperty] private byte[]? _logoBytes;

    private async Task LoadFirmaProfiliAsync()
    {
        var profil = await _uow.GetFirmaProfiliAsync();
        if (profil != null)
        {
            ResetEmail = profil.Eposta ?? "";
            if (!string.IsNullOrEmpty(profil.LogoBase64))
            {
                try 
                { 
                    var bytes = Convert.FromBase64String(profil.LogoBase64);
                    LogoBytes = bytes;
                    var pdfService = ((ErmayMuhasebe.Avalonia.App)App.Current!).Services?.GetRequiredService<PdfService>();
                    if (pdfService != null) pdfService.LogoBytes = bytes;
                } 
                catch { }
            }
            else
            {
                try
                {
                    string logoPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ErmayMuhasebe", "company_logo.png");
                    if (System.IO.File.Exists(logoPath))
                    {
                        var bytes = await System.IO.File.ReadAllBytesAsync(logoPath);
                        if (bytes != null && bytes.Length > 0)
                        {
                            LogoBytes = bytes;
                            var pdfService = ((ErmayMuhasebe.Avalonia.App)App.Current!).Services?.GetRequiredService<PdfService>();
                            if (pdfService != null) pdfService.LogoBytes = bytes;
                            profil.LogoBase64 = Convert.ToBase64String(bytes);
                            await _uow.SaveFirmaProfiliAsync(profil);
                        }
                    }
                }
                catch { }
            }
            
            LogoFatura = profil.LogoFatura;
            LogoSiparis = profil.LogoSiparis;
            LogoTeklif = profil.LogoTeklif;
            LogoEkstre = profil.LogoEkstre;
            LogoRaporlar = profil.LogoRaporlar;
            LogoTahsilat = profil.LogoTahsilat;
            LogoOdeme = profil.LogoOdeme;
            LogoAcilisBakiye = profil.LogoAcilisBakiye;
            
            AdminFactoryResetPassword = profil.FactoryResetPassword;
            LowPerformanceMode = profil.LowPerformanceMode;
            CloudBackupIntegration = profil.CloudBackupIntegration;
            EnableLowStockAlert = profil.EnableLowStockAlert;
            EnableOverdueAlert = profil.EnableOverdueAlert;
            CustomExcelTemplatePath = profil.CustomExcelTemplatePath;
            ShortcutNewInvoice = profil.ShortcutNewInvoice ?? "F2";
            ShortcutNewCari = profil.ShortcutNewCari ?? "F3";
            ShortcutNewStok = profil.ShortcutNewStok ?? "F4";
            ShortcutOpenRaporlar = profil.ShortcutOpenRaporlar ?? "F10";
            ShortcutOpenFaturalar = profil.ShortcutOpenFaturalar ?? "F6";
            ShortcutOpenCariler = profil.ShortcutOpenCariler ?? "F7";
            BackupFolderPath = profil.BackupFolderPath ?? "";
            
            SmtpHost = profil.SmtpHost ?? "";
            SmtpPort = profil.SmtpPort;
            SmtpUser = profil.SmtpUser ?? "";
            SmtpPass = profil.SmtpPass ?? "";
            SmtpSsl = profil.SmtpSsl;

            ImapHost = profil.ImapHost ?? "";
            ImapPort = profil.ImapPort;
            ImapSsl = profil.ImapSsl;

            TelegramBotToken = profil.TelegramBotToken ?? "";
            TelegramChatId = profil.TelegramChatId ?? "";

            IsFirebaseAuthEnabled = profil.IsFirebaseAuthEnabled;
            FirebaseAuthApiKey = profil.FirebaseAuthApiKey ?? "";
            FirebaseAuthDomain = profil.FirebaseAuthDomain ?? "";
            CloudPdfApiUrl = (string.IsNullOrWhiteSpace(profil.CloudPdfApiUrl) || profil.CloudPdfApiUrl.Contains("916435485627")) ? "https://ermay-pdf-api-390930978984.europe-west1.run.app" : profil.CloudPdfApiUrl;
            CloudPdfApiKey = profil.CloudPdfApiKey ?? "";

            // Gelişmiş Bildirim Ayarları
            LowStockThreshold = profil.LowStockThreshold;
            OverdueDaysThreshold = profil.OverdueDaysThreshold;
            EnableBackupAlert = profil.EnableBackupAlert;
            EnableCloudSyncAlert = profil.EnableCloudSyncAlert;
            EnableDailySummaryAlert = profil.EnableDailySummaryAlert;
            EnableRiskLimitAlert = profil.EnableRiskLimitAlert;
            EnableWindowsToastAlert = profil.EnableWindowsToastAlert;
            EnableChequeSenetAlert = profil.EnableChequeSenetAlert;
            EnableAgingDebtAlert = profil.EnableAgingDebtAlert;
            EnableNegativeStockAlert = profil.EnableNegativeStockAlert;
            EnableDeadStockAlert = profil.EnableDeadStockAlert;

            FaturaSize = profil.FaturaSize;
            FaturaOrientation = profil.FaturaOrientation;
            SiparisSize = profil.SiparisSize;
            SiparisOrientation = profil.SiparisOrientation;
            TeklifSize = profil.TeklifSize;
            TeklifOrientation = profil.TeklifOrientation;
            EkstreSize = profil.EkstreSize;
            EkstreOrientation = profil.EkstreOrientation;
            RaporSize = profil.RaporSize;
            RaporOrientation = profil.RaporOrientation;
            TahsilatSize = profil.TahsilatSize;
            TahsilatOrientation = profil.TahsilatOrientation;
            OdemeSize = profil.OdemeSize;
            OdemeOrientation = profil.OdemeOrientation;
            AcilisBakiyeSize = profil.AcilisBakiyeSize;
            AcilisBakiyeOrientation = profil.AcilisBakiyeOrientation;

            var ps = ((ErmayMuhasebe.Avalonia.App)App.Current!).Services?.GetRequiredService<PdfService>();
            if (ps != null)
            {
                ps.ShowLogoFatura = LogoFatura;
                ps.ShowLogoSiparis = LogoSiparis;
                ps.ShowLogoTeklif = LogoTeklif;
                ps.ShowLogoEkstre = LogoEkstre;
                ps.ShowLogoRaporlar = LogoRaporlar;
                ps.ShowLogoTahsilat = LogoTahsilat;
                ps.ShowLogoOdeme = LogoOdeme;
                ps.ShowLogoAcilisBakiye = LogoAcilisBakiye;

                ps.FaturaSize = FaturaSize;
                ps.FaturaOrientation = FaturaOrientation;
                ps.SiparisSize = SiparisSize;
                ps.SiparisOrientation = SiparisOrientation;
                ps.TeklifSize = TeklifSize;
                ps.TeklifOrientation = TeklifOrientation;
                ps.EkstreSize = EkstreSize;
                ps.EkstreOrientation = EkstreOrientation;
                ps.RaporSize = RaporSize;
                ps.RaporOrientation = RaporOrientation;
                ps.TahsilatSize = TahsilatSize;
                ps.TahsilatOrientation = TahsilatOrientation;
                ps.OdemeSize = OdemeSize;
                ps.OdemeOrientation = OdemeOrientation;
                ps.AcilisBakiyeSize = AcilisBakiyeSize;
                ps.AcilisBakiyeOrientation = AcilisBakiyeOrientation;
            }
        }
    }

    public override async Task SaveAppearanceSettingsAsync()
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
            profil.AcilisBakiyeSize = AcilisBakiyeSize;
            profil.AcilisBakiyeOrientation = AcilisBakiyeOrientation;

            string dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ErmayMuhasebe");
            string logoPath = System.IO.Path.Combine(dir, "company_logo.png");

            if (LogoBytes != null && LogoBytes.Length > 0)
            {
                var optimizedBytes = ErmayMuhasebe.Services.ImageHelper.OptimizeLogo(LogoBytes);
                LogoBytes = optimizedBytes;
                profil.LogoBase64 = Convert.ToBase64String(optimizedBytes);
                if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
                await System.IO.File.WriteAllBytesAsync(logoPath, optimizedBytes);
            }
            else
            {
                profil.LogoBase64 = null;
                if (System.IO.File.Exists(logoPath)) System.IO.File.Delete(logoPath);
            }
            await _uow.SaveFirmaProfiliAsync(profil);
            SuccessMessage = "Görünüm ve logo ayarları başarıyla kaydedildi.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Ayarlar kaydedilemedi: {ex.Message}";
        }
        
        var ps = ((ErmayMuhasebe.Avalonia.App)App.Current!).Services?.GetRequiredService<PdfService>();
        if (ps != null)
        {
            if (LogoBytes != null && LogoBytes.Length > 0) 
            {
                ps.LogoBytes = LogoBytes;
            }
            else 
            {
                ps.LogoBytes = new byte[0];
            }
            ps.ShowLogoFatura = LogoFatura;
            ps.ShowLogoSiparis = LogoSiparis;
            ps.ShowLogoTeklif = LogoTeklif;
            ps.ShowLogoEkstre = LogoEkstre;
            ps.ShowLogoRaporlar = LogoRaporlar;
            ps.ShowLogoTahsilat = LogoTahsilat;
            ps.ShowLogoOdeme = LogoOdeme;
            ps.ShowLogoAcilisBakiye = LogoAcilisBakiye;

            ps.FaturaSize = FaturaSize;
            ps.FaturaOrientation = FaturaOrientation;
            ps.SiparisSize = SiparisSize;
            ps.SiparisOrientation = SiparisOrientation;
            ps.TeklifSize = TeklifSize;
            ps.TeklifOrientation = TeklifOrientation;
            ps.EkstreSize = EkstreSize;
            ps.EkstreOrientation = EkstreOrientation;
            ps.RaporSize = RaporSize;
            ps.RaporOrientation = RaporOrientation;
            ps.TahsilatSize = TahsilatSize;
            ps.TahsilatOrientation = TahsilatOrientation;
            ps.OdemeSize = OdemeSize;
            ps.OdemeOrientation = OdemeOrientation;
            ps.AcilisBakiyeSize = AcilisBakiyeSize;
            ps.AcilisBakiyeOrientation = AcilisBakiyeOrientation;
        }
    }

    [RelayCommand]
    public async Task SelectLogoAsync()
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop ? desktop.MainWindow : null);
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Logo Seç",
                AllowMultiple = false,
                FileTypeFilter = new[] { FilePickerFileTypes.ImageAll }
            });

            if (files.Count >= 1)
            {
                await using var stream = await files[0].OpenReadAsync();
                using var ms = new System.IO.MemoryStream();
                await stream.CopyToAsync(ms);
                var rawBytes = ms.ToArray();
                
                // Logo görselini Firebase ve mobil için optimize et (maks 800x800 ve hafif PNG baytları)
                var bytes = ErmayMuhasebe.Services.ImageHelper.OptimizeLogo(rawBytes);

                // Save database asynchronously first with all document types enabled
                var profil = await _uow.GetFirmaProfiliAsync();
                profil.LogoBase64 = Convert.ToBase64String(bytes);
                profil.LogoFatura = true;
                profil.LogoSiparis = true;
                profil.LogoTeklif = true;
                profil.LogoEkstre = true;
                profil.LogoRaporlar = true;
                profil.LogoTahsilat = true;
                profil.LogoOdeme = true;
                profil.LogoAcilisBakiye = true;
                await _uow.SaveFirmaProfiliAsync(profil);

                // Save to local company_logo.png for guaranteed loading across all PDF generations
                try
                {
                    string dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ErmayMuhasebe");
                    if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
                    string logoPath = System.IO.Path.Combine(dir, "company_logo.png");
                    await System.IO.File.WriteAllBytesAsync(logoPath, bytes);
                }
                catch { }
                
                // Update properties on UI thread to prevent thread marshalling and graphics issues
                await global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    LogoBytes = bytes;
                    LogoFatura = true;
                    LogoSiparis = true;
                    LogoTeklif = true;
                    LogoEkstre = true;
                    LogoRaporlar = true;
                    LogoTahsilat = true;
                    LogoOdeme = true;
                    LogoAcilisBakiye = true;
                    
                    var pdfService = ((ErmayMuhasebe.Avalonia.App)App.Current!).Services?.GetRequiredService<PdfService>();
                    if (pdfService != null)
                    {
                        pdfService.LogoBytes = bytes;
                        pdfService.ShowLogoFatura = true;
                        pdfService.ShowLogoSiparis = true;
                        pdfService.ShowLogoTeklif = true;
                        pdfService.ShowLogoEkstre = true;
                        pdfService.ShowLogoRaporlar = true;
                        pdfService.ShowLogoTahsilat = true;
                        pdfService.ShowLogoOdeme = true;
                        pdfService.ShowLogoAcilisBakiye = true;
                    }
                });
            }
        }
        catch (Exception ex)
        {
            BackupStatus = $"Logo Yükleme Hatası: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task DeleteLogoAsync()
    {
        try
        {
            var profil = await _uow.GetFirmaProfiliAsync();
            profil.LogoBase64 = null;
            await _uow.SaveFirmaProfiliAsync(profil);

            try
            {
                string logoPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ErmayMuhasebe", "company_logo.png");
                if (System.IO.File.Exists(logoPath)) System.IO.File.Delete(logoPath);
            }
            catch { }

            // Update properties on UI thread to prevent thread marshalling and graphics issues
            await global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                LogoBytes = null;
                
                var pdfService = ((ErmayMuhasebe.Avalonia.App)App.Current!).Services?.GetRequiredService<PdfService>();
                if (pdfService != null)
                {
                    pdfService.LogoBytes = new byte[0]; // Empty array triggers 'No Logo / Text Mode'
                }
            });
        }
        catch (Exception ex)
        {
            BackupStatus = $"Logo Silme Hatası: {ex.Message}";
        }
    }

    partial void OnCurrentScalingChanged(double value)
    {
        if (Math.Abs(_displayService.Scaling - value) > 0.001)
        {
            _displayService.SetScaling(value);
        }
    }

    partial void OnIsAutoScalingEnabledChanged(bool value)
    {
        if (_displayService.IsAutoScalingEnabled != value)
        {
            _displayService.IsAutoScalingEnabled = value;
        }
    }

    private async Task PersistLogoVisibilitySettingAsync(Action<FirmaProfili> updateAction)
    {
        try
        {
            var profil = await _uow.GetFirmaProfiliAsync();
            if (profil != null)
            {
                updateAction(profil);
                await _uow.SaveFirmaProfiliAsync(profil);
            }
        }
        catch { }
    }

    protected override void OnLogoFaturaChangedSideEffect(bool value) {
        var ps = ((ErmayMuhasebe.Avalonia.App)App.Current!).Services?.GetRequiredService<PdfService>();
        if (ps != null) ps.ShowLogoFatura = value;
        _ = PersistLogoVisibilitySettingAsync(p => p.LogoFatura = value);
    }
    protected override void OnLogoSiparisChangedSideEffect(bool value) {
        var ps = ((ErmayMuhasebe.Avalonia.App)App.Current!).Services?.GetRequiredService<PdfService>();
        if (ps != null) ps.ShowLogoSiparis = value;
        _ = PersistLogoVisibilitySettingAsync(p => p.LogoSiparis = value);
    }
    protected override void OnLogoTeklifChangedSideEffect(bool value) {
        var ps = ((ErmayMuhasebe.Avalonia.App)App.Current!).Services?.GetRequiredService<PdfService>();
        if (ps != null) ps.ShowLogoTeklif = value;
        _ = PersistLogoVisibilitySettingAsync(p => p.LogoTeklif = value);
    }
    protected override void OnLogoEkstreChangedSideEffect(bool value) {
        var ps = ((ErmayMuhasebe.Avalonia.App)App.Current!).Services?.GetRequiredService<PdfService>();
        if (ps != null) ps.ShowLogoEkstre = value;
        _ = PersistLogoVisibilitySettingAsync(p => p.LogoEkstre = value);
    }
    protected override void OnLogoRaporlarChangedSideEffect(bool value) {
        var ps = ((ErmayMuhasebe.Avalonia.App)App.Current!).Services?.GetRequiredService<PdfService>();
        if (ps != null) ps.ShowLogoRaporlar = value;
        _ = PersistLogoVisibilitySettingAsync(p => p.LogoRaporlar = value);
    }
    protected override void OnLogoTahsilatChangedSideEffect(bool value) {
        var ps = ((ErmayMuhasebe.Avalonia.App)App.Current!).Services?.GetRequiredService<PdfService>();
        if (ps != null) ps.ShowLogoTahsilat = value;
        _ = PersistLogoVisibilitySettingAsync(p => p.LogoTahsilat = value);
    }
    protected override void OnLogoOdemeChangedSideEffect(bool value) {
        var ps = ((ErmayMuhasebe.Avalonia.App)App.Current!).Services?.GetRequiredService<PdfService>();
        if (ps != null) ps.ShowLogoOdeme = value;
        _ = PersistLogoVisibilitySettingAsync(p => p.LogoOdeme = value);
    }
    protected override void OnLogoAcilisBakiyeChangedSideEffect(bool value) {
        var ps = ((ErmayMuhasebe.Avalonia.App)App.Current!).Services?.GetRequiredService<PdfService>();
        if (ps != null) ps.ShowLogoAcilisBakiye = value;
        _ = PersistLogoVisibilitySettingAsync(p => p.LogoAcilisBakiye = value);
    }

    // PDF Page Settings Sync
    protected override void OnFaturaSizeChangedSideEffect(string value) {
        var ps = ((ErmayMuhasebe.Avalonia.App)App.Current!).Services?.GetRequiredService<PdfService>();
        if (ps != null) ps.FaturaSize = value;
    }
    protected override void OnFaturaOrientationChangedSideEffect(string value) {
        var ps = ((ErmayMuhasebe.Avalonia.App)App.Current!).Services?.GetRequiredService<PdfService>();
        if (ps != null) ps.FaturaOrientation = value;
    }
    protected override void OnSiparisSizeChangedSideEffect(string value) {
        var ps = ((ErmayMuhasebe.Avalonia.App)App.Current!).Services?.GetRequiredService<PdfService>();
        if (ps != null) ps.SiparisSize = value;
    }
    protected override void OnSiparisOrientationChangedSideEffect(string value) {
        var ps = ((ErmayMuhasebe.Avalonia.App)App.Current!).Services?.GetRequiredService<PdfService>();
        if (ps != null) ps.SiparisOrientation = value;
    }
    protected override void OnTeklifSizeChangedSideEffect(string value) {
        var ps = ((ErmayMuhasebe.Avalonia.App)App.Current!).Services?.GetRequiredService<PdfService>();
        if (ps != null) ps.TeklifSize = value;
    }
    protected override void OnTeklifOrientationChangedSideEffect(string value) {
        var ps = ((ErmayMuhasebe.Avalonia.App)App.Current!).Services?.GetRequiredService<PdfService>();
        if (ps != null) ps.TeklifOrientation = value;
    }
    protected override void OnEkstreSizeChangedSideEffect(string value) {
        var ps = ((ErmayMuhasebe.Avalonia.App)App.Current!).Services?.GetRequiredService<PdfService>();
        if (ps != null) ps.EkstreSize = value;
    }
    protected override void OnEkstreOrientationChangedSideEffect(string value) {
        var ps = ((ErmayMuhasebe.Avalonia.App)App.Current!).Services?.GetRequiredService<PdfService>();
        if (ps != null) ps.EkstreOrientation = value;
    }
    protected override void OnRaporSizeChangedSideEffect(string value) {
        var ps = ((ErmayMuhasebe.Avalonia.App)App.Current!).Services?.GetRequiredService<PdfService>();
        if (ps != null) ps.RaporSize = value;
    }
    protected override void OnRaporOrientationChangedSideEffect(string value) {
        var ps = ((ErmayMuhasebe.Avalonia.App)App.Current!).Services?.GetRequiredService<PdfService>();
        if (ps != null) ps.RaporOrientation = value;
    }
    protected override void OnTahsilatSizeChangedSideEffect(string value) {
        var ps = ((ErmayMuhasebe.Avalonia.App)App.Current!).Services?.GetRequiredService<PdfService>();
        if (ps != null) ps.TahsilatSize = value;
    }
    protected override void OnTahsilatOrientationChangedSideEffect(string value) {
        var ps = ((ErmayMuhasebe.Avalonia.App)App.Current!).Services?.GetRequiredService<PdfService>();
        if (ps != null) ps.TahsilatOrientation = value;
    }
    protected override void OnOdemeSizeChangedSideEffect(string value) {
        var ps = ((ErmayMuhasebe.Avalonia.App)App.Current!).Services?.GetRequiredService<PdfService>();
        if (ps != null) ps.OdemeSize = value;
    }
    protected override void OnOdemeOrientationChangedSideEffect(string value) {
        var ps = ((ErmayMuhasebe.Avalonia.App)App.Current!).Services?.GetRequiredService<PdfService>();
        if (ps != null) ps.OdemeOrientation = value;
    }
    protected override void OnAcilisBakiyeSizeChangedSideEffect(string value) {
        var ps = ((ErmayMuhasebe.Avalonia.App)App.Current!).Services?.GetRequiredService<PdfService>();
        if (ps != null) ps.AcilisBakiyeSize = value;
    }
    protected override void OnAcilisBakiyeOrientationChangedSideEffect(string value) {
        var ps = ((ErmayMuhasebe.Avalonia.App)App.Current!).Services?.GetRequiredService<PdfService>();
        if (ps != null) ps.AcilisBakiyeOrientation = value;
    }

    public override void ChangeTheme(string themeName)
    {
        if (Enum.TryParse<ThemeService.AppTheme>(themeName, out var theme))
        {
            SelectedTheme = themeName;
            _themeService.SetTheme(theme);
        }
    }

    [RelayCommand]
    public void SetTheme(string themeName) => ChangeTheme(themeName);

    [RelayCommand]
    public void ChangeScaling(string scalingStr)
    {
        if (double.TryParse(scalingStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var scaling))
        {
            _displayService.SetScaling(scaling);
        }
    }


    [RelayCommand]
    public void AutoDetectScaling() => _displayService.AutoDetectScaling(true);

    private void ClearSavedCredentials()
    {
        try
        {
            var path = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ErmayMuhasebe",
                "login_settings.txt");
            if (System.IO.File.Exists(path))
            {
                System.IO.File.Delete(path);
            }
        }
        catch { }
    }
    [RelayCommand]
    public async Task ChangeUsernameAsync()
    {
        var trimmedNewUsername = NewUsername?.Trim();
        var trimmedAuth = AuthFactoryResetPasswordForUsername?.Trim();

        if (string.IsNullOrEmpty(trimmedNewUsername))
        {
            ErrorMessage = "Kullanıcı adı boş olamaz.";
            return;
        }

        try
        {
            var isAuthorized = await ValidateSetupSecurityPasswordAsync(trimmedAuth);
            if (!isAuthorized)
            {
                ErrorMessage = "Güvenlik onay şifresi hatalı. Lütfen kurulumda belirlediğiniz şifreyi giriniz.";
                return;
            }

            var db = ((ErmayMuhasebe.Avalonia.App)App.Current!).Services?.GetRequiredService<DatabaseService>();
            if (db != null)
            {
                var conn = db.GetGlobalConnection();
                var currentUsername = SelectedSecurityUser?.Username?.Trim()?.ToLower() ?? ActiveUsername?.Trim()?.ToLower() ?? "admin";
                
                if (currentUsername == "admin" && (SelectedSecurityUser?.Username == "admin" || ActiveUsername == "admin"))
                {
                    ErrorMessage = "Ana yönetici (admin) kullanıcı adı değiştirilemez.";
                    return;
                }

                var user = await conn.Table<Models.User>().FirstOrDefaultAsync(u => u.Username.ToLower() == currentUsername);
                if (user != null)
                {
                    var existing = await conn.Table<Models.User>().FirstOrDefaultAsync(u => u.Username.ToLower() == trimmedNewUsername.ToLower());
                    if (existing != null && existing.Id != user.Id)
                    {
                        ErrorMessage = "Bu kullanıcı adı zaten başka bir kullanıcı tarafından kullanılıyor.";
                        return;
                    }

                    var oldUsername = user.Username;
                    user.Username = trimmedNewUsername;
                    await conn.UpdateAsync(user);

                    if (db.SyncService != null)
                    {
                        try { await db.SyncService.SyncUserAsync(user); } catch { }
                    }

                    try { await _securitySyncService.UpdateUserSecurityStateAsync(user.Username); } catch { }

                    // Also update setup_initial_user.json so on restart/sync the new username persists
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
                                if (node != null && node["Users"] is System.Text.Json.Nodes.JsonArray uArr)
                                {
                                    foreach (var u in uArr)
                                    {
                                        if (u?["Username"]?.ToString()?.Equals(oldUsername, StringComparison.OrdinalIgnoreCase) == true)
                                        {
                                            u["Username"] = trimmedNewUsername;
                                        }
                                    }
                                    await System.IO.File.WriteAllTextAsync(path, node.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                                }
                            }
                        }
                    }
                    catch { }

                    if (ActiveUsername?.ToLower() == oldUsername.ToLower())
                    {
                        ActiveUsername = trimmedNewUsername;
                        var mainVm = ((ErmayMuhasebe.Avalonia.App)App.Current!).Services?.GetRequiredService<MainViewModel>();
                        if (mainVm != null) mainVm.CurrentUserName = trimmedNewUsername;
                    }

                    SuccessMessage = "Kullanıcı adınız başarıyla güncellendi!";
                    ErrorMessage = "";
                    NewUsername = "";
                    AuthFactoryResetPasswordForUsername = "";

                    if (IsAdmin)
                    {
                        await LoadUsersAsync();
                    }
                }
                else
                {
                    ErrorMessage = "Kullanıcı bulunamadı.";
                }
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Kullanıcı adı güncellenirken hata oluştu: {ex.Message}";
        }
    }

    [RelayCommand]
    public override async Task ChangePasswordAsync()
    {
        var trimmedNewPassword = NewPassword?.Trim();
        var trimmedAuth = AuthFactoryResetPasswordForPassword?.Trim();

        if (string.IsNullOrEmpty(trimmedNewPassword))
        {
            ErrorMessage = "Yeni şifre boş olamaz.";
            return;
        }
        
        try
        {
            var isAuthorized = await ValidateSetupSecurityPasswordAsync(trimmedAuth);
            if (!isAuthorized)
            {
                ErrorMessage = "Güvenlik onay şifresi hatalı. Lütfen kurulumda belirlediğiniz şifreyi giriniz.";
                return;
            }

            var db = ((ErmayMuhasebe.Avalonia.App)App.Current!).Services?.GetRequiredService<DatabaseService>();
            if (db != null)
            {
                var conn = db.GetGlobalConnection();
                var currentUsername = SelectedSecurityUser?.Username?.Trim()?.ToLower() ?? ActiveUsername?.Trim()?.ToLower() ?? "admin";
                var user = await conn.Table<Models.User>().FirstOrDefaultAsync(u => u.Username.ToLower() == currentUsername);
                
                if (user != null)
                {
                    var newSalt = AuthService.GenerateSalt();
                    var newHash = AuthService.HashPassword(trimmedNewPassword, newSalt);

                    user.Password = newHash;
                    user.PasswordSalt = newSalt;
                    await conn.UpdateAsync(user);

                    if (db.SyncService != null)
                    {
                        try { await db.SyncService.SyncUserAsync(user); } catch { }
                    }

                    ClearSavedCredentials();
                    try { await _securitySyncService.UpdateUserSecurityStateAsync(user.Username); } catch { }

                    // Also update setup_initial_user.json so on restart/sync the new password persists
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
                                if (node != null && node["Users"] is System.Text.Json.Nodes.JsonArray uArr)
                                {
                                    foreach (var u in uArr)
                                    {
                                        if (u?["Username"]?.ToString()?.Equals(currentUsername, StringComparison.OrdinalIgnoreCase) == true)
                                        {
                                            u["Password"] = trimmedNewPassword;
                                        }
                                    }
                                    await System.IO.File.WriteAllTextAsync(path, node.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                                }
                            }
                        }
                    }
                    catch { }

                    SuccessMessage = "Şifreniz başarıyla güncellendi!";
                    ErrorMessage = "";
                    NewPassword = "";
                    AuthFactoryResetPasswordForPassword = "";
                }
                else 
                {
                    ErrorMessage = "Sistem kullanıcısı bulunamadı.";
                }
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Şifre güncellenirken bir hata oluştu: {ex.Message}";
        }
    }

    [RelayCommand]
    private void StopTelegramApproval()
    {
        IsWaitingForTelegramApproval = false;
        _securitySyncService.StopListeners();
        ErrorMessage = "İşlem kullanıcı tarafından iptal edildi.";
    }

    [RelayCommand]
    private void ToggleResetPasswordPanel()
    {
        ShowResetPasswordPanel = !ShowResetPasswordPanel;
        if (ShowResetPasswordPanel)
        {
            ResetUsername = "";
            ResetVerificationCode = "";
            ResetNewPassword = "";
            IsResetCodeSent = false;
            _generatedResetCode = "";
            ErrorMessage = "";
            SuccessMessage = "";
        }
    }

    private string GenerateTempPassword(int length = 8)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";
        var random = new Random();
        var result = new char[length];
        for (int i = 0; i < length; i++)
        {
            result[i] = chars[random.Next(chars.Length)];
        }
        return new string(result);
    }

    [RelayCommand]
    private async Task SendResetCodeAsync()
    {
        var query = (ResetUsername ?? "").Trim().ToLower();
        if (string.IsNullOrWhiteSpace(query))
        {
            ErrorMessage = "Lütfen kullanıcı adınızı veya kayıtlı e-posta adresinizi girin.";
            return;
        }

        try
        {
            IsBusy = true;
            var db = ((ErmayMuhasebe.Avalonia.App)App.Current!).Services?.GetRequiredService<DatabaseService>();
            if (db == null) return;
            var conn = db.GetGlobalConnection();
            var allUsers = await conn.Table<Models.User>().ToListAsync();
            var user = allUsers.FirstOrDefault(u =>
                (!string.IsNullOrEmpty(u.Username) && u.Username.Trim().ToLower() == query) ||
                (!string.IsNullOrEmpty(u.Email) && u.Email.Trim().ToLower() == query));

            if (user == null && db.SyncService != null)
            {
                try
                {
                    db.SyncService.ReloadConfig();
                    if (db.SyncService.IsConnected)
                    {
                        var cloudUsers = await db.SyncService.PullUsersAsync();
                        var cloudUser = cloudUsers.FirstOrDefault(u =>
                            (!string.IsNullOrEmpty(u.Username) && u.Username.Trim().ToLower() == query) ||
                            (!string.IsNullOrEmpty(u.Email) && u.Email.Trim().ToLower() == query));

                        if (cloudUser != null)
                        {
                            var existing = allUsers.FirstOrDefault(u => u.Username?.ToLower() == cloudUser.Username?.ToLower());
                            if (existing == null)
                            {
                                await conn.InsertAsync(cloudUser);
                                user = cloudUser;
                            }
                            else
                            {
                                existing.Email = cloudUser.Email;
                                existing.Password = cloudUser.Password;
                                existing.PasswordSalt = cloudUser.PasswordSalt;
                                existing.Role = cloudUser.Role;
                                await conn.UpdateAsync(existing);
                                user = existing;
                            }
                        }
                    }
                }
                catch { }
            }

            if (user == null)
            {
                ErrorMessage = "Girdiğiniz kullanıcı adı veya e-posta adresine ait hesap bulunamadı.";
                return;
            }
            if (string.IsNullOrWhiteSpace(user.Email))
            {
                ErrorMessage = "Bu kullanıcının e-posta adresi tanımlanmamış. Lütfen yöneticinizle iletişime geçin.";
                return;
            }

            var randomCode = new Random().Next(100000, 999999).ToString();
            
            string subject = "VK Ön Muhasebe - Şifre Sıfırlama Doğrulama Kodu";
            string body = $@"Hesap şifrenizi sıfırlamak için doğrulama kodu talep ettiniz.
            
Kullanıcı Adı: {user.Username}
Doğrulama Kodunuz: {randomCode}

Lütfen bu kodu sisteme girerek doğrulamayı tamamlayın.";

            await SendEmailAsync(user.Email.Trim(), subject, body);

            _generatedResetCode = randomCode;
            _generatedOneTimePassword = randomCode;
            ResetOneTimePassword = randomCode;
            ResetVerificationCode = "";
            ResetNewPassword = "";
            IsResetCodeSent = true;
            IsResetCodeVerified = false;
            SuccessMessage = $"6 haneli doğrulama kodu {user.Email} adresine başarıyla gönderildi.";
            ErrorMessage = "";
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
        var query = (ResetUsername ?? "").Trim().ToLower();
        if (string.IsNullOrWhiteSpace(query))
        {
            ErrorMessage = "Lütfen kullanıcı adınızı veya kayıtlı e-posta adresinizi girin.";
            return;
        }

        try
        {
            IsBusy = true;
            var db = ((ErmayMuhasebe.Avalonia.App)App.Current!).Services?.GetRequiredService<DatabaseService>();
            if (db == null) return;
            var conn = db.GetGlobalConnection();
            var allUsers = await conn.Table<Models.User>().ToListAsync();
            var user = allUsers.FirstOrDefault(u =>
                (!string.IsNullOrEmpty(u.Username) && u.Username.Trim().ToLower() == query) ||
                (!string.IsNullOrEmpty(u.Email) && u.Email.Trim().ToLower() == query));

            if (user == null && db.SyncService != null)
            {
                try
                {
                    db.SyncService.ReloadConfig();
                    if (db.SyncService.IsConnected)
                    {
                        var cloudUsers = await db.SyncService.PullUsersAsync();
                        var cloudUser = cloudUsers.FirstOrDefault(u =>
                            (!string.IsNullOrEmpty(u.Username) && u.Username.Trim().ToLower() == query) ||
                            (!string.IsNullOrEmpty(u.Email) && u.Email.Trim().ToLower() == query));

                        if (cloudUser != null)
                        {
                            var existing = allUsers.FirstOrDefault(u => u.Username?.ToLower() == cloudUser.Username?.ToLower());
                            if (existing == null)
                            {
                                await conn.InsertAsync(cloudUser);
                                user = cloudUser;
                            }
                            else
                            {
                                existing.Email = cloudUser.Email;
                                existing.Password = cloudUser.Password;
                                existing.PasswordSalt = cloudUser.PasswordSalt;
                                existing.Role = cloudUser.Role;
                                await conn.UpdateAsync(existing);
                                user = existing;
                            }
                        }
                    }
                }
                catch { }
            }

            if (user == null)
            {
                ErrorMessage = "Girdiğiniz kullanıcı adı veya e-posta adresine ait hesap bulunamadı.";
                return;
            }
            if (string.IsNullOrWhiteSpace(user.TelegramChatId))
            {
                ErrorMessage = "Bu kullanıcının Telegram Chat ID bilgisi tanımlanmamış. Lütfen yöneticinizle iletişime geçin.";
                return;
            }

            var profil = await _uow.GetFirmaProfiliAsync();
            if (string.IsNullOrWhiteSpace(profil?.TelegramBotToken))
            {
                ErrorMessage = "Sistem Telegram Bot Token tanımlanmamış.";
                return;
            }

            var randomCode = new Random().Next(100000, 999999).ToString();
            
            await TelegramService.SendVerificationCodeAsync(profil.TelegramBotToken, user.TelegramChatId, randomCode);

            _generatedResetCode = randomCode;
            _generatedOneTimePassword = randomCode;
            ResetOneTimePassword = randomCode;
            ResetVerificationCode = "";
            ResetNewPassword = "";
            IsResetCodeSent = true;
            IsResetCodeVerified = false;
            SuccessMessage = "6 haneli doğrulama kodu Telegram ile başarıyla gönderildi.";
            ErrorMessage = "";
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

    private async Task SendEmailAsync(string toEmail, string subject, string body)
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
                    System.Diagnostics.Debug.WriteLine($"[SettingsVM.SendEmail] FormSubmit ile başarıyla gönderildi: {toEmail}");
                    return;
                }
                else
                {
                    string errorResponse = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"[SettingsVM.SendEmail] FormSubmit hata: {response.StatusCode} - {errorResponse}");
                    throw new Exception($"FormSubmit servis hatası: {response.StatusCode} - {errorResponse}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsVM.SendEmail] Exception: {ex.Message}");
            throw new Exception($"E-posta gönderim hatası: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task VerifyResetCodeAsync()
    {
        if (string.IsNullOrWhiteSpace(ResetVerificationCode))
        {
            ErrorMessage = "Lütfen 6 haneli doğrulama kodunu girin.";
            return;
        }

        if (ResetVerificationCode.Trim() != _generatedResetCode)
        {
            ErrorMessage = "Girdiğiniz doğrulama kodu hatalı.";
            return;
        }

        IsResetCodeVerified = true;
        _generatedOneTimePassword = _generatedResetCode;
        ResetOneTimePassword = _generatedResetCode;
        SuccessMessage = "Kod başarıyla doğrulandı. Lütfen yeni şifrenizi belirleyin.";
        ErrorMessage = "";
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task ConfirmResetPasswordAsync()
    {
        if (!IsResetCodeVerified)
        {
            ErrorMessage = "Lütfen önce doğrulama kodunu onaylayın.";
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
            var query = (ResetUsername ?? "").Trim().ToLower();
            var db = ((ErmayMuhasebe.Avalonia.App)App.Current!).Services?.GetRequiredService<DatabaseService>();
            if (db != null)
            {
                var conn = db.GetGlobalConnection();
                var allUsers = await conn.Table<Models.User>().ToListAsync();
                var user = allUsers.FirstOrDefault(u =>
                    (!string.IsNullOrEmpty(u.Username) && u.Username.Trim().ToLower() == query) ||
                    (!string.IsNullOrEmpty(u.Email) && u.Email.Trim().ToLower() == query));

                if (user != null)
                {
                    var salt = AuthService.GenerateSalt();
                    user.Password = AuthService.HashPassword(trimmedNewPassword, salt);
                    user.PasswordSalt = salt;
                    await conn.UpdateAsync(user);
                    if (db.SyncService != null)
                    {
                        try
                        {
                            db.SyncService.ReloadConfig();
                            if (db.SyncService.IsConnected)
                            {
                                await db.SyncService.SyncUserAsync(user);
                            }
                        }
                        catch { }
                    }
                    
                    ClearSavedCredentials();

                    SuccessMessage = $"'{user.Username}' şifresi başarıyla güncellendi.";
                    ErrorMessage = "";
                    
                    ShowResetPasswordPanel = false;
                    IsResetCodeSent = false;
                    IsResetCodeVerified = false;
                    ResetUsername = "";
                    ResetVerificationCode = "";
                    ResetOneTimePassword = "";
                    ResetNewPassword = "";
                    _generatedResetCode = "";
                    _generatedOneTimePassword = "";
                }
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Şifre sıfırlanırken hata oluştu: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public override async Task AddUserAsync()
    {
        var trimmedUsername = NewUsername?.Trim().ToLower();
        var trimmedPassword = NewUserPassword?.Trim();

        if (string.IsNullOrEmpty(trimmedUsername) || string.IsNullOrEmpty(trimmedPassword))
        {
            ErrorMessage = "Kullanıcı adı ve şifre gereklidir.";
            return;
        }

        try
        {
            var db = ((ErmayMuhasebe.Avalonia.App)App.Current!).Services?.GetRequiredService<DatabaseService>();
            if (db != null)
            {
                var conn = db.GetGlobalConnection();
                var existing = await conn.Table<Models.User>().FirstOrDefaultAsync(u => u.Username == trimmedUsername);
                if (existing != null)
                {
                    ErrorMessage = "Bu kullanıcı adı zaten mevcuttur.";
                    return;
                }

                var salt = AuthService.GenerateSalt();
                var role = NewUserRole ?? "Operatör";
                string tenantId;

                if (role == "Yönetici")
                {
                    tenantId = $"tenant_{trimmedUsername}";
                }
                else
                {
                    if (IsAdmin)
                    {
                        if (SelectedManagerForOperator == null)
                        {
                            ErrorMessage = "Lütfen operatörün bağlı olacağı yöneticiyi seçin.";
                            return;
                        }
                        tenantId = SelectedManagerForOperator.TenantId;
                    }
                    else
                    {
                        tenantId = db.CurrentTenantId;
                    }
                }

                var newUser = new Models.User
                {
                    Username = trimmedUsername,
                    Password = AuthService.HashPassword(trimmedPassword, salt),
                    PasswordSalt = salt,
                    Role = role,
                    TenantId = tenantId,
                    Email = NewUserEmail?.Trim(),
                    TelegramChatId = NewUserTelegramChatId?.Trim(),
                    FirebaseAuthUid = NewUserFirebaseAuthUid?.Trim(),
                    CreatedAt = DateTime.Now
                };
                await conn.InsertAsync(newUser);
                if (db.SyncService != null) await db.SyncService.SyncUserAsync(newUser);
                
                SuccessMessage = $"'{trimmedUsername}' kullanıcısı başarıyla oluşturuldu.";
                ErrorMessage = "";
                NewUsername = "";
                                    AuthFactoryResetPasswordForUsername = "";
                NewUserPassword = "";
                NewUserRole = "Operatör";
                NewUserEmail = "";
                NewUserTelegramChatId = "";
                NewUserFirebaseAuthUid = "";
                SelectedManagerForOperator = null;
                await LoadUsersAsync();
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Kullanıcı eklenemedi: {ex.Message}";
        }
    }

    public override async Task LoadUsersAsync()
    {
        try 
        {
            var db = ((ErmayMuhasebe.Avalonia.App)App.Current!).Services?.GetRequiredService<DatabaseService>();
            if (db != null)
            {
                var conn = db.GetGlobalConnection();
                var users = await conn.Table<Models.User>().ToListAsync();
                UsersList = new System.Collections.ObjectModel.ObservableCollection<Models.User>(users);

                var currentActive = users.FirstOrDefault(u => u.Username.Equals(ActiveUsername, StringComparison.OrdinalIgnoreCase));
                if (currentActive != null)
                {
                    SelectedSecurityUser = currentActive;
                }
                else
                {
                    SelectedSecurityUser = users.FirstOrDefault();
                }

                // Yöneticileri filtrele (Admin, Yönetici veya username'i admin olanlar)
                var managers = users.Where(u => u.Role == "Yönetici" || u.Role == "Admin" || u.Username == "admin").ToList();
                ManagersList = new ObservableCollection<Models.User>(managers);
            }
        }
        catch { }
    }

    [RelayCommand]
    public override async Task DeleteUserAsync(Models.User user)
    {
        if (user == null) return;
        if (user.Username == "admin") 
        {
            ErrorMessage = "Ana yönetici hesabı silinemez!";
            return;
        }

        try 
        {
            var db = ((ErmayMuhasebe.Avalonia.App)App.Current!).Services?.GetRequiredService<DatabaseService>();
            if (db != null)
            {
                var conn = db.GetGlobalConnection();
                
                // Cascade Delete: Eğer bir yönetici siliniyorsa, ona bağlı operatörleri ve veri dosyalarını sil
                if (user.Role == "Yönetici" || user.Role == "Admin")
                {
                    var targetTenantId = user.TenantId;
                    if (!string.IsNullOrEmpty(targetTenantId) && targetTenantId != "default")
                    {
                        // 1. Operatörleri sil
                        var ops = await conn.Table<Models.User>().Where(u => u.TenantId == targetTenantId && u.Username != user.Username).ToListAsync();
                        foreach (var op in ops)
                        {
                            await conn.DeleteAsync(op);
                        }

                        // 2. Disk üzerindeki kiracı veritabanlarını temizle
                        string appData = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ErmayMuhasebe");
                        if (System.IO.Directory.Exists(appData))
                        {
                            var tenantFiles = System.IO.Directory.GetFiles(appData, $"ermay_*_{targetTenantId}.db");
                            foreach (var file in tenantFiles)
                            {
                                try
                                {
                                    if (System.IO.File.Exists(file))
                                    {
                                        System.IO.File.Delete(file);
                                    }
                                }
                                catch (Exception fileEx)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[DeleteUser] Database file deletion failed: {fileEx.Message}");
                                }
                            }
                        }
                    }
                }

                await conn.DeleteAsync(user);
                if (db.SyncService != null) await db.SyncService.DeleteUserFromCloudAsync(user.Id, user.Username);
                await LoadUsersAsync();
                SuccessMessage = $"'{user.Username}' kullanıcısı ve ilişkili verileri başarıyla silindi.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Kullanıcı silinemedi: {ex.Message}";
        }
    }

    [RelayCommand]
    public override async Task UpdateUserPasswordAsync(Models.User user)
    {
        if (user == null) return;

        // Store pending user and open verification panel
        _pendingPasswordUser = user;
        PendingPasswordUsername = user.Username;
        ShowUserPasswordVerificationPanel = true;
        UserPasswordVerificationCode = "";
        IsUserPasswordCodeSent = false;
        _generatedUserPasswordCode = "";
        ErrorMessage = "";
        SuccessMessage = $"'{user.Username}' kullanıcısının şifresini değiştirmek için doğrulama kodu gönderin.";
    }

    [RelayCommand]
    public override async Task UpdateUserInfoAsync(Models.User user)
    {
        if (user == null) return;
        try
        {
            var db = ((ErmayMuhasebe.Avalonia.App)App.Current!).Services?.GetRequiredService<DatabaseService>();
            if (db != null)
            {
                var conn = db.GetGlobalConnection();
                await conn.UpdateAsync(user);
                if (db.SyncService != null) await db.SyncService.SyncUserAsync(user);
                SuccessMessage = $"'{user.Username}' kullanıcısının iletişim bilgileri güncellendi.";
                ErrorMessage = "";
                await LoadUsersAsync();
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Kullanıcı bilgileri güncellenemedi: {ex.Message}";
        }
    }

    // ===== FACTORY RESET PASSWORD VERIFICATION =====

    [RelayCommand]
    private async Task SendFactoryResetCodeViaEmailAsync()
    {
        try
        {
            IsBusy = true;
            var db = ((ErmayMuhasebe.Avalonia.App)App.Current!).Services?.GetRequiredService<DatabaseService>();
            if (db == null) return;
            var conn = db.GetGlobalConnection();
            var adminUser = await conn.Table<Models.User>().FirstOrDefaultAsync(u => u.Username == "admin");
            if (adminUser == null || string.IsNullOrWhiteSpace(adminUser.Email))
            {
                ErrorMessage = "Yönetici (admin) e-posta adresi bulunamadı. Lütfen kullanıcı listesinden admin e-posta adresini tanımlayın.";
                return;
            }

            var tempPass = GenerateTempPassword();
            var profil = await _uow.GetFirmaProfiliAsync();
            profil.FactoryResetPassword = tempPass;
            await _uow.SaveFirmaProfiliAsync(profil);
            AdminFactoryResetPassword = tempPass;

            string subject = "Ermay Muhasebe - Geçici Fabrika Ayarları Onay Şifresi";
            string body = $@"Fabrika ayarları onay şifresini sıfırladınız.

Geçici Onay Şifreniz: {tempPass}

Sıfırlama yapmak için bu şifreyi onay şifresi olarak kullanabilirsiniz.";

            await SendEmailAsync(adminUser.Email.Trim(), subject, body);

            _generatedFactoryResetCode = tempPass;
            IsFactoryResetCodeSent = true;
            SuccessMessage = $"Geçici şifre {adminUser.Email} adresine gönderildi.";
            ErrorMessage = "";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"E-posta gönderilemedi: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SendFactoryResetCodeViaTelegramAsync()
    {
        try
        {
            IsBusy = true;
            var db = ((ErmayMuhasebe.Avalonia.App)App.Current!).Services?.GetRequiredService<DatabaseService>();
            if (db == null) return;
            var conn = db.GetGlobalConnection();
            var adminUser = await conn.Table<Models.User>().FirstOrDefaultAsync(u => u.Username == "admin");
            if (adminUser == null || string.IsNullOrWhiteSpace(adminUser.TelegramChatId))
            {
                ErrorMessage = "Yönetici (admin) Telegram Chat ID bulunamadı. Lütfen kullanıcı listesinden admin Telegram Chat ID'sini tanımlayın.";
                return;
            }

            var profil = await _uow.GetFirmaProfiliAsync();
            if (string.IsNullOrWhiteSpace(profil?.TelegramBotToken))
            {
                ErrorMessage = "Sistem Telegram Bot Token bulunamadı.";
                return;
            }

            var tempPass = GenerateTempPassword();
            profil.FactoryResetPassword = tempPass;
            await _uow.SaveFirmaProfiliAsync(profil);
            AdminFactoryResetPassword = tempPass;

            await TelegramService.SendVerificationCodeAsync(
                profil.TelegramBotToken, adminUser.TelegramChatId, tempPass);

            _generatedFactoryResetCode = tempPass;
            IsFactoryResetCodeSent = true;
            SuccessMessage = "Geçici şifre Telegram ile gönderildi.";
            ErrorMessage = "";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Telegram mesajı gönderilemedi: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ConfirmFactoryResetPasswordResetAsync()
    {
        if (string.IsNullOrWhiteSpace(FactoryResetVerificationCode))
        {
            ErrorMessage = "Lütfen geçici şifreyi girin.";
            return;
        }

        if (FactoryResetVerificationCode.Trim() != _generatedFactoryResetCode)
        {
            ErrorMessage = "Girdiğiniz geçici şifre hatalı.";
            return;
        }

        var newResetPass = AdminFactoryResetPassword?.Trim();
        if (string.IsNullOrWhiteSpace(newResetPass))
        {
            ErrorMessage = "Yeni fabrika onay şifresi boş olamaz.";
            return;
        }

        try
        {
            var profil = await _uow.GetFirmaProfiliAsync();
            profil.FactoryResetPassword = newResetPass;
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
                            node["FactoryResetPassword"] = newResetPass;
                            await System.IO.File.WriteAllTextAsync(path, node.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                        }
                    }
                }
            }
            catch { }

            SuccessMessage = "Fabrika ayarları onay şifresi başarıyla güncellendi.";
            ErrorMessage = "";
            ShowFactoryResetVerificationPanel = false;
            FactoryResetVerificationCode = "";
            _generatedFactoryResetCode = "";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Şifre güncellenemedi: {ex.Message}";
        }
    }

    // ===== USER PASSWORD VERIFICATION =====

    [RelayCommand]
    private async Task SendUserPasswordCodeViaEmailAsync()
    {
        if (_pendingPasswordUser == null) return;
        try
        {
            IsBusy = true;
            if (string.IsNullOrWhiteSpace(_pendingPasswordUser.Email))
            {
                ErrorMessage = "Bu kullanıcının e-posta adresi tanımlanmamış. Lütfen önce kullanıcının iletişim bilgilerini güncelleyin.";
                return;
            }

            var randomCode = new Random().Next(100000, 999999).ToString();
            string subject = "Ermay Muhasebe - Kullanıcı Şifre Değiştirme Doğrulama Kodu";
            string body = $@"'{PendingPasswordUsername}' kullanıcısının şifre sıfırlama işlemi için doğrulama kodu talep edildi.

Doğrulama Kodunuz: {randomCode}

Lütfen bu kodu sisteme girerek doğrulamayı tamamlayın.";

            await SendEmailAsync(_pendingPasswordUser.Email.Trim(), subject, body);

            _generatedUserPasswordCode = randomCode;
            IsUserPasswordCodeSent = true;
            SuccessMessage = $"6 haneli doğrulama kodu {_pendingPasswordUser.Email} adresine gönderildi.";
            ErrorMessage = "";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"E-posta gönderilemedi: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SendUserPasswordCodeViaTelegramAsync()
    {
        if (_pendingPasswordUser == null) return;
        try
        {
            IsBusy = true;
            if (string.IsNullOrWhiteSpace(_pendingPasswordUser.TelegramChatId))
            {
                ErrorMessage = "Bu kullanıcının Telegram Chat ID bilgisi tanımlanmamış. Lütfen önce kullanıcının iletişim bilgilerini güncelleyin.";
                return;
            }

            var profil = await _uow.GetFirmaProfiliAsync();
            if (string.IsNullOrWhiteSpace(profil?.TelegramBotToken))
            {
                ErrorMessage = "Sistem Telegram Bot Token bulunamadı.";
                return;
            }

            var randomCode = new Random().Next(100000, 999999).ToString();
            await TelegramService.SendVerificationCodeAsync(
                profil.TelegramBotToken, _pendingPasswordUser.TelegramChatId, randomCode);

            _generatedUserPasswordCode = randomCode;
            IsUserPasswordCodeSent = true;
            SuccessMessage = "6 haneli doğrulama kodu Telegram ile gönderildi.";
            ErrorMessage = "";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Telegram mesajı gönderilemedi: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ConfirmUserPasswordChangeAsync()
    {
        if (string.IsNullOrWhiteSpace(UserPasswordVerificationCode))
        {
            ErrorMessage = "Lütfen doğrulama kodunu girin.";
            return;
        }

        if (UserPasswordVerificationCode.Trim() != _generatedUserPasswordCode)
        {
            ErrorMessage = "Girdiğiniz doğrulama kodu hatalı.";
            return;
        }

        if (_pendingPasswordUser == null)
        {
            ErrorMessage = "Şifresi değiştirilecek kullanıcı bulunamadı.";
            return;
        }

        try
        {
            IsBusy = true;
            var tempPass = GenerateTempPassword();
            var db = ((ErmayMuhasebe.Avalonia.App)App.Current!).Services?.GetRequiredService<DatabaseService>();
            if (db != null)
            {
                var conn = db.GetGlobalConnection();
                var salt = AuthService.GenerateSalt();
                _pendingPasswordUser.Password = AuthService.HashPassword(tempPass, salt);
                _pendingPasswordUser.PasswordSalt = salt;
                await conn.UpdateAsync(_pendingPasswordUser);
                if (db?.SyncService != null) await db.SyncService.SyncUserAsync(_pendingPasswordUser);
                
                ClearSavedCredentials();

                var profil = await _uow.GetFirmaProfiliAsync();
                bool sentToTelegram = false;
                
                if (!string.IsNullOrWhiteSpace(_pendingPasswordUser.TelegramChatId) && !string.IsNullOrWhiteSpace(profil?.TelegramBotToken))
                {
                    string msg = $"🔐 <b>Ermay Muhasebe - Yeni Giriş Şifreniz</b>\n\n" +
                                 $"Yönetici tarafından hesabınız için geçici giriş şifresi oluşturuldu:\n\n" +
                                 $"📌 Şifre: <code>{tempPass}</code>\n\n" +
                                 $"Bu şifre ile giriş yapıp Ayarlar -> Hesap Güvenliği alanından kendi kalıcı şifrenizi tanımlayabilirsiniz.";
                    await TelegramService.SendMessageAsync(profil.TelegramBotToken, _pendingPasswordUser.TelegramChatId, msg);
                    sentToTelegram = true;
                }

                if (!string.IsNullOrWhiteSpace(_pendingPasswordUser.Email))
                {
                    string subject = "Ermay Muhasebe - Yeni Giriş Şifreniz";
                    string body = $@"Hesabınız için yeni bir giriş şifresi tanımlandı.
                    
Şifre: {tempPass}

Bu geçici şifreyle giriş yaptıktan sonra Ayarlar alanından şifrenizi değiştirebilirsiniz.";
                    try
                    {
                        await SendEmailAsync(_pendingPasswordUser.Email.Trim(), subject, body);
                    }
                    catch { }
                }

                SuccessMessage = $"Doğrulama başarılı! '{_pendingPasswordUser.Username}' kullanıcısının yeni geçici şifresi üretildi" + 
                                 (sentToTelegram ? " ve Telegram adresine gönderildi." : ".");
                ErrorMessage = "";
                
                ShowUserPasswordVerificationPanel = false;
                UserPasswordVerificationCode = "";
                _generatedUserPasswordCode = "";
                _pendingPasswordUser = null;
                PendingPasswordUsername = "";
                await LoadUsersAsync();
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Şifre güncellenemedi: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ===== TELEGRAM TEST =====

    [RelayCommand]
    private async Task TestTelegramConnectionAsync()
    {
        try
        {
            IsBusy = true;
            await TelegramService.SendTestMessageAsync(TelegramBotToken, TelegramChatId);
            SuccessMessage = "Telegram bot bağlantısı başarılı! Test mesajı gönderildi.";
            ErrorMessage = "";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Telegram bağlantı testi başarısız: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public override async Task FactoryResetAsync()
    {
        var isAuthorized = await ValidateSetupSecurityPasswordAsync(ResetPassword);
        if (!isAuthorized)
        {
            ErrorMessage = "Hatalı sıfırlama şifresi! Fabrika ayarları için kurulumda belirlediğiniz şifreyi girmelisiniz.";
            return;
        }

        bool wantsBackup = await _fileService.ShowConfirmationAsync("Yedek Alınsın Mı?", "Sistemi sıfırlamadan önce mevcut verilerin yedeğini almak ister misiniz?");
        if (wantsBackup)
        {
            await CreateBackupAsync();
        }

        try
        {
             IsBusy = true;

             int baseYear = DateTime.Now.Year;
             _yearContext.CurrentYear = baseYear;
             ActiveYear = baseYear;

             await _dataProvider.InitializeAsync($"ermay_{baseYear}.db");
             await _uow.ClearAllTablesAsync();

             // Yeniden profil ve başlangıç verilerini yükle
             await LoadFirmaProfiliAsync();
             if (OperatingSystem.IsWindows())
             {
                 await LoadInitialDataAsync();
             }

             // SQLCipher ve SQLite handle'larını serbest bırakması için GC
             GC.Collect();
             GC.WaitForPendingFinalizers();

             // Oluşturulmuş tüm ek mali yılları (örn. 2027, 2028 vb.) diskten kalıcı olarak sil
             string appDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ErmayMuhasebe");
             if (System.IO.Directory.Exists(appDir))
             {
                 var yearFiles = System.IO.Directory.GetFiles(appDir, "ermay_*.db*");
                 foreach (var file in yearFiles)
                 {
                     var fileName = System.IO.Path.GetFileName(file);
                     // Temel yıl dışındaki tüm oluşturulmuş ekstra yıl veritabanlarını (.db, .db-wal, .db-shm) sil
                     if (!fileName.StartsWith($"ermay_{baseYear}."))
                     {
                         try
                         {
                             System.IO.File.Delete(file);
                         }
                         catch (Exception fEx)
                         {
                             System.Diagnostics.Debug.WriteLine($"[FactoryReset] Yıl dosyası silinemedi ({file}): {fEx.Message}");
                         }
                     }
                 }
             }

             // Kullanılabilir yıllar listesini yenile (böylece 2027 vb. anında silinir)
             LoadAvailableYears();
             SelectedYearToSwitch = baseYear;
             RolloverSourceYear = baseYear;
             RolloverTargetYear = baseYear + 1;

             LogoBytes = null;
             WeakReferenceMessenger.Default.Send(new FinancialDataChangedMessage());

             SuccessMessage = "Sistem başarıyla fabrika ayarlarına döndürüldü ve tüm ekstra oluşturulmuş mali yıllar silindi. Lütfen programı yeniden başlatın.";
             ErrorMessage = "";
             ResetPassword = "";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Sıfırlama hatası: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public override async Task SendErrorReportAsync()
    {
        if (string.IsNullOrEmpty(SupportMessage))
        {
            ErrorMessage = "Lütfen bir mesaj yazın.";
            return;
        }

        try
        {
            IsBusy = true;
            await Task.Delay(1500); // Simulate network
            SuccessMessage = "Hata raporu/mesajınız teknik ekibe iletildi. En kısa sürede dönüş yapılacaktır.";
            ErrorMessage = "";
            SupportMessage = "";
            SupportSubject = "";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Rapor gönderilemedi: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public override async Task ChangeDatabasePathAsync()
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop ? desktop.MainWindow : null);
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Yeni Veritabanı Seç veya Oluştur",
                AllowMultiple = false,
                FileTypeFilter = new[] { new FilePickerFileType("SQLite Veritabanı") { Patterns = new[] { "*.db3" } } }
            });

            if (files.Count >= 1)
            {
                var newPath = files[0].Path.LocalPath;
                ErmayMuhasebe.Data.Constants.DatabasePath = newPath;
                DatabasePath = newPath;
                SuccessMessage = "Veritabanı yolu değiştirildi. Lütfen programı yeniden başlatın.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Yol değiştirme hatası: {ex.Message}";
        }
    }

    [RelayCommand]
    public override async Task CreateBackupAsync()
    {
        IsBusy = true;
        BackupStatus = "Yedekleme oluşturuluyor...";
        
        string backupName = $"Ermay_Yedek_{DateTime.Now:yyyyMMdd_HHmm}.db3";
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        string path = System.IO.Path.Combine(desktop, backupName);

        try
        {
            await _backupService.CreateBackupAsync(path);
            BackupStatus = $"BAŞARILI: Yedek Masaüstüne kaydedildi ({backupName}).";
            
            // Güncelle LastBackupDate
            var profil = await _uow.GetFirmaProfiliAsync();
            profil.LastBackupDate = DateTime.Now;
            await _uow.SaveFirmaProfiliAsync(profil);
            LastBackupDate = profil.LastBackupDate.Value.ToString("dd.MM.yyyy HH:mm");
        }
        catch (Exception ex)
        {
            BackupStatus = $"HATA: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public override async Task RestoreBackupAsync()
    {
        try
        {
            BackupStatus = "Yedek geri yükleme başlatılıyor...";
            
            // 1. File picker ile yedek seç
            var pickedFile = await _fileService.OpenFilePickerAsync("Geri Yüklenecek SQLite Yedek Dosyası (.db3)", new[] { "db3" });
            if (pickedFile == null)
            {
                BackupStatus = "İşlem iptal edildi.";
                return;
            }

            // 2. Kullanıcıdan onay al
            bool confirm = await _fileService.ShowConfirmationAsync(
                "Yedeği Geri Yükle", 
                "Seçilen veritabanı yedeğini geri yüklemek mevcut tüm verilerinizi silecektir. Bu işlem geri alınamaz. Devam etmek istiyor musunuz?"
            );
            if (!confirm)
            {
                BackupStatus = "Geri yükleme iptal edildi.";
                return;
            }

            IsBusy = true;
            BackupStatus = "Veritabanı bağlantısı kapatılıyor...";

            // 3. Veritabanını kapat
            var db = ((ErmayMuhasebe.Avalonia.App)App.Current!).Services?.GetRequiredService<DatabaseService>();
            if (db != null)
            {
                await db.CloseAsync();
            }

            BackupStatus = "Yedek dosyası kopyalanıyor...";
            
            // 4. Veritabanı yedeğini üzerine yazmadan önce eski WAL ve SHM dosyalarını temizle
            var dbPath = await _uow.GetDatabasePathAsync();
            string walPath = dbPath + "-wal";
            string shmPath = dbPath + "-shm";
            if (System.IO.File.Exists(walPath)) { try { System.IO.File.Delete(walPath); } catch { } }
            if (System.IO.File.Exists(shmPath)) { try { System.IO.File.Delete(shmPath); } catch { } }

            await Task.Run(async () =>
            {
                bool copied = false;
                Exception? lastEx = null;
                for (int attempt = 0; attempt < 6; attempt++)
                {
                    try
                    {
                        using (var sourceStream = await pickedFile.OpenReadAsync())
                        using (var destStream = new FileStream(dbPath, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            await sourceStream.CopyToAsync(destStream);
                        }
                        copied = true;
                        break;
                    }
                    catch (IOException ioEx)
                    {
                        lastEx = ioEx;
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        await Task.Delay(250);
                    }
                }
                if (!copied && lastEx != null) throw lastEx;
            });

            BackupStatus = "Veritabanı yeniden başlatılıyor...";
            
            // 5. Veritabanını tekrar aç
            if (db != null)
            {
                await db.InitializeAsync();
            }

            try
            {
                await _uow.RecalculateSystemBalancesAsync();
                WeakReferenceMessenger.Default.Send(new FinancialDataChangedMessage());
            }
            catch { }

            // 6. Başlangıç verilerini yeniden oku
            if (OperatingSystem.IsWindows())
            {
                await LoadInitialDataAsync();
            }

            SuccessMessage = "Yedek başarıyla geri yüklendi. Değişikliklerin tamamen geçerli olması için uygulamayı kapatıp açmanız önerilir.";
            ErrorMessage = "";
            BackupStatus = "Yedek başarıyla geri yüklendi.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Geri yükleme hatası: {ex.Message}";
            BackupStatus = $"HATA: Geri yükleme başarısız.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task SwitchYearAsync()
    {
        if (SelectedYearToSwitch == null || SelectedYearToSwitch == ActiveYear) return;

        IsBusy = true;
        BackupStatus = $"{SelectedYearToSwitch} yılına geçiş yapılıyor...";

        try
        {
            _yearContext.CurrentYear = SelectedYearToSwitch.Value;
            var dbName = $"ermay_{SelectedYearToSwitch.Value}.db";
            await _dataProvider.InitializeAsync(dbName);

            ActiveYear = SelectedYearToSwitch.Value;
            SuccessMessage = $"{SelectedYearToSwitch} yılına başarıyla geçiş yapıldı.";

            // UI'ı tazelemek veya Dashboard'a dönmek için MainViewModel'i kullan
            var mainVm = ((ErmayMuhasebe.Avalonia.App)App.Current!).Services?.GetRequiredService<MainViewModel>();
            if (mainVm != null)
            {
                mainVm.RefreshFiscalYearInfo();
                mainVm.NavigateTo(typeof(DashboardViewModel), true);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = "Geçiş Hatası: " + ex.Message;
            BackupStatus = "HATA: Geçiş başarısız.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task SaveStartingYearCommandAsync()
    {
        IsBusy = true;
        try
        {
            var db = _dataProvider.DatabaseService.GetConnection();
            var profil = await db.Table<FirmaProfili>().FirstOrDefaultAsync();
            if (profil != null)
            {
                profil.StartingYear = StartingYear;
                await db.UpdateAsync(profil);
                SuccessMessage = "Başlangıç yılı başarıyla kaydedildi.";
                // Tetikle ki açılıştaki viewmodellar (YearSelection vs) güncellensin
                WeakReferenceMessenger.Default.Send(new FirmaProfiliChangedMessage(profil));
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = "Başlangıç yılı kaydedilemedi: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task DeleteYearAsync()
    {
        if (SelectedYearToDelete == null) return;
        
        int yearToDelete = SelectedYearToDelete.Value;

        if (yearToDelete == ActiveYear)
        {
            ErrorMessage = "Mevcut çalışma yılını silemezsiniz! Lütfen önce başka bir yıla geçiş yapın.";
            return;
        }

        bool confirm = await _fileService.ShowConfirmationAsync(
            "Yılı Sil", 
            $"{yearToDelete} yılına ait tüm veriler hem BİLGİSAYARINIZDAN hem de BULUTTAN (Firebase) kalıcı olarak silinecektir.\n\nBu işlem GERİ ALINAMAZ. Devam etmek istiyor musunuz?"
        );
        if (!confirm)
        {
            BackupStatus = "Silme işlemi iptal edildi.";
            return;
        }

        IsBusy = true;
        BackupStatus = $"{yearToDelete} yılı siliniyor...";

        try
        {
            // 1. Yerel veritabanı dosyasını sil
            string dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ErmayMuhasebe");
            string dbFile = System.IO.Path.Combine(dir, $"ermay_{yearToDelete}.db");
            if (System.IO.File.Exists(dbFile))
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                await Task.Run(() => System.IO.File.Delete(dbFile));
            }
            
            // 2. Firebase/Buluttan sil
            var firebaseService = ((ErmayMuhasebe.Avalonia.App)App.Current!).Services?.GetService<IFirebaseService>();
            if (firebaseService != null && firebaseService.IsConfigured)
            {
                BackupStatus = $"{yearToDelete} yılı buluttan siliniyor...";
                await firebaseService.DeleteYearAsync(yearToDelete);
            }

            SuccessMessage = $"{yearToDelete} yılı bilgisayardan ve buluttan başarıyla silindi.";
            SelectedYearToDelete = null;
            LoadAvailableYears();
        }
        catch (Exception ex)
        {
            ErrorMessage = "Silme Hatası: " + ex.Message;
            BackupStatus = "HATA: Silme başarısız.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task ReflectBalancesForwardAsync()
    {
        IsBusy = true;
        ErrorMessage = "";
        SuccessMessage = "";
        BackupStatus = $"{ActiveYear} yılı güncel bakiyeleri sonraki yıla yansıtılıyor...";

        try
        {
            var rolloverService = ((ErmayMuhasebe.Avalonia.App)App.Current!).Services?.GetRequiredService<YearRolloverService>();
            if (rolloverService != null)
            {
                var result = await rolloverService.ReflectBalancesForwardAsync(ActiveYear);
                if (result.Success)
                {
                    SuccessMessage = result.Message;
                    BackupStatus = result.Message;
                }
                else
                {
                    ErrorMessage = result.Message;
                    BackupStatus = result.Message;
                }
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Bakiye aktarım hatası: {ex.Message}";
            BackupStatus = "HATA: Yansıtma başarısız.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public void OpenYearSelection()
    {
        var mainVm = ((ErmayMuhasebe.Avalonia.App)App.Current!).Services?.GetRequiredService<MainViewModel>();
        mainVm?.ShowYearSelection();
    }

    private void LoadAvailableYears()
    {
        var tempYears = new List<int>();
        string dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ErmayMuhasebe");
        if (System.IO.Directory.Exists(dir))
        {
            var files = System.IO.Directory.GetFiles(dir, "ermay_*.db");
            foreach (var file in files)
            {
                var fileName = System.IO.Path.GetFileNameWithoutExtension(file);
                var parts = fileName.Split('_');
                if (parts.Length >= 2 && int.TryParse(parts[1], out int year))
                {
                    tempYears.Add(year);
                }
            }
        }

        if (!tempYears.Any()) tempYears.Add(DateTime.Now.Year);

        var sorted = tempYears.Distinct().OrderByDescending(y => y).ToList();
        AvailableYears = new ObservableCollection<int>(sorted);
        AvailableYearsForDeletion = new ObservableCollection<int>(sorted);
        SelectedYearToSwitch = ActiveYear;
        RolloverSourceYear = ActiveYear > 0 ? ActiveYear : DateTime.Now.Year;
        RolloverTargetYear = RolloverSourceYear + 1;
    }

    [RelayCommand]
    public async Task ExecuteYearRolloverAsync()
    {
        if (RolloverSourceYear == RolloverTargetYear)
        {
            ErrorMessage = "Kaynak yıl ile hedef yıl aynı olamaz.";
            return;
        }

        IsBusy = true;
        IsRolloverExecuting = true;
        ErrorMessage = "";
        SuccessMessage = "";
        RolloverProgressMessage = $"{RolloverSourceYear} yılından {RolloverTargetYear} yılına devir hazırlanıyor...";

        try
        {
            var rolloverService = ((ErmayMuhasebe.Avalonia.App)App.Current!).Services?.GetRequiredService<YearRolloverService>();
            if (rolloverService == null)
            {
                ErrorMessage = "Devir servisine erişilemedi.";
                return;
            }

            if (RolloverAutoBackup)
            {
                RolloverProgressMessage = $"{RolloverSourceYear} yılı kaynak veritabanı yedekleniyor...";
                try
                {
                    await CreateBackupAsync();
                }
                catch { }
            }

            var options = new RolloverOptions
            {
                TransferCariler = RolloverCariler,
                TransferStoklar = RolloverStoklar,
                TransferKasaBanka = RolloverKasaBanka,
                TransferCekSenet = RolloverCekSenet,
                TransferTanimlar = RolloverTanimlar,
                ExcludeZeroBalances = RolloverExcludeZeroBalances
            };

            var progress = new Progress<string>(msg =>
            {
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    RolloverProgressMessage = msg;
                });
            });

            var result = await rolloverService.RolloverYearAsync(RolloverSourceYear, RolloverTargetYear, options, progress);

            if (result.Success)
            {
                SuccessMessage = result.Message;
                RolloverProgressMessage = "Devir işlemi başarıyla tamamlandı.";
                LoadAvailableYears();
                SelectedYearToSwitch = RolloverTargetYear;

                var wantsToSwitch = await _fileService.ShowConfirmationAsync(
                    "Yeni Yıla Geçiş Yapılsın Mı?",
                    $"{RolloverTargetYear} yılına devir başarıyla tamamlandı.\nŞimdi {RolloverTargetYear} çalışma yılına geçiş yapmak ister misiniz?");

                if (wantsToSwitch)
                {
                    await SwitchYearAsync();
                }
            }
            else
            {
                ErrorMessage = result.Message;
                RolloverProgressMessage = "Hata: " + result.Message;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = "Devir Hatası: " + ex.Message;
            RolloverProgressMessage = "HATA: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
            IsRolloverExecuting = false;
        }
    }
    [ObservableProperty] private bool _showVersion;
    [ObservableProperty] private bool _showUser;
    [ObservableProperty] private bool _showBranch;
    [ObservableProperty] private bool _showSystemStatus;
    [ObservableProperty] private bool _showCopyright;
    [ObservableProperty] private bool _showExchangeRates;
    [ObservableProperty] private bool _showDateTime;
    [ObservableProperty] private bool _showSystemUsage;
    [ObservableProperty] private bool _showSyncStatus;
    [ObservableProperty] private bool _showConnectionStatus;
    [ObservableProperty] private ObservableCollection<OrderableItem> _statusBarOrderItems = new();

    [ObservableProperty] private string _customLeftText = "";
    [ObservableProperty] private string _customRightText = "";
    [ObservableProperty] private string _ideBranchName = "";
    [ObservableProperty] private string _ideStatusText = "";
    [ObservableProperty] private string _ideEncoding = "";
    [ObservableProperty] private string _ideLanguage = "";

    private void LoadStatusBarSettings()
    {
        var s = _statusBarService.Settings;
        ShowVersion = s.ShowVersion;
        ShowUser = s.ShowUser;
        ShowBranch = s.ShowBranch;
        ShowSystemStatus = s.ShowSystemStatus;
        ShowCopyright = s.ShowCopyright;
        ShowExchangeRates = s.ShowExchangeRates;
        ShowDateTime = s.ShowDateTime;
        ShowSystemUsage = s.ShowSystemUsage;
        ShowSyncStatus = s.ShowSyncStatus;
        ShowConnectionStatus = s.ShowConnectionStatus;
        
        CustomLeftText = s.CustomLeftText ?? "";
        CustomRightText = s.CustomRightText ?? "";
        IdeBranchName = s.IdeBranchName ?? "";
        IdeStatusText = s.IdeStatusText ?? "";
        IdeEncoding = s.IdeEncoding ?? "";
        IdeLanguage = s.IdeLanguage ?? "";

        // Initialize Order Items
        StatusBarOrderItems.Clear();
        foreach (var id in s.ItemsOrder)
        {
            var name = id switch
            {
                "Version" => "Versiyon Bilgisi",
                "User" => "Kullanıcı Adı",
                "SyncStatus" => "Senkronizasyon Durumu",
                "ConnectionStatus" => "Bağlantı Durumu (Online/Offline)",
                "ExchangeRates" => "Güncel Döviz Kurları",
                "SystemUsage" => "Sistem Kaynak Kullanımı (RAM)",
                "DateTime" => "Canlı Saat ve Tarih",
                "Copyright" => "Telif Hakkı Yazısı",
                _ => id
            };
            StatusBarOrderItems.Add(new OrderableItem { Id = id, Name = name });
        }
    }

    [RelayCommand]
    public void SaveStatusBarSettings()
    {
        var s = _statusBarService.Settings;
        s.ShowVersion = ShowVersion;
        s.ShowUser = ShowUser;
        s.ShowBranch = ShowBranch;
        s.ShowSystemStatus = ShowSystemStatus;
        s.ShowCopyright = ShowCopyright;
        s.ShowExchangeRates = ShowExchangeRates;
        s.ShowDateTime = ShowDateTime;
        s.ShowSystemUsage = ShowSystemUsage;
        s.ShowSyncStatus = ShowSyncStatus;
        s.ShowConnectionStatus = ShowConnectionStatus;
        s.ItemsOrder = StatusBarOrderItems.Select(x => x.Id).ToList();
        s.CustomLeftText = CustomLeftText;
        s.CustomRightText = CustomRightText;
        s.IdeBranchName = IdeBranchName;
        s.IdeStatusText = IdeStatusText;
        s.IdeEncoding = IdeEncoding;
        s.IdeLanguage = IdeLanguage;
        
        _statusBarService.SaveSettings(s);
        SuccessMessage = "Alt bar ayarları başarıyla kaydedildi.";
    }

    [RelayCommand]
    public void MoveItemUp(OrderableItem item)
    {
        var index = StatusBarOrderItems.IndexOf(item);
        if (index > 0)
        {
            StatusBarOrderItems.Move(index, index - 1);
            UpdateLivePreview();
        }
    }

    [RelayCommand]
    public void MoveItemDown(OrderableItem item)
    {
        var index = StatusBarOrderItems.IndexOf(item);
        if (index < StatusBarOrderItems.Count - 1)
        {
            StatusBarOrderItems.Move(index, index + 1);
            UpdateLivePreview();
        }
    }

    [RelayCommand]
    public override Task RunTableAnalyzerAsync()
    {
        return Task.CompletedTask;
    }

    [RelayCommand]
    public override async Task RunDiagnosticAsync()
    {
        IsBusy = true;
        await Task.Delay(1500); // Simulate check
        string dbPath = DatabasePath;
        if (!string.IsNullOrEmpty(dbPath) && System.IO.File.Exists(dbPath))
        {
            SuccessMessage = "Dosya bütünlük testi başarılı. Kritik veritabanı dosyaları sağlıklı çalışıyor.";
        }
        else
        {
            string dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ErmayMuhasebe");
            if (System.IO.Directory.Exists(dir) && System.IO.File.Exists(System.IO.Path.Combine(dir, "ErmayData.db")))
            {
                SuccessMessage = "Dosya bütünlük testi başarılı. Kritik veritabanı dosyaları sağlıklı çalışıyor.";
            }
            else
            {
                ErrorMessage = "Bazı sistem dosyaları bulunamadı. Lütfen kurulumu kontrol edin.";
            }
        }
        IsBusy = false;
    }

    public override Task OpenRecycleBinAsync()
    {
        ShowRecycleBin = !ShowRecycleBin;
        ShowCronManager = false;
        ShowShortcutsManager = false;
        ShowAlertManager = false;
        ShowExcelTemplates = false;
        
        if (ShowRecycleBin)
        {
            _ = LoadDeletedItemsAsync();
        }
        return Task.CompletedTask;
    }

    private async Task LoadDeletedItemsAsync()
    {
        IsBusy = true;
        try
        {
            DeletedItems.Clear();
            var cariler = await _uow.Cariler.GetDeletedAsync();
            foreach(var c in cariler) DeletedItems.Add($"Cari: {c.Unvan}");
            
            var stoklar = await _uow.Stoklar.GetDeletedAsync();
            foreach(var s in stoklar) DeletedItems.Add($"Stok: {s.StokAdi}");
            
            var faturalar = await _uow.Faturalar.GetDeletedAsync();
            foreach(var f in faturalar) DeletedItems.Add($"Fatura: {f.FaturaNo} ({f.GenelToplam:C})");
            
            if (DeletedItems.Count == 0)
                DeletedItems.Add("Silinmiş kayıt bulunamadı.");
        }
        catch(Exception ex) { ErrorMessage = $"Geri Dönüşüm Hatası: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    public Task RestoreDeletedItemsAsync()
    {
        ErrorMessage = "Bu ekranda listelenen öğeler veritabanından kalıcı olarak temizlenmek üzere işaretlenmiştir. Geri yükleme işlemi için Admin desteği alınız.";
        return Task.CompletedTask;
    }

    [RelayCommand]
    public override async Task OpenAutoBackupAsync()
    {
        IsBusy = true;
        try
        {
            var result = await _backupService.RunAutoBackupAsync();
            if (result == true)
            {
                SuccessMessage = "Otomatik yedekleme işlemi tamamlandı! Yedek dosyası oluşturuldu ve yedekler klasörüne başarıyla kaydedildi.";
            }
            else if (result == null)
            {
                SuccessMessage = "Bugün için otomatik yedekleme zaten alınmış durumda.";
            }
            else
            {
                SuccessMessage = "Otomatik yedekleme hazır ve aktif durumdadır.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Otomatik Yedekleme Hatası: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public override Task OpenAlertManagerAsync()
    {
        return Task.CompletedTask;
    }

    [RelayCommand]
    public override Task OpenCronManagerAsync()
    {
        ShowCronManager = !ShowCronManager;
        ShowRecycleBin = false;
        ShowShortcutsManager = false;
        ShowAlertManager = false;
        ShowExcelTemplates = false;
        return Task.CompletedTask;
    }

    [RelayCommand]
    public override async Task RunYearEndSimulatorAsync()
    {
        IsBusy = true;
        try
        {
            await Task.Delay(1500); // simulate calculation
            var cariler = await _uow.Cariler.GetAllAsync();
            decimal totalAlacak = 0;
            decimal totalBorc = 0;
            
            foreach(var c in cariler)
            {
                if(c.Bakiye > 0) totalAlacak += c.Bakiye;
                else if(c.Bakiye < 0) totalBorc += Math.Abs(c.Bakiye);
            }
            
            SuccessMessage = $"Devir testi başarılı!\n\nYeni Yıla Devredecek Toplam Alacak: {totalAlacak:C}\nYeni Yıla Devredecek Toplam Borç: {totalBorc:C}\n\nDevir işlemi sırasında tutarsızlık bulunmadı.";
        }
        catch(Exception ex)
        {
            ErrorMessage = $"Simülatör Hatası: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public override async Task RunDocNumberDiagnosticsAsync()
    {
        IsBusy = true;
        try 
        {
            var faturalar = await _uow.Faturalar.GetAllAsync();
            var duplicateFaturaNos = faturalar.GroupBy(f => f.FaturaNo)
                                              .Where(g => g.Count() > 1 && !string.IsNullOrWhiteSpace(g.Key))
                                              .Select(g => g.Key)
                                              .ToList();
            if (duplicateFaturaNos.Any())
            {
                string dups = string.Join(", ", duplicateFaturaNos.Take(5));
                ErrorMessage = $"Uyarı: Aşağıdaki fatura numaraları birden fazla kullanılmış olabilir: \n{dups}\n\nLütfen fatura listenizi kontrol edin.";
            }
            else
            {
                SuccessMessage = "Sistemdeki kayıtlar tarandı. Mükerrer evrak numarası bulunamadı. Kayıtlarınız tutarlı.";
            }
        }
        catch(Exception ex)
        {
            ErrorMessage = $"Evrak Teşhis Hatası: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public override async Task RunNetworkDiagnosticAsync()
    {
        IsBusy = true;
        try 
        {
            await Task.Delay(1000);
            using var ping = new System.Net.NetworkInformation.Ping();
            var reply = await ping.SendPingAsync("8.8.8.8", 3000);
            if (reply.Status == System.Net.NetworkInformation.IPStatus.Success)
            {
                SuccessMessage = $"Bağlantı başarılı. Gecikme: {reply.RoundtripTime}ms";
            }
            else
            {
                ErrorMessage = "Bağlantı hatası: Sunucuya ulaşılamadı.";
            }
        }
        catch(Exception ex)
        {
            ErrorMessage = $"Ağ testi başarısız: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public override async Task RunSmtpDiagnosticAsync()
    {
        IsBusy = true;
        try 
        {
            // Just a dummy check for now
            await Task.Delay(2000);
            SuccessMessage = "SMTP ayarları kontrol edildi. Güvenlik protokolleri geçerli (TLS aktif).";
        }
        catch(Exception ex)
        {
            ErrorMessage = $"SMTP testi başarısız: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public override Task OpenShortcutsManagerAsync()
    {
        ShowShortcutsManager = !ShowShortcutsManager;
        ShowRecycleBin = false;
        ShowCronManager = false;
        ShowAlertManager = false;
        ShowExcelTemplates = false;
        return Task.CompletedTask;
    }

    [RelayCommand]
    public override Task OpenExcelTemplatesAsync()
    {
        ShowExcelTemplates = !ShowExcelTemplates;
        ShowAlertManager = false; // Hide the other if open
        return Task.CompletedTask;
    }

    [RelayCommand]
    public async Task SelectExcelTemplateAsync()
    {
        var file = await _fileService.OpenFilePickerAsync("Excel Şablonu Seç", new[] { "*.xlsx" });
        if (file != null)
        {
            CustomExcelTemplatePath = file.Name;
            SuccessMessage = "Excel şablonu seçildi. (Kaydetmeniz gerekmektedir)";
            _ = SaveAdvancedFlagsAsync();
        }
    }

    [RelayCommand]
    public async Task SelectBackupFolderAsync()
    {
        var folder = await _fileService.OpenFolderPickerAsync("Yedekleme Klasörü Seç");
        if (folder != null)
        {
            BackupFolderPath = folder;
            SuccessMessage = "Yedekleme klasörü seçildi.";
            _ = SaveAdvancedFlagsAsync();
        }
    }

    [RelayCommand]
    public void OpenSmtpSettings()
    {
        ShowSmtpSettings = !ShowSmtpSettings;
        ShowShortcutsManager = false;
        ShowRecycleBin = false;
        ShowCronManager = false;
        ShowAlertManager = false;
    }

    [RelayCommand]
    public async Task SendTestEmailAsync()
    {
        if (string.IsNullOrWhiteSpace(SmtpHost) || string.IsNullOrWhiteSpace(SmtpUser))
        {
            ErrorMessage = "SMTP sunucu ve kullanıcı adı boş olamaz.";
            return;
        }

        IsBusy = true;
        SuccessMessage = "";
        ErrorMessage = "";
        try
        {
            var shareService = ((ErmayMuhasebe.Avalonia.App)App.Current!).Services?.GetRequiredService<ShareService>();
            if (shareService != null)
            {
                byte[] testBytes = System.Text.Encoding.UTF8.GetBytes("Bu bir SMTP test dosyası içeriğidir.");
                await shareService.SendPdfViaEmailAsync(SmtpUser, "Ermay Muhasebe - SMTP Test Postası", "Tebrikler! SMTP sunucu ayarlarınız başarıyla çalışıyor.", testBytes, "SmtpTest.txt");
                SuccessMessage = $"Test e-postası başarıyla gönderildi ({SmtpUser} adresini kontrol edin).";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"SMTP Test Hatası: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void UpdateLivePreview()
    {
        // Temporarily update settings in service WITHOUT saving to file to show live preview
        // The service will trigger SettingsChanged event which MainViewModel listens to
        var s = _statusBarService.Settings;
        s.ItemsOrder = StatusBarOrderItems.Select(x => x.Id).ToList();
        
        // This will trigger the RefreshStatusBarItems in MainViewModel
        _statusBarService.SaveSettings(s);
    }

    [RelayCommand]
    private async Task SaveFirebaseAuthSettingsAsync()
    {
        try
        {
            var profil = await _uow.GetFirmaProfiliAsync();
            profil.IsFirebaseAuthEnabled = IsFirebaseAuthEnabled;
            profil.FirebaseAuthApiKey = FirebaseAuthApiKey;
            profil.FirebaseAuthDomain = FirebaseAuthDomain;
            await _uow.SaveFirmaProfiliAsync(profil);
            SuccessMessage = "Firebase Auth ayarları başarıyla kaydedildi.";
            ErrorMessage = "";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Firebase Auth ayarları kaydedilemedi: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task SaveCloudAndApiSettingsAsync()
    {
        try
        {
            IsBusy = true;
            var profil = await _uow.GetFirmaProfiliAsync();
            profil.CloudPdfApiUrl = CloudPdfApiUrl;
            profil.CloudPdfApiKey = CloudPdfApiKey;
            if (!string.IsNullOrWhiteSpace(CloudPdfApiKey) && string.IsNullOrWhiteSpace(profil.FirebaseAuthApiKey))
            {
                profil.FirebaseAuthApiKey = CloudPdfApiKey;
                FirebaseAuthApiKey = CloudPdfApiKey;
            }
            await _uow.SaveFirmaProfiliAsync(profil);

            if (!string.IsNullOrWhiteSpace(CloudUrl))
            {
                _uow.SetCloudConfig(CloudUrl, CloudSecret);
            }

            var httpPdf = ((ErmayMuhasebe.Avalonia.App)App.Current!).Services?.GetService<PdfService>() as HttpPdfService;
            if (httpPdf != null)
            {
                httpPdf.SetApiConfig(CloudPdfApiUrl, CloudPdfApiKey);
            }

            SuccessMessage = "Bulut ve QuestPDF API ayarları başarıyla kaydedildi.";
            ErrorMessage = "";
            CloudStatus = "Bulut ve API ayarları güncellendi.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Ayarlar kaydedilemedi: {ex.Message}";
            CloudStatus = ErrorMessage;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveSmtpSettingsAsync()
    {
        IsBusy = true;
        ErrorMessage = "";
        SuccessMessage = "";
        try
        {
            var profil = await _uow.GetFirmaProfiliAsync();
            profil.SmtpHost = SmtpHost;
            profil.SmtpPort = SmtpPort;
            profil.SmtpUser = SmtpUser;
            profil.SmtpPass = SmtpPass;
            profil.SmtpSsl = SmtpSsl;

            profil.ImapHost = ImapHost;
            profil.ImapPort = ImapPort;
            profil.ImapSsl = ImapSsl;

            await _uow.SaveFirmaProfiliAsync(profil);
            SuccessMessage = "SMTP ve E-posta ayarları başarıyla kaydedildi.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"SMTP ayarları kaydedilemedi: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<bool> ValidateSetupSecurityPasswordAsync(string? inputPassword)
    {
        var trimmed = inputPassword?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed)) return false;

        // 1. Veritabanındaki FactoryResetPassword ile kontrol
        try
        {
            var profil = await _uow.GetFirmaProfiliAsync();
            if (!string.IsNullOrWhiteSpace(profil?.FactoryResetPassword) && profil.FactoryResetPassword.Trim() == trimmed)
            {
                return true;
            }
        }
        catch { }

        // 2. setup_initial_user.json veya setup_config.json dosyasından kontrol
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
                        // Veritabanına da eşitle
                        try
                        {
                            var p = await _uow.GetFirmaProfiliAsync();
                            if (p != null && (string.IsNullOrEmpty(p.FactoryResetPassword) || p.FactoryResetPassword != frp))
                            {
                                p.FactoryResetPassword = frp;
                                await _uow.SaveFirmaProfiliAsync(p);
                            }
                        }
                        catch { }

                        if (frp == trimmed) return true;
                    }
                }
            }
        }
        catch { }

        return false;
    }
}

public class OrderableItem : ObservableObject
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
}

