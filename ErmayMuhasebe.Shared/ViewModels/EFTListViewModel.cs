using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ErmayMuhasebe.Models;
using ErmayMuhasebe.Services;
using ErmayMuhasebe.Repositories;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;

namespace ErmayMuhasebe.Shared.ViewModels;

public abstract partial class EFTListViewModel : ViewModelBase
{
    protected readonly IUnitOfWork _uow;
    protected readonly IPdfService _pdfService;

    [ObservableProperty] private bool _isEditDialogVisible;
    [ObservableProperty] private string _dialogTitle = "Yeni Havale / EFT İşlemi";
    
    [ObservableProperty] private int _editId;
    [ObservableProperty] private string _editTarihStr = DateTime.Now.ToString("dd.MM.yyyy");
    [ObservableProperty] private decimal _editTutar;
    [ObservableProperty] private string _editBanka = "";
    [ObservableProperty] private string _editHesapNo = "";
    [ObservableProperty] private string _editDekontNo = "";
    [ObservableProperty] private string _editDurum = "Portföyde";
    [ObservableProperty] private string _editAciklama = "";
    [ObservableProperty] private string _editDekontPath = ""; 
    
    [ObservableProperty] private int _editMusteriId;
    [ObservableProperty] private string _editMusteriUnvan = "Müşteri Seçiniz...";
    
    [ObservableProperty] private int? _editYonlendirilenCariId;
    [ObservableProperty] private string _editYonlendirilenCariUnvan = "Tedarikçi Seçiniz (Opsiyonel)";

    [ObservableProperty] private bool _isSaving;

    [ObservableProperty]
    private ObservableCollection<EftIslem> _islemler = new();

    [ObservableProperty]
    private EftIslem? _selectedIslem;

    // Filtering
    [ObservableProperty] private DateTime _filterBaslangicTarihi = DateTime.Now.AddMonths(-1);
    [ObservableProperty] private DateTime _filterBitisTarihi = DateTime.Now;
    [ObservableProperty] private string _filterText = "";
    [ObservableProperty] private string _filterDurum = "Hepsi";

    partial void OnFilterBaslangicTarihiChanged(DateTime value) => _ = LoadIslemlerAsync();
    partial void OnFilterBitisTarihiChanged(DateTime value) => _ = LoadIslemlerAsync();
    partial void OnFilterTextChanged(string value) => _ = LoadIslemlerAsync();
    partial void OnFilterDurumChanged(string value) => _ = LoadIslemlerAsync();

    // Cari Seçim Penceresi Kontrolleri
    [ObservableProperty] private bool _isCariSecimVisible;
    [ObservableProperty] private string _cariSearchText = "";
    [ObservableProperty] private ObservableCollection<CariKart> _cariListesi = new();
    [ObservableProperty] private CariKart? _selectedCariInList;
    [ObservableProperty] private bool _selectingForSupplier; // true if selecting supplier, false if customer

    public EFTListViewModel(IUnitOfWork uow, IPdfService pdfService)
    {
        _uow = uow;
        _pdfService = pdfService;
        _ = LoadIslemlerAsync();
    }

    [RelayCommand]
    public async Task LoadIslemlerAsync()
    {
        var list = await _uow.EftIslemleri.GetAllAsync();
        await InvokeOnUIThreadAsync(() => 
        {
            var filtered = list.Where(x => x.Tarih.Date >= FilterBaslangicTarihi.Date && x.Tarih.Date <= FilterBitisTarihi.Date);
            
            if (FilterDurum != "Hepsi")
            {
                filtered = filtered.Where(x => x.Durum == FilterDurum);
            }

            if (!string.IsNullOrWhiteSpace(FilterText))
            {
                filtered = filtered.Where(x => (x.MusteriUnvan ?? "").Contains(FilterText, StringComparison.OrdinalIgnoreCase) || 
                                             (x.Banka ?? "").Contains(FilterText, StringComparison.OrdinalIgnoreCase) ||
                                             (x.HesapNo ?? "").Contains(FilterText, StringComparison.OrdinalIgnoreCase) ||
                                             (x.DekontNo ?? "").Contains(FilterText, StringComparison.OrdinalIgnoreCase));
            }

            Islemler = new ObservableCollection<EftIslem>(filtered.OrderByDescending(x => x.Tarih));
        });
    }

    public override void OnNavigatedTo()
    {
        _ = LoadIslemlerAsync();
    }

