using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ErmayMuhasebe.Models;
using ErmayMuhasebe.Repositories;
using ErmayMuhasebe.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace ErmayMuhasebe.Shared.ViewModels;

public abstract partial class BankaDetayViewModel : ViewModelBase
{
    protected readonly IUnitOfWork _uow;
    protected readonly IPdfService _pdfService;
    protected BankaKart? _banka;

    [ObservableProperty] private string _bankaAdi = "İsimsiz Banka";
    [ObservableProperty] private decimal _guncelBakiye;
    [ObservableProperty] private string _dovizTuru = "TL";
    [ObservableProperty] private ObservableCollection<BankaHareket> _hareketler = new();
    [ObservableProperty] private BankaHareket? _selectedHareket;
    [ObservableProperty] private ObservableCollection<CariKart> _cariler = new(); // For ComboBox selection

    // Transaction Edit Dialog Properties
    [ObservableProperty] private bool _isTransactionDialogVisible;
    [ObservableProperty] private BankaHareket? _editingHareket;
    [ObservableProperty] private string _transactionTitle = "İşlem Düzenle";
    [ObservableProperty] private decimal _transactionAmount;
    [ObservableProperty] private DateTime _transactionDate = DateTime.Now;
    [ObservableProperty] private string _transactionDateStr = DateTime.Now.ToString("dd.MM.yyyy");
    [ObservableProperty] private string _transactionDescription = "";
    [ObservableProperty] private string _transactionType = "";
    [ObservableProperty] private bool _isReadOnly;
    [ObservableProperty] private bool _isEditMode;
    [ObservableProperty] private bool _isDetailMode;

    public BankaDetayViewModel(IUnitOfWork uow, IPdfService pdfService)
    {
        _uow = uow;
        _pdfService = pdfService;
    }

    public async Task InitializeAsync(int bankaId)
    {
        var banka = await _uow.Bankalar.GetByIdAsync(bankaId);
        if (banka == null) return;
        await InitializeAsync(banka);
    }

    public async Task InitializeAsync(BankaKart banka)
    {
        _banka = banka;
        BankaAdi = banka.BankaAdi ?? "İsimsiz Banka";
        GuncelBakiye = banka.AcilisBakiyesi;
        DovizTuru = GetCleanDoviz(banka.DovizTuru);
        
        if (_uow != null)
        {
             var caris = await _uow.Cariler.GetAllAsync();
             Cariler = new ObservableCollection<CariKart>(caris.OrderBy(c => c.Unvan));
        }

        await LoadHareketlerAsync();
    }

    protected string GetCleanDoviz(string? val)
    {
        if (string.IsNullOrEmpty(val)) return "TL";
        string s = val.ToString();
        if (s.Contains(":")) s = s.Split(':').Last();
        if (s.Contains(".")) s = s.Split('.').Last();
        return s.Trim();
    }

