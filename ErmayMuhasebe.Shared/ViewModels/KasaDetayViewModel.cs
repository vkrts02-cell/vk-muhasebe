using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ErmayMuhasebe.Models;
using ErmayMuhasebe.Services;
using ErmayMuhasebe.Repositories;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;

namespace ErmayMuhasebe.Shared.ViewModels;

public abstract partial class KasaDetayViewModel : ViewModelBase
{
    public event Action? GoBackRequest;
    protected readonly IUnitOfWork _uow;
    protected readonly IPdfService _pdfService;
    protected BankaKart? _kasa;

    [ObservableProperty] private string _kasaAdi = "İsimsiz Kasa";
    [ObservableProperty] private decimal _guncelBakiye;
    [ObservableProperty] private string _dovizTuru = "TL";
    [ObservableProperty] private ObservableCollection<KasaHareket> _hareketler = new();
    [ObservableProperty] private KasaHareket? _selectedHareket;

    // Transaction Edit Dialog Properties
    [ObservableProperty] private bool _isTransactionDialogVisible;
    [ObservableProperty] private KasaHareket? _editingHareket;
    [ObservableProperty] private string _transactionTitle = "İşlem Düzenle";
    [ObservableProperty] private decimal _transactionAmount;
    [ObservableProperty] private DateTime _transactionDate = DateTime.Now;
    [ObservableProperty] private string _transactionDateStr = DateTime.Now.ToString("dd.MM.yyyy");
    [ObservableProperty] private string _transactionDescription = "";
    [ObservableProperty] private string _transactionType = "";
    [ObservableProperty] private bool _isReadOnly;
    [ObservableProperty] private bool _isEditMode;
    [ObservableProperty] private bool _isDetailMode;

    // New Transaction (Giriş/Çıkış) Properties
    [ObservableProperty] private bool _isNewTransactionMode;
    [ObservableProperty] private bool _isSaving;

    // Cari Selection for New Transaction
    [ObservableProperty] private string _newCariSearchText = "";
    [ObservableProperty] private ObservableCollection<CariKart> _newCariSearchResults = new();
    [ObservableProperty] private CariKart? _selectedNewCari;
    [ObservableProperty] private bool _isCariAssociation; // Whether to link to a cari

    // Filtering
    [ObservableProperty] private DateTime _filterBaslangicTarihi = DateTime.Now.AddMonths(-1);
    [ObservableProperty] private DateTime _filterBitisTarihi = DateTime.Now;
    [ObservableProperty] private string _filterText = "";

    partial void OnFilterBaslangicTarihiChanged(DateTime value) => _ = LoadHareketlerAsync();
    partial void OnFilterBitisTarihiChanged(DateTime value) => _ = LoadHareketlerAsync();
    partial void OnFilterTextChanged(string value) => _ = LoadHareketlerAsync();

    public KasaDetayViewModel(IUnitOfWork uow, IPdfService pdfService)
    {
        _uow = uow;
        _pdfService = pdfService;
    }

    public async Task InitializeAsync(int kasaId)
    {
        var kasa = await _uow.Bankalar.GetByIdAsync(kasaId); // Reuse GetBanka as Kasas are stored in same table
        if (kasa == null) return;
        await InitializeAsync(kasa);
    }

