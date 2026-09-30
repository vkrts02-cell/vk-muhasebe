using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using ErmayMuhasebe.Avalonia.Messages; 
using CommunityToolkit.Mvvm.Messaging;
using System;
using SVM = ErmayMuhasebe.Shared.ViewModels;
using AVM = ErmayMuhasebe.Avalonia.ViewModels;
using ErmayMuhasebe.Avalonia.Services;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;

namespace ErmayMuhasebe.Avalonia.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ErmayMuhasebe.Services.DisplayService _displayService;
    private readonly ErmayMuhasebe.Repositories.IUnitOfWork _uow;
    private readonly ErmayMuhasebe.Services.DatabaseService _dbService;
    private readonly ErmayMuhasebe.Services.IYearContext _yearContext;
    private readonly ErmayMuhasebe.Services.SecuritySyncService _securitySyncService;

    [ObservableProperty]
    private bool _isAuthenticated = false;

    [ObservableProperty]
    private LoginViewModel? _loginViewModel;

    [ObservableProperty]
    private YearSelectionViewModel? _yearSelectionViewModel;

    [ObservableProperty]
    private object? _activeAuthView;

    [ObservableProperty]
    private double _scaling = 1.0;

    [ObservableProperty]
    private bool _isPaneOpen = false;

    [ObservableProperty]
    private ErmayMuhasebe.Shared.ViewModels.ViewModelBase? _currentPage;

    [ObservableProperty]
    private MenuItemViewModel? _selectedMenuItem;
    
    [ObservableProperty] private string _appVersion = "1.2.5 LTS";
    [ObservableProperty] private string _currentUserName = "Admin";
    [ObservableProperty] private string _currentTheme = "ModernSaaS";

    partial void OnCurrentUserNameChanged(string value)
    {
        RefreshStatusBarItems();
    }

    [ObservableProperty] private string _liveDateTime = "";
    [ObservableProperty] private string _liveSystemUsage = "";
    [ObservableProperty] private string _liveExchangeRates = "";
    [ObservableProperty] private string _syncStatusText = "Bulut Eşitlenemedi";
    [ObservableProperty] private bool _isOnline = true;

    public string ConnectionStatusText => IsOnline ? "Çevrimiçi" : "Çevrimdışı";
    public string ConnectionStatusIcon => IsOnline ? "Wifi" : "WifiWarning";

    partial void OnIsOnlineChanged(bool value)
    {
        OnPropertyChanged(nameof(ConnectionStatusText));
        OnPropertyChanged(nameof(ConnectionStatusIcon));
        RefreshStatusBarItems();

        if (!IsAuthenticated || _yearContext == null || _yearContext.CurrentYear <= 0) return;

        try
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var profil = await _uow.GetFirmaProfiliAsync();
                    if (profil != null && profil.EnableCloudSyncAlert)
                    {
                        await global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            if (value)
                                AddNotification("Ağ Bağlantısı Sağlandı", "Sistem çevrimiçi moduna geçti. Veriler bulut ile otomatik olarak eşitlenecektir.", "Wifi", typeof(DashboardViewModel));
                            else
                                AddNotification("Ağ Bağlantısı Koptu", "İnternet bağlantısı kesildi. Sistem çevrimdışı (offline) modunda çalışıyor.", "WifiWarning", typeof(DashboardViewModel));
                        });
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MainViewModel] OnIsOnlineChanged error: {ex.Message}");
                }
            });
        }
        catch { }
    }

    public string ActiveFiscalYear => $"Çalışma Yılı: {(_yearContext?.CurrentYear > 0 ? _yearContext.CurrentYear : DateTime.Now.Year)}";
    public string DatabaseModeText => $"Yerel Mod: {System.IO.Path.GetFileName(_dbService?.DbPath ?? "")}";

    [ObservableProperty]
    private ObservableCollection<NotificationItem> _notifications = new();

    [ObservableProperty]
    private bool _hasUnreadNotifications = false;

    private string GetNotificationsFilePath()
    {
        var dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ErmayMuhasebe");
        if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
        var safeUser = string.IsNullOrWhiteSpace(CurrentUserName) ? "admin" : CurrentUserName.ToLower().Trim();
        return System.IO.Path.Combine(dir, $"notifications_{safeUser}.json");
    }

    private HashSet<string> _dismissedAlertKeys = new();

    private string GetDismissedAlertsFilePath()
    {
        var dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ErmayMuhasebe");
        if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
        var safeUser = string.IsNullOrWhiteSpace(CurrentUserName) ? "admin" : CurrentUserName.ToLower().Trim();
        return System.IO.Path.Combine(dir, $"dismissed_alerts_{safeUser}.json");
    }

    private void LoadDismissedAlerts()
    {
        try
        {
            var filePath = GetDismissedAlertsFilePath();
            if (System.IO.File.Exists(filePath))
            {
                var json = System.IO.File.ReadAllText(filePath);
                var list = System.Text.Json.JsonSerializer.Deserialize<List<string>>(json);
                if (list != null)
                {
                    _dismissedAlertKeys = new HashSet<string>(list);
                }
            }
        }
        catch { }
    }

    private void SaveDismissedAlerts()
    {
        try
        {
            var filePath = GetDismissedAlertsFilePath();
            var json = System.Text.Json.JsonSerializer.Serialize(_dismissedAlertKeys.ToList());
            System.IO.File.WriteAllText(filePath, json);
        }
        catch { }
    }

    public void LoadSavedNotifications()
    {
        try
        {
            LoadDismissedAlerts();
            var filePath = GetNotificationsFilePath();
            if (System.IO.File.Exists(filePath))
            {
                var json = System.IO.File.ReadAllText(filePath);
                var list = System.Text.Json.JsonSerializer.Deserialize<List<NotificationItem>>(json);
                if (list != null)
                {
                    Notifications.Clear();
                    foreach (var item in list)
                    {
                        Notifications.Add(item);
                    }
                    HasUnreadNotifications = Notifications.Any(n => !n.IsRead);
                }
            }
        }
        catch { }
    }

    public void SaveCurrentNotifications()
    {
        try
        {
            var filePath = GetNotificationsFilePath();
            var json = System.Text.Json.JsonSerializer.Serialize(Notifications.Take(50).ToList());
            System.IO.File.WriteAllText(filePath, json);
        }
        catch { }
    }

    [RelayCommand]
    public void MarkAllAsRead()
    {
        foreach (var item in Notifications)
        {
            item.IsRead = true;
        }
        HasUnreadNotifications = false;
        SaveCurrentNotifications();
    }

    [RelayCommand]
    public void ClearNotifications()
    {
        foreach (var n in Notifications)
        {
            _dismissedAlertKeys.Add($"{n.Title}_{DateTime.Today:yyyyMMdd}");
        }
        Notifications.Clear();
        HasUnreadNotifications = false;
        SaveCurrentNotifications();
        SaveDismissedAlerts();
    }

    [RelayCommand]
    public void DeleteNotification(NotificationItem item)
    {
        if (item != null)
        {
            _dismissedAlertKeys.Add($"{item.Title}_{DateTime.Today:yyyyMMdd}");
            Notifications.Remove(item);
            HasUnreadNotifications = Notifications.Any(n => !n.IsRead);
            SaveCurrentNotifications();
            SaveDismissedAlerts();
        }
    }

    [RelayCommand]
    public void MarkAsRead(NotificationItem item)
    {
        if (item != null)
        {
            item.IsRead = true;
            HasUnreadNotifications = Notifications.Any(n => !n.IsRead);
            SaveCurrentNotifications();
        }
    }

    [RelayCommand]
    public void NotificationClick(NotificationItem item)
    {
        if (item == null) return;
        
        MarkAsRead(item);
        
        if (item.TargetPageType != null)
        {
            NavigateTo(item.TargetPageType, true);
            
            var menuItem = MenuItems.FirstOrDefault(m => m.ModelType == item.TargetPageType);
            if (menuItem != null)
            {
                SelectedMenuItem = menuItem;
            }
        }
    }

    public void AddNotification(string title, string message, string icon, Type? targetPageType = null)
    {
        // Kullanıcı bu bildirimi bugün temizlediyse tekrar çıkarma
        var alertKey = $"{title}_{DateTime.Today:yyyyMMdd}";
        if (_dismissedAlertKeys.Contains(alertKey)) return;

        // Başlık bazında kontrol et: Zaten listede varsa ve okunmuşsa, tekrar unread yapma
        var existing = Notifications.FirstOrDefault(n => n.Title == title);
        if (existing != null)
        {
            if (existing.Message != message)
            {
                existing.Message = message;
                existing.Timestamp = DateTime.Now;
                SaveCurrentNotifications();
            }
            return;
        }

        var item = new NotificationItem(title, message, icon, targetPageType);
        Notifications.Insert(0, item);
        HasUnreadNotifications = true;
        SaveCurrentNotifications();

        try
        {
            if (OperatingSystem.IsWindows())
            {
                _ = System.Threading.Tasks.Task.Run(async () =>
                {
                    try
                    {
                        var profil = await _uow.GetFirmaProfiliAsync();
                        if (profil != null && profil.EnableWindowsToastAlert)
                        {
                            ShowWindowsNotification(title, message);
                        }
                    }
                    catch { }
                });
            }
        }
        catch { }
    }

    private void ShowWindowsNotification(string title, string message)
    {
        try
        {
            var escapedTitle = title.Replace("\"", "`\"");
            var escapedMessage = message.Replace("\"", "`\"");
            var psCommand = $"[void] [System.Reflection.Assembly]::LoadWithPartialName('System.Windows.Forms'); " +
                            $"$n = New-Object System.Windows.Forms.NotifyIcon; " +
                            $"$n.Icon = [System.Drawing.SystemIcons]::Information; " +
                            $"$n.BalloonTipIcon = 'Info'; " +
                            $"$n.BalloonTipTitle = \"{escapedTitle}\"; " +
                            $"$n.BalloonTipText = \"{escapedMessage}\"; " +
                            $"$n.Visible = $true; " +
                            $"$n.ShowBalloonTip(5000); " +
                            $"Start-Sleep -s 6; " +
                            $"$n.Dispose();";

            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -WindowStyle Hidden -Command \"{psCommand}\"",
                CreateNoWindow = true,
                UseShellExecute = false
            };
            System.Diagnostics.Process.Start(startInfo);
        }
        catch { }
    }

    [ObservableProperty] private ObservableCollection<StatusBarItemViewModel> _statusBarItems = new();

    public ObservableCollection<MenuItemViewModel> MenuItems { get; } = new()
    {
        new MenuItemViewModel(typeof(DashboardViewModel), "Ana Menü", "Home"),
        new MenuItemViewModel(typeof(CariListViewModel), "Cari Hesaplar", "People"),
        new MenuItemViewModel(typeof(StokListViewModel), "Stok Kartları", "Box"),
        new MenuItemViewModel(typeof(FaturaListViewModel), "Faturalar", "Document"),
        new MenuItemViewModel(typeof(SVM.FinansViewModel), "Finans", "Money"), 
        new MenuItemViewModel(typeof(VadeTakipViewModel), "Vade Takip", "CalendarClock"),
        new MenuItemViewModel(typeof(RaporListViewModel), "Raporlar", "ChartMultiple"),
        new MenuItemViewModel(typeof(SiparisListViewModel), "Siparişler", "ClipboardText"),
        new MenuItemViewModel(typeof(TeklifListViewModel), "Teklifler", "Tag"),
        new MenuItemViewModel(typeof(KanbanViewModel), "Görev Panosu", "Board"),
        new MenuItemViewModel(typeof(MaliyetHesaplamaViewModel), "Hesap Makinesi", "Calculator"),
        new MenuItemViewModel(typeof(MusteriTakipViewModel), "Müşteri Takip", "PeopleSearch"),
        new MenuItemViewModel(typeof(ToolsViewModel), "Araçlar", "Wrench"),
        new MenuItemViewModel(typeof(SettingsViewModel), "Ayarlar", "Settings"),
    };



    [ObservableProperty]
    private StatusBarService _statusService;

    public StatusBarSettings CurrentSettings => StatusService.Settings;

    private readonly ErmayMuhasebe.Services.DovizService _dovizService;
    private DispatcherTimer? _cronTimer;
    private readonly SemaphoreSlim _syncSemaphore = new(1, 1);
    private readonly DispatcherTimer _liveUpdateTimer;

    public MainViewModel(
        IServiceProvider serviceProvider, 
        ErmayMuhasebe.Services.DisplayService displayService, 
        ErmayMuhasebe.Repositories.IUnitOfWork uow, 
        ErmayMuhasebe.Services.ThemeService themeService, 
        StatusBarService statusBarService, 
        ErmayMuhasebe.Services.DovizService dovizService,
        ErmayMuhasebe.Services.SecuritySyncService securitySyncService)
    {
        _serviceProvider = serviceProvider;
        _displayService = displayService;
        _uow = uow;
        _statusService = statusBarService;
        _dovizService = dovizService;
        _securitySyncService = securitySyncService;
        _dbService = _serviceProvider.GetRequiredService<ErmayMuhasebe.Services.DatabaseService>();
        _yearContext = _serviceProvider.GetRequiredService<ErmayMuhasebe.Services.IYearContext>();
        _yearContext.YearChanged += (newYear) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                OnPropertyChanged(nameof(ActiveFiscalYear));
                OnPropertyChanged(nameof(DatabaseModeText));
                RefreshStatusBarItems();
            });
        };
        
        themeService.ThemeChanged += (t) => CurrentTheme = t.ToString();
        CurrentTheme = themeService.CurrentTheme.ToString();
        
        _displayService.ScalingChanged += (s) => Scaling = s;
        Scaling = _displayService.Scaling;

        // Force UI update on settings changed
        _statusService.SettingsChanged += () => {
            OnPropertyChanged(nameof(StatusService));
            OnPropertyChanged(nameof(CurrentSettings));
            // Trigger refresh for all elements that depend on service settings
            OnPropertyChanged("CurrentSettings.ShowVersion");
            OnPropertyChanged("CurrentSettings.ShowUser");
            OnPropertyChanged("CurrentSettings.ShowExchangeRates");
            OnPropertyChanged("CurrentSettings.ShowDateTime");
            OnPropertyChanged("CurrentSettings.ShowSystemUsage");
            OnPropertyChanged("CurrentSettings.CustomLeftText");
            OnPropertyChanged("CurrentSettings.CustomRightText");
            RefreshStatusBarItems();
        };

        RefreshStatusBarItems();

        // Initialize Live Update Timer
        _liveUpdateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _liveUpdateTimer.Tick += async (s, e) => await UpdateLiveStatusBarInfo();
        _liveUpdateTimer.Start();

        LiveDateTime = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss");
        _ = UpdateExchangeRatesAsync();

        var session = _serviceProvider.GetRequiredService<ErmayMuhasebe.Services.SessionService>();
        session.OnTimeout += () => Logout();
