using ErmayMuhasebe.Models;
using ErmayMuhasebe.Services;
using System.IO;
using Xunit;

namespace ErmayMuhasebe.Tests.Services;

/// <summary>
/// DatabaseService için unit testler
/// CRUD işlemlerini test eder
/// </summary>
[Collection("DatabaseTests")] // Paralel çalışmayı engelle
public class DatabaseServiceTests : IDisposable
{
    private readonly DatabaseService _dbService;
    private readonly string _testDbPath;

    public DatabaseServiceTests()
    {
        // Test için geçici veritabanı dosyası oluştur
        _testDbPath = Path.Combine(Path.GetTempPath(), $"TestDb_{Guid.NewGuid()}.db3");
        
        // Test veritabanı yolunu ayarla
        ErmayMuhasebe.Data.Constants.DatabasePath = _testDbPath;
        
        _dbService = new DatabaseService();
        _dbService.UseEncryption = false;
        _dbService.DisableCloudSync = true;
    }

    [Fact]
    public async Task InitializeAsync_ShouldCreateDatabase()
    {
        // Act
        await _dbService.InitializeAsync();
        
        // Assert
        Assert.True(File.Exists(_testDbPath)); // Veritabanı dosyası oluşmalı
    }

    [Fact]
    public async Task SaveCariKartAsync_NewCari_ShouldReturnId()
    {
        // Arrange
        await _dbService.InitializeAsync();
        var cari = new CariKart
        {
            Unvan = "Test Cari A.Ş.",
            Tur = "Alici",
            Telefon = "02121234567",
            Email = "test@test.com"
        };
        
        // Act
        int id = await _dbService.SaveCariKartAsync(cari);
        
        // Assert
        Assert.True(id > 0); // ID atanmış olmalı
        Assert.Equal(id, cari.Id); // Cari'nin ID'si set edilmiş olmalı
    }

    [Fact]
    public async Task GetCariKartAsync_ExistingCari_ShouldReturnCari()
    {
        // Arrange
        await _dbService.InitializeAsync();
        var cari = new CariKart
        {
            Unvan = "Test Cari A.Ş.",
            Tur = "Alici"
        };
        int id = await _dbService.SaveCariKartAsync(cari);
        
        // Act
        var retrieved = await _dbService.GetCariKartAsync(id);
        
        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(id, retrieved.Id);
        Assert.Equal("Test Cari A.Ş.", retrieved.Unvan);
        Assert.Equal("Alici", retrieved.Tur);
    }

    [Fact]
    public async Task GetCariKartAsync_NonExistingCari_ShouldReturnNull()
    {
        // Arrange
        await _dbService.InitializeAsync();
        
        // Act
        var retrieved = await _dbService.GetCariKartAsync(99999);
        
        // Assert
        Assert.Null(retrieved); // Olmayan ID için null dönmeli
    }

    [Fact]
    public async Task SaveCariKartAsync_UpdateExisting_ShouldUpdate()
    {
        // Arrange
        await _dbService.InitializeAsync();
        var cari = new CariKart
        {
            Unvan = "Test Cari A.Ş.",
            Tur = "Alici"
        };
        int id = await _dbService.SaveCariKartAsync(cari);
        
        // Act
        cari.Unvan = "Güncellenmiş Cari A.Ş.";
        await _dbService.SaveCariKartAsync(cari);
        
        // Assert
        var updated = await _dbService.GetCariKartAsync(id);
        Assert.NotNull(updated);
        Assert.Equal("Güncellenmiş Cari A.Ş.", updated.Unvan);
    }

    [Fact]
    public async Task GetCarilerAsync_ShouldReturnList()
    {
        // Arrange
        await _dbService.InitializeAsync();
        await _dbService.SaveCariKartAsync(new CariKart { Unvan = "Cari 1", Tur = "Alici" });
        await _dbService.SaveCariKartAsync(new CariKart { Unvan = "Cari 2", Tur = "Satici" });
        
        // Act
        var list = await _dbService.GetCarilerAsync();
        
        // Assert
        Assert.NotNull(list);
        Assert.True(list.Count >= 2); // En az 2 cari olmalı
    }

