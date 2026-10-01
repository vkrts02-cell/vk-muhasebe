using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ErmayMuhasebe.Models;
using ErmayMuhasebe.Services;

namespace ErmayMuhasebe.Repositories.Firebase;

public class FirebaseCariRepository : BaseFirebaseRepository<CariKart>, ICariRepository
{
    public FirebaseCariRepository(IFirebaseService firebaseService) : base(firebaseService)
    {
    }

    protected override string ResourceName => "Cariler";

    public async Task<List<CariKart>> GetByTurAsync(string tur)
    {
        var all = await GetAllAsync();
        return all.Where(c => c.Tur == tur).ToList();
    }

    public async Task<List<CariKart>> GetHareketsizCarilerAsync(int gunSayisi = 180)
    {
        var all = await GetAllAsync();
        var hareketler = await GetAllHareketlerAsync();
        var cutoffDate = DateTime.Now.AddDays(-gunSayisi);
        var result = new List<CariKart>();

        foreach (var c in all)
        {
            var lastTransaction = hareketler
                .Where(h => h.CariId == c.Id)
                .OrderByDescending(h => h.Tarih)
                .FirstOrDefault();

            var compareDate = lastTransaction != null
                ? lastTransaction.Tarih
                : (c.KayitTarihi.Year > 2000 ? c.KayitTarihi : DateTime.MinValue);

            if (compareDate < cutoffDate)
            {
                result.Add(c);
            }
        }

        return result;
    }

    public async Task<List<CariHareket>> GetHareketlerAsync(int cariId)
    {
        var all = await _firebaseService.GetAllAsync<CariHareket>("CariHareketler");
        return all.Where(h => h.CariId == cariId).OrderByDescending(h => h.Tarih).ToList();
    }

    public async Task<int> SaveHareketAsync(CariHareket hareket)
    {
        if (hareket.Id == 0)
        {
            var all = await _firebaseService.GetAllAsync<CariHareket>("CariHareketler");
            hareket.Id = all.Any() ? all.Max(h => h.Id) + 1 : 1;
        }
        await _firebaseService.SaveAsync("CariHareketler", hareket, hareket.Id);
        return hareket.Id;
    }

