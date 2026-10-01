using SQLite;
using System;

namespace ErmayMuhasebe.Models
{
    public class KasaHareket : ITenantEntity, IBaseEntity
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
        public int KasaId { get; set; } // Added KasaId
        public DateTime Tarih { get; set; } = DateTime.Now;
        [Indexed]
        public string? EvrakNo { get; set; }
        public string? Aciklama { get; set; }
        public string? CariUnvan { get; set; }
        [Indexed]
        public int? CariId { get; set; } // Added CariId to link with customer
        public string? IslemTuru { get; set; } 
        
        [Indexed]
        public int? FaturaId { get; set; }
        
        [Indexed]
        public string? RefId { get; set; }
        [Indexed]
        public int? YonlendirilenCariId { get; set; }
        public string? YonlendirilenCariUnvan { get; set; }
        public DateTime? YonlendirmeTarihi { get; set; }

        public decimal Giren { get; set; }
        public decimal Cikan { get; set; }
        
        private decimal _tutar;
        public decimal Tutar 
        { 
            get => _tutar != 0 ? _tutar : (Giren > 0 ? Giren : Cikan); 
            set => _tutar = value; 
        }
    }
}
