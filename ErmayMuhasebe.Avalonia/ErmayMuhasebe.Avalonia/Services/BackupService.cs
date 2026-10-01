using System;
using System.IO;
using System.Threading.Tasks;

namespace ErmayMuhasebe.Services
{
    public class BackupService
    {
        private readonly DatabaseService _dbService;
        private readonly string _backupFolder;

        public BackupService(DatabaseService dbService)
        {
            _dbService = dbService;
            
            // ErmayMuhasebe/Backups klasörünü oluştur
            string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ErmayMuhasebe");
            _backupFolder = Path.Combine(appData, "Backups");
            
            if (!Directory.Exists(_backupFolder))
                Directory.CreateDirectory(_backupFolder);
        }

        public async Task CreateBackupAsync(string destinationPath)
        {
            string sourcePath = _dbService.GetDatabasePath();
            
            if (!File.Exists(sourcePath)) 
                throw new FileNotFoundException($"Veritabanı dosyası ({sourcePath}) bulunamadı.");

            // Canlı veritabanını atomik VACUUM INTO ile güvenli ve tutarlı şekilde yedekle
            await _dbService.BackupToAsync(destinationPath);
        }

        public async Task<bool?> RunAutoBackupAsync()
        {
            try 
            {
                string backupDir = _backupFolder;
                try
                {
                    var profil = await _dbService.GetFirmaProfiliAsync();
                    if (profil != null && !string.IsNullOrWhiteSpace(profil.BackupFolderPath) && Directory.Exists(profil.BackupFolderPath))
                    {
                        backupDir = profil.BackupFolderPath;
                    }
                }
                catch { }

                // Günlük bir yedekleme varmı kontrol et
                string today = DateTime.Now.ToString("yyyyMMdd");
                string backupFileName = $"Ermay_AutoBackup_{today}.db3";
                string backupPath = Path.Combine(backupDir, backupFileName);

                if (!File.Exists(backupPath))
                {
                    await CreateBackupAsync(backupPath);
                    System.Diagnostics.Debug.WriteLine($"[BackupService] Günlük otomatik yedekleme oluşturuldu: {backupFileName}");
                    
                    // 30 günden eski yedekleri temizle
                    CleanupOldBackupsInFolder(backupDir, 30);
                    return true;
                }
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BackupService] Otomatik yedekleme hatası: {ex.Message}");
                throw;
            }
        }

        private void CleanupOldBackupsInFolder(string folder, int days)
        {
            try 
            {
                var files = Directory.GetFiles(folder, "Ermay_AutoBackup_*.db3");
                foreach (var file in files)
                {
                    var info = new FileInfo(file);
                    if (info.CreationTime < DateTime.Now.AddDays(-days))
                    {
                        File.Delete(file);
                    }
                }
            }
            catch { }
        }
    }
}