    [Fact]
    public async Task SaveStokKartAsync_NewStok_ShouldReturnId()
    {
        // Arrange
        await _dbService.InitializeAsync();
        var stok = new StokKart
        {
            StokKodu = "STK001",
            StokAdi = "Test Ürün",
            Birim = "Adet",
            SatisFiyati = 100.50m,
            KDV = 20
        };
        
        // Act
        int id = await _dbService.SaveStokKartAsync(stok);
        
        // Assert
        Assert.True(id > 0);
        Assert.Equal(id, stok.Id);
    }

    [Fact]
    public async Task GetStokKartAsync_ExistingStok_ShouldReturnStok()
    {
        // Arrange
        await _dbService.InitializeAsync();
        var stok = new StokKart
        {
            StokKodu = "STK001",
            StokAdi = "Test Ürün",
            SatisFiyati = 100.50m
        };
        int id = await _dbService.SaveStokKartAsync(stok);
        
        // Act
        var retrieved = await _dbService.GetStokKartAsync(id);
        
        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(id, retrieved.Id);
        Assert.Equal("STK001", retrieved.StokKodu);
        Assert.Equal("Test Ürün", retrieved.StokAdi);
        Assert.Equal(100.50m, retrieved.SatisFiyati);
    }

    [Fact]
    public async Task SoftDeleteCariKartAsync_ShouldMarkAsDeleted()
    {
        // Arrange
        await _dbService.InitializeAsync();
        var cari = new CariKart
        {
            Unvan = "Silinecek Cari",
            Tur = "Alici"
        };
        int id = await _dbService.SaveCariKartAsync(cari);
        
        // Act
        await _dbService.SoftDeleteCariKartAsync(cari);
        
        // Assert
        var deleted = await _dbService.GetDeletedCariKartsAsync();
        Assert.Contains(deleted, c => c.Id == id && c.IsDeleted);
    }

    [Fact]
    public async Task BackupToAsync_ShouldCreateConsistentBackup_AndVerifyData()
    {
        // Arrange
        await _dbService.InitializeAsync();
        var cari = new CariKart
        {
            Unvan = "Yedek Test Cari Ltd.",
            Tur = "Musteri",
            VergiNo = "1234567890"
        };
        int id = await _dbService.SaveCariKartAsync(cari);
        string backupFile = Path.Combine(Path.GetTempPath(), $"TestBackup_{Guid.NewGuid()}.db3");

        try
        {
            // Act
            await _dbService.BackupToAsync(backupFile);

            // Assert: Backup file exists and has size
            Assert.True(File.Exists(backupFile));
            var fi = new FileInfo(backupFile);
            Assert.True(fi.Length > 0);

            var backupDbService = new DatabaseService();
            backupDbService.UseEncryption = false;
            backupDbService.DisableCloudSync = true;
            await backupDbService.InitializeAsync(backupFile);
            var restoredCari = await backupDbService.GetCariKartAsync(id);

            Assert.NotNull(restoredCari);
            Assert.Equal("Yedek Test Cari Ltd.", restoredCari.Unvan);
            Assert.Equal("1234567890", restoredCari.VergiNo);

            await backupDbService.CloseConnectionAsync();
        }
        finally
        {
            if (File.Exists(backupFile))
            {
                try { File.Delete(backupFile); } catch { }
            }
            ErmayMuhasebe.Data.Constants.DatabasePath = _testDbPath;
        }
    }


    public void Dispose()
    {
        // Test sonrası geçici veritabanı dosyasını temizle
        try
        {
            if (File.Exists(_testDbPath))
            {
                File.Delete(_testDbPath);
            }
        }
        catch
        {
            // Silme hatası olursa görmezden gel
        }
    }
}

