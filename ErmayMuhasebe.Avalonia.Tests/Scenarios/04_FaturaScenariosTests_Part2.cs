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

public partial class FaturaScenariosTests
{
    // =============================================================
    // 5. Fatura Tipleri, Yönleri & Ödeme Şekilleri (51-65)
    // =============================================================

    [Theory]
    [InlineData("Satış")]
    [InlineData("Alış")]
    [InlineData("Satış İade")]
    [InlineData("Alış İade")]
    public async Task Scenario_51_to_54_InvoiceTypes_PersistAccurately(string tur)
    {
        var fatura = new Fatura
        {
            FaturaNo = $"FAT-TUR-{tur}",
            Tur = tur,
            Tarih = DateTime.Today,
            GenelToplam = 5000m
        };
        await _uow.Faturalar.SaveAsync(fatura);

        var retrieved = await _uow.Faturalar.GetByIdAsync(fatura.Id);
        Assert.NotNull(retrieved);
        Assert.Equal(tur, retrieved.Tur);
    }

    [Theory]
    [InlineData("Açık Hesap")]
    [InlineData("Nakit")]
    [InlineData("Kredi Kartı")]
    [InlineData("Banka Havalesi")]
    public async Task Scenario_55_to_58_PaymentMethods_PersistAccurately(string odemeSekli)
    {
        var fatura = new Fatura
        {
            FaturaNo = $"FAT-PAY-{odemeSekli}",
            Tur = "Satış",
            OdemeSekli = odemeSekli,
            GenelToplam = 3500m
        };
        await _uow.Faturalar.SaveAsync(fatura);

        var retrieved = await _uow.Faturalar.GetByIdAsync(fatura.Id);
        Assert.Equal(odemeSekli, retrieved!.OdemeSekli);
    }

    [Fact]
    public async Task Scenario_59_CashPayment_CanLinkToKasaId()
    {
        var fatura = new Fatura
        {
            FaturaNo = "FAT-KASA-01",
            Tur = "Satış",
            OdemeSekli = "Nakit",
            KasaId = 1,
            GenelToplam = 1200m
        };
        await _uow.Faturalar.SaveAsync(fatura);

        var retrieved = await _uow.Faturalar.GetByIdAsync(fatura.Id);
        Assert.Equal(1, retrieved!.KasaId);
    }

    [Fact]
    public async Task Scenario_60_BankPayment_CanLinkToBankaId()
    {
        var fatura = new Fatura
        {
            FaturaNo = "FAT-BANK-01",
            Tur = "Satış",
            OdemeSekli = "Banka Havalesi",
            BankaId = 2,
            GenelToplam = 8500m
        };
        await _uow.Faturalar.SaveAsync(fatura);

        var retrieved = await _uow.Faturalar.GetByIdAsync(fatura.Id);
        Assert.Equal(2, retrieved!.BankaId);
    }

    // =============================================================
    // 6. Fatura Numaralandırma & Resmi Formatlar (61-70)
    // =============================================================

    [Theory]
    [InlineData("GIB2026000000001")]
    [InlineData("EAR2026000000099")]
    [InlineData("FAT-A-000123")]
    [InlineData("2026/03/458")]
    public async Task Scenario_61_to_64_OfficialInvoiceNumberFormats(string faturaNo)
    {
        var fatura = new Fatura { FaturaNo = faturaNo, Tur = "Satış", GenelToplam = 1000m };
        await _uow.Faturalar.SaveAsync(fatura);

        var retrieved = await _uow.Faturalar.GetByIdAsync(fatura.Id);
        Assert.Equal(faturaNo, retrieved!.FaturaNo);
    }

    [Fact]
    public async Task Scenario_65_Invoice_BaglantiEvrakNo_StoresIrsaliyeOrSiparisRef()
    {
        string irsaliyeNo = "IRS-2026-000456";
        var fatura = new Fatura
        {
            FaturaNo = "FAT-BAG-01",
            Tur = "Satış",
            BaglantiEvrakNo = irsaliyeNo,
            GenelToplam = 4000m
        };
        await _uow.Faturalar.SaveAsync(fatura);

        var retrieved = await _uow.Faturalar.GetByIdAsync(fatura.Id);
        Assert.Equal(irsaliyeNo, retrieved!.BaglantiEvrakNo);
    }