    public async Task<int> DeleteHareketAsync(CariHareket hareket)
    {
        if (hareket == null) return 0;

        try
        {
            // Cascade Delete: Find and remove linked financial records (Kasa, Banka, KK, EFT)
            var startDay = hareket.Tarih.Date;
            var endDay = startDay.AddDays(1);
            var amount = hareket.Borc + hareket.Alacak;

            // 1. Kasa Hareketleri
            var kasalar = await _firebaseService.GetAllAsync<KasaHareket>("KasaHareketler");
            var matchKasalar = kasalar.Where(k => 
                k.Tarih.Date == startDay &&
                ((!string.IsNullOrEmpty(hareket.EvrakNo) && k.EvrakNo == hareket.EvrakNo) || 
                 (Math.Abs((k.Giren + k.Cikan) - amount) < 0.05m && k.CariUnvan == hareket.CariUnvan))
            ).ToList();

            foreach (var k in matchKasalar)
            {
                var bankalar = await _firebaseService.GetAllAsync<BankaKart>("Bankalar");
                var b = bankalar.FirstOrDefault(x => x.Id == k.KasaId);
                if (b != null)
                {
                    b.GuncelBakiye -= (k.Giren - k.Cikan);
                    await _firebaseService.SaveAsync("Bankalar", b, b.Id);
                }
                await _firebaseService.DeleteAsync("KasaHareketler", k.Id);
            }

            // 2. Banka Hareketleri
            var bankaHarekets = await _firebaseService.GetAllAsync<BankaHareket>("BankaHareketler");
            var matchBankas = bankaHarekets.Where(b => 
                b.Tarih.Date == startDay &&
                ((!string.IsNullOrEmpty(hareket.EvrakNo) && b.EvrakNo == hareket.EvrakNo) || 
                 (Math.Abs((b.Giren + b.Cikan) - amount) < 0.05m && b.CariUnvan == hareket.CariUnvan))
            ).ToList();

            foreach (var b in matchBankas)
            {
                var bankalar = await _firebaseService.GetAllAsync<BankaKart>("Bankalar");
                var card = bankalar.FirstOrDefault(x => x.Id == b.BankaId);
                if (card != null)
                {
                    card.GuncelBakiye -= (b.Giren - b.Cikan);
                    await _firebaseService.SaveAsync("Bankalar", card, card.Id);
                }
                await _firebaseService.DeleteAsync("BankaHareketler", b.Id);
            }

            // 3. Kredi Kartı İşlemleri
            var kkIslemler = await _firebaseService.GetAllAsync<KrediKartiIslem>("KrediKartiIslemleri");
            var matchKks = kkIslemler.Where(k => 
                k.MusteriId == hareket.CariId && k.Tarih.Date == startDay &&
                ((!string.IsNullOrEmpty(hareket.EvrakNo) && (k.OnayKodu == hareket.EvrakNo || hareket.EvrakNo.Contains(k.Id.ToString()))) || 
                 Math.Abs(k.Tutar - amount) < 0.05m)
            ).ToList();
            foreach (var kk in matchKks) await _firebaseService.DeleteAsync("KrediKartiIslemleri", kk.Id);

            // 4. EFT/Havale İşlemleri
            var eftIslemler = await _firebaseService.GetAllAsync<EftIslem>("EftIslemleri");
            var matchEfts = eftIslemler.Where(e => 
                e.MusteriId == hareket.CariId && e.Tarih.Date == startDay &&
                ((!string.IsNullOrEmpty(hareket.EvrakNo) && (e.DekontNo == hareket.EvrakNo || hareket.EvrakNo.Contains(e.Id.ToString()))) || 
                 Math.Abs(e.Tutar - amount) < 0.05m)
            ).ToList();
            foreach (var eft in matchEfts) await _firebaseService.DeleteAsync("EftIslemleri", eft.Id);

            // 5. ÇEK / SENET İşlemleri
            if (!string.IsNullOrEmpty(hareket.EvrakNo))
            {
                var cekler = await _firebaseService.GetAllAsync<Cek>("Cekler");
                var matchCeks = cekler.Where(c => c.PortfoyNo == hareket.EvrakNo && c.CariId == hareket.CariId).ToList();
                foreach (var c in matchCeks) await _firebaseService.DeleteAsync("Cekler", c.Id);

                var senetler = await _firebaseService.GetAllAsync<Senet>("Senetler");
                var matchSenets = senetler.Where(s => s.PortfoyNo == hareket.EvrakNo && s.CariId == hareket.CariId).ToList();
                foreach (var s in matchSenets) await _firebaseService.DeleteAsync("Senetler", s.Id);
            }

        }
        catch (Exception ex)
        {
            Console.WriteLine($"Cascade Delete Error (Firebase): {ex.Message}");
        }

        await _firebaseService.DeleteAsync("CariHareketler", hareket.Id);
        
        // Recalculate Cari Balance
        await RecalculateBalanceAsync(hareket.CariId);
        
        return 1;
    }

    public async Task<int> DeleteHareketByEvrakNoAsync(string evrakNo)
    {
        if (string.IsNullOrEmpty(evrakNo)) return 0;
        var h = await GetHareketByEvrakNoAsync(evrakNo);
        if (h != null)
        {
            await DeleteHareketAsync(h);
            return 1;
        }
        return 0;
    }

    public async Task<CariHareket?> GetHareketByEvrakNoAsync(string evrakNo)
    {
        if (string.IsNullOrEmpty(evrakNo)) return null;
        var all = await GetAllHareketlerAsync();
        return all.FirstOrDefault(x => x.EvrakNo == evrakNo);
    }

