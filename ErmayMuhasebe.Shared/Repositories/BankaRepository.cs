using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ErmayMuhasebe.Models;
using ErmayMuhasebe.Services;
using SQLite;

namespace ErmayMuhasebe.Repositories;

/// <summary>
/// Banka Kart Repository
/// Banka işlemlerini yönetir
/// </summary>
public class BankaRepository : BaseRepository<BankaKart>, IBankaRepository
{
    public BankaRepository(DatabaseService dbService) : base(dbService)
    {
    }

    public override async Task<List<BankaKart>> GetAllAsync()
    {
        var db = await GetConnectionAsync();
        return await db.Table<BankaKart>()
            .OrderBy(b => b.BankaAdi)
            .ToListAsync();
    }

    public override async Task<BankaKart?> GetByIdAsync(int id)
    {
        var db = await GetConnectionAsync();
        return await db.Table<BankaKart>()
            .FirstOrDefaultAsync(b => b.Id == id);
    }

    public override async Task<int> SaveAsync(BankaKart entity)
    {
        var db = await GetConnectionAsync();
        
        if (entity.Id != 0)
        {
            await db.UpdateAsync(entity);
        }
        else
        {
            var maxId = await db.ExecuteScalarAsync<int>("SELECT IFNULL(MAX(Id), 0) FROM BankaKart");
            entity.Id = maxId + 1;
            await db.InsertAsync(entity);
        }

        // Bulut senkronizasyonu
        await _syncService.SyncBankaAsync(entity);
        
        _dbService.AutoReflectKasa(entity.Id);

        return entity.Id;
    }

    public override async Task<int> DeleteAsync(BankaKart entity)
    {
        var db = await GetConnectionAsync();
        return await db.DeleteAsync(entity);
    }

    public override async Task<int> DeleteAsync(int id)
    {
        var db = await GetConnectionAsync();
        return await db.ExecuteAsync("DELETE FROM BankaKart WHERE Id = ?", id);
    }

    public override Task<List<BankaKart>> GetDeletedAsync()
    {
        // BankaKart'ta IsDeleted yok, boş liste döndür
        return Task.FromResult(new List<BankaKart>());
    }

    public override async Task RestoreAsync(BankaKart entity)
    {
        // BankaKart'ta soft delete yok, restore işlemi yok
        await Task.CompletedTask;
    }

    // Özel metodlar
    public async Task<List<BankaHareket>> GetHareketlerAsync(int bankaId)
    {
        var db = await GetConnectionAsync();
        return await db.Table<BankaHareket>()
            .Where(h => h.BankaId == bankaId)
            .OrderByDescending(h => h.Tarih)
            .ToListAsync();
    }

    public async Task<List<BankaHareket>> GetAllHareketlerAsync()
    {
        var db = await GetConnectionAsync();
        return await db.Table<BankaHareket>().ToListAsync();
    }

    public async Task<decimal> GetBakiyeAsync(int bankaId)
    {
        var db = await GetConnectionAsync();
        var hareketler = await db.Table<BankaHareket>()
            .Where(h => h.BankaId == bankaId)
            .ToListAsync();
        
        return hareketler.Sum(h => h.Tutar);
    }

