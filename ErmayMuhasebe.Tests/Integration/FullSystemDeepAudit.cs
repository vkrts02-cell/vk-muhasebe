using ErmayMuhasebe.Models;
using ErmayMuhasebe.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ErmayMuhasebe.Tests.Integration
{
    [Collection("DatabaseTests")]
    public class FullSystemDeepAudit : IDisposable
    {
        private readonly DatabaseService _dbService;
        private readonly string _testDbPath;
        private readonly PdfService _pdfService;
        private readonly ExcelService _excelService;

        public FullSystemDeepAudit()
        {
            _testDbPath = Path.Combine(Path.GetTempPath(), $"DeepAudit_{Guid.NewGuid()}.db3");
            ErmayMuhasebe.Data.Constants.DatabasePath = _testDbPath;
            
            // Test ortamında SQLite kütüphanesini başlat
            if (!OperatingSystem.IsBrowser())
            {
                SQLitePCL.Batteries_V2.Init();
            }

            _dbService = new DatabaseService();
            _dbService.UseEncryption = false; // Test ortamında şifrelemeyi kapat
            _dbService.InitializeAsync().Wait();
            
            _pdfService = new PdfService();
            _excelService = new ExcelService();
        }

        [Fact]
        public async Task DeepAudit_AllModulesAndFeatures_ShouldSucceed()
        {
            // --- 1. CARİ VE STOK MODÜLÜ ---
            var supplier = new CariKart { Unvan = "ANA TEDARİKÇİ", Tur = "Satici", CariKod = "T-001" };
            var customer = new CariKart { Unvan = "ANA MÜŞTERİ", Tur = "Alici", CariKod = "M-001" };
            await _dbService.SaveCariKartAsync(supplier);
            await _dbService.SaveCariKartAsync(customer);

            var item = new StokKart { StokAdi = "AUDIT PRODUCT", StokKodu = "STK-AUD", AlisFiyati = 100, SatisFiyati = 200, KDV = 20 };
            await _dbService.SaveStokKartAsync(item);

            // --- 2. FATURA VE HAREKETLER ---
            var inv = new Fatura { CariId = customer.Id, CariUnvan = customer.Unvan, Tur = "Satış", FaturaNo = "INV-001", Tarih = DateTime.Now, GenelToplam = 240 };
            var details = new List<FaturaDetay> { new FaturaDetay { StokId = item.Id, Miktar = 1, BirimFiyat = 200, KDVOrani = 20, ToplamTutar = 240 } };
            await _dbService.SaveFaturaWithTransactionAsync(inv, details, customer);

            // --- 3. FİNANS MODÜLÜ (KASA/BANKA/EFT/KK) ---
            var bank = new BankaKart { BankaAdi = "TEST BANKASI", HesapNo = "12345", IBAN = "TR00...", KartTuru = "Vadesiz", AcilisBakiyesi = 5000 };
            await _dbService.SaveBankaKartAsync(bank);

            // Tahsilat (Banka)
            var bankMove = new BankaHareket { BankaId = bank.Id, CariId = customer.Id, CariUnvan = customer.Unvan, Giren = 100, IslemTuru = "Tahsilat", Tarih = DateTime.Now, EvrakNo = "BNK-001" };
            await _dbService.SaveBankaHareketAsync(bankMove);

            // EFT İşlemi
            var eft = new EftIslem { MusteriId = customer.Id, MusteriUnvan = customer.Unvan, Tutar = 50, Banka = "TEST BANKASI", IslemTuru = "Tahsilat", Tarih = DateTime.Now };
            await _dbService.SaveEftIslemAsync(eft);

            // Kredi Kartı İşlemi
            var kk = new KrediKartiIslem { MusteriId = customer.Id, MusteriUnvan = customer.Unvan, Tutar = 40, Banka = "BONUS", KartNo = "4444****", IslemTuru = "Tahsilat", Tarih = DateTime.Now };
            await _dbService.SaveKrediKartiIslemAsync(kk);

            // --- 4. ÇEK / SENET MODÜLÜ ---
            var cek = new Cek { CariId = customer.Id, Tutar = 500, Banka = "AKBANK", VadeTarihi = DateTime.Now.AddMonths(1), Durum = "Portföyde", CekTuru = "Alınan" };
            await _dbService.SaveCekWithTransactionAsync(cek);

            // --- 5. DASHBOARD VE GRAFİKLER ---
            await _dbService.RecalculateSystemBalancesAsync();
            
            var stats = await _dbService.GetDashboardStatsAsync();
            Assert.NotNull(stats);

            var dbCustomer = await _dbService.GetCariKartAsync(customer.Id);
            System.Diagnostics.Debug.WriteLine($"[AUDIT] Customer Balance: Borc={dbCustomer.Borc}, Alacak={dbCustomer.Alacak}, Net={dbCustomer.Borc - dbCustomer.Alacak}");

            // Satış faturası 240 TL, Tahsilatlar (100 Banka + 50 EFT + 40 KK = 190)
            // Çek (Alınan) 500 TL -> Toplam Tahsilat = 690 TL.
            // Cari Bakiye: 240 - 690 = -450 TL (Cari Alacaklı).
            Assert.True(dbCustomer.Alacak == 690, $"Expected 690 Alacak but got {dbCustomer.Alacak}");
            Assert.True(stats.ToplamBorc == 450, $"Expected 450 ToplamBorc but got {stats.ToplamBorc}");
            Assert.Equal(240, stats.AylikCiro);

            var chartData = await _dbService.GetMonthlyIncomeExpenseAsync(6);
            Assert.NotEmpty(chartData);

            // --- 6. VADE TAKİP VE RİSK ANALİZİ ---
            var risky = await _dbService.GetRiskyCarisAsync(10);
            Assert.Empty(risky); // Müşteri şu an alacaklı olduğu için riskli (borçlu) listesinde olmamalı
            
            var payable = await _dbService.GetPayableCarisAsync(10);
            Assert.Contains(payable, c => c.CariId == customer.Id);

            // --- 7. PERSONEL VE GÖREV MODÜLÜ ---
            var staff = new Personel { Ad = "Test", Soyad = "Audit", Unvan = "Tester" };
            await _dbService.SavePersonelAsync(staff);
            
            var task = new Gorev { Baslik = "Sistemi Denetle", AtananPersonelId = staff.Id, Durum = "Bekliyor" };
            await _dbService.SaveGorevAsync(task);

            // --- 8. RAPORLAMA (PDF/EXCEL) AUDIT ---
            var pdfInv = await _pdfService.GenerateFaturaPdfBytesAsync(inv, details);
            Assert.NotEmpty(pdfInv);

            var pdfCari = await _pdfService.GenerateCariEkstrePdfBytesAsync(customer, await _dbService.GetCariHareketleriAsync(customer.Id));
            Assert.NotEmpty(pdfCari);

            var excelCari = await _excelService.ExportStyledListToMemoryAsync(await _dbService.GetCarilerAsync(), "Cari Liste");
            Assert.NotEmpty(excelCari);

            // --- 9. AYARLAR VE SİSTEM ---
            var profil = await _dbService.GetFirmaProfiliAsync();
            profil.FirmaAdi = "AUDIT CORP";
            await _dbService.SaveFirmaProfiliAsync(profil);

            // --- 10. REVERSAL / DELETE AUDIT ---
            await _dbService.SoftDeleteFaturaAsync(inv);
            var statsAfterDelete = await _dbService.GetDashboardStatsAsync();
            Assert.Equal(0, statsAfterDelete.AylikCiro); // Fatura silindiği için ciro sıfırlanmalı
        }

        public void Dispose()
        {
            try { if (File.Exists(_testDbPath)) File.Delete(_testDbPath); } catch { }
        }
    }
}
