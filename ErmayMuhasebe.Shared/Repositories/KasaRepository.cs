using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ErmayMuhasebe.Models;
using ErmayMuhasebe.Services;
using SQLite;

namespace ErmayMuhasebe.Repositories;

/// <summary>
/// Kasa Repository
/// Kasa işlemlerini yönetir
/// </summary>
public class KasaRepository : BaseRepository<KasaHareket>, IKasaRepository
{
    public KasaRepository(DatabaseService dbService) : base(dbService)
    {
    }

    public override async Task<List<KasaHareket>> GetAllAsync()
    {
        var db = await GetConnectionAsync();
        return await db.Table<KasaHareket>()
            .OrderByDescending(h => h.Tarih)
            .ToListAsync();
    }

    public override async Task<KasaHareket?> GetByIdAsync(int id)
    {
        var db = await GetConnectionAsync();
        return await db.Table<KasaHareket>()
            .FirstOrDefaultAsync(h => h.Id == id);
    }

    public override async Task<int> SaveAsync(KasaHareket entity)
    {
        var db = await GetConnectionAsync();
        if (entity.Id != 0)
        {
            await db.UpdateAsync(entity);
        }
        else
        {
            await db.InsertAsync(entity);
        }

        // Bulut senkronizasyonu
        await _syncService.SyncKasaHareketAsync(entity);
        
        if (entity.KasaId > 0) _dbService.AutoReflectKasa(entity.KasaId);

        return entity.Id;
    }

    public override async Task<int> DeleteAsync(KasaHareket entity)
    {
        var db = await GetConnectionAsync();
        if (entity != null && entity.Id > 0)
        {
            await _syncService.DeleteKasaHareketAsync(entity.Id);
            if (entity.KasaId > 0) _dbService.AutoReflectKasa(entity.KasaId);
        }
        return await db.DeleteAsync(entity);
    }

    public override async Task<int> DeleteAsync(int id)
    {
        var db = await GetConnectionAsync();
        if (id > 0)
        {
            var entity = await GetByIdAsync(id);
            await _syncService.DeleteKasaHareketAsync(id);
            if (entity != null && entity.KasaId > 0) _dbService.AutoReflectKasa(entity.KasaId);
        }
        return await db.ExecuteAsync("DELETE FROM KasaHareket WHERE Id = ?", id);
    }

    public override Task<List<KasaHareket>> GetDeletedAsync() => Task.FromResult(new List<KasaHareket>());
    public override Task RestoreAsync(KasaHareket entity) => Task.CompletedTask;

    /// <summary>
    /// Kasa bakiyesini hesaplar
    /// </summary>
    public async Task<decimal> GetBakiyeAsync()
    {
        var hareketler = await GetAllAsync();
        return hareketler.Sum(h => h.Giren - h.Cikan);
    }

    /// <summary>
    /// Tarih aralığına göre kasa hareketlerini getirir
    /// </summary>
    public async Task<List<KasaHareket>> GetHareketlerByTarihAsync(DateTime baslangic, DateTime bitis)
    {
        var db = await GetConnectionAsync();
        return await db.Table<KasaHareket>()
            .Where(h => h.Tarih >= baslangic && h.Tarih <= bitis)
            .OrderByDescending(h => h.Tarih)
            .ToListAsync();
    }

    public async Task<List<KasaHareket>> GetHareketlerAsync(int kasaId)
    {
        return await _dbService.GetKasaHareketleriAsync(kasaId);
    }

    public async Task<List<KasaHareket>> GetAllHareketlerAsync()
    {
        return await _dbService.GetKasaHareketleriAsync();
    }
}

