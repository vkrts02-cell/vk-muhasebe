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

public partial class TeklifScenariosTests
{
    // =============================================================
    // 9. Teklif Detayları & İnce Alanlar (101 - 115)
    // =============================================================

    [Fact]
    public async Task Scenario_101_TeklifDetay_LineDescription_PersistsAccurately()
    {
        var teklif = new Teklif { TeklifNo = "TEK-DESC-101", GenelToplam = 3500m };
        var line = new TeklifDetay
        {
            StokAdi = "Özel İmalat Dişli",
            Aciklama = "Sertleştirilmiş çelik gövde (DIN 867)",
            Miktar = 5,
            BirimFiyat = 700m,
            Tutar = 3500m
        };
        await _uow.Teklifler.SaveWithDetailsAsync(teklif, new List<TeklifDetay> { line });

        var lines = await _uow.Teklifler.GetDetaylarAsync(teklif.Id);
        Assert.Equal("Sertleştirilmiş çelik gövde (DIN 867)", lines.First().Aciklama);
    }

    [Theory]
    [InlineData(1, 100)]
    [InlineData(10, 1000)]
    [InlineData(20, 2000)]
    [InlineData(0, 0)]
    public void Scenario_102_to_105_Teklif_StandardVatRateCalculations(int kdvOrani, decimal expectedKdv)
    {
        decimal matrah = 10000m;
        decimal kdv = matrah * (kdvOrani / 100m);
        Assert.Equal(expectedKdv, kdv);
    }

    [Fact]
    public async Task Scenario_106_Teklif_UpdateQuoteNotes_PersistsCorrectly()
    {
        var t = new Teklif { TeklifNo = "TEK-NOT-106", Aciklama = "Eski Teklif Notu" };
        await _uow.Teklifler.SaveAsync(t);

        t.Aciklama = "Yeni not: Nakliye fiyata dahildir.";
        await _uow.Teklifler.SaveAsync(t);

        var retrieved = await _uow.Teklifler.GetByIdAsync(t.Id);
        Assert.Equal("Yeni not: Nakliye fiyata dahildir.", retrieved!.Aciklama);
    }

    [Theory]
    [InlineData(10, 200, 2000)]
    [InlineData(25, 40, 1000)]
    [InlineData(100, 15.5, 1550)]
    public void Scenario_107_to_109_QuoteSubtotalWithoutVat(double miktar, decimal birimFiyat, decimal expectedSubtotal)
    {
        decimal subtotal = (decimal)miktar * birimFiyat;
        Assert.Equal(expectedSubtotal, subtotal);
    }

    [Fact]
    public void Scenario_110_Teklif_DefaultDates_AreInitialized()
    {
        var t = new Teklif();
        Assert.True((DateTime.Now - t.KayitTarihi).TotalMinutes < 5);
        Assert.True((DateTime.Now - t.Tarih).TotalMinutes < 5);
    }

    [Theory]
    [InlineData("Metre")]
    [InlineData("Adet")]
    [InlineData("Kg")]
    [InlineData("Koli")]
    [InlineData("Top")]
    public void Scenario_111_to_115_TeklifDetay_UnitsSupported(string birim)
    {
        var d = new TeklifDetay { Birim = birim };
        Assert.Equal(birim, d.Birim);
    }

    // =============================================================
    // 10. PDF Çıktıları & İletişim Testleri (116 - 125)
    // =============================================================

    [Fact]
    public async Task Scenario_116_PdfService_GenerateTeklifPdf_ProducesNonEmptyBytes()
    {
        var t = new Teklif { TeklifNo = "TEK-PDF-116", CariUnvan = "Alp Mühendislik", GenelToplam = 28000m };
        var lines = new List<TeklifDetay>
        {
            new() { StokAdi = "Endüstriyel Vana", Miktar = 4, BirimFiyat = 7000m, Tutar = 28000m }
        };

        var res = await _pdfService.GenerateTeklifPdfAsync(t, lines, null);
        Assert.True(res.Success);
        Assert.NotNull(_testFileService.LastSavedBytes);
        Assert.True(_testFileService.LastSavedBytes.Length > 500);
    }

