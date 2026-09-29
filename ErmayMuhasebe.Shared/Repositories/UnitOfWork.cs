using System.Threading.Tasks;
using System.Collections.Generic;
using ErmayMuhasebe.Models;
using ErmayMuhasebe.Services;
using System.Text.Json;
using SQLite;

namespace ErmayMuhasebe.Repositories;

/// <summary>
/// Unit of Work Implementation (SQLite)
/// Tüm repository'leri tek bir yerden yönetir
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    protected readonly DatabaseService _dbService;

    public UnitOfWork(DatabaseService dbService)
    {
        _dbService = dbService;
        Cariler = new CariRepository(dbService);
        Stoklar = new StokRepository(dbService);
        Faturalar = new FaturaRepository(dbService);
        Bankalar = new BankaRepository(dbService);
        Kasalar = new KasaRepository(dbService);
        Siparisler = new SiparisRepository(dbService);
        Teklifler = new TeklifRepository(dbService);
        Cekler = new CekRepository(dbService);
        Senetler = new SenetRepository(dbService);
        KrediKartlari = new KrediKartiRepository(dbService);
        EftIslemleri = new EftRepository(dbService);
        StokSayimlar = new StokSayimRepository(dbService);
        Portfolyo = new PortfoyRepository(dbService);
        Hedefler = new HedefRepository(dbService);
        DovizKurlari = new DovizRepository(dbService);
        BelgeArsiv = new BelgeArsivRepository(dbService);
        Notes = new NoteRepository(dbService);
        MusteriTakip = new MusteriTakipRepository(dbService);
    }

    public ICariRepository Cariler { get; }
    public IStokRepository Stoklar { get; }
    public IFaturaRepository Faturalar { get; }
    public IBankaRepository Bankalar { get; }
    public IKasaRepository Kasalar { get; }
    public ISiparisRepository Siparisler { get; }
    public ITeklifRepository Teklifler { get; }
    public ICekRepository Cekler { get; }
    public ISenetRepository Senetler { get; }
    public IKrediKartiRepository KrediKartlari { get; }
    public IEftRepository EftIslemleri { get; }
    public IStokSayimRepository StokSayimlar { get; }
    public IPortfoyRepository Portfolyo { get; }
    public IHedefRepository Hedefler { get; }
    public IDovizRepository DovizKurlari { get; }
    public IBelgeArsivRepository BelgeArsiv { get; }
    public INoteRepository Notes { get; }
    public IMusteriTakipRepository MusteriTakip { get; }

    // Maintenance
    public Task<string> GetDatabasePathAsync() => Task.FromResult(_dbService.GetDatabasePath());
    public async Task<long> GetDatabaseSizeAsync() => await _dbService.GetDatabaseSizeAsync();
    public async Task PerformMaintenanceAsync() => await _dbService.OptimizeDatabaseAsync();

    public async Task InsertWithIdAsync<T>(T entity) where T : class
    {
        await _dbService.InitializeAsync();
        var db = await _dbService.GetConnectionAsync();
        await db.InsertAsync(entity);
    }

    // Analytics
    public async Task<DashboardStats> GetDashboardStatsAsync() => await _dbService.GetDashboardStatsAsync();
    public async Task<List<RecentTransactionItem>> GetRecentTransactionsAsync() => await _dbService.GetRecentTransactionsAsync();
    public async Task<List<CariAlertItem>> GetRiskyCarisAsync() => await _dbService.GetRiskyCarisAsync();
    public async Task<List<CariAlertItem>> GetPayableCarisAsync() => await _dbService.GetPayableCarisAsync();
    public async Task<List<IncomeExpenseItem>> GetMonthlyIncomeExpenseAsync() => await _dbService.GetMonthlyIncomeExpenseAsync();
    public async Task<List<FinanceTrendItem>> GetFinanceTrendAsync(string period) => await _dbService.GetFinanceTrendAsync(period);
    public async Task<List<SatisHedefi>> GetSatisHedefleriAsync() => await _dbService.GetSatisHedefleriAsync();
    public async Task<List<SatisHedefi>> GetSatisHedefleriAsync(int yil) => await _dbService.GetSatisHedefleriAsync(yil);
    public async Task<List<YillikSatisHedefi>> GetYillikSatisHedefleriAsync() => await _dbService.GetYillikSatisHedefleriAsync();
    public async Task<List<HaftalikSatisHedefi>> GetHaftalikSatisHedefleriAsync() => await _dbService.GetHaftalikSatisHedefleriAsync();
    public async Task<List<CityProfitStat>> GetCityProfitStatsAsync() => await _dbService.GetCityProfitStatsAsync();
    public async Task RecalculateSystemBalancesAsync() => await _dbService.RecalculateSystemBalancesAsync();

    public async Task RestoreBackupAsync(string jsonData)
    {
        using JsonDocument doc = JsonDocument.Parse(jsonData);
        JsonElement root = doc.RootElement;

        await _dbService.ClearAllTablesAsync();

        var db = await _dbService.GetConnectionAsync();

        var jsonOptions = new JsonSerializerOptions 
        { 
            PropertyNameCaseInsensitive = true,
            NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
        };

        async Task Restore<T>(string propName) where T : class, new()
        {
             await Task.Yield(); // Suppress CS1998 warning
             if (root.TryGetProperty(propName, out JsonElement json))
             {
                 try 
                 {
                     var list = JsonSerializer.Deserialize<List<T>>(json.GetRawText(), jsonOptions);
                     if (list != null && list.Count > 0)
                     {
                         // Using sync transaction on the underlying connection for reliable ID preservation
                         var conn = db.GetConnection();
                         conn.RunInTransaction(() => {
                             foreach (var item in list)
                             {
                                 conn.InsertOrReplace(item);
                             }
                         });
                     }
                 }
                 catch (Exception ex)
                 {
                     System.Diagnostics.Debug.WriteLine($"RESTORE ERROR [{propName}]: {ex.Message}");
                     throw new Exception($"Tablo geri yükleme hatası ({propName}): {ex.Message}");
                 }
             }
        }

        await Restore<CariKart>("Cariler");
        await Restore<StokKart>("Stoklar");
        await Restore<Fatura>("Faturalar");
        await Restore<BankaKart>("Bankalar");
        await Restore<Siparis>("Siparisler");
        await Restore<Teklif>("Teklifler");
        await Restore<Cek>("Cekler");
        await Restore<Senet>("Senetler");
        await Restore<KrediKartiIslem>("KrediKartlari");
        await Restore<EftIslem>("EftIslemleri");
        await Restore<StokSayimFisi>("StokSayimlar");
        await Restore<PortfoyKart>("Portfolyo");
        await Restore<DovizKur>("DovizKurlari");
        await Restore<Note>("Notes");
        
        // Specialized: Hedefler
        await Restore<SatisHedefi>("AylikHedefler");
        await Restore<HaftalikSatisHedefi>("HaftalikHedefler");
        await Restore<YillikSatisHedefi>("YillikHedefler");

        // Movements & Details 
        await Restore<CariHareket>("CariHareketler");
        await Restore<StokHareket>("StokHareketler");
        await Restore<FaturaDetay>("FaturaDetaylar");
        await Restore<KasaHareket>("Kasalar");
        // "KasaHareketler" key is often used interchangeably in different versions
        if (root.TryGetProperty("KasaHareketler", out _)) await Restore<KasaHareket>("KasaHareketler"); 
        await Restore<BankaHareket>("BankaHareketler");
        await Restore<SiparisDetay>("SiparisDetaylar");
        await Restore<TeklifDetay>("TeklifDetaylar");
        await Restore<StokSayimDetay>("StokSayimDetaylar");

        await RecalculateSystemBalancesAsync();
        await UpdateSizeAsync();
    }
    
    private async Task UpdateSizeAsync() => await GetDatabaseSizeAsync();
    public void SetCloudConfig(string url, string secret) => _dbService.SetCloudConfig(url, secret);
    public (string Url, string Secret) GetCloudConfig() => _dbService.GetCloudConfig();
    public void EnableAutoSync(bool enable) => _dbService.EnableAutoSync(enable);
    public async Task SyncToCloudAsync() => await _dbService.SyncToCloudAsync();
    public async Task SyncFromCloudAsync() => await _dbService.SyncFromCloudAsync();
    
    // IDataProvider implementation
    public virtual async Task InitializeAsync() => await _dbService.InitializeAsync();
    public virtual async Task InitializeAsync(string dbNameOrPath) => await _dbService.InitializeAsync(dbNameOrPath);
    public virtual void InvalidateAllCache() => _dbService.InvalidateAllCache();

    public async Task<int> SaveChangesAsync()
    {
        await Task.CompletedTask;
        return 1;
    }

    public async Task BeginTransactionAsync()
    {
        await _dbService.BeginTransactionAsync();
    }

    public async Task CommitTransactionAsync()
    {
        await _dbService.CommitTransactionAsync();
    }

    public async Task RollbackTransactionAsync()
    {
        await _dbService.RollbackTransactionAsync();
    }

    public Task<FirmaProfili> GetFirmaProfiliAsync() => _dbService.GetFirmaProfiliAsync();
    public Task SaveFirmaProfiliAsync(FirmaProfili f) => _dbService.SaveFirmaProfiliAsync(f);
    public Task ClearAllTablesAsync() => _dbService.ClearAllTablesAsync();

    // Mali Yıl Devir & Yansıtma
    public async Task<ErmayMuhasebe.Services.RolloverResult> RolloverYearAsync(int sourceYear, int targetYear, ErmayMuhasebe.Services.RolloverOptions? options = null, System.IProgress<string>? progress = null)
    {
        var service = new ErmayMuhasebe.Services.YearRolloverService(_dbService, _dbService.YearContext);
        return await service.RolloverYearAsync(sourceYear, targetYear, options, progress);
    }

    public async Task<ErmayMuhasebe.Services.RolloverResult> ReflectBalancesForwardAsync(int sourceYear, System.IProgress<string>? progress = null)
    {
        var service = new ErmayMuhasebe.Services.YearRolloverService(_dbService, _dbService.YearContext);
        return await service.ReflectBalancesForwardAsync(sourceYear, progress);
    }

    public List<int> GetAvailableYears()
    {
        var service = new ErmayMuhasebe.Services.YearRolloverService(_dbService, _dbService.YearContext);
        return service.GetAvailableYears();
    }
}

