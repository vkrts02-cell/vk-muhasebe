using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ErmayMuhasebe.Models;
using ErmayMuhasebe.Services;
using SQLite;

namespace ErmayMuhasebe.Repositories;

/// <summary>
/// Fatura Repository
/// Fatura işlemlerini yönetir
/// </summary>
public class FaturaRepository : BaseRepository<Fatura>, IFaturaRepository
{
    public FaturaRepository(DatabaseService dbService) : base(dbService)
    {
    }

    public override async Task<List<Fatura>> GetAllAsync()
    {
        var db = await GetConnectionAsync();
        return await db.Table<Fatura>()
            .Where(f => !f.IsDeleted)
            .OrderByDescending(f => f.Tarih)
            .ToListAsync();
    }

    public override async Task<Fatura?> GetByIdAsync(int id)
    {
        var db = await GetConnectionAsync();
        return await db.Table<Fatura>()
            .FirstOrDefaultAsync(f => f.Id == id && !f.IsDeleted);
    }

    public override async Task<int> SaveAsync(Fatura entity)
    {
        var db = await GetConnectionAsync();
        
        if (entity.Id != 0)
        {
            var existing = await db.Table<Fatura>().FirstOrDefaultAsync(f => f.Id == entity.Id);
            if (existing != null && existing.Version != entity.Version)
            {
                throw new System.Exception("Çakışma Tespit Edildi! Bu fatura başka bir yerde güncellenmiş veya kilitlenmiş. Lütfen sayfayı yenileyip tekrar deneyin.");
            }
            entity.Version++;
            entity.UpdatedAt = DateTime.Now;
            await db.UpdateAsync(entity);
        }
        else
        {
            entity.Version = 1;
            entity.UpdatedAt = DateTime.Now;
            await db.InsertAsync(entity);
        }

        // Bulut senkronizasyonu
        await _syncService.SyncFaturaAsync(entity);
        
        return entity.Id;
    }