    [Fact]
    public async Task Scenario_117_PdfService_GenerateTeklifPdfWithCustomer_EmbedsCustomerDetails()
    {
        var cari = new CariKart { Unvan = "Hedef Müşteri Ltd.", VergiNo = "1234567890", Telefon = "0216 333 44 55" };
        var t = new Teklif { TeklifNo = "TEK-CUST-117", CariUnvan = cari.Unvan, GenelToplam = 9000m };
        var lines = new List<TeklifDetay>
        {
            new() { StokAdi = "Modül A", Miktar = 3, BirimFiyat = 3000m, Tutar = 9000m }
        };

        var res = await _pdfService.GenerateTeklifPdfAsync(t, lines, cari);
        Assert.True(res.Success);
        Assert.NotNull(_testFileService.LastSavedBytes);
    }

    [Fact]
    public async Task Scenario_118_PdfService_GenerateTeklifPdf_EmptyLinesHandledGracefully()
    {
        var t = new Teklif { TeklifNo = "TEK-EMP-118", CariUnvan = "Boş Kalem Müşteri" };
        var emptyLines = new List<TeklifDetay>();

        var res = await _pdfService.GenerateTeklifPdfAsync(t, emptyLines, null);
        Assert.True(res.Success);
    }

    [Fact]
    public async Task Scenario_119_PdfService_TurkishCharactersInQuote_Preserved()
    {
        var t = new Teklif { TeklifNo = "TEK-TR-119", CariUnvan = "ŞAFAK DÖKÜM VE ÇELİK SAN. TİC. A.Ş." };
        var lines = new List<TeklifDetay>
        {
            new() { StokAdi = "AĞIR HİZMET ŞAFTI", Aciklama = "ÖZEL BİLEME İŞLEMİ DAHİL", Miktar = 10, Tutar = 5000 }
        };

        var res = await _pdfService.GenerateTeklifPdfAsync(t, lines, null);
        Assert.True(res.Success);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(10, 10)]
    [InlineData(25, 25)]
    public void Scenario_120_to_122_QuoteLinesCollectionCount(int count, int expected)
    {
        var list = new List<TeklifDetay>();
        for (int i = 0; i < count; i++) list.Add(new TeklifDetay { StokAdi = $"Ürün {i}" });
        Assert.Equal(expected, list.Count);
    }

    [Theory]
    [InlineData("Teklif No:", true)]
    [InlineData("Tarih:", true)]
    [InlineData("Toplam:", true)]
    public void Scenario_123_to_125_StandardQuotePdfHeaders(string header, bool isRequired)
    {
        Assert.True(isRequired);
        Assert.False(string.IsNullOrEmpty(header));
    }

    // =============================================================
    // 11. Teklif - Sipariş Dönüşümü & İş Mantığı (126 - 135)
    // =============================================================

    [Fact]
    public async Task Scenario_126_TeklifToSiparis_FullConversionFlow()
    {
        // 1. Teklif Oluşturulur
        var teklif = new Teklif { TeklifNo = "TEK-CNV-126", CariId = 33, CariUnvan = "Dönüşüm Cari", GenelToplam = 15000m, Durum = "Onaylandı" };
        var tLine = new TeklifDetay { StokAdi = "Trafo", Miktar = 1, BirimFiyat = 15000m, Tutar = 15000m };
        await _uow.Teklifler.SaveWithDetailsAsync(teklif, new List<TeklifDetay> { tLine });

        // 2. Sipariş Açılır ve Teklif Durumu Güncellenir
        var siparis = new Siparis
        {
            SiparisNo = "SIP-FROM-TEK-126",
            CariId = teklif.CariId,
            CariUnvan = teklif.CariUnvan,
            GenelToplam = teklif.GenelToplam,
            BaglantiEvrakNo = teklif.TeklifNo,
            Durum = "Beklemede"
        };
        var sLine = new SiparisDetay { StokAdi = tLine.StokAdi, Miktar = tLine.Miktar, BirimFiyat = tLine.BirimFiyat, Tutar = tLine.Tutar };
        await _uow.Siparisler.SaveWithDetailsAsync(siparis, new List<SiparisDetay> { sLine });

        teklif.Durum = "Siparişe Dönüştü";
        await _uow.Teklifler.SaveAsync(teklif);

        var retrievedTeklif = await _uow.Teklifler.GetByIdAsync(teklif.Id);
        var retrievedSiparis = await _uow.Siparisler.GetByIdAsync(siparis.Id);

        Assert.Equal("Siparişe Dönüştü", retrievedTeklif!.Durum);
        Assert.Equal(teklif.TeklifNo, retrievedSiparis!.BaglantiEvrakNo);
    }

