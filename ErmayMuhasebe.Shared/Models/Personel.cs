using SQLite;
using System;

namespace ErmayMuhasebe.Models
{
    public class Personel
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string? Ad { get; set; } 
        public string? Soyad { get; set; } 
        public string? AdSoyad { get => $"{Ad} {Soyad}"; set { /* Split logic if needed */ } } 
        public string? Telefon { get; set; }
        public string? Gorevi { get; set; }
        public string? Unvan { get => Gorevi; set => Gorevi = value; } // Alias for Unvan
        public string? Email { get; set; } // Missing
        public decimal Maas { get; set; }
        public DateTime IseGirisTarihi { get; set; }
        public bool IsDeleted { get; set; }
    }
}
