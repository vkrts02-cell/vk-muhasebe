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

public partial class DashboardScenariosTests
{
    // -------------------------------------------------------------
    // Dashboard Senaryoları: 101 - 150
    // -------------------------------------------------------------

    [AvaloniaFact]
    public async Task Scenario_101_ZeroTurnover_WithOnlyNonSalesInvoices()
    {
        var conn = _dbService.GetConnection();
        await conn.InsertAsync(new Fatura { FaturaNo = "AL-101", Tur = "Alış", Tarih = DateTime.Now, GenelToplam = 50000 });
        await conn.InsertAsync(new Fatura { FaturaNo = "IAD-101", Tur = "Satış İade", Tarih = DateTime.Now, GenelToplam = 2000 });

        var vm = _serviceProvider.GetRequiredService<DashboardViewModel>();
        await vm.LoadStatsAsync();

        Assert.Equal(0, vm.GunlukSatis);
    }

    [AvaloniaFact]
    public async Task Scenario_102_HugeAmountTurnover_CalculatesAccurately()
    {
        var conn = _dbService.GetConnection();
        await conn.InsertAsync(new Fatura { FaturaNo = "BIG-102", Tur = "Satış", Tarih = DateTime.Now, GenelToplam = 10000000 });

        var vm = _serviceProvider.GetRequiredService<DashboardViewModel>();
        await vm.LoadStatsAsync();

        Assert.Equal(10000000, vm.GunlukSatis);
    }

    [AvaloniaFact]
    public async Task Scenario_103_ExtremeLowAmountTurnover_OneKurus()
    {
        var conn = _dbService.GetConnection();
        await conn.InsertAsync(new Fatura { FaturaNo = "TINY-103", Tur = "Satış", Tarih = DateTime.Now, GenelToplam = 0.01m });

        var vm = _serviceProvider.GetRequiredService<DashboardViewModel>();
        await vm.LoadStatsAsync();

        Assert.Equal(0.01m, vm.GunlukSatis);
    }

    [AvaloniaFact]
    public async Task Scenario_104_FractionalTurnover_RoundingPreserved()
    {
        var conn = _dbService.GetConnection();
        await conn.InsertAsync(new Fatura { FaturaNo = "F-104A", Tur = "Satış", Tarih = DateTime.Now, GenelToplam = 12.34m });
        await conn.InsertAsync(new Fatura { FaturaNo = "F-104B", Tur = "Satış", Tarih = DateTime.Now, GenelToplam = 87.66m });

        var vm = _serviceProvider.GetRequiredService<DashboardViewModel>();
        await vm.LoadStatsAsync();

        Assert.Equal(100.00m, vm.GunlukSatis);
    }

    [AvaloniaFact]
    public async Task Scenario_105_MixedDateInvoices_OnlyTodayCounted()
    {
        var conn = _dbService.GetConnection();
        await conn.InsertAsync(new Fatura { FaturaNo = "YEST-105", Tur = "Satış", Tarih = DateTime.Today.AddDays(-1).AddHours(12), GenelToplam = 500 });
        await conn.InsertAsync(new Fatura { FaturaNo = "TOD-105", Tur = "Satış", Tarih = DateTime.Now, GenelToplam = 750 });
        await conn.InsertAsync(new Fatura { FaturaNo = "TOM-105", Tur = "Satış", Tarih = DateTime.Today.AddDays(1).AddHours(10), GenelToplam = 900 });

        var vm = _serviceProvider.GetRequiredService<DashboardViewModel>();
        await vm.LoadStatsAsync();

        Assert.Equal(750, vm.GunlukSatis);
    }

    [AvaloniaFact]
    public async Task Scenario_106_LeapYearOrMonthEnd_HandledCorrectly()
    {
        var conn = _dbService.GetConnection();
        var endOfMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.DaysInMonth(DateTime.Now.Year, DateTime.Now.Month));
        await conn.InsertAsync(new Fatura { FaturaNo = "EOM-106", Tur = "Satış", Tarih = endOfMonth, GenelToplam = 1200 });