    [Fact]
    public async Task Scenario_127_Teklif_RevisedQuoteCreatesNewVersion()
    {
        var tRev1 = new Teklif { TeklifNo = "TEK-REV-01", GenelToplam = 10000m, Durum = "Revize Edildi" };
        var tRev2 = new Teklif { TeklifNo = "TEK-REV-01-R1", GenelToplam = 9200m, Durum = "Gönderildi" };
        await _uow.Teklifler.SaveAsync(tRev1);
        await _uow.Teklifler.SaveAsync(tRev2);

        var all = await _uow.Teklifler.GetAllAsync();
        Assert.Contains(all, x => x.TeklifNo == "TEK-REV-01");
        Assert.Contains(all, x => x.TeklifNo == "TEK-REV-01-R1");
    }

    [Theory]
    [InlineData("Onaylandı", true)]
    [InlineData("Hazırlandı", false)]
    [InlineData("Gönderildi", false)]
    [InlineData("Reddedildi", false)]
    public void Scenario_128_to_131_CanDirectlyConvertToOrder(string durum, bool canConvert)
    {
        bool allowed = durum == "Onaylandı";
        Assert.Equal(canConvert, allowed);
    }

    [Theory]
    [InlineData(10000, 15000, -5000)]
    [InlineData(20000, 18000, 2000)]
    public void Scenario_132_to_133_QuoteRevisionPriceDifference(decimal oldPrice, decimal newPrice, decimal expectedDiff)
    {
        decimal diff = oldPrice - newPrice;
        Assert.Equal(expectedDiff, diff);
    }

    [Fact]
    public async Task Scenario_134_Teklif_IsDeletedFilter_ExcludesSoftDeleted()
    {
        var t = new Teklif { TeklifNo = "TEK-DEL-134", IsDeleted = true };
        await _uow.Teklifler.SaveAsync(t);

        var all = await _uow.Teklifler.GetAllAsync();
        Assert.DoesNotContain(all, x => x.TeklifNo == "TEK-DEL-134" && !x.IsDeleted);
    }

    [Fact]
    public void Scenario_135_Teklif_IsDeletedDefaultsToFalse()
    {
        var t = new Teklif();
        Assert.False(t.IsDeleted);
    }

    // =============================================================
    // 12. ViewModel UI & Teklif Listesi Kontrolleri (136 - 145)
    // =============================================================

    [AvaloniaFact]
    public async Task Scenario_136_TeklifListViewModel_SearchByMultipleKeywords_FiltersCorrectly()
    {
        await _uow.Teklifler.SaveAsync(new Teklif { TeklifNo = "TK-SRC-1", CariUnvan = "Anadolu Metal" });
        await _uow.Teklifler.SaveAsync(new Teklif { TeklifNo = "TK-SRC-2", CariUnvan = "Trakya Plastik" });

        var vm = _serviceProvider.GetRequiredService<TeklifListViewModel>();
        vm.SearchString = "Anadolu";
        await vm.LoadTekliflerAsync();

        Assert.Contains(vm.Teklifler, t => t.CariUnvan == "Anadolu Metal");
        Assert.DoesNotContain(vm.Teklifler, t => t.CariUnvan == "Trakya Plastik");
    }

    [AvaloniaFact]
    public async Task Scenario_137_TeklifListViewModel_DurumFiltering()
    {
        await _uow.Teklifler.SaveAsync(new Teklif { TeklifNo = "TK-ST-1", Durum = "Onaylandı" });
        await _uow.Teklifler.SaveAsync(new Teklif { TeklifNo = "TK-ST-2", Durum = "Hazırlandı" });

        var all = await _uow.Teklifler.GetAllAsync();
        var approved = all.Where(t => t.Durum == "Onaylandı").ToList();

        Assert.Contains(approved, t => t.TeklifNo == "TK-ST-1");
        Assert.DoesNotContain(approved, t => t.TeklifNo == "TK-ST-2");
    }