    public async Task InitializeAsync(BankaKart kasa)
    {
        _kasa = kasa;
        KasaAdi = kasa.BankaAdi ?? "İsimsiz Kasa";
        GuncelBakiye = kasa.AcilisBakiyesi; 
        DovizTuru = GetCleanDoviz(kasa.DovizTuru);
        
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
    public async Task LoadHareketlerAsync()
    {
        if (_kasa == null) return;
        try 
        {
            IsLoading = true;
            var list = await _uow.Kasalar.GetHareketlerAsync(_kasa.Id);
            
            await InvokeOnUIThreadAsync(() => 
            {
                var filtered = list.Where(h => h.Tarih.Date >= FilterBaslangicTarihi.Date && h.Tarih.Date <= FilterBitisTarihi.Date);
                
                if (!string.IsNullOrWhiteSpace(FilterText))
                {
                    filtered = filtered.Where(h => (h.Aciklama ?? "").Contains(FilterText, StringComparison.OrdinalIgnoreCase) || 
                                                 (h.EvrakNo ?? "").Contains(FilterText, StringComparison.OrdinalIgnoreCase) ||
                                                 (h.IslemTuru ?? "").Contains(FilterText, StringComparison.OrdinalIgnoreCase));
                }

                Hareketler = new ObservableCollection<KasaHareket>(filtered.OrderByDescending(h => h.Tarih));
                
                decimal bakiye = _kasa.AcilisBakiyesi;
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
    public async Task DeleteHareketAsync(KasaHareket? hareket)
    {
        try 
        {
            KasaHareket? target = hareket ?? SelectedHareket;
            if (target == null || target.Id == 0) return;

            int deletedCari = 0;
            if (!string.IsNullOrEmpty(target.EvrakNo))
            {
                deletedCari = await _uow.Cariler.DeleteHareketByEvrakNoAsync(target.EvrakNo);
            }

            int deletedRows = await _uow.Kasalar.DeleteAsync(target.Id);
            
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
    public void OpenEditTransactionDialog(KasaHareket? hareket)
    {
        if (hareket == null) hareket = SelectedHareket;
        if (hareket == null) return;

        IsReadOnly = false;
        IsEditMode = true;
        IsDetailMode = false;
        IsNewTransactionMode = false;
        SetupTransactionDialog(hareket, "İŞLEMİ DÜZENLE");
    }

    [RelayCommand]
    public void ViewTransactionDetail(KasaHareket? hareket)
    {
        if (hareket == null) hareket = SelectedHareket;
        if (hareket == null) return;

        IsReadOnly = true;
        IsEditMode = false;
        IsDetailMode = true;
        IsNewTransactionMode = false;
        SetupTransactionDialog(hareket, "İŞLEM DETAYLARI");
    }

    protected void SetupTransactionDialog(KasaHareket hareket, string title)
    {
        EditingHareket = hareket;
        TransactionAmount = hareket.Giren > 0 ? hareket.Giren : hareket.Cikan;
        TransactionDate = hareket.Tarih;
        TransactionDateStr = hareket.Tarih.ToString("dd.MM.yyyy");
        TransactionDescription = hareket.Aciklama ?? "";
        TransactionType = hareket.IslemTuru ?? "";
        TransactionTitle = $"{title}: {hareket.EvrakNo}";
        IsTransactionDialogVisible = true;
    }

    // === New Transaction (Giriş / Çıkış) ===

    [RelayCommand]
    public void OpenNewGirisDialog()
    {
        ResetNewTransaction();
        TransactionType = "Kasa Girişi";
        TransactionTitle = "KASA GİRİŞİ";
        IsNewTransactionMode = true;
        IsEditMode = false;
        IsDetailMode = false;
        IsReadOnly = false;
        IsTransactionDialogVisible = true;
    }

    [RelayCommand]
    public void OpenNewCikisDialog()
    {
        ResetNewTransaction();
        TransactionType = "Kasa Çıkışı";
        TransactionTitle = "KASA ÇIKIŞI";
        IsNewTransactionMode = true;
        IsEditMode = false;
        IsDetailMode = false;
        IsReadOnly = false;
        IsTransactionDialogVisible = true;
    }

    private void ResetNewTransaction()
    {
        TransactionAmount = 0;
        TransactionDescription = "";
        TransactionDate = DateTime.Now;
        TransactionDateStr = DateTime.Now.ToString("dd.MM.yyyy");
        SelectedNewCari = null;
        NewCariSearchText = "";
        IsCariAssociation = false;
        ErrorMessage = "";
        SuccessMessage = "";
    }

    private System.Threading.CancellationTokenSource? _newCariCts;

    partial void OnNewCariSearchTextChanged(string value)
    {
        _newCariCts?.Cancel();
        _newCariCts = new System.Threading.CancellationTokenSource();
        var token = _newCariCts.Token;

        Task.Delay(300, token).ContinueWith(t =>
        {
            if (!t.IsCanceled)
            {
                InvokeOnUIThreadAsync(async () => await SearchCariForNewTransactionAsync());
            }
        }, token);
    }

    private async Task SearchCariForNewTransactionAsync()
    {
        try
        {
            var all = await _uow.Cariler.GetAllAsync();
            var filtered = all.Where(c => !c.IsDeleted);

            if (!string.IsNullOrWhiteSpace(NewCariSearchText))
            {
                filtered = filtered.Where(c =>
                    (c.Unvan ?? "").Contains(NewCariSearchText, StringComparison.OrdinalIgnoreCase) ||
                    (c.CariKod ?? "").Contains(NewCariSearchText, StringComparison.OrdinalIgnoreCase));
            }

            await InvokeOnUIThreadAsync(() =>
            {
                NewCariSearchResults = new ObservableCollection<CariKart>(filtered.Take(30));
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Cari search error: {ex.Message}");
        }
    }

    [RelayCommand]
    public void SelectNewCari(CariKart? cari)
    {
        if (cari != null)
        {
            SelectedNewCari = cari;
            IsCariAssociation = true;
        }
    }

    [RelayCommand]
    public void ClearNewCari()
    {
        SelectedNewCari = null;
        IsCariAssociation = false;
    }

    [RelayCommand]
    public async Task SaveNewTransactionAsync()
    {
        if (IsSaving) return;
        if (_kasa == null) return;

        if (TransactionAmount <= 0)
        {
            ErrorMessage = "Tutar 0'dan büyük olmalıdır!";
            return;
        }

        IsSaving = true;
        try
        {
            var tarih = GetParsedTransactionDate();
            var evrakNo = "K-" + DateTime.Now.ToString("yyMMddHHmmss");
            bool isGiris = TransactionType == "Kasa Girişi";

            // 1. Create KasaHareket
            var kasaHareket = new KasaHareket
            {
                KasaId = _kasa.Id,
                Tarih = tarih,
                Aciklama = IsCariAssociation && SelectedNewCari != null
                    ? $"{SelectedNewCari.Unvan} - {TransactionDescription}"
                    : TransactionDescription,
                EvrakNo = evrakNo,
                IslemTuru = TransactionType,
                Giren = isGiris ? TransactionAmount : 0,
                Cikan = !isGiris ? TransactionAmount : 0,
                CariUnvan = SelectedNewCari?.Unvan
            };
            await _uow.Kasalar.SaveAsync(kasaHareket);

            // 2. Update Kasa Balance
            _kasa.GuncelBakiye += (kasaHareket.Giren - kasaHareket.Cikan);
            await _uow.Bankalar.SaveAsync(_kasa);

            // 3. If linked to Cari, create CariHareket
            if (IsCariAssociation && SelectedNewCari != null)
            {
                var cariHareket = new CariHareket
                {
                    CariId = SelectedNewCari.Id,
                    CariUnvan = SelectedNewCari.Unvan,
                    Tarih = tarih,
                    Aciklama = $"[Nakit] {TransactionDescription}".Trim(),
                    IslemTuru = isGiris ? "Tahsilat (Nakit)" : "Ödeme (Nakit)",
                    EvrakNo = evrakNo,
                    Alacak = isGiris ? TransactionAmount : 0,
                    Borc = !isGiris ? TransactionAmount : 0
                };
                await _uow.Cariler.SaveHareketAsync(cariHareket);
                await _uow.Cariler.RecalculateBalanceAsync(SelectedNewCari.Id);
            }

            IsTransactionDialogVisible = false;
            ResetNewTransaction();
            await LoadHareketlerAsync();
            NotifyFinancialDataChanged();
            SuccessMessage = isGiris ? "Kasa girişi kaydedildi." : "Kasa çıkışı kaydedildi.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"İşlem kaydedilemedi: {ex.Message}";
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    public async Task SaveTransactionAsync()
    {
        if (EditingHareket == null) return;

        DateTime parsedDate = GetParsedTransactionDate();
        EditingHareket.Tarih = parsedDate;
        EditingHareket.Aciklama = TransactionDescription;
        
        if (EditingHareket.Giren > 0) EditingHareket.Giren = TransactionAmount;
        else EditingHareket.Cikan = TransactionAmount;

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

        await _uow.Kasalar.SaveAsync(EditingHareket);
        IsTransactionDialogVisible = false;
        await LoadHareketlerAsync();
        NotifyFinancialDataChanged();
        SuccessMessage = "İşlem güncellendi.";
    }

    [RelayCommand]
    public void CloseTransactionDialog()
    {
        IsTransactionDialogVisible = false;
        EditingHareket = null;
        IsNewTransactionMode = false;
    }

    [RelayCommand]
    public async Task ViewMakbuzAsync(KasaHareket? hareket)
    {
        if (hareket == null) hareket = EditingHareket ?? SelectedHareket;
        if (hareket == null) return;

        try
        {
            var pdfBytes = await _pdfService.GenerateMakbuzFromKasaPdfBytesAsync(hareket);
            await HandleFileOpenAsync(pdfBytes, $"Makbuz_{hareket.Id}.pdf");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Makbuz hatası: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task PrintMakbuzAsync(KasaHareket? hareket)
    {
        await ViewMakbuzAsync(hareket);
    }

    [RelayCommand]
    public async Task GenerateFilteredReportAsync()
    {
        if (_kasa == null) return;
        try 
        {
            var pdfBytes = await _pdfService.GenerateKasaEkstrePdfBytesAsync(_kasa, Hareketler.ToList());
            await HandleFileOpenAsync(pdfBytes, $"KasaEkstre_{_kasa.BankaAdi}_{FilterBaslangicTarihi:ddMM}_{FilterBitisTarihi:ddMM}.pdf");
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    protected abstract Task HandleFileOpenAsync(byte[] content, string fileName);

    [RelayCommand]
    public virtual void GoBack() 
    {
        GoBackRequest?.Invoke();
    }

    protected DateTime GetParsedTransactionDate()
    {
        if (DateTime.TryParseExact(TransactionDateStr, "dd.MM.yyyy", null, System.Globalization.DateTimeStyles.None, out var dt))
            return dt;
        return DateTime.Now;
    }

    protected override Task InvokeOnUIThreadAsync(Action action)
    {
        action();
        return Task.CompletedTask;
    }
}

