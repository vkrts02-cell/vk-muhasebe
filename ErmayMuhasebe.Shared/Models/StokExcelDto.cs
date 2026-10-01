namespace ErmayMuhasebe.Models;

public class StokExcelDto
{
    public string? StokKodu { get; set; }
    public string? StokAdi { get; set; }
    public string? Barkod { get; set; }
    public string? Birim { get; set; } = "Adet";
    public string? Kategori { get; set; }
    public decimal AlisFiyati { get; set; }
    public decimal SatisFiyati { get; set; }
    public int KDV { get; set; } = 20;
    public decimal Miktar { get; set; }
    public decimal MinSeviye { get; set; }
    public string? Aciklama { get; set; }
}

