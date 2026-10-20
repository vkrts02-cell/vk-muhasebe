using System.Net.Http.Json;
using ErmayMuhasebe.Models;
using Microsoft.JSInterop;
using ErmayMuhasebe.Services;
using ErmayMuhasebe.Repositories;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ErmayMuhasebe.Cloud.Services;

public class CloudPdfService : IPdfService
{
    private readonly HttpClient _httpClient;
    private readonly IJSRuntime _js;
    private readonly IUnitOfWork _uow;
    
    // API URL - Google Cloud Run 7/24 Canlı PDF Servisi
    private const string ApiBaseUrl = "https://ermay-pdf-api-390930978984.europe-west1.run.app";
    private readonly System.Text.Json.JsonSerializerOptions _jsonOptions = new() { MaxDepth = 256, PropertyNameCaseInsensitive = true };

    public CloudPdfService(HttpClient httpClient, IJSRuntime js, IUnitOfWork uow)
    {
        _httpClient = httpClient;
        _js = js;
        _uow = uow;
    }

    private async Task<(bool Success, string Error)> SendPdfRequest(string endpoint, object data)
    {
        try
        {
            Console.WriteLine($"PDF isteği gönderiliyor: {endpoint}");
            var response = await _httpClient.PostAsJsonAsync($"{ApiBaseUrl}/generate/{endpoint}", data, _jsonOptions);
            
            if (response.IsSuccessStatusCode)
            {
                var pdfBytes = await response.Content.ReadAsByteArrayAsync();
                
                if (pdfBytes == null || pdfBytes.Length == 0)
                {
                    return (false, "Sunucu boş bir PDF dosyası döndürdü.");
                }

                Console.WriteLine($"PDF alındı: {pdfBytes.Length} byte. JS tarafına aktarılıyor...");
                await _js.InvokeVoidAsync("openPdf", pdfBytes);
                return (true, "");
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            return (false, $"Sunucu Hatası ({response.StatusCode}): {errorContent}");
        }
        catch (Exception ex)
        {
            var targetFullUrl = $"{ApiBaseUrl}/generate/{endpoint}";
            Console.WriteLine($"[PDF-CLIENT] HATA! URL: {targetFullUrl} | Mesaj: {ex.Message}");
            
            if (ex.Message.Contains("fetch", StringComparison.OrdinalIgnoreCase))
            {
                return (false, $"Tarayıcı '{targetFullUrl}' adresine bağlanamadı (CORS veya Ağ Hatası).\n1. API penceresinin açık olduğunu kontrol edin.\n2. Web uygulamasını HTTPS (https://) ile değil, mutlaka HTTP (http://localhost:5242) üzerinden açtığınızdan emin olun.");
            }
            
        return (false, $"Bağlantı Hatası: {ex.Message}");
        }
    }

    private async Task<byte[]> FetchPdfBytes(string endpoint, object data)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"{ApiBaseUrl}/generate/{endpoint}", data, _jsonOptions);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsByteArrayAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PDF-FETCH] HATA! {endpoint} | {ex.Message}");
        }
        return Array.Empty<byte>();
    }

    private async Task<(byte[]? Logo, FirmaProfili? Profil)> GetLogoAndProfileAsync()
    {
        var profil = await _uow.GetFirmaProfiliAsync();
        byte[]? logo = null;
        if (LogoBytes != null)
        {
            logo = LogoBytes.Length > 0 ? LogoBytes : null;
        }
        else if (profil != null && !string.IsNullOrEmpty(profil.LogoBase64))
        {
            try { logo = Convert.FromBase64String(profil.LogoBase64); } catch { }
        }
        return (logo, profil);
    }

    public async Task<(bool Success, string Error)> GenerateFaturaPdfAsync(Fatura fatura, List<FaturaDetay> detaylar) 
    {
        var (logo, profil) = await GetLogoAndProfileAsync();
        return await SendPdfRequest("fatura", new ErmayMuhasebe.Models.FaturaRequest(fatura, detaylar, logo, profil?.LogoFatura ?? ShowLogoFatura, FaturaSize, FaturaOrientation, AktifFaturaTasarimi));
    }

    public async Task<(bool Success, string Error)> GenerateBatchFaturaPdfAsync(List<(Fatura Fatura, List<FaturaDetay> Detaylar)> items)
    {
        var (logo, profil) = await GetLogoAndProfileAsync();
        var requests = items.Select(x => new ErmayMuhasebe.Models.FaturaRequest(x.Fatura, x.Detaylar, logo, profil?.LogoFatura ?? ShowLogoFatura, FaturaSize, FaturaOrientation, AktifFaturaTasarimi)).ToList();
        return await SendPdfRequest("fatura-batch", requests);
    }
 
    public async Task<(bool Success, string Error)> GenerateTeklifPdfAsync(Teklif teklif, List<TeklifDetay> detaylar, CariKart? cari)
    {
        var (logo, profil) = await GetLogoAndProfileAsync();
        return await SendPdfRequest("teklif", new ErmayMuhasebe.Models.TeklifRequest(teklif, detaylar, cari, logo, profil?.LogoTeklif ?? ShowLogoTeklif, TeklifSize, TeklifOrientation));
    }
 
    public async Task<(bool Success, string Error)> GenerateSiparisPdfAsync(Siparis siparis, List<SiparisDetay> detaylar, CariKart? cari = null)
    {
        var (logo, profil) = await GetLogoAndProfileAsync();
        return await SendPdfRequest("siparis", new ErmayMuhasebe.Models.SiparisRequest(siparis, detaylar, cari, logo, profil?.LogoSiparis ?? ShowLogoSiparis, SiparisSize, SiparisOrientation));
    }

    public async Task<(bool Success, string Error)> GenerateGenericTablePdfAsync(string title, string[] headers, List<string[]> rows, string? subtitle = null)
    {
        var (logo, profil) = await GetLogoAndProfileAsync();
        return await SendPdfRequest("generic", new ErmayMuhasebe.Models.GenericTableRequest(title, headers, rows, subtitle, logo, profil?.LogoRaporlar ?? ShowLogoRaporlar));
    }

    public async Task<(bool Success, string Error)> GenerateConsolidatedReportPdfAsync(string title, List<ReportSection> sections)
    {
        var (logo, profil) = await GetLogoAndProfileAsync();
        return await SendPdfRequest("consolidated", new ErmayMuhasebe.Models.ConsolidatedReportRequest(title, sections, logo, profil?.LogoRaporlar ?? ShowLogoRaporlar));
    }

    public async Task<(bool Success, string Error)> GenerateBudgetReportPdfAsync(string title, List<ChartDataItem> annual, List<ChartDataItem> monthly, List<ChartDataItem> weekly)
    {
        var (logo, profil) = await GetLogoAndProfileAsync();
        return await SendPdfRequest("budget", new ErmayMuhasebe.Models.BudgetReportRequest(title, annual, monthly, weekly, logo, profil?.LogoRaporlar ?? ShowLogoRaporlar));
    }

    public async Task<(bool Success, string Error)> GenerateMakbuzPdfAsync(string? makbuzTipi, string? cariUnvan, DateTime tarih, decimal tutar, string? aciklama)
    {
        var (logo, profil) = await GetLogoAndProfileAsync();
        return await SendPdfRequest("makbuz", new ErmayMuhasebe.Models.MakbuzRequest(makbuzTipi, cariUnvan, tarih, tutar, aciklama, logo, profil?.LogoTahsilat ?? ShowLogoTahsilat));
    }

    public async Task<(bool Success, string Error)> GenerateCariEkstrePdfAsync(CariKart cari, List<CariHareket> hareketler)
    {
        var (logo, profil) = await GetLogoAndProfileAsync();
        return await SendPdfRequest("ekstre", new ErmayMuhasebe.Models.EkstreRequest(cari, hareketler, logo, profil?.LogoEkstre ?? ShowLogoEkstre));
    }

    public async Task<(bool Success, string Error)> GenerateDetayliCariEkstrePdfAsync(CariKart cari, List<CariHareket> hareketler, Dictionary<int, List<FaturaDetay>> detaylar)
    {
        var (logo, profil) = await GetLogoAndProfileAsync();
        return await SendPdfRequest("ekstre-detayli", new ErmayMuhasebe.Models.DetayliEkstreRequest(cari, hareketler, detaylar, logo, profil?.LogoEkstre ?? ShowLogoEkstre));
    }

    public async Task<(bool Success, string Error)> GenerateEftPdfAsync(EftIslem islem)
    {
        var (logo, profil) = await GetLogoAndProfileAsync();
        return await SendPdfRequest("eft", new ErmayMuhasebe.Models.EftRequest(islem, logo, profil?.LogoTahsilat ?? ShowLogoTahsilat));
    }

    public async Task<(bool Success, string Error)> GenerateKkPdfAsync(KrediKartiIslem request)
    {
        var (logo, profil) = await GetLogoAndProfileAsync();
        return await SendPdfRequest("kk", new ErmayMuhasebe.Models.KkRequest(request, logo, profil?.LogoTahsilat ?? ShowLogoTahsilat));
    }

    public async Task<byte[]> GenerateFaturaPdfBytesAsync(Fatura fatura, List<FaturaDetay> detaylar)
    {
        var (logo, profil) = await GetLogoAndProfileAsync();
        return await FetchPdfBytes("fatura", new ErmayMuhasebe.Models.FaturaRequest(fatura, detaylar, logo, profil?.LogoFatura ?? ShowLogoFatura, FaturaSize, FaturaOrientation, AktifFaturaTasarimi));
    }

    public async Task<byte[]> GenerateTeklifPdfBytesAsync(Teklif teklif, List<TeklifDetay> detaylar, CariKart? cari)
    {
        var (logo, profil) = await GetLogoAndProfileAsync();
        return await FetchPdfBytes("teklif", new ErmayMuhasebe.Models.TeklifRequest(teklif, detaylar, cari, logo, profil?.LogoTeklif ?? ShowLogoTeklif, TeklifSize, TeklifOrientation));
    }

    public async Task<byte[]> GenerateSiparisPdfBytesAsync(Siparis siparis, List<SiparisDetay> detaylar, CariKart? cari)
    {
        var (logo, profil) = await GetLogoAndProfileAsync();
        return await FetchPdfBytes("siparis", new ErmayMuhasebe.Models.SiparisRequest(siparis, detaylar, cari, logo, profil?.LogoSiparis ?? ShowLogoSiparis, SiparisSize, SiparisOrientation));
    }

    public async Task<byte[]> GenerateGenericTablePdfBytesAsync(string title, string[] headers, List<string[]> rows, string? subtitle = null)
    {
        var (logo, profil) = await GetLogoAndProfileAsync();
        return await FetchPdfBytes("generic", new ErmayMuhasebe.Models.GenericTableRequest(title, headers, rows, subtitle, logo, profil?.LogoRaporlar ?? ShowLogoRaporlar));
    }

    public async Task<byte[]> GenerateCariEkstrePdfBytesAsync(CariKart cari, List<CariHareket> hareketler)
    {
        var (logo, profil) = await GetLogoAndProfileAsync();
        return await FetchPdfBytes("ekstre", new ErmayMuhasebe.Models.EkstreRequest(cari, hareketler, logo, profil?.LogoEkstre ?? ShowLogoEkstre));
    }

    public async Task<byte[]> GenerateDetayliCariEkstrePdfBytesAsync(CariKart cari, List<CariHareket> hareketler, Dictionary<int, List<FaturaDetay>> detaylar)
    {
        var (logo, profil) = await GetLogoAndProfileAsync();
        return await FetchPdfBytes("ekstre-detayli", new ErmayMuhasebe.Models.DetayliEkstreRequest(cari, hareketler, detaylar, logo, profil?.LogoEkstre ?? ShowLogoEkstre));
    }

    public async Task<byte[]> GenerateFaturaBatchPdfBytesAsync(List<(Fatura Fatura, List<FaturaDetay> Detaylar)> items)
    {
        var (logo, profil) = await GetLogoAndProfileAsync();
        var requests = items.Select(x => new ErmayMuhasebe.Models.FaturaRequest(x.Fatura, x.Detaylar, logo, profil?.LogoFatura ?? ShowLogoFatura)).ToList();
        return await FetchPdfBytes("fatura-batch", requests);
    }

    public async Task<byte[]> GenerateMakbuzPdfBytesAsync(string? makbuzTipi, string? cariUnvan, DateTime tarih, decimal tutar, string? aciklama)
    {
        var (logo, profil) = await GetLogoAndProfileAsync();
        return await FetchPdfBytes("makbuz", new ErmayMuhasebe.Models.MakbuzRequest(makbuzTipi, cariUnvan, tarih, tutar, aciklama, logo, profil?.LogoTahsilat ?? ShowLogoTahsilat));
    }

    public async Task<byte[]> GenerateEftSlipPdfBytesAsync(EftIslem islem)
    {
        var (logo, profil) = await GetLogoAndProfileAsync();
        return await FetchPdfBytes("eft", new ErmayMuhasebe.Models.EftRequest(islem, logo, profil?.LogoTahsilat ?? ShowLogoTahsilat));
    }

    public async Task<byte[]> GenerateKrediKartiSlipPdfBytesAsync(KrediKartiIslem islem)
    {
        var (logo, profil) = await GetLogoAndProfileAsync();
        return await FetchPdfBytes("kk", new ErmayMuhasebe.Models.KkRequest(islem, logo, profil?.LogoTahsilat ?? ShowLogoTahsilat));
    }

    public async Task<byte[]> GenerateBudgetReportPdfBytesAsync(string title, List<ChartDataItem> annual, List<ChartDataItem> monthly, List<ChartDataItem> weekly)
    {
        var (logo, profil) = await GetLogoAndProfileAsync();
        return await FetchPdfBytes("budget", new ErmayMuhasebe.Models.BudgetReportRequest(title, annual, monthly, weekly, logo, profil?.LogoRaporlar ?? ShowLogoRaporlar));
    }

    public async Task<byte[]> GenerateConsolidatedReportPdfBytesAsync(string title, List<ReportSection> sections)
    {
        var (logo, profil) = await GetLogoAndProfileAsync();
        return await FetchPdfBytes("consolidated", new ErmayMuhasebe.Models.ConsolidatedReportRequest(title, sections, logo, profil?.LogoRaporlar ?? ShowLogoRaporlar));
    }

    public async Task<byte[]> GenerateKasaEkstrePdfBytesAsync(BankaKart kasa, List<KasaHareket> hareketler)
    {
        var (logo, profil) = await GetLogoAndProfileAsync();
        return await FetchPdfBytes("kasa-ekstre", new ErmayMuhasebe.Models.KasaEkstreRequest(kasa, hareketler, logo, profil?.LogoRaporlar ?? ShowLogoRaporlar));
    }

    public async Task<byte[]> GenerateMakbuzFromKasaPdfBytesAsync(KasaHareket h)
    {
        var (logo, profil) = await GetLogoAndProfileAsync();
        return await FetchPdfBytes("makbuz-from-kasa", new ErmayMuhasebe.Models.KasaMakbuzRequest(h, logo, profil?.LogoTahsilat ?? ShowLogoTahsilat));
    }

    public async Task<byte[]> GenerateCekPdfBytesAsync(Cek cek)
    {
        var (logo, profil) = await GetLogoAndProfileAsync();
        return await FetchPdfBytes("cek", new ErmayMuhasebe.Models.CekRequest(cek, logo, profil?.LogoRaporlar ?? ShowLogoRaporlar));
    }

    public async Task<byte[]> GenerateCekListPdfBytesAsync(List<Cek> cekler)
    {
        var (logo, profil) = await GetLogoAndProfileAsync();
        return await FetchPdfBytes("cek-list", new ErmayMuhasebe.Models.CekListRequest(cekler, logo, profil?.LogoRaporlar ?? ShowLogoRaporlar));
    }

    public async Task<byte[]> GenerateKrediKartiListPdfBytesAsync(List<KrediKartiIslem> islemler)
    {
        var (logo, profil) = await GetLogoAndProfileAsync();
        return await FetchPdfBytes("kk-list", new ErmayMuhasebe.Models.KrediKartiListRequest(islemler, logo, profil?.LogoRaporlar ?? ShowLogoRaporlar));
    }

    public async Task<byte[]> GenerateEftListPdfBytesAsync(List<EftIslem> islemler)
    {
        var (logo, profil) = await GetLogoAndProfileAsync();
        return await FetchPdfBytes("eft-list", new ErmayMuhasebe.Models.EftListRequest(islemler, logo, profil?.LogoRaporlar ?? ShowLogoRaporlar));
    }

    public async Task<byte[]> GenerateKasaListesiPdfBytesAsync(List<BankaKart> kasalar)
    {
        var (logo, profil) = await GetLogoAndProfileAsync();
        return await FetchPdfBytes("kasa-list", new ErmayMuhasebe.Models.KasaListRequest(kasalar, logo, profil?.LogoRaporlar ?? ShowLogoRaporlar));
    }

    public async Task<byte[]> GenerateAdresEtiketiPdfBytesAsync(string unvan, string adSoyad, string adres, string ilIlce, string postaKodu, string telefon)
    {
        var (logo, profil) = await GetLogoAndProfileAsync();
        return await FetchPdfBytes("adres-etiketi", new ErmayMuhasebe.Models.AdresEtiketiRequest(unvan, adSoyad, adres, ilIlce, postaKodu, telefon, logo, profil?.LogoRaporlar ?? ShowLogoRaporlar));
    }

    public async Task<byte[]> GenerateFaturaListPdfBytesAsync(List<Fatura> faturalar, DateTime startDate, DateTime endDate)
    {
        var (logo, profil) = await GetLogoAndProfileAsync();
        return await FetchPdfBytes("fatura-list", new ErmayMuhasebe.Models.FaturaListRequest(faturalar, startDate, endDate, logo, profil?.LogoRaporlar ?? ShowLogoRaporlar));
    }

    public async Task<byte[]> GenerateFinansRaporPdfBytesAsync(string title, FinancialReportData data)
    {
        var (logo, profil) = await GetLogoAndProfileAsync();
        return await FetchPdfBytes("finans-rapor", new ErmayMuhasebe.Models.FinansRaporRequest(title, data, logo, profil?.LogoRaporlar ?? ShowLogoRaporlar));
    }

    public async Task<byte[]> GenerateStokListPdfBytesAsync(List<StokKart> stoklar)
    {
        var (logo, profil) = await GetLogoAndProfileAsync();
        return await FetchPdfBytes("stok-list", new ErmayMuhasebe.Models.StokListReportRequest(stoklar, logo, profil?.LogoRaporlar ?? ShowLogoRaporlar));
    }

    public async Task<byte[]> GenerateStokHareketleriPdfBytesAsync(StokKart stok, List<StokHareket> hareketler)
    {
        var (logo, profil) = await GetLogoAndProfileAsync();
        return await FetchPdfBytes("stok-hareket", new ErmayMuhasebe.Models.StokHareketReportRequest(stok, hareketler, logo, profil?.LogoRaporlar ?? ShowLogoRaporlar));
    }

    public async Task<byte[]> GenerateCokluStokHareketleriPdfBytesAsync(List<(StokKart Stok, List<StokHareket> Hareketler)> data)
    {
        var (logo, profil) = await GetLogoAndProfileAsync();
        var items = data.Select(d => new { Stok = d.Stok, Hareketler = d.Hareketler }).ToList();
        return await FetchPdfBytes("coklu-stok-hareket", new { Data = items, Logo = logo, ShowLogo = profil?.LogoRaporlar ?? ShowLogoRaporlar });
    }

    public async Task<byte[]> GenerateVadeRaporuPdfBytesAsync(List<VadeReportItem> items)
    {
        var (logo, profil) = await GetLogoAndProfileAsync();
        return await FetchPdfBytes("vade-rapor", new ErmayMuhasebe.Models.VadeRaporuRequest(items, logo, profil?.LogoRaporlar ?? ShowLogoRaporlar));
    }

    public async Task<(bool Success, string Error)> GenerateMusteriTakipRaporuPdfAsync(MusteriTakipKlasor klasor, List<MusteriTakipDetay> detaylar)
        => await SendPdfRequest("musteri-takip", new { Klasor = klasor, Detaylar = detaylar });

    public async Task<byte[]> GenerateMusteriTakipRaporuPdfBytesAsync(MusteriTakipKlasor klasor, List<MusteriTakipDetay> detaylar)
        => await FetchPdfBytes("musteri-takip", new { Klasor = klasor, Detaylar = detaylar });

    public byte[]? LogoBytes { get; set; }
    public void ResetLogoCache() { LogoBytes = null; }
    public bool ShowLogoFatura { get; set; } = true;
    public bool ShowLogoSiparis { get; set; } = true;
    public bool ShowLogoTeklif { get; set; } = true;
    public bool ShowLogoEkstre { get; set; } = true;
    public bool ShowLogoRaporlar { get; set; } = true;
    public bool ShowLogoTahsilat { get; set; } = true;
    public bool ShowLogoOdeme { get; set; } = true;
    public bool ShowLogoAcilisBakiye { get; set; } = true;

    public string FaturaSize { get; set; } = "A4";
    public string FaturaOrientation { get; set; } = "Portrait";
    public ErmayMuhasebe.Models.FaturaTasarimi? AktifFaturaTasarimi { get; set; }
    public string TeklifSize { get; set; } = "A4";
    public string TeklifOrientation { get; set; } = "Portrait";
    public string SiparisSize { get; set; } = "A5";
    public string SiparisOrientation { get; set; } = "Portrait";
}
