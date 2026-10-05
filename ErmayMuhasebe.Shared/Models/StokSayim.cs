using SQLite;
using System;
using System.Collections.Generic;

namespace ErmayMuhasebe.Models
{
    public class StokSayimFisi
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string? FisNo { get; set; }
        public DateTime Tarih { get; set; } = DateTime.Now;
        public string? Aciklama { get; set; }
        public string? SayimYapan { get; set; }
        public bool IsApplied { get; set; } // Stoklara işlendi mi?
        public bool IsDeleted { get; set; }

        [Ignore]
        public List<StokSayimDetay> Detaylar { get; set; } = new();
    }

    public class StokSayimDetay
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public int FisId { get; set; }
        
        public int StokId { get; set; }
        public string? StokKodu { get; set; }
        public string? StokAdi { get; set; }
        
        public decimal MevcutMiktar { get; set; } // Sistemdeki
        public decimal SayilanMiktar { get; set; } // Gerçek
        public decimal Fark { get => SayilanMiktar - MevcutMiktar; }
        
        public string? Aciklama { get; set; }
    }
}
