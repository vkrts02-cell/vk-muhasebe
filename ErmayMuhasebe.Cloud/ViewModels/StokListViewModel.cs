using ErmayMuhasebe.Models;
using ErmayMuhasebe.Services;
using ErmayMuhasebe.Repositories;
using ErmayMuhasebe.Cloud.Services;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.JSInterop;

namespace ErmayMuhasebe.Cloud.ViewModels;

public partial class StokListViewModel : ErmayMuhasebe.Shared.ViewModels.StokListViewModel
{
    private readonly IJSRuntime _jsRuntime;

    public StokListViewModel(ErmayMuhasebe.Repositories.IUnitOfWork uow, IExcelService excelService, IPdfService pdfService, ErmayMuhasebe.Cloud.Services.IFileService fileService, IJSRuntime jsRuntime) 
        : base(uow, excelService, pdfService, (ErmayMuhasebe.Services.IFileService)fileService)
    {
        _jsRuntime = jsRuntime;
    }

    private ErmayMuhasebe.Cloud.Services.IFileService CloudFileService => (ErmayMuhasebe.Cloud.Services.IFileService)_fileService;

    protected override async Task HandleFileOpenAsync(byte[] content, string fileName)
    {
        if (fileName.EndsWith(".pdf")) await CloudFileService.OpenPdfAsync(content);
        else 
        {
            string contentType = fileName.EndsWith(".xlsx") ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" : "application/octet-stream";
            await CloudFileService.DownloadFileAsync(fileName, contentType, content);
        }
    }

    public override async Task PrintStokList()
    {
        try
        {
            var headers = new string[] { "Kod", "Stok Adı", "Grup", "Birim", "Alış Fiyati", "Satış Fiyatı", "Miktar" };
            var rows = Stoklar.Select(s => new string[] {
                s.StokKodu ?? "-",
                s.StokAdi ?? "-",
                s.Kategori ?? "-",
                s.Birim ?? "Adet",
                s.AlisFiyati.ToString("N2"),
                s.SatisFiyati.ToString("N2"),
                s.Miktar.ToString("N2")
            }).ToList();

            await _jsRuntime.InvokeVoidAsync("generateGenericTablePdf", "STOK LİSTESİ", headers, rows, true);
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    public override async Task GenerateStokHareketleriReportAsync()
    {
        var selectedList = Stoklar.Where(s => s.IsSelected).ToList();
        if (selectedList.Count == 0 && SelectedStok != null)
        {
            selectedList.Add(SelectedStok);
        }

        if (selectedList.Count == 0)
        {
            ErrorMessage = "Lütfen önce hareket raporunu almak istediğiniz stok kartını seçiniz.";
            return;
        }

        try
        {
            var headers = new string[] { "Tarih", "İşlem Türü", "Açıklama", "Giren", "Çıkan", "Kalan" };
            foreach (var stok in selectedList)
            {
                var list = await _uow.Stoklar.GetHareketlerAsync(stok.Id);
                decimal running = 0;
                var rows = list.OrderBy(h => h.Tarih).Select(h => {
                    if (h.IslemTuru == "GİRİŞ") running += h.Miktar;
                    else if (h.IslemTuru == "ÇIKIŞ") running -= h.Miktar;
                    return new string[] {
                        h.Tarih.ToString("dd.MM.yyyy HH:mm"),
                        h.IslemTuru ?? "-",
                        h.Aciklama ?? "-",
                        h.IslemTuru == "GİRİŞ" ? h.Miktar.ToString("N2") : "-",
                        h.IslemTuru == "ÇIKIŞ" ? h.Miktar.ToString("N2") : "-",
                        running.ToString("N2")
                    };
                }).ToList();

                await _jsRuntime.InvokeVoidAsync("generateGenericTablePdf", $"STOK HAREKETLERİ: {stok.StokAdi}", headers, rows, true);
            }
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    public override async Task DownloadTemplateAsync()
    {
        try
        {
            var template = new List<StokExcelDto>
            {
                new StokExcelDto
                {
                    StokKodu = "KOD-001",
                    StokAdi = "Örnek Ürün",
                    Birim = "Adet",
                    Kategori = "Grup",
                    AlisFiyati = 0,
                    SatisFiyati = 100,
                    KDV = 20,
                    Miktar = 0,
                    MinSeviye = 0,
                    Aciklama = ""
                }
            };
            var bytes = await _excelService.ExportListToMemoryAsync(template, "Stok_Sablon");
            await HandleFileOpenAsync(bytes, "Stok_Yukleme_Sablonu.xlsx");
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    public async Task ImportExcelFromStreamAsync(System.IO.Stream stream)
    {
        try 
        {
            // First, copy everything to a memory buffer because IBrowserStream is not seekable
            using var ms = new System.IO.MemoryStream();
            await stream.CopyToAsync(ms);
            byte[] data = ms.ToArray();
            
            using var seekableStream = new System.IO.MemoryStream(data);
            var importedList = await _excelService.ReadExcelAsync<StokKart>(seekableStream);
            
            var existingItems = await _uow.Stoklar.GetAllAsync();
            
            foreach (var item in importedList)
            {
                if (string.IsNullOrEmpty(item.StokKodu)) continue;

                var existing = existingItems.FirstOrDefault(x => 
                    string.Equals(x.StokKodu?.Trim(), item.StokKodu?.Trim(), StringComparison.OrdinalIgnoreCase));

                if (existing != null)
                {
                    // Update existing item
                    existing.StokAdi = !string.IsNullOrEmpty(item.StokAdi) ? item.StokAdi : existing.StokAdi;
                    existing.Kategori = !string.IsNullOrEmpty(item.Kategori) ? item.Kategori : existing.Kategori;
                    existing.Birim = !string.IsNullOrEmpty(item.Birim) ? item.Birim : existing.Birim;
                    existing.AlisFiyati = item.AlisFiyati != 0 ? item.AlisFiyati : existing.AlisFiyati;
                    existing.SatisFiyati = item.SatisFiyati != 0 ? item.SatisFiyati : existing.SatisFiyati;
                    existing.KDV = item.KDV != 0 ? item.KDV : existing.KDV;
                    existing.Miktar = item.Miktar; // Always sync quantity

                    await _uow.Stoklar.SaveAsync(existing);
                }
                else
                {
                    // Create new item
                    await _uow.Stoklar.SaveAsync(item);
                }
            }
            await LoadStoklarAsync();
        }
        catch (Exception ex) 
        { 
            ErrorMessage = $"Excel okuma hatası: {ex.Message}";
            throw new Exception($"Excel okuma hatası: {ex.Message}"); 
        }
    }
}
