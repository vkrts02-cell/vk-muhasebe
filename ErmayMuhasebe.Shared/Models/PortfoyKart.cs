using SQLite;
using System;
using System.Linq;
using System.Collections.Generic;

namespace ErmayMuhasebe.Models
{
    public class PortfoyKart
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string? PortfoyNo { get; set; }
        public string? FirmaIsmi { get; set; } 
        public string? CariTipi { get; set; } // Müşteri, Tedarikçi
        public string? YetkiliKisi { get; set; }
        public int VadeGunu { get; set; } = 30;
        public decimal RiskLimiti { get; set; }
        
        public string? VKN { get; set; } 
        public string? VergiDairesi { get; set; } 
        public string? GSM { get; set; } 
        public string? Telefon { get; set; } 
        public string? Email { get; set; } 
        public string? WebSitesi { get; set; } 
        
        public string? Ulke { get; set; } = "Türkiye";
        public string? Il { get; set; } 
        public string? Ilce { get; set; } 
        public string? Adres { get; set; } 
        public string? SevkAdresi { get; set; }
        
        public string? Sektor { get; set; } 
        public string? Aciklama { get; set; }
        public decimal AcilisBakiyesi { get; set; }
        
        // Financial Risk Parameters
        public bool RiskTakibiYapilsin { get; set; }
        public bool VadeGecmisteEngelle { get; set; }
        public bool FaturadaRiskKontrolu { get; set; } = true;
        
        public bool Gorusuldu { get; set; }
        public string? Notlar { get; set; }
        public DateTime KayitTarihi { get; set; } = DateTime.Now;
        public DateTime SonGuncelleme { get; set; } = DateTime.Now;
        public bool IsDeleted { get; set; }

        public static PortfoyKart FromDictionary(IDictionary<string, object> dict)
        {
            var p = new PortfoyKart();
            
            string GetVal(params string[] keys)
            {
                foreach (var key in keys)
                {
                    if (dict.TryGetValue(key, out var val) && val != null)
                    {
                        return val.ToString() ?? "";
                    }
                    var match = dict.Keys.FirstOrDefault(k => k.Equals(key, StringComparison.OrdinalIgnoreCase));
                    if (match != null && dict[match] != null)
                    {
                        return dict[match].ToString() ?? "";
                    }
                }
                return "";
            }

            p.FirmaIsmi = GetVal("FirmaIsmi", "Firma", "Firma İsmi", "Ünvan", "Unvan");
            p.YetkiliKisi = GetVal("YetkiliKisi", "Yetkili Kisi", "Yetkili", "Yetkili Kişi");
            p.GSM = GetVal("GSM", "CepTelefonu", "Cep Telefonu", "Cep Telefon");
            p.Telefon = GetVal("Telefon");
            p.Email = GetVal("Email", "E-Mail", "E-Posta", "Eposta");
            p.WebSitesi = GetVal("WebSitesi", "Web Sitesi", "Web", "Web Adresi");
            p.Adres = GetVal("Adres", "Açık Adres", "Acik Adres");
            p.SevkAdresi = GetVal("SevkAdresi", "Sevk Adresi");
            p.Il = GetVal("Il", "İl", "Şehir", "Sehir");
            p.Ilce = GetVal("Ilce", "İlçe");
            p.Sektor = GetVal("Sektor", "Sektör");
            p.Aciklama = GetVal("Aciklama", "Açıklama", "Notlar", "Not");
            p.Notlar = p.Aciklama;
            p.VKN = GetVal("VKN", "VergiNo", "Vergi No", "Vergi Numarası", "TCNo", "TC Kimlik No");
            p.VergiDairesi = GetVal("VergiDairesi", "Vergi Dairesi");
            p.CariTipi = GetVal("CariTipi", "Cari Tipi", "Tür", "Tur");
            
            if (int.TryParse(GetVal("VadeGunu", "Vade Günü", "Vade"), out var vade)) p.VadeGunu = vade;
            if (decimal.TryParse(GetVal("RiskLimiti", "Risk Limiti", "Limit"), out var limit)) p.RiskLimiti = limit;
            if (decimal.TryParse(GetVal("AcilisBakiyesi", "Açılış Bakiyesi", "Bakiye"), out var bakiye)) p.AcilisBakiyesi = bakiye;

            return p;
        }
    }
}