    public async Task<int> SaveHareketAsync(BankaHareket hareket)
    {
        var db = await GetConnectionAsync();
        
        BankaKart? impactedBanka = null;
        CariHareket? impactedCariHareket = null;
        CariKart? impactedCari = null;
        
        await db.RunInTransactionAsync(tran => 
        {
            var banka = tran.Find<BankaKart>(hareket.BankaId);
            if (banka != null)
            {
                if (hareket.Id != 0) // UPDATE
                {
                    var oldItem = tran.Find<BankaHareket>(hareket.Id);
                    if (oldItem != null)
                    {
                        banka.GuncelBakiye -= (oldItem.Giren - oldItem.Cikan);
                        
                        if (!string.IsNullOrEmpty(oldItem.EvrakNo))
                        {
                            var linkedCariHareket = tran.Table<CariHareket>().Where(c => c.EvrakNo == oldItem.EvrakNo).FirstOrDefault();
                            if (linkedCariHareket != null)
                            {
                                var cari = tran.Find<CariKart>(linkedCariHareket.CariId);
                                if (cari != null)
                                {
                                    cari.Borc -= linkedCariHareket.Borc;
                                    cari.Alacak -= linkedCariHareket.Alacak;
                                }

                                linkedCariHareket.Tarih = hareket.Tarih;
                                linkedCariHareket.Aciklama = "Banka - " + hareket.Aciklama;
                                
                                if (hareket.Giren > 0)
                                {
                                    linkedCariHareket.Alacak = hareket.Giren;
                                    linkedCariHareket.Borc = 0;
                                    linkedCariHareket.IslemTuru = "Gelen Havale";
                                }
                                else
                                {
                                    linkedCariHareket.Borc = hareket.Cikan;
                                    linkedCariHareket.Alacak = 0;
                                    linkedCariHareket.IslemTuru = "Giden Havale";
                                }
                                
                                if (cari != null)
                                {
                                    cari.Borc += linkedCariHareket.Borc;
                                    cari.Alacak += linkedCariHareket.Alacak;
                                    tran.Update(cari);
                                    impactedCari = cari;
                                }
                                
                                tran.Update(linkedCariHareket);
                                impactedCariHareket = linkedCariHareket;
                            }
                        }
                    }
                }

                banka.GuncelBakiye += (hareket.Giren - hareket.Cikan);
                tran.Update(banka);
                impactedBanka = banka;
            }

            if (hareket.Id != 0) tran.Update(hareket); else tran.Insert(hareket);
        });

        await _syncService.SyncBankaHareketAsync(hareket);
        if(impactedBanka != null) await _syncService.SyncBankaAsync(impactedBanka);
        if(impactedCariHareket != null) await _syncService.SyncCariHareketAsync(impactedCariHareket);
        if(impactedCari != null) await _syncService.SyncCariAsync(impactedCari);

        if (hareket.BankaId > 0) _dbService.AutoReflectKasa(hareket.BankaId);

        return hareket.Id;
    }

    public async Task<int> DeleteHareketAsync(BankaHareket hareket)
    {
        var db = await GetConnectionAsync();
        if (hareket == null || hareket.Id == 0) return 0;

        if (!string.IsNullOrEmpty(hareket.EvrakNo))
        {
            var cariHareket = await db.Table<CariHareket>().Where(c => c.EvrakNo == hareket.EvrakNo).FirstOrDefaultAsync();
            if (cariHareket != null)
            {
                var cari = await db.Table<CariKart>().FirstOrDefaultAsync(x => x.Id == cariHareket.CariId);
                if (cari != null)
                {
                    cari.Borc -= cariHareket.Borc;
                    cari.Alacak -= cariHareket.Alacak;
                    await db.UpdateAsync(cari);
                    await _syncService.SyncCariAsync(cari);
                }
                await db.DeleteAsync(cariHareket);
                await _syncService.DeleteCariHareketAsync(cariHareket.Id);
            }
        }

        await _syncService.DeleteBankaHareketAsync(hareket.Id);
        int result = await db.DeleteAsync(hareket);

        if (hareket.BankaId > 0)
        {
            await RecalculateBalanceAsync(hareket.BankaId);
            var updatedBanka = await GetByIdAsync(hareket.BankaId);
            if (updatedBanka != null)
            {
                await _syncService.SyncBankaAsync(updatedBanka);
            }
            _dbService.AutoReflectKasa(hareket.BankaId);
        }

        return result;
    }

    public async Task<int> DeleteHareketAsync(int id)
    {
        var db = await GetConnectionAsync();
        var item = await db.Table<BankaHareket>().FirstOrDefaultAsync(x => x.Id == id);
        if (item == null) return 0;
        return await DeleteHareketAsync(item);
    }

    public async Task<int> RecalculateBalanceAsync(int bankaId)
    {
        var db = await GetConnectionAsync();
        var banka = await GetByIdAsync(bankaId);
        if (banka == null) return 0;

        decimal bakiye = banka.AcilisBakiyesi;
        if (banka.KartTuru == "Kasa")
        {
            var movements = await db.Table<KasaHareket>().Where(h => h.KasaId == bankaId).ToListAsync();
            bakiye += movements.Sum(h => h.Giren - h.Cikan);
        }
        else
        {
            var movements = await db.Table<BankaHareket>().Where(h => h.BankaId == bankaId).ToListAsync();
            bakiye += movements.Sum(h => h.Giren - h.Cikan);
        }

        banka.GuncelBakiye = bakiye;
        await db.UpdateAsync(banka);
        return 1;
    }
}

