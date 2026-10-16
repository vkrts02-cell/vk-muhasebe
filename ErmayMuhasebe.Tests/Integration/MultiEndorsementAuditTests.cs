using ErmayMuhasebe.Models;
using ErmayMuhasebe.Services;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace ErmayMuhasebe.Tests.Integration
{
    [Collection("DatabaseTests")]
    public class MultiEndorsementAuditTests : IDisposable
    {
        private readonly DatabaseService _dbService;
        private readonly string _testDbPath;

        public MultiEndorsementAuditTests()
        {
            _testDbPath = Path.Combine(Path.GetTempPath(), $"MultiEndorsement_{Guid.NewGuid()}.db3");
            ErmayMuhasebe.Data.Constants.DatabasePath = _testDbPath;
            if (!OperatingSystem.IsBrowser()) SQLitePCL.Batteries_V2.Init();
            CloudSyncService.DisableCloudSync = true;
            _dbService = new DatabaseService { IsTestMode = true, DisableCloudSync = true, UseEncryption = false };
            _dbService.InitializeAsync().Wait();
        }

        [Fact]
        public async Task AllPaymentTypes_Endorsement_ShouldSucceed()
        {
            var customer = new CariKart { Unvan = "HİPER MÜŞTERİ", Tur = "Alici" };
            var supplier = new CariKart { Unvan = "DEV TEDARİKÇİ", Tur = "Satici" };
            await _dbService.SaveCariKartAsync(customer);
            await _dbService.SaveCariKartAsync(supplier);

            // --- 1. KREDİ KARTI YÖNLENDİRME ---
            var kk = new KrediKartiIslem 
            { 
                MusteriId = customer.Id, 
                MusteriUnvan = customer.Unvan,
                Tutar = 1000, 
                IslemTuru = "Tahsilat",
                YonlendirilenCariId = supplier.Id,
                YonlendirilenCariUnvan = supplier.Unvan,
                Tarih = DateTime.Now
            };
            await _dbService.SaveKrediKartiIslemAsync(kk);

            // --- 2. EFT/HAVALE YÖNLENDİRME ---
            var eft = new EftIslem 
            { 
                MusteriId = customer.Id, 
                MusteriUnvan = customer.Unvan,
                Tutar = 2000, 
                IslemTuru = "Tahsilat",
                YonlendirilenCariId = supplier.Id,
                YonlendirilenCariUnvan = supplier.Unvan,
                Tarih = DateTime.Now
            };
            await _dbService.SaveEftIslemAsync(eft);

            // --- 3. NAKİT (DOLAYLI YÖNLENDİRME) ---
            // Nakit işlemlerde sistem tahsilat ve ödemeyi ayrı işler
            var kasa = new BankaKart { BankaAdi = "MERKEZ KASA", KartTuru = "Vadesiz" }; // Kasa da bir banka kartı gibi tutulabiliyor
            await _dbService.SaveBankaKartAsync(kasa);

            var nakitTahsilat = new BankaHareket 
            { 
                BankaId = kasa.Id, 
                CariId = customer.Id, 
                Giren = 3000, 
                IslemTuru = "Tahsilat",
                EvrakNo = "NK-001"
            };
            await _dbService.SaveBankaHareketAsync(nakitTahsilat);

            var nakitOdeme = new BankaHareket 
            { 
                BankaId = kasa.Id, 
                CariId = supplier.Id, 
                Cikan = 3000, 
                IslemTuru = "Ödeme",
                EvrakNo = "NK-001-OUT"
            };
            await _dbService.SaveBankaHareketAsync(nakitOdeme);

            // --- DOĞRULAMA ---
            var dbCust = await _dbService.GetCariKartAsync(customer.Id);
            var dbSup = await _dbService.GetCariKartAsync(supplier.Id);

            // Müşteri Toplam Alacak: 1000 (KK) + 2000 (EFT) + 3000 (Nakit) = 6000
            Assert.Equal(6000, dbCust.Alacak);

            // Tedarikçi Toplam Borç: 1000 (KK) + 2000 (EFT) + 3000 (Nakit) = 6000
            Assert.Equal(6000, dbSup.Borc);
            
            System.Diagnostics.Debug.WriteLine($"[MULTI-ENDORSEMENT OK] Total Flow: 6000 TL Balanced.");
        }

        public void Dispose()
        {
            try { if (File.Exists(_testDbPath)) File.Delete(_testDbPath); } catch { }
        }
    }
}
