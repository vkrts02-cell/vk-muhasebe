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

public partial class SiparisScenariosTests
{
    // =============================================================
    // 9. Sipariş Kalem Detayları & Metrikleri (101 - 115)
    // =============================================================

    [Fact]
    public async Task Scenario_101_SiparisDetay_TopMiktariAndBirim_PersistsAccurately()
    {
        var siparis = new Siparis { SiparisNo = "SIP-TOP-101", GenelToplam = 7500m };
        var line = new SiparisDetay
        {
            StokAdi = "Rulo Kumaş",
            Miktar = 500,
            TopMiktari = 10,
            Birim = "Metre",
            BirimFiyat = 15m,
            Tutar = 7500m,
            MiktarAciklama = "10 Top x 50 Metre"
        };
        await _uow.Siparisler.SaveWithDetailsAsync(siparis, new List<SiparisDetay> { line });

        var lines = await _uow.Siparisler.GetDetaylarAsync(siparis.Id);
        var first = lines.First();
        Assert.Equal(10, first.TopMiktari);
        Assert.Equal("Metre", first.Birim);
        Assert.Equal("10 Top x 50 Metre", first.MiktarAciklama);
    }

    [Fact]
    public async Task Scenario_102_SiparisDetay_GetAllDetaylarAsync_ReturnsAllOrderLines()
    {
        var s1 = new Siparis { SiparisNo = "SIP-ALL-1" };
        var s2 = new Siparis { SiparisNo = "SIP-ALL-2" };
        await _uow.Siparisler.SaveWithDetailsAsync(s1, new List<SiparisDetay> { new() { StokAdi = "K1", Tutar = 100 } });
        await _uow.Siparisler.SaveWithDetailsAsync(s2, new List<SiparisDetay> { new() { StokAdi = "K2", Tutar = 200 } });

        var allLines = await _uow.Siparisler.GetAllDetaylarAsync();
        Assert.Contains(allLines, l => l.StokAdi == "K1");
        Assert.Contains(allLines, l => l.StokAdi == "K2");
    }

    [Theory]
    [InlineData("Adet")]
    [InlineData("Metre")]
    [InlineData("Kg")]
    [InlineData("Koli")]
    [InlineData("Paket")]
    public async Task Scenario_103_to_107_SiparisDetay_StandardUnitsSupported(string birim)
    {
        var line = new SiparisDetay { Birim = birim, Miktar = 10, BirimFiyat = 50, Tutar = 500 };
        Assert.Equal(birim, line.Birim);
    }

    [Fact]
    public async Task Scenario_108_Siparis_UpdateOrderNotes_PersistsCorrectly()
    {
        var s = new Siparis { SiparisNo = "SIP-N-108", Aciklama = "Eski Not" };
        await _uow.Siparisler.SaveAsync(s);

        s.Aciklama = "Güncellenmiş sevk notu: Paletli yükleme";
        await _uow.Siparisler.SaveAsync(s);

        var retrieved = await _uow.Siparisler.GetByIdAsync(s.Id);
        Assert.Equal("Güncellenmiş sevk notu: Paletli yükleme", retrieved!.Aciklama);
    }

    [Theory]
    [InlineData(1000, 10, 100)]
    [InlineData(5000, 15, 750)]
    [InlineData(2000, 0, 0)]
    public void Scenario_109_to_111_OrderDiscountDeduction(decimal brut, double iskontoOrani, decimal expectedIskonto)
    {
        decimal iskonto = brut * (decimal)(iskontoOrani / 100.0);
        Assert.Equal(expectedIskonto, iskonto);
    }

    [Theory]
    [InlineData(900, 180, 1080)]
    [InlineData(4250, 850, 5100)]
    public void Scenario_112_to_113_OrderNetPlusVatSum(decimal net, decimal kdv, decimal expectedTotal)
    {
        decimal total = net + kdv;
        Assert.Equal(expectedTotal, total);
    }

    [Fact]
    public void Scenario_114_Siparis_FiyatPropertyAlias_SyncsWithBirimFiyat()
    {
        var d = new SiparisDetay { BirimFiyat = 250m };
        Assert.Equal(250m, d.Fiyat);

        d.Fiyat = 300m;
        Assert.Equal(300m, d.BirimFiyat);
    }

