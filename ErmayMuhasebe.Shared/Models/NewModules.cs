using SQLite;
using System;
using System.Collections.Generic;

namespace ErmayMuhasebe.Models
{
    public class Teklif : ITenantEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string TenantId { get; set; } = "default";
        public string? TeklifNo { get; set; }
        public int CariId { get; set; }
        public string? CariUnvan { get; set; }
        public DateTime Tarih { get; set; } = DateTime.Now;
        public DateTime? GecerlilikTarihi { get; set; }
        public string? Aciklama { get; set; }
        public decimal GenelToplam { get; set; }
        public string? Durum { get; set; } 
        public string? OdemeBilgisi { get; set; }
        public DateTime KayitTarihi { get; set; } = DateTime.Now;
        public bool IsDeleted { get; set; }
    }

    public class TeklifDetay
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public int TeklifId { get; set; }
        public int StokId { get; set; }
        public string? StokAdi { get; set; }
        public double Miktar { get; set; }
        public double TopMiktari { get; set; } 
        public string? Birim { get; set; }
        public decimal BirimFiyat { get; set; } 
        public decimal Fiyat { get => BirimFiyat; set => BirimFiyat = value; } 
        public decimal Tutar { get; set; }
        public double KdvOrani { get; set; }
        public string? Aciklama { get; set; } 
        public string? ParaBirimi { get; set; }
    }

    public class Siparis : ITenantEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string TenantId { get; set; } = "default";
        public string? SiparisNo { get; set; }
        public int CariId { get; set; }
        public string? CariUnvan { get; set; }
        public DateTime Tarih { get; set; } = DateTime.Now;
        public DateTime? TeslimatTarihi { get; set; } // Set to Nullable to match PdfService logic
        public string? Aciklama { get; set; }
        public string? PdfNotlar { get; set; }
        public decimal GenelToplam { get; set; }
        public string? Durum { get; set; }
        public string? OdemeBilgisi { get; set; }
        public string? Oncelik { get; set; } 
        public string? BaglantiEvrakNo { get; set; } 
        public DateTime KayitTarihi { get; set; } = DateTime.Now;
        public bool IsDeleted { get; set; }
    }

    public class SiparisDetay
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public int SiparisId { get; set; }
        public int StokId { get; set; }
        public string? StokAdi { get; set; }
        public double Miktar { get; set; }
        public double TopMiktari { get; set; } 
        public string? Birim { get; set; } 
        public decimal BirimFiyat { get; set; } 
        public decimal Fiyat { get => BirimFiyat; set => BirimFiyat = value; }
        public decimal Tutar { get; set; }
        public string? Aciklama { get; set; } 
        public string? MiktarAciklama { get; set; }
        public string? ParaBirimi { get; set; }
        public double KdvOrani { get; set; }
    }

    public class FirmaProfili : ITenantEntity
    {
        [PrimaryKey]
        public int Id { get; set; } = 1;
        public string TenantId { get; set; } = "default";
        public string? FirmaAdi { get; set; }
        public string? Yetkili { get; set; }
        public string? Telefon { get; set; }
        public string? Eposta { get; set; }
        public string? VergiDairesi { get; set; }
        public string? VergiNo { get; set; }
        public string? Adres { get; set; }
        public string? WebSitesi { get; set; }
        public string? LogoUrl { get; set; }
        public string? LogoBase64 { get; set; } // Base64 formatında logo verisi
        
        // Yıl Ayarları
        public int StartingYear { get; set; } = 2026;

        // PDF bazlı logo ayarları
        public bool LogoFatura { get; set; } = true;
        public bool LogoSiparis { get; set; } = true;
        public bool LogoTeklif { get; set; } = true;
        public bool LogoEkstre { get; set; } = true;
        public bool LogoRaporlar { get; set; } = true;
        public bool LogoTahsilat { get; set; } = true;
        public bool LogoOdeme { get; set; } = true;
        public bool LogoAcilisBakiye { get; set; } = true;

        // PDF bazlı sayfa ayarları (Boyut: "A4", "A5" | Yönlendirme: "Portrait", "Landscape")
        public string FaturaSize { get; set; } = "A4";
        public string FaturaOrientation { get; set; } = "Dikey";
        
        public string SiparisSize { get; set; } = "A5";
        public string SiparisOrientation { get; set; } = "Dikey";
        
        public string TeklifSize { get; set; } = "A4";
        public string TeklifOrientation { get; set; } = "Dikey";
        
        public string EkstreSize { get; set; } = "A4";
        public string EkstreOrientation { get; set; } = "Dikey";
        
        public string RaporSize { get; set; } = "A4";
        public string RaporOrientation { get; set; } = "Dikey";
        
        public string TahsilatSize { get; set; } = "A5";
        public string TahsilatOrientation { get; set; } = "Dikey";
        
        public string OdemeSize { get; set; } = "A5";
        public string OdemeOrientation { get; set; } = "Dikey";

        public string AcilisBakiyeSize { get; set; } = "A5";
        public string AcilisBakiyeOrientation { get; set; } = "Dikey";

        public DateTime? LastBackupDate { get; set; }
        public string FactoryResetPassword { get; set; } = "";
        
        public bool LowPerformanceMode { get; set; } = false;
        public bool CloudBackupIntegration { get; set; } = false;
        
        // Yeni Aşama 5 Ayarları
        public bool EnableLowStockAlert { get; set; } = true;
        public bool EnableOverdueAlert { get; set; } = true;
        public string CustomExcelTemplatePath { get; set; } = "";
        
        // Gelişmiş Bildirim Ayarları
        public int LowStockThreshold { get; set; } = 5;
        public int OverdueDaysThreshold { get; set; } = 3;
        public bool EnableBackupAlert { get; set; } = true;
        public bool EnableCloudSyncAlert { get; set; } = true;
        public bool EnableDailySummaryAlert { get; set; } = true;
        public bool EnableRiskLimitAlert { get; set; } = true;
        public bool EnableWindowsToastAlert { get; set; } = true;
        public bool EnableChequeSenetAlert { get; set; } = true;
        public bool EnableAgingDebtAlert { get; set; } = true;
        public bool EnableNegativeStockAlert { get; set; } = true;
        public bool EnableDeadStockAlert { get; set; } = true;
        
        // Kısayollar
        public string ShortcutNewInvoice { get; set; } = "F2";
        public string ShortcutNewCari { get; set; } = "F3";
        public string ShortcutNewStok { get; set; } = "F4";
        public string ShortcutOpenRaporlar { get; set; } = "F10";
        public string ShortcutOpenFaturalar { get; set; } = "F6";
        public string ShortcutOpenCariler { get; set; } = "F7";

        // Yedekleme Konumu
        public string BackupFolderPath { get; set; } = "";

        // SMTP E-posta Ayarları
        public string SmtpHost { get; set; } = "";
        public int SmtpPort { get; set; } = 587;
        public string SmtpUser { get; set; } = "";
        public string SmtpPass { get; set; } = "";
        public bool SmtpSsl { get; set; } = true;

        // IMAP E-posta Ayarları (Gönderilen Kutusu)
        public string ImapHost { get; set; } = "";
        public int ImapPort { get; set; } = 993;
        public bool ImapSsl { get; set; } = true;

        // Telegram Bot Ayarları
        public string TelegramBotToken { get; set; } = "";
        public string TelegramChatId { get; set; } = "";

        // Firebase Auth & Google OAuth Ayarları
        public bool IsFirebaseAuthEnabled { get; set; } = false;
        public string? FirebaseAuthApiKey { get; set; } = "";
        public string? FirebaseAuthDomain { get; set; } = "";
        public string? GoogleClientId { get; set; } = "";
        public string? GoogleClientSecret { get; set; } = "";

        // Bulut QuestPDF & Cloud Run Servis Ayarları
        public string? CloudPdfApiUrl { get; set; } = "https://ermay-pdf-api-390930978984.europe-west1.run.app";
        public string? CloudPdfApiKey { get; set; } = "";
    }

    public class RecycleBinRecord
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string EntityType { get; set; } = ""; // "StokKart", "CariKart", "Fatura" vs.
        public string EntityJson { get; set; } = ""; // Silinen nesnenin JSON kopyası
        public DateTime DeletedAt { get; set; } = DateTime.Now;
        public string Description { get; set; } = ""; // "Cari Kart: Test Firması" gibi
    }

    public class CronJobRecord
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string JobName { get; set; } = ""; // Görev adı "Otomatik Yedekleme"
        public string TargetTime { get; set; } = "18:00"; // Çalışma saati
        public bool IsEnabled { get; set; } = false;
        public DateTime? LastRunTime { get; set; }
    }


    public class BelgeArsiv : ITenantEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string TenantId { get; set; } = "default";
        public string? Ad { get; set; }
        public string? Kategori { get; set; }
        public string? Tur { get; set; }
        public string? Boyut { get; set; }
        public DateTime Tarih { get; set; } = DateTime.Now;
        public string? Veri { get; set; } // Base64 for Cloud
        public string? Yol { get; set; } // Local path for Desktop
        public bool IsDeleted { get; set; }
    }

    public class Note : ITenantEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string TenantId { get; set; } = "default";
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Color { get; set; } = "#FBBF24"; 
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public int? RelatedId { get; set; } 
        public string? RelatedType { get; set; } 
        public bool IsPinned { get; set; }
        public bool IsDeleted { get; set; }
    }
}