// ... (rest of constructor)

        WeakReferenceMessenger.Default.Register<NavigationRequestMessage>(this, (r, m) => 
        {
            session.ResetActivity();
            NavigateTo(m.Value, true);
        });

        WeakReferenceMessenger.Default.Register<LogoutMessage>(this, (r, m) => 
        {
            Logout();
        });

        WeakReferenceMessenger.Default.Register<NavigateViewModelMessage>(this, (r, m) => 
        { 
            session.ResetActivity();
            if (m.Value != null)
            {
                // Dashboard veya ana menü öğeleri ana pencerede kalır.
                // Masaüstünde değilsek her şey ana pencerede kalır.
                if (IsMainModule(m.Value) || global::Avalonia.Application.Current?.ApplicationLifetime is not global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)
                {
                    CurrentPage = m.Value;
                }
            }
        });

        // Initialize Login ViewModel
        var db = _serviceProvider.GetRequiredService<ErmayMuhasebe.Services.DatabaseService>();
        var yearCtx = _serviceProvider.GetRequiredService<ErmayMuhasebe.Services.IYearContext>();
        var dataProvider = _serviceProvider.GetRequiredService<ErmayMuhasebe.Repositories.DataProviders.IDataProvider>();

        LoginViewModel = new LoginViewModel(db, (username) => 
        {
            if (!string.IsNullOrEmpty(username))
            {
                // Set CurrentUserName from LoginViewModel parameter
                CurrentUserName = username;

                if (db.IsTestMode)
                {
                    int selectedYear = DateTime.Now.Year;
                    CompleteLoginWithYear(selectedYear, session);
                    return;
                }

                // Transition to Year Selection view
                ShowYearSelection(session);
            }
        });

        ActiveAuthView = LoginViewModel;

        System.Net.NetworkInformation.NetworkChange.NetworkAddressChanged += async (s, e) => 
        {
            await TriggerInternetCheckAsync();
        };
    }

    [ObservableProperty]
    private bool _isOffline;

    private async System.Threading.Tasks.Task StartAutoSync()
    {
        while(true)
        {
            if (!IsAuthenticated || _dbService == null || _dbService.IsClosed || !_dbService.IsCloudConnected)
            {
                SyncStatusText = !IsAuthenticated ? "" : "Bulut Devre Dışı";
                IsOffline = false;
                await System.Threading.Tasks.Task.Delay(2000);
                continue;
            }

            try
            {
                if (_lastInternetStatus)
                {
                    SyncStatusText = "Bulut Eşitleniyor...";
                    await _uow.SyncFromCloudAsync();
                    SyncStatusText = "Bulut Eşitlendi";
                    
                    if (IsOffline)
                    {
                        IsOffline = false;
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await _uow.SyncToCloudAsync();
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"[MainViewModel] Background SyncToCloudAsync error: {ex.Message}");
                            }
                        });
                    }
                }
                else
                {
                    SyncStatusText = "Bulut Eşitlenemedi";
                    IsOffline = false;
                }
            }
            catch (Exception ex)
            {
                SyncStatusText = "Bulut Eşitlenemedi";
                if (!IsOffline)
                {
                    IsOffline = true;
                    System.Diagnostics.Debug.WriteLine("Sync Error (Offline Mode): " + ex.Message);
                }
            }
            
            await System.Threading.Tasks.Task.Delay(2000); // Ultra responsive live sync (2 sec)
        }
    }

    private async System.Threading.Tasks.Task RunAlertChecksAsync()
    {
        try
        {
            var profil = await _uow.GetFirmaProfiliAsync();

            // 1. Düşük Stok Uyarısı
            if (profil.EnableLowStockAlert)
            {
                var stoklar = await _uow.Stoklar.GetAllAsync();
                var kritikStoklar = stoklar.Where(s => !s.IsDeleted && s.Miktar < profil.LowStockThreshold).ToList();
                if (kritikStoklar.Any())
                {
                    AddNotification("Düşük Stok Uyarısı", $"{kritikStoklar.Count} adet ürünün stoku kritik seviyenin ({profil.LowStockThreshold} Adet) altına düştü!", "Box", typeof(StokListViewModel));
                }
            }

            // 2. Negatif Stok Uyarısı
            if (profil.EnableNegativeStockAlert)
            {
                var stoklar = await _uow.Stoklar.GetAllAsync();
                var negatifStoklar = stoklar.Where(s => !s.IsDeleted && s.Miktar < 0).ToList();
                if (negatifStoklar.Any())
                {
                    AddNotification("Negatif Stok Uyarısı", $"{negatifStoklar.Count} adet ürünün stoku sıfırın altına (eksi bakiye) düştü!", "Box", typeof(StokListViewModel));
                }
            }

             // 4. Vadesi Geçen Alacaklar
            if (profil.EnableOverdueAlert)
            {
                var faturalar = await _uow.Faturalar.GetAllAsync();
                var overdueFaturalar = faturalar.Where(f => !f.IsDeleted && f.OdemeSekli == "Açık Hesap" && f.VadeTarihi < DateTime.Now && f.Kalan > 0).ToList();
                if (overdueFaturalar.Any())
                {
                    AddNotification("Geciken Alacak Uyarısı", $"{overdueFaturalar.Count} adet açık hesap faturanın ödeme vadesi geçti!", "Alert", typeof(VadeTakipViewModel));
                }
            }

            // 5. Vade Yaklaşma Uyarısı (Eşik Gününe Göre)
            if (profil.EnableOverdueAlert && profil.OverdueDaysThreshold > 0)
            {
                var faturalar = await _uow.Faturalar.GetAllAsync();
                var limitDate = DateTime.Now.AddDays(profil.OverdueDaysThreshold);
                var yaklasanFaturalar = faturalar.Where(f => !f.IsDeleted && f.OdemeSekli == "Açık Hesap" && f.VadeTarihi >= DateTime.Now && f.VadeTarihi <= limitDate && f.Kalan > 0).ToList();
                if (yaklasanFaturalar.Any())
                {
                    AddNotification("Yaklaşan Fatura Vadesi", $"{yaklasanFaturalar.Count} adet faturanın vadesine {profil.OverdueDaysThreshold} günden az kaldı!", "CalendarClock", typeof(VadeTakipViewModel));
                }
            }

            // 6. Cari Risk Limiti Aşımları
            if (profil.EnableRiskLimitAlert)
            {
                var cariler = await _uow.Cariler.GetAllAsync();
                var limitiAsanlar = cariler.Where(c => !c.IsDeleted && c.RiskLimiti > 0 && (c.Borc - c.Alacak) > c.RiskLimiti).ToList();
                if (limitiAsanlar.Any())
                {
                    AddNotification("Risk Limiti Aşımı", $"{limitiAsanlar.Count} adet carinin borcu tanımlı risk limitini aştı!", "ShieldLock", typeof(CariListViewModel));
                }
            }

            // 7. Çek / Senet Vade Uyarısı
            if (profil.EnableChequeSenetAlert)
            {
                var cekler = await _uow.Cekler.GetAllAsync();
                var senetler = await _uow.Senetler.GetAllAsync();
                var ikiGunSonra = DateTime.Now.AddDays(2);

                var yaklasanCekler = cekler.Where(c => c.VadeTarihi >= DateTime.Now && c.VadeTarihi <= ikiGunSonra && c.Durum != "Tahsil Edildi" && c.Durum != "Ödendi").ToList();
                var yaklasanSenetler = senetler.Where(s => s.VadeTarihi >= DateTime.Now && s.VadeTarihi <= ikiGunSonra && s.Durum != "Tahsil Edildi" && s.Durum != "Ödendi").ToList();

                int toplamEvrak = yaklasanCekler.Count + yaklasanSenetler.Count;
                if (toplamEvrak > 0)
                {
                    AddNotification("Evrak Vade Hatırlatıcısı", $"{toplamEvrak} adet çek/senedin vadesine 2 gün veya daha az kaldı!", "CalendarClock", typeof(VadeTakipViewModel));
                }
            }

            // 8. Bakiye Yaşlandırma Bildirimi
            if (profil.EnableAgingDebtAlert)
            {
                var faturalar = await _uow.Faturalar.GetAllAsync();
                var otuzGunOnce = DateTime.Now.AddDays(-30);
                var yasliFaturalar = faturalar.Where(f => !f.IsDeleted && f.OdemeSekli == "Açık Hesap" && f.VadeTarihi < otuzGunOnce && f.Kalan > 0).ToList();
                if (yasliFaturalar.Any())
                {
                    AddNotification("Yaşlandırılmış Borç Uyarısı", $"{yasliFaturalar.Count} adet faturanın ödeme vadesi 30 günden fazla gecikti!", "Alert", typeof(VadeTakipViewModel));
                }
            }

            // 9. Gün Sonu / Giriş Özeti
            if (profil.EnableDailySummaryAlert)
            {
                var faturalar = await _uow.Faturalar.GetAllAsync();
                var bugunFaturalar = faturalar.Where(f => !f.IsDeleted && f.Tarih.Date == DateTime.Today).ToList();
                decimal bugunSatis = bugunFaturalar.Where(f => f.Tur == "Satış").Sum(f => f.GenelToplam);
                decimal bugunTahsilat = bugunFaturalar.Sum(f => f.GenelToplam - f.Kalan);

                decimal toplamKasa = await _uow.Kasalar.GetBakiyeAsync();
                var bankalar = await _uow.Bankalar.GetAllAsync();
                decimal toplamBanka = bankalar.Sum(b => b.Bakiye);

                AddNotification("Günlük Finansal Özet", $"Satış: {bugunSatis:N2} TL | Tahsilat: {bugunTahsilat:N2} TL | Kasa/Banka: {(toplamKasa + toplamBanka):N2} TL", "Money", typeof(SVM.FinansViewModel));
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Alert Error: " + ex.Message);
        }
    }

    private async System.Threading.Tasks.Task RunAutoBackupWithNotificationAsync()
    {
        try
        {
            var profil = await _uow.GetFirmaProfiliAsync();
            var backupService = _serviceProvider.GetRequiredService<ErmayMuhasebe.Services.BackupService>();
            
            var result = await backupService.RunAutoBackupAsync();
            if (result == true && profil.EnableBackupAlert)
            {
                AddNotification("Otomatik Yedekleme", "Günlük otomatik veritabanı yedeklemesi başarıyla tamamlandı.", "Database", typeof(SettingsViewModel));
            }
        }
        catch (Exception ex)
        {
            var profil = await _uow.GetFirmaProfiliAsync();
            if (profil.EnableBackupAlert)
            {
                AddNotification("Yedekleme Başarısız", $"Otomatik yedekleme işlemi sırasında bir hata oluştu: {ex.Message}", "Database", typeof(SettingsViewModel));
            }
        }
    }

    private void StartCronTimer()
    {
        _cronTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(1)
        };
        _cronTimer.Tick += async (s, e) => await RunCronJobsAsync();
        _cronTimer.Start();
    }

    private async System.Threading.Tasks.Task RunCronJobsAsync()
    {
        try
        {
            // Simple cron: Check if time is 18:00 for backup
            if (DateTime.Now.Hour == 18 && DateTime.Now.Minute == 0)
            {
                var backupService = _serviceProvider.GetRequiredService<ErmayMuhasebe.Services.BackupService>();
                // Not running it for now because CreateBackupAsync needs a path
                // await backupService.CreateBackupAsync(null);
            }
        }
        catch { }
    }

    private System.Collections.Generic.Stack<ErmayMuhasebe.Shared.ViewModels.ViewModelBase> _history = new();
    private bool _isNavigatingBack;

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    public void GoBack()
    {
        if (CurrentPage is IHandleBack handler && handler.HandleBack())
        {
            return;
        }

        if (_history.Count > 0)
        {
            _isNavigatingBack = true;
            CurrentPage = _history.Pop();
            _isNavigatingBack = false;
        }
        else if (CurrentPage != null && CurrentPage.GetType() != typeof(DashboardViewModel))
        {
            // Modül kökündeysek ESC ile Dashboard'a dön
            NavigateTo(typeof(DashboardViewModel), true);
        }
    }

    partial void OnCurrentPageChanging(ErmayMuhasebe.Shared.ViewModels.ViewModelBase? value)
    {
        // Geri gitmiyorsak ve sayfa gerçekten değişiyorsa geçmişe at
        if (!_isNavigatingBack && CurrentPage != null && value != null && value != CurrentPage)
        {
            // Aynı tipte sayfalar arası geçişte (örn. menüden tekrar tıklama) yığına ekleme
            if (value.GetType() == CurrentPage.GetType()) return;

            _history.Push(CurrentPage);
            
            if (_history.Count > 20)
            {
                var temp = _history.ToArray().Take(20).Reverse();
                _history = new System.Collections.Generic.Stack<ErmayMuhasebe.Shared.ViewModels.ViewModelBase>(temp);
            }
        }
    }

    partial void OnCurrentPageChanged(ErmayMuhasebe.Shared.ViewModels.ViewModelBase? value)
    {
        value?.OnNavigatedTo();
    }

    partial void OnSelectedMenuItemChanged(MenuItemViewModel? value)
    {
        if (value == null || value.IsHeader) return;
        
        NavigateTo(value.ModelType, true); // Menüden tıklandığında geçmişi temizle
    }

    public void NavigateTo(System.Type? viewModelType, bool isRoot = false)
    {
        if (viewModelType == null) return;
        
        if (isRoot)
        {
            _isNavigatingBack = true;
            _history.Clear();
        }

        var vm = _serviceProvider.GetRequiredService(viewModelType) as ErmayMuhasebe.Shared.ViewModels.ViewModelBase;
        if (vm != null)
        {
            if (isRoot || IsMainModule(vm) || global::Avalonia.Application.Current?.ApplicationLifetime is not global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)
            {
                CurrentPage = vm;
            }
            else
            {
                // Masaüstünde ve root olmayan (alt sayfa/detay) modülleri pencere olarak aç
                WeakReferenceMessenger.Default.Send(new NavigateViewModelMessage(vm));
            }
        }

        if (isRoot)
        {
            _isNavigatingBack = false;
        }
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void Logout()
    {
        _securitySyncService.StopListeners();
        _dbService?.StopRealtimeSync();
        _ = _dbService?.CloseConnectionAsync();
        _yearContext.CurrentYear = 0;
        IsAuthenticated = false;
        ActiveAuthView = LoginViewModel;
        
        // Reset CurrentUserName
        CurrentUserName = "Admin";
        
        // Reset CurrentTenantId back to default on logout
        var db = _serviceProvider.GetRequiredService<ErmayMuhasebe.Services.DatabaseService>();
        if (db != null)
        {
            db.CurrentTenantId = "default";
        }
        
        if (LoginViewModel != null)
        {
            if (LoginViewModel.RememberMe)
            {
                LoginViewModel.LoadSavedCredentials();
            }
            else
            {
                LoginViewModel.Password = "";
            }
        }
    }

    public void ShowYearSelection(ErmayMuhasebe.Services.SessionService? session = null)
    {
        var sess = session ?? _serviceProvider.GetRequiredService<ErmayMuhasebe.Services.SessionService>();
        var yearCtx = _serviceProvider.GetRequiredService<ErmayMuhasebe.Services.IYearContext>();
        var dataProvider = _serviceProvider.GetRequiredService<ErmayMuhasebe.Repositories.DataProviders.IDataProvider>();
        var db = _serviceProvider.GetRequiredService<ErmayMuhasebe.Services.DatabaseService>();

        YearSelectionViewModel = new YearSelectionViewModel(
            yearCtx,
            dataProvider,
            db,
            onYearSelected: (selectedYear) =>
            {
                CompleteLoginWithYear(selectedYear, sess);
            },
            onBackToLogin: () =>
            {
                if (IsAuthenticated)
                {
                    ActiveAuthView = this;
                }
                else
                {
                    ActiveAuthView = LoginViewModel;
                }
            },
            isAlreadyAuthenticated: IsAuthenticated);

        ActiveAuthView = YearSelectionViewModel;
    }

    public void RefreshFiscalYearInfo()
    {
        OnPropertyChanged(nameof(ActiveFiscalYear));
        OnPropertyChanged(nameof(DatabaseModeText));
        RefreshStatusBarItems();
    }

    [RelayCommand]
    public void SwitchFiscalYear()
    {
        ShowYearSelection();
    }

    private void CompleteLoginWithYear(int selectedYear, ErmayMuhasebe.Services.SessionService session)
    {
        _yearContext.CurrentYear = selectedYear;

        IsAuthenticated = true;
        ActiveAuthView = this;
        SelectedMenuItem = MenuItems.First();
        OnPropertyChanged(nameof(ActiveFiscalYear));
        OnPropertyChanged(nameof(DatabaseModeText));
        RefreshStatusBarItems();

        if (CurrentPage is ErmayMuhasebe.Avalonia.ViewModels.DashboardViewModel dbVm)
        {
            _ = dbVm.LoadStatsAsync();
        }
        else
        {
            try
            {
                var newDbVm = _serviceProvider.GetRequiredService<DashboardViewModel>();
                CurrentPage = newDbVm;
                _ = newDbVm.LoadStatsAsync();
            }
            catch { }
        }

        // Kullanıcının kayıtlı ve okunmuş/silinmiş bildirim durumunu diskten yükle
        LoadSavedNotifications();
        AddNotification("Giriş Yapıldı", $"{CurrentUserName} kullanıcısı {selectedYear} yılına başarıyla giriş yaptı.", "Person", typeof(DashboardViewModel));
        
        // Start Services after login & year selection
        _dbService.StartRealtimeSync();
        _ = StartAutoSync();
        _ = RunAutoBackupWithNotificationAsync();
        
        _ = RunAlertChecksAsync();
        
        // Start Cron Timer
        StartCronTimer();
        
        // Start Session Monitor (30 min)
        session.Start(30);

        // Start Session Status Listener
        _securitySyncService.StopListeners();
        _securitySyncService.OnSessionRevoked += () =>
        {
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                Logout();
                AddNotification("Güvenlik Bildirimi", "Şifreniz başka bir cihazdan değiştirildiği için bu cihazdaki oturumunuz güvenlik nedeniyle sonlandırıldı.", "ShieldLock", typeof(DashboardViewModel));
            });
        };
        _securitySyncService.ListenToSessionStatus(CurrentUserName, DateTime.UtcNow);
    }

    private bool IsMainModule(object vm)
    {
        if (vm == null) return false;
        var type = vm.GetType();
        // MenuItems içindeki tipler veya List ile bitenler ana modüldür
        return MenuItems.Any(m => m.ModelType == type) || type.Name.EndsWith("ListViewModel") || type.Name == "DashboardViewModel";
    }

    private int _internetCheckCounter = 0;
    private bool _lastInternetStatus = true;
    private bool _isCheckingInternet = false;

    private async Task<bool> CheckInternetConnectionAsync()
    {
        try
        {
            if (!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
                return false;

            using (var client = new System.Net.Sockets.TcpClient())
            {
                using (var cts = new System.Threading.CancellationTokenSource(2000))
                {
                    await client.ConnectAsync("8.8.8.8", 53, cts.Token);
                    return client.Connected;
                }
            }
        }
        catch
        {
            return false;
        }
    }

    public async Task TriggerInternetCheckAsync()
    {
        if (_isCheckingInternet) return;
        _isCheckingInternet = true;
        try
        {
            _lastInternetStatus = await CheckInternetConnectionAsync();
            IsOnline = _lastInternetStatus;
            
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() => 
            {
                RefreshStatusBarItems();
            });
        }
        catch { }
        finally
        {
            _isCheckingInternet = false;
        }
    }

    private async System.Threading.Tasks.Task UpdateLiveStatusBarInfo()
    {
        try
        {
            LiveDateTime = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss");

            if (CurrentSettings.ShowSystemUsage)
            {
                try
                {
                    var process = System.Diagnostics.Process.GetCurrentProcess();
                    var ramUsage = process.PrivateMemorySize64 / 1024 / 1024; // MB
                    LiveSystemUsage = $"RAM: {ramUsage} MB";
                }
                catch { }
            }

            // Update exchange rates every 60 seconds (or if empty / placeholder)
            if (CurrentSettings.ShowExchangeRates && (DateTime.Now.Second == 0 || string.IsNullOrEmpty(LiveExchangeRates) || LiveExchangeRates.Contains("Yükleniyor")))
            {
                _ = UpdateExchangeRatesAsync();
            }

            // Internet Status Check (every 5 seconds or first tick) without blocking UI timer
            _internetCheckCounter++;
            if ((_internetCheckCounter >= 5 || _internetCheckCounter == 1) && !_isCheckingInternet)
            {
                if (_internetCheckCounter >= 5) _internetCheckCounter = 1;
                _ = TriggerInternetCheckAsync();
            }

            // Update Online Status
            IsOnline = _lastInternetStatus;

            // Refresh text in items
            RefreshStatusBarItems();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] Live Status Update Exception: {ex.Message}");
        }
    }

    private bool _isUpdatingRates = false;
    private async System.Threading.Tasks.Task UpdateExchangeRatesAsync()
    {
        if (_isUpdatingRates) return;
        _isUpdatingRates = true;
        try
        {
            if (string.IsNullOrEmpty(LiveExchangeRates))
            {
                LiveExchangeRates = "USD: Yükleniyor...";
            }

            var usd = await _dovizService.GetLiveRateAsync("USD");
            var eur = await _dovizService.GetLiveRateAsync("EUR");

            if (usd > 0 && eur > 0)
            {
                LiveExchangeRates = $"USD: {usd:N2} ₺ | EUR: {eur:N2} ₺";
            }
            else if (usd > 0)
            {
                LiveExchangeRates = $"USD: {usd:N2} ₺";
            }
            else if (eur > 0)
            {
                LiveExchangeRates = $"EUR: {eur:N2} ₺";
            }

            RefreshStatusBarItems();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] Exchange rate update exception: {ex.Message}");
        }
        finally
        {
            _isUpdatingRates = false;
        }
    }

    [ObservableProperty] private bool _isStatusBarEditMode = false;
    private List<string>? _pendingItemsOrder;

    public void RefreshStatusBarItems()
    {
        var items = new List<StatusBarItemViewModel>();
        var settings = CurrentSettings;
        var order = IsStatusBarEditMode && _pendingItemsOrder != null ? _pendingItemsOrder : settings.ItemsOrder;

        foreach (var itemId in order)
        {
            double itemOffsetX = 0;
            if (settings.ItemsMargins != null && settings.ItemsMargins.ContainsKey(itemId))
            {
                itemOffsetX = settings.ItemsMargins[itemId];
            }

            switch (itemId)
            {
                case "Version":
                    if (settings.ShowVersion) items.Add(new StatusBarItemViewModel("Version", AppVersion, "Tag", itemOffsetX));
                    break;
                case "User":
                    if (settings.ShowUser) items.Add(new StatusBarItemViewModel("User", CurrentUserName, "Person", itemOffsetX));
                    break;
                case "SyncStatus":
                    if (settings.ShowSyncStatus) items.Add(new StatusBarItemViewModel("SyncStatus", SyncStatusText, "CloudCheckmark", itemOffsetX));
                    break;
                case "ConnectionStatus":
                    if (settings.ShowConnectionStatus) items.Add(new StatusBarItemViewModel("ConnectionStatus", IsOnline ? "Çevrimiçi" : "Çevrimdışı", IsOnline ? "Wifi" : "WifiWarning", itemOffsetX));
                    break;
                case "ExchangeRates":
                    if (settings.ShowExchangeRates) items.Add(new StatusBarItemViewModel("ExchangeRates", LiveExchangeRates, "Money", itemOffsetX));
                    break;
                case "SystemUsage":
                    if (settings.ShowSystemUsage) items.Add(new StatusBarItemViewModel("SystemUsage", LiveSystemUsage, "Laptop", itemOffsetX));
                    break;
                case "DateTime":
                    if (settings.ShowDateTime) items.Add(new StatusBarItemViewModel("DateTime", LiveDateTime, "Clock", itemOffsetX));
                    break;
                case "Copyright":
                    if (settings.ShowCopyright) items.Add(new StatusBarItemViewModel("Copyright", "ERMAY Bilişim A.Ş. © 2025", "ShieldCheckmark", itemOffsetX));
                    break;
            }
        }

        // Smart update: Avoid replacing the entire collection if not necessary
        // This is crucial for maintaining drag-drop state and animations
        if (StatusBarItems.Count != items.Count)
        {
            StatusBarItems = new ObservableCollection<StatusBarItemViewModel>(items);
        }
        else
        {
            // If count is same, update properties of existing items or reorder them if needed
            // But if we are in EditMode, we let MoveStatusBarItem handle the UI order
            if (!IsStatusBarEditMode)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    if (StatusBarItems[i].Id != items[i].Id)
                    {
                        // Different item at this position, find it and move or replace
                        var existing = StatusBarItems.FirstOrDefault(x => x.Id == items[i].Id);
                        if (existing != null)
                        {
                            int oldIndex = StatusBarItems.IndexOf(existing);
                            StatusBarItems.Move(oldIndex, i);
                        }
                        else
                        {
                            StatusBarItems[i] = items[i];
                        }
                    }
                    
                    // Update text/icon if changed
                    if (StatusBarItems[i].Text != items[i].Text) StatusBarItems[i].Text = items[i].Text;
                    if (StatusBarItems[i].Icon != items[i].Icon) StatusBarItems[i].Icon = items[i].Icon;
                    if (StatusBarItems[i].OffsetX != items[i].OffsetX) StatusBarItems[i].OffsetX = items[i].OffsetX;
                }
            }
            else
            {
                // In edit mode, only update text/icon, don't touch the order (user is managing it)
                foreach (var newItem in items)
                {
                    var existing = StatusBarItems.FirstOrDefault(x => x.Id == newItem.Id);
                    if (existing != null)
                    {
                        if (existing.Text != newItem.Text) existing.Text = newItem.Text;
                        if (existing.Icon != newItem.Icon) existing.Icon = newItem.Icon;
                        if (existing.OffsetX != newItem.OffsetX) existing.OffsetX = newItem.OffsetX;
                    }
                }
            }
        }
    }

    public void MoveStatusBarItem(string draggedId, string targetId)
    {
        if (draggedId == targetId) return;

        if (!IsStatusBarEditMode)
        {
            StartStatusBarEdit();
        }

        if (_pendingItemsOrder != null)
        {
            int oldIndex = _pendingItemsOrder.IndexOf(draggedId);
            int newIndex = _pendingItemsOrder.IndexOf(targetId);

            if (oldIndex != -1 && newIndex != -1)
            {
                _pendingItemsOrder.RemoveAt(oldIndex);
                _pendingItemsOrder.Insert(newIndex, draggedId);
                
                // Optimized update: Move the item in the actual UI collection
                var itemToMove = StatusBarItems.FirstOrDefault(i => i.Id == draggedId);
                var targetItem = StatusBarItems.FirstOrDefault(i => i.Id == targetId);

                if (itemToMove != null && targetItem != null)
                {
                    int currentUiIndex = StatusBarItems.IndexOf(itemToMove);
                    int targetUiIndex = StatusBarItems.IndexOf(targetItem);
                    
                    if (currentUiIndex != -1 && targetUiIndex != -1 && currentUiIndex != targetUiIndex)
                    {
                        var newList = StatusBarItems.ToList();
                        newList.RemoveAt(currentUiIndex);
                        newList.Insert(targetUiIndex, itemToMove);
                        StatusBarItems = new ObservableCollection<StatusBarItemViewModel>(newList);
                    }
                }
                
                // Anında otomatik kaydet: Ayarlar sekmesindeyken sürükle-bırak yapıldığında sola kaymasını önler
                CurrentSettings.ItemsOrder = _pendingItemsOrder.ToList();
                StatusService.SaveSettings(CurrentSettings);
            }
        }
    }

    public void AdjustItemMargin(string draggedId, double deltaX)
    {
        if (CurrentSettings.ItemsMargins == null) CurrentSettings.ItemsMargins = new System.Collections.Generic.Dictionary<string, double>();
        
        double currentOffsetX = CurrentSettings.ItemsMargins.ContainsKey(draggedId) ? CurrentSettings.ItemsMargins[draggedId] : 0;
        double newOffsetX = currentOffsetX + deltaX;
        
        CurrentSettings.ItemsMargins[draggedId] = newOffsetX;
        
        var item = StatusBarItems.FirstOrDefault(i => i.Id == draggedId);
        if (item != null)
        {
            item.OffsetX = newOffsetX;
        }
        
        StatusService.SaveSettings(CurrentSettings);
    }

    [RelayCommand]
    public void StartStatusBarEdit()
    {
        _pendingItemsOrder = CurrentSettings.ItemsOrder.ToList();
        IsStatusBarEditMode = true;
        RefreshStatusBarItems();
    }

    [RelayCommand]
    public void SaveStatusBarEdit()
    {
        if (_pendingItemsOrder != null)
        {
            CurrentSettings.ItemsOrder = _pendingItemsOrder.ToList();
            StatusService.SaveSettings(CurrentSettings);
        }
        IsStatusBarEditMode = false;
        _pendingItemsOrder = null;
        RefreshStatusBarItems();
    }

    [RelayCommand]
    public void CancelStatusBarEdit()
    {
        IsStatusBarEditMode = false;
        _pendingItemsOrder = null;
        RefreshStatusBarItems();
    }
}

public partial class StatusBarItemViewModel : ObservableObject
{
    public string Id { get; }
    [ObservableProperty] private string _text;
    [ObservableProperty] private string _icon;
    [ObservableProperty] private double _offsetX;

    public StatusBarItemViewModel(string id, string text, string icon, double offsetX = 0)
    {
        Id = id;
        _text = text;
        _icon = icon;
        _offsetX = offsetX;
    }
}

public class MenuItemViewModel
{
    public string Name { get; }
    public string IconKey { get; }
    public System.Type? ModelType { get; }
    public bool IsHeader => ModelType == null;

    public MenuItemViewModel(System.Type? modelType, string name, string iconKey)
    {
        ModelType = modelType!;
        Name = name;
        IconKey = iconKey;
    }
}
