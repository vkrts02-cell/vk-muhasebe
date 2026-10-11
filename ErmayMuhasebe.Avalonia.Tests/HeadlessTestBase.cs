using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ErmayMuhasebe.Models;
using ErmayMuhasebe.Repositories;
using ErmayMuhasebe.Repositories.DataProviders;
using ErmayMuhasebe.Services;
using ErmayMuhasebe.Avalonia.Services;
using AVM = ErmayMuhasebe.Avalonia.ViewModels;
using SVM = ErmayMuhasebe.Shared.ViewModels;
using Xunit;

namespace ErmayMuhasebe.Avalonia.Tests;

public class TestFileService : IFileService
{
    public byte[]? LastSavedBytes { get; set; }
    public string? LastSavedFileName { get; set; }

    public Task SaveAndOpenFileAsync(string fileName, byte[] content, string contentType)
    {
        LastSavedFileName = fileName;
        LastSavedBytes = content;
        return Task.CompletedTask;
    }

    public Task SaveFileAsync(string fileName, byte[] content)
    {
        LastSavedFileName = fileName;
        LastSavedBytes = content;
        return Task.CompletedTask;
    }

    public Task<IFileWrapper?> OpenFilePickerAsync(string title, string[] extensions) => Task.FromResult<IFileWrapper?>(null);
    public Task<string?> SaveFilePickerAsync(string title, string defaultFileName, string extension) => Task.FromResult<string?>(Path.Combine(Path.GetTempPath(), defaultFileName));
    public Task<string?> OpenFolderPickerAsync(string title) => Task.FromResult<string?>(Path.GetTempPath());
    public Task<bool> ShowConfirmationAsync(string title, string message) => Task.FromResult(true);
}

public abstract class HeadlessTestBase : IAsyncLifetime, IDisposable
{
    protected readonly string _testDbPath;
    protected readonly IServiceProvider _serviceProvider;
    protected readonly DatabaseService _dbService;
    protected readonly IUnitOfWork _uow;
    protected readonly TestFileService _testFileService;
    protected readonly IPdfService _pdfService;

    protected HeadlessTestBase()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"HeadlessTest_{Guid.NewGuid():N}.db3");
        ErmayMuhasebe.Data.Constants.DatabasePath = _testDbPath;

        var services = new ServiceCollection();

        // Utils
        services.AddSingleton<HttpClient>();

        // Business Services
        var yearContext = new YearContext();
        services.AddSingleton<IYearContext>(yearContext);

        _dbService = new DatabaseService(yearContext, _testDbPath) { IsTestMode = true };
        services.AddSingleton(_dbService);

        var dataProvider = new SqliteDataProvider(_dbService);
        services.AddSingleton<IDataProvider>(dataProvider);
        _uow = dataProvider;
        services.AddSingleton<IUnitOfWork>(_uow);

        _testFileService = new TestFileService();
        services.AddSingleton<IFileService>(_testFileService);

        _pdfService = new PdfService(_testFileService);
        services.AddSingleton<IPdfService>(_pdfService);
        services.AddSingleton<PdfService>((PdfService)_pdfService);

        services.AddSingleton<IExcelService, ErmayMuhasebe.Avalonia.Services.ExcelService>();
        services.AddSingleton<ExternalApiService>();
        services.AddSingleton<ThemeService>();
        services.AddSingleton<DisplayService>();
        services.AddSingleton<IFinansService, FinansService>();
        services.AddSingleton<StatusBarService>();
        services.AddSingleton<DovizService>();
        services.AddSingleton<BackupService>();
        services.AddSingleton<SessionService>();
        services.AddSingleton<ShareService>();
        _dbService.SyncService.Disconnect();
        services.AddSingleton<CloudSyncService>(_dbService.SyncService);
        services.AddSingleton<SecuritySyncService>();

        // ViewModels
        services.AddSingleton<AVM.MainViewModel>();
        services.AddSingleton<AVM.DashboardViewModel>(sp => new AVM.DashboardViewModel(
            sp.GetRequiredService<IUnitOfWork>(), 
            sp.GetRequiredService<ThemeService>(), 
            sp.GetRequiredService<IPdfService>(),
            disableAutoRefresh: true)); 
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
        services.AddTransient<AVM.VadeTakipViewModel>(sp => new AVM.VadeTakipViewModel(
            sp.GetRequiredService<IUnitOfWork>(), 
            sp.GetRequiredService<IExcelService>(), 
            sp.GetRequiredService<IPdfService>(), 
            sp.GetRequiredService<IFileService>(),
            disableAutoRefresh: true));
        services.AddTransient<AVM.FinansDashboardViewModel>(sp => new AVM.FinansDashboardViewModel(sp.GetRequiredService<IUnitOfWork>()) { DisableAutoRefresh = true });
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

        _serviceProvider = services.BuildServiceProvider();
    }

    public virtual async Task InitializeAsync()
    {
        CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Reset();
        await _dbService.InitializeAsync();

        try
        {
            var globalConn = _dbService.GetGlobalConnection();
            await globalConn.CreateTableAsync<User>();
            var userCount = await globalConn.Table<User>().CountAsync();
            if (userCount == 0)
            {
                var salt = AuthService.GenerateSalt();
                var hashedPassword = AuthService.HashPassword("123", salt);
                await globalConn.InsertAsync(new User 
                { 
                    Username = "admin", 
                    Password = hashedPassword, 
                    PasswordSalt = salt,
                    Role = "Admin", 
                    CreatedAt = DateTime.Now 
                });
            }
        }
        catch { }
    }

    public virtual async Task DisposeAsync()
    {
        try
        {
            CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Reset();
            await _dbService.CloseAsync();
        }
        catch { }
    }

    public virtual void Dispose()
    {
        try
        {
            CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Reset();
            if (File.Exists(_testDbPath)) File.Delete(_testDbPath);
            if (File.Exists(_testDbPath + "-wal")) File.Delete(_testDbPath + "-wal");
            if (File.Exists(_testDbPath + "-shm")) File.Delete(_testDbPath + "-shm");
        }
        catch { }
    }
}
