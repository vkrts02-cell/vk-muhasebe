using ErmayMuhasebe.Models;
using ErmayMuhasebe.Services;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace ErmayMuhasebe.Tests.Integration
{
    [Collection("DatabaseTests")]
    public class EndorsementAuditTests : IDisposable
    {
        private readonly DatabaseService _dbService;
        private readonly string _testDbPath;

        public EndorsementAuditTests()
        {
            _testDbPath = Path.Combine(Path.GetTempPath(), $"EndorsementAudit_{Guid.NewGuid()}.db3");
            ErmayMuhasebe.Data.Constants.DatabasePath = _testDbPath;
            if (!OperatingSystem.IsBrowser()) SQLitePCL.Batteries_V2.Init();
            CloudSyncService.DisableCloudSync = true;
            _dbService = new DatabaseService { IsTestMode = true, DisableCloudSync = true, UseEncryption = false };
            _dbService.InitializeAsync().Wait();
        }

        [Fact]
        public async Task CustomerPayment_EndorsedToSupplier_ShouldUpdateBothBalancesCorrectly()
        {
            // 1. Hazırlık: Müşteri ve Tedarikçi oluştur
            var customer = new CariKart { Unvan = "ÖDEME YAPAN MÜŞTERİ", Tur = "Alici" };
            var supplier = new CariKart { Unvan = "ÖDEME ALAN TEDARİKÇİ", Tur = "Satici" };
            await _dbService.SaveCariKartAsync(customer);
            await _dbService.SaveCariKartAsync(supplier);

            // 2. İşlem: Müşteriden Çek Al ve Tedarikçiye Yönlendir
            var cek = new Cek 
            { 
                CariId = customer.Id, 
                CariUnvan = customer.Unvan,
                Tutar = 5000, 
                Banka = "İŞ BANKASI", 
                CekNo = "CK-123",
                VadeTarihi = DateTime.Now.AddMonths(1),
                CekTuru = "Alınan", // Müşteriden geldi
                Durum = "Yönlendirildi", // Ciro edildi
                YonlendirilenCariId = supplier.Id // TEDARİKÇİYE YÖNLENDİR
            };

            // DatabaseService içindeki yönlendirme mantığını içeren metodu çağır
            await _dbService.SaveCekWithTransactionAsync(cek);

            // 3. Doğrulama: Bakiyeleri kontrol et
            var dbCustomer = await _dbService.GetCariKartAsync(customer.Id);
            var dbSupplier = await _dbService.GetCariKartAsync(supplier.Id);

            // Müşteri 5000 TL ödediği için Alacaklı olmalı (veya borcu düşmeli)
            Assert.Equal(5000, dbCustomer.Alacak);
            
            // Tedarikçiye 5000 TL verdiğimiz için bizden alacağı düşmeli (Borcu artmalı)
            Assert.Equal(5000, dbSupplier.Borc);

            // Ekstreleri (Hareketleri) Kontrol Et
            var customerMoves = await _dbService.GetCariHareketleriAsync(customer.Id);
            var supplierMoves = await _dbService.GetCariHareketleriAsync(supplier.Id);

            Assert.Contains(customerMoves, m => m.Alacak == 5000 && m.IslemTuru.Contains("Çek"));
            Assert.Contains(supplierMoves, m => m.Borc == 5000 && m.IslemTuru.Contains("Çek"));
            
            System.Diagnostics.Debug.WriteLine($"[ENDORSEMENT OK] Müşteri Alacak: {dbCustomer.Alacak}, Tedarikçi Borç: {dbSupplier.Borc}");
        }

        public void Dispose()
        {
            try { if (File.Exists(_testDbPath)) File.Delete(_testDbPath); } catch { }
        }
    }
}