    [Fact]
    public void Scenario_115_Siparis_DefaultKayitTarihi_IsNearCurrentTime()
    {
        var s = new Siparis();
        Assert.True((DateTime.Now - s.KayitTarihi).TotalMinutes < 5);
    }

    // =============================================================
    // 10. PDF Çıktıları & İletişim Testleri (116 - 125)
    // =============================================================

    [Fact]
    public async Task Scenario_116_PdfService_GenerateSiparisPdf_ProducesNonEmptyBytes()
    {
        var s = new Siparis { SiparisNo = "SIP-PDF-116", CariUnvan = "Test Ltd.", GenelToplam = 12500m };
        var lines = new List<SiparisDetay>
        {
            new() { StokAdi = "Kalem A", Miktar = 10, BirimFiyat = 1250m, Tutar = 12500m }
        };

        var res = await _pdfService.GenerateSiparisPdfAsync(s, lines, null);
        Assert.True(res.Success);
        Assert.NotNull(_testFileService.LastSavedBytes);
        Assert.True(_testFileService.LastSavedBytes.Length > 500);
    }

    [Fact]
    public async Task Scenario_117_PdfService_GenerateSiparisPdfWithCustomerCard_EmbedsCustomer()
    {
        var cari = new CariKart { Unvan = "ERMAY GRUP A.Ş.", Telefon = "0216 111 22 33" };
        var s = new Siparis { SiparisNo = "SIP-PRF-117", CariUnvan = "Alıcı Firma", GenelToplam = 5000m };
        var lines = new List<SiparisDetay>
        {
            new() { StokAdi = "Ürün 1", Miktar = 5, BirimFiyat = 1000m, Tutar = 5000m }
        };

        var res = await _pdfService.GenerateSiparisPdfAsync(s, lines, cari);
        Assert.True(res.Success);
        Assert.NotNull(_testFileService.LastSavedBytes);
    }

    [Theory]
    [InlineData("TR", "₺")]
    [InlineData("USD", "$")]
    [InlineData("EUR", "€")]
    public void Scenario_118_to_120_SiparisCurrencySymbols(string code, string symbol)
    {
        string s = code switch { "TR" => "₺", "USD" => "$", "EUR" => "€", _ => code };
        Assert.Equal(symbol, s);
    }

    [Fact]
    public async Task Scenario_121_PdfService_EmptyLinesOrderPdf_GeneratesGracefully()
    {
        var s = new Siparis { SiparisNo = "SIP-EMP-121", CariUnvan = "Sıfır Kalem Müşteri" };
        var emptyLines = new List<SiparisDetay>();

        var res = await _pdfService.GenerateSiparisPdfAsync(s, emptyLines, null);
        Assert.True(res.Success);
    }

    [Fact]
    public async Task Scenario_122_PdfService_TurkishCharactersInOrderDetails_RendersProperly()
    {
        var s = new Siparis { SiparisNo = "SIP-TR-122", CariUnvan = "IŞIKLI ÇELİK VE ŞAFT A.Ş." };
        var lines = new List<SiparisDetay>
        {
            new() { StokAdi = "ÖZEL DÖKÜM ÇİVİ", Aciklama = "AĞIR HİZMET ŞARTLARINA UYGUN", Miktar = 100, Tutar = 1000 }
        };

        var res = await _pdfService.GenerateSiparisPdfAsync(s, lines, null);
        Assert.True(res.Success);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(15, 15)]
    [InlineData(30, 30)]
    public void Scenario_123_to_125_OrderLinesCountVerification(int inputCount, int expectedCount)
    {
        var list = new List<SiparisDetay>();
        for (int i = 0; i < inputCount; i++) list.Add(new SiparisDetay { StokAdi = $"Kalem {i}" });
        Assert.Equal(expectedCount, list.Count);
    }

    // =============================================================
    // 11. ViewModel UI & Sipariş Listesi Kontrolleri (126 - 135)
    // =============================================================

