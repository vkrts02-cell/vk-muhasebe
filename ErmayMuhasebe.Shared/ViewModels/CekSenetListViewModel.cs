using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ErmayMuhasebe.Models;
using ErmayMuhasebe.Services;
using ErmayMuhasebe.Repositories;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using CommunityToolkit.Mvvm.Messaging;

namespace ErmayMuhasebe.Shared.ViewModels;

public abstract partial class CekSenetListViewModel : ViewModelBase
{
    protected readonly IUnitOfWork _uow;
    protected readonly IPdfService _pdfService;

    [ObservableProperty]
    private ObservableCollection<Cek> _cekler = new();

    [ObservableProperty]
    private Cek? _selectedCek;

    // --- Edit Form Properties ---
    [ObservableProperty] private bool _isEditFormVisible;
    [ObservableProperty] private string _editTitle = "Yeni Çek Kaydı";
    [ObservableProperty] private int? _editingCekId;
    [ObservableProperty] private string? _editPortfoyNo;
    [ObservableProperty] private string? _editAsilBorclu;
    [ObservableProperty] private DateTime _editVadeTarihi = DateTime.Now;
    [ObservableProperty] private string _editVadeTarihiStr = DateTime.Now.ToString("dd.MM.yyyy");
    [ObservableProperty] private DateTime _editIslemTarihi = DateTime.Now;
    [ObservableProperty] private string _editIslemTarihiStr = DateTime.Now.ToString("dd.MM.yyyy");
    [ObservableProperty] private decimal? _editTutar;
    [ObservableProperty] private string? _editBanka;
    [ObservableProperty] private string? _editSube;
    [ObservableProperty] private string? _editCekTuru = "Alınan"; // Alınan / Verilen
    [ObservableProperty] private string? _editSeriNo;
    [ObservableProperty] private string? _editAciklama;
    [ObservableProperty] private int? _editCariId;
    [ObservableProperty] private string? _editCariUnvan;
    [ObservableProperty] private string? _editGorselYoluOn;
    [ObservableProperty] private string? _editGorselYoluArka;
    [ObservableProperty] private string? _editIslemTuru = "Tahsilat";
    [ObservableProperty] private string? _editTransactionMethod = "Çek";
    [ObservableProperty] private decimal _totalCekTutari;
    
    // Filtering
    [ObservableProperty] private DateTime _filterBaslangicTarihi = DateTime.Now.AddMonths(-3);
    [ObservableProperty] private DateTime _filterBitisTarihi = DateTime.Now.AddMonths(3);
    [ObservableProperty] private string _filterText = "";
    [ObservableProperty] private string _filterDurum = "Hepsi";

    partial void OnFilterBaslangicTarihiChanged(DateTime value) => _ = LoadDataAsync();
    partial void OnFilterBitisTarihiChanged(DateTime value) => _ = LoadDataAsync();
    partial void OnFilterTextChanged(string value) => _ = LoadDataAsync();
    partial void OnFilterDurumChanged(string value) => _ = LoadDataAsync();

    partial void OnEditCekTuruChanged(string? value)
    {
        if (string.Equals(value, "Verilen", StringComparison.OrdinalIgnoreCase))
        {
            EditIslemTuru = "Ödeme";
        }
        else
        {
            EditIslemTuru = "Tahsilat";
        }
    }

    // --- Yönlendirilen Tedarikçi ---
    [ObservableProperty] private int? _editYonlendirilenCariId;
    [ObservableProperty] private string? _editYonlendirilenCariUnvan;
    [ObservableProperty] private bool _selectingForSupplier;
    [ObservableProperty] private string? _selectedCariGrup; // Müşteri veya Tedarikçi ayrımı için

    // --- Cari Selection ---
    [ObservableProperty] private bool _isCariSecimVisible;
    [ObservableProperty] private string _cariSearchText = "";
    [ObservableProperty] private ObservableCollection<CariKart> _cariListesi = new();
    [ObservableProperty] private CariKart? _selectedCariForAdd;

    public CekSenetListViewModel(IUnitOfWork uow, IPdfService pdfService)
    {
        _uow = uow;
        _pdfService = pdfService;
        
        _ = LoadDataAsync();
    }

    public override void OnNavigatedTo()
    {
        _ = LoadDataAsync();
    }

