using ErmayMuhasebe.Models;
using ErmayMuhasebe.Services;
using Xunit;
using Xunit.Abstractions;

namespace ErmayMuhasebe.Tests.Services;

[Collection("DatabaseTests")]
public class CloudSyncMigrationTests
{
    private readonly ITestOutputHelper _output;

    public CloudSyncMigrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact(Skip = "Manual Migration Script - ran successfully, 26 cariler synced to Supabase")]
    public async Task MigrateActualDesktopDbToSupabase()
    {
        string actualDb = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ErmayMuhasebe", "ermay_2026.db");
        _output.WriteLine($"Database exists: {File.Exists(actualDb)} at {actualDb}");
        Assert.True(File.Exists(actualDb), "Database file must exist");

        var yearContext = new YearContext { CurrentYear = 2026 };
        var dbService = new DatabaseService(yearContext);
        await dbService.InitializeAsync("ermay_2026.db");

        var cariler = await dbService.GetCarilerAsync();
        _output.WriteLine($"Loaded {cariler.Count} cariler from SQLite.");

        var stoklar = await dbService.GetStoklarAsync();
        _output.WriteLine($"Loaded {stoklar.Count} stoklar from SQLite.");

        var faturalar = await dbService.GetFaturalarAsync();
        _output.WriteLine($"Loaded {faturalar.Count} faturalar from SQLite.");

        _output.WriteLine($"IsCloudConnected: {dbService.IsCloudConnected}");
        
        await dbService.SyncService.SyncMaliYillarAsync(new[] { 2026 });
        await dbService.SyncToCloudAsync();
        _output.WriteLine("SyncToCloudAsync finished successfully!");
    }

    [Fact(Skip = "Live integration test; bidirectional sync is deprecated in favor of Inbox pattern")]
    public async Task TestBidirectionalSync_PullsMobilRecordToDesktop()
    {
        string actualDb = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ErmayMuhasebe", "ErmayV4_Stable.db3");
        ErmayMuhasebe.Data.Constants.DatabasePath = actualDb;
        var dbService = new DatabaseService();
        await dbService.InitializeAsync();

        var syncService = new CloudSyncService(new YearContext());
        
        // 0. Ensure clean state
        var old = await dbService.GetCariKartAsync(9999);
        if (old != null) await dbService.DeleteCariKartAsync(old);
        await syncService.DeleteAsync("cariler", 9999);
        await syncService.DeleteAsync("cari_hareketler", 99991);

        // 1. Ensure Cari and CariHareket exist in cloud
        await syncService.SyncCariAsync(new CariKart
        {
            Id = 9999,
            CariKod = "MOB-001",
            Unvan = "Mobil Test Müşteri",
            Il = "İstanbul"
        });

        await syncService.SyncCariHareketAsync(new CariHareket
        {
            Id = 99991,
            CariId = 9999,
            IslemTuru = "Nakit Tahsilat",
            EvrakNo = "EVR-9999",
            Borc = 1500m,
            Alacak = 500m,
            Tarih = DateTime.UtcNow
        });

        // 2. Pull from cloud to local SQLite
        await dbService.SyncFromCloudAsync();

        var pulled = await dbService.GetCariKartAsync(9999);
        Assert.NotNull(pulled);
        Assert.Equal("Mobil Test Müşteri", pulled.Unvan);
        Assert.Equal("MOB-001", pulled.CariKod);
        Assert.Equal("İstanbul", pulled.Il);

        var pulledHareketler = await dbService.GetCariHareketlerAsync(9999);
        _output.WriteLine($"Pulled {pulledHareketler.Count} hareketler for cari 9999.");
        Assert.NotEmpty(pulledHareketler);

        // 3. Clean up test records
        await dbService.DeleteCariKartAsync(pulled);
        await syncService.DeleteAsync("cariler", 9999);
        await syncService.DeleteAsync("cari_hareketler", 99991);
        _output.WriteLine("Cleaned up test records successfully.");
    }

    [Fact(Skip = "Diagnostic Script for specific invoice FTR-381497")]
    public async Task CheckInvoiceFtr381497Details()
    {
        string actualDb = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ErmayMuhasebe", "ErmayV4_Stable.db3");
        ErmayMuhasebe.Data.Constants.DatabasePath = actualDb;
        var dbService = new DatabaseService();
        await dbService.InitializeAsync();

        var sync = new CloudSyncService(new ErmayMuhasebe.Services.YearContext());
        var cloudFtrs = await sync.PullFaturalarAsync();
        var cloudTargetFtr = cloudFtrs?.FirstOrDefault(x => x.FaturaNo == "FTR-381497");
        _output.WriteLine($"Cloud Target Ftr: {(cloudTargetFtr != null ? $"Id={cloudTargetFtr.Id}, No={cloudTargetFtr.FaturaNo}" : "NULL")}");
        if (cloudTargetFtr != null)
        {
            var directDetails = await sync.PullFaturaDetaylarAsync(cloudTargetFtr.Id);
            _output.WriteLine($"Direct PullFaturaDetaylarAsync({cloudTargetFtr.Id}) count: {directDetails?.Count ?? -1}");
            if (directDetails != null)
            {
                foreach(var dd in directDetails)
                {
                    _output.WriteLine($"  DD: Id={dd.Id}, FaturaId={dd.FaturaId}, StokAdi={dd.StokAdi}, Miktar={dd.Miktar}, ToplamTutar={dd.ToplamTutar}");
                }
            }
        }

        var profil = await dbService.GetFirmaProfiliAsync();
        string diskLogo = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ErmayMuhasebe", "company_logo.png");
        _output.WriteLine($"FirmaProfili in SQLite: FirmaAdi={profil.FirmaAdi}, HasLogoBase64={!string.IsNullOrEmpty(profil.LogoBase64)}, LogoBase64Length={profil.LogoBase64?.Length ?? 0}, LogoFatura={profil.LogoFatura}");
        _output.WriteLine($"company_logo.png exists on disk: {File.Exists(diskLogo)} (length={ (File.Exists(diskLogo) ? new FileInfo(diskLogo).Length : 0) })");

        var cloudProfil = await sync.PullFirmaProfiliAsync();
        _output.WriteLine($"Cloud FirmaProfili: FirmaAdi={cloudProfil?.FirmaAdi}, HasLogoBase64={!string.IsNullOrEmpty(cloudProfil?.LogoBase64)}, LogoBase64Length={cloudProfil?.LogoBase64?.Length ?? 0}");

        var db = await dbService.GetGlobalConnectionAsync();
        var allDbFaturalar = await db.Table<Fatura>().ToListAsync();
        _output.WriteLine($"All Faturalar in DB count: {allDbFaturalar.Count}");
        foreach (var f in allDbFaturalar)
        {
            _output.WriteLine($"  DB Fatura: Id={f.Id}, No={f.FaturaNo}, Cari={f.CariUnvan}, Toplam={f.GenelToplam}, IsDeleted={f.IsDeleted}");
        }

        var allDbDetaylar = await db.Table<FaturaDetay>().ToListAsync();
        _output.WriteLine($"All FaturaDetay in DB count: {allDbDetaylar.Count}");
        foreach (var d in allDbDetaylar)
        {
            _output.WriteLine($"  DB Detay: Id={d.Id}, FaturaId={d.FaturaId}, StokAdi={d.StokAdi}, Miktar={d.Miktar}, Tutar={d.ToplamTutar}");
        }

        var ftr = allDbFaturalar.FirstOrDefault(x => x.FaturaNo == "FTR-381497");
        _output.WriteLine($"Fatura FTR-381497 after sync: {(ftr != null ? $"Found Id={ftr.Id}, Cari={ftr.CariUnvan}, GenelToplam={ftr.GenelToplam}" : "NOT FOUND")}");

        if (ftr != null)
        {
            var uow = new ErmayMuhasebe.Repositories.UnitOfWork(dbService);
            var detaylar = await uow.Faturalar.GetDetaylarAsync(ftr.Id);
            _output.WriteLine($"Detaylar count for FTR-381497 (Id={ftr.Id}): {detaylar.Count}");
            foreach (var d in detaylar)
            {
                _output.WriteLine($"  Detay: Id={d.Id}, StokId={d.StokId}, StokKodu={d.StokKodu}, StokAdi={d.StokAdi}, Miktar={d.Miktar}, BirimFiyat={d.BirimFiyat}, ToplamTutar={d.ToplamTutar}");
            }
            Assert.NotEmpty(detaylar);

            // Test PDF Generation
            var pdfService = new PdfService();
            var pdfBytes = pdfService.GenerateFaturaPdf(ftr, detaylar);
            _output.WriteLine($"Generated Fatura PDF byte length: {pdfBytes.Length}");
            Assert.True(pdfBytes.Length > 1000);
        }

        _output.WriteLine($"End of test.");
    }
}