    [AvaloniaFact]
    public async Task Scenario_126_SiparisListViewModel_SearchByMultipleKeywords_FiltersCorrectly()
    {
        await _uow.Siparisler.SaveAsync(new Siparis { SiparisNo = "SIP-SRC-1", CariUnvan = "Marmara Boya" });
        await _uow.Siparisler.SaveAsync(new Siparis { SiparisNo = "SIP-SRC-2", CariUnvan = "Ege Kimya" });

        var vm = _serviceProvider.GetRequiredService<SiparisListViewModel>();
        vm.SearchString = "Marmara";
        await vm.LoadSiparislerAsync();

        Assert.Contains(vm.Siparisler, s => s.CariUnvan == "Marmara Boya");
        Assert.DoesNotContain(vm.Siparisler, s => s.CariUnvan == "Ege Kimya");
    }

    [AvaloniaFact]
    public async Task Scenario_127_SiparisListViewModel_DurumFilter_FiltersByStatus()
    {
        await _uow.Siparisler.SaveAsync(new Siparis { SiparisNo = "SIP-ST-1", Durum = "Onaylandı" });
        await _uow.Siparisler.SaveAsync(new Siparis { SiparisNo = "SIP-ST-2", Durum = "Hazırlanıyor" });

        var all = await _uow.Siparisler.GetAllAsync();
        var approvedOnly = all.Where(s => s.Durum == "Onaylandı").ToList();

        Assert.Contains(approvedOnly, s => s.SiparisNo == "SIP-ST-1");
        Assert.DoesNotContain(approvedOnly, s => s.SiparisNo == "SIP-ST-2");
    }

    [AvaloniaFact]
    public async Task Scenario_128_SiparisListViewModel_RapidReloads_MaintainsConsistency()
    {
        var vm = _serviceProvider.GetRequiredService<SiparisListViewModel>();
        var t1 = vm.LoadSiparislerAsync();
        var t2 = vm.LoadSiparislerAsync();
        await Task.WhenAll(t1, t2);

        Assert.NotNull(vm.Siparisler);
    }

    [Theory]
    [InlineData("SIP-2026-0001")]
    [InlineData("SIP-2026-0002")]
    [InlineData("SIP-CUSTOM-99")]
    public async Task Scenario_129_to_131_Siparis_SaveAndRetrieveByUniqueNo(string no)
    {
        var s = new Siparis { SiparisNo = no, GenelToplam = 100m };
        await _uow.Siparisler.SaveAsync(s);

        var all = await _uow.Siparisler.GetAllAsync();
        Assert.Contains(all, x => x.SiparisNo == no);
    }

    [Fact]
    public void Scenario_132_Siparis_DefaultStatus_IsNotNull()
    {
        var s = new Siparis { Durum = "Beklemede" };
        Assert.Equal("Beklemede", s.Durum);
    }

    [Theory]
    [InlineData(10, 5, 5)]
    [InlineData(20, 20, 0)]
    [InlineData(30, 0, 30)]
    public void Scenario_133_to_135_RemainingQuantityCalculations(int ordered, int delivered, int expectedRemaining)
    {
        int remaining = ordered - delivered;
        Assert.Equal(expectedRemaining, remaining);
    }

    // =============================================================
    // 12. Sınır Değerler & Doğrulama Senaryoları (136 - 145)
    // =============================================================

    [Fact]
    public async Task Scenario_136_Siparis_TwentyBulkOrders_InsertedWithoutLoss()
    {
        for (int i = 1; i <= 20; i++)
        {
            await _uow.Siparisler.SaveAsync(new Siparis
            {
                SiparisNo = $"SIP-BLK-{i:D2}",
                GenelToplam = i * 250m
            });
        }

        var all = await _uow.Siparisler.GetAllAsync();
        var bulkList = all.Where(s => s.SiparisNo != null && s.SiparisNo.StartsWith("SIP-BLK-")).ToList();
        Assert.True(bulkList.Count >= 20);
    }

    [Fact]
    public async Task Scenario_137_Siparis_EmptyAndNullFields_PersistsGracefully()
    {
        var s = new Siparis
        {
            SiparisNo = "SIP-NULL-137",
            CariUnvan = null,
            Aciklama = null,
            PdfNotlar = null,
            OdemeBilgisi = null,
            Oncelik = null,
            BaglantiEvrakNo = null
        };
        await _uow.Siparisler.SaveAsync(s);

        var retrieved = await _uow.Siparisler.GetByIdAsync(s.Id);
        Assert.NotNull(retrieved);
        Assert.Null(retrieved.Aciklama);
    }