    [RelayCommand]
    public async Task LoadDataAsync()
    {
        var cekList = await _uow.Cekler.GetAllAsync();
        
        await InvokeOnUIThreadAsync(() => 
        {
            var filtered = cekList.Where(x => x.VadeTarihi.Date >= FilterBaslangicTarihi.Date && x.VadeTarihi.Date <= FilterBitisTarihi.Date);
            
            if (FilterDurum != "Hepsi")
            {
                filtered = filtered.Where(x => x.Durum == FilterDurum);
            }

            if (!string.IsNullOrWhiteSpace(FilterText))
            {
                filtered = filtered.Where(x => (x.AsilBorclu ?? "").Contains(FilterText, StringComparison.OrdinalIgnoreCase) || 
                                             (x.PortfoyNo ?? "").Contains(FilterText, StringComparison.OrdinalIgnoreCase) ||
                                             (x.SeriNo ?? "").Contains(FilterText, StringComparison.OrdinalIgnoreCase) ||
                                             (x.Banka ?? "").Contains(FilterText, StringComparison.OrdinalIgnoreCase));
            }

            Cekler = new ObservableCollection<Cek>(filtered.OrderByDescending(x => x.VadeTarihi));
            TotalCekTutari = Cekler.Sum(x => x.Tutar);
        });
    }

    [RelayCommand]
    public void OpenNewCekForm()
    {
        ClearEditForm();
        EditingCekId = null;
        EditTitle = "Yeni Çek Kaydı";
        EditVadeTarihiStr = DateTime.Now.ToString("dd.MM.yyyy");
        EditIslemTarihiStr = DateTime.Now.ToString("dd.MM.yyyy");
        IsEditFormVisible = true;
    }

    [RelayCommand]
    public void OpenEditCekForm(Cek? cek)
    {
        if (cek == null) return;
        
        EditingCekId = cek.Id;
        EditTitle = "Çek Bilgilerini Düzenle";
        EditPortfoyNo = cek.PortfoyNo;
        EditAsilBorclu = cek.AsilBorclu;
        EditVadeTarihi = cek.VadeTarihi;
        EditVadeTarihiStr = cek.VadeTarihi.ToString("dd.MM.yyyy");
        EditIslemTarihi = cek.IslemTarihi;
        EditIslemTarihiStr = cek.IslemTarihi.ToString("dd.MM.yyyy");
        EditTutar = cek.Tutar;
        EditBanka = cek.Banka;
        EditSube = cek.Sube;
        EditCekTuru = cek.CekTuru;
        EditSeriNo = cek.SeriNo;
        EditAciklama = cek.Aciklama;
        EditCariId = cek.CariId;
        EditCariUnvan = cek.CariUnvan ?? "Cari Seçiniz...";
        EditGorselYoluOn = cek.GorselYoluOn ?? cek.GorselYolu; 
        EditGorselYoluArka = cek.GorselYoluArka;
        EditIslemTuru = cek.IslemTuru;
        EditYonlendirilenCariId = cek.YonlendirilenCariId;
        EditYonlendirilenCariUnvan = cek.YonlendirilenCariUnvan;
        
        IsEditFormVisible = true;
    }

    [RelayCommand]
    public void CloseEditForm() => IsEditFormVisible = false;

    private void ClearEditForm()
    {
        EditPortfoyNo = Guid.NewGuid().ToString().Substring(0, 8).ToUpper();
        EditAsilBorclu = "";
        EditVadeTarihi = DateTime.Now;
        EditIslemTarihi = DateTime.Now;
        EditIslemTarihiStr = DateTime.Now.ToString("dd.MM.yyyy");
        EditTutar = 0;
        EditBanka = "";
        EditSube = "";
        EditCekTuru = "Alınan";
        EditSeriNo = "";
        EditAciklama = "";
        EditCariId = null;
        EditCariUnvan = "Cari Seçiniz...";
        EditGorselYoluOn = null;
        EditGorselYoluArka = null;
        EditIslemTuru = "Tahsilat";
        EditTransactionMethod = "Çek";
        EditYonlendirilenCariId = null;
        EditYonlendirilenCariUnvan = null;
        SelectingForSupplier = false;
    }

