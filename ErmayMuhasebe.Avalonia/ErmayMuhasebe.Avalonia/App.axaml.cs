using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using AVM = ErmayMuhasebe.Avalonia.ViewModels;
using SVM = ErmayMuhasebe.Shared.ViewModels;
using ErmayMuhasebe.Avalonia.Views;
using Microsoft.Extensions.DependencyInjection;
using ErmayMuhasebe.Services;
using ErmayMuhasebe.Avalonia.Services;
using ErmayMuhasebe.Repositories.DataProviders;
using System;
using System.Net.Http;
using System.Threading.Tasks;

using ErmayMuhasebe.Avalonia.Messages;
using CommunityToolkit.Mvvm.Messaging;
using FluentIcons.Avalonia.Fluent;
using FluentIcons.Common;

namespace ErmayMuhasebe.Avalonia;

public partial class App : Application, IRecipient<ShowCariDetailMessage>, IRecipient<NavigateViewModelMessage>
{
    public IServiceProvider? Services { get; private set; }

    public App()
    {
        // Initialize SQLite SQLCipher native library before any dependency injection or DB call
        try
        {
            SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_e_sqlcipher());
            SQLitePCL.Batteries_V2.Init();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[App] SQLitePCL Init Exception: {ex.Message}");
        }

        var serviceCollection = new ServiceCollection();
        ConfigureServices(serviceCollection);
        Services = serviceCollection.BuildServiceProvider();
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Utils
        services.AddSingleton<HttpClient>();

        // Business Services
        services.AddSingleton<IYearContext, YearContext>();
        services.AddSingleton<DatabaseService>();
        services.AddSingleton<IFinansService, FinansService>();
        services.AddSingleton<ErmayMuhasebe.Repositories.DataProviders.IDataProvider, ErmayMuhasebe.Repositories.DataProviders.SqliteDataProvider>();
        services.AddSingleton<DovizService>();
        services.AddSingleton<YearRolloverService>();
        if (OperatingSystem.IsIOS() || OperatingSystem.IsAndroid())
        {
            services.AddSingleton<PdfService, HttpPdfService>();
        }
        else
        {
            services.AddSingleton<PdfService>();
        }
        services.AddSingleton<IPdfService>(sp => sp.GetRequiredService<PdfService>());
        services.AddSingleton<ExternalApiService>();
        services.AddSingleton<ThemeService>();
        services.AddSingleton<DisplayService>();
        services.AddSingleton<IFileService, ErmayMuhasebe.Avalonia.Services.FileService>();
        services.AddSingleton<ErmayMuhasebe.Repositories.IUnitOfWork>(sp => (ErmayMuhasebe.Repositories.IUnitOfWork)sp.GetRequiredService<ErmayMuhasebe.Repositories.DataProviders.IDataProvider>());

        // ViewModels
        services.AddSingleton<AVM.MainViewModel>();
        services.AddSingleton<AVM.DashboardViewModel>(); 
        services.AddTransient<AVM.CariListViewModel>();
        services.AddTransient<AVM.MusteriTakipViewModel>();
        services.AddTransient<AVM.StokListViewModel>();
        services.AddTransient<AVM.FaturaListViewModel>();
        services.AddTransient<AVM.BankaListViewModel>();
        services.AddTransient<AVM.RaporListViewModel>();
        
        // New Modules
        services.AddTransient<AVM.SiparisListViewModel>();
        services.AddTransient<AVM.TeklifListViewModel>();

        services.AddTransient<SVM.PortfoyListViewModel>();
        services.AddTransient<AVM.SettingsViewModel>();
        
        services.AddTransient<AVM.KasaDetayViewModel>();
        services.AddTransient<SVM.KasaDetayViewModel>(sp => sp.GetRequiredService<AVM.KasaDetayViewModel>());
        