    public override async Task<int> DeleteAsync(Fatura entity)
    {
        var db = await GetConnectionAsync();
        string fNo = entity.FaturaNo?.Trim() ?? "";
        string kplNo = !string.IsNullOrEmpty(fNo) ? $"KPL-{fNo}" : "";

        // 1. Get Details to reverse stock
        var detaylar = await db.Table<FaturaDetay>().Where(d => d.FaturaId == entity.Id).ToListAsync();
        
        // Fallback: If no FaturaDetay records, infer from StokHareket
        if (!detaylar.Any())
        {
            var movements = await db.Table<StokHareket>()
                .Where(s => s.FaturaId == entity.Id || (!string.IsNullOrEmpty(fNo) && (s.EvrakNo == fNo || s.EvrakNo == kplNo)))
                .ToListAsync();
            foreach (var m in movements)
            {
                double miktar = m.Miktar > 0 ? (double)m.Miktar : (double)(m.Giren > 0 ? m.Giren : (m.Cikan > 0 ? m.Cikan : 0));
                detaylar.Add(new FaturaDetay 
                { 
                    FaturaId = entity.Id, 
                    StokId = m.StokId, 
                    Miktar = miktar,
                    BirimFiyat = m.Fiyat
                });
            }
        }

        // 2. Determine Invoice Direction (Sale vs Purchase)
        string tur = (entity.Tur ?? "").Trim();
        bool isSatis = tur.Contains("Satış", System.StringComparison.OrdinalIgnoreCase) || 
                       tur.Contains("Satis", System.StringComparison.OrdinalIgnoreCase);

        // Fallback detection for Tur if not explicit
        if (!isSatis && !tur.Contains("Alış", System.StringComparison.OrdinalIgnoreCase) && !tur.Contains("Alis", System.StringComparison.OrdinalIgnoreCase))
        {
            var sampleSh = await db.Table<StokHareket>().FirstOrDefaultAsync(s => s.FaturaId == entity.Id || (!string.IsNullOrEmpty(fNo) && s.EvrakNo == fNo));
            if (sampleSh != null)
            {
                if (sampleSh.Cikan > 0 || (sampleSh.IslemTuru != null && (sampleSh.IslemTuru.Contains("Satış", System.StringComparison.OrdinalIgnoreCase) || sampleSh.IslemTuru.Contains("Satis", System.StringComparison.OrdinalIgnoreCase))))
                    isSatis = true;
            }
            else
            {
                var sampleCh = await db.Table<CariHareket>().FirstOrDefaultAsync(c => c.FaturaId == entity.Id || (!string.IsNullOrEmpty(fNo) && c.EvrakNo == fNo));
                if (sampleCh != null && (sampleCh.Borc > 0 || (sampleCh.IslemTuru != null && sampleCh.IslemTuru.Contains("Satış", System.StringComparison.OrdinalIgnoreCase))))
                    isSatis = true;
            }
        }

        var affectedStokIds = detaylar.Select(d => d.StokId).Where(id => id > 0).Distinct().ToList();

        // 3. Reverse Stock Balances
        foreach (var d in detaylar)
        {
            var stok = await db.Table<StokKart>().FirstOrDefaultAsync(s => s.Id == d.StokId);
            if (stok != null)
            {
                if (isSatis) stok.Miktar += d.Miktar; // Sale reversed = Add back
                else stok.Miktar -= d.Miktar; // Buy reversed = Remove
                await db.UpdateAsync(stok);
                await _syncService.SyncStokAsync(stok);
            }
        }

        // 4. Reverse Cari Balance
        var cari = await db.Table<CariKart>().FirstOrDefaultAsync(c => c.Id == entity.CariId);
        if (cari != null)
        {
            if (isSatis) cari.Borc -= entity.GenelToplam;
            else cari.Alacak -= entity.GenelToplam;
            await db.UpdateAsync(cari);
            await _syncService.SyncCariAsync(cari);
        }

        // 5. Delete Movements & Soft Delete Invoice (Preserve FaturaDetay lines for audit history)
        var relatedSh = await db.Table<StokHareket>().Where(s => s.FaturaId == entity.Id || (!string.IsNullOrEmpty(fNo) && (s.EvrakNo == fNo || s.EvrakNo == kplNo))).ToListAsync();
        var relatedCh = await db.Table<CariHareket>().Where(c => c.FaturaId == entity.Id || (!string.IsNullOrEmpty(fNo) && (c.EvrakNo == fNo || c.EvrakNo == kplNo))).ToListAsync();

        foreach (var sh in relatedSh)
        {
            await _syncService.DeleteStokHareketAsync(sh.Id);
        }
        foreach (var ch in relatedCh)
        {
            await _syncService.DeleteCariHareketAsync(ch.Id);
        }

        await db.RunInTransactionAsync(tran =>
        {
            entity.IsDeleted = true;
            entity.UpdatedAt = DateTime.Now;
            tran.Update(entity);

            tran.Execute("DELETE FROM StokHareket WHERE FaturaId = ? OR (EvrakNo IS NOT NULL AND EvrakNo != '' AND (EvrakNo = ? OR EvrakNo = ?))", entity.Id, fNo, kplNo);
            tran.Execute("DELETE FROM CariHareket WHERE FaturaId = ? OR (CariId = ? AND EvrakNo IS NOT NULL AND EvrakNo != '' AND (EvrakNo = ? OR EvrakNo = ?))", entity.Id, entity.CariId, fNo, kplNo);
        });

        // Sync Deletions to Cloud
        await _syncService.DeleteStokHareketByFaturaIdAsync(entity.Id, entity.FaturaNo);
        await _syncService.DeleteCariHareketByFaturaIdAsync(entity.Id, entity.FaturaNo);
        await _syncService.SyncFaturaAsync(entity);

        // 6. Clean up linked financial records (Kasa / Banka)
        if (!string.IsNullOrEmpty(fNo))
        {
            var mkasa = await db.Table<KasaHareket>()
                .Where(k => k.EvrakNo == fNo || k.EvrakNo == kplNo || (k.Aciklama != null && k.Aciklama.Contains(fNo)))
                .ToListAsync();
            foreach(var k in mkasa)
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
            
            var mbanka = await db.Table<BankaHareket>()
                .Where(b => b.EvrakNo == fNo || b.EvrakNo == kplNo || (b.Aciklama != null && b.Aciklama.Contains(fNo)))
                .ToListAsync();
            foreach(var b in mbanka)
            {
                var banka = await db.Table<BankaKart>().FirstOrDefaultAsync(bk => bk.Id == b.BankaId);
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

        // 7. Ensure Exact Stock Quantities & Costs from remaining movements
        foreach (var sId in affectedStokIds)
        {
            var currentStok = await db.Table<StokKart>().FirstOrDefaultAsync(s => s.Id == sId);
            if (currentStok != null)
            {
                var sumGiren = await db.ExecuteScalarAsync<double>("SELECT IFNULL(SUM(CASE WHEN Giren > 0 THEN Giren WHEN Miktar > 0 AND (IslemTuru LIKE '%Giriş%' OR IslemTuru LIKE '%Alış%' OR IslemTuru LIKE '%Açılış%') THEN Miktar ELSE 0 END), 0) FROM StokHareket WHERE StokId = ?", sId);
                var sumCikan = await db.ExecuteScalarAsync<double>("SELECT IFNULL(SUM(CASE WHEN Cikan > 0 THEN Cikan WHEN Miktar > 0 AND (IslemTuru LIKE '%Çıkış%' OR IslemTuru LIKE '%Satış%') THEN Miktar ELSE 0 END), 0) FROM StokHareket WHERE StokId = ?", sId);
                currentStok.Miktar = sumGiren - sumCikan;
                await db.UpdateAsync(currentStok);
                await _syncService.SyncStokAsync(currentStok);
            }
        }

        await db.RunInTransactionAsync(tran => 
        {
            foreach(var sId in affectedStokIds)
            {
                _recalculateStockCostInternal(tran, sId);
            }
        });

        foreach (var sId in affectedStokIds)
        {
            var updatedStok = await db.Table<StokKart>().FirstOrDefaultAsync(s => s.Id == sId);
            if (updatedStok != null)
            {
                await _syncService.SyncStokAsync(updatedStok);
            }
        }

        // 8. Recalculate and Sync Cari Balance
        if (entity.CariId > 0)
        {
            var cariToRecalc = await db.Table<CariKart>().FirstOrDefaultAsync(c => c.Id == entity.CariId);
            if (cariToRecalc != null)
            {
                var totalBorc = await db.ExecuteScalarAsync<decimal>("SELECT IFNULL(SUM(Borc), 0) FROM CariHareket WHERE CariId = ?", entity.CariId);
                var totalAlacak = await db.ExecuteScalarAsync<decimal>("SELECT IFNULL(SUM(Alacak), 0) FROM CariHareket WHERE CariId = ?", entity.CariId);
                cariToRecalc.Borc = totalBorc;
                cariToRecalc.Alacak = totalAlacak;
                await db.UpdateAsync(cariToRecalc);
                await _syncService.SyncCariAsync(cariToRecalc);
                _dbService.AutoReflectCari(entity.CariId);
            }
        }

        foreach (var sId in affectedStokIds)
        {
            _dbService.AutoReflectStok(sId);
        }

        return 1;
    }

    public override async Task<int> DeleteAsync(int id)
    {
        var entity = await GetByIdAsync(id);
        if (entity == null) return 0;
        return await DeleteAsync(entity);
    }

    public override async Task<List<Fatura>> GetDeletedAsync()
    {
        var db = await GetConnectionAsync();
        return await db.Table<Fatura>()
            .Where(f => f.IsDeleted)
            .ToListAsync();
    }

    public override async Task RestoreAsync(Fatura entity)
    {
        entity.IsDeleted = false;
        await SaveAsync(entity);
    }

    // Özel metodlar
    public async Task<Fatura?> GetByNoAsync(string faturaNo)
    {
        if (string.IsNullOrWhiteSpace(faturaNo)) return null;
        var db = await GetConnectionAsync();
        string clean = faturaNo.Trim();
        if (clean.StartsWith("KPL-", StringComparison.OrdinalIgnoreCase))
        {
            clean = clean.Substring(4).Trim();
        }

        var exact = await db.Table<Fatura>().FirstOrDefaultAsync(f => f.FaturaNo == clean && !f.IsDeleted);
        if (exact != null) return exact;

        var allActive = await db.Table<Fatura>().Where(f => !f.IsDeleted).ToListAsync();
        return allActive.FirstOrDefault(f => !string.IsNullOrEmpty(f.FaturaNo) && f.FaturaNo.Trim().Equals(clean, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<List<Fatura>> GetByCariIdAsync(int cariId)
    {
        var db = await GetConnectionAsync();
        return await db.Table<Fatura>()
            .Where(f => f.CariId == cariId && !f.IsDeleted)
            .OrderByDescending(f => f.Tarih)
            .ToListAsync();
    }

    public async Task<List<Fatura>> GetByTurAsync(string tur)
    {
        var db = await GetConnectionAsync();
        return await db.Table<Fatura>()
            .Where(f => f.Tur == tur && !f.IsDeleted)
            .OrderByDescending(f => f.Tarih)
            .ToListAsync();
    }

    public async Task<List<FaturaDetay>> GetDetaylarAsync(int faturaId)
    {
        var db = await GetConnectionAsync();
        var details = await db.Table<FaturaDetay>()
            .Where(d => d.FaturaId == faturaId)
            .ToListAsync();

        if ((details == null || details.Count == 0) && faturaId > 0)
        {
            try
            {
                var fatura = await db.Table<Fatura>().FirstOrDefaultAsync(f => f.Id == faturaId);
                if (fatura != null && !string.IsNullOrWhiteSpace(fatura.FaturaNo))
                {
                    // Aynı numaraya sahip diğer faturalarda detay var mı?
                    var otherFaturalar = await db.Table<Fatura>()
                        .Where(f => f.FaturaNo == fatura.FaturaNo && f.Id != faturaId)
                        .ToListAsync();

                    foreach (var other in otherFaturalar)
                    {
                        var otherDetails = await db.Table<FaturaDetay>()
                            .Where(d => d.FaturaId == other.Id)
                            .ToListAsync();

                        if (otherDetails != null && otherDetails.Count > 0)
                        {
                            foreach (var od in otherDetails)
                            {
                                od.FaturaId = faturaId;
                                await db.UpdateAsync(od);
                            }
                            return otherDetails;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FaturaRepository] GetDetaylarAsync self-healing error: {ex.Message}");
            }
        }

        return details ?? new List<FaturaDetay>();
    }

    public async Task<List<FaturaDetay>> GetAllDetaylarAsync()
    {
        var db = await GetConnectionAsync();
        return await db.Table<FaturaDetay>().ToListAsync();
    }

    public async Task<int> SaveWithDetailsAsync(Fatura fatura, List<FaturaDetay> detaylar)
    {
        var db = await GetConnectionAsync();
        
        await db.RunInTransactionAsync(tran =>
        {
            // Fatura kaydet
            if (fatura.Id != 0)
            {
                tran.Update(fatura);
            }
            else
            {
                var maxId = tran.ExecuteScalar<int>("SELECT COALESCE(MAX(Id), 0) FROM Fatura");
                fatura.Id = maxId + 1;
                tran.Insert(fatura);
            }

            // Eski detayları sil
            tran.Execute("DELETE FROM FaturaDetay WHERE FaturaId = ?", fatura.Id);

            // Yeni detayları ekle
            foreach (var detay in detaylar)
            {
                detay.FaturaId = fatura.Id;
                tran.Insert(detay);
            }
        });

        // Bulut senkronizasyonu
        await _syncService.SyncFaturaAsync(fatura);
        await _syncService.SyncFaturaDetaylarAsync(fatura.Id, detaylar);
        
        return fatura.Id;
    }

    public async Task<int> SaveWithDetailsAndTransactionAsync(Fatura fatura, List<FaturaDetay> detaylar, bool isSatis = true, bool updateCari = true, bool updateStok = true, bool updateStokPrices = false)
    {
        // This is a simplified alias to SaveWithTransactionAsync
        var db = await GetConnectionAsync();
        var cari = await db.Table<CariKart>().FirstOrDefaultAsync(c => c.Id == fatura.CariId);
        if (cari == null) return 0;

        await SaveWithTransactionAsync(fatura, detaylar, cari, updateCari, updateStok, updateStokPrices);
        return 1;
    }

    public async Task<decimal> GetSumAsync(DateTime? start = null, DateTime? end = null, string? tur = null)
    {
        var db = await GetConnectionAsync();
        
        string query = "SELECT SUM(GenelToplam) FROM Fatura WHERE NOT IsDeleted";
        var args = new List<object>();

        if (start != null) 
        {
            query += " AND Tarih >= ?";
            args.Add(start.Value);
        }
        if (end != null) 
        {
            query += " AND Tarih <= ?";
            args.Add(end.Value);
        }
        if (!string.IsNullOrEmpty(tur))
        {
            if (tur.Equals("Satış", StringComparison.OrdinalIgnoreCase) || tur.Equals("Satis", StringComparison.OrdinalIgnoreCase))
            {
               query += " AND (Tur = 'Satış' OR Tur = 'Satis')";
            }
            else if (tur.Equals("Alış", StringComparison.OrdinalIgnoreCase) || tur.Equals("Alis", StringComparison.OrdinalIgnoreCase))
            {
               query += " AND (Tur = 'Alış' OR Tur = 'Alis')";
            }
            else 
            {
                query += " AND Tur = ?";
                args.Add(tur);
            }
        }

        try 
        {
            return await db.ExecuteScalarAsync<decimal>(query, args.ToArray());
        }
        catch 
        {
            return 0;
        }
    }

    public async Task<int> SaveWithTransactionAsync(Fatura fatura, List<FaturaDetay> detaylar, CariKart cari, bool updateCari = true, bool updateStok = true, bool updateStokPrices = false)
    {
        var db = await GetConnectionAsync();
        var newStokHarekets = new List<StokHareket>();
        var oldCariHareketIdsToDelete = new List<int>();
        var oldStokHareketIdsToDelete = new List<int>();
        CariHareket? newCariHareket = null;

        await db.RunInTransactionAsync(tran => 
        {
            List<CariHareket> existingCariHarekets = new();
            if (fatura.Id != 0) 
            {
                var oldDetails = tran.Query<FaturaDetay>("SELECT * FROM FaturaDetay WHERE FaturaId = ?", fatura.Id);
                var oldFatura = tran.Find<Fatura>(fatura.Id);
                if (oldFatura != null)
                {
                    string oldTur = (oldFatura.Tur ?? "").Trim();
                    bool oldIsSatis = oldTur.Contains("Satış", System.StringComparison.OrdinalIgnoreCase) || 
                                      oldTur.Contains("Satis", System.StringComparison.OrdinalIgnoreCase);

                    // If oldDetails is empty in table, fallback to existing StokHareket
                    if (!oldDetails.Any())
                    {
                        var oldMovements = tran.Query<StokHareket>("SELECT * FROM StokHareket WHERE FaturaId = ? OR EvrakNo = ?", fatura.Id, fatura.FaturaNo ?? "");
                        foreach (var m in oldMovements)
                        {
                            double miktar = m.Miktar > 0 ? (double)m.Miktar : (double)(m.Giren > 0 ? m.Giren : (m.Cikan > 0 ? m.Cikan : 0));
                            oldDetails.Add(new FaturaDetay { FaturaId = fatura.Id, StokId = m.StokId, Miktar = miktar });
                        }
                    }

                    foreach (var od in oldDetails)
                    {
                        var stk = tran.Find<StokKart>(od.StokId);
                        if (stk != null)
                        {
                            if (oldIsSatis) stk.Miktar += od.Miktar;
                            else stk.Miktar -= od.Miktar;
                            tran.Update(stk);
                        }
                    }
                    var cr = tran.Find<CariKart>(oldFatura.CariId);
                    if (cr != null)
                    {
                        if (oldIsSatis) cr.Borc -= oldFatura.GenelToplam;
                        else cr.Alacak -= oldFatura.GenelToplam;
                        tran.Update(cr);
                    }
                }

                tran.Update(fatura);
                tran.Execute("DELETE FROM FaturaDetay WHERE FaturaId = ?", fatura.Id);

                // Collect old stock movements to delete from cloud
                var oldSh = tran.Query<StokHareket>("SELECT * FROM StokHareket WHERE FaturaId = ? OR EvrakNo = ?", fatura.Id, fatura.FaturaNo ?? "");
                foreach (var s in oldSh) oldStokHareketIdsToDelete.Add(s.Id);
                tran.Execute("DELETE FROM StokHareket WHERE FaturaId = ?", fatura.Id);

                // Fetch existing CariHareketler to update in-place instead of deleting and recreating (prevents duplicate cloud sync)
                existingCariHarekets = tran.Query<CariHareket>("SELECT * FROM CariHareket WHERE FaturaId = ? OR (CariId = ? AND EvrakNo IS NOT NULL AND EvrakNo = ?)", fatura.Id, fatura.CariId, fatura.FaturaNo ?? "");

                // Also clean up linked financial movements if updating
                tran.Execute("DELETE FROM KasaHareket WHERE EvrakNo = ? AND CariId = ?", fatura.FaturaNo, fatura.CariId);
                tran.Execute("DELETE FROM BankaHareket WHERE EvrakNo = ? AND CariId = ?", fatura.FaturaNo, fatura.CariId);
            }
            else 
            {
                var maxId = tran.ExecuteScalar<int>("SELECT COALESCE(MAX(Id), 0) FROM Fatura");
                fatura.Id = maxId + 1;
                tran.Insert(fatura);
            }
            
            string curTur = (fatura.Tur ?? "").Trim();
            bool isSatisIade = curTur.Contains("Satış İade", System.StringComparison.OrdinalIgnoreCase) || 
                               curTur.Contains("Satis Iade", System.StringComparison.OrdinalIgnoreCase);
            bool isAlisIade = curTur.Contains("Alış İade", System.StringComparison.OrdinalIgnoreCase) || 
                              curTur.Contains("Alis Iade", System.StringComparison.OrdinalIgnoreCase);
            bool currentIsSatis = !isAlisIade && !isSatisIade && (curTur.Contains("Satış", System.StringComparison.OrdinalIgnoreCase) || 
                                  curTur.Contains("Satis", System.StringComparison.OrdinalIgnoreCase));

            bool isStockInflow = isAlisIade ? false : (isSatisIade ? true : (!currentIsSatis));
            bool isCariBorc = isSatisIade ? false : (isAlisIade ? true : currentIsSatis);

            string stokIslemTuru = isSatisIade ? "Satış İade Faturası" : (isAlisIade ? "Alış İade Faturası" : (currentIsSatis ? "Satış Faturası" : "Alış Faturası"));
            string cariIslemTuru = stokIslemTuru;

            // Automated Payment Movement
            if (fatura.OdemeSekli == "Nakit" && fatura.KasaId.HasValue && fatura.KasaId > 0)
            {
                bool isKasaGiris = isSatisIade ? false : (isAlisIade ? true : currentIsSatis);
                var kasaHareket = new KasaHareket
                {
                    KasaId = fatura.KasaId.Value,
                    FaturaId = fatura.Id,
                    Tarih = fatura.Tarih,
                    EvrakNo = fatura.FaturaNo,
                    CariId = fatura.CariId,
                    CariUnvan = fatura.CariUnvan,
                    IslemTuru = isKasaGiris ? "Tahsilat (Fatura)" : "Ödeme (Fatura)",
                    Aciklama = $"Fatura No: {fatura.FaturaNo} Peşin Nakit",
                    Giren = isKasaGiris ? fatura.GenelToplam : 0,
                    Cikan = !isKasaGiris ? fatura.GenelToplam : 0,
                    TenantId = fatura.TenantId
                };
                tran.Insert(kasaHareket);

                var dbKasa = tran.Find<BankaKart>(fatura.KasaId.Value);
                if (dbKasa != null)
                {
                    dbKasa.Bakiye += (kasaHareket.Giren - kasaHareket.Cikan);
                    tran.Update(dbKasa);
                }
            }
            else if ((fatura.OdemeSekli == "Kredi Kartı" || fatura.OdemeSekli == "Banka Havalesi" || fatura.OdemeSekli == "Banka") && fatura.BankaId.HasValue && fatura.BankaId > 0)
            {
                bool isBankaGiris = isSatisIade ? false : (isAlisIade ? true : currentIsSatis);
                var bankaHareket = new BankaHareket
                {
                    BankaId = fatura.BankaId.Value,
                    FaturaId = fatura.Id,
                    Tarih = fatura.Tarih,
                    EvrakNo = fatura.FaturaNo,
                    CariId = fatura.CariId,
                    CariUnvan = fatura.CariUnvan,
                    IslemTuru = isBankaGiris ? "Tahsilat (Fatura)" : "Ödeme (Fatura)",
                    Aciklama = $"Fatura No: {fatura.FaturaNo} {fatura.OdemeSekli}",
                    Giren = isBankaGiris ? fatura.GenelToplam : 0,
                    Cikan = !isBankaGiris ? fatura.GenelToplam : 0,
                    Tutar = fatura.GenelToplam,
                    TenantId = fatura.TenantId
                };
                tran.Insert(bankaHareket);

                var dbBanka = tran.Find<BankaKart>(fatura.BankaId.Value);
                if (dbBanka != null)
                {
                    dbBanka.Bakiye += (bankaHareket.Giren - bankaHareket.Cikan);
                    tran.Update(dbBanka);
                }
            }

            foreach(var d in detaylar)
            {
                d.FaturaId = fatura.Id;
                tran.Insert(d);

                var stokHareket = new StokHareket
                {
                    StokId = d.StokId,
                    StokKodu = d.StokKodu ?? "",
                    StokAdi = d.StokAdi ?? "",
                    Tarih = fatura.Tarih,
                    IslemTuru = stokIslemTuru,
                    EvrakNo = fatura.FaturaNo,
                    FaturaId = fatura.Id,
                    Miktar = (decimal)d.Miktar,
                    Fiyat = d.BirimFiyat,
                    Giren = isStockInflow ? (decimal)d.Miktar : 0,
                    Cikan = !isStockInflow ? (decimal)d.Miktar : 0,
                    Aciklama = $"Fatura No: {fatura.FaturaNo}",
                    TenantId = fatura.TenantId // Assuming TenantId exists on Fatura
                };
                tran.Insert(stokHareket);
                newStokHarekets.Add(stokHareket);

                if (updateStok)
                {
                    var stok = tran.Find<StokKart>(d.StokId);
                    if(stok != null)
                    {
                        if(isStockInflow) stok.Miktar += d.Miktar;
                        else stok.Miktar -= d.Miktar;

                        if (updateStokPrices)
                        {
                            if (!currentIsSatis) stok.AlisFiyati = d.BirimFiyat; 
                            else stok.SatisFiyati = d.BirimFiyat;
                        }
                        tran.Update(stok);
                    }
                }
            }

            if (updateCari)
            {
                var cr = tran.Find<CariKart>(fatura.CariId);
                if (cr != null)
                {
                    if (isCariBorc) cr.Borc += fatura.GenelToplam;
                    else cr.Alacak += fatura.GenelToplam;
                    tran.Update(cr);

                    if (existingCariHarekets != null && existingCariHarekets.Count > 0)
                    {
                        // Update existing movement in-place to maintain stable ID
                        var cariHareket = existingCariHarekets[0];
                        cariHareket.CariId = fatura.CariId;
                        cariHareket.CariUnvan = fatura.CariUnvan;
                        cariHareket.Tarih = fatura.Tarih;
                        cariHareket.IslemTuru = currentIsSatis ? "Satış Faturası" : "Alış Faturası";
                        cariHareket.Borc = currentIsSatis ? fatura.GenelToplam : 0;
                        cariHareket.Alacak = !currentIsSatis ? fatura.GenelToplam : 0;
                        cariHareket.Aciklama = $"Fatura No: {fatura.FaturaNo}";
                        cariHareket.EvrakNo = fatura.FaturaNo;
                        cariHareket.FaturaId = fatura.Id;
                        cariHareket.Vade = fatura.VadeTarihi;
                        tran.Update(cariHareket);
                        newCariHareket = cariHareket;

                        // Delete any redundant duplicate movements beyond the first one
                        for (int i = 1; i < existingCariHarekets.Count; i++)
                        {
                            tran.Delete(existingCariHarekets[i]);
                            oldCariHareketIdsToDelete.Add(existingCariHarekets[i].Id);
                        }
                    }
                    else
                    {
                        var cariHareket = new CariHareket
                        {
                            CariId = fatura.CariId,
                            CariUnvan = fatura.CariUnvan,
                            Tarih = fatura.Tarih,
                            IslemTuru = cariIslemTuru,
                            Borc = isCariBorc ? fatura.GenelToplam : 0,
                            Alacak = !isCariBorc ? fatura.GenelToplam : 0,
                            Aciklama = $"Fatura No: {fatura.FaturaNo}",
                            EvrakNo = fatura.FaturaNo,
                            FaturaId = fatura.Id,
                            Vade = fatura.VadeTarihi
                        };
                        tran.Insert(cariHareket);
                        newCariHareket = cariHareket;
                    }
                }
            }
            else
            {
                if (existingCariHarekets != null)
                {
                    foreach (var ch in existingCariHarekets)
                    {
                        tran.Delete(ch);
                        oldCariHareketIdsToDelete.Add(ch.Id);
                    }
                }
            }

            // RECALCULATE STOCK COSTS AFTER ALL MOVEMENTS SAVED
            foreach(var d in detaylar)
            {
                _recalculateStockCostInternal(tran, d.StokId);
            }
        });

        // Delete superseded old movements from cloud to avoid resurrecting duplicates
        foreach (var chId in oldCariHareketIdsToDelete)
        {
            await _syncService.DeleteCariHareketAsync(chId);
        }
        foreach (var shId in oldStokHareketIdsToDelete)
        {
            await _syncService.DeleteStokHareketAsync(shId);
        }

        await _dbService.RecalculateCariBalanceAsync(fatura.CariId);
        await _syncService.SyncFaturaAsync(fatura);
        await _syncService.SyncFaturaDetaylarAsync(fatura.Id, detaylar);

        if (newCariHareket != null)
        {
            await _syncService.SyncCariHareketAsync(newCariHareket);
        }

        foreach (var sh in newStokHarekets)
        {
            await _syncService.SyncStokHareketAsync(sh);
        }

        if (updateCari && fatura.CariId > 0)
        {
            _dbService.AutoReflectCari(fatura.CariId);
        }

        if (updateStok && detaylar != null)
        {
            var updatedStokIds = detaylar.Select(d => d.StokId).Distinct().ToList();
            var dbConn = await GetConnectionAsync();
            foreach (var sId in updatedStokIds)
            {
                var stk = await dbConn.Table<StokKart>().FirstOrDefaultAsync(s => s.Id == sId);
                if (stk != null)
                {
                    await _syncService.SyncStokAsync(stk);
                }
                _dbService.AutoReflectStok(sId);
            }
        }

        return fatura.Id;
    }

    private void _recalculateStockCostInternal(SQLiteConnection tran, int stokId)
    {
        var stok = tran.Table<StokKart>().FirstOrDefault(s => s.Id == stokId);
        if (stok == null) return;
        
        var movements = tran.Table<StokHareket>()
            .Where(h => h.StokId == stokId)
            .OrderBy(h => h.Tarih)
            .ThenBy(h => h.Id)
            .ToList();
        
        decimal currentQuantity = 0;
        decimal currentTotalValue = 0;
        decimal averagePrice = 0;
        
        decimal totalSoldQuantity = 0;
        decimal totalSalesRevenue = 0;
        decimal averageSalesPrice = 0;

        foreach (var m in movements)
        {
            // Primary check: numeric flags Giren/Cikan
            bool isGiris = m.Giren > 0;
            bool isCikis = m.Cikan > 0;

            // Secondary check: Fallback to string matching if numeric fields are zero/empty
            if (!isGiris && !isCikis)
            {
                string tur = (m.IslemTuru ?? "").ToUpper(System.Globalization.CultureInfo.InvariantCulture);
                isGiris = (tur.Contains("GİRİŞ") || tur.Contains("ALIS") || tur.Contains("ALIŞ") || tur.Contains("ACILIS") || tur.Contains("AÇILIŞ") || tur.Contains("GİREN"));
                isCikis = (tur.Contains("ÇIKIŞ") || tur.Contains("CIKIS") || tur.Contains("SATIS") || tur.Contains("SATIŞ") || tur.Contains("ÇIKAN"));
                
                // Extra safety for Turkish characters with OrdinalIgnoreCase
                if (!isGiris && !isCikis)
                {
                    string t = m.IslemTuru ?? "";
                    isGiris = t.Contains("Giriş", StringComparison.OrdinalIgnoreCase) || t.Contains("Alış", StringComparison.OrdinalIgnoreCase) || t.Contains("Açılış", StringComparison.OrdinalIgnoreCase);
                    isCikis = t.Contains("Çıkış", StringComparison.OrdinalIgnoreCase) || t.Contains("Satış", StringComparison.OrdinalIgnoreCase);
                }
            }

            if (isGiris) 
            {
                decimal qty = m.Miktar > 0 ? m.Miktar : (m.Giren > 0 ? m.Giren : 0);
                decimal price = m.Fiyat;

                if (qty > 0)
                {
                    currentTotalValue += (qty * price);
                    currentQuantity += qty;
                    
                    if (currentQuantity > 0)
                        averagePrice = currentTotalValue / currentQuantity;
                    else
                    {
                         averagePrice = price; 
                         currentTotalValue = 0;
                    }
                }
            }
            else if (isCikis)
            {
                decimal qty = m.Miktar > 0 ? m.Miktar : (m.Cikan > 0 ? m.Cikan : 0);
                
                if (qty > 0)
                {
                    currentTotalValue -= (qty * averagePrice);
                    currentQuantity -= qty;

                    decimal salePrice = m.Fiyat;
                    totalSalesRevenue += (qty * salePrice);
                    totalSoldQuantity += qty;
                    
                    if (totalSoldQuantity > 0)
                        averageSalesPrice = totalSalesRevenue / totalSoldQuantity;
                }
            }
        }

        var lastPurchase = movements.Where(x => {
             var t = (x.IslemTuru ?? "").ToUpperInvariant();
             return t.Contains("GİRİŞ") || t.Contains("ALIS") || t.Contains("ALIŞ") || t.Contains("ACILIS") || t.Contains("AÇILIŞ");
        }).LastOrDefault();

        var lastSale = movements.Where(x => {
             var t = (x.IslemTuru ?? "").ToUpperInvariant();
             return t.Contains("ÇIKIŞ") || t.Contains("CIKIS") || t.Contains("SATIS") || t.Contains("SATIŞ");
        }).LastOrDefault();

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
            stok.OrtalamaAlisFiyati = averagePrice;
            stok.OrtalamaSatisFiyati = averageSalesPrice;
            if (lastPurchase != null) stok.AlisFiyati = lastPurchase.Fiyat;
            if (lastSale != null) stok.SatisFiyati = lastSale.Fiyat;
        }

        tran.Update(stok);
    }

    private async Task<int> SoftDeleteAsync(Fatura entity)
    {
        entity.IsDeleted = true;
        return await SaveAsync(entity);
    }
}