    [RelayCommand]
    public async Task SaveCekAsync()
    {
        if (string.IsNullOrWhiteSpace(EditAsilBorclu) && string.IsNullOrWhiteSpace(EditCariUnvan)) return;

        var currentDurum = "Portföyde";
        int? oldSupplierId = null;
        if (EditingCekId.HasValue)
        {
            var existing = await _uow.Cekler.GetByIdAsync(EditingCekId.Value);
            if (existing != null)
            {
                currentDurum = existing.Durum;
                oldSupplierId = existing.YonlendirilenCariId;
            }
        }

        var cek = new Cek
        {
            Id = EditingCekId ?? 0,
            PortfoyNo = EditPortfoyNo,
            AsilBorclu = string.IsNullOrWhiteSpace(EditAsilBorclu) ? EditCariUnvan : EditAsilBorclu,
            VadeTarihi = GetParsedVadeTarihi(),
            IslemTarihi = GetParsedIslemTarihi(),
            Tutar = EditTutar ?? 0,
            Durum = EditYonlendirilenCariId.HasValue ? "Ciro Edildi" : currentDurum,
            Banka = EditBanka,
            Sube = EditSube,
            CekTuru = EditCekTuru,
            SeriNo = EditSeriNo,
            Aciklama = EditAciklama,
            CariId = EditCariId,
            CariUnvan = EditCariUnvan,
            GorselYoluOn = EditGorselYoluOn,
            GorselYoluArka = EditGorselYoluArka,
            GorselYolu = EditGorselYoluOn,
            IslemTuru = EditIslemTuru,
            YonlendirilenCariId = EditYonlendirilenCariId,
            YonlendirilenCariUnvan = EditYonlendirilenCariUnvan
        };

        await _uow.Cekler.SaveWithTransactionAsync(cek);

        if (cek.CariId.HasValue)
        {
            await _uow.Cariler.RecalculateBalanceAsync(cek.CariId.Value);
        }
        if (cek.YonlendirilenCariId.HasValue)
        {
            await _uow.Cariler.RecalculateBalanceAsync(cek.YonlendirilenCariId.Value);
        }
        if (oldSupplierId.HasValue && oldSupplierId.Value != cek.YonlendirilenCariId)
        {
            await _uow.Cariler.RecalculateBalanceAsync(oldSupplierId.Value);
        }

        IsEditFormVisible = false;
        await LoadDataAsync();

        WeakReferenceMessenger.Default.Send(new TransactionSavedMessage(true));
        NotifyFinancialDataChanged(cek);
    }

    protected abstract void NotifyFinancialDataChanged(Cek? cek);

    [RelayCommand]
    public void DeleteCekConfirm(Cek? cek)
    {
        if (cek == null) return;
        ShowConfirm("Çek/Senet Sil", $"{cek.PortfoyNo} portföy numaralı kaydı silmek istediğinize emin misiniz?", () => DeleteCekAsync(cek));
    }

    public async Task DeleteCekAsync(Cek? cek)
    {
        if (cek == null) return;

        int? oldSupplierId = cek.YonlendirilenCariId;
        
        // Sync with Cari
        if (!string.IsNullOrEmpty(cek.PortfoyNo))
        {
            await _uow.Cariler.DeleteHareketByEvrakNoAsync(cek.PortfoyNo);
            await _uow.Cariler.DeleteHareketByEvrakNoAsync("Cek-SUP-" + cek.PortfoyNo);
        }
        else if (!string.IsNullOrEmpty(cek.SeriNo))
        {
            await _uow.Cariler.DeleteHareketByEvrakNoAsync(cek.SeriNo);
            await _uow.Cariler.DeleteHareketByEvrakNoAsync("Cek-SUP-" + cek.SeriNo);
        }

        await _uow.Cekler.DeleteAsync(cek.Id);

        if (cek.CariId.HasValue)
        {
            await _uow.Cariler.RecalculateBalanceAsync(cek.CariId.Value);
        }
        if (oldSupplierId.HasValue)
        {
            await _uow.Cariler.RecalculateBalanceAsync(oldSupplierId.Value);
        }

        WeakReferenceMessenger.Default.Send(new TransactionSavedMessage(true));
        NotifyFinancialDataChanged(null);
        await LoadDataAsync();
    }

    [RelayCommand]
    public async Task ViewCekPdfAsync(Cek? cek)
    {
        if (cek == null) return;
        try 
        {
            var pdfBytes = await _pdfService.GenerateCekPdfBytesAsync(cek);
            await HandleFileOpenAsync(pdfBytes, $"Cek_{cek.PortfoyNo}.pdf");
        }
        catch (Exception ex) 
        {
            System.Diagnostics.Debug.WriteLine($"PDF Error: {ex}");
            ErrorMessage = "PDF oluşturulurken hata!";
        }
    }