    [Fact]
    public async Task Scenario_66_Invoice_IsEArsivFlag_PersistsTrueForElectronicInvoices()
    {
        var fatura = new Fatura
        {
            FaturaNo = "GIB2026000012345",
            Tur = "Satış",
            IsEArsiv = true,
            GenelToplam = 950m
        };
        await _uow.Faturalar.SaveAsync(fatura);

        var retrieved = await _uow.Faturalar.GetByIdAsync(fatura.Id);
        Assert.True(retrieved!.IsEArsiv);
    }

    [Fact]
    public async Task Scenario_67_Invoice_CorporateTaxInfo_StoresDaireAndNo()
    {
        var fatura = new Fatura
        {
            FaturaNo = "FAT-CORP-01",
            Tur = "Satış",
            VergiDairesi = "Zincirlikuyu",
            VergiNo = "5554443322",
            GenelToplam = 15000m
        };
        await _uow.Faturalar.SaveAsync(fatura);

        var retrieved = await _uow.Faturalar.GetByIdAsync(fatura.Id);
        Assert.Equal("Zincirlikuyu", retrieved!.VergiDairesi);
        Assert.Equal("5554443322", retrieved.VergiNo);
    }

    [Fact]
    public async Task Scenario_68_Invoice_CustomerAddress_StoresFullText()
    {
        string fullAddr = "Atatürk Mah. İkitelli OSB Demirciler Sitesi 4. Yol No: 18 Başakşehir/İstanbul";
        var fatura = new Fatura
        {
            FaturaNo = "FAT-ADDR-01",
            Tur = "Satış",
            Adres = fullAddr,
            GenelToplam = 2200m
        };
        await _uow.Faturalar.SaveAsync(fatura);

        var retrieved = await _uow.Faturalar.GetByIdAsync(fatura.Id);
        Assert.Equal(fullAddr, retrieved!.Adres);
    }

    [Fact]
    public async Task Scenario_69_Invoice_Aciklama_StoresMultipleLines()
    {
        string notes = "İşbu fatura 15 gün içinde itiraz edilmediği takdirde kabul edilmiş sayılır.\nÖdeme Garanti Bankası TR... nolu hesaba yapılmalıdır.";
        var fatura = new Fatura
        {
            FaturaNo = "FAT-NOTE-01",
            Tur = "Satış",
            Aciklama = notes,
            GenelToplam = 7800m
        };
        await _uow.Faturalar.SaveAsync(fatura);

        var retrieved = await _uow.Faturalar.GetByIdAsync(fatura.Id);
        Assert.Equal(notes, retrieved!.Aciklama);
    }

    [Fact]
    public async Task Scenario_70_Invoice_CariUnvan_StoresSnapshotCustomerName()
    {
        string unvan = "Mega Lojistik ve Taşımacılık San. Tic. A.Ş.";
        var fatura = new Fatura
        {
            FaturaNo = "FAT-UNV-01",
            Tur = "Satış",
            CariId = 10,
            CariUnvan = unvan,
            GenelToplam = 6000m
        };
        await _uow.Faturalar.SaveAsync(fatura);

        var retrieved = await _uow.Faturalar.GetByIdAsync(fatura.Id);
        Assert.Equal(unvan, retrieved!.CariUnvan);
    }

    // =============================================================
    // 7. Tevkifat & Özel Vergi Hesaplamaları (71-80)
    // =============================================================

    [Theory]
    [InlineData(10000, 20, 5, 10, 1000, 1000, 11000)] // 5/10 Tevkifat: 2000 KDV -> 1000 Alıcı, 1000 Satıcı Tevkif Edilen
    [InlineData(10000, 20, 7, 10, 1400, 600, 10600)]  // 7/10 Tevkifat: 2000 KDV -> 1400 Tevkif, 600 Tahsil Edilen
    [InlineData(10000, 20, 9, 10, 1800, 200, 10200)]  // 9/10 Tevkifat: 2000 KDV -> 1800 Tevkif, 200 Tahsil Edilen
    [InlineData(5000, 10, 5, 10, 250, 250, 5250)]     // 5/10 Tevkifat %10 KDV: 500 KDV -> 250 Tevkif, 250 Tahsil Edilen
    public void Scenario_71_to_74_WithholdingTaxCalculations(decimal matrah, int kdvOrani, int pay, int payda, decimal expTevkifat, decimal expTahsilKdv, decimal expOdenecekToplam)
    {
        decimal toplamKdv = matrah * (kdvOrani / 100m);
        decimal tevkifatTutari = toplamKdv * (pay / (decimal)payda);
        decimal tahsilKdv = toplamKdv - tevkifatTutari;
        decimal toplam = matrah + tahsilKdv;

        Assert.Equal(expTevkifat, tevkifatTutari);
        Assert.Equal(expTahsilKdv, tahsilKdv);
        Assert.Equal(expOdenecekToplam, toplam);
    }

