using SQLite;
using System;

namespace ErmayMuhasebe.Models
{
    public class Gorev
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string? Baslik { get; set; }
        public string? Aciklama { get; set; }
        public int AtananPersonelId { get; set; }
        public string? AtananPersonelAd { get; set; } // Missing
        public string? Durum { get; set; } 
        public string? Oncelik { get; set; } // Missing
        public DateTime SonTarih { get; set; }
        public DateTime BitisTarihi { get; set; } // Missing
        public DateTime OlusturmaTarihi { get; set; }
        public bool IsDeleted { get; set; }
    }
}
