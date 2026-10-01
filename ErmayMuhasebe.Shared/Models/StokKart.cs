using SQLite;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ErmayMuhasebe.Models
{
    public class StokKart : INotifyPropertyChanged, ITenantEntity, IBaseEntity
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private string _tenantId = "default";
        public string TenantId { get => _tenantId; set { _tenantId = value; OnPropertyChanged(); } }
        
        public long Version { get; set; } = 1;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        private int _id;
        [PrimaryKey]
        public int Id { get => _id; set { _id = value; OnPropertyChanged(); } }

        private string? _stokKodu;
        public string? StokKodu { get => _stokKodu; set { _stokKodu = value; OnPropertyChanged(); } }

        private string? _stokAdi;
        public string? StokAdi { get => _stokAdi; set { _stokAdi = value; OnPropertyChanged(); } }

        private string? _barkod;
        public string? Barkod { get => _barkod; set { _barkod = value; OnPropertyChanged(); } }

        private string? _birim = "Adet";
        public string? Birim { get => _birim; set { _birim = value; OnPropertyChanged(); } }

        private string? _kategori;
        public string? Kategori { get => _kategori; set { _kategori = value; OnPropertyChanged(); } }

        private decimal _alisFiyati;
        public decimal AlisFiyati { get => _alisFiyati; set { _alisFiyati = value; OnPropertyChanged(); } }

        private decimal _ortalamaAlisFiyati;
        public decimal OrtalamaAlisFiyati { get => _ortalamaAlisFiyati; set { _ortalamaAlisFiyati = value; OnPropertyChanged(); } }

        private decimal _satisFiyati;
        public decimal SatisFiyati { get => _satisFiyati; set { _satisFiyati = value; OnPropertyChanged(); } }

        private decimal _ortalamaSatisFiyati;
        public decimal OrtalamaSatisFiyati { get => _ortalamaSatisFiyati; set { _ortalamaSatisFiyati = value; OnPropertyChanged(); } }

        private int _kdv = 20;
        public int KDV { get => _kdv; set { _kdv = value; OnPropertyChanged(); OnPropertyChanged(nameof(KdvOrani)); } }

        private decimal _miktar;
        public decimal Miktar { get => _miktar; set { _miktar = value; OnPropertyChanged(); } }

        private decimal _minSeviye;
        public decimal MinSeviye { get => _minSeviye; set { _minSeviye = value; OnPropertyChanged(); OnPropertyChanged(nameof(KritikSeviye)); } }

        private string? _aciklama;
        public string? Aciklama { get => _aciklama; set { _aciklama = value; OnPropertyChanged(); } }

        private DateTime _kayitTarihi = DateTime.Now;
        public DateTime KayitTarihi { get => _kayitTarihi; set { _kayitTarihi = value; OnPropertyChanged(); } }

        // Compatibility Aliases (Not stored in DB)
        [Ignore] public string? Grup { get => Kategori; set => Kategori = value; }
        [Ignore] public int KdvOrani { get => KDV; set => KDV = value; }
        [Ignore] public decimal KritikSeviye { get => MinSeviye; set => MinSeviye = value; }
        
        public bool IsDeleted { get; set; }
        [Ignore] public string? StokGrubu { get => Kategori; set => Kategori = value; }
        
        private bool _isSelected;
        [Ignore]
        public bool IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChanged(); } }

        public override string ToString() => StokAdi ?? string.Empty;
    }
}
