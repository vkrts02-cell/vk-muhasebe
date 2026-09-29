using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ErmayMuhasebe.Models;
using ErmayMuhasebe.Services;
using SQLite;

namespace ErmayMuhasebe.Repositories;

/// <summary>
/// Stok Kart Repository
/// Stok işlemlerini yönetir
/// </summary>
public class StokRepository : BaseRepository<StokKart>, IStokRepository
{
    public StokRepository(DatabaseService dbService) : base(dbService)
    {
    }

    public override async Task<List<StokKart>> GetAllAsync()
    {
        var db = await GetConnectionAsync();
        return await db.Table<StokKart>()
            .Where(s => !s.IsDeleted)
            .OrderBy(s => s.StokAdi)
            .ToListAsync();
    }

    public override async Task<StokKart?> GetByIdAsync(int id)
    {
        var db = await GetConnectionAsync();
        return await db.Table<StokKart>()
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);
    }

    public override async Task<int> SaveAsync(StokKart entity)
    {
        var db = await GetConnectionAsync();
        
        if (entity.Id != 0)
        {
            var existing = await db.Table<StokKart>().FirstOrDefaultAsync(s => s.Id == entity.Id);
            if (existing != null && existing.Version != entity.Version)
            {
                throw new System.Exception("Çakışma Tespit Edildi! Bu stok kartı başka bir yerde güncellenmiş. Lütfen sayfayı yenileyip tekrar deneyin.");
            }
            entity.Version++;
            entity.UpdatedAt = DateTime.Now;
            await db.UpdateAsync(entity);
        }
        else
        {
            var maxId = await db.ExecuteScalarAsync<int>("SELECT IFNULL(MAX(Id), 0) FROM StokKart");
            entity.Id = maxId + 1;
            entity.Version = 1;
            entity.UpdatedAt = DateTime.Now;
            await db.InsertAsync(entity);
        }

        // Bulut senkronizasyonu
        await _syncService.SyncStokAsync(entity);
        _dbService.AutoReflectStok(entity.Id);
        
        return entity.Id;
    }

    public override async Task<int> DeleteAsync(StokKart entity)
    {
        return await SoftDeleteAsync(entity);
    }

    public override async Task<int> DeleteAsync(int id)
    {
        var db = await GetConnectionAsync();
        
        // Cascade delete: Stok hareketlerini de sil
        await db.ExecuteAsync("DELETE FROM StokHareket WHERE StokId = ?", id);
        
        // Stok kartını sil
        int result = await db.ExecuteAsync("DELETE FROM StokKart WHERE Id = ?", id);
        
        // Bulut senkronizasyonu
        await _syncService.DeleteStokAsync(id);
        
        return result;
    }

    public override async Task<List<StokKart>> GetDeletedAsync()
    {
        var db = await GetConnectionAsync();
        return await db.Table<StokKart>()
            .Where(s => s.IsDeleted)
            .ToListAsync();
    }

    public override async Task RestoreAsync(StokKart entity)
    {
        entity.IsDeleted = false;
        await SaveAsync(entity);
    }

    // Özel metodlar
    public async Task<StokKart?> GetByKodAsync(string kod)
    {
        var db = await GetConnectionAsync();
        return await db.Table<StokKart>()
            .FirstOrDefaultAsync(s => s.StokKodu == kod && !s.IsDeleted);
    }

    public async Task<StokKart?> GetByBarkodAsync(string barkod)
    {
        var db = await GetConnectionAsync();
        return await db.Table<StokKart>()
            .FirstOrDefaultAsync(s => s.Barkod == barkod && !s.IsDeleted);
    }

    public async Task<List<StokKart>> GetByKategoriAsync(string kategori)
    {
        var db = await GetConnectionAsync();
        return await db.Table<StokKart>()
            .Where(s => s.Kategori == kategori && !s.IsDeleted)
            .OrderBy(s => s.StokAdi)
            .ToListAsync();
    }

    public async Task<List<StokKart>> GetKritikStoklarAsync()
    {
        var db = await GetConnectionAsync();
        return await db.Table<StokKart>()
            .Where(s => s.Miktar <= s.MinSeviye && !s.IsDeleted)
            .OrderBy(s => s.Miktar)
            .ToListAsync();
    }

    public async Task<List<StokHareket>> GetHareketlerAsync(int stokId)
    {
        var db = await GetConnectionAsync();
        return await db.Table<StokHareket>()
            .Where(h => h.StokId == stokId)
            .OrderByDescending(h => h.Tarih)
            .ToListAsync();
    }

    public async Task<int> SaveHareketAsync(StokHareket hareket)
    {
        var db = await GetConnectionAsync();
        int result = hareket.Id != 0 ? await db.UpdateAsync(hareket) : await db.InsertAsync(hareket);
        await _syncService.SyncStokHareketAsync(hareket);
        _dbService.AutoReflectStok(hareket.StokId);
        return result;
    }

    public async Task<int> DeleteHareketAsync(StokHareket hareket)
    {
        var db = await GetConnectionAsync();
        int result = await db.DeleteAsync(hareket);
        await _syncService.DeleteStokHareketAsync(hareket.Id);

        var currentStok = await GetByIdAsync(hareket.StokId);
        if (currentStok != null)
        {
            var remainingMovements = await GetHareketlerAsync(hareket.StokId);
            if (!remainingMovements.Any())
            {
                currentStok.Miktar = 0;
                currentStok.OrtalamaAlisFiyati = 0;
                currentStok.OrtalamaSatisFiyati = 0;
            }
            else
            {
                double sumGiren = (double)remainingMovements.Sum(h => h.Giren > 0 ? h.Giren : (h.Miktar > 0 && ((h.IslemTuru ?? "").Contains("Giriş", StringComparison.OrdinalIgnoreCase) || (h.IslemTuru ?? "").Contains("Alış", StringComparison.OrdinalIgnoreCase) || (h.IslemTuru ?? "").Contains("Açılış", StringComparison.OrdinalIgnoreCase)) ? h.Miktar : 0));
                double sumCikan = (double)remainingMovements.Sum(h => h.Cikan > 0 ? h.Cikan : (h.Miktar > 0 && ((h.IslemTuru ?? "").Contains("Çıkış", StringComparison.OrdinalIgnoreCase) || (h.IslemTuru ?? "").Contains("Satış", StringComparison.OrdinalIgnoreCase)) ? h.Miktar : 0));
                currentStok.Miktar = sumGiren - sumCikan;
            }
            await db.UpdateAsync(currentStok);
            await _syncService.SyncStokAsync(currentStok);
        }

        await RecalculateCostsAsync(hareket.StokId);
        _dbService.AutoReflectStok(hareket.StokId);
        return result;
    }

    public async Task<List<StokHareket>> GetAllHareketlerAsync()
    {
        var db = await GetConnectionAsync();
        return await db.Table<StokHareket>()
            .OrderByDescending(h => h.Tarih)
            .ToListAsync();
    }

    public async Task RecalculateCostsAsync(int? stokId = null)
    {
        var db = await GetConnectionAsync();
        List<StokKart> changedStoks = new();
        await db.RunInTransactionAsync(tran => 
        {
            changedStoks = _recalculateStockCostInternal(tran, stokId);
        });

        foreach (var s in changedStoks)
        {
            await _syncService.SyncStokAsync(s);
        }
    }

    private List<StokKart> _recalculateStockCostInternal(SQLiteConnection tran, int? specificStokId = null)
    {
        var changedStoks = new List<StokKart>();
        var allStoklar = specificStokId.HasValue 
            ? tran.Table<StokKart>().Where(s => s.Id == specificStokId.Value).ToList()
            : tran.Table<StokKart>().ToList();
        
        var allHareketler = specificStokId.HasValue
            ? tran.Table<StokHareket>().Where(h => h.StokId == specificStokId.Value).OrderBy(h => h.Tarih).ThenBy(h => h.Id).ToList()
            : tran.Table<StokHareket>().OrderBy(h => h.Tarih).ThenBy(h => h.Id).ToList();

        foreach (var stok in allStoklar)
        {
            var movements = allHareketler.Where(x => x.StokId == stok.Id).ToList();
            
            decimal currentQuantity = 0;
            decimal currentTotalValue = 0;
            decimal averagePrice = 0;
            
            decimal totalSoldQuantity = 0;
            decimal totalSalesRevenue = 0;
            decimal averageSalesPrice = 0;

            foreach (var m in movements)
            {
                // Enhanced movement identification logic
                bool isGiris = false;
                bool isCikis = false;

                // Prioritize numeric flags if available
                if (m.Giren > 0) isGiris = true;
                else if (m.Cikan > 0) isCikis = true;
                // Fallback to string matching if numeric fields are zero/empty
                else
                {
                    string tur = (m.IslemTuru ?? "").ToUpper(System.Globalization.CultureInfo.InvariantCulture);
                    if (tur.Contains("GİRİŞ") || tur.Contains("ALIS") || tur.Contains("ALIŞ") || tur.Contains("ACILIS") || tur.Contains("AÇILIŞ") || tur.Contains("GİREN"))
                        isGiris = true;
                    else if (tur.Contains("ÇIKIŞ") || tur.Contains("CIKIS") || tur.Contains("SATIS") || tur.Contains("SATIŞ") || tur.Contains("ÇIKAN"))
                        isCikis = true;

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
                        // If current quantity is negative or zero, this purchase starts a new basis for cost
                        if (currentQuantity <= 0)
                        {
                            currentQuantity = 0;
                            currentTotalValue = 0;
                        }

                        currentTotalValue += (qty * price);
                        currentQuantity += qty;
                        
                        if (currentQuantity > 0)
                        {
                            averagePrice = currentTotalValue / currentQuantity;
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

                        // Calculate Sales Stats
                        decimal salePrice = m.Fiyat;
                        totalSalesRevenue += (qty * salePrice);
                        totalSoldQuantity += qty;
                        
                        if (totalSoldQuantity > 0)
                            averageSalesPrice = totalSalesRevenue / totalSoldQuantity;
                    }
                }
            }

            bool changed = false;
            decimal lastPurchasePrice = 0;
            decimal lastSalesPrice = 0;
            var lastPurchase = movements.Where(x => {
                 var t = (x.IslemTuru ?? "").ToUpperInvariant();
                 return t.Contains("GİRİŞ") || t.Contains("ALIS") || t.Contains("ALIŞ") || t.Contains("ACILIS") || t.Contains("AÇILIŞ");
            }).LastOrDefault();
            if (lastPurchase != null) lastPurchasePrice = lastPurchase.Fiyat;

            var lastSale = movements.Where(x => {
                 var t = (x.IslemTuru ?? "").ToUpperInvariant();
                 return t.Contains("ÇIKIŞ") || t.Contains("CIKIS") || t.Contains("SATIS") || t.Contains("SATIŞ");
            }).LastOrDefault();
            if (lastSale != null) lastSalesPrice = lastSale.Fiyat;

            // If no movements exist, reset quantity and average prices completely
            if (!movements.Any())
            {
                if (stok.Miktar != 0)
                {
                    stok.Miktar = 0;
                    changed = true;
                }
                if (stok.OrtalamaAlisFiyati != 0)
                {
                    stok.OrtalamaAlisFiyati = 0;
                    changed = true;
                }
                if (stok.OrtalamaSatisFiyati != 0)
                {
                    stok.OrtalamaSatisFiyati = 0;
                    changed = true;
                }
                if (stok.AlisFiyati != 0)
                {
                    stok.AlisFiyati = 0;
                    changed = true;
                }
                if (stok.SatisFiyati != 0)
                {
                    stok.SatisFiyati = 0;
                    changed = true;
                }
            }
            else
            {
                double sumGiren = (double)movements.Sum(h => h.Giren > 0 ? h.Giren : (h.Miktar > 0 && ((h.IslemTuru ?? "").Contains("Giriş", StringComparison.OrdinalIgnoreCase) || (h.IslemTuru ?? "").Contains("Alış", StringComparison.OrdinalIgnoreCase) || (h.IslemTuru ?? "").Contains("Açılış", StringComparison.OrdinalIgnoreCase)) ? h.Miktar : 0));
                double sumCikan = (double)movements.Sum(h => h.Cikan > 0 ? h.Cikan : (h.Miktar > 0 && ((h.IslemTuru ?? "").Contains("Çıkış", StringComparison.OrdinalIgnoreCase) || (h.IslemTuru ?? "").Contains("Satış", StringComparison.OrdinalIgnoreCase)) ? h.Miktar : 0));
                double computedMiktar = sumGiren - sumCikan;
                if (Math.Abs(stok.Miktar - computedMiktar) > 0.0001)
                {
                    stok.Miktar = computedMiktar;
                    changed = true;
                }

                if (stok.OrtalamaAlisFiyati != averagePrice)
                {
                     stok.OrtalamaAlisFiyati = averagePrice;
                     changed = true;
                }
                if (stok.OrtalamaSatisFiyati != averageSalesPrice)
                {
                    stok.OrtalamaSatisFiyati = averageSalesPrice;
                    changed = true;
                }
                if (lastPurchase != null && stok.AlisFiyati != lastPurchasePrice)
                {
                    stok.AlisFiyati = lastPurchasePrice;
                    changed = true;
                }
                if (lastSale != null && stok.SatisFiyati != lastSalesPrice)
                {
                    stok.SatisFiyati = lastSalesPrice;
                    changed = true;
                }
            }

            if (changed) 
            {
                tran.Update(stok);
                changedStoks.Add(stok);
            }
        }
        return changedStoks;
    }

    public async Task MergeStokAsync(int kaynakStokId, int hedefStokId)
    {
        var db = await GetConnectionAsync();
        
        var hedef = await GetByIdAsync(hedefStokId);
        var kaynak = await GetByIdAsync(kaynakStokId);
        if (hedef != null && kaynak != null)
        {
            await db.RunInTransactionAsync(tran => 
            {
                // Update StokHareket
                var hareketler = tran.Table<StokHareket>().Where(h => h.StokId == kaynakStokId).ToList();
                foreach (var h in hareketler)
                {
                    h.StokId = hedefStokId;
                    h.StokKodu = hedef.StokKodu;
                    h.StokAdi = hedef.StokAdi;
                    tran.Update(h);
                }

                // Update FaturaDetay
                var faturaDetaylar = tran.Table<FaturaDetay>().Where(fd => fd.StokId == kaynakStokId).ToList();
                foreach (var fd in faturaDetaylar)
                {
                    fd.StokId = hedefStokId;
                    fd.StokKodu = hedef.StokKodu;
                    fd.StokAdi = hedef.StokAdi;
                    tran.Update(fd);
                }

                // Update SiparisDetay
                var siparisDetaylar = tran.Table<SiparisDetay>().Where(sd => sd.StokId == kaynakStokId).ToList();
                foreach (var sd in siparisDetaylar)
                {
                    sd.StokId = hedefStokId;
                    sd.StokAdi = hedef.StokAdi;
                    tran.Update(sd);
                }

                // Update TeklifDetay
                var teklifDetaylar = tran.Table<TeklifDetay>().Where(td => td.StokId == kaynakStokId).ToList();
                foreach (var td in teklifDetaylar)
                {
                    td.StokId = hedefStokId;
                    td.StokAdi = hedef.StokAdi;
                    tran.Update(td);
                }

                // Update StokSayimDetay
                var sayimDetaylar = tran.Table<StokSayimDetay>().Where(s => s.StokId == kaynakStokId).ToList();
                foreach (var s in sayimDetaylar)
                {
                    s.StokId = hedefStokId;
                    s.StokKodu = hedef.StokKodu;
                    s.StokAdi = hedef.StokAdi;
                    tran.Update(s);
                }

                hedef.Miktar += kaynak.Miktar;
                tran.Update(hedef);
            });

            await DeleteAsync(kaynakStokId);
            await RecalculateCostsAsync(hedefStokId);
        }
    }

    public async Task<List<string>> GetGruplarAsync()
    {
        var db = await GetConnectionAsync();
        var definedGroups = await db.Table<StokGrupDef>()
            .Where(g => !g.IsDeleted && g.Ad != null && g.Ad != "")
            .ToListAsync();
        
        var stockCategories = await db.Table<StokKart>()
            .Where(s => !s.IsDeleted && s.Kategori != null && s.Kategori != "")
            .ToListAsync();

        var groupNames = definedGroups.Select(g => g.Ad.Trim())
            .Union(stockCategories.Select(s => s.Kategori!.Trim()))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name)
            .ToList();

        return groupNames;
    }

    public async Task<int> SaveGrupAsync(string grupAdi)
    {
        if (string.IsNullOrWhiteSpace(grupAdi)) return 0;
        var trimmed = grupAdi.Trim();
        var db = await GetConnectionAsync();
        
        var existing = await db.Table<StokGrupDef>().FirstOrDefaultAsync(g => g.Ad == trimmed && !g.IsDeleted);
        if (existing != null) return existing.Id;

        var maxId = await db.ExecuteScalarAsync<int>("SELECT IFNULL(MAX(Id), 0) FROM StokGrupDef");
        var def = new StokGrupDef
        {
            Id = maxId + 1,
            Ad = trimmed,
            UpdatedAt = DateTime.Now,
            IsDeleted = false
        };
        await db.InsertAsync(def);
        await _syncService.SyncStokGrupAsync(def);
        return def.Id;
    }

    public async Task<int> DeleteGrupAsync(string grupAdi)
    {
        if (string.IsNullOrWhiteSpace(grupAdi)) return 0;
        var trimmed = grupAdi.Trim();
        var db = await GetConnectionAsync();
        
        var existing = await db.Table<StokGrupDef>().FirstOrDefaultAsync(g => g.Ad == trimmed && !g.IsDeleted);
        if (existing != null)
        {
            existing.IsDeleted = true;
            existing.UpdatedAt = DateTime.Now;
            await db.UpdateAsync(existing);
            await _syncService.DeleteStokGrupAsync(existing.Id);
            return existing.Id;
        }
        return 0;
    }

    private async Task<int> SoftDeleteAsync(StokKart entity)
    {
        entity.IsDeleted = true;
        return await SaveAsync(entity);
    }
}

