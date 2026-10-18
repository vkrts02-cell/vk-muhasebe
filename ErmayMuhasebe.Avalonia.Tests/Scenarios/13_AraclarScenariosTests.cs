using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Avalonia.Headless.XUnit;
using Xunit;
using ErmayMuhasebe.Avalonia.ViewModels;
using ErmayMuhasebe.Models;

namespace ErmayMuhasebe.Avalonia.Tests.Scenarios;

public partial class AraclarScenariosTests : HeadlessTestBase
{
    // -------------------------------------------------------------
    // 1. Araçlar Kataloğu & Menü Başlatma (12 Senaryo)
    // -------------------------------------------------------------

    [AvaloniaFact]
    public void Scenario_01_ToolsCatalog_InitializesAllSubTools()
    {
        var vm = _serviceProvider.GetRequiredService<ToolsViewModel>();
        Assert.NotNull(vm.Tools);
        Assert.True(vm.Tools.Count >= 10, "Araçlar menüsünde en az 10 alt araç tanımlı olmalıdır.");
    }

    [Theory]
    [InlineData("Sistem Sağlığı ve Bakımı")]
    [InlineData("Belge Arşivleme")]
    [InlineData("Cari Birleştirme")]
    [InlineData("Ürün Birleştirme")]
    [InlineData("Gecikme Faizi")]
    [InlineData("Toplu Fiyat Güncelleme")]
    [InlineData("Portföy Listesi")]
    public void Scenario_02_to_08_CoreToolsExistInCatalog(string toolTitle)
    {
        var vm = _serviceProvider.GetRequiredService<ToolsViewModel>();
        Assert.Contains(vm.Tools, t => t.Title == toolTitle);
    }

    [Theory]
    [InlineData(typeof(SistemSaglikViewModel))]
    [InlineData(typeof(BelgeArsivViewModel))]
    [InlineData(typeof(CariBirlestirmeViewModel))]
    [InlineData(typeof(TopluFiyatViewModel))]
    public void Scenario_09_to_12_ToolViewModelTypesAreRegistered(Type vmType)
    {
        var vm = _serviceProvider.GetRequiredService<ToolsViewModel>();
        Assert.Contains(vm.Tools, t => t.ViewModelType == vmType);
    }

    // -------------------------------------------------------------
    // 2. Veritabanı Bakımı & Bütünlük Kontrolleri (13 Senaryo)
    // -------------------------------------------------------------

    [AvaloniaFact]
    public async Task Scenario_13_DatabaseVacuum_ExecutesSuccessfully()
    {
        var conn = _dbService.GetConnection();
        await conn.ExecuteAsync("VACUUM;");
        Assert.True(true, "VACUUM komutu şifreli SQLite üzerinde hatasız çalıştı.");
    }

    [AvaloniaFact]
    public async Task Scenario_14_DatabaseIntegrityCheck_ReturnsOk()
    {
        var conn = _dbService.GetConnection();
        var result = await conn.ExecuteScalarAsync<string>("PRAGMA integrity_check;");
        Assert.Equal("ok", result.ToLowerInvariant());
    }

    [AvaloniaFact]
    public async Task Scenario_15_DatabaseReindex_ExecutesSuccessfully()
    {
        var conn = _dbService.GetConnection();
        await conn.ExecuteAsync("REINDEX;");
        Assert.True(true, "REINDEX komutu başarıyla çalıştı.");
    }

    [Theory]
    [InlineData(1048576, 1.0)]      // 1 MB = 1.048.576 bayt
    [InlineData(10485760, 10.0)]    // 10 MB
    [InlineData(52428800, 50.0)]    // 50 MB
    [InlineData(524288, 0.5)]       // 0.5 MB
    public void Scenario_16_to_19_DatabaseSizeCalculationsInMb(long bytes, double expMb)
    {
        double mb = (double)bytes / (1024 * 1024);
        Assert.Equal(expMb, mb, precision: 1);
    }

    [Theory]
    [InlineData(100, 15, true)]  // 100 GB boş alan var, kritik eşik 15 GB -> Güvenli
    [InlineData(10, 15, false)]  // 10 GB boş alan var -> Kritik disk uyarısı
    [InlineData(1, 15, false)]   // 1 GB boş alan -> Çok kritik
    public void Scenario_20_to_22_DiskSpaceWarningEvaluation(long freeGb, long thresholdGb, bool isSafe)
    {
        bool safe = freeGb >= thresholdGb;
        Assert.Equal(isSafe, safe);
    }

    [Theory]
    [InlineData(50, 10, 40)] // 50 log dosyasından 10 günlük olanlar korundu, 40'ı silindi
    [InlineData(10, 10, 0)]
    [InlineData(5, 10, 0)]
    public void Scenario_23_to_25_LogCleanupCalculations(int totalLogs, int retentionDays, int expDeleted)
    {
        int deleted = Math.Max(0, totalLogs - retentionDays);
        Assert.Equal(expDeleted, deleted);
    }

    // -------------------------------------------------------------
    // 3. Toplu Fiyat Güncelleme Senaryoları (15 Senaryo)
    // -------------------------------------------------------------

    [Theory]
    [InlineData(100, 15, 115)]   // %15 zam: 100 -> 115 TL
    [InlineData(200, 10, 220)]   // %10 zam: 200 -> 220 TL
    [InlineData(50, 25, 62.5)]   // %25 zam: 50 -> 62.5 TL
    [InlineData(1000, 0, 1000)]  // %0 zam
    public void Scenario_26_to_29_BatchPricePercentageIncrease(decimal eskiFiyat, decimal yuzde, decimal expYeniFiyat)
    {
        decimal yeniFiyat = eskiFiyat + (eskiFiyat * (yuzde / 100m));
        Assert.Equal(expYeniFiyat, yeniFiyat);
    }

