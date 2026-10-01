using SQLite;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ErmayMuhasebe.Models
{
    public class StokGrupDef : INotifyPropertyChanged, IBaseEntity, ITenantEntity
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private int _id;
        [PrimaryKey]
        public int Id { get => _id; set { _id = value; OnPropertyChanged(); } }

        private string _tenantId = "default";
        public string TenantId { get => _tenantId; set { _tenantId = value; OnPropertyChanged(); } }

        public long Version { get; set; } = 1;
        [Indexed]
        public string Uuid { get; set; } = Guid.NewGuid().ToString();

        private string _ad = "";
        public string Ad { get => _ad; set { _ad = value; OnPropertyChanged(); } }

        private DateTime _updatedAt = DateTime.Now;
        public DateTime UpdatedAt { get => _updatedAt; set { _updatedAt = value; OnPropertyChanged(); } }

        private bool _isDeleted;
        public bool IsDeleted { get => _isDeleted; set { _isDeleted = value; OnPropertyChanged(); } }

        public override string ToString() => Ad;
    }
}
