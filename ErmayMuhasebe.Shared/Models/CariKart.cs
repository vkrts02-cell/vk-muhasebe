using SQLite;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ErmayMuhasebe.Models
{
    public class CariKart : INotifyPropertyChanged, ITenantEntity, IBaseEntity
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private string _tenantId = "default";
        public string TenantId { get => _tenantId; set { _tenantId = value; OnPropertyChanged(); } }

        public long Version { get; set; } = 1;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        [Indexed]
        public string Uuid { get; set; } = Guid.NewGuid().ToString();

        private int _id;
        [PrimaryKey]
        public int Id { get => _id; set { _id = value; OnPropertyChanged(); } }

        private string? _cariKod;
        public string? CariKod { get => _cariKod; set { _cariKod = value; OnPropertyChanged(); } }

        private string? _unvan;
        public string? Unvan { get => _unvan; set { _unvan = value; OnPropertyChanged(); } }

        private string? _tur;
        public string? Tur { get => _tur; set { _tur = value; OnPropertyChanged(); } }

        private string? _grup;
        public string? Grup { get => _grup; set { _grup = value; OnPropertyChanged(); } }
        
        private string? _vergiDairesi;
        public string? VergiDairesi { get => _vergiDairesi; set { _vergiDairesi = value; OnPropertyChanged(); } }

        private string? _vergiNo;
        public string? VergiNo { get => _vergiNo; set { _vergiNo = value; OnPropertyChanged(); } }

        [Ignore]
        public string? VKN { get => VergiNo; set => VergiNo = value; }

        private string? _ticaretSicilNo;
        public string? TicaretSicilNo { get => _ticaretSicilNo; set { _ticaretSicilNo = value; OnPropertyChanged(); } }
        
        private string? _yetkili;
        public string? Yetkili { get => _yetkili; set { _yetkili = value; OnPropertyChanged(); } }

        private string? _telefon;
        public string? Telefon { get => _telefon; set { _telefon = value; OnPropertyChanged(); } }

        private string? _cepTelefon;
        public string? CepTelefon { get => _cepTelefon; set { _cepTelefon = value; OnPropertyChanged(); } }

        private string? _email;
        public string? Email { get => _email; set { _email = value; OnPropertyChanged(); } }

        private string? _webAdresi;
        public string? WebAdresi { get => _webAdresi; set { _webAdresi = value; OnPropertyChanged(); } }
        
        private string? _adres;
        public string? Adres { get => _adres; set { _adres = value; OnPropertyChanged(); } }

        private string? _sevkAdresi;
        public string? SevkAdresi { get => _sevkAdresi; set { _sevkAdresi = value; OnPropertyChanged(); } }

        private string? _il;
        public string? Il { get => _il; set { _il = value; OnPropertyChanged(); } }

        private string? _ilce;
        public string? Ilce { get => _ilce; set { _ilce = value; OnPropertyChanged(); } }

        private string? _postaKodu;
        public string? PostaKodu { get => _postaKodu; set { _postaKodu = value; OnPropertyChanged(); } }

        private string? _ulke;
        public string? Ulke { get => _ulke; set { _ulke = value; OnPropertyChanged(); } }
        
        private decimal _devirBorc;
        public decimal DevirBorc 
        { 
            get => _devirBorc; 
            set { _devirBorc = value; OnPropertyChanged(); OnPropertyChanged(nameof(Bakiye)); } 
        }

        private decimal _devirAlacak;
        public decimal DevirAlacak 
        { 
            get => _devirAlacak; 
            set { _devirAlacak = value; OnPropertyChanged(); OnPropertyChanged(nameof(Bakiye)); } 
        }

        private decimal _borc;
        public decimal Borc 
        { 
            get => _borc; 
            set { _borc = value; OnPropertyChanged(); OnPropertyChanged(nameof(Bakiye)); } 
        }

        private decimal _alacak;
        public decimal Alacak 
        { 
            get => _alacak; 
            set { _alacak = value; OnPropertyChanged(); OnPropertyChanged(nameof(Bakiye)); } 
        }

        [Ignore]
        public decimal Bakiye => (Borc + DevirBorc) - (Alacak + DevirAlacak);
        
        private decimal _riskLimiti;
        public decimal RiskLimiti { get => _riskLimiti; set { _riskLimiti = value; OnPropertyChanged(); } }

        private int _vadeGunu;
        public int VadeGunu { get => _vadeGunu; set { _vadeGunu = value; OnPropertyChanged(); } }

        private string? _iban;
        public string? IBAN { get => _iban; set { _iban = value; OnPropertyChanged(); } }

        private string? _odemePlani;
        public string? OdemePlani { get => _odemePlani; set { _odemePlani = value; OnPropertyChanged(); } }

        private string? _tcNo;
        public string? TCNo { get => _tcNo; set { _tcNo = value; OnPropertyChanged(); } }

        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? Aciklama { get; set; }

        public bool RiskTakibiYapilsin { get; set; }
        public bool VadeGecmisteEngelle { get; set; }
        public bool FaturadaRiskKontrolu { get; set; } = true;

        public bool AktifMi { get; set; } = true;
        public DateTime KayitTarihi { get; set; } = DateTime.Now;
        public bool IsDeleted { get; set; }

        public override string ToString() => Unvan ?? string.Empty;
    }
}
