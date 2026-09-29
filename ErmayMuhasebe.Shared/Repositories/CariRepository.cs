using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using ErmayMuhasebe.Models;
using ErmayMuhasebe.Services;
using SQLite;

namespace ErmayMuhasebe.Repositories;

/// <summary>
/// Cari Kart Repository
/// Cari hesap işlemlerini yönetir
/// </summary>
public class CariRepository : BaseRepository<CariKart>, ICariRepository
{
    public CariRepository(DatabaseService dbService) : base(dbService)
    {
    }

    public override async Task<List<CariKart>> GetAllAsync()
    {
        var db = await GetConnectionAsync();
        return await db.Table<CariKart>()
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.Unvan)
            .ToListAsync();
    }

    public override async Task<CariKart?> GetByIdAsync(int id)
    {
        var db = await GetConnectionAsync();
        return await db.Table<CariKart>()
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);
    }

    public override async Task<int> SaveAsync(CariKart entity)
    {
        var db = await GetConnectionAsync();
        
        // VALIDATIONS
        // 1. Vergi No / VKN Duplicate Check (if not empty)
        if (!string.IsNullOrEmpty(entity.VergiNo))
        {
            var existing = await db.Table<CariKart>()
                .FirstOrDefaultAsync(c => c.VergiNo == entity.VergiNo && c.Id != entity.Id && !c.IsDeleted);
            if (existing != null) throw new System.Exception($"Bu Vergi/TC numarası ({entity.VergiNo}) ile zaten bir kayıt var: {existing.Unvan}");
        }

        // 2. TC Kimlik No 11 Hane Validation
        if (!string.IsNullOrEmpty(entity.TCNo))
        {
            if (entity.TCNo.Length != 11 || !entity.TCNo.All(char.IsDigit))
                throw new System.Exception("TC Kimlik Numarası tam olarak 11 hane ve rakamlardan oluşmalıdır.");
            
            var existingTC = await db.Table<CariKart>()
                .FirstOrDefaultAsync(c => c.TCNo == entity.TCNo && c.Id != entity.Id && !c.IsDeleted);
            if (existingTC != null) throw new System.Exception($"Bu TC numarası ({entity.TCNo}) ile zaten bir kayıt var: {existingTC.Unvan}");
        }
        
        if (entity.Id != 0)
        {
            var existing = await db.Table<CariKart>().FirstOrDefaultAsync(c => c.Id == entity.Id);
            if (existing != null && existing.Version != entity.Version)
            {
                throw new System.Exception("Çakışma Tespit Edildi! Bu veri başka bir cihazda güncellenmiş. Lütfen sayfayı yenileyip tekrar deneyin.");
            }
            entity.Version++;
            entity.UpdatedAt = DateTime.Now;
            await db.UpdateAsync(entity);
        }
        else
        {
            var maxId = await db.ExecuteScalarAsync<int>("SELECT IFNULL(MAX(Id), 0) FROM CariKart");
            entity.Id = maxId + 1;
            entity.Version = 1;
            entity.UpdatedAt = DateTime.Now;
            await db.InsertAsync(entity);
        }

        // Bulut senkronizasyonu
        await _syncService.SyncCariAsync(entity);
        _dbService.AutoReflectCari(entity.Id);
        
        return entity.Id;
    }

    public override async Task<int> DeleteAsync(CariKart entity)
    {
        var hCount = await GetCountAsync<CariHareket>(h => h.CariId == entity.Id);
        if (hCount > 0) throw new System.Exception("Bu carinin hareketleri bulunmaktadır. Hareket görmüş bir cari silinemez! Önce hareketleri temizlemeli veya cariyi Pasif yapmalısınız.");
        
        return await _dbService.DeleteCariAsync(entity);
    }

    public override async Task<int> DeleteAsync(int id)
    {
        var entity = await GetByIdAsync(id);
        if (entity == null) return 0;
        return await DeleteAsync(entity);
    }

    private async Task<int> GetCountAsync<TTable>(Expression<Func<TTable, bool>> predicate) where TTable : class, new()
    {
        var db = await GetConnectionAsync();
        return await db.Table<TTable>().Where(predicate).CountAsync();
    }

    public override async Task<List<CariKart>> GetDeletedAsync()
    {
        var db = await GetConnectionAsync();
        return await db.Table<CariKart>()
            .Where(c => c.IsDeleted)
            .ToListAsync();
    }

    public override async Task RestoreAsync(CariKart entity)
    {
        entity.IsDeleted = false;
        await SaveAsync(entity);
    }

    // Özel metodlar
    public async Task<List<CariKart>> GetByTurAsync(string tur)
    {
        var db = await GetConnectionAsync();
        return await db.Table<CariKart>()
            .Where(c => c.Tur == tur && !c.IsDeleted)
            .OrderBy(c => c.Unvan)
            .ToListAsync();
    }

    public async Task<List<CariKart>> GetHareketsizCarilerAsync(int gunSayisi = 180)
    {
        var db = await GetConnectionAsync();
        var cariler = await db.Table<CariKart>()
            .Where(c => !c.IsDeleted)
            .ToListAsync();
        var hareketler = await db.Table<CariHareket>().ToListAsync();
        var cutoffDate = DateTime.Now.AddDays(-gunSayisi);
        var result = new List<CariKart>();

        foreach (var c in cariler)
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
        var db = await GetConnectionAsync();
        return await db.Table<CariHareket>()
            .Where(h => h.CariId == cariId)
            .OrderByDescending(h => h.Tarih)
            .ToListAsync();
    }

    public async Task<List<CariHareket>> GetAllHareketlerAsync()
    {
        var db = await GetConnectionAsync();
        return await db.Table<CariHareket>().OrderByDescending(x => x.Tarih).ToListAsync();
    }

    public async Task<CariHareket?> GetHareketByEvrakNoAsync(string evrakNo)
    {
        if (string.IsNullOrEmpty(evrakNo)) return null;
        var hlist = await GetAllHareketlerAsync();
        return hlist.FirstOrDefault(x => x.EvrakNo == evrakNo);
    }

    public async Task<int> DeleteHareketByEvrakNoAsync(string evrakNo)
    {
        if (string.IsNullOrEmpty(evrakNo)) return 0;
        var h = await GetHareketByEvrakNoAsync(evrakNo);
        if (h != null)
        {
            await DeleteHareketAsync(h);
            await RecalculateBalanceAsync(h.CariId);
            return 1;
        }
        return 0;
    }

    public async Task<int> SaveHareketAsync(CariHareket hareket)
    {
        var db = await GetConnectionAsync();
        decimal borcDelta = 0;
        decimal alacakDelta = 0;
        
        await db.RunInTransactionAsync(tran => 
        {
            var cari = tran.Find<CariKart>(hareket.CariId);
            if (cari != null)
            {
                bool isDevir = (hareket.IslemTuru != null && (hareket.IslemTuru == "Devir Fişi" || hareket.IslemTuru == "Açılış Fişi" || hareket.IslemTuru.StartsWith("Devir"))) ||
                               (!string.IsNullOrEmpty(hareket.EvrakNo) && hareket.EvrakNo.StartsWith("DEVIR-"));

                if (isDevir)
                {
                    cari.DevirBorc = hareket.Borc;
                    cari.DevirAlacak = hareket.Alacak;
                    tran.Update(cari);
                }
                else
                {
                    if (hareket.Id != 0) // UPDATE
                    {
                        var oldItem = tran.Find<CariHareket>(hareket.Id);
                        if (oldItem != null)
                        {
                            bool oldWasDevir = (oldItem.IslemTuru != null && (oldItem.IslemTuru == "Devir Fişi" || oldItem.IslemTuru == "Açılış Fişi" || oldItem.IslemTuru.StartsWith("Devir"))) ||
                                               (!string.IsNullOrEmpty(oldItem.EvrakNo) && oldItem.EvrakNo.StartsWith("DEVIR-"));
                            if (!oldWasDevir)
                            {
                                cari.Borc -= oldItem.Borc;
                                cari.Alacak -= oldItem.Alacak;
                                
                                borcDelta = hareket.Borc - oldItem.Borc;
                                alacakDelta = hareket.Alacak - oldItem.Alacak;
                            }
                        }
                    }
                    else // INSERT
                    {
                        borcDelta = hareket.Borc;
                        alacakDelta = hareket.Alacak;
                    }

                    // Apply New
                    cari.Borc += hareket.Borc;
                    cari.Alacak += hareket.Alacak;

                    tran.Update(cari);
                }
            }

            if (hareket.Id != 0) tran.Update(hareket); else tran.Insert(hareket);
        });
        await _syncService.SyncCariHareketAsync(hareket);
        
        if (borcDelta != 0 || alacakDelta != 0)
        {
            await _syncService.UpdateFutureBalancesAsync("Cariler", hareket.CariId, borcDelta, alacakDelta);
        }
        _dbService.AutoReflectCari(hareket.CariId);
        
        return hareket.Id;
    }

    public async Task<int> DeleteHareketAsync(CariHareket hareket)
    {
        var db = await GetConnectionAsync();
        if (hareket == null) return 0;

        try 
        {
            // 1. REVERSE CARI BALANCE
            var cari = await GetByIdAsync(hareket.CariId);
            if (cari != null)
            {
                cari.Borc -= hareket.Borc;
                cari.Alacak -= hareket.Alacak;
                await SaveAsync(cari);
                
                await _syncService.UpdateFutureBalancesAsync("Cariler", hareket.CariId, -hareket.Borc, -hareket.Alacak);
                _dbService.AutoReflectCari(hareket.CariId);
            }

            // CASCADE DELETE: Find and remove linked financial records (Kasa, Banka, KK)
            var startDay = hareket.Tarih.Date;
            var endDay = startDay.AddDays(1);
            var amount = hareket.Borc + hareket.Alacak;

            // 2. KASA HAREKETİ
            var kasalar = await db.Table<KasaHareket>()
                .Where(k => k.Tarih >= startDay && k.Tarih < endDay)
                .ToListAsync();
            
            foreach (var k in kasalar)
            {
                bool match = false;
                if (!string.IsNullOrEmpty(hareket.EvrakNo) && k.EvrakNo == hareket.EvrakNo) match = true;
                else if (Math.Abs((k.Giren + k.Cikan) - amount) < 0.01m)
                {
                     if (hareket.CariUnvan != null && (k.CariUnvan == hareket.CariUnvan || (k.Aciklama != null && k.Aciklama.Contains(hareket.CariUnvan))))
                        match = true;
                }

                if (match)
                {
                    var kasa = await db.Table<BankaKart>().FirstOrDefaultAsync(b => b.Id == k.KasaId);
                    if (kasa != null)
                    {
                        kasa.GuncelBakiye -= (k.Giren - k.Cikan);
                        await db.UpdateAsync(kasa);
                        await _syncService.SyncBankaAsync(kasa);
                    }
                    await db.DeleteAsync(k);
                    await _syncService.DeleteKasaHareketAsync(k.Id);
                }
            }

            // 3. BANKA HAREKETİ
            var bankalar = await db.Table<BankaHareket>()
                .Where(b => b.Tarih >= startDay && b.Tarih < endDay)
                .ToListAsync();
            
            foreach (var b in bankalar)
            {
                bool match = false;
                if (!string.IsNullOrEmpty(hareket.EvrakNo) && b.EvrakNo == hareket.EvrakNo) match = true;
                else if (Math.Abs((b.Giren + b.Cikan) - amount) < 0.01m)
                {
                    if (hareket.CariUnvan != null && (b.CariUnvan == hareket.CariUnvan || (b.Aciklama != null && b.Aciklama.Contains(hareket.CariUnvan))))
                        match = true;
                }

                if (match)
                {
                    var banka = await db.Table<BankaKart>().FirstOrDefaultAsync(Bk => Bk.Id == b.BankaId);
                    if (banka != null)
                    {
                        banka.GuncelBakiye -= (b.Giren - b.Cikan);
                        await db.UpdateAsync(banka);
                        await _syncService.SyncBankaAsync(banka);
                    }
                    await db.DeleteAsync(b);
                    await _syncService.DeleteBankaHareketAsync(b.Id);
                }
            }

            // 4. KREDİ KARTI İŞLEMİ
            var kkIslemler = await db.Table<KrediKartiIslem>()
                    .Where(k => k.MusteriId == hareket.CariId && k.Tarih >= startDay && k.Tarih < endDay)
                    .ToListAsync();

            foreach(var kk in kkIslemler)
            {
                bool kkMatch = false;
                if (!string.IsNullOrEmpty(hareket.EvrakNo) && kk.OnayKodu == hareket.EvrakNo) kkMatch = true;
                else if (Math.Abs(kk.Tutar - amount) < 0.01m) kkMatch = true;

                if (kkMatch)
                {
                    await db.DeleteAsync(kk);
                    await _syncService.DeleteKrediKartiIslemAsync(kk.Id);
                }
            }

            // 5. ÇEK/SENET TEMİZLİĞİ
            if (!string.IsNullOrEmpty(hareket.EvrakNo))
            {
                var ceks = await db.Table<Cek>().Where(c => c.CariId == hareket.CariId && c.PortfoyNo == hareket.EvrakNo).ToListAsync();
                foreach(var c in ceks) 
                {
                    await db.DeleteAsync(c);
                    await _syncService.DeleteCekAsync(c.Id);
                }
                
                var senets = await db.Table<Senet>().Where(s => s.CariId == hareket.CariId && s.PortfoyNo == hareket.EvrakNo).ToListAsync();
                foreach(var s in senets) 
                {
                    await db.DeleteAsync(s);
                    await _syncService.DeleteSenetAsync(s.Id);
                }
            }
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Cascade Delete Error: {ex.Message}");
        }
        
        await _syncService.DeleteCariHareketAsync(hareket.Id);
        return await db.DeleteAsync(hareket);
    }

    public async Task<int> RecalculateBalanceAsync(int cariId)
    {
        var db = await GetConnectionAsync();
        var hareketler = await db.Table<CariHareket>().Where(x => x.CariId == cariId).ToListAsync();
        var cari = await db.Table<CariKart>().FirstOrDefaultAsync(x => x.Id == cariId);
        
        if (cari != null)
        {
            // 1. Cari toplam borç/alacak güncelle (Devir fişleri hariç dönem içi hareketler)
            var nonDevirMoves = hareketler.Where(x => (x.IslemTuru == null || (x.IslemTuru != "Devir Fişi" && x.IslemTuru != "Açılış Fişi" && !x.IslemTuru.StartsWith("Devir"))) && 
                                                      (x.EvrakNo == null || !x.EvrakNo.StartsWith("DEVIR-"))).ToList();
            var devirMove = hareketler.FirstOrDefault(x => (x.IslemTuru != null && (x.IslemTuru == "Devir Fişi" || x.IslemTuru == "Açılış Fişi" || x.IslemTuru.StartsWith("Devir"))) || 
                                                           (x.EvrakNo != null && x.EvrakNo.StartsWith("DEVIR-")));
            if (devirMove != null)
            {
                cari.DevirBorc = devirMove.Borc;
                cari.DevirAlacak = devirMove.Alacak;
            }

            cari.Borc = nonDevirMoves.Sum(x => x.Borc);
            cari.Alacak = nonDevirMoves.Sum(x => x.Alacak);
            await db.UpdateAsync(cari);
            
            // 2. Ödemeleri Faturalarla Eşleştir (FIFO) - Vade Takibi için
            await MatchInvoicePaymentsAsync(cariId);

            // Cloud sync
            await _syncService.SyncCariAsync(cari);
            _dbService.AutoReflectCari(cariId);
            return 1;
        }
        return 0;
    }

    public async Task MatchInvoicePaymentsAsync(int cariId)
    {
        var db = await GetConnectionAsync();
        
        // 1. Faturaları Al (Tarih sırasına göre FIFO)
        var faturalar = await db.Table<Fatura>()
            .Where(f => f.CariId == cariId && !f.IsDeleted)
            .OrderBy(f => f.Tarih)
            .ToListAsync();

        // 2. Tüm hareketleri al (Ödemeleri tespit etmek için)
        var hareketler = await db.Table<CariHareket>()
            .Where(h => h.CariId == cariId)
            .ToListAsync();

        // FIFO Mantığı:
        // Satış Faturaları (Bizim Alacağımız): Tahsilatlar ve Alacak Dekontları ile kapanır.
        // Alış Faturaları (Bizim Borcumuz): Ödemeler ve Borç Dekontları ile kapanır.

        // Toplam Tahsilat Kapasitesi (Satışları kapatacak olanlar)
        decimal totalCollection = hareketler.Where(h => 
            (h.IslemTuru != null && (h.IslemTuru.Contains("Tahsilat") || h.IslemTuru.Contains("Alacak Dekontu") || h.IslemTuru == "Açılış" || h.IslemTuru == "İade")) && h.Alacak > 0
        ).Sum(h => h.Alacak);

        // Toplam Ödeme Kapasitesi (Alışları kapatacak olanlar)
        decimal totalPayment = hareketler.Where(h => 
            (h.IslemTuru != null && (h.IslemTuru.Contains("Ödeme") || h.IslemTuru.Contains("Borç Dekontu") || h.IslemTuru == "Açılış" || h.IslemTuru == "İade")) && h.Borc > 0
        ).Sum(h => h.Borc);

        // Not: 'İade' ve 'Açılış' her iki yönde de olabilir, borç/alacak kontrolü yapıyoruz.
        // Aslında daha genel bir kural: 
        // Satış faturası dışındaki tüm ALACAK hareketleri Satış faturalarını kapatır.
        // Alış faturası dışındaki tüm BORÇ hareketleri Alış faturalarını kapatır.

        // SATIŞ FATURALARI FIFO DAĞITIMI
        var satisFaturalari = faturalar.Where(f => (f.Tur ?? "").Equals("Satış", System.StringComparison.OrdinalIgnoreCase) || (f.Tur ?? "").Equals("Satis", System.StringComparison.OrdinalIgnoreCase)).ToList();
        decimal remCollection = totalCollection;
        foreach (var f in satisFaturalari)
        {
            if (remCollection > 0)
            {
                if (remCollection >= f.GenelToplam) { f.Odenen = f.GenelToplam; remCollection -= f.GenelToplam; }
                else { f.Odenen = remCollection; remCollection = 0; }
            }
            else f.Odenen = 0;
            await db.UpdateAsync(f);
        }

        // ALIŞ FATURALARI FIFO DAĞITIMI
        var alisFaturalari = faturalar.Where(f => (f.Tur ?? "").Equals("Alış", System.StringComparison.OrdinalIgnoreCase) || (f.Tur ?? "").Equals("Alis", System.StringComparison.OrdinalIgnoreCase)).ToList();
        decimal remPayment = totalPayment;
        foreach (var f in alisFaturalari)
        {
            if (remPayment > 0)
            {
                if (remPayment >= f.GenelToplam) { f.Odenen = f.GenelToplam; remPayment -= f.GenelToplam; }
                else { f.Odenen = remPayment; remPayment = 0; }
            }
            else f.Odenen = 0;
            await db.UpdateAsync(f);
        }
    }

    public async Task<CariSummary> GetGlobalSummaryAsync(string? search = null)
    {
        var db = await GetConnectionAsync();
        var query = db.Table<CariKart>().Where(x => !x.IsDeleted);

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(c => (c.Unvan != null && c.Unvan.Contains(search)) || 
                                     (c.CariKod != null && c.CariKod.Contains(search)));
        }

        var allCaris = await query.ToListAsync();
        
        return new CariSummary
        {
            ToplamBorc = allCaris.Sum(x => x.Borc + x.DevirBorc),
            ToplamAlacak = allCaris.Sum(x => x.Alacak + x.DevirAlacak),
            MusteriBakiye = allCaris.Where(c => (c.Tur?.ToLower() ?? "") is "alici" or "alıcı" or "müşteri" || (c.Grup?.ToLower() ?? "") is "müşteri")
                                    .Sum(c => c.Bakiye),
            TedarikciBakiye = allCaris.Where(c => (c.Tur?.ToLower() ?? "") is "satici" or "satıcı" or "tedarikçi" || (c.Grup?.ToLower() ?? "") is "tedarikçi")
                                      .Sum(c => c.Bakiye),
            ToplamCari = allCaris.Count
        };
    }

    private async Task<int> SoftDeleteAsync(CariKart entity)
    {
        entity.IsDeleted = true;
        var db = await GetConnectionAsync();
        return await db.UpdateAsync(entity);
    }

    public async Task MergeCariAsync(int sourceId, int targetId)
    {
        if (sourceId == targetId) return;
        var db = await GetConnectionAsync();
        
        var source = await GetByIdAsync(sourceId);
        var target = await GetByIdAsync(targetId);
        
        if (source == null || target == null) throw new System.Exception("Kaynak veya hedef cari bulunamadı.");

        await db.RunInTransactionAsync(tran => 
        {
            // 1. Move Movements
            var hareketler = tran.Table<CariHareket>().Where(h => h.CariId == sourceId).ToList();
            foreach (var h in hareketler)
            {
                h.CariId = targetId;
                h.CariUnvan = target.Unvan ?? "";
                tran.Update(h);
            }

            var ciroHareketler = tran.Table<CariHareket>().Where(h => h.YonlendirilenCariId == sourceId).ToList();
            foreach (var h in ciroHareketler)
            {
                h.YonlendirilenCariId = targetId;
                h.YonlendirilenCariUnvan = target.Unvan ?? "";
                tran.Update(h);
            }

            // 2. Move Invoices
            var faturalar = tran.Table<Fatura>().Where(f => f.CariId == sourceId).ToList();
            foreach (var f in faturalar)
            {
                f.CariId = targetId;
                tran.Update(f);
            }
            
            // 3. Move Orders/Offers
            var siparisler = tran.Table<Siparis>().Where(s => s.CariId == sourceId).ToList();
            foreach (var s in siparisler) 
            { 
                s.CariId = targetId; 
                s.CariUnvan = target.Unvan ?? "";
                tran.Update(s); 
            }

            var teklifler = tran.Table<Teklif>().Where(t => t.CariId == sourceId).ToList();
            foreach (var t in teklifler) 
            { 
                t.CariId = targetId; 
                t.CariUnvan = target.Unvan ?? "";
                tran.Update(t); 
            }

            // 4. Move Checks & Notes (Cek/Senet)
            var cekler = tran.Table<Cek>().Where(c => c.CariId == sourceId).ToList();
            foreach (var c in cekler)
            {
                c.CariId = targetId;
                c.CariUnvan = target.Unvan ?? "";
                tran.Update(c);
            }

            var ciroCekler = tran.Table<Cek>().Where(c => c.YonlendirilenCariId == sourceId).ToList();
            foreach (var c in ciroCekler)
            {
                c.YonlendirilenCariId = targetId;
                c.YonlendirilenCariUnvan = target.Unvan ?? "";
                tran.Update(c);
            }

            var senetler = tran.Table<Senet>().Where(s => s.CariId == sourceId).ToList();
            foreach (var s in senetler)
            {
                s.CariId = targetId;
                s.CariUnvan = target.Unvan ?? "";
                tran.Update(s);
            }

            // 5. Move Cash/Bank Movements
            var kasaHareketler = tran.Table<KasaHareket>().Where(k => k.CariId == sourceId).ToList();
            foreach (var k in kasaHareketler)
            {
                k.CariId = targetId;
                k.CariUnvan = target.Unvan ?? "";
                tran.Update(k);
            }

            var ciroKasaHareketler = tran.Table<KasaHareket>().Where(k => k.YonlendirilenCariId == sourceId).ToList();
            foreach (var k in ciroKasaHareketler)
            {
                k.YonlendirilenCariId = targetId;
                k.YonlendirilenCariUnvan = target.Unvan ?? "";
                tran.Update(k);
            }

            var bankaHareketler = tran.Table<BankaHareket>().Where(b => b.CariId == sourceId).ToList();
            foreach (var b in bankaHareketler)
            {
                b.CariId = targetId;
                b.CariUnvan = target.Unvan ?? "";
                tran.Update(b);
            }

            var ciroBankaHareketler = tran.Table<BankaHareket>().Where(b => b.YonlendirilenCariId == sourceId).ToList();
            foreach (var b in ciroBankaHareketler)
            {
                b.YonlendirilenCariId = targetId;
                b.YonlendirilenCariUnvan = target.Unvan ?? "";
                tran.Update(b);
            }

            // 6. Move Credit Card & EFT Operations
            var kkIslemler = tran.Table<KrediKartiIslem>().Where(k => k.MusteriId == sourceId).ToList();
            foreach (var k in kkIslemler)
            {
                k.MusteriId = targetId;
                k.MusteriUnvan = target.Unvan ?? "";
                tran.Update(k);
            }

            var ciroKkIslemler = tran.Table<KrediKartiIslem>().Where(k => k.YonlendirilenCariId == sourceId).ToList();
            foreach (var k in ciroKkIslemler)
            {
                k.YonlendirilenCariId = targetId;
                k.YonlendirilenCariUnvan = target.Unvan ?? "";
                tran.Update(k);
            }

            var eftIslemler = tran.Table<EftIslem>().Where(e => e.MusteriId == sourceId).ToList();
            foreach (var e in eftIslemler)
            {
                e.MusteriId = targetId;
                e.MusteriUnvan = target.Unvan ?? "";
                tran.Update(e);
            }

            var ciroEftIslemler = tran.Table<EftIslem>().Where(e => e.YonlendirilenCariId == sourceId).ToList();
            foreach (var e in ciroEftIslemler)
            {
                e.YonlendirilenCariId = targetId;
                e.YonlendirilenCariUnvan = target.Unvan ?? "";
                tran.Update(e);
            }

            // 7. Move Files and SMS History
            var cariDosyalar = tran.Table<CariDosya>().Where(cd => cd.CariId == sourceId).ToList();
            foreach (var cd in cariDosyalar)
            {
                cd.CariId = targetId;
                cd.CariUnvan = target.Unvan ?? "";
                tran.Update(cd);
            }

            var smsGecmisi = tran.Table<SmsGecmisi>().Where(sg => sg.CariId == sourceId).ToList();
            foreach (var sg in smsGecmisi)
            {
                sg.CariId = targetId;
                sg.Unvan = target.Unvan ?? "";
                tran.Update(sg);
            }

            // 8. Mark source as deleted
            source.IsDeleted = true;
            tran.Update(source);
        });

        // 5. Recalculate balances
        await RecalculateBalanceAsync(targetId);
        
        // Cloud sync
        await _syncService.SyncCariAsync(target);
        await _syncService.DeleteCariAsync(sourceId);
    }
}