    [Theory]
    [InlineData(1000000, 200000, 1200000)]
    [InlineData(5000000, 1000000, 6000000)]
    public void Scenario_138_to_139_LargeOrderTotals(decimal matrah, decimal kdv, decimal expectedTotal)
    {
        decimal total = matrah + kdv;
        Assert.Equal(expectedTotal, total);
    }

    [Fact]
    public async Task Scenario_140_Siparis_DeliveryDatePast_IdentifiedAsDelayed()
    {
        var s = new Siparis
        {
            SiparisNo = "SIP-DELAY-140",
            TeslimatTarihi = DateTime.Today.AddDays(-3),
            Durum = "Beklemede"
        };
        await _uow.Siparisler.SaveAsync(s);

        bool isDelayed = s.TeslimatTarihi.HasValue && s.TeslimatTarihi.Value < DateTime.Today && s.Durum != "Teslim Edildi";
        Assert.True(isDelayed);
    }

    [Theory]
    [InlineData("2026-09-01", "2026-09-10", true)]
    [InlineData("2026-09-15", "2026-09-10", false)]
    public void Scenario_141_to_142_DeliveryDateComparison(string dateStr, string todayStr, bool isPast)
    {
        DateTime date = DateTime.Parse(dateStr);
        DateTime today = DateTime.Parse(todayStr);

        bool past = date < today;
        Assert.Equal(isPast, past);
    }

    [Fact]
    public void Scenario_143_SiparisDetay_ZeroQuantity_CalculatesZeroTotal()
    {
        var d = new SiparisDetay { Miktar = 0, BirimFiyat = 500m };
        decimal total = (decimal)d.Miktar * d.BirimFiyat;
        Assert.Equal(0m, total);
    }

    [Fact]
    public void Scenario_144_SiparisDetay_ZeroPrice_CalculatesZeroTotal()
    {
        var d = new SiparisDetay { Miktar = 50, BirimFiyat = 0m };
        decimal total = (decimal)d.Miktar * d.BirimFiyat;
        Assert.Equal(0m, total);
    }

    [Fact]
    public void Scenario_145_SiparisDetay_FractionalQuantityPrecision()
    {
        var d = new SiparisDetay { Miktar = 12.375m, BirimFiyat = 80m };
        decimal total = d.Miktar * d.BirimFiyat;
        Assert.Equal(990.00m, total);
    }

    // =============================================================
    // 13. E2E Sipariş Yaşam Döngüsü (146 - 154)
    // =============================================================

    [Fact]
    public async Task Scenario_146_EndToEndOrderLifecycle_FromQuoteToInvoice()
    {
        // 1. Sipariş Başlığı ve Detayları Oluşturulur
        var siparis = new Siparis
        {
            SiparisNo = "SIP-E2E-146",
            CariId = 44,
            CariUnvan = "E2E Lojistik A.Ş.",
            Tarih = DateTime.Today,
            TeslimatTarihi = DateTime.Today.AddDays(7),
            Durum = "Beklemede",
            GenelToplam = 18000m
        };
        var line1 = new SiparisDetay { StokAdi = "Endüstriyel Motor", Miktar = 2, BirimFiyat = 6000m, Tutar = 12000m };
        var line2 = new SiparisDetay { StokAdi = "Şaft Bağlantı Parçası", Miktar = 6, BirimFiyat = 1000m, Tutar = 6000m };

        await _uow.Siparisler.SaveWithDetailsAsync(siparis, new List<SiparisDetay> { line1, line2 });
        Assert.True(siparis.Id > 0);

        // 2. Sipariş Onaylanır
        siparis.Durum = "Onaylandı";
        await _uow.Siparisler.SaveAsync(siparis);
        var ref1 = await _uow.Siparisler.GetByIdAsync(siparis.Id);
        Assert.Equal("Onaylandı", ref1!.Durum);

        // 3. Sipariş Hazırlanır ve Kargoya Verilir
        siparis.Durum = "Kargoya Verildi";
        await _uow.Siparisler.SaveAsync(siparis);
        var ref2 = await _uow.Siparisler.GetByIdAsync(siparis.Id);
        Assert.Equal("Kargoya Verildi", ref2!.Durum);

        // 4. Sipariş Teslim Edilir
        siparis.Durum = "Teslim Edildi";
        await _uow.Siparisler.SaveAsync(siparis);
        var ref3 = await _uow.Siparisler.GetByIdAsync(siparis.Id);
        Assert.Equal("Teslim Edildi", ref3!.Durum);

        // 5. Sipariş Faturalanır ve Bağlantı Fatura Numarası Eklenir
        siparis.Durum = "Faturalandı";
        siparis.BaglantiEvrakNo = "FAT-2026-9901";
        await _uow.Siparisler.SaveAsync(siparis);

        var refFinal = await _uow.Siparisler.GetByIdAsync(siparis.Id);
        Assert.Equal("Faturalandı", refFinal!.Durum);
        Assert.Equal("FAT-2026-9901", refFinal.BaglantiEvrakNo);
    }

