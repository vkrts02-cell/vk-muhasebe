using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ErmayMuhasebe.Models;

namespace ErmayMuhasebe.Services;

public class ReportSection
{
    public string Title { get; set; } = "";
    public string[] Headers { get; set; } = Array.Empty<string>();
    public List<string[]> Rows { get; set; } = new();
    public List<ChartDataItem>? ChartData { get; set; }
    public List<ChartDataItem>? KeyMetrics { get; set; }
    public List<(string Label, decimal Value)>? SingleBarData { get; set; }
    public string SingleBarColor { get; set; } = "#3b82f6";
    public List<(string Label, decimal Value)>? HorizontalBarData { get; set; }
    public string HorizontalBarColor { get; set; } = "#8b5cf6";
    public string LeftChartTitle { get; set; } = "PERFORMANS ANALİZİ";
    public string RightChartTitle { get; set; } = "DAĞILIM ANALİZİ";
    public string ActualLabel { get; set; } = "Gerçekleşen";
    public string TargetLabel { get; set; } = "Hedef";
    public string? FooterNote { get; set; }
    public bool NewPage { get; set; }
    public List<ReportChart> ExtraCharts { get; set; } = new();
}

public class ReportChart
{
    public string Title { get; set; } = "";
    public List<ChartDataItem>? BarData { get; set; }
    public List<(string Label, decimal Value)>? SingleData { get; set; }
    public string Color { get; set; } = "#3b82f6";
    public string ActualLabel { get; set; } = "Alınan";
    public string TargetLabel { get; set; } = "Yönlendirilen";
}

public class ChartDataItem
{
    public string Label { get; set; } = "";
    public decimal Value { get; set; }
    public decimal Target { get; set; }
    public decimal Actual { get; set; }
}

public interface IPdfService
{
    Task<(bool Success, string Error)> GenerateBudgetReportPdfAsync(string title, List<ChartDataItem> annual, List<ChartDataItem> monthly, List<ChartDataItem> weekly);
    Task<(bool Success, string Error)> GenerateConsolidatedReportPdfAsync(string title, List<ReportSection> sections);
    Task<(bool Success, string Error)> GenerateFaturaPdfAsync(Fatura fatura, List<FaturaDetay> detaylar);
    Task<(bool Success, string Error)> GenerateTeklifPdfAsync(Teklif teklif, List<TeklifDetay> detaylar, CariKart? cari);
    Task<(bool Success, string Error)> GenerateSiparisPdfAsync(Siparis siparis, List<SiparisDetay> detaylar, CariKart? cari = null);
    Task<(bool Success, string Error)> GenerateGenericTablePdfAsync(string title, string[] headers, List<string[]> rows, string? subtitle = null);
    Task<(bool Success, string Error)> GenerateMakbuzPdfAsync(string? makbuzTipi, string? cariUnvan, DateTime tarih, decimal tutar, string? aciklama);
    Task<(bool Success, string Error)> GenerateCariEkstrePdfAsync(CariKart cari, List<CariHareket> hareketler);
    Task<(bool Success, string Error)> GenerateDetayliCariEkstrePdfAsync(CariKart cari, List<CariHareket> hareketler, Dictionary<int, List<FaturaDetay>> detaylar);
    Task<(bool Success, string Error)> GenerateEftPdfAsync(EftIslem islem);
    Task<(bool Success, string Error)> GenerateKkPdfAsync(KrediKartiIslem request);
    Task<(bool Success, string Error)> GenerateMusteriTakipRaporuPdfAsync(MusteriTakipKlasor klasor, List<MusteriTakipDetay> detaylar);