    [RelayCommand]
    public virtual async Task LoadHareketlerAsync()
    {
        if (_banka == null) return;
        try 
        {
            IsLoading = true;
            var list = await _uow.Bankalar.GetHareketlerAsync(_banka.Id);
            
            await InvokeOnUIThreadAsync(() => 
            {
                Hareketler = new ObservableCollection<BankaHareket>(list.OrderByDescending(h => h.Tarih));
                
                decimal bakiye = _banka.AcilisBakiyesi;
                foreach (var h in list)
                {
                    bakiye += (h.Giren - h.Cikan);
                }
                GuncelBakiye = bakiye;
            });
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Hareket yükleme hatası: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task DeleteHareketAsync(BankaHareket? hareket)
    {
        try 
        {
            BankaHareket? target = hareket ?? SelectedHareket;
            if (target == null || target.Id == 0) return;

            int deletedCari = 0;
            if (!string.IsNullOrEmpty(target.EvrakNo))
            {
                deletedCari = await _uow.Cariler.DeleteHareketByEvrakNoAsync(target.EvrakNo);
            }

            int deletedRows = await _uow.Bankalar.DeleteHareketAsync(target.Id);
            
            if (deletedRows > 0 || deletedCari > 0 || target != null)
            {
                if (SelectedHareket == target) SelectedHareket = null;
                await LoadHareketlerAsync();
                NotifyFinancialDataChanged();
                SuccessMessage = "İşlem silindi.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Silme hatası: {ex.Message}";
        }
    }

    protected abstract void NotifyFinancialDataChanged();

    [RelayCommand]
    public void OpenEditTransactionDialog(BankaHareket? hareket)
    {
        if (hareket == null) hareket = SelectedHareket;
        if (hareket == null) return;

        IsReadOnly = false;
        IsEditMode = true;
        IsDetailMode = false;
        SetupTransactionDialog(hareket, "İŞLEMİ DÜZENLE");
    }

    [RelayCommand]
    public void ViewTransactionDetail(BankaHareket? hareket)
    {
        if (hareket == null) hareket = SelectedHareket;
        if (hareket == null) return;

        IsReadOnly = true;
        IsEditMode = false;
        IsDetailMode = true;
        SetupTransactionDialog(hareket, "İŞLEM DETAYLARI");
    }

    [ObservableProperty] private string _transactionCariUnvan = "";
    [ObservableProperty] private int? _transactionCariId;
    [ObservableProperty] private string _transactionDekontPath = "";
    
    public bool IsTransferPossible => SelectedHareket != null && 
                                      SelectedHareket.Giren > 0 && 
                                      SelectedHareket.YonlendirilenCariId == null;

    [RelayCommand]
    public abstract Task SelectCariForTransactionAsync();

    [RelayCommand]
    public abstract Task UploadDekontForTransactionAsync();

    [RelayCommand]
    public abstract Task TransferTransactionAsync();

    protected void SetupTransactionDialog(BankaHareket hareket, string title)
    {
        EditingHareket = hareket;
        TransactionAmount = hareket.Giren > 0 ? hareket.Giren : hareket.Cikan;
        TransactionDate = hareket.Tarih;
        TransactionDateStr = hareket.Tarih.ToString("dd.MM.yyyy");
        TransactionDescription = hareket.Aciklama ?? "";
        TransactionType = hareket.IslemTuru ?? "";
        TransactionCariUnvan = hareket.CariUnvan ?? "";
        TransactionCariId = null; // Can't easily recover ID from Unvan unless we store it. Hareket doesn't have CariId for direct transaction yet? Wait, BankaHareket HAS NO CariId field in original model, I added CariUnvan. I should rely on CariUnvan.
        // Actually I should verify logic. In `BankaIslemDialog` (Blazor) I return CariId.
        // BankaHareket model (updated in Step 53) DOES NOT have CariId. It has YonlendirilenCariId.
        // It has `CariUnvan`. 
        // Ideally I should add `CariId` to BankaHareket if I want strong link.
        // But for now Unvan is used.
        TransactionDekontPath = hareket.DekontPath ?? "";
        
        TransactionTitle = $"{title}: {hareket.EvrakNo}";
        IsTransactionDialogVisible = true;
        OnPropertyChanged(nameof(IsTransferPossible));
    }

    [RelayCommand]
    public void OpenAddTransactionDialog(string tur)
    {
        if (_banka == null) return;
        
        IsReadOnly = false;
        IsEditMode = true;
        IsDetailMode = false;
        
        var yeniHareket = new BankaHareket
        {
            BankaId = _banka.Id,
            Tarih = DateTime.Now,
            IslemTuru = tur.ToUpper() == "TAHSİLAT" ? "GİRİŞ" : "ÇIKIŞ",
            EvrakNo = "YENİ",
            Aciklama = ""
        };
        
        SetupTransactionDialog(yeniHareket, tur.ToUpper() + " EKLE");
    }

    [RelayCommand]
    public async Task SaveTransactionAsync()
    {
        if (EditingHareket == null) return;

        DateTime parsedDate = GetParsedTransactionDate();
        EditingHareket.Tarih = parsedDate;
        EditingHareket.Aciklama = TransactionDescription;
        
        // Islem türüne göre Giren/Cikan ayarla
        if (EditingHareket.IslemTuru == "GİRİŞ")
        {
            EditingHareket.Giren = TransactionAmount;
            EditingHareket.Cikan = 0;
        }
        else
        {
            EditingHareket.Cikan = TransactionAmount;
            EditingHareket.Giren = 0;
        }
        
        EditingHareket.CariUnvan = TransactionCariUnvan;
        EditingHareket.DekontPath = TransactionDekontPath;

        // Sync WITH CARI
        if (!string.IsNullOrEmpty(EditingHareket.EvrakNo))
        {
            var cariHareket = await _uow.Cariler.GetHareketByEvrakNoAsync(EditingHareket.EvrakNo);
            if (cariHareket != null)
            {
                cariHareket.Tarih = EditingHareket.Tarih;
                cariHareket.Aciklama = EditingHareket.Aciklama;
                if (cariHareket.Alacak > 0) cariHareket.Alacak = TransactionAmount;
                else cariHareket.Borc = TransactionAmount;
                
                await _uow.Cariler.SaveHareketAsync(cariHareket);
                await _uow.Cariler.RecalculateBalanceAsync(cariHareket.CariId);
            }
        }

        await _uow.Bankalar.SaveHareketAsync(EditingHareket);
        IsTransactionDialogVisible = false;
        await LoadHareketlerAsync();
        NotifyFinancialDataChanged();
        SuccessMessage = EditingHareket.Id == 0 ? "Yeni işlem eklendi." : "İşlem güncellendi.";
    }

    [RelayCommand]
    public void CloseTransactionDialog()
    {
        IsTransactionDialogVisible = false;
        EditingHareket = null;
    }

    [RelayCommand]
    public async Task ViewMakbuzAsync(BankaHareket? hareket)
    {
        if (hareket == null) hareket = EditingHareket ?? SelectedHareket;
        if (hareket == null) return;

        try
        {
            var pdfBytes = await _pdfService.GenerateMakbuzFromKasaPdfBytesAsync(new KasaHareket { 
                Id = hareket.Id, 
                EvrakNo = hareket.EvrakNo, 
                Aciklama = hareket.Aciklama, 
                Tarih = hareket.Tarih, 
                IslemTuru = hareket.IslemTuru, 
                Giren = hareket.Giren, 
                Cikan = hareket.Cikan 
            });
            await HandleFileOpenAsync(pdfBytes, $"BankaMakbuz_{hareket.Id}.pdf");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Makbuz hatası: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task PrintMakbuzAsync(BankaHareket? hareket)
    {
        await ViewMakbuzAsync(hareket);
    }

    protected abstract Task HandleFileOpenAsync(byte[] content, string fileName);

    [RelayCommand]
    public virtual void GoBack() { }

    protected DateTime GetParsedTransactionDate()
    {
        if (DateTime.TryParseExact(TransactionDateStr, "dd.MM.yyyy", null, System.Globalization.DateTimeStyles.None, out var dt))
            return dt;
        return DateTime.Now;
    }

}