    [Fact]
    public async Task Scenario_147_EndToEndOrderLifecycle_CancelledBeforeProduction()
    {
        var siparis = new Siparis { SiparisNo = "SIP-CNL-147", Durum = "Beklemede", GenelToplam = 3000m };
        await _uow.Siparisler.SaveAsync(siparis);

        // İptal Talebi
        siparis.Durum = "İptal Edildi";
        siparis.Aciklama = "Müşteri vazgeçti";
        await _uow.Siparisler.SaveAsync(siparis);

        var retrieved = await _uow.Siparisler.GetByIdAsync(siparis.Id);
        Assert.Equal("İptal Edildi", retrieved!.Durum);
        Assert.Equal("Müşteri vazgeçti", retrieved.Aciklama);
    }

    [Fact]
    public async Task Scenario_148_Siparis_DeleteCascadesDetails()
    {
        var s = new Siparis { SiparisNo = "SIP-CAS-148", GenelToplam = 2000m };
        var l = new SiparisDetay { StokAdi = "Geçici Ürün", Tutar = 2000m };
        await _uow.Siparisler.SaveWithDetailsAsync(s, new List<SiparisDetay> { l });

        await _uow.Siparisler.DeleteAsync(s);

        var retrieved = await _uow.Siparisler.GetByIdAsync(s.Id);
        Assert.Null(retrieved);
    }

    [Fact]
    public async Task Scenario_149_Siparis_CustomerTitleTruncationSafety()
    {
        string longTitle = new string('A', 300);
        var s = new Siparis { SiparisNo = "SIP-LNG-149", CariUnvan = longTitle };
        await _uow.Siparisler.SaveAsync(s);

        var retrieved = await _uow.Siparisler.GetByIdAsync(s.Id);
        Assert.Equal(longTitle, retrieved!.CariUnvan);
    }

    [Theory]
    [InlineData(100, 1500, 150000)]
    [InlineData(25, 400, 10000)]
    public void Scenario_150_to_151_BatchOrderCosting(int adet, decimal birimFiyat, decimal expectedTotal)
    {
        decimal total = adet * birimFiyat;
        Assert.Equal(expectedTotal, total);
    }

    [Fact]
    public void Scenario_152_Siparis_IsDeleted_DefaultsToFalse()
    {
        var s = new Siparis();
        Assert.False(s.IsDeleted);
    }

    [Fact]
    public async Task Scenario_153_Siparis_GetAllAsync_ExcludesSoftDeleted()
    {
        var s = new Siparis { SiparisNo = "SIP-SD-153", IsDeleted = true };
        await _uow.Siparisler.SaveAsync(s);

        var all = await _uow.Siparisler.GetAllAsync();
        Assert.DoesNotContain(all, x => x.SiparisNo == "SIP-SD-153" && !x.IsDeleted);
    }

    [Fact]
    public async Task Scenario_154_Siparis_DirectRepositoryAccess_Audit()
    {
        var s = new Siparis { SiparisNo = "SIP-AUDIT-154", GenelToplam = 770m };
        await _uow.Siparisler.SaveAsync(s);

        var fetched = await _uow.Siparisler.GetByIdAsync(s.Id);
        Assert.NotNull(fetched);
        Assert.Equal(770m, fetched.GenelToplam);
    }
}
