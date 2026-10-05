using SQLite;
using System;

namespace ErmayMuhasebe.Models
{
    public class Cek : ITenantEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string TenantId { get; set; } = "default";
        public string? CekNo { get; set; }
        public int? CariId { get; set; } 
        public string? CariUnvan { get; set; }
        public DateTime IslemTarihi { get; set; } = DateTime.Now;

        public string? PortfoyNo { get; set; }
        public string? SeriNo { get; set; }
        public DateTime VadeTarihi { get; set; }
        public decimal Tutar { get; set; }
        
        public string? Borclu { get; set; }
        [Ignore]
        public string? AsilBorclu { get => Borclu; set => Borclu = value; } 

        public string? Banka { get; set; }
        public string? Sube { get; set; }
        public string? HesapNo { get; set; }
        
        public string? CekTuru { get; set; } 
        public string? Durum { get; set; } 
        public string? Aciklama { get; set; }
        public string? IslemTuru { get; set; } // İşlem Türü (Tahsilat, Ödeme vb.)

        // New properties for Endorsement (Ciro)
        public int? YonlendirilenCariId { get; set; }
        public string? YonlendirilenCariUnvan { get; set; }
        public DateTime? YonlendirmeTarihi { get; set; }

        // New properties for PDF images
        public string? GorselYoluOn { get; set; }
        public string? GorselYoluArka { get; set; }
        public string? GorselYolu { get; set; } // Added generic path if needed
        public bool IsDeleted { get; set; }
    }

    public class Senet : ITenantEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string TenantId { get; set; } = "default";
        public string? SenetNo { get; set; }
        public int? CariId { get; set; } 
        public string? CariUnvan { get; set; }

        public string? PortfoyNo { get; set; }
        public DateTime VadeTarihi { get; set; }
        public decimal Tutar { get; set; }
        public string? Borclu { get; set; }
        [Ignore]
        public string? AsilBorclu { get => Borclu; set => Borclu = value; } // Added alias

        public string? SenetTuru { get; set; }
        public string? Durum { get; set; }
        public string? Aciklama { get; set; }
        public bool IsDeleted { get; set; }
    }
}
