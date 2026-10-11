using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ErmayMuhasebe.Avalonia.Messages;
using ErmayMuhasebe.Models;
using ErmayMuhasebe.Services;
using ErmayMuhasebe.Repositories;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.IO;
using ErmayMuhasebe.Helpers;

namespace ErmayMuhasebe.Avalonia.ViewModels;

public partial class VadeTakipViewModel : ViewModelBase
{
    private readonly IUnitOfWork _uow;
    private readonly IExcelService _excelService;
    private readonly IPdfService _pdfService;
    private readonly IFileService _fileService;

    private List<VadeItem> _allVadeler = new();
    
    [ObservableProperty] private ObservableCollection<VadeItem> _vadeler = new();
    [ObservableProperty] private decimal _toplamAlacak;
    [ObservableProperty] private decimal _toplamBorc;
    [ObservableProperty] private int _gecikmisAdet;
    
    [ObservableProperty] private string? _searchText;
    [ObservableProperty] private string _selectedFilter = "Tümü";
    public string[] Filters { get; } = { "Tümü", "Bugün", "Bu Hafta", "Bu Ay", "Gecikmiş" };

    public bool DisableAutoRefresh { get; set; } = false;

    public VadeTakipViewModel(IUnitOfWork uow, IExcelService excelService, IPdfService pdfService, IFileService fileService, bool disableAutoRefresh = false)
    {
        DisableAutoRefresh = disableAutoRefresh;
        _uow = uow;
        _excelService = excelService;
        _pdfService = pdfService;
        _fileService = fileService;
        if (!DisableAutoRefresh)
            _ = LoadVadelerAsync();

        WeakReferenceMessenger.Default.Register<FinancialDataChangedMessage>(this, (r, m) => 
        {
            if (!DisableAutoRefresh)
                _ = LoadVadelerAsync(isSilent: true);
        });
    }

    partial void OnSearchTextChanged(string? value) => ApplyFilters();
    partial void OnSelectedFilterChanged(string value) => ApplyFilters();

    [RelayCommand]
    public async Task LoadVadelerAsync(bool isSilent = false)
    {
        if (!isSilent && IsLoading) return;
        try
        {
            if (!isSilent && Vadeler.Count == 0) IsLoading = true;
            var items = new List<VadeItem>();

            // 1. Faturalar (Borç/Alacak Kalanı olanlar)
            var faturalar = await _uow.Faturalar.GetAllAsync();
            foreach (var f in faturalar.Where(x => x.Kalan > 0))
            {
                items.Add(new VadeItem
                {
                    Type = "Fatura",
                    SourceId = f.Id,
                    CariId = f.CariId,
                    Tur = f.Tur == "Satis" || f.Tur == "Satış" ? "Alacak (Fatura)" : "Borç (Fatura)",
                    CariAdi = f.CariUnvan ?? "Bilinmeyen",
                    VadeTarihi = f.VadeTarihi,
                    Tutar = f.Kalan,
                    IsIncoming = f.Tur == "Satis" || f.Tur == "Satış"
                });
            }

            // 2. Çekler (Müşteri Çekleri + Bizim verdiğimiz çekler)
            var cekler = await _uow.Cekler.GetAllAsync();
            foreach (var c in cekler.Where(x => x.Durum == "Portföyde" || x.Durum == "Portfoyde" || x.CekTuru == "Verilen"))
            {
                 items.Add(new VadeItem
                {
                    Type = "Cek",
                    SourceId = c.Id,
                    CariId = c.CariId ?? 0,
                    Tur = c.CekTuru == "Verilen" ? "Borç (Çek)" : "Alacak (Çek)",
                    CariAdi = c.CariUnvan ?? c.AsilBorclu ?? "Bilinmeyen",
                    VadeTarihi = c.VadeTarihi,
                    Tutar = c.Tutar,
                    IsIncoming = c.CekTuru != "Verilen"
                });
            }

            // 3. Senetler
            var senetler = await _uow.Senetler.GetAllAsync();
            foreach (var s in senetler.Where(x => x.Durum == "Portföyde" || x.Durum == "Portfoyde" || x.SenetTuru == "Verilen"))
            {
                items.Add(new VadeItem
                {
                    Type = "Senet",
                    SourceId = s.Id,
                    CariId = s.CariId ?? 0,
                    Tur = s.SenetTuru == "Verilen" ? "Borç (Senet)" : "Alacak (Senet)",
                    CariAdi = s.CariUnvan ?? s.AsilBorclu ?? "Bilinmeyen",
                    VadeTarihi = s.VadeTarihi,
                    Tutar = s.Tutar,
                    IsIncoming = s.SenetTuru != "Verilen"
                });
            }

            _allVadeler = items.OrderBy(x => x.VadeTarihi).ToList();
            ApplyFilters();
        }
        catch (Exception ex)
        {
            ErrorMessage = "Veriler yüklenirken hata oluştu: " + ex.Message;
        }
        finally
        {
            if (!isSilent) IsLoading = false;
        }
    }

    private void ApplyFilters()
    {
        var result = _allVadeler.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var lowerCase = SearchText.ToLower();
            result = result.Where(x => x.CariAdi.ToLower().Contains(lowerCase) || x.Tur.ToLower().Contains(lowerCase));
        }

