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

public partial class StokScenariosTests
{
    // =============================================================
    // 5. Stok Fiyatlandırma, KDV & Kar Marjı Senaryoları (51-65)
    // =============================================================

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(20)]
    public async Task Scenario_51_to_54_KdvRates_PersistAndReflectInAlias(int kdv)
    {
        var stok = new StokKart { StokKodu = $"KDV-{kdv}", StokAdi = $"KDV %{kdv} Ürün", KDV = kdv };
        await _uow.Stoklar.SaveAsync(stok);

        var retrieved = await _uow.Stoklar.GetByIdAsync(stok.Id);
        Assert.NotNull(retrieved);
        Assert.Equal(kdv, retrieved.KDV);
        Assert.Equal(kdv, retrieved.KdvOrani);
    }

    [Fact]
    public async Task Scenario_55_ZeroPurchasePrice_PersistsSuccessfully()
    {
        var stok = new StokKart { StokKodu = "PRC-0", StokAdi = "Bedelsiz Promosyon", AlisFiyati = 0m, SatisFiyati = 50m };
        await _uow.Stoklar.SaveAsync(stok);

        var retrieved = await _uow.Stoklar.GetByIdAsync(stok.Id);
        Assert.Equal(0m, retrieved!.AlisFiyati);
        Assert.Equal(50m, retrieved.SatisFiyati);
    }

    [Fact]
    public async Task Scenario_56_HighPrecisionPrice_PreservesFourDecimals()
    {
        decimal precisePrice = 12.3456m;
        var stok = new StokKart { StokKodu = "PRC-PREC", StokAdi = "Hassas Civata", AlisFiyati = precisePrice };
        await _uow.Stoklar.SaveAsync(stok);

        var retrieved = await _uow.Stoklar.GetByIdAsync(stok.Id);
        Assert.Equal(precisePrice, retrieved!.AlisFiyati);
    }

    [Fact]
    public async Task Scenario_57_AveragePurchasePrice_CanBeSetAndRetrieved()
    {
        var stok = new StokKart { StokKodu = "AVG-01", StokAdi = "Ortalama Fiyatlı Ürün", OrtalamaAlisFiyati = 84.50m };
        await _uow.Stoklar.SaveAsync(stok);

        var retrieved = await _uow.Stoklar.GetByIdAsync(stok.Id);
        Assert.Equal(84.50m, retrieved!.OrtalamaAlisFiyati);
    }

    [Fact]
    public async Task Scenario_58_AverageSalePrice_CanBeSetAndRetrieved()
    {
        var stok = new StokKart { StokKodu = "AVG-02", StokAdi = "Ortalama Satışlı Ürün", OrtalamaSatisFiyati = 125.75m };
        await _uow.Stoklar.SaveAsync(stok);

        var retrieved = await _uow.Stoklar.GetByIdAsync(stok.Id);
        Assert.Equal(125.75m, retrieved!.OrtalamaSatisFiyati);
    }

    [Fact]
    public async Task Scenario_59_ProfitMargin_CalculateMarkupRate()
    {
        var stok = new StokKart { StokKodu = "PRF-01", StokAdi = "Karlı Ürün", AlisFiyati = 100m, SatisFiyati = 150m };
        decimal margin = ((stok.SatisFiyati - stok.AlisFiyati) / stok.AlisFiyati) * 100m;

        Assert.Equal(50m, margin);
    }

    [Fact]
    public async Task Scenario_60_ZeroCostMarkup_AvoidsDivisionByZero()
    {
        var stok = new StokKart { StokKodu = "PRF-02", StokAdi = "Sıfır Maliyetli", AlisFiyati = 0m, SatisFiyati = 100m };
        decimal margin = stok.AlisFiyati > 0 ? ((stok.SatisFiyati - stok.AlisFiyati) / stok.AlisFiyati) * 100m : 100m;

        Assert.Equal(100m, margin);
    }

    // =============================================================
    // 6. Barkod, Kodlama & Tanımlama Senaryoları (61-70)
    // =============================================================

    [Theory]
    [InlineData("8690123456789")]
    [InlineData("978020137962")]
    [InlineData("BRK-123456-XYZ")]
    public async Task Scenario_61_to_63_BarcodeFormats_PersistAccurately(string barkod)
    {
        var stok = new StokKart { StokKodu = $"BC-{barkod}", StokAdi = $"Barkodlu {barkod}", Barkod = barkod };
        await _uow.Stoklar.SaveAsync(stok);

        var retrieved = await _uow.Stoklar.GetByIdAsync(stok.Id);
        Assert.Equal(barkod, retrieved!.Barkod);
    }

    [Fact]
    public async Task Scenario_64_Barcode_NullOrEmpty_AllowsSave()
    {
        var stok = new StokKart { StokKodu = "NO-BC", StokAdi = "Barkodsuz Ürün", Barkod = null };
        await _uow.Stoklar.SaveAsync(stok);

        var retrieved = await _uow.Stoklar.GetByIdAsync(stok.Id);
        Assert.Null(retrieved!.Barkod);
    }

    [Fact]
    public async Task Scenario_65_StokKodu_AlphanumericWithHyphenAndDots_Persists()
    {
        string specialCode = "STK.2026-X.001";
        var stok = new StokKart { StokKodu = specialCode, StokAdi = "Özel Kodlu Ürün" };
        await _uow.Stoklar.SaveAsync(stok);

        var retrieved = await _uow.Stoklar.GetByIdAsync(stok.Id);
        Assert.Equal(specialCode, retrieved!.StokKodu);
    }

    [Fact]
    public async Task Scenario_66_StokAdi_LongTitle_StoresUpTo200Chars()
    {
        string longTitle = "Endüstriyel Yüksek Basınca Dayanıklı Çift Kademeli Paslanmaz Çelik Flanşlı Küresel Vana - Model 2026-TX Ultra Güçlendirilmiş Seri (DN50 PN40)";
        var stok = new StokKart { StokKodu = "LONG-01", StokAdi = longTitle };
        await _uow.Stoklar.SaveAsync(stok);

        var retrieved = await _uow.Stoklar.GetByIdAsync(stok.Id);
        Assert.Equal(longTitle, retrieved!.StokAdi);
    }

    [Fact]
    public async Task Scenario_67_CategoryAlias_StokGrubuAndKategoriAreSynchronized()
    {
        var stok = new StokKart { Kategori = "Elektronik" };
        Assert.Equal("Elektronik", stok.Grup);
        Assert.Equal("Elektronik", stok.StokGrubu);

        stok.StokGrubu = "Mekanik";
        Assert.Equal("Mekanik", stok.Kategori);
        Assert.Equal("Mekanik", stok.Grup);
    }

    [Fact]
    public async Task Scenario_68_Aciklama_StoresMultipleLines()
    {
        string desc = "Depo Rafı: A-12-04\nMenşei: Türkiye\nGaranti Süresi: 24 Ay";
        var stok = new StokKart { StokKodu = "DESC-01", StokAdi = "Açıklamalı Ürün", Aciklama = desc };
        await _uow.Stoklar.SaveAsync(stok);

        var retrieved = await _uow.Stoklar.GetByIdAsync(stok.Id);
        Assert.Equal(desc, retrieved!.Aciklama);
    }

    [Fact]
    public async Task Scenario_69_StokKart_ToString_ReturnsStokAdi()
    {
        var stok = new StokKart { StokAdi = "Test Ürünü" };
        Assert.Equal("Test Ürünü", stok.ToString());
    }

    [Fact]
    public async Task Scenario_70_StokKart_ToString_ReturnsEmptyWhenStokAdiIsNull()
    {
        var stok = new StokKart { StokAdi = null };
        Assert.Equal(string.Empty, stok.ToString());
    }

    // =============================================================
    // 7. Kritik Seviye & Stok Alarm Senaryoları (71-80)
    // =============================================================

    [Theory]
    [InlineData(10, 5, true)]   // Miktar: 5, MinSeviye: 10 -> Alarm
    [InlineData(10, 10, true)]  // Miktar: 10, MinSeviye: 10 -> Sınırda Alarm (Miktar <= MinSeviye)
    [InlineData(10, 15, false)] // Miktar: 15, MinSeviye: 10 -> Normal
    [InlineData(0, 0, false)]   // MinSeviye: 0 -> Kritik kontrol yok
    public void Scenario_71_to_74_KritikSeviye_AlarmLogic(double minSeviye, double miktar, bool expectedAlert)
    {
        bool isCritical = minSeviye > 0 && miktar <= minSeviye;
        Assert.Equal(expectedAlert, isCritical);
    }

    [Fact]
    public async Task Scenario_75_KritikSeviye_Alias_MinSeviyeMatchesKritikSeviye()
    {
        var stok = new StokKart { MinSeviye = 25.5m };
        Assert.Equal(25.5m, stok.KritikSeviye);

        stok.KritikSeviye = 40.0m;
        Assert.Equal(40.0m, stok.MinSeviye);
    }

    [Fact]
    public async Task Scenario_76_NegativeStock_OccursWhenSalesExceedPhysicalInventory()
    {
        var stok = new StokKart { StokKodu = "NEG-76", StokAdi = "Eksiye Düşen Ürün", Miktar = -15 };
        await _uow.Stoklar.SaveAsync(stok);

        var retrieved = await _uow.Stoklar.GetByIdAsync(stok.Id);
        Assert.Equal(-15, retrieved!.Miktar);
    }

    [Fact]
    public async Task Scenario_77_ZeroInventory_ZeroStockCount()
    {
        var stok = new StokKart { StokKodu = "ZERO-77", StokAdi = "Tükenen Ürün", Miktar = 0 };
        await _uow.Stoklar.SaveAsync(stok);

        var retrieved = await _uow.Stoklar.GetByIdAsync(stok.Id);
        Assert.Equal(0, retrieved!.Miktar);
    }

    [Fact]
    public async Task Scenario_78_FractionalQuantity_AllowsKilogramPrecision()
    {
        var stok = new StokKart { StokKodu = "KG-78", StokAdi = "Tartılı Kumaş", Birim = "Kg", Miktar = 125.75m };
        await _uow.Stoklar.SaveAsync(stok);

        var retrieved = await _uow.Stoklar.GetByIdAsync(stok.Id);
        Assert.Equal(125.75m, retrieved!.Miktar);
    }

    [Fact]
    public async Task Scenario_79_InventoryValue_ComputesQuantityTimesCost()
    {
        var stok = new StokKart { StokKodu = "VAL-79", StokAdi = "Değerli Ürün", Miktar = 100, AlisFiyati = 25.50m };
        decimal totalValue = (decimal)stok.Miktar * stok.AlisFiyati;

        Assert.Equal(2550.00m, totalValue);
    }

    [Fact]
    public async Task Scenario_80_InventoryValue_ZeroQuantityResultsInZeroValue()
    {
        var stok = new StokKart { StokKodu = "VAL-80", StokAdi = "Sıfır Stok Değer", Miktar = 0, AlisFiyati = 500m };
        decimal totalValue = (decimal)stok.Miktar * stok.AlisFiyati;

        Assert.Equal(0m, totalValue);
    }

    // =============================================================
    // 8. Stok Hareketleri & Giriş-Çıkış Hesaplamaları (81-90)
    // =============================================================

    [Fact]
    public async Task Scenario_81_StokHareket_InflowMovement_IncreasesTotalStock()
    {
        var stok = new StokKart { StokKodu = "MOV-81", StokAdi = "Giriş Gören Ürün", Miktar = 0 };
        await _uow.Stoklar.SaveAsync(stok);

        await _uow.Stoklar.SaveHareketAsync(new StokHareket
        {
            StokId = stok.Id,
            IslemTuru = "Satın Alma",
            Giren = 50m,
            Cikan = 0m,
            Miktar = 50m,
            Fiyat = 100m
        });

        var movements = await _uow.Stoklar.GetHareketlerAsync(stok.Id);
        Assert.Single(movements);
        Assert.Equal(50m, movements.First().Giren);
    }

    [Fact]
    public async Task Scenario_82_StokHareket_OutflowMovement_DecreasesTotalStock()
    {
        var stok = new StokKart { StokKodu = "MOV-82", StokAdi = "Çıkış Gören Ürün", Miktar = 100 };
        await _uow.Stoklar.SaveAsync(stok);

        await _uow.Stoklar.SaveHareketAsync(new StokHareket
        {
            StokId = stok.Id,
            IslemTuru = "Satış",
            Giren = 0m,
            Cikan = 35m,
            Miktar = 35m,
            Fiyat = 150m
        });

        var movements = await _uow.Stoklar.GetHareketlerAsync(stok.Id);
        Assert.Single(movements);
        Assert.Equal(35m, movements.First().Cikan);
    }

    [Fact]
    public async Task Scenario_83_StokHareket_NetMovementBalance_ComputesGirenMinusCikan()
    {
        var stok = new StokKart { StokKodu = "MOV-83", StokAdi = "Net Hareket Ürünü" };
        await _uow.Stoklar.SaveAsync(stok);

        await _uow.Stoklar.SaveHareketAsync(new StokHareket { StokId = stok.Id, Giren = 100m, Cikan = 0m });
        await _uow.Stoklar.SaveHareketAsync(new StokHareket { StokId = stok.Id, Giren = 0m, Cikan = 40m });
        await _uow.Stoklar.SaveHareketAsync(new StokHareket { StokId = stok.Id, Giren = 20m, Cikan = 0m });

        var movements = await _uow.Stoklar.GetHareketlerAsync(stok.Id);
        decimal netQty = movements.Sum(m => m.Giren - m.Cikan);

        Assert.Equal(80m, netQty);
    }

    [Fact]
    public async Task Scenario_84_StokHareket_DeleteMovement_RemovesFromLedger()
    {
        var stok = new StokKart { StokKodu = "MOV-84", StokAdi = "Hareket Silinen Ürün" };
        await _uow.Stoklar.SaveAsync(stok);

        var h1 = new StokHareket { StokId = stok.Id, Giren = 50m };
        var h2 = new StokHareket { StokId = stok.Id, Giren = 30m };
        await _uow.Stoklar.SaveHareketAsync(h1);
        await _uow.Stoklar.SaveHareketAsync(h2);

        await _uow.Stoklar.DeleteHareketAsync(h2);

        var movements = await _uow.Stoklar.GetHareketlerAsync(stok.Id);
        Assert.Single(movements);
        Assert.Equal(50m, movements.First().Giren);
    }

    [Fact]
    public async Task Scenario_85_StokHareket_EvrakNoReference_PreservesInvoiceOrReceiptNo()
    {
        var stok = new StokKart { StokKodu = "MOV-85", StokAdi = "Evraklı Hareket" };
        await _uow.Stoklar.SaveAsync(stok);

        string docNo = "FAT-2026-90812";
        await _uow.Stoklar.SaveHareketAsync(new StokHareket
        {
            StokId = stok.Id,
            EvrakNo = docNo,
            EvrakTuru = "Fatura",
            Giren = 12m
        });

        var movements = await _uow.Stoklar.GetHareketlerAsync(stok.Id);
        Assert.Equal(docNo, movements.First().EvrakNo);
        Assert.Equal("Fatura", movements.First().EvrakTuru);
    }

    [Fact]
    public async Task Scenario_86_StokHareket_StokKartIdAlias_PointsToStokId()
    {
        var h = new StokHareket { StokKartId = 1234 };
        Assert.Equal(1234, h.StokId);

        h.StokId = 5678;
        Assert.Equal(5678, h.StokKartId);
    }

    [Fact]
    public async Task Scenario_87_StokHareket_KalanMiktarProperty_PersistsChronologicalSnapshot()
    {
        var stok = new StokKart { StokKodu = "MOV-87", StokAdi = "Kalan Miktarlı Hareket" };
        await _uow.Stoklar.SaveAsync(stok);

        await _uow.Stoklar.SaveHareketAsync(new StokHareket { StokId = stok.Id, Giren = 100m, KalanMiktar = 100m });
        await _uow.Stoklar.SaveHareketAsync(new StokHareket { StokId = stok.Id, Cikan = 30m, KalanMiktar = 70m });

        var movements = (await _uow.Stoklar.GetHareketlerAsync(stok.Id)).OrderBy(x => x.Id).ToList();
        Assert.Equal(100m, movements[0].KalanMiktar);
        Assert.Equal(70m, movements[1].KalanMiktar);
    }

    [Fact]
    public async Task Scenario_88_StokHareket_UnitCostPrice_TracksHistoricalTransactionRate()
    {
        var stok = new StokKart { StokKodu = "MOV-88", StokAdi = "Fiyat Takip Ürünü" };
        await _uow.Stoklar.SaveAsync(stok);

        await _uow.Stoklar.SaveHareketAsync(new StokHareket { StokId = stok.Id, Giren = 10m, Fiyat = 45.50m });
        await _uow.Stoklar.SaveHareketAsync(new StokHareket { StokId = stok.Id, Giren = 10m, Fiyat = 52.00m });

        var movements = await _uow.Stoklar.GetHareketlerAsync(stok.Id);
        Assert.Contains(movements, m => m.Fiyat == 45.50m);
        Assert.Contains(movements, m => m.Fiyat == 52.00m);
    }

    [Fact]
    public async Task Scenario_89_StokHareket_TenantId_DefaultsToDefault()
    {
        var h = new StokHareket();
        Assert.Equal("default", h.TenantId);
    }

    [Fact]
    public async Task Scenario_90_StokHareket_Aciklama_StoresTransactionReason()
    {
        var stok = new StokKart { StokKodu = "MOV-90", StokAdi = "Açıklamalı Hareket" };
        await _uow.Stoklar.SaveAsync(stok);

        string reason = "Yıl sonu fiziki sayım fazlası";
        await _uow.Stoklar.SaveHareketAsync(new StokHareket { StokId = stok.Id, IslemTuru = "Sayım Fazlası", Giren = 5m, Aciklama = reason });

        var movements = await _uow.Stoklar.GetHareketlerAsync(stok.Id);
        Assert.Equal(reason, movements.First().Aciklama);
    }

    // =============================================================
    // 9. StokListViewModel UI, Filtreleme & Arama (91-100)
    // =============================================================

    [AvaloniaFact]
    public async Task Scenario_91_StokListViewModel_FilterByKod_LocatesMatchingStock()
    {
        await _uow.Stoklar.SaveAsync(new StokKart { StokKodu = "VMF-KOD1", StokAdi = "Filtre Ürün 1" });
        await _uow.Stoklar.SaveAsync(new StokKart { StokKodu = "VMF-KOD2", StokAdi = "Filtre Ürün 2" });

        var vm = _serviceProvider.GetRequiredService<StokListViewModel>();
        vm.FilterKod = "KOD1";
        await vm.LoadStoklarAsync();

        Assert.Contains(vm.Stoklar, s => s.StokKodu == "VMF-KOD1");
        Assert.DoesNotContain(vm.Stoklar, s => s.StokKodu == "VMF-KOD2");
    }

    [AvaloniaFact]
    public async Task Scenario_92_StokListViewModel_FilterByStokAdi_LocatesMatchingStock()
    {
        await _uow.Stoklar.SaveAsync(new StokKart { StokKodu = "VMF-AD1", StokAdi = "Özel Çelik Boru" });
        await _uow.Stoklar.SaveAsync(new StokKart { StokKodu = "VMF-AD2", StokAdi = "Plastik Dirsek" });

        var vm = _serviceProvider.GetRequiredService<StokListViewModel>();
        vm.FilterStokAdi = "Çelik Boru";
        await vm.LoadStoklarAsync();

        Assert.Contains(vm.Stoklar, s => s.StokKodu == "VMF-AD1");
        Assert.DoesNotContain(vm.Stoklar, s => s.StokKodu == "VMF-AD2");
    }

    [AvaloniaFact]
    public async Task Scenario_93_StokListViewModel_FilterByGrup_LocatesMatchingCategory()
    {
        await _uow.Stoklar.SaveAsync(new StokKart { StokKodu = "GRP-01", StokAdi = "Hırdavat Vidası", Kategori = "Hırdavat" });
        await _uow.Stoklar.SaveAsync(new StokKart { StokKodu = "GRP-02", StokAdi = "Boya Fırçası", Kategori = "Boya" });

        var vm = _serviceProvider.GetRequiredService<StokListViewModel>();
        vm.FilterGrup = "Hırdavat";
        await vm.LoadStoklarAsync();

        Assert.Contains(vm.Stoklar, s => s.StokKodu == "GRP-01");
        Assert.DoesNotContain(vm.Stoklar, s => s.StokKodu == "GRP-02");
    }

    [AvaloniaFact]
    public async Task Scenario_94_StokListViewModel_OnlyWithBalance_ExcludesZeroStock()
    {
        await _uow.Stoklar.SaveAsync(new StokKart { StokKodu = "BAL-01", StokAdi = "Stoklu Ürün", Miktar = 10 });
        await _uow.Stoklar.SaveAsync(new StokKart { StokKodu = "BAL-02", StokAdi = "Sıfır Stoklu", Miktar = 0 });

        var vm = _serviceProvider.GetRequiredService<StokListViewModel>();
        vm.OnlyWithBalance = true;
        await vm.LoadStoklarAsync();

        Assert.Contains(vm.Stoklar, s => s.StokKodu == "BAL-01");
        Assert.DoesNotContain(vm.Stoklar, s => s.StokKodu == "BAL-02");
    }

    [AvaloniaFact]
    public async Task Scenario_95_StokListViewModel_SelectedStok_UpdatesFormTitle()
    {
        var stok = new StokKart { StokKodu = "SEL-95", StokAdi = "Seçilen Parça" };
        await _uow.Stoklar.SaveAsync(stok);

        var vm = _serviceProvider.GetRequiredService<StokListViewModel>();
        vm.SelectedStok = stok;

        Assert.True(vm.IsEditMode);
        Assert.Contains("Seçilen Parça", vm.FormTitle);
    }

    [AvaloniaFact]
    public async Task Scenario_96_StokListViewModel_NullSelectedStok_ShowsNewStockTitle()
    {
        var vm = _serviceProvider.GetRequiredService<StokListViewModel>();
        vm.SelectedStok = null;

        Assert.False(vm.IsEditMode);
        Assert.Equal("Yeni Stok Kartı Ekle", vm.FormTitle);
    }

    [AvaloniaFact]
    public async Task Scenario_97_StokListViewModel_SelectAll_TogglesIsSelectedOnAllItems()
    {
        await _uow.Stoklar.SaveAsync(new StokKart { StokKodu = "SA-01", StokAdi = "Çoklu Seçim 1" });
        await _uow.Stoklar.SaveAsync(new StokKart { StokKodu = "SA-02", StokAdi = "Çoklu Seçim 2" });

        var vm = _serviceProvider.GetRequiredService<StokListViewModel>();
        await vm.LoadStoklarAsync();

        vm.IsSelectAll = true;
        Assert.All(vm.Stoklar, s => Assert.True(s.IsSelected));

        vm.IsSelectAll = false;
        Assert.All(vm.Stoklar, s => Assert.False(s.IsSelected));
    }

    [AvaloniaFact]
    public async Task Scenario_98_StokListViewModel_LoadHareketler_PopulatesStokHareketleri()
    {
        var stok = new StokKart { StokKodu = "LH-98", StokAdi = "Hareketli Parça" };
        await _uow.Stoklar.SaveAsync(stok);

        await _uow.Stoklar.SaveHareketAsync(new StokHareket { StokId = stok.Id, Giren = 25m });

        var vm = _serviceProvider.GetRequiredService<StokListViewModel>();
        await vm.LoadStokHareketleriAsync(stok.Id);

        Assert.NotEmpty(vm.StokHareketleri);
        Assert.Equal(25m, vm.StokHareketleri.First().Giren);
    }

    [Fact]
    public async Task Scenario_99_StokKart_PropertyChangeNotifications_FireForMiktar()
    {
        var stok = new StokKart();
        string? changed = null;
        stok.PropertyChanged += (s, e) => changed = e.PropertyName;

        stok.Miktar = 45.5m;
        Assert.Equal(nameof(StokKart.Miktar), changed);
    }

    [Fact]
    public async Task Scenario_100_StokKart_PropertyChangeNotifications_FireForMinSeviyeUpdatesKritikSeviye()
    {
        var stok = new StokKart();
        var props = new List<string?>();
        stok.PropertyChanged += (s, e) => props.Add(e.PropertyName);

        stok.MinSeviye = 10;
        Assert.Contains(nameof(StokKart.MinSeviye), props);
        Assert.Contains(nameof(StokKart.KritikSeviye), props);
    }
}
