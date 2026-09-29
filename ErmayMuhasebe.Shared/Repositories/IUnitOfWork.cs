using System.Collections.Generic;
using System.Threading.Tasks;
using ErmayMuhasebe.Models;

namespace ErmayMuhasebe.Repositories;
using ErmayMuhasebe.Repositories.DataProviders;

/// <summary>
/// Unit of Work Pattern Interface
/// Tüm repository'leri tek bir yerden yönetmeyi sağlar
/// </summary>
public interface IUnitOfWork : IDataProvider
{
    // Maintenance
    Task<string> GetDatabasePathAsync();
    Task<long> GetDatabaseSizeAsync();
    Task PerformMaintenanceAsync();
    Task RestoreBackupAsync(string jsonData);

    // Analytics & Dashboard
    Task<DashboardStats> GetDashboardStatsAsync();
    Task<List<RecentTransactionItem>> GetRecentTransactionsAsync();
    Task<List<CariAlertItem>> GetRiskyCarisAsync();
    Task<List<CariAlertItem>> GetPayableCarisAsync();
    Task<List<IncomeExpenseItem>> GetMonthlyIncomeExpenseAsync();
    Task<List<FinanceTrendItem>> GetFinanceTrendAsync(string period); // weekly, monthly, yearly
    Task<List<SatisHedefi>> GetSatisHedefleriAsync();
    Task<List<SatisHedefi>> GetSatisHedefleriAsync(int yil);
    Task<List<YillikSatisHedefi>> GetYillikSatisHedefleriAsync();
    Task<List<HaftalikSatisHedefi>> GetHaftalikSatisHedefleriAsync();
    Task<List<CityProfitStat>> GetCityProfitStatsAsync();
    Task RecalculateSystemBalancesAsync();
    void SetCloudConfig(string url, string secret);
    (string Url, string Secret) GetCloudConfig();
    void EnableAutoSync(bool enable);
    Task SyncToCloudAsync();
    Task SyncFromCloudAsync();

    /// <summary>
    /// ID korumalı olarak veritabanına kayıt ekler (Devir işlemleri için)
    /// </summary>
    Task InsertWithIdAsync<T>(T entity) where T : class;

    // Mali Yıl Devir & Yansıtma
    Task<ErmayMuhasebe.Services.RolloverResult> RolloverYearAsync(int sourceYear, int targetYear, ErmayMuhasebe.Services.RolloverOptions? options = null, System.IProgress<string>? progress = null);
    Task<ErmayMuhasebe.Services.RolloverResult> ReflectBalancesForwardAsync(int sourceYear, System.IProgress<string>? progress = null);
    List<int> GetAvailableYears();

    /// <summary>

    /// Transaction başlatır
    /// </summary>
    Task BeginTransactionAsync();

    /// <summary>
    /// Transaction'ı commit eder
    /// </summary>
    Task CommitTransactionAsync();

    /// <summary>
    /// Transaction'ı rollback eder
    /// </summary>
    Task RollbackTransactionAsync();
    
    // Firma Profili
    Task<FirmaProfili> GetFirmaProfiliAsync();
    Task SaveFirmaProfiliAsync(FirmaProfili f);

    Task ClearAllTablesAsync();
}

/// <summary>
/// Cari Repository Interface
/// </summary>
public interface ICariRepository : IRepository<CariKart>
{
    Task<List<CariKart>> GetByTurAsync(string tur);
    Task<List<CariKart>> GetHareketsizCarilerAsync(int gunSayisi = 180);
    Task<List<CariHareket>> GetHareketlerAsync(int cariId);
    Task<int> SaveHareketAsync(CariHareket hareket);
    Task<int> DeleteHareketAsync(CariHareket hareket);
    Task<int> DeleteHareketByEvrakNoAsync(string evrakNo);
    Task<CariHareket?> GetHareketByEvrakNoAsync(string evrakNo);
    Task<List<CariHareket>> GetAllHareketlerAsync();
    Task<int> RecalculateBalanceAsync(int cariId);
    Task MatchInvoicePaymentsAsync(int cariId);
    Task<CariSummary> GetGlobalSummaryAsync(string? search = null);
    Task MergeCariAsync(int sourceId, int targetId);
}

/// <summary>
/// Stok Repository Interface
/// </summary>
public interface IStokRepository : IRepository<StokKart>
{
    Task<StokKart?> GetByKodAsync(string kod);
    Task<StokKart?> GetByBarkodAsync(string barkod);
    Task<List<StokKart>> GetByKategoriAsync(string kategori);
    Task<List<StokKart>> GetKritikStoklarAsync();
    Task<List<StokHareket>> GetHareketlerAsync(int stokId);
    Task<int> SaveHareketAsync(StokHareket hareket);
    Task<int> DeleteHareketAsync(StokHareket hareket);
    Task<List<StokHareket>> GetAllHareketlerAsync();
    Task RecalculateCostsAsync(int? stokId = null);
    Task MergeStokAsync(int kaynakStokId, int hedefStokId);
    Task<List<string>> GetGruplarAsync();
    Task<int> SaveGrupAsync(string grupAdi);
    Task<int> DeleteGrupAsync(string grupAdi);
}

