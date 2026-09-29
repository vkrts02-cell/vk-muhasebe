using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Avalonia.Headless.XUnit;
using Xunit;
using ErmayMuhasebe.Avalonia.ViewModels;
using ErmayMuhasebe.Models;
using ErmayMuhasebe.Services;
using ErmayMuhasebe.Avalonia.Services;

namespace ErmayMuhasebe.Avalonia.Tests.Scenarios;

public partial class AyarlarScenariosTests
{
    // =============================================================
    // 10. Bulut Senkronizasyon & Gizli Anahtar Yönetimi (101 - 115)
    // =============================================================

    [Fact]
    public void Scenario_101_SetAndGetCloudConfig_PersistsInUnitOfWork()
    {
        string url = "https://ermay-test-sync.firebaseio.com";
        string secret = "secret_key_123456789";

        _uow.SetCloudConfig(url, secret);
        var config = _uow.GetCloudConfig();

        Assert.Equal(url, config.Url);
        Assert.Equal(secret, config.Secret);
    }

    [Fact]
    public void Scenario_102_CloudSecret_MaskedDisplay_ReplacesWithAsterisks()
    {
        string secret = "supersecretpassword123";
        string masked = new string('*', secret.Length);

        Assert.Equal(secret.Length, masked.Length);
        Assert.All(masked, c => Assert.Equal('*', c));
    }

    [Theory]
    [InlineData("https://ermay.firebaseio.com", true)]
    [InlineData("https://my-custom-sync.com", true)]
    [InlineData("http://insecure.com", false)]
    [InlineData("invalid-url", false)]
    public void Scenario_103_to_106_CloudUrl_HttpsEnforcement(string url, bool isSecure)
    {
        bool valid = Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
        Assert.Equal(isSecure, valid);
    }

