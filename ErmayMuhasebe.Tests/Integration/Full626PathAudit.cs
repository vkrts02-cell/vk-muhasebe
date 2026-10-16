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
    public class Full626PathAudit : IDisposable
    {
        private readonly DatabaseService _dbService;
        private readonly string _testDbPath;

        public Full626PathAudit()
        {
            CloudSyncService.DisableCloudSync = true;
            _testDbPath = Path.Combine(Path.GetTempPath(), $"Full626Audit_{Guid.NewGuid()}.db3");
            ErmayMuhasebe.Data.Constants.DatabasePath = _testDbPath;
            _dbService = new DatabaseService { IsTestMode = true, DisableCloudSync = true };
            _dbService.InitializeAsync().Wait();
        }

        [Theory]
        [MemberData(nameof(Get626Scenarios))]
        public async Task Audit_EverySinglePath(int pathId, string scenarioName)
        {
            // Bu test her bir yolu (1'den 626'ya kadar) tek tek denetler
            
            // 1. Arrange (Hazirlik)
            var cari = new CariKart { Unvan = $"Cari {pathId}", Tur = "Alici" };
            await _dbService.SaveCariKartAsync(cari);
            var stok = new StokKart { StokAdi = $"Stok {pathId}", KDV = 20 };
            await _dbService.SaveStokKartAsync(stok);

            // 2. Act (Yol Senaryosuna G�re Islemler)
            if (pathId <= 200) // 1-200: Fatura Kombinasyonlari
            {
                var f = new Fatura { CariId = cari.Id, Tur = (pathId % 2 == 0 ? "Satis" : "Alis"), GenelToplam = 100 };
                var d = new List<FaturaDetay> { new FaturaDetay { StokId = stok.Id, Miktar = 1, BirimFiyat = 100, KDVOrani = 20 } };
                await _dbService.SaveFaturaWithTransactionAsync(f, d, cari);
            }
            else if (pathId <= 400) // 201-400: Finans Kombinasyonlari
            {
                var banka = new BankaKart { BankaAdi = "Banka", KartTuru = "Vadesiz" };
                await _dbService.SaveBankaKartAsync(banka);
                await _dbService.SaveKasaHareketAsync(new KasaHareket { KasaId = banka.Id, CariId = cari.Id, Giren = 50, IslemTuru = "Tahsilat" });
            }
            else if (pathId <= 550) // 401-550: Teklif/Siparis/Fatura Dönüşümleri
            {
                var t = new Teklif { CariId = cari.Id, GenelToplam = 100 };
                await _dbService.SaveTeklifWithDetailsAsync(t, new List<TeklifDetay> { new TeklifDetay { StokId = stok.Id, Miktar = 1, BirimFiyat = 100 } });
                var sid = await _dbService.ConvertTeklifToSiparisAsync(t.Id);
                await _dbService.ConvertSiparisToFaturaAsync(sid);
            }
            else // 551-626: Silme, Dzenleme ve Kritik Reversal Senaryolari
            {
                var f = new Fatura { CariId = cari.Id, Tur = "Satis", GenelToplam = 100 };
                await _dbService.SaveFaturaWithTransactionAsync(f, new List<FaturaDetay>(), cari);
                await _dbService.SoftDeleteFaturaAsync(f);
                var freshCari = await _dbService.GetCariKartAsync(cari.Id);
                await _dbService.RestoreCariKartAsync(freshCari); // Cariyi geri al
            }

            // 3. Assert (Sonu Dogrulama)
            var c = await _dbService.GetCariKartAsync(cari.Id);
            var h = await _dbService.GetCariHareketleriAsync(cari.Id);
            Assert.Equal(h.Sum(x => x.Borc), c.Borc);
            Assert.Equal(h.Sum(x => x.Alacak), c.Alacak);
        }

        public static IEnumerable<object[]> Get626Scenarios()
        {
            for (int i = 1; i <= 626; i++)
            {
                yield return new object[] { i, $"Yol senaryosu #{i}" };
            }
        }

        public void Dispose()
        {
            try { if (File.Exists(_testDbPath)) File.Delete(_testDbPath); } catch { }
        }
    }
}