/// <summary>
/// Fatura Repository Interface
/// </summary>
public interface IFaturaRepository : IRepository<Fatura>
{
    Task<Fatura?> GetByNoAsync(string faturaNo);
    Task<List<Fatura>> GetByCariIdAsync(int cariId);
    Task<List<Fatura>> GetByTurAsync(string tur);
    Task<List<FaturaDetay>> GetDetaylarAsync(int faturaId);
    Task<List<FaturaDetay>> GetAllDetaylarAsync();
    Task<int> SaveWithDetailsAsync(Fatura fatura, List<FaturaDetay> detaylar);
    Task<int> SaveWithDetailsAndTransactionAsync(Fatura fatura, List<FaturaDetay> detaylar, bool isSatis = true, bool updateCari = true, bool updateStok = true, bool updateStokPrices = false);
    Task<int> SaveWithTransactionAsync(Fatura fatura, List<FaturaDetay> detaylar, CariKart cari, bool updateCari = true, bool updateStok = true, bool updateStokPrices = false);
    Task<decimal> GetSumAsync(DateTime? start = null, DateTime? end = null, string? tur = null);
}

/// <summary>
/// Banka Repository Interface
/// </summary>
public interface IBankaRepository : IRepository<BankaKart>
{
    Task<List<BankaHareket>> GetHareketlerAsync(int bankaId);
    Task<decimal> GetBakiyeAsync(int bankaId);
    Task<List<BankaHareket>> GetAllHareketlerAsync();
    Task<int> SaveHareketAsync(BankaHareket hareket);
    Task<int> DeleteHareketAsync(BankaHareket hareket);
    Task<int> DeleteHareketAsync(int id);
    Task<int> RecalculateBalanceAsync(int bankaId);
}

/// <summary>
/// Kasa Repository Interface
/// </summary>
public interface IKasaRepository : IRepository<KasaHareket>
{
    Task<decimal> GetBakiyeAsync();
    Task<List<KasaHareket>> GetHareketlerByTarihAsync(DateTime baslangic, DateTime bitis);
    Task<List<KasaHareket>> GetHareketlerAsync(int kasaId);
    Task<List<KasaHareket>> GetAllHareketlerAsync();
}

/// <summary>
/// Sipariş Repository Interface
/// </summary>
public interface ISiparisRepository : IRepository<Siparis>
{
    Task<List<SiparisDetay>> GetDetaylarAsync(int siparisId);
    Task<List<SiparisDetay>> GetAllDetaylarAsync();
    Task<int> SaveWithDetailsAsync(Siparis siparis, List<SiparisDetay> detaylar);
}

/// <summary>
/// Teklif Repository Interface
/// </summary>
public interface ITeklifRepository : IRepository<Teklif>
{
    Task<List<TeklifDetay>> GetDetaylarAsync(int teklifId);
    Task<List<TeklifDetay>> GetAllDetaylarAsync();
    Task<int> SaveWithDetailsAsync(Teklif teklif, List<TeklifDetay> detaylar);
}

/// <summary>
/// Çek Repository Interface
/// </summary>
public interface ICekRepository : IRepository<Cek>
{
    Task<List<Cek>> GetByCariIdAsync(int cariId);
    Task<int> SaveWithTransactionAsync(Cek cek);
}

/// <summary>
/// Senet Repository Interface
/// </summary>
public interface ISenetRepository : IRepository<Senet>
{
    Task<List<Senet>> GetByCariIdAsync(int cariId);
}

public interface IKrediKartiRepository : IRepository<KrediKartiIslem>
{
    Task<KrediKartiIslem?> GetByNoAsync(string no);
}

public interface IEftRepository : IRepository<EftIslem>
{
    Task<EftIslem?> GetByNoAsync(string no);
}

public interface IStokSayimRepository : IRepository<StokSayimFisi>
{
    Task<List<StokSayimDetay>> GetDetaylarAsync(int fisId);
    Task<List<StokSayimDetay>> GetAllDetaylarAsync();
    Task<int> SaveWithDetailsAsync(StokSayimFisi fis, List<StokSayimDetay> detaylar);
}

public interface IPortfoyRepository : IRepository<PortfoyKart>
{
}

public interface IHedefRepository
{
    Task<List<SatisHedefi>> GetAylikHedeflerAsync(int yil);
    Task<List<SatisHedefi>> GetAllAylikHedeflerAsync();
    Task<int> SaveAylikHedefAsync(SatisHedefi hedef);
    Task<int> DeleteAylikHedefAsync(int id);
    
    Task<List<HaftalikSatisHedefi>> GetHaftalikHedeflerAsync(int yil);
    Task<List<HaftalikSatisHedefi>> GetAllHaftalikHedeflerAsync();
    Task<int> SaveHaftalikHedefAsync(HaftalikSatisHedefi hedef);
    Task<int> DeleteHaftalikHedefAsync(int id);
    
    Task<List<YillikSatisHedefi>> GetAllYillikHedeflerAsync();
    Task<int> SaveYillikHedefAsync(YillikSatisHedefi hedef);
}

public interface IDovizRepository : IRepository<DovizKur>
{
    Task<List<DovizKur>> GetLatestAsync();
}

public interface IBelgeArsivRepository : IRepository<BelgeArsiv>
{
    Task<List<BelgeArsiv>> GetByKategoriAsync(string kategori);
}

public interface INoteRepository : IRepository<Note>
{
    Task<List<Note>> GetByRelatedAsync(string type, int id);
}
