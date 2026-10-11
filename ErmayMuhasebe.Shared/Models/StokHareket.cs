using SQLite;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ErmayMuhasebe.Models
{
    public class StokHareket : INotifyPropertyChanged, ITenantEntity, IBaseEntity
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public long Version { get; set; } = 1;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        [Indexed]
        public string Uuid { get; set; } = Guid.NewGuid().ToString();
        public bool IsDeleted { get; set; }

        private string _tenantId = "default";
        public string TenantId { get => _tenantId; set { _tenantId = value; OnPropertyChanged(); } }

        private int _id;
        [PrimaryKey, AutoIncrement]
        public int Id { get => _id; set { _id = value; OnPropertyChanged(); } }

        private int _stokId;
        [Indexed]
        public int StokId { get => _stokId; set { _stokId = value; OnPropertyChanged(); } }

        [Indexed]
        public string? EvrakNo { get; set; }
        public string? IslemTuru { get; set; } 
        public DateTime Tarih { get; set; } = DateTime.Now;
        public decimal Giren { get; set; }
        public decimal Cikan { get; set; }
        public string? Aciklama { get; set; }
        public decimal Fiyat { get; set; }
        public string? Birim { get; set; }
        public string? EvrakTuru { get; set; }
        public string? StokKodu { get; set; }
        public string? StokAdi { get; set; }
        private decimal _miktar;
        public decimal Miktar { get => _miktar; set { _miktar = value; OnPropertyChanged(); } }

        private decimal _kalanMiktar;
        public decimal KalanMiktar { get => _kalanMiktar; set { _kalanMiktar = value; OnPropertyChanged(); } }

        [Indexed]
        public int? FaturaId { get; set; }

        [Ignore]
        public int StokKartId { get => StokId; set => StokId = value; }

        [Ignore]
        public bool IsGiris
        {
            get
            {
                if (Giren > 0 && Cikan == 0) return true;
                if (Cikan > 0 && Giren == 0) return false;
                return IsStockInflow(IslemTuru);
            }
        }

        private static string NormalizeTurkish(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            return text
                .Replace('İ', 'i')
                .Replace('I', 'i')
                .Replace('ı', 'i')
                .Replace('i', 'i')
                .Replace('Ş', 's')
                .Replace('ş', 's')
                .Replace('Ğ', 'g')
                .Replace('ğ', 'g')
                .Replace('Ü', 'u')
                .Replace('ü', 'u')
                .Replace('Ö', 'o')
                .Replace('ö', 'o')
                .Replace('Ç', 'c')
                .Replace('ç', 'c')
                .Trim()
                .ToLowerInvariant();
        }

        public static bool IsStockInflow(string? islemTuru)
        {
            if (string.IsNullOrWhiteSpace(islemTuru)) return false;
            string norm = NormalizeTurkish(islemTuru);

            if (norm.Contains("satis iade"))
                return true;

            if (norm.Contains("alis iade"))
                return false;

            if (norm.Contains("alis") ||
                norm.Contains("giris") ||
                norm.Contains("giren") ||
                norm.Contains("acilis") ||
                norm.Contains("devir") ||
                norm.Contains("sayim fazlasi"))
            {
                return true;
            }

            return false;
        }
    }
}
