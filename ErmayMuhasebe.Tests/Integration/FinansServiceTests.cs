using System;
using System.IO;
using System.Threading.Tasks;
using ErmayMuhasebe.Models;
using ErmayMuhasebe.Services;
using ErmayMuhasebe.Repositories;
using Xunit;
using System.Linq;

namespace ErmayMuhasebe.Tests.Integration
{
    [Collection("DatabaseTests")]
    public class FinansServiceTests : IDisposable
    {
        private readonly DatabaseService _dbService;
        private readonly string _testDbPath;
        private readonly IUnitOfWork _uow;
        private readonly FinansService _finansService;

        public FinansServiceTests()
        {
            _testDbPath = Path.Combine(Path.GetTempPath(), $"FinansServiceTests_{Guid.NewGuid()}.db3");
            ErmayMuhasebe.Data.Constants.DatabasePath = _testDbPath;
            if (!OperatingSystem.IsBrowser()) SQLitePCL.Batteries_V2.Init();
            
            CloudSyncService.DisableCloudSync = true;
            _dbService = new DatabaseService { IsTestMode = true, DisableCloudSync = true, UseEncryption = false };
            _dbService.InitializeAsync().Wait();

            var dataProvider = new ErmayMuhasebe.Repositories.DataProviders.SqliteDataProvider(_dbService);
            _uow = dataProvider;
            _finansService = new FinansService(_dbService, _uow, _dbService.SyncService);
        }

        [Fact]
        public async Task CashPayment_EndorsedToSupplier_ShouldExecuteCorrectly()
        {
            // 1. Hazırlık
            var customer = new CariKart { Unvan = "TEST MÜŞTERİ", Tur = "Alici" };
            var supplier = new CariKart { Unvan = "TEST TEDARİKÇİ", Tur = "Satici" };
            var kasa = new BankaKart { BankaAdi = "MERKEZ KASA", KartTuru = "Kasa", GuncelBakiye = 10000 };
            
            await _dbService.SaveCariKartAsync(customer);
            await _dbService.SaveCariKartAsync(supplier);
            await _dbService.SaveBankaKartAsync(kasa);

            // 2. İşlem: Müşteriden 2500 TL Nakit Tahsilat Al ve Tedarikçiye Ciro Et
            var request = new FinancialTransactionRequest
            {
                Cari = customer,
                TransactionType = "Tahsilat",
                Amount = 2500,
                Method = "Nakit",
                Description = "Müşteri nakit ödemesi ciro edildi",
                Date = DateTime.Now,
                SelectedKasaOrBanka = kasa,
                DirectedSupplier = supplier,
                YonlendirmeTarihi = DateTime.Now
            };

            var saveResult = await _finansService.SaveTransactionAsync(request);
            Assert.True(saveResult);

            // 3. Doğrulama: Bakiyeleri ve Hareketleri Oku
            var dbCustomer = await _dbService.GetCariKartAsync(customer.Id);
            var dbSupplier = await _dbService.GetCariKartAsync(supplier.Id);
            var dbKasa = await _dbService.GetBankaAsync(kasa.Id);

            // Müşteri bakiye değişimi
            Assert.Equal(2500, dbCustomer.Alacak);
            Assert.Equal(0, dbCustomer.Borc);

            // Tedarikçi bakiye değişimi
            Assert.Equal(2500, dbSupplier.Borc);
            Assert.Equal(0, dbSupplier.Alacak);

            // Kasa bakiyesi değişmemeli (giriş ve çıkış birbirini götürdü)
            Assert.Equal(10000, dbKasa.GuncelBakiye);

            // Hareketleri doğrula
            var customerMoves = await _dbService.GetCariHareketleriAsync(customer.Id);
            var supplierMoves = await _dbService.GetCariHareketleriAsync(supplier.Id);
            var kasaMoves = await _dbService.GetKasaHareketleriAsync(kasa.Id);

            // Müşteri hareketi kontrolü
            Assert.Single(customerMoves);
            var customerMove = customerMoves.First();
            Assert.Equal(2500, customerMove.Alacak);
            Assert.Equal(supplier.Id, customerMove.YonlendirilenCariId);
            Assert.NotNull(customerMove.RefId);

            // Tedarikçi hareketi kontrolü
            Assert.Single(supplierMoves);
            var supplierMove = supplierMoves.First();
            Assert.Equal(2500, supplierMove.Borc);
            Assert.Equal(customerMove.RefId + "-SUP", supplierMove.RefId);

            // Kasa hareketleri kontrolü (Hem giriş hem çıkış olmak üzere 2 hareket olmalı)
            Assert.Equal(2, kasaMoves.Count);
            Assert.Contains(kasaMoves, k => k.Giren == 2500 && k.RefId == customerMove.RefId);
            Assert.Contains(kasaMoves, k => k.Cikan == 2500 && k.RefId == customerMove.RefId + "-SUP");

            // 4. Test: İşlemi Sil ve Geri Almayı Doğrula
            var deleteResult = await _finansService.DeleteTransactionAsync(customerMove);
            Assert.True(deleteResult);

            var postDeleteCustomer = await _dbService.GetCariKartAsync(customer.Id);
            var postDeleteSupplier = await _dbService.GetCariKartAsync(supplier.Id);
            var postDeleteKasa = await _dbService.GetBankaAsync(kasa.Id);

            // Bakiyeler sıfırlanmalı
            Assert.Equal(0, postDeleteCustomer.Alacak);
            Assert.Equal(0, postDeleteSupplier.Borc);
            Assert.Equal(10000, postDeleteKasa.GuncelBakiye);

            // Hareketler silinmeli
            var postDeleteCustomerMoves = await _dbService.GetCariHareketleriAsync(customer.Id);
            var postDeleteSupplierMoves = await _dbService.GetCariHareketleriAsync(supplier.Id);
            var postDeleteKasaMoves = await _dbService.GetKasaHareketleriAsync(kasa.Id);

            Assert.Empty(postDeleteCustomerMoves);
            Assert.Empty(postDeleteSupplierMoves);
            Assert.Empty(postDeleteKasaMoves);
        }

        public void Dispose()
        {
            try { if (File.Exists(_testDbPath)) File.Delete(_testDbPath); } catch { }
        }
    }
}