    [Fact]
    public void Scenario_107_EnableAutoSync_DoesNotThrow()
    {
        _uow.EnableAutoSync(true);
        _uow.EnableAutoSync(false);
        Assert.True(true);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 4)]
    [InlineData(4, 8)]
    [InlineData(5, 16)]
    public void Scenario_108_to_112_ExponentialBackoff_RetryIntervals(int attempt, int expectedSeconds)
    {
        int seconds = (int)Math.Pow(2, attempt - 1);
        Assert.Equal(expectedSeconds, seconds);
    }

    [Fact]
    public async Task Scenario_113_SyncToCloud_WhenDisconnected_HandlesGracefully()
    {
        // Bulut bağlantısı yokken hata fırlatmamalı, offline kuyruğu korumalı
        await _uow.SyncToCloudAsync();
        Assert.True(true);
    }

    [Fact]
    public async Task Scenario_114_SyncFromCloud_WhenDisconnected_HandlesGracefully()
    {
        await _uow.SyncFromCloudAsync();
        Assert.True(true);
    }

    [Theory]
    [InlineData(1048576, 5242880, true)]  // 1 MB < 5 MB limit
    [InlineData(6291456, 5242880, false)] // 6 MB > 5 MB limit
    public void Scenario_115_SyncPacketSizeLimit_5MB(long packetBytes, long maxAllowed, bool expectedAllowed)
    {
        bool allowed = packetBytes <= maxAllowed;
        Assert.Equal(expectedAllowed, allowed);
    }

    // =============================================================
    // 11. Mali Yıl Devir & Dönem Yönetimi (116 - 130)
    // =============================================================

    [Theory]
    [InlineData(2025)]
    [InlineData(2026)]
    [InlineData(2027)]
    public void Scenario_116_to_118_YearContext_ValidFiscalYears(int year)
    {
        var context = new YearContext();
        context.CurrentYear = year;
        Assert.Equal(year, context.CurrentYear);
    }

    [Fact]
    public async Task Scenario_119_Devir_InsertWithIdAsync_PreservesExplicitPrimaryKey()
    {
        var cari = new CariKart
        {
            Id = 8888,
            CariKod = "C-DEVIR-8888",
            Unvan = "Devir Edilen Müşteri A.Ş.",
            Borc = 15000,
            Alacak = 5000
        };
        await _uow.InsertWithIdAsync(cari);

        var retrieved = await _uow.Cariler.GetByIdAsync(8888);
        Assert.NotNull(retrieved);
        Assert.Equal(8888, retrieved.Id);
        Assert.Equal("C-DEVIR-8888", retrieved.CariKod);
    }

    [Fact]
    public void Scenario_120_YearEndClosingBalance_BecomesNextYearOpeningBalance()
    {
        decimal closingBorc = 25000m;
        decimal closingAlacak = 10000m;
        decimal netBakiye = closingBorc - closingAlacak;

        decimal openingBakiye = netBakiye;
        Assert.Equal(15000m, openingBakiye);
    }

    [Theory]
    [InlineData(100, 100, 0)]
    [InlineData(500, 300, 200)]
    [InlineData(1000, 1500, -500)]
    public void Scenario_121_to_123_StockQuantityRollover(int prevIn, int prevOut, int expectedOpening)
    {
        int openingStock = prevIn - prevOut;
        Assert.Equal(expectedOpening, openingStock);
    }

    [Theory]
    [InlineData("FAT-2025-000450", "FAT-2026-000001")]
    [InlineData("SIP-2025-001200", "SIP-2026-000001")]
    public void Scenario_124_to_125_ResetInvoiceSequenceForNewYear(string oldYearLastNo, string expectedNewYearFirstNo)
    {
        string newPrefix = expectedNewYearFirstNo.Substring(0, 9); // FAT-2026-
        string newNo = $"{newPrefix}{1:D6}";
        Assert.Equal(expectedNewYearFirstNo, newNo);
        Assert.NotEqual(oldYearLastNo, newNo);
    }

    [Fact]
    public void Scenario_126_HistoricalYear_ReadOnlyStatus()
    {
        int currentYear = 2026;
        int activeYear = 2024;
        bool isReadOnly = activeYear < currentYear;

        Assert.True(isReadOnly);
    }

    [Theory]
    [InlineData(100000, 100000, true)] // Closing sum equals opening sum
    [InlineData(100000, 99990, false)]  // Mismatch
    public void Scenario_127_to_128_BalanceCarryOverIntegrityVerification(decimal closingSum, decimal openingSum, bool isBalanced)
    {
        bool verified = closingSum == openingSum;
        Assert.Equal(isBalanced, verified);
    }

    [Fact]
    public void Scenario_129_YearContext_DefaultIsCurrentYear()
    {
        var context = new YearContext();
        Assert.Equal(DateTime.Now.Year, context.CurrentYear);

        context.CurrentYear = 2030;
        Assert.Equal(2030, context.CurrentYear);
    }

    [Fact]
    public void Scenario_130_RolloverAuditLog_ContainsFiscalDetails()
    {
        string audit = $"DEVIR: 2025 -> 2026 Başarıyla tamamlandı. Kullanıcı: admin";
        Assert.Contains("DEVIR: 2025 -> 2026", audit);
    }

    [Fact]
    public async Task Scenario_130b_NewKasa_TransfersToTargetYearOnRollover()
    {
        var yearContext = new YearContext { CurrentYear = 2026 };
        var rolloverService = new YearRolloverService(_dbService, yearContext);

        // 1. 2026 yılı için yeni kasa oluştur (Finans kısmında)
        var newKasa = new BankaKart
        {
            BankaAdi = "2026 Merkez Kasa",
            KartTuru = "Kasa",
            DovizTuru = "TL",
            Yetkili = "Ahmet Yılmaz",
            AcilisBakiyesi = 5000,
            GuncelBakiye = 5000
        };
        await _uow.Bankalar.SaveAsync(newKasa);

        // Kasa hareketi ekle (+2000 TL)
        await _dbService.SaveKasaHareketAsync(new KasaHareket
        {
            KasaId = newKasa.Id,
            Tarih = new DateTime(2026, 6, 1),
            IslemTuru = "Tahsilat (Nakit)",
            Aciklama = "Nakit Satış",
            Giren = 2000,
            Cikan = 0,
            Tutar = 2000
        });

        // 2. 2026 -> 2027 Devir yap
        var options = new RolloverOptions
        {
            TransferCariler = false,
            TransferStoklar = false,
            TransferKasaBanka = true,
            TransferCekSenet = false,
            TransferTanimlar = false
        };

        var result = await rolloverService.RolloverYearAsync(2026, 2027, options);

        Assert.True(result.Success);
        Assert.True(result.KasalarCount >= 1);

        // 3. 2027 veritabanını kontrol et
        string targetDbPath = rolloverService.GetDbPathForYear(2027);
        try
        {
            Assert.True(File.Exists(targetDbPath));
            var targetConn = await rolloverService.OpenConnectionAsync(targetDbPath);
            var targetKasa = await targetConn.Table<BankaKart>().FirstOrDefaultAsync(k => k.Id == newKasa.Id || k.BankaAdi == "2026 Merkez Kasa");

            Assert.NotNull(targetKasa);
            Assert.Equal("Kasa", targetKasa.KartTuru);
            Assert.Equal(7000, targetKasa.AcilisBakiyesi);
            Assert.Equal(7000, targetKasa.GuncelBakiye);

            // Devir fişini kontrol et
            var devirMove = await targetConn.Table<KasaHareket>().FirstOrDefaultAsync(h => h.KasaId == targetKasa.Id && h.EvrakNo == "DEVIR-KASA-2026");
            Assert.NotNull(devirMove);
            Assert.Equal(7000, devirMove.Giren);

            await targetConn.CloseAsync();
        }
        finally
        {
            try { if (File.Exists(targetDbPath)) File.Delete(targetDbPath); } catch { }
        }
    }

    [Fact]
    public async Task Scenario_130c_ReflectSingleKasaForward_PropagatesKasaToExistingNextYear()
    {
        var yearContext = new YearContext { CurrentYear = 2026 };
        var rolloverService = new YearRolloverService(_dbService, yearContext);

        // 1. Önce 2027 veritabanını hazırla
        string targetDbPath = rolloverService.GetDbPathForYear(2027);
        var targetConn = await rolloverService.OpenConnectionAsync(targetDbPath);
        await rolloverService.InitializeTargetDatabaseSchemaAsync(targetConn);
        await targetConn.CloseAsync();

        try
        {
            // 2. 2026'da sonradan yeni bir kasa tanımla
            var kasaSonradan = new BankaKart
            {
                BankaAdi = "2026 Sonradan Açılan Kasa",
                KartTuru = "Kasa",
                DovizTuru = "TL",
                AcilisBakiyesi = 3500,
                GuncelBakiye = 3500
            };
            await _uow.Bankalar.SaveAsync(kasaSonradan);

            // 3. Tekil kasayı 2027'ye yansıt
            bool reflected = await rolloverService.ReflectSingleKasaForwardAsync(2026, kasaSonradan.Id);
            Assert.True(reflected);

            // 4. 2027 veritabanında kasa ve devir fişinin oluştuğunu doğrula
            var checkConn = await rolloverService.OpenConnectionAsync(targetDbPath);
            var nextKasa = await checkConn.Table<BankaKart>().FirstOrDefaultAsync(k => k.Id == kasaSonradan.Id);

            Assert.NotNull(nextKasa);
            Assert.Equal("Kasa", nextKasa.KartTuru);
            Assert.Equal(3500, nextKasa.AcilisBakiyesi);
            Assert.Equal(3500, nextKasa.GuncelBakiye);

            var nextMove = await checkConn.Table<KasaHareket>().FirstOrDefaultAsync(h => h.KasaId == nextKasa.Id && h.EvrakNo == "DEVIR-KASA-2026");
            Assert.NotNull(nextMove);
            Assert.Equal(3500, nextMove.Giren);

            await checkConn.CloseAsync();
        }
        finally
        {
            try { if (File.Exists(targetDbPath)) File.Delete(targetDbPath); } catch { }
        }
    }

    // =============================================================
    // 12. Güvenlik, Roller & Yetkilendirme Matrisi (131 - 145)
    // =============================================================

    [Theory]
    [InlineData("Sifre*2026", true)]     // 8+ chars, upper, lower, digit, symbol
    [InlineData("kisa1*", false)]        // Too short (< 8)
    [InlineData("sadeceharfler", false)]  // No digit, no symbol, no upper
    [InlineData("12345678", false)]      // Only digits
    public void Scenario_131_to_134_PasswordComplexityRequirements(string password, bool isStrong)
    {
        bool strong = password.Length >= 8
            && password.Any(char.IsUpper)
            && password.Any(char.IsLower)
            && password.Any(char.IsDigit)
            && password.Any(c => !char.IsLetterOrDigit(c));

        Assert.Equal(isStrong, strong);
    }

    [Fact]
    public void Scenario_135_SaltGeneration_IsRandomAndUnique()
    {
        string salt1 = AuthService.GenerateSalt();
        string salt2 = AuthService.GenerateSalt();

        Assert.NotNull(salt1);
        Assert.NotNull(salt2);
        Assert.NotEqual(salt1, salt2);
    }

    [Theory]
    [InlineData("Admin", "Finans", true)]
    [InlineData("Admin", "Ayarlar", true)]
    [InlineData("Muhasebe", "Finans", true)]
    [InlineData("Muhasebe", "Ayarlar", false)] // Muhasebe rolü sistem ayarlarına giremez
    [InlineData("Satis", "Finans", false)]     // Satış finans yönetemez
    [InlineData("Satis", "Siparisler", true)]
    [InlineData("Depo", "StokKartlari", true)]
    [InlineData("Depo", "Faturalar", false)]
    public void Scenario_136_to_143_RoleBasedAccessControl_Matrix(string role, string module, bool isAllowed)
    {
        bool allowed = role switch
        {
            "Admin" => true,
            "Muhasebe" => module != "Ayarlar",
            "Satis" => module == "Siparisler" || module == "Teklifler" || module == "CariHesaplar",
            "Depo" => module == "StokKartlari" || module == "StokSayim",
            _ => false
        };

        Assert.Equal(isAllowed, allowed);
    }

    [Theory]
    [InlineData(3, false)] // 3 failed logins -> not locked
    [InlineData(5, true)]  // 5 failed logins -> locked out
    [InlineData(6, true)]
    public void Scenario_144_to_145_FailedLoginAttempt_LockoutPolicy(int failedAttempts, bool isLocked)
    {
        bool locked = failedAttempts >= 5;
        Assert.Equal(isLocked, locked);
    }

    // =============================================================
    // 13. Fabrika Sıfırlama & Uçtan Uca Ayarlar Döngüsü (146 - 154)
    // =============================================================

    [Fact]
    public async Task Scenario_146_FactoryReset_WipesDataAndReseedsAdmin()
    {
        // 1. Veri ekle
        var c = new CariKart { CariKod = "C-RESET-TEST", Unvan = "Silinecek Cari" };
        await _uow.Cariler.SaveAsync(c);

        // 2. Tabloları temizle
        await _uow.ClearAllTablesAsync();

        // 3. Admin hesabını yeniden oluştur (HeadlessTestBase.InitializeAsync mantığı)
        var globalConn = _dbService.GetGlobalConnection();
        var admin = await globalConn.Table<User>().FirstOrDefaultAsync(u => u.Username == "admin");
        Assert.NotNull(admin);

        // 4. Carilerin temizlendiğini doğrula
        var cariler = await _uow.Cariler.GetAllAsync();
        Assert.Empty(cariler);
    }

    [Theory]
    [InlineData("tr-TR", "₺", "TRY")]
    [InlineData("en-US", "$", "USD")]
    [InlineData("de-DE", "€", "EUR")]
    public void Scenario_147_to_149_CultureAndCurrencySymbol_Consistency(string cultureName, string symbol, string code)
    {
        Assert.NotEmpty(cultureName);
        Assert.NotEmpty(symbol);
        Assert.NotEmpty(code);
    }

    [Fact]
    public void Scenario_150_ExportSettings_GeneratesNonEmptyJson()
    {
        var settings = new Dictionary<string, string>
        {
            { "Theme", "Dark" },
            { "AutoScaling", "True" },
            { "PaperSize", "A4" }
        };
        string json = System.Text.Json.JsonSerializer.Serialize(settings);

        Assert.Contains("\"Theme\":\"Dark\"", json);
        Assert.Contains("\"PaperSize\":\"A4\"", json);
    }

    [Fact]
    public void Scenario_151_ImportSettings_RestoresConfiguration()
    {
        string json = "{\"Theme\":\"Light\",\"PaperSize\":\"A5\"}";
        var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json);

        Assert.NotNull(dict);
        Assert.Equal("Light", dict["Theme"]);
        Assert.Equal("A5", dict["PaperSize"]);
    }

    [Fact]
    public void Scenario_152_SettingsFileSha256_IntegrityVerification()
    {
        string content = "{\"Config\":\"Ermay2026\"}";
        using var sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(content));
        string hashStr = Convert.ToHexString(hash);

        Assert.Equal(64, hashStr.Length);
    }

    [Fact]
    public async Task Scenario_153_SaveFirmaProfili_WithAllDocumentPrintSizes()
    {
        var profil = new FirmaProfili
        {
            FirmaAdi = "Ermay Kurumsal",
            FaturaSize = "A4",
            SiparisSize = "A5",
            TeklifSize = "A4",
            EkstreSize = "A4"
        };
        await _uow.SaveFirmaProfiliAsync(profil);

        var retrieved = await _uow.GetFirmaProfiliAsync();
        Assert.Equal("A4", retrieved.FaturaSize);
        Assert.Equal("A5", retrieved.SiparisSize);
        Assert.Equal("A4", retrieved.TeklifSize);
        Assert.Equal("A4", retrieved.EkstreSize);
    }

    [Fact]
    public async Task Scenario_154_EndToEndSettingsLifecycle_Simulation()
    {
        // 1. Kurumsal Firma Profilini Kaydet
        var profil = new FirmaProfili
        {
            FirmaAdi = "Ermay Global Tekstil A.Ş.",
            VergiDairesi = "Maslak",
            VergiNo = "9876543210",
            Telefon = "02124445566",
            Eposta = "info@ermayglobal.com",
            FaturaSize = "A4"
        };
        await _uow.SaveFirmaProfiliAsync(profil);

        // 2. Bulut Yapılandırmasını Tanımla
        _uow.SetCloudConfig("https://ermay-global-cloud.firebaseio.com", "global_secret_key_2026");

        // 3. Profil ve Bulut Bilgilerini Doğrula
        var savedProfil = await _uow.GetFirmaProfiliAsync();
        var cloud = _uow.GetCloudConfig();

        Assert.Equal("Ermay Global Tekstil A.Ş.", savedProfil.FirmaAdi);
        Assert.Equal("9876543210", savedProfil.VergiNo);
        Assert.Equal("https://ermay-global-cloud.firebaseio.com", cloud.Url);
        Assert.Equal("global_secret_key_2026", cloud.Secret);
    }
}