    [RelayCommand]
    public void OpenAddIslemDialog()
    {
        EditId = 0;
        EditTarihStr = DateTime.Now.ToString("dd.MM.yyyy");
        EditTutar = 0;
        EditBanka = "";
        EditHesapNo = "";
        EditDekontNo = "";
        EditDurum = "Portföyde";
        EditAciklama = "";
        EditDekontPath = "";
        EditMusteriId = 0;
        EditMusteriUnvan = "Seçiniz...";
        EditYonlendirilenCariId = null;
        EditYonlendirilenCariUnvan = "Seçiniz (Opsiyonel)...";
        
        DialogTitle = "Yeni Havale / EFT İşlemi";
        IsEditDialogVisible = true;
    }

    [RelayCommand]
    public void OpenEditIslemDialog(EftIslem? islem)
    {
        if (islem == null) return;
        
        EditId = islem.Id;
        EditTarihStr = islem.Tarih.ToString("dd.MM.yyyy");
        EditTutar = islem.Tutar;
        EditBanka = islem.Banka ?? "";
        EditHesapNo = islem.HesapNo ?? "";
        EditDekontNo = islem.DekontNo ?? "";
        EditDurum = islem.Durum ?? "Portföyde";
        EditAciklama = islem.Aciklama ?? "";
        EditDekontPath = islem.DekontPath ?? "";
        EditMusteriId = islem.MusteriId;
        EditMusteriUnvan = islem.MusteriUnvan ?? "Seçiniz...";
        EditYonlendirilenCariId = islem.YonlendirilenCariId;
        EditYonlendirilenCariUnvan = islem.YonlendirilenCariUnvan ?? "Seçiniz (Opsiyonel)...";
        
        DialogTitle = "İşlem Bilgilerini Düzenle";
        IsEditDialogVisible = true;
    }

    [RelayCommand]
    public async Task SaveIslemAsync()
    {
        if (EditMusteriId == 0 || EditTutar <= 0) return;
        if (IsSaving) return;

        IsSaving = true;
        try
        {
            int? oldSupplierId = null;
            if (EditId != 0)
            {
                var existing = await _uow.EftIslemleri.GetByIdAsync(EditId);
                if (existing != null)
                {
                    oldSupplierId = existing.YonlendirilenCariId;
                }
            }

            var islem = new EftIslem
            {
                Id = EditId,
                MusteriId = EditMusteriId,
                MusteriUnvan = EditMusteriUnvan,
                Tarih = GetParsedDate(),
                Tutar = EditTutar,
                Banka = EditBanka,
                HesapNo = EditHesapNo,
                DekontNo = EditDekontNo,
                Durum = EditYonlendirilenCariId.HasValue ? "Tedarikçiye Yönlendirildi" : (EditDurum == "Tedarikçiye Yönlendirildi" || EditDurum == "Tedarikçiye Verildi" ? "Tamamlandı" : EditDurum),
                Aciklama = EditAciklama,
                DekontPath = EditDekontPath,
                YonlendirilenCariId = EditYonlendirilenCariId,
                YonlendirilenCariUnvan = (EditYonlendirilenCariUnvan == "Tedarikçi Seçiniz (Opsiyonel)" || EditYonlendirilenCariUnvan == "Seçiniz (Opsiyonel)..." || EditYonlendirilenCariUnvan == "Seçiniz (Opsiyonel)...") ? null : EditYonlendirilenCariUnvan
            };

            if (EditYonlendirilenCariId.HasValue)
            {
                islem.YonlendirmeTarihi = islem.Tarih;
            }

            // Sync with Cari
            if (!string.IsNullOrEmpty(islem.DekontNo))
            {
                var cariHareket = await _uow.Cariler.GetHareketByEvrakNoAsync(islem.DekontNo);
                if (cariHareket != null)
                {
                    cariHareket.Tarih = islem.Tarih;
                    cariHareket.Aciklama = islem.Aciklama;
                    if (cariHareket.Alacak > 0) cariHareket.Alacak = islem.Tutar;
                    else cariHareket.Borc = islem.Tutar;
                    await _uow.Cariler.SaveHareketAsync(cariHareket);
                    await _uow.Cariler.RecalculateBalanceAsync(cariHareket.CariId);
                }
            }

            await _uow.EftIslemleri.SaveAsync(islem);

            await _uow.Cariler.RecalculateBalanceAsync(islem.MusteriId);
            if (islem.YonlendirilenCariId.HasValue)
            {
                await _uow.Cariler.RecalculateBalanceAsync(islem.YonlendirilenCariId.Value);
            }
            if (oldSupplierId.HasValue && oldSupplierId.Value != islem.YonlendirilenCariId)
            {
                await _uow.Cariler.RecalculateBalanceAsync(oldSupplierId.Value);
            }

            IsEditDialogVisible = false;
            await LoadIslemlerAsync();
            NotifyFinancialDataChanged();
        }
        finally
        {
            IsSaving = false;
        }
    }