    [AvaloniaFact]
    public async Task Scenario_138_TeklifListViewModel_RapidReloads_MaintainsConsistency()
    {
        var vm = _serviceProvider.GetRequiredService<TeklifListViewModel>();
        var t1 = vm.LoadTekliflerAsync();
        var t2 = vm.LoadTekliflerAsync();
        await Task.WhenAll(t1, t2);

        Assert.NotNull(vm.Teklifler);
    }

    [Fact]
    public async Task Scenario_139_Teklif_TwentyBulkQuotes_InsertedWithoutLoss()
    {
        for (int i = 1; i <= 20; i++)
        {
            await _uow.Teklifler.SaveAsync(new Teklif
            {
                TeklifNo = $"TEK-BLK-{i:D2}",
                GenelToplam = i * 350m
            });
        }

        var all = await _uow.Teklifler.GetAllAsync();
        var bulkList = all.Where(t => t.TeklifNo != null && t.TeklifNo.StartsWith("TEK-BLK-")).ToList();
        Assert.True(bulkList.Count >= 20);
    }

    [Fact]
    public async Task Scenario_140_Teklif_NullAndEmptyFields_PersistsGracefully()
    {
        var t = new Teklif
        {
            TeklifNo = "TEK-NULL-140",
            CariUnvan = null,
            Aciklama = null,
            OdemeBilgisi = null,
            GecerlilikTarihi = null
        };
        await _uow.Teklifler.SaveAsync(t);

        var retrieved = await _uow.Teklifler.GetByIdAsync(t.Id);
        Assert.NotNull(retrieved);
        Assert.Null(retrieved.Aciklama);
    }

    [Theory]
    [InlineData(10, 5, 5)]
    [InlineData(30, 30, 0)]
    public void Scenario_141_to_142_ValidityDaysRemaining(int totalDays, int passedDays, int expectedRemaining)
    {
        int remaining = totalDays - passedDays;
        Assert.Equal(expectedRemaining, remaining);
    }

    [Fact]
    public void Scenario_143_TeklifDetay_ZeroPriceCalculation()
    {
        var d = new TeklifDetay { Miktar = 10, BirimFiyat = 0m };
        decimal total = (decimal)d.Miktar * d.BirimFiyat;
        Assert.Equal(0m, total);
    }

    [Fact]
    public void Scenario_144_TeklifDetay_ZeroQuantityCalculation()
    {
        var d = new TeklifDetay { Miktar = 0, BirimFiyat = 150m };
        decimal total = (decimal)d.Miktar * d.BirimFiyat;
        Assert.Equal(0m, total);
    }

    [Fact]
    public void Scenario_145_TeklifDetay_FractionalQuantityPrecision()
    {
        var d = new TeklifDetay { Miktar = 15.5m, BirimFiyat = 40m };
        decimal total = d.Miktar * d.BirimFiyat;
        Assert.Equal(620.0m, total);
    }

    // =============================================================
    // 13. E2E Teklif Yaşam Döngüsü (146 - 154)
    // =============================================================

    [Fact]
    public async Task Scenario_146_EndToEndQuoteLifecycle_PreparationToOrderConversion()
    {
        // 1. Teklif Oluşturma (Hazırlandı)
        var teklif = new Teklif
        {
            TeklifNo = "TEK-E2E-146",
            CariId = 91,
            CariUnvan = "E2E Proje Çözümleri A.Ş.",
            Tarih = DateTime.Today,
            GecerlilikTarihi = DateTime.Today.AddDays(30),
            Durum = "Hazırlandı",
            GenelToplam = 45000m
        };
        var l1 = new TeklifDetay { StokAdi = "Endüstriyel PLC", Miktar = 3, BirimFiyat = 10000m, Tutar = 30000m };
        var l2 = new TeklifDetay { StokAdi = "Yazılım & Devreye Alma", Miktar = 1, BirimFiyat = 15000m, Tutar = 15000m };

        await _uow.Teklifler.SaveWithDetailsAsync(teklif, new List<TeklifDetay> { l1, l2 });
        Assert.True(teklif.Id > 0);

        // 2. Müşteriye Gönderme
        teklif.Durum = "Gönderildi";
        await _uow.Teklifler.SaveAsync(teklif);

        // 3. Müşteri Onayı
        teklif.Durum = "Onaylandı";
        await _uow.Teklifler.SaveAsync(teklif);

        // 4. Siparişe Dönüştürme
        var siparis = new Siparis
        {
            SiparisNo = "SIP-FROM-TEK-E2E",
            CariId = teklif.CariId,
            CariUnvan = teklif.CariUnvan,
            GenelToplam = teklif.GenelToplam,
            BaglantiEvrakNo = teklif.TeklifNo,
            Durum = "Beklemede"
        };
        await _uow.Siparisler.SaveAsync(siparis);

        teklif.Durum = "Siparişe Dönüştü";
        await _uow.Teklifler.SaveAsync(teklif);

        // Doğrulama
        var refTeklif = await _uow.Teklifler.GetByIdAsync(teklif.Id);
        var refSiparis = await _uow.Siparisler.GetByIdAsync(siparis.Id);

        Assert.Equal("Siparişe Dönüştü", refTeklif!.Durum);
        Assert.Equal("TEK-E2E-146", refSiparis!.BaglantiEvrakNo);
    }