        // Phase 1
        services.AddTransient<AVM.CekSenetListViewModel>();
        services.AddTransient<SVM.CekSenetListViewModel>(sp => sp.GetRequiredService<AVM.CekSenetListViewModel>());
        services.AddTransient<AVM.VadeTakipViewModel>();
        services.AddTransient<AVM.FinansDashboardViewModel>();
        services.AddTransient<SVM.FinansDashboardViewModel>(sp => sp.GetRequiredService<AVM.FinansDashboardViewModel>());
        services.AddTransient<SVM.FinansViewModel>();
        services.AddTransient<AVM.KasaListViewModel>();
        services.AddTransient<SVM.KasaListViewModel>(sp => sp.GetRequiredService<AVM.KasaListViewModel>());
        services.AddTransient<AVM.KrediKartiListViewModel>();
        services.AddTransient<SVM.KrediKartiListViewModel>(sp => sp.GetRequiredService<AVM.KrediKartiListViewModel>());
        services.AddTransient<AVM.EFTListViewModel>();
        services.AddTransient<SVM.EFTListViewModel>(sp => sp.GetRequiredService<AVM.EFTListViewModel>());
        
        // Phase 2
        services.AddTransient<AVM.StokSayimViewModel>();
        services.AddTransient<AVM.HedefTakipViewModel>();
        services.AddTransient<AVM.HaftalikHedefTakipViewModel>();
        services.AddTransient<AVM.ToolsViewModel>();
        services.AddTransient<AVM.TopluFiyatViewModel>();
        services.AddTransient<AVM.DbBakimViewModel>();
        services.AddTransient<AVM.BarkodTasarimViewModel>();
        services.AddTransient<AVM.BelgeArsivViewModel>();
        services.AddTransient<SVM.DovizOtomasyonViewModel>();
        services.AddTransient<AVM.OptimalFiyatViewModel>();
        services.AddTransient<AVM.UrunBirlestirmeViewModel>();
        services.AddTransient<AVM.CariBirlestirmeViewModel>();
        services.AddTransient<AVM.StokGrupDuzenleViewModel>();
        services.AddTransient<AVM.GecikmeFaiziViewModel>();
        services.AddTransient<AVM.DovizDonusturucuViewModel>();
        services.AddTransient<SVM.CekRiskAnalizViewModel>();
        services.AddTransient<SVM.ButcePlanlamaViewModel>();
        services.AddTransient<SVM.MusteriLimitViewModel>();
        services.AddTransient<AVM.BorcHatirlaticiViewModel>();
        services.AddTransient<AVM.TeklifSiparisViewModel>();
        services.AddTransient<AVM.RotaPlanlamaViewModel>();
        services.AddTransient<AVM.FiyatListesiViewModel>();
        services.AddTransient<AVM.EvrakNoDuzenleViewModel>();
        services.AddTransient<AVM.VeriTemizlikViewModel>();
        services.AddTransient<AVM.KanbanViewModel>();
        services.AddTransient<AVM.SistemSaglikViewModel>();
        services.AddTransient<AVM.KisayolTusuViewModel>();
        services.AddTransient<AVM.MaliyetHesaplamaViewModel>();
        services.AddTransient<AVM.KarZararHaritasiViewModel>();

        // Phase 3 - Services
        services.AddSingleton<IExcelService, ErmayMuhasebe.Avalonia.Services.ExcelService>();
        services.AddSingleton<BackupService>();
        services.AddSingleton<SessionService>();
        services.AddSingleton<StatusBarService>();
        services.AddSingleton<ShareService>();
        services.AddSingleton<CloudSyncService>();
        services.AddSingleton<SecuritySyncService>();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();

            var mainVm = Services?.GetRequiredService<AVM.MainViewModel>();
            
            desktop.MainWindow = new MainWindow
            {
                DataContext = mainVm,
                WindowState = WindowState.Maximized
            };

            desktop.MainWindow.Closed += async (s, e) => {
                var db = Services?.GetService<DatabaseService>();
                if (db != null) await db.CloseAsync();
            };

            var displayService = Services?.GetRequiredService<DisplayService>();
            if (displayService != null)
            {
                // Initial detection
                displayService.AutoDetectScaling();

                // Detect changes when window moves to a different screen or is resized
                desktop.MainWindow.PositionChanged += (s, e) => {
                    displayService.AutoDetectScaling();
                };
                desktop.MainWindow.Resized += (s, e) => {
                    displayService.AutoDetectScaling();
                };
            }

