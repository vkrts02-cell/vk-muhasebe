using ErmayMuhasebe.Data;
using ErmayMuhasebe.Models;
using ErmayMuhasebe.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace ErmayMuhasebe.Tests.Services
{
    [Collection("DatabaseTests")]
    public class MobilGelenKutusuServiceTests : IDisposable
    {
        private readonly string _testDbPath;
        private readonly DatabaseService _dbService;
        private readonly YearContext _yearContext;

        public MobilGelenKutusuServiceTests()
        {
            _testDbPath = Path.Combine(Path.GetTempPath(), $"test_inbox_{Guid.NewGuid():N}.db");
            Constants.DatabasePath = _testDbPath;
            _yearContext = new YearContext { CurrentYear = 2026 };
            _dbService = new DatabaseService(_yearContext);
            _dbService.InitializeAsync().GetAwaiter().GetResult();
        }

        [Fact]
        public async Task ProcessTahsilat_DirectExecution_CreatesKasaAndUpdatesCari()
        {
            // 1. Arrange: Cari oluştur
            var cari = new CariKart
            {
                CariKod = "CAR-INBOX-01",
                Unvan = "Gelen Kutusu Test Müşterisi",
                Borc = 1000m,
                Alacak = 0m
            };
            await _dbService.SaveCariKartAsync(cari);

            // 2. Mock payload for Tahsilat
            var payload = new
            {
                cariId = cari.Id,
                amount = 400m,
                method = "Nakit",
                description = "Saha tahsilatı",
                date = DateTime.Now.ToString("yyyy-MM-dd")
            };

            var inboxItem = new MobilGelenKutusu
            {
                Id = Guid.NewGuid().ToString(),
                IslemTuru = "Tahsilat",
                MaliYil = 2026,
                Durum = "Bekliyor",
                Payload = JsonSerializer.Serialize(payload)
            };

            // Test execution through service reflection or direct invocation
            var inboxService = new MobilGelenKutusuService(_dbService, _dbService.SyncService, _yearContext);
            
            // Invoke ProcessItemAsync through reflection
            var methodInfo = typeof(MobilGelenKutusuService).GetMethod("ProcessItemAsync", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(methodInfo);

            var task = (Task<string>)methodInfo.Invoke(inboxService, new object[] { inboxItem })!;
            string officialNo = await task;

            // 3. Assert
            Assert.StartsWith("TS-2026-", officialNo);
            
            var updatedCari = await _dbService.GetCariKartAsync(cari.Id);
            Assert.NotNull(updatedCari);
            Assert.Equal(400m, updatedCari.Alacak);
            Assert.Equal(600m, updatedCari.Borc - updatedCari.Alacak);
        }

        [Fact]
        public async Task ProcessYeniCari_DirectExecution_CreatesCariInDatabase()
        {
            var payload = new
            {
                unvan = "Mobil Yeni Müşteri A.Ş.",
                telefon = "05551112233",
                email = "mobil@musteri.com",
                vergiNo = "1234567890",
                il = "Ankara",
                ilce = "Çankaya"
            };

            var inboxItem = new MobilGelenKutusu
            {
                Id = Guid.NewGuid().ToString(),
                IslemTuru = "YeniCari",
                MaliYil = 2026,
                Durum = "Bekliyor",
                Payload = JsonSerializer.Serialize(payload)
            };

            var inboxService = new MobilGelenKutusuService(_dbService, _dbService.SyncService, _yearContext);
            var methodInfo = typeof(MobilGelenKutusuService).GetMethod("ProcessItemAsync", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(methodInfo);

            var task = (Task<string>)methodInfo.Invoke(inboxService, new object[] { inboxItem })!;
            string officialCode = await task;

            Assert.NotEmpty(officialCode);
            var cariler = await _dbService.GetCarilerAsync();
            var created = cariler.Find(c => c.Unvan == "Mobil Yeni Müşteri A.Ş.");
            Assert.NotNull(created);
            Assert.Equal("05551112233", created.Telefon);
            Assert.Equal("Ankara", created.Il);
        }

        [Fact]
        public async Task ProcessTaslakFatura_DirectExecution_CreatesInvoiceAndDecreasesStock()
        {
            // 1. Arrange: Cari ve Stok oluştur
            var cari = new CariKart { CariKod = "CAR-INV-02", Unvan = "Fatura Test Cari" };
            await _dbService.SaveCariKartAsync(cari);

            var stok = new StokKart
            {
                StokKodu = "STK-INV-01",
                StokAdi = "Mobil Satış Ürünü",
                Miktar = 100m,
                SatisFiyati = 50m,
                KDV = 20
            };
            await _dbService.SaveStokKartAsync(stok);
            await _dbService.SaveStokHareketAsync(new StokHareket { StokId = stok.Id, Giren = 100m, IslemTuru = "Devir Girişi" });

            var payload = new
            {
                cariId = cari.Id,
                tur = "Satış",
                tarih = DateTime.Now.ToString("yyyy-MM-dd"),
                details = new[]
                {
                    new { stokId = stok.Id, miktar = 10m, birimFiyat = 50m, kdvOrani = 20 }
                }
            };

            var inboxItem = new MobilGelenKutusu
            {
                Id = Guid.NewGuid().ToString(),
                IslemTuru = "TaslakFatura",
                MaliYil = 2026,
                Durum = "Bekliyor",
                Payload = JsonSerializer.Serialize(payload)
            };

            var inboxService = new MobilGelenKutusuService(_dbService, _dbService.SyncService, _yearContext);
            var methodInfo = typeof(MobilGelenKutusuService).GetMethod("ProcessItemAsync", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(methodInfo);

            var task = (Task<string>)methodInfo.Invoke(inboxService, new object[] { inboxItem })!;
            string fNo = await task;

            Assert.NotEmpty(fNo);
            var faturalar = await _dbService.GetFaturalarAsync();
            var createdInvoice = faturalar.Find(f => f.FaturaNo == fNo);
            Assert.NotNull(createdInvoice);
            Assert.Equal(600m, createdInvoice.GenelToplam); // 10 * 50 = 500 + %20 KDV = 600

            // Stok miktarının düştüğünü teyit et
            var updatedStok = await _dbService.GetStokKartAsync(stok.Id);
            Assert.NotNull(updatedStok);
            Assert.Equal(90m, updatedStok.Miktar);
        }

        public void Dispose()
        {
            try { if (File.Exists(_testDbPath)) File.Delete(_testDbPath); } catch { }
        }
    }
}