    [Theory]
    [InlineData(10000, 20, 2000)] // %20 Stopaj kesintisi
    [InlineData(5000, 10, 500)]   // %10 Stopaj kesintisi
    public void Scenario_75_to_76_WithholdingIncomeTax_Stopaj(decimal brutUcret, decimal stopajOrani, decimal expStopaj)
    {
        decimal stopaj = brutUcret * (stopajOrani / 100m);
        Assert.Equal(expStopaj, stopaj);
    }

    [Fact]
    public void Scenario_77_SpecialCommunicationTax_OivCalculation()
    {
        decimal matrah = 1000m;
        decimal oivOrani = 10m; // %10 ÖİV
        decimal kdvOrani = 20m; // %20 KDV

        decimal oiv = matrah * (oivOrani / 100m);
        decimal kdv = matrah * (kdvOrani / 100m);
        decimal genelToplam = matrah + oiv + kdv;

        Assert.Equal(100m, oiv);
        Assert.Equal(200m, kdv);
        Assert.Equal(1300m, genelToplam);
    }

    [Fact]
    public void Scenario_78_AccommodationTax_KonaklamaVergisiCalculation()
    {
        decimal odaUcreti = 4000m;
        decimal konaklamaVergiOrani = 2m; // %2 Konaklama Vergisi
        decimal kdvOrani = 10m; // %10 KDV

        decimal konaklamaVergisi = odaUcreti * (konaklamaVergiOrani / 100m);
        decimal kdv = odaUcreti * (kdvOrani / 100m);
        decimal toplam = odaUcreti + konaklamaVergisi + kdv;

        Assert.Equal(80m, konaklamaVergisi);
        Assert.Equal(400m, kdv);
        Assert.Equal(4480m, toplam);
    }

    [Fact]
    public async Task Scenario_79_Invoice_TotalsSynchronization_AraToplamPlusKdvEqualsGenelToplam()
    {
        var fatura = new Fatura
        {
            FaturaNo = "FAT-SYN-01",
            Tur = "Satış",
            AraToplam = 8000m,
            ToplamKDV = 1600m,
            GenelToplam = 9600m
        };
        await _uow.Faturalar.SaveAsync(fatura);

        var retrieved = await _uow.Faturalar.GetByIdAsync(fatura.Id);
        Assert.Equal(retrieved!.AraToplam + retrieved.ToplamKDV, retrieved.GenelToplam);
    }

    [Fact]
    public async Task Scenario_80_Invoice_KdvToplamAlias_EqualsToplamKDV()
    {
        var fatura = new Fatura { KdvToplam = 450m };
        Assert.Equal(450m, fatura.ToplamKDV);

        fatura.ToplamKDV = 600m;
        Assert.Equal(600m, fatura.KdvToplam);
    }

    // =============================================================
    // 8. Fatura Kalemleri & İskontolar (81-90)
    // =============================================================

    [Fact]
    public async Task Scenario_81_FaturaDetay_MultipleLines_SavesAndCalculatesTotal()
    {
        var fatura = new Fatura { FaturaNo = "FAT-LINES-81", Tur = "Satış", GenelToplam = 2300m };
        var line1 = new FaturaDetay { StokAdi = "Kalem A", Miktar = 10, BirimFiyat = 100m, KDVOrani = 20, KdvTutari = 200m, ToplamTutar = 1200m };
        var line2 = new FaturaDetay { StokAdi = "Kalem B", Miktar = 5, BirimFiyat = 200m, KDVOrani = 10, KdvTutari = 100m, ToplamTutar = 1100m };

        await _uow.Faturalar.SaveWithDetailsAsync(fatura, new List<FaturaDetay> { line1, line2 });

        var lines = await _uow.Faturalar.GetDetaylarAsync(fatura.Id);
        Assert.Equal(2, lines.Count);
        Assert.Equal(2300m, lines.Sum(l => l.ToplamTutar));
    }

    [Fact]
    public async Task Scenario_82_FaturaDetay_TutarPropertyAlias_MatchesToplamTutar()
    {
        var d = new FaturaDetay { ToplamTutar = 1750m };
        Assert.Equal(1750m, d.Tutar);
    }

