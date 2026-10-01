using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ErmayMuhasebe.Models;
using ErmayMuhasebe.Services;
using ErmayMuhasebe.Repositories;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ErmayMuhasebe.Shared.ViewModels;

public abstract partial class FaturaListViewModel : ViewModelBase
{
    protected readonly IUnitOfWork _uow;
    protected readonly IExcelService _excelService;
    protected readonly IPdfService _pdfService;

    [ObservableProperty]
    private ObservableCollection<Fatura> _faturalar = new();

    [ObservableProperty]
    private Fatura? _selectedFatura;

    [ObservableProperty]
    private string _searchString = "";

    [ObservableProperty] private string _startDateStr = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).ToString("dd.MM.yyyy");
    [ObservableProperty] private string _endDateStr = DateTime.Now.ToString("dd.MM.yyyy");

    // DateTime properties for CalendarDatePicker binding
    [ObservableProperty] private DateTime? _startDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
    [ObservableProperty] private DateTime? _endDate = DateTime.Now;

    private bool _isSyncingDates;

    partial void OnStartDateChanged(DateTime? value)
    {
        if (_isSyncingDates) return;
        _isSyncingDates = true;
        if (value.HasValue) StartDateStr = value.Value.ToString("dd.MM.yyyy");
        _isSyncingDates = false;
    }

    partial void OnEndDateChanged(DateTime? value)
    {
        if (_isSyncingDates) return;
        _isSyncingDates = true;
        if (value.HasValue) EndDateStr = value.Value.ToString("dd.MM.yyyy");
        _isSyncingDates = false;
    }

    partial void OnStartDateStrChanged(string value)
    {
        if (_isSyncingDates) return;
        _isSyncingDates = true;
        if (DateTime.TryParseExact(value, "dd.MM.yyyy", null, System.Globalization.DateTimeStyles.None, out var dt))
            StartDate = dt;
        _isSyncingDates = false;
        _ = LoadFaturalarAsync();
    }

    partial void OnEndDateStrChanged(string value)
    {
        if (_isSyncingDates) return;
        _isSyncingDates = true;
        if (DateTime.TryParseExact(value, "dd.MM.yyyy", null, System.Globalization.DateTimeStyles.None, out var dt))
            EndDate = dt;
        _isSyncingDates = false;
        _ = LoadFaturalarAsync();
    }
    partial void OnSearchStringChanged(string value) => _ = LoadFaturalarAsync();

    // Create Invoice Props
    [ObservableProperty] private bool _isCreateVisible;
    [ObservableProperty] private Fatura _newFatura = new();
    [ObservableProperty] private ObservableCollection<FaturaDetay> _newFaturaDetaylar = new();
    
    // Line Item Entry
    [ObservableProperty] private string _lineStokKodu = "";
    [ObservableProperty] private string _lineStokAdi = "";
    [ObservableProperty] private decimal _lineMiktar = 1;
    [ObservableProperty] private decimal _lineFiyat;
    [ObservableProperty] private StokKart? _selectedStockForLine;
    [ObservableProperty] private CariKart? _selectedCariForInvoice;
    [ObservableProperty] private string _cariSearchTerm = "";
    [ObservableProperty] private ObservableCollection<CariKart> _cariSearchResults = new();

    public FaturaListViewModel(IUnitOfWork uow, IExcelService excelService, IPdfService pdfService)
    {
        _uow = uow;
        _excelService = excelService;
        _pdfService = pdfService;
        
        _ = LoadFaturalarAsync();
    }

    public override void OnNavigatedTo()
    {
        _ = LoadFaturalarAsync();
    }

    [RelayCommand]
    public async Task LoadFaturalarAsync()
    {
        var list = await _uow.Faturalar.GetAllAsync();
        
        DateTime sDate = ParseDate(StartDateStr);
        DateTime eDate = ParseDate(EndDateStr, true);

        var filtered = list
            .Where(f => f.Tur == "Satış" || f.Tur == "Alış" || f.Tur == "Satis" || f.Tur == "Alis")
            .Where(f => f.Tarih.Date >= sDate.Date && f.Tarih.Date <= eDate.Date)
            .Where(f => string.IsNullOrEmpty(SearchString) || 
                        (f.FaturaNo != null && f.FaturaNo.Contains(SearchString, StringComparison.OrdinalIgnoreCase)) ||
                        (f.CariUnvan != null && f.CariUnvan.Contains(SearchString, StringComparison.OrdinalIgnoreCase)) ||
                        (f.Tur != null && f.Tur.Contains(SearchString, StringComparison.OrdinalIgnoreCase)) ||
                        (f.Tarih.ToString("dd.MM.yyyy").Contains(SearchString)))
            .OrderByDescending(f => f.Tarih)
            .ToList();

        await InvokeOnUIThreadAsync(() => 
        {
            Faturalar = new ObservableCollection<Fatura>(filtered);
        });
    }

    [RelayCommand]
    public void DeleteFaturaConfirm(Fatura? fatura)
    {
        if (fatura == null) return;
        ShowConfirm("Faturayı Sil", $"{fatura.FaturaNo} numaralı faturayı silmek istediğinize emin misiniz? Bu işlem stok ve cari bakiyelerini de etkileyecektir.", () => DeleteFaturaAsync(fatura));
    }

    public async Task DeleteFaturaAsync(Fatura? fatura)
    {
        if (fatura == null) return;
        
        await _uow.Faturalar.DeleteAsync(fatura.Id);
        NotifyFinancialDataChanged();
        await LoadFaturalarAsync();
    }

    protected abstract void NotifyFinancialDataChanged();

    [RelayCommand]
    public virtual async Task EditFaturaAsync(Fatura? fatura)
    {
        if (fatura == null) return;
        await Task.CompletedTask;
        // Platform specific implementation
    }

    [RelayCommand]
    public virtual async Task ViewFaturaPdfAsync(Fatura? fatura)
    {
        if (fatura == null) return;
        try 
        {
            var detaylar = await _uow.Faturalar.GetDetaylarAsync(fatura.Id);
            var pdfBytes = await _pdfService.GenerateFaturaPdfBytesAsync(fatura, detaylar);
            await HandleFileOpenAsync(pdfBytes, $"Fatura_{fatura.FaturaNo}.pdf");
        }
        catch (Exception ex) 
        {
            System.Diagnostics.Debug.WriteLine($"PDF Error: {ex}");
            ErrorMessage = "PDF oluşturulurken hata!";
        }
    }

    [RelayCommand]
    public async Task ShareFaturaWhatsAppAsync(Fatura? fatura)
    {
        if (fatura == null) return;
        try
        {
            var cari = await _uow.Cariler.GetByIdAsync(fatura.CariId);
            if (cari == null) return;

            var detaylar = await _uow.Faturalar.GetDetaylarAsync(fatura.Id);
            var pdfBytes = await _pdfService.GenerateFaturaPdfBytesAsync(fatura, detaylar);
            
            string telefon = cari.Telefon ?? cari.CepTelefon ?? "";
            string mesaj = $"Sayın {cari.Unvan}, {fatura.FaturaNo} numaralı faturanız ekte sunulmuştur.";
            string dosyaAdi = $"Fatura_{fatura.FaturaNo}.pdf";

            var shareService = ResolveShareService();
            if (shareService != null)
            {
                await shareService.ShareViaWhatsAppAsync(telefon, mesaj, pdfBytes, dosyaAdi);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"WhatsApp Paylaşım Hatası: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task ShareFaturaEmailAsync(Fatura? fatura)
    {
        if (fatura == null) return;
        try
        {
            var cari = await _uow.Cariler.GetByIdAsync(fatura.CariId);
            if (cari == null) return;

            string aliciEposta = cari.Email ?? "";
            if (string.IsNullOrWhiteSpace(aliciEposta))
            {
                ErrorMessage = "Cariye tanımlı e-posta adresi bulunamadı.";
                return;
            }

            var detaylar = await _uow.Faturalar.GetDetaylarAsync(fatura.Id);
            var pdfBytes = await _pdfService.GenerateFaturaPdfBytesAsync(fatura, detaylar);
            
            string konu = $"Fatura - {fatura.FaturaNo}";
            string mesaj = $"Sayın {cari.Unvan},\n\n{fatura.FaturaNo} numaralı faturanız ekte yer almaktadır.\n\nİyi çalışmalar.";
            string dosyaAdi = $"Fatura_{fatura.FaturaNo}.pdf";

            var shareService = ResolveShareService();
            if (shareService != null)
            {
                await shareService.SendPdfViaEmailAsync(aliciEposta, konu, mesaj, pdfBytes, dosyaAdi);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"E-posta Gönderim Hatası: {ex.Message}";
        }
    }

    private dynamic? ResolveShareService()
    {
        try
        {
            var appType = Type.GetType("Avalonia.Application, Avalonia.Base");
            if (appType == null) appType = Type.GetType("Avalonia.Application, Avalonia.Controls");
            if (appType == null) return null;

            var currentProp = appType.GetProperty("Current");
            var currentApp = currentProp?.GetValue(null);
            if (currentApp == null) return null;

            var servicesProp = currentApp.GetType().GetProperty("Services");
            var services = servicesProp?.GetValue(currentApp);
            if (services == null) return null;

            var getServiceMethod = services.GetType().GetMethod("GetService", new Type[] { typeof(Type) });
            if (getServiceMethod == null) return null;

            var shareServiceType = Type.GetType("ErmayMuhasebe.Avalonia.Services.ShareService, ErmayMuhasebe.Avalonia");
            if (shareServiceType != null)
            {
                return getServiceMethod.Invoke(services, new object[] { shareServiceType });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ResolveShareService Error: {ex.Message}");
        }
        return null;
    }

    [RelayCommand]
    public async Task PrintFaturaAsync(Fatura? fatura) => await ViewFaturaPdfAsync(fatura);

    [RelayCommand]
    public void Search()
    {
        _ = LoadFaturalarAsync();
    }

    [RelayCommand]
    public async Task ExportToExcelAsync()
    {
        if (!Faturalar.Any()) return;
        
        try 
        {
            var excelBytes = await _excelService.ExportListToMemoryAsync(Faturalar.ToList(), "Faturalar");
            await HandleFileOpenAsync(excelBytes, $"Faturalar_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
        }
        catch (Exception ex) 
        { 
            System.Diagnostics.Debug.WriteLine(ex.Message); 
            ErrorMessage = "Excel oluşturulurken hata!";
        }
    }

    [RelayCommand]
    public virtual async Task ExportAllToPdfAsync()
    {
        if (!Faturalar.Any()) return;
        try 
        {
            var pdfBytes = await _pdfService.GenerateFaturaListPdfBytesAsync(Faturalar.ToList(), ParseDate(StartDateStr), ParseDate(EndDateStr, true));
            await HandleFileOpenAsync(pdfBytes, $"FaturaListesi_{DateTime.Now:yyyyMMdd_HHmm}.pdf");
        }
        catch (Exception ex) 
        {
            System.Diagnostics.Debug.WriteLine($"PDF Error: {ex}");
            ErrorMessage = "Liste PDF oluşturulurken hata!";
        }
    }

    protected abstract Task HandleFileOpenAsync(byte[] content, string fileName);

    [RelayCommand]
    public virtual void OpenCreateInvoice(string type)
    {
        // Platform specific implementation
    }

    [RelayCommand]
    public void CloseCreateInvoice()
    {
        IsCreateVisible = false;
    }

    [RelayCommand]
    public async Task SearchCariAsync()
    {
        if (string.IsNullOrEmpty(CariSearchTerm)) return;
        var all = await _uow.Cariler.GetAllAsync();
        CariSearchResults = new ObservableCollection<CariKart>(
            all.Where(c => c.Unvan != null && c.Unvan.Contains(CariSearchTerm, StringComparison.OrdinalIgnoreCase)));
    }

    [RelayCommand]
    public void SelectCari(CariKart cari)
    {
        SelectedCariForInvoice = cari;
        NewFatura.CariId = cari.Id;
        NewFatura.CariUnvan = cari.Unvan;
        NewFatura.VergiDairesi = cari.VergiDairesi;
        NewFatura.VergiNo = cari.VergiNo;
        CariSearchResults.Clear();
    }

    [RelayCommand]
    public async Task FindStockByCodeAsync()
    {
        if (string.IsNullOrEmpty(LineStokKodu)) return;
        var stok = await _uow.Stoklar.GetByKodAsync(LineStokKodu);
        if (stok != null)
        {
            LineStokKodu = stok.StokKodu ?? "";
            LineStokAdi = stok.StokAdi ?? "";
            LineFiyat = NewFatura.Tur == "Satis" ? stok.SatisFiyati : stok.AlisFiyati;
            SelectedStockForLine = stok;
        }
    }

    [RelayCommand]
    public void AddLine()
    {
        if (string.IsNullOrEmpty(LineStokKodu) || LineMiktar <= 0) return;

        var line = new FaturaDetay
        {
            StokId = SelectedStockForLine?.Id ?? 0,
            StokKodu = LineStokKodu,
            StokAdi = LineStokAdi,
            Miktar = LineMiktar,
            BirimFiyat = LineFiyat,
            ToplamTutar = LineMiktar * LineFiyat,
            KDVOrani = SelectedStockForLine?.KDV ?? 20
        };
        line.KDVTutari = line.ToplamTutar * line.KDVOrani / 100m;

        NewFaturaDetaylar.Add(line);
        UpdateTotals();
        
        LineStokKodu = "";
        LineStokAdi = "";
        LineMiktar = 1;
        LineFiyat = 0;
        SelectedStockForLine = null;
    }

    [RelayCommand]
    public void RemoveLine(FaturaDetay line)
    {
        NewFaturaDetaylar.Remove(line);
        UpdateTotals();
    }

    protected void UpdateTotals()
    {
        decimal araToplam = NewFaturaDetaylar.Sum(d => d.ToplamTutar);
        decimal kdv = NewFaturaDetaylar.Sum(d => d.KDVTutari);
        NewFatura.AraToplam = araToplam;
        NewFatura.ToplamKDV = kdv;
        NewFatura.GenelToplam = araToplam + kdv;
        OnPropertyChanged(nameof(NewFatura));
    }

    [RelayCommand]
    public async Task SaveInvoiceAsync()
    {
        if (SelectedCariForInvoice == null || !NewFaturaDetaylar.Any()) return;

        NewFatura.FaturaNo = NewFatura.FaturaNo ?? ("FAT-" + DateTime.Now.Ticks.ToString().Substring(10));
        NewFatura.KayitTarihi = DateTime.Now;
        
        await _uow.Faturalar.SaveWithDetailsAndTransactionAsync(NewFatura, NewFaturaDetaylar.ToList(), NewFatura.Tur == "Satis");

        IsCreateVisible = false;
        await LoadFaturalarAsync();
    }

    protected DateTime ParseDate(string str, bool isEnd = false)
    {
        if (DateTime.TryParseExact(str, "dd.MM.yyyy", null, System.Globalization.DateTimeStyles.None, out var dt))
            return dt;
        return isEnd ? DateTime.MaxValue : DateTime.MinValue;
    }

    protected override Task InvokeOnUIThreadAsync(Action action)
    {
        action();
        return Task.CompletedTask;
    }
}