    [RelayCommand]
    public async Task GenerateFilteredReportAsync()
    {
        try 
        {
             // Note: Reusing StokListPdf logic if no specific CekListPdf exists, 
             // but ideally we should have a CekList report. 
             // For now assume _pdfService has a method or we export as Excel/Generic report.
             // If not, we can use a generic list report.
             var bytes = await _pdfService.GenerateCekListPdfBytesAsync(Cekler.ToList());
             await HandleFileOpenAsync(bytes, $"CekListesi_{DateTime.Now:ddMM}.pdf");
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    protected abstract Task HandleFileOpenAsync(byte[] content, string fileName);

    [RelayCommand]
    public async Task UpdateCekDurumAsync(string durum)
    {
        if (SelectedCek == null) return;
        SelectedCek.Durum = durum == "Tahsil" ? "Tahsil Edildi" : (durum == "Karsiliksiz" ? "Karşılıksız" : durum);
        await _uow.Cekler.SaveAsync(SelectedCek);
        await LoadDataAsync();
    }

    // --- Cari Search Methods ---
    [RelayCommand]
    public void OpenCariSecim()
    {
        SelectingForSupplier = false;
        IsCariSecimVisible = true;
        _ = SearchCariAsync();
    }

    [RelayCommand]
    public void OpenTedarikciSecim()
    {
        SelectingForSupplier = true;
        IsCariSecimVisible = true;
        _ = SearchCariAsync();
    }

    [RelayCommand]
    public void CloseCariSecim() => IsCariSecimVisible = false;

    [RelayCommand]
    public async Task SearchCariAsync()
    {
        var cariler = await _uow.Cariler.GetAllAsync();
        if (!string.IsNullOrWhiteSpace(CariSearchText))
        {
            cariler = cariler.Where(c => c.Unvan?.Contains(CariSearchText, StringComparison.OrdinalIgnoreCase) == true).ToList();
        }
        
        if (SelectingForSupplier)
        {
            cariler = cariler.Where(c => c.Grup == "Tedarikçi").ToList();
        }

        CariListesi = new ObservableCollection<CariKart>(cariler);
    }

    [RelayCommand]
    public void SelectCari()
    {
        if (SelectedCariForAdd != null)
        {
            if (SelectingForSupplier)
            {
                EditYonlendirilenCariId = SelectedCariForAdd.Id;
                EditYonlendirilenCariUnvan = SelectedCariForAdd.Unvan;
                // Tedarikçiye ciro edildiğini belirtmek için durumu güncelleyebiliriz:
                // EditDurum = "Ciro Edildi"; (Eğer Durum alanı formda gösteriliyorsa)
            }
            else
            {
                EditCariId = SelectedCariForAdd.Id;
                EditCariUnvan = SelectedCariForAdd.Unvan;
                if (string.IsNullOrWhiteSpace(EditAsilBorclu)) EditAsilBorclu = SelectedCariForAdd.Unvan;
            }
            IsCariSecimVisible = false;
        }
    }

    public async Task OpenCheckFromEvrakNoAsync(string? evrakNo)
    {
        if (string.IsNullOrWhiteSpace(evrakNo)) return;
        
        var allCeks = await _uow.Cekler.GetAllAsync();
        var cek = allCeks.FirstOrDefault(c => c.PortfoyNo == evrakNo || c.SeriNo == evrakNo);
        
        if (cek != null)
        {
            OpenEditCekForm(cek);
        }
    }

    protected DateTime GetParsedVadeTarihi()
    {
        if (DateTime.TryParseExact(EditVadeTarihiStr, "dd.MM.yyyy", null, System.Globalization.DateTimeStyles.None, out var dt))
            return dt;
        return DateTime.Now;
    }

    protected DateTime GetParsedIslemTarihi()
    {
        if (DateTime.TryParseExact(EditIslemTarihiStr, "dd.MM.yyyy", null, System.Globalization.DateTimeStyles.None, out var dt))
            return dt;
        return DateTime.Now;
    }

    partial void OnEditTransactionMethodChanged(string? value)
    {
        if (value != "Çek" && value != null && IsEditFormVisible)
        {
            CloseEditForm();
            // Notify parent to open the main transaction dialog with this new method
            WeakReferenceMessenger.Default.Send(new TransactionMethodChangedMessage(value));
        }
    }

    public class TransactionMethodChangedMessage : CommunityToolkit.Mvvm.Messaging.Messages.ValueChangedMessage<string>
    {
        public TransactionMethodChangedMessage(string value) : base(value) { }
    }

    public class TransactionSavedMessage : CommunityToolkit.Mvvm.Messaging.Messages.ValueChangedMessage<bool>
    {
        public TransactionSavedMessage(bool value) : base(value) { }
    }

    protected override Task InvokeOnUIThreadAsync(Action action)
    {
        action();
        return Task.CompletedTask;
    }
}
