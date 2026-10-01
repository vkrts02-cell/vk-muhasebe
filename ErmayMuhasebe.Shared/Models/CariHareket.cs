using SQLite;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ErmayMuhasebe.Models
{
    public class CariHareket : ITenantEntity, INotifyPropertyChanged, IBaseEntity
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public long Version { get; set; } = 1;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        [Indexed]
        public string Uuid { get; set; } = Guid.NewGuid().ToString();
        public bool IsDeleted { get; set; }

        private int _id;
        [PrimaryKey, AutoIncrement]
        public int Id { get => _id; set { _id = value; OnPropertyChanged(); } }

        public string TenantId { get; set; } = "default";
        
        [Indexed]
        public int CariId { get; set; }
        
        public string? CariUnvan { get; set; }
        
        public DateTime Tarih { get; set; } = DateTime.Now;
        public DateTime? Vade { get; set; } 
        
        [Indexed]
        public string? EvrakNo { get; set; }
        public string? IslemTuru { get; set; } 
        public string? Aciklama { get; set; }
        
        public decimal Borc { get; set; }
        public decimal Alacak { get; set; }
        
        [Ignore]
        public decimal KalanBakiye { get; set; }
        
        public string? SlipImage { get; set; } 
        [Indexed]
        public int? FaturaId { get; set; }
        
        [Indexed]
        public string? RefId { get; set; }
        [Indexed]
        public int? YonlendirilenCariId { get; set; }
        public string? YonlendirilenCariUnvan { get; set; }

        private bool _isSelected;
        [Ignore]
        public bool IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChanged(); } }
    }
}