        var today = DateTime.Today;
        switch (SelectedFilter)
        {
            case "Bugün":
                result = result.Where(x => x.VadeTarihi.Date == today);
                break;
            case "Bu Hafta":
                var diff = ((int)today.DayOfWeek == 0 ? 7 : (int)today.DayOfWeek) - 1;
                var startOfWeek = today.AddDays(-diff);
                var endOfWeek = today.AddDays(8);
                result = result.Where(x => x.VadeTarihi.Date >= startOfWeek && x.VadeTarihi.Date < endOfWeek);
                break;
            case "Bu Ay":
                result = result.Where(x => x.VadeTarihi.Month == today.Month && x.VadeTarihi.Year == today.Year);
                break;
            case "Gecikmiş":
                result = result.Where(x => x.VadeTarihi.Date < today);
                break;
        }

        Vadeler.SyncWith(result, x => $"{x.Type}_{x.SourceId}", (target, src) =>
        {
            if (target.VadeTarihi != src.VadeTarihi) target.VadeTarihi = src.VadeTarihi;
            if (target.Tutar != src.Tutar) target.Tutar = src.Tutar;
            if (target.Tur != src.Tur) target.Tur = src.Tur;
            if (target.CariAdi != src.CariAdi) target.CariAdi = src.CariAdi;
            if (target.IsIncoming != src.IsIncoming) target.IsIncoming = src.IsIncoming;
            if (target.CariId != src.CariId) target.CariId = src.CariId;
        });

        ToplamAlacak = Vadeler.Where(x => x.IsIncoming).Sum(x => x.Tutar);
        ToplamBorc = Vadeler.Where(x => !x.IsIncoming).Sum(x => x.Tutar);
        GecikmisAdet = Vadeler.Count(x => x.KalanGun < 0);
    }

    [RelayCommand]
    private void GoToCari(VadeItem? item)
    {
        if (item == null || item.CariId <= 0) return;
        WeakReferenceMessenger.Default.Send(new ShowCariDetailMessage(item.CariId));
    }

    [RelayCommand]
    private async Task SavePostponeAsync(VadeItem? item)
    {
        if (item == null) return;
        
        if (item.Type == "Fatura")
        {
            var f = await _uow.Faturalar.GetByIdAsync(item.SourceId);
            if (f != null) { f.VadeTarihi = item.VadeTarihi; await _uow.Faturalar.SaveAsync(f); }
        }
        else if (item.Type == "Cek")
        {
            var c = await _uow.Cekler.GetByIdAsync(item.SourceId);
            if (c != null) { c.VadeTarihi = item.VadeTarihi; await _uow.Cekler.SaveAsync(c); }
        }
        else if (item.Type == "Senet")
        {
            var s = await _uow.Senetler.GetByIdAsync(item.SourceId);
            if (s != null) { s.VadeTarihi = item.VadeTarihi; await _uow.Senetler.SaveAsync(s); }
        }
        
        await LoadVadelerAsync();
    }

    [RelayCommand]
    private async Task ExportToExcel()
    {
        var targetList = Vadeler.Where(x => x.IsChecked).ToList();
        if (!targetList.Any()) targetList = Vadeler.ToList();

        var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        var fileName = $"VadeTakip_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
        var fullPath = Path.Combine(desktopPath, fileName);
        
        var data = targetList.Select(x => new { x.Tur, x.CariAdi, VadeTarihi = x.VadeTarihi.ToString("dd.MM.yyyy"), x.Tutar, Durum = x.KalanGunText }).ToList();
        await _excelService.ExportListToExcelAsync(data, fullPath);
    }

    [RelayCommand]
    private async Task Print()
    {
        var targetList = Vadeler.Where(x => x.IsChecked).ToList();
        if (!targetList.Any()) targetList = Vadeler.ToList();

        var fileName = $"VadeRaporu_{DateTime.Now:yyyyMMdd_HHmm}.pdf";
        
        var reportData = targetList.Select(x => new VadeReportItem 
        { 
            Tur = x.Tur, 
            CariAdi = x.CariAdi, 
            VadeTarihi = x.VadeTarihi, 
            Tutar = x.Tutar, 
            Durum = x.KalanGunText 
        }).ToList();

        var pdfBytes = await _pdfService.GenerateVadeRaporuPdfBytesAsync(reportData);
        if (_fileService != null)
        {
            await _fileService.SaveAndOpenFileAsync(fileName, pdfBytes, "application/pdf");
        }
        else
        {
            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var fullPath = Path.Combine(desktopPath, fileName);
            await File.WriteAllBytesAsync(fullPath, pdfBytes);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(fullPath) { UseShellExecute = true });
        }
    }
}

public partial class VadeItem : ObservableObject
{
    [ObservableProperty] private bool _isChecked;
    [ObservableProperty] private string _type = ""; // Fatura, Cek, Senet
    [ObservableProperty] private int _sourceId;
    [ObservableProperty] private int _cariId;
    [ObservableProperty] private string _tur = "";
    [ObservableProperty] private string _cariAdi = "";
    
    [ObservableProperty] 
    [NotifyPropertyChangedFor(nameof(KalanGun))]
    [NotifyPropertyChangedFor(nameof(KalanGunText))]
    [NotifyPropertyChangedFor(nameof(RenkliUyari))]
    private DateTime _vadeTarihi;

    [ObservableProperty] private decimal _tutar;
    [ObservableProperty] private bool _isIncoming;
    public bool RenkliUyari => KalanGun < 0; 
    
    public int KalanGun => (VadeTarihi.Date - DateTime.Today).Days;
    public string KalanGunText => KalanGun < 0 ? $"{Math.Abs(KalanGun)} GÜN GECİKTİ" : (KalanGun == 0 ? "BUGÜN" : $"{KalanGun} Gün Kaldı");
}