    // Raw byte generation methods (used by ViewModels to get bytes for open/print)
    Task<byte[]> GenerateFaturaPdfBytesAsync(Fatura fatura, List<FaturaDetay> detaylar);
    Task<byte[]> GenerateTeklifPdfBytesAsync(Teklif teklif, List<TeklifDetay> detaylar, CariKart? cari);
    Task<byte[]> GenerateSiparisPdfBytesAsync(Siparis siparis, List<SiparisDetay> detaylar, CariKart? cari);
    Task<byte[]> GenerateGenericTablePdfBytesAsync(string title, string[] headers, List<string[]> rows, string? subtitle = null);
    Task<byte[]> GenerateCariEkstrePdfBytesAsync(CariKart cari, List<CariHareket> hareketler);
    Task<byte[]> GenerateDetayliCariEkstrePdfBytesAsync(CariKart cari, List<CariHareket> hareketler, Dictionary<int, List<FaturaDetay>> detaylar);
    Task<byte[]> GenerateFaturaBatchPdfBytesAsync(List<(Fatura Fatura, List<FaturaDetay> Detaylar)> items);
    Task<byte[]> GenerateMakbuzPdfBytesAsync(string? makbuzTipi, string? cariUnvan, DateTime tarih, decimal tutar, string? aciklama);
    Task<byte[]> GenerateEftSlipPdfBytesAsync(EftIslem islem);
    Task<byte[]> GenerateKrediKartiSlipPdfBytesAsync(KrediKartiIslem islem);
    Task<byte[]> GenerateBudgetReportPdfBytesAsync(string title, List<ChartDataItem> annual, List<ChartDataItem> monthly, List<ChartDataItem> weekly);
    Task<byte[]> GenerateConsolidatedReportPdfBytesAsync(string title, List<ReportSection> sections);
    
    // NEW ONES FROM PdfService
    Task<byte[]> GenerateKasaEkstrePdfBytesAsync(BankaKart kasa, List<KasaHareket> hareketler);
    Task<byte[]> GenerateMakbuzFromKasaPdfBytesAsync(KasaHareket h);
    Task<byte[]> GenerateCekPdfBytesAsync(Cek cek);
    Task<byte[]> GenerateCekListPdfBytesAsync(List<Cek> cekler);
    Task<byte[]> GenerateKrediKartiListPdfBytesAsync(List<KrediKartiIslem> islemler);
    Task<byte[]> GenerateEftListPdfBytesAsync(List<EftIslem> islemler);
    Task<byte[]> GenerateKasaListesiPdfBytesAsync(List<BankaKart> kasalar);
    Task<byte[]> GenerateAdresEtiketiPdfBytesAsync(string unvan, string adSoyad, string adres, string ilIlce, string postaKodu, string telefon);
    Task<byte[]> GenerateFaturaListPdfBytesAsync(List<Fatura> faturalar, DateTime startDate, DateTime endDate);
    Task<byte[]> GenerateFinansRaporPdfBytesAsync(string title, FinancialReportData data);
    Task<byte[]> GenerateStokListPdfBytesAsync(List<StokKart> stoklar);
    Task<byte[]> GenerateStokHareketleriPdfBytesAsync(StokKart stok, List<StokHareket> hareketler);
    Task<byte[]> GenerateCokluStokHareketleriPdfBytesAsync(List<(StokKart Stok, List<StokHareket> Hareketler)> data);
    Task<byte[]> GenerateVadeRaporuPdfBytesAsync(List<VadeReportItem> items);
    Task<byte[]> GenerateMusteriTakipRaporuPdfBytesAsync(MusteriTakipKlasor klasor, List<MusteriTakipDetay> detaylar);

    byte[]? LogoBytes { get; set; }
    void ResetLogoCache();
    bool ShowLogoFatura { get; set; }
    bool ShowLogoSiparis { get; set; }
    bool ShowLogoTeklif { get; set; }
    bool ShowLogoEkstre { get; set; }
    bool ShowLogoRaporlar { get; set; }

    // Fatura çıktı düzeni (Fatura Tasarımı modülü) — tüm platformlarda birebir aynı olması için
    string FaturaSize { get; set; }
    string FaturaOrientation { get; set; }
    FaturaTasarimi? AktifFaturaTasarimi { get; set; }
    string TeklifSize { get; set; }
    string TeklifOrientation { get; set; }
    string SiparisSize { get; set; }
    string SiparisOrientation { get; set; }
}


