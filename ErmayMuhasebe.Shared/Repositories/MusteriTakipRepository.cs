using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ErmayMuhasebe.Models;
using ErmayMuhasebe.Services;

namespace ErmayMuhasebe.Repositories
{
    public class MusteriTakipRepository : BaseRepository<MusteriTakipKlasor>, IMusteriTakipRepository
    {
        public MusteriTakipRepository(DatabaseService dbService) : base(dbService)
        {
        }

        public override async Task<List<MusteriTakipKlasor>> GetAllAsync()
        {
            return await GetKlasorlerAsync();
        }

        public async Task<List<MusteriTakipKlasor>> GetKlasorlerAsync()
        {
            var db = await GetConnectionAsync();
            return await db.Table<MusteriTakipKlasor>()
                .Where(x => !x.IsDeleted)
                .OrderByDescending(x => x.SonIslemTarihi)
                .ToListAsync();
        }

        public override async Task<MusteriTakipKlasor?> GetByIdAsync(int id)
        {
            var db = await GetConnectionAsync();
            return await db.Table<MusteriTakipKlasor>().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
        }

        public async Task<MusteriTakipKlasor?> GetByCariIdAsync(int cariId)
        {
            var db = await GetConnectionAsync();
            return await db.Table<MusteriTakipKlasor>().FirstOrDefaultAsync(x => x.CariId == cariId && !x.IsDeleted);
        }

        public override async Task<int> SaveAsync(MusteriTakipKlasor entity)
        {
            var db = await GetConnectionAsync();
            entity.SonIslemTarihi = DateTime.Now;
            if (entity.Id != 0)
            {
                await db.UpdateAsync(entity);
            }
            else
            {
                entity.OlusturmaTarihi = DateTime.Now;
                await db.InsertAsync(entity);
            }

            try
            {
                if (_syncService != null && _syncService.IsConnected)
                {
                    await _syncService.SyncMusteriTakipKlasorAsync(entity);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MusteriTakipRepository] Cloud sync Save error: {ex.Message}");
            }

            return entity.Id;
        }

        public override async Task<int> DeleteAsync(MusteriTakipKlasor entity)
        {
            var db = await GetConnectionAsync();
            entity.IsDeleted = true;
            await db.UpdateAsync(entity);

            // İlişkili detayları da mantıksal sil
            var detaylar = await db.Table<MusteriTakipDetay>().Where(x => x.KlasorId == entity.Id).ToListAsync();
            foreach (var d in detaylar)
            {
                d.IsDeleted = true;
                await db.UpdateAsync(d);
                try
                {
                    if (_syncService != null && _syncService.IsConnected)
                    {
                        await _syncService.SyncMusteriTakipDetayAsync(d);
                    }
                }
                catch { }
            }

            try
            {
                if (_syncService != null && _syncService.IsConnected)
                {
                    await _syncService.DeleteMusteriTakipKlasorAsync(entity.Id);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MusteriTakipRepository] Cloud sync Delete error: {ex.Message}");
            }

            return entity.Id;
        }

        public override async Task<int> DeleteAsync(int id)
        {
            var entity = await GetByIdAsync(id);
            if (entity == null) return 0;
            return await DeleteAsync(entity);
        }

        public override async Task<List<MusteriTakipKlasor>> GetDeletedAsync()
        {
            var db = await GetConnectionAsync();
            return await db.Table<MusteriTakipKlasor>().Where(x => x.IsDeleted).ToListAsync();
        }

        public override async Task RestoreAsync(MusteriTakipKlasor entity)
        {
            entity.IsDeleted = false;
            await SaveAsync(entity);
        }

        public async Task<List<MusteriTakipDetay>> GetDetaylarByKlasorIdAsync(int klasorId)
        {
            var db = await GetConnectionAsync();
            return await db.Table<MusteriTakipDetay>()
                .Where(x => x.KlasorId == klasorId && !x.IsDeleted)
                .OrderByDescending(x => x.Tarih)
                .ToListAsync();
        }

        public async Task<List<MusteriTakipDetay>> GetDetaylarByTipAsync(int klasorId, string tip)
        {
            var db = await GetConnectionAsync();
            return await db.Table<MusteriTakipDetay>()
                .Where(x => x.KlasorId == klasorId && x.Tip == tip && !x.IsDeleted)
                .OrderByDescending(x => x.Tarih)
                .ToListAsync();
        }

        public async Task<int> SaveDetayAsync(MusteriTakipDetay detay)
        {
            var db = await GetConnectionAsync();
            if (detay.Id != 0)
            {
                await db.UpdateAsync(detay);
            }
            else
            {
                if (detay.Tarih == default) detay.Tarih = DateTime.Now;
                await db.InsertAsync(detay);
            }

            // Klasörün son işlem tarihini güncelle
            var klasor = await GetByIdAsync(detay.KlasorId);
            if (klasor != null)
            {
                klasor.SonIslemTarihi = DateTime.Now;
                await db.UpdateAsync(klasor);
                try
                {
                    if (_syncService != null && _syncService.IsConnected)
                    {
                        await _syncService.SyncMusteriTakipKlasorAsync(klasor);
                    }
                }
                catch { }
            }

            try
            {
                if (_syncService != null && _syncService.IsConnected)
                {
                    await _syncService.SyncMusteriTakipDetayAsync(detay);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MusteriTakipRepository] Cloud sync SaveDetay error: {ex.Message}");
            }

            return detay.Id;
        }

        public async Task<int> DeleteDetayAsync(int id)
        {
            var db = await GetConnectionAsync();
            var item = await db.Table<MusteriTakipDetay>().FirstOrDefaultAsync(x => x.Id == id);
            if (item != null)
            {
                item.IsDeleted = true;
                await db.UpdateAsync(item);

                try
                {
                    if (_syncService != null && _syncService.IsConnected)
                    {
                        await _syncService.DeleteMusteriTakipDetayAsync(item.Id);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MusteriTakipRepository] Cloud sync DeleteDetay error: {ex.Message}");
                }

                return item.Id;
            }
            return 0;
        }
    }
}