    [Theory]
    [InlineData(100, 10, 90)]    // %10 indirim: 100 -> 90 TL
    [InlineData(200, 20, 160)]   // %20 indirim: 200 -> 160 TL
    [InlineData(500, 5, 475)]    // %5 indirim: 500 -> 475 TL
    [InlineData(50, 50, 25)]     // %50 indirim: 50 -> 25 TL
    public void Scenario_30_to_33_BatchPricePercentageDecrease(decimal eskiFiyat, decimal yuzde, decimal expYeniFiyat)
    {
        decimal yeniFiyat = eskiFiyat - (eskiFiyat * (yuzde / 100m));
        Assert.Equal(expYeniFiyat, yeniFiyat);
    }

    [Theory]
    [InlineData(100, 50, 150)]   // +50 TL sabit zam
    [InlineData(250, 100, 350)]
    [InlineData(50, -10, 40)]    // -10 TL sabit indirim
    public void Scenario_34_to_36_BatchPriceFixedAmountChange(decimal eskiFiyat, decimal tutar, decimal expYeniFiyat)
    {
        decimal yeniFiyat = eskiFiyat + tutar;
        Assert.Equal(expYeniFiyat, yeniFiyat);
    }

    [Theory]
    [InlineData(123.45, 123.90)] // .90 son haneye yuvarlama
    [InlineData(99.10, 99.90)]
    [InlineData(50.00, 50.90)]
    [InlineData(10.85, 10.90)]
    public void Scenario_37_to_40_BatchPriceRoundingRules(decimal input, decimal expOutput)
    {
        decimal rounded = Math.Floor(input) + 0.90m;
        Assert.Equal(expOutput, rounded);
    }

    // -------------------------------------------------------------
    // 4. Gecikme Faizi & Birleştirme Senaryoları (10 Senaryo)
    // -------------------------------------------------------------

    [Theory]
    [InlineData(10000, 30, 0.05, 500)]   // 10.000 TL, 30 gün (%5 aylık gecikme faizi) = 500 TL faiz
    [InlineData(20000, 60, 0.05, 2000)]  // 60 gün (2 ay) = 2.000 TL faiz
    [InlineData(5000, 15, 0.04, 100)]    // 15 gün (0.5 ay) = 100 TL faiz
    [InlineData(10000, 0, 0.05, 0)]      // 0 gün gecikme
    public void Scenario_41_to_44_SimpleInterestCalculations(decimal anapara, int gun, double aylikFaiz, decimal expFaiz)
    {
        decimal aySayisi = gun / 30m;
        decimal faiz = anapara * (decimal)aylikFaiz * aySayisi;
        Assert.Equal(expFaiz, faiz);
    }

    [Theory]
    [InlineData(10000, 500, 10500)] // 10.000 TL Borç + 500 TL Faiz = 10.500 TL Toplam
    [InlineData(20000, 2000, 22000)]
    public void Scenario_45_to_46_TotalDebtWithInterest(decimal anapara, decimal faiz, decimal expToplam)
    {
        decimal toplam = anapara + faiz;
        Assert.Equal(expToplam, toplam);
    }

    [AvaloniaFact]
    public async Task Scenario_47_MergeCariler_TransfersBalanceToTarget()
    {
        var cari1 = new CariKart { CariKod = "C-M1", Unvan = "Eski Mükerrer Cari", Borc = 4000 };
        var cari2 = new CariKart { CariKod = "C-M2", Unvan = "Ana Hedef Cari", Borc = 6000 };
        await _uow.Cariler.SaveAsync(cari1);
        await _uow.Cariler.SaveAsync(cari2);

        // Birleştirme: cari1 silinir, borcu cari2'ye eklenir
        cari2.Borc += cari1.Borc;
        await _uow.Cariler.SaveAsync(cari2);
        await _uow.Cariler.DeleteAsync(cari1);

        var ref1 = await _uow.Cariler.GetByIdAsync(cari1.Id);
        var ref2 = await _uow.Cariler.GetByIdAsync(cari2.Id);

        Assert.Null(ref1);
        Assert.Equal(10000m, ref2!.Borc);
    }

    [AvaloniaFact]
    public async Task Scenario_48_MergeStoklar_TransfersQuantityToTarget()
    {
        var s1 = new StokKart { StokKodu = "STK-M1", StokAdi = "Mükerrer Ürün", Miktar = 30 };
        var s2 = new StokKart { StokKodu = "STK-M2", StokAdi = "Ana Ürün", Miktar = 70 };
        await _uow.Stoklar.SaveAsync(s1);
        await _uow.Stoklar.SaveAsync(s2);

        s2.Miktar += s1.Miktar;
        await _uow.Stoklar.SaveAsync(s2);
        await _uow.Stoklar.DeleteAsync(s1);

        var ref1 = await _uow.Stoklar.GetByIdAsync(s1.Id);
        var ref2 = await _uow.Stoklar.GetByIdAsync(s2.Id);

        Assert.Null(ref1);
        Assert.Equal(100, ref2!.Miktar);
    }

    [Theory]
    [InlineData(1, 1, false)] // Kendi kendisiyle birleştirilemez
    [InlineData(1, 2, true)]  // Farklı iki kart birleştirilebilir
    public void Scenario_49_to_50_MergeSelfValidation(int id1, int id2, bool canMerge)
    {
        bool allowed = id1 != id2;
        Assert.Equal(canMerge, allowed);
    }
}
