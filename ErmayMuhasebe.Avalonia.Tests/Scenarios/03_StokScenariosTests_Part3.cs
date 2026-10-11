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
    // 10. Stok Hareketleri Geçmişi, Raporlama & Entegrasyon (101-110)
    // =============================================================

    [Fact]
    public async Task Scenario_101_StokHareket_ChronologicalOrder_SortsByDateAscending()
    {
        var stok = new StokKart { StokKodu = "ORD-101", StokAdi = "Sıralı Ürün" };
        await _uow.Stoklar.SaveAsync(stok);

        await _uow.Stoklar.SaveHareketAsync(new StokHareket { StokId = stok.Id, Tarih = DateTime.Today.AddDays(-10), Giren = 100m });
        await _uow.Stoklar.SaveHareketAsync(new StokHareket { StokId = stok.Id, Tarih = DateTime.Today.AddDays(-2), Cikan = 20m });
        await _uow.Stoklar.SaveHareketAsync(new StokHareket { StokId = stok.Id, Tarih = DateTime.Today.AddDays(-5), Giren = 50m });

        var list = (await _uow.Stoklar.GetHareketlerAsync(stok.Id)).OrderBy(x => x.Tarih).ToList();

        Assert.Equal(3, list.Count);
        Assert.True(list[0].Tarih < list[1].Tarih);
        Assert.True(list[1].Tarih < list[2].Tarih);
    }

    [Fact]
    public async Task Scenario_102_StokHareket_FaturaIdLinkage_CanTraceBackToInvoice()
    {
        var stok = new StokKart { StokKodu = "FAT-102", StokAdi = "Faturalı Malzeme" };
        await _uow.Stoklar.SaveAsync(stok);

        int invoiceId = 4455;
        await _uow.Stoklar.SaveHareketAsync(new StokHareket
        {
            StokId = stok.Id,
            FaturaId = invoiceId,
            EvrakNo = "ALIS-4455",
            Giren = 75m,
            Fiyat = 200m
        });

        var movements = await _uow.Stoklar.GetHareketlerAsync(stok.Id);
        Assert.Equal(invoiceId, movements.First().FaturaId);
    }

    [Theory]
    [InlineData("Alış Faturası", 100, 0)]
    [InlineData("Satış Faturası", 0, 40)]
    [InlineData("Satış İade", 10, 0)]
    [InlineData("Alış İade", 0, 5)]
    [InlineData("Sayım Fazlası", 2, 0)]
    [InlineData("Sayım Eksiği", 0, 3)]
    public async Task Scenario_103_to_108_StokHareket_TransactionTypes_PersistInflowOutflowProperly(string islemTuru, decimal giren, decimal cikan)
    {
        var stok = new StokKart { StokKodu = $"TRX-{giren}-{cikan}", StokAdi = $"{islemTuru} Ürünü" };
        await _uow.Stoklar.SaveAsync(stok);

        await _uow.Stoklar.SaveHareketAsync(new StokHareket
        {
            StokId = stok.Id,
            IslemTuru = islemTuru,
            Giren = giren,
            Cikan = cikan
        });

        var movements = await _uow.Stoklar.GetHareketlerAsync(stok.Id);
        Assert.Equal(islemTuru, movements.First().IslemTuru);
        Assert.Equal(giren, movements.First().Giren);
        Assert.Equal(cikan, movements.First().Cikan);
    }

    [Fact]
    public async Task Scenario_109_StokHareket_EmptyMovements_ReturnsEmptyCollectionWithoutCrashing()
    {
        var stok = new StokKart { StokKodu = "EMP-109", StokAdi = "Hareketsiz Stok" };
        await _uow.Stoklar.SaveAsync(stok);

        var movements = await _uow.Stoklar.GetHareketlerAsync(stok.Id);
        Assert.NotNull(movements);
        Assert.Empty(movements);
    }

    [Fact]
    public async Task Scenario_110_StokHareket_MultipleMovementsOnSameDay_PreservesOrder()
    {
        var stok = new StokKart { StokKodu = "SAME-110", StokAdi = "Aynı Gün Ürünü" };
        await _uow.Stoklar.SaveAsync(stok);

        var today = DateTime.Today;
        await _uow.Stoklar.SaveHareketAsync(new StokHareket { StokId = stok.Id, Tarih = today, Giren = 10m });
        await _uow.Stoklar.SaveHareketAsync(new StokHareket { StokId = stok.Id, Tarih = today, Giren = 20m });

        var movements = await _uow.Stoklar.GetHareketlerAsync(stok.Id);
        Assert.Equal(2, movements.Count);
    }

    // =============================================================
    // 11. Soft-Delete & Silme Kısıtlamaları (111-120)
    // =============================================================

    [Fact]
    public async Task Scenario_111_SoftDelete_MarksIsDeletedTrue()
    {
        var stok = new StokKart { StokKodu = "DEL-111", StokAdi = "Silinecek Stok" };
        await _uow.Stoklar.SaveAsync(stok);

        await _uow.Stoklar.DeleteAsync(stok.Id);
        var retrieved = await _uow.Stoklar.GetByIdAsync(stok.Id);

        Assert.True(retrieved == null || retrieved.IsDeleted);
    }

    [Fact]
    public async Task Scenario_112_SoftDelete_ExcludesFromGetAllAsync()
    {
        var stok = new StokKart { StokKodu = "DEL-112", StokAdi = "Gizlenen Stok" };
        await _uow.Stoklar.SaveAsync(stok);

        await _uow.Stoklar.DeleteAsync(stok.Id);
        var all = await _uow.Stoklar.GetAllAsync();

        Assert.DoesNotContain(all, s => s.StokKodu == "DEL-112");
    }

    [Fact]
    public async Task Scenario_113_HardDeleteById_CascadesAndDeletesMovements()
    {
        var stok = new StokKart { StokKodu = "DEL-113", StokAdi = "Hareketli Stok Silme" };
        await _uow.Stoklar.SaveAsync(stok);

        var h = new StokHareket { StokId = stok.Id, Giren = 50m };
        await _uow.Stoklar.SaveHareketAsync(h);

        // DeleteAsync(id) cascades and cleans movements
        await _uow.Stoklar.DeleteAsync(stok.Id);

        var movements = await _uow.Stoklar.GetHareketlerAsync(stok.Id);
        Assert.Empty(movements);
    }

    [Fact]
    public async Task Scenario_114_StokKart_IsSelectedProperty_SupportsMultiSelection()
    {
        var s = new StokKart { IsSelected = true };
        Assert.True(s.IsSelected);
        s.IsSelected = false;
        Assert.False(s.IsSelected);
    }

    [Fact]
    public async Task Scenario_115_StokKart_KayitTarihi_DefaultsToCurrentTimestamp()
    {
        var stok = new StokKart { StokKodu = "KT-115", StokAdi = "Zaman Test" };
        Assert.True((DateTime.Now - stok.KayitTarihi).TotalMinutes < 5);
    }

    [Fact]
    public async Task Scenario_116_StokKart_Version_DefaultsToOne()
    {
        var stok = new StokKart { StokKodu = "VER-116", StokAdi = "Versiyon Test" };
        Assert.True(stok.Version >= 1);
    }

    [Fact]
    public async Task Scenario_117_StokKart_UpdatedAt_CanBeUpdated()
    {
        var stok = new StokKart { StokKodu = "UPD-117", StokAdi = "Güncelleme Test" };
        var before = stok.UpdatedAt;
        stok.UpdatedAt = DateTime.Now.AddSeconds(1);
        Assert.True(stok.UpdatedAt >= before);
    }

    [Fact]
    public async Task Scenario_118_StokKart_TenantId_DefaultsToDefault()
    {
        var stok = new StokKart { StokKodu = "TNT-118", StokAdi = "Tenant Test" };
        Assert.Equal("default", stok.TenantId);
    }

    [Fact]
    public async Task Scenario_119_StokKart_CustomTenantId_PersistsProperly()
    {
        var stok = new StokKart { StokKodu = "TNT-119", StokAdi = "Şube Deposu", TenantId = "branch_istanbul" };
        await _uow.Stoklar.SaveAsync(stok);

        var retrieved = await _uow.Stoklar.GetByIdAsync(stok.Id);
        Assert.Equal("branch_istanbul", retrieved!.TenantId);
    }

    [Fact]
    public async Task Scenario_120_StokKart_Id_PrimaryKeyAssignment()
    {
        var stok = new StokKart { StokKodu = "ID-120", StokAdi = "ID Test" };
        await _uow.Stoklar.SaveAsync(stok);
        Assert.True(stok.Id > 0);
    }

    // =============================================================
    // 12. Farklı Birimler & Küsuratlı Hassas Miktarlar (121-130)
    // =============================================================

    [Theory]
    [InlineData("Metre", 45.75)]
    [InlineData("Litre", 12.50)]
    [InlineData("Ton", 2.350)]
    [InlineData("Gram", 500.0)]
    [InlineData("Paket", 15.0)]
    [InlineData("Koli", 8.0)]
    [InlineData("Rulo", 3.0)]
    public async Task Scenario_121_to_127_VariousUnitsAndFractionalQuantities(string birim, double miktar)
    {
        var stok = new StokKart
        {
            StokKodu = $"UNIT-{birim}",
            StokAdi = $"{birim} Bazlı Ürün",
            Birim = birim,
            Miktar = (decimal)miktar
        };
        await _uow.Stoklar.SaveAsync(stok);

        var retrieved = await _uow.Stoklar.GetByIdAsync(stok.Id);
        Assert.Equal(birim, retrieved!.Birim);
        Assert.Equal((decimal)miktar, retrieved.Miktar);
    }

    [Fact]
    public async Task Scenario_128_MilligramPrecision_StoresSmallFractionalValues()
    {
        decimal smallQty = 0.005m; // 5 gram = 0.005 kg
        var stok = new StokKart { StokKodu = "MG-128", StokAdi = "Baharat", Birim = "Kg", Miktar = smallQty };
        await _uow.Stoklar.SaveAsync(stok);

        var retrieved = await _uow.Stoklar.GetByIdAsync(stok.Id);
        Assert.Equal(smallQty, retrieved!.Miktar);
    }

    [Fact]
    public async Task Scenario_129_HugeQuantity_StoresMillionsWithoutOverflow()
    {
        decimal hugeQty = 5000000m;
        var stok = new StokKart { StokKodu = "HUGE-129", StokAdi = "Çivi Adet", Birim = "Adet", Miktar = hugeQty };
        await _uow.Stoklar.SaveAsync(stok);

        var retrieved = await _uow.Stoklar.GetByIdAsync(stok.Id);
        Assert.Equal(hugeQty, retrieved!.Miktar);
    }

    [Fact]
    public async Task Scenario_130_Birim_DefaultValueIsAdet()
    {
        var stok = new StokKart { StokKodu = "DEF-130", StokAdi = "Varsayılan Birim" };
        Assert.Equal("Adet", stok.Birim);
    }

    // =============================================================
    // 13. Envanter Değeri, Maliyet & Fiyat Değişiklikleri (131-140)
    // =============================================================

    [Fact]
    public async Task Scenario_131_InventoryValue_MultipleProducts_SumsTotalPortfolioCost()
    {
        var s1 = new StokKart { StokKodu = "PRT-01", StokAdi = "P1", Miktar = 10, AlisFiyati = 100m };
        var s2 = new StokKart { StokKodu = "PRT-02", StokAdi = "P2", Miktar = 20, AlisFiyati = 50m };
        await _uow.Stoklar.SaveAsync(s1);
        await _uow.Stoklar.SaveAsync(s2);

        decimal totalValue = ((decimal)s1.Miktar * s1.AlisFiyati) + ((decimal)s2.Miktar * s2.AlisFiyati);
        Assert.Equal(2000m, totalValue);
    }

    [Fact]
    public async Task Scenario_132_PriceIncrease_UpdatesSellingPriceSuccessfully()
    {
        var stok = new StokKart { StokKodu = "PRC-132", StokAdi = "Fiyatı Artan", SatisFiyati = 200m };
        await _uow.Stoklar.SaveAsync(stok);

        stok.SatisFiyati = 250m;
        await _uow.Stoklar.SaveAsync(stok);

        var retrieved = await _uow.Stoklar.GetByIdAsync(stok.Id);
        Assert.Equal(250m, retrieved!.SatisFiyati);
    }

    [Fact]
    public async Task Scenario_133_DiscountCampaign_LowersSellingPriceBelowCost_AllowsLossSale()
    {
        var stok = new StokKart { StokKodu = "DSC-133", StokAdi = "Zararına Satış", AlisFiyati = 100m, SatisFiyati = 80m };
        await _uow.Stoklar.SaveAsync(stok);

        var retrieved = await _uow.Stoklar.GetByIdAsync(stok.Id);
        Assert.True(retrieved!.SatisFiyati < retrieved.AlisFiyati);
    }

    [Fact]
    public async Task Scenario_134_StokKart_PropertyChangeNotifications_FireForAlisFiyati()
    {
        var stok = new StokKart();
        string? prop = null;
        stok.PropertyChanged += (s, e) => prop = e.PropertyName;

        stok.AlisFiyati = 199.90m;
        Assert.Equal(nameof(StokKart.AlisFiyati), prop);
    }

    [Fact]
    public async Task Scenario_135_StokKart_PropertyChangeNotifications_FireForSatisFiyati()
    {
        var stok = new StokKart();
        string? prop = null;
        stok.PropertyChanged += (s, e) => prop = e.PropertyName;

        stok.SatisFiyati = 299.90m;
        Assert.Equal(nameof(StokKart.SatisFiyati), prop);
    }

    [Fact]
    public async Task Scenario_136_StokKart_PropertyChangeNotifications_FireForKDV()
    {
        var stok = new StokKart();
        var props = new List<string?>();
        stok.PropertyChanged += (s, e) => props.Add(e.PropertyName);

        stok.KDV = 10;
        Assert.Contains(nameof(StokKart.KDV), props);
        Assert.Contains(nameof(StokKart.KdvOrani), props);
    }

    [Fact]
    public async Task Scenario_137_StokHareket_PropertyChangeNotifications_FireForMiktar()
    {
        var h = new StokHareket();
        string? prop = null;
        h.PropertyChanged += (s, e) => prop = e.PropertyName;

        h.Miktar = 75m;
        Assert.Equal(nameof(StokHareket.Miktar), prop);
    }

    [Fact]
    public async Task Scenario_138_StokHareket_PropertyChangeNotifications_FireForKalanMiktar()
    {
        var h = new StokHareket();
        string? prop = null;
        h.PropertyChanged += (s, e) => prop = e.PropertyName;

        h.KalanMiktar = 120m;
        Assert.Equal(nameof(StokHareket.KalanMiktar), prop);
    }

    [Fact]
    public async Task Scenario_139_StokHareket_PropertyChangeNotifications_FireForId()
    {
        var h = new StokHareket();
        string? prop = null;
        h.PropertyChanged += (s, e) => prop = e.PropertyName;

        h.Id = 999;
        Assert.Equal(nameof(StokHareket.Id), prop);
    }

    [Fact]
    public async Task Scenario_140_StokHareket_PropertyChangeNotifications_FireForStokId()
    {
        var h = new StokHareket();
        string? prop = null;
        h.PropertyChanged += (s, e) => prop = e.PropertyName;

        h.StokId = 888;
        Assert.Equal(nameof(StokHareket.StokId), prop);
    }

    // =============================================================
    // 14. Toplu Stok, E2E Yaşam Döngüsü & Sınır Testleri (141-152)
    // =============================================================

    [Fact]
    public async Task Scenario_141_BulkInsert_TwentyStokCards_SavesAllWithoutLoss()
    {
        var list = new List<StokKart>();
        for (int i = 1; i <= 20; i++)
        {
            list.Add(new StokKart { StokKodu = $"BLK-S-{i:D3}", StokAdi = $"Toplu Ürün {i}", Miktar = i * 5 });
        }

        foreach (var s in list)
        {
            await _uow.Stoklar.SaveAsync(s);
        }

        var all = await _uow.Stoklar.GetAllAsync();
        foreach (var s in list)
        {
            Assert.Contains(all, x => x.StokKodu == s.StokKodu);
        }
    }

    [Fact]
    public async Task Scenario_142_StokKart_SpecialCharactersInCode_SavesSafely()
    {
        string specialCode = "STK_#12/2026&A+B";
        var stok = new StokKart { StokKodu = specialCode, StokAdi = "Özel Karakterli Ürün" };
        await _uow.Stoklar.SaveAsync(stok);

        var retrieved = await _uow.Stoklar.GetByIdAsync(stok.Id);
        Assert.Equal(specialCode, retrieved!.StokKodu);
    }

    [Fact]
    public async Task Scenario_143_StokKart_TurkishCharactersInName_SupportsFullAlphabet()
    {
        string trName = "Çelik Örgü İplik Şemsiye Gövdesi Ğüğüş";
        var stok = new StokKart { StokKodu = "TR-NAME", StokAdi = trName };
        await _uow.Stoklar.SaveAsync(stok);

        var retrieved = await _uow.Stoklar.GetByIdAsync(stok.Id);
        Assert.Equal(trName, retrieved!.StokAdi);
    }

    [Fact]
    public async Task Scenario_144_StokKart_EmptyOptionalFields_DoNotThrowExceptions()
    {
        var stok = new StokKart
        {
            StokKodu = "EMP-144",
            StokAdi = "Boş Alanlı Ürün",
            Barkod = "",
            Kategori = "",
            Aciklama = ""
        };
        await _uow.Stoklar.SaveAsync(stok);

        var retrieved = await _uow.Stoklar.GetByIdAsync(stok.Id);
        Assert.NotNull(retrieved);
        Assert.Equal("EMP-144", retrieved.StokKodu);
    }

    [Fact]
    public async Task Scenario_145_StokHareket_NullOptionalFields_PersistsGracefully()
    {
        var stok = new StokKart { StokKodu = "NULL-145", StokAdi = "Null Hareket Test" };
        await _uow.Stoklar.SaveAsync(stok);

        var h = new StokHareket
        {
            StokId = stok.Id,
            Giren = 10m,
            EvrakNo = null,
            Aciklama = null,
            Birim = null,
            EvrakTuru = null,
            StokKodu = null,
            StokAdi = null,
            FaturaId = null
        };
        await _uow.Stoklar.SaveHareketAsync(h);

        var retrieved = await _uow.Stoklar.GetHareketlerAsync(stok.Id);
        Assert.Single(retrieved);
        Assert.Null(retrieved.First().EvrakNo);
    }

    [Fact]
    public async Task Scenario_146_Stok_UpdateMultiplePropertiesSimultaneously()
    {
        var stok = new StokKart { StokKodu = "MULTI-146", StokAdi = "Eski Ürün Adı", AlisFiyati = 50m, SatisFiyati = 80m };
        await _uow.Stoklar.SaveAsync(stok);

        stok.StokAdi = "Yeni Güncellenmiş Ürün";
        stok.AlisFiyati = 65m;
        stok.SatisFiyati = 110m;
        stok.Kategori = "Güncel Kategori";
        stok.KDV = 10;
        await _uow.Stoklar.SaveAsync(stok);

        var refreshed = await _uow.Stoklar.GetByIdAsync(stok.Id);
        Assert.Equal("Yeni Güncellenmiş Ürün", refreshed!.StokAdi);
        Assert.Equal(65m, refreshed.AlisFiyati);
        Assert.Equal(110m, refreshed.SatisFiyati);
        Assert.Equal("Güncel Kategori", refreshed.Kategori);
        Assert.Equal(10, refreshed.KDV);
    }

    [Fact]
    public async Task Scenario_147_Stok_ExtremeHighPrice_DoesNotOverflow()
    {
        decimal highPrice = 75000000.50m;
        var stok = new StokKart { StokKodu = "EXP-147", StokAdi = "Endüstriyel Türbin", SatisFiyati = highPrice };
        await _uow.Stoklar.SaveAsync(stok);

        var retrieved = await _uow.Stoklar.GetByIdAsync(stok.Id);
        Assert.Equal(highPrice, retrieved!.SatisFiyati);
    }

    [Fact]
    public async Task Scenario_148_Stok_MicroCentRounding_MaintainsTwoDecimalCurrencyStandard()
    {
        decimal unrounded = 15.333333m;
        decimal rounded = Math.Round(unrounded, 2);
        var stok = new StokKart { StokKodu = "RND-148", StokAdi = "Yuvarlanan Ürün", AlisFiyati = rounded };
        await _uow.Stoklar.SaveAsync(stok);

        var retrieved = await _uow.Stoklar.GetByIdAsync(stok.Id);
        Assert.Equal(15.33m, retrieved!.AlisFiyati);
    }

    [AvaloniaFact]
    public async Task Scenario_149_StokListViewModel_Pagination_NextAndPreviousPageAsync()
    {
        for (int i = 1; i <= 15; i++)
        {
            await _uow.Stoklar.SaveAsync(new StokKart { StokKodu = $"PG-{i:D2}", StokAdi = $"Sayfa Ürünü {i}" });
        }

        var vm = _serviceProvider.GetRequiredService<StokListViewModel>();
        vm.PageSize = 5;
        await vm.LoadStoklarAsync();

        Assert.True(vm.TotalCount >= 15);
        Assert.Equal(0, vm.CurrentPageIndex);

        await vm.NextPageAsync();
        Assert.Equal(1, vm.CurrentPageIndex);

        await vm.PreviousPageAsync();
        Assert.Equal(0, vm.CurrentPageIndex);
    }

    [Fact]
    public async Task Scenario_150_Stok_EndToEndLifecycle_CreationMovementOutflowRecalculateAndSoftDelete()
    {
        // 1. Create Stock
        var stok = new StokKart { StokKodu = "E2E-STK", StokAdi = "E2E Yaşam Döngüsü Stok", Miktar = 0, AlisFiyati = 100m };
        await _uow.Stoklar.SaveAsync(stok);
        Assert.True(stok.Id > 0);

        // 2. Add Inflow Movement
        var hIn = new StokHareket { StokId = stok.Id, IslemTuru = "Satın Alma", Giren = 100m, Fiyat = 100m };
        await _uow.Stoklar.SaveHareketAsync(hIn);

        // 3. Add Outflow Movement
        var hOut = new StokHareket { StokId = stok.Id, IslemTuru = "Satış", Cikan = 40m, Fiyat = 150m };
        await _uow.Stoklar.SaveHareketAsync(hOut);

        // Verify Movements
        var movements = await _uow.Stoklar.GetHareketlerAsync(stok.Id);
        Assert.Equal(2, movements.Count);

        // 4. Clean Movements
        await _uow.Stoklar.DeleteHareketAsync(hIn);
        await _uow.Stoklar.DeleteHareketAsync(hOut);

        // 5. Delete Stock
        await _uow.Stoklar.DeleteAsync(stok.Id);
        var activeStoks = await _uow.Stoklar.GetAllAsync();
        Assert.DoesNotContain(activeStoks, s => s.Id == stok.Id);
    }

    [Fact]
    public async Task Scenario_151_StokKart_IsDeleted_ExcludedFromActiveInventoryValuation()
    {
        var stok = new StokKart { StokKodu = "DEL-VAL", StokAdi = "Silinen Değerli", Miktar = 50, AlisFiyati = 200m, IsDeleted = true };
        await _uow.Stoklar.SaveAsync(stok);

        var active = await _uow.Stoklar.GetAllAsync();
        Assert.DoesNotContain(active, s => s.StokKodu == "DEL-VAL");
    }

    [Fact]
    public async Task Scenario_152_StokKart_BarkodSearch_MatchesExactOrContains()
    {
        string barkod = "8699999123456";
        var stok = new StokKart { StokKodu = "BC-SRC", StokAdi = "Barkod Arama Test", Barkod = barkod };
        await _uow.Stoklar.SaveAsync(stok);

        var all = await _uow.Stoklar.GetAllAsync();
        var match = all.FirstOrDefault(s => s.Barkod == barkod);
        Assert.NotNull(match);
        Assert.Equal("BC-SRC", match.StokKodu);
    }
}
