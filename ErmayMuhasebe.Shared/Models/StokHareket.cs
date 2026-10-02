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

        public static bool IsStockInflow(string? islemTuru)
        {
            if (string.IsNullOrWhiteSpace(islemTuru)) return false;
            string tur = islemTuru.Trim();

            if (tur.Contains("Satış İade", StringComparison.OrdinalIgnoreCase) || 
                tur.Contains("Satis Iade", StringComparison.OrdinalIgnoreCase))
                return true;

            if (tur.Contains("Alış İade", StringComparison.OrdinalIgnoreCase) || 
                tur.Contains("Alis Iade", StringComparison.OrdinalIgnoreCase))
                return false;

            if (tur.Contains("Alış", StringComparison.OrdinalIgnoreCase) ||
                tur.Contains("Alis", StringComparison.OrdinalIgnoreCase) ||
                tur.Contains("Giriş", StringComparison.OrdinalIgnoreCase) ||
                tur.Contains("Giris", StringComparison.OrdinalIgnoreCase) ||
                tur.Contains("GİREN", StringComparison.OrdinalIgnoreCase) ||
                tur.Contains("Açılış", StringComparison.OrdinalIgnoreCase) ||
                tur.Contains("Acilis", StringComparison.OrdinalIgnoreCase) ||
                tur.Contains("Devir", StringComparison.OrdinalIgnoreCase) ||
                tur.Contains("Sayım Fazlası", StringComparison.OrdinalIgnoreCase) ||
                tur.Contains("Sayim Fazlasi", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }
    }
}
