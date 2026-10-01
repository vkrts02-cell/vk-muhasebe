using SQLite;
using System;
using System.Collections.Generic;

namespace ErmayMuhasebe.Models
{
    public class Fatura : ITenantEntity, IBaseEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string TenantId { get; set; } = "default";
        public long Version { get; set; } = 1;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        [Indexed]
        public string Uuid { get; set; } = Guid.NewGuid().ToString();
        public string? FaturaNo { get; set; }
        public DateTime Tarih { get; set; } = DateTime.Now;
        public DateTime VadeTarihi { get; set; } = DateTime.Now;
        
        public int CariId { get; set; }
        public string? CariUnvan { get; set; }
        public string? VergiDairesi { get; set; }
        public string? VergiNo { get; set; }
        public string? Adres { get; set; }

        public string? Tur { get; set; } 
        public string? Aciklama { get; set; }
        
        public string? DovizTuru { get; set; } 
        public decimal DovizKuru { get; set; } 

        public decimal AraToplam { get; set; }
        public decimal ToplamKDV { get; set; } 
        public decimal KdvToplam { get => ToplamKDV; set => ToplamKDV = value; }
        public decimal GenelToplam { get; set; }
        public decimal Odenen { get; set; } 
        public decimal Kalan => GenelToplam - Odenen; 
        public decimal Bakiye => Kalan; 

        public bool IptalMi { get; set; }
        public DateTime KayitTarihi { get; set; } = DateTime.Now;

        public string? OdemeSekli { get; set; } = "Açık Hesap"; // Açık Hesap, Nakit, Kredi Kartı
        public int? KasaId { get; set; }
        public int? BankaId { get; set; }
        
        public string? BaglantiEvrakNo { get; set; } // Missing from one error log
        public bool IsEArsiv { get; set; } // E-Arşiv Faturası mı?

        [Ignore]
        public List<FaturaDetay> Detaylar { get; set; } = new();
        public bool IsDeleted { get; set; } // Soft Delete
    }
}
