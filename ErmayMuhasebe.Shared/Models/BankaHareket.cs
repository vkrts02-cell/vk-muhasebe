using SQLite;
using System;

namespace ErmayMuhasebe.Models
{
    public class BankaHareket : ITenantEntity, IBaseEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string TenantId { get; set; } = "default";
        public long Version { get; set; } = 1;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        [Indexed]
        public string Uuid { get; set; } = Guid.NewGuid().ToString();
        public bool IsDeleted { get; set; }
        [Indexed]
        public int BankaId { get; set; }
        public string? BankaAdi { get; set; } 
        public string? IBAN { get; set; } 
        
        public DateTime Tarih { get; set; } = DateTime.Now;
        [Indexed]
        public string? EvrakNo { get; set; }
        public string? IslemTuru { get; set; } // İşlem Türü (Tahsilat, Ödeme vb.)

        // New properties for Endorsement (Ciro)
        public int? YonlendirilenCariId { get; set; }
        public string? YonlendirilenCariUnvan { get; set; }
        public DateTime? YonlendirmeTarihi { get; set; }
        public string? Aciklama { get; set; }
        public string? CariUnvan { get; set; }
        [Indexed]
        public int? CariId { get; set; } // Added CariId to link with customer
        [Indexed]
        public int? FaturaId { get; set; }
        
        [Indexed]
        public string? RefId { get; set; }
        
        public decimal Giren { get; set; }
        public decimal Cikan { get; set; }
        public decimal Tutar { get; set; }
        public int BankaKartId { get => BankaId; set => BankaId = value; } 

        // New fields for Havale/EFT logic
        public string? DekontPath { get; set; } // Path to attached receipt
        public string? Durum { get; set; } = "Tamamlandı"; // Beklemede, Tamamlandı, Yönlendirildi
    }
}
