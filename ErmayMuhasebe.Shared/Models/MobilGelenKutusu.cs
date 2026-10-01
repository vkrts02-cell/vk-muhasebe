using SQLite;
using System;

namespace ErmayMuhasebe.Models
{
    public class MobilGelenKutusu
    {
        [PrimaryKey]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Indexed]
        public string IslemTuru { get; set; } = ""; // Tahsilat, Odeme, TaslakFatura, Siparis, Teklif, YeniCari, ZiyaretNotu

        public string? KaynakCihaz { get; set; }

        public string Payload { get; set; } = "{}"; // JSON string containing transaction payload

        [Indexed]
        public string Durum { get; set; } = "Bekliyor"; // Bekliyor, Islendi, Hata, Iptal

        public string? HataMesaji { get; set; }

        public string? ResmiEvrakNo { get; set; }

        [Indexed]
        public int MaliYil { get; set; } = DateTime.Now.Year;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? IslenmeTarihi { get; set; }
    }
}