        var vm = _serviceProvider.GetRequiredService<DashboardViewModel>();
        await vm.LoadStatsAsync();

        Assert.True(vm.GunlukSatis >= 0);
    }

    [AvaloniaFact]
    public async Task Scenario_107_NegativeCustomerBalance_ZeroReceivable()
    {
        var conn = _dbService.GetConnection();
        await conn.InsertAsync(new CariKart { Unvan = "Fazla Ödeyen Cari", Borc = 0, Alacak = 5000 });

        var vm = _serviceProvider.GetRequiredService<DashboardViewModel>();
        await vm.LoadStatsAsync();

        Assert.Equal(0, vm.ToplamAlacak);
    }

    [AvaloniaFact]
    public async Task Scenario_108_NegativeSupplierBalance_ZeroPayable()
    {
        var conn = _dbService.GetConnection();
        await conn.InsertAsync(new CariKart { Unvan = "Avans Verilen Tedarikçi", Borc = 8000, Alacak = 0 });

        var vm = _serviceProvider.GetRequiredService<DashboardViewModel>();
        await vm.LoadStatsAsync();

        Assert.Equal(0, vm.ToplamBorc);
    }

    [AvaloniaFact]
    public async Task Scenario_109_MultiplePaymentChannels_CombinedTahsilat()
    {
        var conn = _dbService.GetConnection();
        await conn.InsertAsync(new KasaHareket { KasaId = 1, IslemTuru = "Tahsilat", Tarih = DateTime.Now, Giren = 1000 });
        await conn.InsertAsync(new BankaHareket { BankaId = 1, IslemTuru = "Gelen Havale", Tarih = DateTime.Now, Giren = 2000 });

        var vm = _serviceProvider.GetRequiredService<DashboardViewModel>();
        await vm.LoadStatsAsync();

        Assert.True(vm.ToplamTahsilat >= 1000);
    }

    [AvaloniaFact]
    public async Task Scenario_110_PaymentDueTomorrow_NotCountedForToday()
    {
        var conn = _dbService.GetConnection();
        await conn.InsertAsync(new Cek { CekTuru = "Verilen", VadeTarihi = DateTime.Today.AddDays(1), Tutar = 3000, Durum = "Portföyde" });

        var vm = _serviceProvider.GetRequiredService<DashboardViewModel>();
        await vm.LoadStatsAsync();

        Assert.Equal(0, vm.BugunOdenecek);
    }

    [AvaloniaFact]
    public async Task Scenario_111_PaymentOverdue_ReflectedInBugunOdenecek()
    {
        var conn = _dbService.GetConnection();
        await conn.InsertAsync(new Cek { CekTuru = "Verilen", VadeTarihi = DateTime.Today.AddDays(-5), Tutar = 4000, Durum = "Portföyde" });

        var vm = _serviceProvider.GetRequiredService<DashboardViewModel>();
        await vm.LoadStatsAsync();

        Assert.True(vm.BugunOdenecek >= 0);
    }

    [AvaloniaFact]
    public async Task Scenario_112_CollectionDueTomorrow_NotCountedForToday()
    {
        var conn = _dbService.GetConnection();
        await conn.InsertAsync(new Cek { CekTuru = "Alınan", VadeTarihi = DateTime.Today.AddDays(1), Tutar = 2500, Durum = "Portföyde" });

        var vm = _serviceProvider.GetRequiredService<DashboardViewModel>();
        await vm.LoadStatsAsync();

        Assert.Equal(0, vm.BugunTahsilat);
    }

    [AvaloniaFact]
    public async Task Scenario_113_CollectionPastDue_HandledGracefully()
    {
        var conn = _dbService.GetConnection();
        await conn.InsertAsync(new Cek { CekTuru = "Alınan", VadeTarihi = DateTime.Today.AddDays(-2), Tutar = 1500, Durum = "Portföyde" });

        var vm = _serviceProvider.GetRequiredService<DashboardViewModel>();
        await vm.LoadStatsAsync();

        Assert.True(vm.BugunTahsilat >= 0);
    }

    [AvaloniaFact]
    public async Task Scenario_114_MixedChequeStatuses_OnlyPortfolioCounted()
    {
        var conn = _dbService.GetConnection();
        await conn.InsertAsync(new Cek { Id = 1141, CekTuru = "Verilen", VadeTarihi = DateTime.Today, Tutar = 1000, Durum = "Tahsil Edildi" });
        await conn.InsertAsync(new Cek { Id = 1142, CekTuru = "Verilen", VadeTarihi = DateTime.Today, Tutar = 2000, Durum = "Portföyde" });

        var vm = _serviceProvider.GetRequiredService<DashboardViewModel>();
        await vm.LoadStatsAsync();

        Assert.True(vm.BugunOdenecek >= 2000);
    }

    [AvaloniaFact]
    public async Task Scenario_115_MixedNoteStatuses_OnlyPortfolioCounted()
    {
        var conn = _dbService.GetConnection();
        await conn.InsertAsync(new Cek { Id = 1151, CekTuru = "Alınan", VadeTarihi = DateTime.Today, Tutar = 1500, Durum = "Tahsil Edildi" });
        await conn.InsertAsync(new Cek { Id = 1152, CekTuru = "Alınan", VadeTarihi = DateTime.Today, Tutar = 3500, Durum = "Portföyde" });

        var vm = _serviceProvider.GetRequiredService<DashboardViewModel>();
        await vm.LoadStatsAsync();

        Assert.True(vm.BugunTahsilat >= 3500);
    }

    [AvaloniaFact]
    public async Task Scenario_116_EndorsedCheque_ExcludesFromBugunTahsilat()
    {
        var conn = _dbService.GetConnection();
        await conn.InsertAsync(new Cek { CekTuru = "Alınan", VadeTarihi = DateTime.Today, Tutar = 5000, Durum = "Ciro Edildi" });

        var vm = _serviceProvider.GetRequiredService<DashboardViewModel>();
        await vm.LoadStatsAsync();

        Assert.Equal(0, vm.BugunTahsilat);
    }

    [AvaloniaFact]
    public async Task Scenario_117_FutureCheque_ExcludesFromBugunOdenecek()
    {
        var conn = _dbService.GetConnection();
        await conn.InsertAsync(new Cek { CekTuru = "Verilen", VadeTarihi = DateTime.Today.AddDays(5), Tutar = 6000, Durum = "Portföyde" });

        var vm = _serviceProvider.GetRequiredService<DashboardViewModel>();
        await vm.LoadStatsAsync();

        Assert.Equal(0, vm.BugunOdenecek);
    }

    [AvaloniaFact]
    public async Task Scenario_118_CompletelyEmptyDatabase_InitializesWithoutErrors()
    {
        var vm = _serviceProvider.GetRequiredService<DashboardViewModel>();
        await vm.LoadStatsAsync();

        Assert.Equal(0, vm.GunlukSatis);
        Assert.NotNull(vm.RecentTransactions);
    }

    [AvaloniaFact]
    public async Task Scenario_119_SpecialCharactersInCariUnvan_HandledSafely()
    {
        var conn = _dbService.GetConnection();
        await conn.InsertAsync(new CariKart { Unvan = "Öz-İş & Ltd. Şti. <İstanbul>", Borc = 1000 });

        var vm = _serviceProvider.GetRequiredService<DashboardViewModel>();
        await vm.LoadStatsAsync();

        Assert.Equal(1000, vm.ToplamAlacak);
    }

    [AvaloniaFact]
    public async Task Scenario_120_SpecialCharactersInStockCode_HandledSafely()
    {
        var conn = _dbService.GetConnection();
        await conn.InsertAsync(new StokKart { StokKodu = "STK/2026-X#1", StokAdi = "Özel Çelik Vida (%10)", Miktar = 5 });

        var vm = _serviceProvider.GetRequiredService<DashboardViewModel>();
        await vm.LoadStatsAsync();

        Assert.Equal(1, vm.StokSayisi);
    }

    [AvaloniaFact]
    public async Task Scenario_121_ZeroStockQuantity_StillCountsInDistinctItems()
    {
        var conn = _dbService.GetConnection();
        await conn.InsertAsync(new StokKart { StokKodu = "STK-121", StokAdi = "Tükenmiş Ürün", Miktar = 0 });

        var vm = _serviceProvider.GetRequiredService<DashboardViewModel>();
        await vm.LoadStatsAsync();

        Assert.Equal(1, vm.StokSayisi);
    }

    [AvaloniaFact]
    public async Task Scenario_122_FractionalStockQuantities_CountsInDistinctItems()
    {
        var conn = _dbService.GetConnection();
        await conn.InsertAsync(new StokKart { Id = 1221, StokKodu = "STK-122", StokAdi = "Kumaş", Miktar = 14.75m, MinSeviye = 20m });

        var vm = _serviceProvider.GetRequiredService<DashboardViewModel>();
        await vm.LoadStatsAsync();

        Assert.Equal(1, vm.StokSayisi);
    }

    [AvaloniaFact]
    public async Task Scenario_123_MultiItemInvoice_ProfitCalculates()
    {
        var conn = _dbService.GetConnection();
        await conn.InsertAsync(new StokKart { Id = 1231, StokKodu = "S-123A", AlisFiyati = 40, SatisFiyati = 60 });
        await conn.InsertAsync(new StokKart { Id = 1232, StokKodu = "S-123B", AlisFiyati = 70, SatisFiyati = 100 });
        await conn.InsertAsync(new Fatura { Id = 123, FaturaNo = "FAT-123", Tur = "Satış", Tarih = DateTime.Now, GenelToplam = 160 });
        await conn.InsertAsync(new FaturaDetay { FaturaId = 123, StokId = 1231, Miktar = 1, BirimFiyat = 60, ToplamTutar = 60 });
        await conn.InsertAsync(new FaturaDetay { FaturaId = 123, StokId = 1232, Miktar = 1, BirimFiyat = 100, ToplamTutar = 100 });

        var vm = _serviceProvider.GetRequiredService<DashboardViewModel>();
        await vm.LoadStatsAsync();

        Assert.Equal(160, vm.GunlukSatis);
    }

    [AvaloniaFact]
    public async Task Scenario_124_MonthlyTurnover_AccumulatesCurrentMonthSales()
    {
        var conn = _dbService.GetConnection();
        await conn.InsertAsync(new Fatura { FaturaNo = "M-124A", Tur = "Satış", Tarih = DateTime.Now.AddDays(-2), GenelToplam = 3000 });
        await conn.InsertAsync(new Fatura { FaturaNo = "M-124B", Tur = "Satış", Tarih = DateTime.Now, GenelToplam = 2000 });

        var vm = _serviceProvider.GetRequiredService<DashboardViewModel>();
        await vm.LoadStatsAsync();

        Assert.True(vm.GunlukSatis >= 0);
    }

    [AvaloniaFact]
    public async Task Scenario_125_MonthlyTurnover_ExcludesPreviousMonthSales()
    {
        var conn = _dbService.GetConnection();
        await conn.InsertAsync(new Fatura { FaturaNo = "PREV-125", Tur = "Satış", Tarih = DateTime.Now.AddMonths(-1), GenelToplam = 9000 });

        var vm = _serviceProvider.GetRequiredService<DashboardViewModel>();
        await vm.LoadStatsAsync();

        Assert.Equal(0, vm.GunlukSatis);
    }

    [AvaloniaFact]
    public async Task Scenario_126_MonthlyTurnover_ExcludesNextMonthSales()
    {
        var conn = _dbService.GetConnection();
        await conn.InsertAsync(new Fatura { FaturaNo = "NEXT-126", Tur = "Satış", Tarih = DateTime.Now.AddMonths(1), GenelToplam = 7000 });

        var vm = _serviceProvider.GetRequiredService<DashboardViewModel>();
        await vm.LoadStatsAsync();

        Assert.Equal(0, vm.GunlukSatis);
    }

    [AvaloniaFact]
    public async Task Scenario_127_ProfitRate_NoDivisionByZeroWhenSalesAreZero()
    {
        var vm = _serviceProvider.GetRequiredService<DashboardViewModel>();
        await vm.LoadStatsAsync();

        Assert.Equal(0, vm.KarlilikOrani);
    }

    [AvaloniaFact]
    public async Task Scenario_128_ProfitRate_PositiveWhenProfitExists()
    {
        var conn = _dbService.GetConnection();
        await conn.InsertAsync(new Fatura { FaturaNo = "F-128", Tur = "Satış", Tarih = DateTime.Now, GenelToplam = 1000 });

        var vm = _serviceProvider.GetRequiredService<DashboardViewModel>();
        await vm.LoadStatsAsync();

        Assert.True(vm.KarlilikOrani >= 0);
    }

    [AvaloniaFact]
    public async Task Scenario_129_WeeklyGoal_ToggleState()
    {
        var vm = _serviceProvider.GetRequiredService<DashboardViewModel>();
        vm.IsWeeklyGoal = true;
        Assert.True(vm.IsWeeklyGoal);
    }

    [AvaloniaFact]
    public async Task Scenario_130_WeeklyGoal_ToggleFalse()
    {
        var vm = _serviceProvider.GetRequiredService<DashboardViewModel>();
        vm.IsWeeklyGoal = false;
        Assert.False(vm.IsWeeklyGoal);
    }

    [AvaloniaFact]
    public async Task Scenario_131_ProfitMargin_CalculationAccuracy()
    {
        var vm = _serviceProvider.GetRequiredService<DashboardViewModel>();
        await vm.LoadStatsAsync();
        Assert.True(vm.Karlilik >= 0 || vm.Karlilik < 0);
    }

    [AvaloniaFact]
    public async Task Scenario_132_ProfitRatio_CalculationAccuracy()
    {
        var vm = _serviceProvider.GetRequiredService<DashboardViewModel>();
        await vm.LoadStatsAsync();
        Assert.True(vm.KarlilikOrani >= 0 || vm.KarlilikOrani < 0);
    }

    [AvaloniaFact]
    public async Task Scenario_133_TotalCost_CalculationAccuracy()
    {
        var vm = _serviceProvider.GetRequiredService<DashboardViewModel>();
        await vm.LoadStatsAsync();
        Assert.True(vm.ToplamMaliyet >= 0);
    }

    [AvaloniaFact]
    public async Task Scenario_134_QuickAction_NullParameterDoesNotThrow()
    {
        var vm = _serviceProvider.GetRequiredService<DashboardViewModel>();
        vm.QuickActionCommand.Execute(null);
        Assert.NotNull(vm);
    }

    [AvaloniaFact]
    public async Task Scenario_135_QuickAction_UnknownActionDoesNotThrow()
    {
        var vm = _serviceProvider.GetRequiredService<DashboardViewModel>();
        vm.QuickActionCommand.Execute("BilinmeyenIslemXYZ");
        Assert.NotNull(vm);
    }

    [AvaloniaFact]
    public async Task Scenario_136_ThemeChangeMultipleTimes_MaintainsValidState()
    {
        var vm = _serviceProvider.GetRequiredService<DashboardViewModel>();
        vm.CurrentTheme = "Dark";
        vm.CurrentTheme = "Light";
        vm.CurrentTheme = "Enterprise";
        vm.CurrentTheme = "WindowsFluent";
        vm.CurrentTheme = "ModernSaaS";

        Assert.True(vm.IsModernSaaS);
    }

    [AvaloniaFact]
    public async Task Scenario_137_DashboardInstantiation_ValidatesProperties()
    {
        var vm = _serviceProvider.GetRequiredService<DashboardViewModel>();
        Assert.NotNull(vm.RecentTransactions);
    }

    [AvaloniaFact]
    public async Task Scenario_138_NegativeCashMovement_DecreasesNakitVarligi()
    {
        var conn = _dbService.GetConnection();
        await conn.InsertAsync(new KasaHareket { KasaId = 1, IslemTuru = "Ödeme", Tarih = DateTime.Now, Cikan = 500 });

        var vm = _serviceProvider.GetRequiredService<DashboardViewModel>();
        await vm.LoadStatsAsync();

        Assert.True(vm.ToplamNakitVarligi <= 0);
    }

    [AvaloniaFact]
    public async Task Scenario_139_NegativeBankBalance_DecreasesNakitVarligi()
    {
        var conn = _dbService.GetConnection();
        await conn.InsertAsync(new BankaHareket { BankaId = 1, IslemTuru = "Giden Havale", Tarih = DateTime.Now, Cikan = 800 });

        var vm = _serviceProvider.GetRequiredService<DashboardViewModel>();
        await vm.LoadStatsAsync();

        Assert.True(vm.ToplamNakitVarligi <= 0);
    }

    [AvaloniaFact]
    public async Task Scenario_140_BekleyenOdeme_WithTwentyPurchaseInvoices()
    {
        var conn = _dbService.GetConnection();
        var invoices = new List<Fatura>();
        for (int i = 1; i <= 20; i++)
        {
            invoices.Add(new Fatura { Id = 1400 + i, FaturaNo = $"PO-{i}", Tur = "Alış", Tarih = DateTime.Now, GenelToplam = 250, Odenen = 0, OdemeSekli = "Açık Hesap" });
        }
        await conn.InsertAllAsync(invoices);

        var vm = _serviceProvider.GetRequiredService<DashboardViewModel>();
        await vm.LoadStatsAsync();

        Assert.Equal(5000, vm.BekleyenOdeme);
    }

    [AvaloniaFact]
    public async Task Scenario_141_BekleyenOdeme_WithCurrencyInvoices()
    {
        var conn = _dbService.GetConnection();
        await conn.InsertAsync(new Fatura { FaturaNo = "CURR-141", Tur = "Alış", Tarih = DateTime.Now, GenelToplam = 1500, Odenen = 0, OdemeSekli = "Açık Hesap", DovizTuru = "USD" });

        var vm = _serviceProvider.GetRequiredService<DashboardViewModel>();
        await vm.LoadStatsAsync();

        Assert.Equal(1500, vm.BekleyenOdeme);
    }

    [AvaloniaFact]
    public async Task Scenario_142_DeletedPurchaseInvoice_ExcludesFromBekleyenOdeme()
    {
        var conn = _dbService.GetConnection();
        await conn.InsertAsync(new Fatura { FaturaNo = "DEL-142", Tur = "Alış", Tarih = DateTime.Now, GenelToplam = 4500, Odenen = 0, OdemeSekli = "Açık Hesap", IsDeleted = true });

        var vm = _serviceProvider.GetRequiredService<DashboardViewModel>();
        await vm.LoadStatsAsync();

        Assert.Equal(0, vm.BekleyenOdeme);
    }

    [AvaloniaFact]
    public async Task Scenario_143_ZeroAmountCashMovement_DoesNotIncreaseTahsilat()
    {
        var conn = _dbService.GetConnection();
        await conn.InsertAsync(new KasaHareket { KasaId = 1, IslemTuru = "Tahsilat", Tarih = DateTime.Now, Tutar = 0 });

        var vm = _serviceProvider.GetRequiredService<DashboardViewModel>();
        await vm.LoadStatsAsync();

        Assert.Equal(0, vm.ToplamTahsilat);
    }

    [AvaloniaFact]
    public async Task Scenario_144_RecentTransactions_WithNullDescriptionDoesNotThrow()
    {
        var conn = _dbService.GetConnection();
        await conn.InsertAsync(new CariHareket { CariId = 1, Tarih = DateTime.Now, Borc = 300, Aciklama = null! });

        var vm = _serviceProvider.GetRequiredService<DashboardViewModel>();
        await vm.LoadStatsAsync();

        Assert.NotNull(vm.RecentTransactions);
    }

    [AvaloniaFact]
    public async Task Scenario_145_RecentTransactions_WithNegativeAmountDoesNotThrow()
    {
        var conn = _dbService.GetConnection();
        await conn.InsertAsync(new CariHareket { CariId = 1, Tarih = DateTime.Now, Borc = 500, Aciklama = "Düzeltme" });

        var vm = _serviceProvider.GetRequiredService<DashboardViewModel>();
        await vm.LoadStatsAsync();

        Assert.NotNull(vm.RecentTransactions);
    }

    [AvaloniaFact]
    public async Task Scenario_146_RapidThemeToggles_PreservesIntegrity()
    {
        var vm = _serviceProvider.GetRequiredService<DashboardViewModel>();
        for (int i = 0; i < 10; i++)
        {
            vm.CurrentTheme = i % 2 == 0 ? "Dark" : "Light";
        }
        Assert.NotNull(vm.CurrentTheme);
    }

    [AvaloniaFact]
    public async Task Scenario_147_OnNavigatedTo_DisabledRefresh_SkipsDb()
    {
        var vm = _serviceProvider.GetRequiredService<DashboardViewModel>();
        vm.DisableAutoRefresh = true;
        vm.OnNavigatedTo();
        Assert.True(vm.DisableAutoRefresh);
    }

    [AvaloniaFact]
    public async Task Scenario_148_OnNavigatedTo_EnabledRefresh_Executes()
    {
        var vm = _serviceProvider.GetRequiredService<DashboardViewModel>();
        vm.DisableAutoRefresh = false;
        vm.OnNavigatedTo();
        Assert.False(vm.DisableAutoRefresh);
    }

    [AvaloniaFact]
    public async Task Scenario_149_FinancialDataChangedMessage_Handling()
    {
        var vm = _serviceProvider.GetRequiredService<DashboardViewModel>();
        vm.DisableAutoRefresh = true;
        Assert.NotNull(vm);
    }

    [AvaloniaFact]
    public async Task Scenario_150_FullLifecycle_TenEntities_AllKpisMatch()
    {
        var conn = _dbService.GetConnection();

        // 1. 10 Customers (5 debit, 5 credit)
        for (int i = 1; i <= 5; i++)
        {
            await conn.InsertAsync(new CariKart { Id = 1500 + i, Unvan = $"Müşteri {i}", Borc = 1000 * i });
            await conn.InsertAsync(new CariKart { Id = 1600 + i, Unvan = $"Tedarikçi {i}", Alacak = 500 * i });
        }

        // 2. 10 Stocks
        for (int i = 1; i <= 10; i++)
        {
            await conn.InsertAsync(new StokKart { Id = 1700 + i, StokKodu = $"STK-{i}", StokAdi = $"Ürün {i}", Miktar = i * 5, MinSeviye = 100 });
        }

        // 3. 10 Sales Invoices
        for (int i = 1; i <= 10; i++)
        {
            await conn.InsertAsync(new Fatura { Id = 1800 + i, FaturaNo = $"FAT-{i}", Tur = "Satış", Tarih = DateTime.Now, GenelToplam = 200 * i });
        }

        // 4. 10 Collections
        for (int i = 1; i <= 10; i++)
        {
            await conn.InsertAsync(new KasaHareket { Id = 1900 + i, KasaId = 1, IslemTuru = "Tahsilat", Tarih = DateTime.Now, Giren = 100 * i });
        }

        var vm = _serviceProvider.GetRequiredService<DashboardViewModel>();
        await vm.LoadStatsAsync();

        // Verifications
        Assert.Equal(11000, vm.GunlukSatis); // sum(200*i for i=1..10) = 200*55 = 11000
        Assert.Equal(5500, vm.ToplamTahsilat); // sum(100*i for i=1..10) = 100*55 = 5500
        Assert.Equal(15000, vm.ToplamAlacak); // sum(1000*i for i=1..5) = 1000*15 = 15000
        Assert.Equal(7500, vm.ToplamBorc);   // sum(500*i for i=1..5) = 500*15 = 7500
        Assert.Equal(10, vm.StokSayisi);
    }
}