    protected abstract void NotifyFinancialDataChanged();

    [RelayCommand]
    public void CloseDialog() => IsEditDialogVisible = false;

    [RelayCommand]
    public void CloseCariSecim() => IsCariSecimVisible = false;

    [RelayCommand]
    public void DeleteIslemConfirm(EftIslem islem)
    {
        if (islem == null) return;
        ShowConfirm("İşlemi Sil", "Seçili Havale/EFT işlemini silmek istediğinize emin misiniz?", () => DeleteIslemAsync(islem));
    }

    public async Task DeleteIslemAsync(EftIslem islem)
    {
        if (islem == null) return;

        int? oldSupplierId = islem.YonlendirilenCariId;
        
        // Sync with Cari
        if (!string.IsNullOrEmpty(islem.DekontNo))
        {
            await _uow.Cariler.DeleteHareketByEvrakNoAsync(islem.DekontNo);
        }

        await _uow.EftIslemleri.DeleteAsync(islem.Id);

        await _uow.Cariler.RecalculateBalanceAsync(islem.MusteriId);
        if (oldSupplierId.HasValue)
        {
            await _uow.Cariler.RecalculateBalanceAsync(oldSupplierId.Value);
        }

        NotifyFinancialDataChanged();
        await LoadIslemlerAsync();
    }

    [RelayCommand]
    public async Task ViewIslemPdfAsync(EftIslem? islem)
    {
        if (islem == null) return;
        try 
        {
            var pdfBytes = await _pdfService.GenerateEftSlipPdfBytesAsync(islem);
            await HandleFileOpenAsync(pdfBytes, $"EftSlip_{islem.Id}.pdf");
        }
        catch { }
    }

    [RelayCommand]
    public async Task ViewIslemAsync(EftIslem? islem) => await ViewIslemPdfAsync(islem);

    [RelayCommand]
    public async Task GenerateFilteredReportAsync()
    {
        try 
        {
             var bytes = await _pdfService.GenerateEftListPdfBytesAsync(Islemler.ToList());
             await HandleFileOpenAsync(bytes, $"EftListesi_{DateTime.Now:ddMM}.pdf");
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    protected abstract Task HandleFileOpenAsync(byte[] content, string fileName);

    [RelayCommand]
    public void ViewIslemDetail(EftIslem? islem)
    {
        if (islem == null) return;
        OpenEditIslemDialog(islem);
        DialogTitle = "İşlem Detayı";
    }

    [RelayCommand]
    public void OpenMusteriSecim()
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
    public async Task SearchCariAsync()
    {
        var list = await _uow.Cariler.GetAllAsync();
        if (SelectingForSupplier) list = list.Where(x => x.Grup == "Tedarikçi").ToList();
        else list = list.Where(x => x.Grup == "Müşteri").ToList();

        if (!string.IsNullOrWhiteSpace(CariSearchText))
            list = list.Where(x => x.Unvan != null && x.Unvan.Contains(CariSearchText, StringComparison.OrdinalIgnoreCase)).ToList();
        
        CariListesi = new ObservableCollection<CariKart>(list);
    }

    [RelayCommand]
    public void SelectCari(CariKart? cari)
    {
        if (cari == null) return;
        if (SelectingForSupplier)
        {
            EditYonlendirilenCariId = cari.Id;
            EditYonlendirilenCariUnvan = cari.Unvan ?? "";
            EditDurum = "Tedarikçiye Yönlendirildi";
        }
        else
        {
            EditMusteriId = cari.Id;
            EditMusteriUnvan = cari.Unvan ?? "";
        }
        IsCariSecimVisible = false;
    }

    private DateTime GetParsedDate()
    {
        if (DateTime.TryParseExact(EditTarihStr, "dd.MM.yyyy", null, System.Globalization.DateTimeStyles.None, out var dt))
            return dt;
        return DateTime.Now;
    }

    protected override Task InvokeOnUIThreadAsync(Action action)
    {
        action();
        return Task.CompletedTask;
    }
}
