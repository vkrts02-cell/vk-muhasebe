using SQLite;
using System;

namespace ErmayMuhasebe.Models
{
    public class BankaKart : ITenantEntity, IBaseEntity
    {
        [PrimaryKey]
        public int Id { get; set; }
        public string TenantId { get; set; } = "default";
        public long Version { get; set; } = 1;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        [Indexed]
        public string Uuid { get; set; } = Guid.NewGuid().ToString();
        public string? BankaAdi { get; set; }
        public string? SubeAdi { get; set; }
        public string? SubeKodu { get; set; } // Missing
        public string? HesapNo { get; set; }
        public string? IBAN { get; set; } // Iban -> IBAN (Capitalized)
        
        public string? DovizTuru { get; set; } = "TL"; // ParaBirimi -> DovizTuru
        public string? Yetkili { get; set; } // Missing
        public string? Telefon { get; set; } // Missing
        
        public string? KartTuru { get; set; } // Vadesiz, Kredi Karti vb.
        
        public decimal AcilisBakiyesi { get; set; }
        public decimal GuncelBakiye { get; set; } 
        public decimal Bakiye { get => GuncelBakiye; set => GuncelBakiye = value; } 
        public decimal MevcutBakiye { get => GuncelBakiye; set => GuncelBakiye = value; } 
        public bool IsDeleted { get; set; }
    }
}