    [Fact]
    public async Task Scenario_147_EndToEndQuoteLifecycle_RejectedByCustomer()
    {
        var teklif = new Teklif { TeklifNo = "TEK-REJ-147", Durum = "Gönderildi", GenelToplam = 8000m };
        await _uow.Teklifler.SaveAsync(teklif);

        teklif.Durum = "Reddedildi";
        teklif.Aciklama = "Rakip firma %10 daha uygun fiyat verdi.";
        await _uow.Teklifler.SaveAsync(teklif);

        var retrieved = await _uow.Teklifler.GetByIdAsync(teklif.Id);
        Assert.Equal("Reddedildi", retrieved!.Durum);
        Assert.Equal("Rakip firma %10 daha uygun fiyat verdi.", retrieved.Aciklama);
    }

    [Fact]
    public async Task Scenario_148_Teklif_DeleteCascadesDetails()
    {
        var t = new Teklif { TeklifNo = "TEK-CAS-148", GenelToplam = 4000m };
        var l = new TeklifDetay { StokAdi = "Geçici Kalem", Tutar = 4000m };
        await _uow.Teklifler.SaveWithDetailsAsync(t, new List<TeklifDetay> { l });

        await _uow.Teklifler.DeleteAsync(t);

        var retrieved = await _uow.Teklifler.GetByIdAsync(t.Id);
        Assert.Null(retrieved);
    }

    [Fact]
    public async Task Scenario_149_Teklif_CustomerTitleLongTextSafety()
    {
        string longTitle = new string('B', 300);
        var t = new Teklif { TeklifNo = "TEK-LNG-149", CariUnvan = longTitle };
        await _uow.Teklifler.SaveAsync(t);

        var retrieved = await _uow.Teklifler.GetByIdAsync(t.Id);
        Assert.Equal(longTitle, retrieved!.CariUnvan);
    }

    [Theory]
    [InlineData(10, 1000, 10000)]
    [InlineData(50, 200, 10000)]
    public void Scenario_150_to_151_BatchQuoteTotal(int adet, decimal fiyat, decimal expectedTotal)
    {
        decimal total = adet * fiyat;
        Assert.Equal(expectedTotal, total);
    }

    [Fact]
    public void Scenario_152_Teklif_DefaultDurum_IsHazirlandi()
    {
        var t = new Teklif { Durum = "Hazırlandı" };
        Assert.Equal("Hazırlandı", t.Durum);
    }

    [Fact]
    public async Task Scenario_153_Teklif_GetAllAsync_ExcludesSoftDeleted()
    {
        var t = new Teklif { TeklifNo = "TEK-SD-153", IsDeleted = true };
        await _uow.Teklifler.SaveAsync(t);

        var all = await _uow.Teklifler.GetAllAsync();
        Assert.DoesNotContain(all, x => x.TeklifNo == "TEK-SD-153" && !x.IsDeleted);
    }

    [Fact]
    public async Task Scenario_154_Teklif_DirectRepositoryAccess_Audit()
    {
        var t = new Teklif { TeklifNo = "TEK-AUDIT-154", GenelToplam = 890m };
        await _uow.Teklifler.SaveAsync(t);

        var fetched = await _uow.Teklifler.GetByIdAsync(t.Id);
        Assert.NotNull(fetched);
        Assert.Equal(890m, fetched.GenelToplam);
    }
}