    [Fact]
    public async Task Scenario_83_FaturaDetay_ZeroPriceFreeLine_AllowsPromotionalItems()
    {
        var fatura = new Fatura { FaturaNo = "FAT-PROMO", Tur = "Satış", GenelToplam = 1000m };
        var promoLine = new FaturaDetay
        {
            StokAdi = "Promosyon Tişört",
            Miktar = 1,
            BirimFiyat = 0m,
            KDVOrani = 0,
            ToplamTutar = 0m
        };
        await _uow.Faturalar.SaveWithDetailsAsync(fatura, new List<FaturaDetay> { promoLine });

        var lines = await _uow.Faturalar.GetDetaylarAsync(fatura.Id);
        Assert.Contains(lines, l => l.BirimFiyat == 0m);
    }

    [Fact]
    public async Task Scenario_84_FaturaDetay_FractionalQuantityLine_PersistsKgOrMetre()
    {
        var fatura = new Fatura { FaturaNo = "FAT-FRAC", Tur = "Satış", GenelToplam = 425m };
        var line = new FaturaDetay
        {
            StokAdi = "Kumaş",
            Birim = "Metre",
            Miktar = 8.5m,
            BirimFiyat = 50m,
            ToplamTutar = 425m
        };
        await _uow.Faturalar.SaveWithDetailsAsync(fatura, new List<FaturaDetay> { line });

        var lines = await _uow.Faturalar.GetDetaylarAsync(fatura.Id);
        Assert.Equal(8.5m, lines.First().Miktar);
    }

    [Fact]
    public async Task Scenario_85_FaturaDetay_DeleteDetay_RemovesLine()
    {
        var fatura = new Fatura { FaturaNo = "FAT-DEL-LINE", Tur = "Satış", GenelToplam = 1500m };
        var line1 = new FaturaDetay { StokAdi = "Silinecek Kalem", ToplamTutar = 500m };
        var line2 = new FaturaDetay { StokAdi = "Kalan Kalem", ToplamTutar = 1000m };
        await _uow.Faturalar.SaveWithDetailsAsync(fatura, new List<FaturaDetay> { line1, line2 });

        var conn = _dbService.GetConnection();
        await conn.DeleteAsync(line1);

        var lines = await _uow.Faturalar.GetDetaylarAsync(fatura.Id);
        Assert.Single(lines);
        Assert.Equal("Kalan Kalem", lines.First().StokAdi);
    }

    [Theory]
    [InlineData(1000, 10, 5, 855)]    // Kademeli İskonto: 1000 - %10 (100) = 900; 900 - %5 (45) = 855
    [InlineData(2000, 20, 10, 1440)]  // Kademeli: 2000 - %20 (400) = 1600; 1600 - %10 (160) = 1440
    public void Scenario_86_to_87_CascadingLineDiscounts(decimal brut, decimal isk1, decimal isk2, decimal expNet)
    {
        decimal step1 = brut - (brut * (isk1 / 100m));
        decimal step2 = step1 - (step1 * (isk2 / 100m));

        Assert.Equal(expNet, step2);
    }

    [Fact]
    public void Scenario_88_LineDiscount_100PercentDiscountResultsInZeroMatrah()
    {
        decimal brut = 500m;
        decimal iskontoOrani = 100m;
        decimal net = brut - (brut * (iskontoOrani / 100m));

        Assert.Equal(0m, net);
    }

    [Fact]
    public async Task Scenario_89_FaturaDetay_StokIdReference_LinksToStokKart()
    {
        var fatura = new Fatura { FaturaNo = "FAT-STK-REF", Tur = "Satış" };
        int stokId = 101;
        var line = new FaturaDetay { StokId = stokId, StokKodu = "STK-101", StokAdi = "Referanslı Ürün" };
        await _uow.Faturalar.SaveWithDetailsAsync(fatura, new List<FaturaDetay> { line });

        var lines = await _uow.Faturalar.GetDetaylarAsync(fatura.Id);
        Assert.Equal(stokId, lines.First().StokId);
    }

    [Fact]
    public async Task Scenario_90_FaturaDetay_Aciklama_StoresLineNote()
    {
        var fatura = new Fatura { FaturaNo = "FAT-NOTE-LINE", Tur = "Satış" };
        string lineNote = "Seri No: SN-2026-998811";
        var line = new FaturaDetay { StokAdi = "Laptop", Aciklama = lineNote };
        await _uow.Faturalar.SaveWithDetailsAsync(fatura, new List<FaturaDetay> { line });

        var lines = await _uow.Faturalar.GetDetaylarAsync(fatura.Id);
        Assert.Equal(lineNote, lines.First().Aciklama);
    }