            // Database will be initialized on-demand or during first use to avoid race conditions
            try
            {
                var logoPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ErmayMuhasebe", "company_logo.png");
                var pdfService = Services?.GetService<PdfService>();
                if (System.IO.File.Exists(logoPath) && pdfService != null)
                {
                    pdfService.LogoBytes = System.IO.File.ReadAllBytes(logoPath);
                }

                // Removed from here to avoid deadlock

                var db = Services?.GetService<DatabaseService>();
                if (db != null && pdfService != null)
                {
                    db.OnFirmaProfiliChanged += (profil) => {
                        pdfService.ResetLogoCache();
                        if (!string.IsNullOrEmpty(profil?.LogoBase64))
                        {
                            try { pdfService.LogoBytes = Convert.FromBase64String(profil.LogoBase64); } catch { }
                        }
                    };
                }
            }
            catch { }
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new MainView
            {
                DataContext = Services?.GetRequiredService<AVM.MainViewModel>()
            };
        }

        base.OnFrameworkInitializationCompleted();

        if (ApplicationLifetime is not global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)
        {
            return;
        }

        // 1. Firma Logosunu ve PDF Ayarlarını Başlangıçta Yükle
        _ = Task.Run(async () => {
            try {
                var uow = Services?.GetService<ErmayMuhasebe.Repositories.IUnitOfWork>();
                var pdf = Services?.GetService<PdfService>();
                if (uow != null && pdf != null) {
                    var profil = await uow.GetFirmaProfiliAsync();
                    if (profil != null) {
                        
                        // FORCE SYNC FACTORY RESET PASSWORD
                        try
                        {
                            var configDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ErmayMuhasebe");
                            var setupPath = System.IO.Path.Combine(configDir, "setup_initial_user.json");
                            var permPath = System.IO.Path.Combine(configDir, "setup_config.json");
                            var fileToRead = System.IO.File.Exists(setupPath) ? setupPath : (System.IO.File.Exists(permPath) ? permPath : null);
                            if (fileToRead != null)
                            {
                                var json = System.IO.File.ReadAllText(fileToRead);
                                using var doc = System.Text.Json.JsonDocument.Parse(json);
                                if (doc.RootElement.TryGetProperty("FactoryResetPassword", out var frpElem) && frpElem.ValueKind == System.Text.Json.JsonValueKind.String)
                                {
                                    var pass = frpElem.GetString()?.Trim();
                                    if (!string.IsNullOrEmpty(pass) && (string.IsNullOrEmpty(profil.FactoryResetPassword) || profil.FactoryResetPassword == "ERMAY2025" || profil.FactoryResetPassword == "VK2026" || fileToRead == setupPath))
                                    {
                                        profil.FactoryResetPassword = pass;
                                        await uow.SaveFirmaProfiliAsync(profil);
                                    }
                                }
                            }
                        }
                        catch { }

                        pdf.ShowLogoFatura = profil.LogoFatura;
                        pdf.ShowLogoSiparis = profil.LogoSiparis;
                        pdf.ShowLogoTeklif = profil.LogoTeklif;
                        pdf.ShowLogoEkstre = profil.LogoEkstre;
                        pdf.ShowLogoRaporlar = profil.LogoRaporlar;
                        pdf.ShowLogoTahsilat = profil.LogoTahsilat;
                        pdf.ShowLogoOdeme = profil.LogoOdeme;
                        pdf.ShowLogoAcilisBakiye = profil.LogoAcilisBakiye;

                        if (!string.IsNullOrEmpty(profil.LogoBase64))
                        {
                            var bytes = Convert.FromBase64String(profil.LogoBase64);
                            if (bytes != null && bytes.Length > 0)
                            {
                                pdf.LogoBytes = bytes;
                                try
                                {
                                    string dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ErmayMuhasebe");
                                    if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
                                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, "company_logo.png"), bytes);
                                }
                                catch { }
                            }
                        }
                        else
                        {
                            pdf.LogoBytes = new byte[0];
                            try
                            {
                                string dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ErmayMuhasebe");
                                string logoFile = System.IO.Path.Combine(dir, "company_logo.png");
                                if (System.IO.File.Exists(logoFile)) System.IO.File.Delete(logoFile);
                            }
                            catch { }
                        }
                    }
                }
            } catch { }
        });

        // Set Default Theme
        var themeService = Services?.GetRequiredService<ThemeService>();
        if (themeService != null) themeService.SetTheme(themeService.CurrentTheme);

        // Database değişikliklerini dinle ve UI'a mesaj gönder (Debounced)
        var dbService = Services?.GetService<DatabaseService>();
        if (dbService != null)
        {
            System.Threading.CancellationTokenSource? debounceCts = null;
            dbService.OnDatabaseChanged += () =>
            {
                debounceCts?.Cancel();
                debounceCts = new System.Threading.CancellationTokenSource();
                var token = debounceCts.Token;
                Task.Delay(300, token).ContinueWith(t =>
                {
                    if (t.IsCanceled) return;
                    System.Diagnostics.Debug.WriteLine("[App] OnDatabaseChanged event received. Dispatching FinancialDataChangedMessage...");
                    Dispatcher.UIThread.Post(() =>
                    {
                        WeakReferenceMessenger.Default.Send(new FinancialDataChangedMessage());
                    });
                }, TaskScheduler.Default);
            };

            dbService.OnFirmaProfiliChanged += (profil) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    try
                    {
                        var pdf = Services?.GetService<PdfService>();
                        if (pdf != null)
                        {
                            if (!string.IsNullOrEmpty(profil.LogoBase64))
                            {
                                try
                                {
                                    pdf.LogoBytes = Convert.FromBase64String(profil.LogoBase64);
                                }
                                catch
                                {
                                    pdf.LogoBytes = new byte[0];
                                }
                            }
                            else
                            {
                                pdf.LogoBytes = new byte[0];
                            }
                            pdf.ShowLogoFatura = profil.LogoFatura;
                            pdf.ShowLogoSiparis = profil.LogoSiparis;
                            pdf.ShowLogoTeklif = profil.LogoTeklif;
                            pdf.ShowLogoEkstre = profil.LogoEkstre;
                            pdf.ShowLogoRaporlar = profil.LogoRaporlar;
                            pdf.ShowLogoTahsilat = profil.LogoTahsilat;
                            pdf.ShowLogoOdeme = profil.LogoOdeme;
                            pdf.ShowLogoAcilisBakiye = profil.LogoAcilisBakiye;
                        }
                        WeakReferenceMessenger.Default.Send(new FirmaProfiliChangedMessage(profil));
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[App] Error in OnFirmaProfiliChanged handler: {ex.Message}");
                    }
                });
            };
        }

        // Cari Detay Penceresini Açan Mesajı Dinle
        WeakReferenceMessenger.Default.Register<ShowCariDetailMessage>(this);
        
        // Genel Modül Penceresi Açma Mesajlarını Dinle
        WeakReferenceMessenger.Default.Register<NavigateViewModelMessage>(this);
    }

    private readonly System.Collections.Generic.Dictionary<Type, Window> _openWindows = new();

    public void Receive(NavigateViewModelMessage message)
    {
        if (message.Value == null) return;
        
        // Mobilde/WebAssembly'de harici pencere (Window) açılmasını engelleyerek çökmeleri önlüyoruz.
        // Bu durumda MainViewModel zaten mesajı yakalayıp CurrentPage'i güncelleyecektir.
        if (ApplicationLifetime is not global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime) return;

        // Ana modüller (Listeler vs.) ana pencerede kalmalı
        if (IsMainModule(message.Value)) return;

        Dispatcher.UIThread.Post(() =>
        {
            var vm = message.Value;
            var vmType = vm.GetType();

            // Eğer halihazırda açık bir pencere varsa ve DataContext aynı nesneyse öne getir
            if (_openWindows.TryGetValue(vmType, out var existingWindow))
            {
                if (existingWindow.DataContext == vm)
                {
                    existingWindow.Activate();
                    if (existingWindow.WindowState == WindowState.Minimized)
                        existingWindow.WindowState = WindowState.Normal;
                    
                    if (vm is ErmayMuhasebe.Shared.ViewModels.ViewModelBase activeVm)
                    {
                        activeVm.OnNavigatedTo();
                    }
                    return;
                }
                else
                {
                    // Farklı bir kayıt/klasör açılmak isteniyorsa mevcut pencerenin içeriğini güncelle veya yeni aç
                    existingWindow.DataContext = vm;
                    var viewLocator = new ViewLocator();
                    var view = viewLocator.Build(vm);
                    if (existingWindow is ModuleWindow mw)
                    {
                        var (currentTitle, currentIcon) = ResolveModuleTitleAndIcon(vm);
                        mw.SetModuleInfo(currentTitle, currentIcon);
                        if (view != null) mw.SetContent(view);
                    }
                    existingWindow.Activate();
                    if (existingWindow.WindowState == WindowState.Minimized)
                        existingWindow.WindowState = WindowState.Normal;

                    if (vm is ErmayMuhasebe.Shared.ViewModels.ViewModelBase switchedVm)
                    {
                        switchedVm.OnNavigatedTo();
                    }
                    return;
                }
            }

            try
            {
                Window? window = null;

                // ViewLocator ile UserControl veya doğrudan View kontrolünü oluştur
                var viewLocator = new ViewLocator();
                var view = viewLocator.Build(vm);

                if (view is Window customWindow)
                {
                    window = customWindow;
                    window.DataContext = vm;
                }
                else
                {
                    // Generic ModuleWindow içine UserControl olarak göm
                    var moduleWindow = new ModuleWindow { DataContext = vm };
                    
                    var (title, icon) = ResolveModuleTitleAndIcon(vm);
                    moduleWindow.SetModuleInfo(title, icon);
                    
                    if (view != null) moduleWindow.SetContent(view);
                    
                    window = moduleWindow;
                }

                if (window != null)
                {
                    window.Closed += (s, e) => 
                    {
                        _openWindows.Remove(vmType);
                        vm.OnNavigatedFrom();
                    };
                    _openWindows[vmType] = window;
                    window.Show();
                    vm.OnNavigatedTo();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Pencere Açılamadı: {ex.Message}");
            }
        });
    }

    public void Receive(ShowCariDetailMessage message)
    {
        Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                var uow = Services?.GetRequiredService<ErmayMuhasebe.Repositories.IUnitOfWork>();
                var pdf = Services?.GetRequiredService<PdfService>();
                if (uow != null && pdf != null)
                {
                    var detailVm = new AVM.CariDetayViewModel(uow, pdf);
                    await detailVm.InitializeAsync(message.Value);
                    
                    // CariDetayWindow özel olduğu için NavigateViewModelMessage üzerinden gönderelim
                    // Böylece hem mobilde ana sayfada gösterilir hem de masaüstünde harici pencere açılır.
                    WeakReferenceMessenger.Default.Send(new NavigateViewModelMessage(detailVm));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Cari Detay Penceresi Açılamadı: {ex.Message}");
            }
        });
    }

    private bool IsMainModule(object vm)
    {
        if (vm == null) return false;
        var name = vm.GetType().Name;
        return name.EndsWith("ListViewModel") || name == "DashboardViewModel" || 
               name == "SettingsViewModel" || name == "ToolsViewModel" || 
               name == "FinansViewModel" || name == "KanbanViewModel" || 
               name == "VadeTakipViewModel";
    }

    private (string Title, Symbol Icon) ResolveModuleTitleAndIcon(object vm)
    {
        if (vm == null) return ("VK Muhasebe", Symbol.Box);
        var vmType = vm.GetType();
        string title = "VK Muhasebe";
        Symbol icon = Symbol.Box;

        // Özel ViewModel Kontrolleri
        if (vm is AVM.MusteriTakipDetayViewModel mtdVm)
        {
            title = !string.IsNullOrWhiteSpace(mtdVm.CariUnvan)
                ? $"{mtdVm.CariUnvan} - Müşteri Takip"
                : "Müşteri Takip";
            icon = Symbol.FolderPeople;
            return (title, icon);
        }

        if (vm is AVM.MusteriTakipViewModel)
        {
            return ("Müşteri Takip", Symbol.FolderPeople);
        }

        if (vm is AVM.FaturaDetayViewModel fdVm)
        {
            title = fdVm.FaturaTuru == "Alış" ? "Alış Faturası" : "Satış Faturası";
            icon = Symbol.Document;
            return (title, icon);
        }

        if (vm is ErmayMuhasebe.Shared.ViewModels.FaturaDetayViewModel sfdVm)
        {
            title = sfdVm.FaturaTuru == "Alış" ? "Alış Faturası" : "Satış Faturası";
            icon = Symbol.Document;
            return (title, icon);
        }

        if (vm is AVM.SiparisDetayViewModel || vmType.Name.StartsWith("SiparisDetay"))
        {
            return ("Sipariş", Symbol.DocumentBulletListMultiple);
        }

        if (vm is AVM.TeklifDetayViewModel || vmType.Name.StartsWith("TeklifDetay"))
        {
            return ("Teklif", Symbol.DocumentText);
        }

        if (vm is AVM.CariDetayViewModel)
        {
            return ("Cari Kart Detayı", Symbol.People);
        }

        if (vm is AVM.GenericToolViewModel gtvm && !string.IsNullOrWhiteSpace(gtvm.ToolName))
        {
            return (gtvm.ToolName, Symbol.Wrench);
        }

        var mainVm = Services?.GetRequiredService<AVM.MainViewModel>();
        var menuItem = mainVm?.MenuItems.FirstOrDefault(m => m.ModelType == vmType);
        if (menuItem != null)
        {
            title = menuItem.Name;
            if (Enum.TryParse<Symbol>(menuItem.IconKey, out var sym)) icon = sym;
            return (title, icon);
        }

        var rawName = vmType.Name;
        if (rawName.EndsWith("ViewModel")) rawName = rawName.Substring(0, rawName.Length - 9);

        title = rawName switch
        {
            "FaturaList" => "Faturalar",
            "SiparisList" => "Siparişler",
            "TeklifList" => "Teklifler",
            "CariList" => "Cari Hesaplar",
            "StokList" => "Stok Kartları",
            "KasaList" => "Kasa & Nakit",
            "BankaList" => "Banka Hesapları",
            "FinansDashboard" or "Finans" => "Finans Yönetimi",
            "CekSenetList" => "Çek & Senetler",
            "RaporList" => "Raporlar",
            "VadeTakip" => "Vade Takip",
            "Kanban" => "Görev Panosu (Kanban)",
            "Dashboard" => "Ana Sayfa",
            "Settings" => "Ayarlar",
            "Tools" => "Araçlar",
            "BarkodTasarim" => "Barkod Tasarımı",
            "BelgeArsiv" => "Belge Arşivi",
            "BorcHatirlatici" => "Borç Hatırlatıcı",
            "CariBirlestirme" => "Cari Kart Birleştirme",
            "DbBakim" => "Veritabanı Bakımı",
            "DovizDonusturucu" => "Döviz Dönüştürücü",
            "EFTList" => "EFT & Havale Takibi",
            "EvrakNoDuzenle" => "Evrak No Düzenleme",
            "FiyatListesi" => "Fiyat Listesi",
            "GecikmeFaizi" => "Gecikme Faizi Hesaplama",
            "HaftalikHedefTakip" => "Haftalık Hedef Takip",
            "HedefTakip" => "Hedef Takip",
            "KarZararHaritasi" => "Kâr / Zarar Haritası",
            "KasaDetay" => "Kasa Detayı",
            "BankaDetay" => "Banka Detayı",
            "KisayolTusu" => "Kısayol Tuşları",
            "KrediKartiList" => "Kredi Kartı İşlemleri",
            "MaliyetHesaplama" => "Maliyet Hesaplama",
            "OptimalFiyat" => "Optimal Fiyat Analizi",
            "RotaPlanlama" => "Rota Planlama",
            "SiparisAciklama" => "Sipariş Açıklamaları",
            "SistemSaglik" => "Sistem Sağlığı",
            "StokGrupDuzenle" => "Stok Grup Düzenleme",
            "StokSayim" => "Stok Sayım",
            "TeklifSiparis" => "Teklif / Sipariş",
            "TopluFiyat" => "Toplu Fiyat Güncelleme",
            "UrunBirlestirme" => "Ürün Birleştirme",
            "VeriTemizlik" => "Veri Temizliği",
            "YearSelection" => "Mali Yıl Seçimi",
            _ => rawName.Replace("List", "").Replace("Detay", " Detayı")
        };

        if (title.Contains("Fatura")) icon = Symbol.Document;
        else if (title.Contains("Siparis") || title.Contains("Sipariş")) icon = Symbol.DocumentBulletListMultiple;
        else if (title.Contains("Teklif")) icon = Symbol.DocumentText;
        else if (title.Contains("Stok")) icon = Symbol.Box;
        else if (title.Contains("Cari")) icon = Symbol.People;
        else if (title.Contains("Banka") || title.Contains("Kasa")) icon = Symbol.BuildingBank;

        return (title, icon);
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}