    public async Task<int> RecalculateBalanceAsync(int cariId)
    {
        try
        {
            var cari = await GetByIdAsync(cariId);
            if (cari == null) return 0;

            var hareketler = await GetHareketlerAsync(cariId);
            cari.Borc = hareketler.Sum(h => h.Borc);
            cari.Alacak = hareketler.Sum(h => h.Alacak);
            
            await SaveAsync(cari);

            // Trigger FIFO matching
            await MatchInvoicePaymentsAsync(cariId);
            
            return 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"RecalculateBalanceAsync Error: {ex.Message}");
            return 0;
        }
    }

    public async Task MatchInvoicePaymentsAsync(int cariId)
    {
        try
        {
            // 1. Faturaları Al (FIFO için tarihe göre sıralı)
            var allFaturalar = await _firebaseService.GetAllAsync<Fatura>("Faturalar");
            var faturalar = allFaturalar.Where(f => f.CariId == cariId && !f.IsDeleted).OrderBy(f => f.Tarih).ToList();

            // 2. Hareketleri al
            var hareketler = await GetHareketlerAsync(cariId);

            // Toplam Tahsilat ve Ödeme kapasitesi
            decimal totalCollection = hareketler.Where(h => (h.IslemTuru == "Tahsilat" || h.IslemTuru == "Alacak Dekontu" || h.IslemTuru == "Açılış" || h.IslemTuru == "İade") && h.Alacak > 0).Sum(h => h.Alacak);
            decimal totalPayment = hareketler.Where(h => (h.IslemTuru == "Ödeme" || h.IslemTuru == "Borç Dekontu" || h.IslemTuru == "Açılış" || h.IslemTuru == "İade") && h.Borc > 0).Sum(h => h.Borc);

            // Satış Faturaları DAĞITIMI
            var satisFaturalari = faturalar.Where(f => (f.Tur ?? "").Equals("Satış", StringComparison.OrdinalIgnoreCase) || (f.Tur ?? "").Equals("Satis", StringComparison.OrdinalIgnoreCase)).ToList();
            decimal remCollection = totalCollection;
            foreach (var f in satisFaturalari)
            {
                if (remCollection > 0)
                {
                    if (remCollection >= f.GenelToplam) { f.Odenen = f.GenelToplam; remCollection -= f.GenelToplam; }
                    else { f.Odenen = remCollection; remCollection = 0; }
                }
                else f.Odenen = 0;
                await _firebaseService.SaveAsync("Faturalar", f, f.Id);
            }

            // Alış Faturaları DAĞITIMI
            var alisFaturalari = faturalar.Where(f => (f.Tur ?? "").Equals("Alış", StringComparison.OrdinalIgnoreCase) || (f.Tur ?? "").Equals("Alis", StringComparison.OrdinalIgnoreCase)).ToList();
            decimal remPayment = totalPayment;
            foreach (var f in alisFaturalari)
            {
                if (remPayment > 0)
                {
                    if (remPayment >= f.GenelToplam) { f.Odenen = f.GenelToplam; remPayment -= f.GenelToplam; }
                    else { f.Odenen = remPayment; remPayment = 0; }
                }
                else f.Odenen = 0;
                await _firebaseService.SaveAsync("Faturalar", f, f.Id);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"MatchInvoicePaymentsAsync Error (Firebase): {ex.Message}");
        }
    }

    public async Task<CariSummary> GetGlobalSummaryAsync(string? search = null)
    {
        var allCaris = await GetAllAsync();
        var query = allCaris.AsEnumerable();

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(c => (c.Unvan != null && c.Unvan.Contains(search, StringComparison.OrdinalIgnoreCase)) || 
                                     (c.CariKod != null && c.CariKod.Contains(search, StringComparison.OrdinalIgnoreCase)));
        }

        var list = query.ToList();
        
        return new CariSummary
        {
            ToplamBorc = list.Sum(x => x.Borc + x.DevirBorc),
            ToplamAlacak = list.Sum(x => x.Alacak + x.DevirAlacak),
            MusteriBakiye = list.Where(c => (c.Tur?.ToLower() ?? "") is "alici" or "alıcı" or "müşteri" || (c.Grup?.ToLower() ?? "") is "müşteri")
                                .Sum(c => c.Bakiye),
            TedarikciBakiye = list.Where(c => (c.Tur?.ToLower() ?? "") is "satici" or "satıcı" or "tedarikçi" || (c.Grup?.ToLower() ?? "") is "tedarikçi")
                                  .Sum(c => c.Bakiye),
            ToplamCari = list.Count
        };
    }

    public override async Task<int> DeleteAsync(CariKart entity)
    {
        await CleanupHareketlerAsync(entity.Id);
        return await base.DeleteAsync(entity);
    }

    public override async Task<int> DeleteAsync(int id)
    {
        await CleanupHareketlerAsync(id);
        return await base.DeleteAsync(id);
    }

    public async Task<List<CariHareket>> GetAllHareketlerAsync()
    {
        return await _firebaseService.GetAllAsync<CariHareket>("CariHareketler");
    }

    private async Task CleanupHareketlerAsync(int cariId)
    {
        try
        {
            // 1. Delete Cari Movements and reverse related financial totals
            var hareketler = await GetAllHareketlerAsync();
            var toDelete = hareketler.Where(h => h.CariId == cariId).ToList();
            foreach (var h in toDelete)
            {
                await DeleteHareketAsync(h);
            }

            // 2. Process Associated Faturalar with Stock and Movement cleanup
            var faturalar = await _firebaseService.GetAllAsync<Fatura>("Faturalar");
            var filteredFaturalar = faturalar.Where(f => f.CariId == cariId).ToList();
            
            var allStoklar = await _firebaseService.GetAllAsync<StokKart>("Stoklar");
            var allStokH = await _firebaseService.GetAllAsync<StokHareket>("StokHareketler");

            foreach (var f in filteredFaturalar)
            {
                // Reverse Stock Balances
                var detaylar = await _firebaseService.GetFaturaDetaylarAsync(f.Id);
                bool isSatis = (f.Tur?.Equals("Satış", StringComparison.OrdinalIgnoreCase) == true || 
                                f.Tur?.Equals("Satis", StringComparison.OrdinalIgnoreCase) == true);

                foreach (var d in detaylar)
                {
                    var stok = allStoklar.FirstOrDefault(s => s.Id == d.StokId);
                    if (stok != null)
                    {
                        if (isSatis) stok.Miktar += d.Miktar;
                        else stok.Miktar -= d.Miktar;
                        await _firebaseService.SaveAsync("Stoklar", stok, stok.Id);
                    }
                }

                // Delete Stock Movements
                var shToDel = allStokH.Where(h => h.FaturaId == f.Id || h.EvrakNo == f.FaturaNo).ToList();
                foreach (var sh in shToDel) await _firebaseService.DeleteAsync("StokHareketler", sh.Id);

                // Raw Deletion of Fatura and Details
                await _firebaseService.DeleteAsync("Faturalar", f.Id);
                await _firebaseService.DeleteAsync("FaturaDetaylar", f.Id);
            }

            // 2.1. Recalculate Stock Costs for all affected stocks
            var affectedStokIds = filteredFaturalar.SelectMany(f => _firebaseService.GetFaturaDetaylarAsync(f.Id).Result.Select(d => d.StokId)).Distinct().ToList();
            if (affectedStokIds.Any())
            {
                 // We don't have direct access to Stok repo here without circular ref, 
                 // but we can manually reset or better, let the UnitOfWork handle it if called.
                 // For now, let's at least reset those affected to 0 or re-scan movements.
                 var currentMovements = await _firebaseService.GetAllAsync<StokHareket>("StokHareketler");
                 foreach(var sid in affectedStokIds)
                 {
                     var stok = allStoklar.FirstOrDefault(s => s.Id == sid);
                     if (stok == null) continue;
                     
                     var movements = currentMovements.Where(m => m.StokId == sid).OrderBy(m => m.Tarih).ToList();
                     if (!movements.Any())
                     {
                         stok.Miktar = 0;
                         stok.OrtalamaAlisFiyati = 0;
                         stok.OrtalamaSatisFiyati = 0;
                         stok.AlisFiyati = 0;
                         stok.SatisFiyati = 0;
                     }
                     else
                     {
                         decimal avgPur = 0; decimal totalPurVal = 0; decimal totalPurQty = 0;
                         decimal avgSale = 0; decimal totalSaleVal = 0; decimal totalSaleQty = 0;
                         decimal lastP = 0; decimal lastS = 0;
                         foreach(var m in movements)
                         {
                              if (m.IslemTuru == "GİRİŞ" || m.IslemTuru == "Alış Faturası" || m.Giren > 0 || (m.IslemTuru != null && (m.IslemTuru.Contains("Giriş", StringComparison.OrdinalIgnoreCase) || m.IslemTuru.Contains("Alış", StringComparison.OrdinalIgnoreCase))))
                              {
                                  decimal q = m.Giren > 0 ? m.Giren : m.Miktar;
                                  totalPurVal += (q * m.Fiyat);
                                  totalPurQty += q;
                                  if (totalPurQty > 0) avgPur = totalPurVal / totalPurQty;
                                  lastP = m.Fiyat;
                              }
                              else 
                              {
                                  decimal q = m.Cikan > 0 ? m.Cikan : m.Miktar;
                                  totalPurVal -= (q * avgPur);
                                  totalPurQty -= q;
                                  if (totalPurQty <= 0) { totalPurQty = 0; totalPurVal = 0; }

                                  totalSaleVal += (q * m.Fiyat);
                                  totalSaleQty += q;
                                  if (totalSaleQty > 0) avgSale = totalSaleVal / totalSaleQty;
                                  lastS = m.Fiyat;
                              }
                         }
                         stok.OrtalamaAlisFiyati = avgPur;
                         stok.OrtalamaSatisFiyati = avgSale;
                         if (lastP > 0) stok.AlisFiyati = lastP;
                         if (lastS > 0) stok.SatisFiyati = lastS;
                     }
                     await _firebaseService.SaveAsync("Stoklar", stok, stok.Id);
                 }
            }

            // 3. Delete Associated Siparisler and their details
            var siparisler = await _firebaseService.GetAllAsync<Siparis>("Siparisler");
            var filteredSiparisler = siparisler.Where(s => s.CariId == cariId).ToList();
            foreach (var s in filteredSiparisler)
            {
                await _firebaseService.DeleteAsync("Siparisler", s.Id);
                await _firebaseService.DeleteAsync("SiparisDetaylar", s.Id);
            }

            // 4. Delete Associated Teklifler and their details
            var teklifler = await _firebaseService.GetAllAsync<Teklif>("Teklifler");
            var filteredTeklifler = teklifler.Where(t => t.CariId == cariId).ToList();
            foreach (var t in filteredTeklifler)
            {
                await _firebaseService.DeleteAsync("Teklifler", t.Id);
                await _firebaseService.DeleteAsync("TeklifDetaylar", t.Id);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"CleanupHareketlerAsync (Cascade Delete) Error: {ex.Message}");
        }
    }
    public async Task MergeCariAsync(int sourceId, int targetId)
    {
        if (sourceId == targetId) return;

        var source = await GetByIdAsync(sourceId);
        var target = await GetByIdAsync(targetId);

        if (source == null || target == null) throw new Exception("Kaynak veya hedef cari bulunamadı.");

        // 1. Hareketleri Taşı (CariHareketler)
        var hareketler = await GetAllHareketlerAsync();
        var toMove = hareketler.Where(h => h.CariId == sourceId).ToList();
        foreach (var h in toMove)
        {
            h.CariId = targetId;
            h.CariUnvan = target.Unvan ?? "";
            await _firebaseService.SaveAsync("CariHareketler", h, h.Id);
        }

        var ciroHareketler = hareketler.Where(h => h.YonlendirilenCariId == sourceId).ToList();
        foreach (var h in ciroHareketler)
        {
            h.YonlendirilenCariId = targetId;
            h.YonlendirilenCariUnvan = target.Unvan ?? "";
            await _firebaseService.SaveAsync("CariHareketler", h, h.Id);
        }

        // 2. Faturaları Taşı
        var faturalar = await _firebaseService.GetAllAsync<Fatura>("Faturalar");
        var fToMove = faturalar.Where(f => f.CariId == sourceId).ToList();
        foreach (var f in fToMove)
        {
            f.CariId = targetId;
            await _firebaseService.SaveAsync("Faturalar", f, f.Id);
        }

        // 3. Sipariş/Teklif Taşı
        var siparisler = await _firebaseService.GetAllAsync<Siparis>("Siparisler");
        foreach (var s in siparisler.Where(x => x.CariId == sourceId))
        {
            s.CariId = targetId;
            s.CariUnvan = target.Unvan ?? "";
            await _firebaseService.SaveAsync("Siparisler", s, s.Id);
        }

        var teklifler = await _firebaseService.GetAllAsync<Teklif>("Teklifler");
        foreach (var t in teklifler.Where(x => x.CariId == sourceId))
        {
            t.CariId = targetId;
            t.CariUnvan = target.Unvan ?? "";
            await _firebaseService.SaveAsync("Teklifler", t, t.Id);
        }

        // 4. Çek & Senet Taşı
        var cekler = await _firebaseService.GetAllAsync<Cek>("Cekler");
        foreach (var c in cekler.Where(x => x.CariId == sourceId))
        {
            c.CariId = targetId;
            c.CariUnvan = target.Unvan ?? "";
            await _firebaseService.SaveAsync("Cekler", c, c.Id);
        }
        foreach (var c in cekler.Where(x => x.YonlendirilenCariId == sourceId))
        {
            c.YonlendirilenCariId = targetId;
            c.YonlendirilenCariUnvan = target.Unvan ?? "";
            await _firebaseService.SaveAsync("Cekler", c, c.Id);
        }

        var senetler = await _firebaseService.GetAllAsync<Senet>("Senetler");
        foreach (var s in senetler.Where(x => x.CariId == sourceId))
        {
            s.CariId = targetId;
            s.CariUnvan = target.Unvan ?? "";
            await _firebaseService.SaveAsync("Senetler", s, s.Id);
        }

        // 5. Kasa & Banka Hareketlerini Taşı
        var kasaHarekets = await _firebaseService.GetAllAsync<KasaHareket>("KasaHareketler");
        foreach (var k in kasaHarekets.Where(x => x.CariId == sourceId))
        {
            k.CariId = targetId;
            k.CariUnvan = target.Unvan ?? "";
            await _firebaseService.SaveAsync("KasaHareketler", k, k.Id);
        }
        foreach (var k in kasaHarekets.Where(x => x.YonlendirilenCariId == sourceId))
        {
            k.YonlendirilenCariId = targetId;
            k.YonlendirilenCariUnvan = target.Unvan ?? "";
            await _firebaseService.SaveAsync("KasaHareketler", k, k.Id);
        }

        var bankaHarekets = await _firebaseService.GetAllAsync<BankaHareket>("BankaHareketler");
        foreach (var b in bankaHarekets.Where(x => x.CariId == sourceId))
        {
            b.CariId = targetId;
            b.CariUnvan = target.Unvan ?? "";
            await _firebaseService.SaveAsync("BankaHareketler", b, b.Id);
        }
        foreach (var b in bankaHarekets.Where(x => x.YonlendirilenCariId == sourceId))
        {
            b.YonlendirilenCariId = targetId;
            b.YonlendirilenCariUnvan = target.Unvan ?? "";
            await _firebaseService.SaveAsync("BankaHareketler", b, b.Id);
        }

        // 6. Kredi Kartı & EFT İşlemlerini Taşı
        var kkIslemler = await _firebaseService.GetAllAsync<KrediKartiIslem>("KrediKartiIslemleri");
        foreach (var k in kkIslemler.Where(x => x.MusteriId == sourceId))
        {
            k.MusteriId = targetId;
            k.MusteriUnvan = target.Unvan ?? "";
            await _firebaseService.SaveAsync("KrediKartiIslemleri", k, k.Id);
        }
        foreach (var k in kkIslemler.Where(x => x.YonlendirilenCariId == sourceId))
        {
            k.YonlendirilenCariId = targetId;
            k.YonlendirilenCariUnvan = target.Unvan ?? "";
            await _firebaseService.SaveAsync("KrediKartiIslemleri", k, k.Id);
        }

        var eftIslemler = await _firebaseService.GetAllAsync<EftIslem>("EftIslemleri");
        foreach (var e in eftIslemler.Where(x => x.MusteriId == sourceId))
        {
            e.MusteriId = targetId;
            e.MusteriUnvan = target.Unvan ?? "";
            await _firebaseService.SaveAsync("EftIslemleri", e, e.Id);
        }
        foreach (var e in eftIslemler.Where(x => x.YonlendirilenCariId == sourceId))
        {
            e.YonlendirilenCariId = targetId;
            e.YonlendirilenCariUnvan = target.Unvan ?? "";
            await _firebaseService.SaveAsync("EftIslemleri", e, e.Id);
        }

        // 7. Kaynağı Sil
        await DeleteAsync(sourceId);

        // 8. Bakiyeleri Yenile
        await RecalculateBalanceAsync(targetId);
    }
}