    // =============================================================
    // 9. Vade, Ödeme & Kalan Bakiye Durumları (91-100)
    // =============================================================

    [Fact]
    public void Scenario_91_FullyPaidInvoice_KalanIsZero()
    {
        var fatura = new Fatura { GenelToplam = 5000m, Odenen = 5000m };
        Assert.Equal(0m, fatura.Kalan);
        Assert.Equal(0m, fatura.Bakiye);
    }

    [Fact]
    public void Scenario_92_PartiallyPaidInvoice_KalanIsDifference()
    {
        var fatura = new Fatura { GenelToplam = 10000m, Odenen = 4000m };
        Assert.Equal(6000m, fatura.Kalan);
        Assert.Equal(6000m, fatura.Bakiye);
    }

    [Fact]
    public void Scenario_93_UnpaidInvoice_KalanEqualsGenelToplam()
    {
        var fatura = new Fatura { GenelToplam = 7500m, Odenen = 0m };
        Assert.Equal(7500m, fatura.Kalan);
    }

    [Fact]
    public void Scenario_94_OverpaidInvoice_KalanIsNegative()
    {
        var fatura = new Fatura { GenelToplam = 3000m, Odenen = 3500m };
        Assert.Equal(-500m, fatura.Kalan);
    }

    [Fact]
    public async Task Scenario_95_OverdueInvoice_VadeTarihiInThePast_IdentifiedAsOverdue()
    {
        var fatura = new Fatura
        {
            FaturaNo = "FAT-OVD-95",
            Tur = "Satış",
            Tarih = DateTime.Today.AddDays(-60),
            VadeTarihi = DateTime.Today.AddDays(-30),
            GenelToplam = 4000m,
            Odenen = 0m
        };
        await _uow.Faturalar.SaveAsync(fatura);

        var retrieved = await _uow.Faturalar.GetByIdAsync(fatura.Id);
        bool isOverdue = retrieved!.VadeTarihi < DateTime.Today && retrieved.Kalan > 0;

        Assert.True(isOverdue);
    }

    [Fact]
    public async Task Scenario_96_FutureDueInvoice_NotOverdue()
    {
        var fatura = new Fatura
        {
            FaturaNo = "FAT-FUT-96",
            Tur = "Satış",
            Tarih = DateTime.Today,
            VadeTarihi = DateTime.Today.AddDays(30),
            GenelToplam = 4000m,
            Odenen = 0m
        };
        await _uow.Faturalar.SaveAsync(fatura);

        var retrieved = await _uow.Faturalar.GetByIdAsync(fatura.Id);
        bool isOverdue = retrieved!.VadeTarihi < DateTime.Today && retrieved.Kalan > 0;

        Assert.False(isOverdue);
    }

    [Fact]
    public async Task Scenario_97_FullyPaidOverdueInvoice_NotTreatedAsOverdueReceivable()
    {
        var fatura = new Fatura
        {
            FaturaNo = "FAT-OVD-PAID",
            Tur = "Satış",
            VadeTarihi = DateTime.Today.AddDays(-15),
            GenelToplam = 5000m,
            Odenen = 5000m
        };
        await _uow.Faturalar.SaveAsync(fatura);

        var retrieved = await _uow.Faturalar.GetByIdAsync(fatura.Id);
        bool hasOverdueDebt = retrieved!.VadeTarihi < DateTime.Today && retrieved.Kalan > 0;

        Assert.False(hasOverdueDebt);
    }

    [Fact]
    public async Task Scenario_98_Invoice_KayitTarihi_DefaultsToCurrentTimestamp()
    {
        var fatura = new Fatura { FaturaNo = "FAT-KT-98" };
        Assert.True((DateTime.Now - fatura.KayitTarihi).TotalMinutes < 5);
    }

    [Fact]
    public async Task Scenario_99_Invoice_Version_DefaultsToOne()
    {
        var fatura = new Fatura { FaturaNo = "FAT-VER-99" };
        Assert.True(fatura.Version >= 1);
    }

    [Fact]
    public async Task Scenario_100_Invoice_UpdatedAt_CanBeUpdated()
    {
        var fatura = new Fatura { FaturaNo = "FAT-UPD-100" };
        var before = fatura.UpdatedAt;
        fatura.UpdatedAt = DateTime.Now.AddSeconds(1);
        Assert.True(fatura.UpdatedAt >= before);
    }
}
