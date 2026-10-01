using SQLite;
using System;

namespace ErmayMuhasebe.Models
{
    public class FaturaDetay : IBaseEntity
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public long Version { get; set; } = 1;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        [Indexed]
        public string Uuid { get; set; } = Guid.NewGuid().ToString();
        public bool IsDeleted { get; set; }
        
        [Indexed]
        public int FaturaId { get; set; }
        
        [Indexed]
        public int StokId { get; set; }
        public string? StokKodu { get; set; }
        public string? StokAdi { get; set; }
        
        public decimal Miktar { get; set; }
        public string? Birim { get; set; }
        
        public decimal BirimFiyat { get; set; }
        public int KDVOrani { get; set; } 
        public decimal KdvTutari { get; set; }

        [Ignore]
        [System.Text.Json.Serialization.JsonIgnore]
        public decimal KDVTutari { get => KdvTutari; set => KdvTutari = value; } // Made settable
        
        public decimal ToplamTutar { get; set; }
        [System.Text.Json.Serialization.JsonIgnore] 
        public decimal Tutar => ToplamTutar; 
        public string? Aciklama { get; set; }
    }
}
