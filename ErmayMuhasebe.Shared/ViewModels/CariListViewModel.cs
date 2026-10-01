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
using System.Linq.Expressions;

namespace ErmayMuhasebe.Shared.ViewModels;

public abstract partial class CariListViewModel : ViewModelBase
{
    protected readonly IUnitOfWork _uow;
    protected readonly IPdfService _pdfService;
    protected readonly IExcelService _excelService;
    protected readonly ExternalApiService _externalApi;
    protected readonly IFinansService _finansService;

    [ObservableProperty]
    private ObservableCollection<CariKart> _cariler = new();

    [ObservableProperty]
    private CariKart? _selectedCari;

    [ObservableProperty]
    private ObservableCollection<CariHareket> _cariHareketler = new();

    [ObservableProperty]
    private CariHareket? _selectedHareket;

    [ObservableProperty]
    private string _searchString = "";

    [ObservableProperty]
    private bool _isCariSelected;

    [ObservableProperty] private bool _isSelectAll;
    partial void OnIsSelectAllChanged(bool value)
    {
        foreach (var h in CariHareketler) h.IsSelected = value;
    }

    // Add/Edit Dialog Properties
    [ObservableProperty] private bool _isSaving;
    [ObservableProperty] private bool _isCariEkleVisible;
    [ObservableProperty] private string _dialogTitle = "Yeni Cari Kart";
    [ObservableProperty] private string _editUnvan = "";
    [ObservableProperty] private string _editCariKod = "";
    [ObservableProperty] private string _editGrup = "Müşteri";
    [ObservableProperty] private string _editVergiNo = "";
    [ObservableProperty] private string _editVergiDairesi = "";
    [ObservableProperty] private string _editTicaretSicilNo = "";
    [ObservableProperty] private string _editTelefon = "";
    [ObservableProperty] private string _editCepTelefon = "";
    [ObservableProperty] private string _editEmail = "";
    [ObservableProperty] private string _editWebAdresi = "";
    [ObservableProperty] private string _editAdres = "";
    [ObservableProperty] private string _editSevkAdresi = "";
    [ObservableProperty] private string _editIl = "";
    [ObservableProperty] private string _editIlce = "";
    [ObservableProperty] private string _editPostaKodu = "";
    [ObservableProperty] private string _editUlke = "Türkiye";
    [ObservableProperty] private string _editYetkili = "";
    [ObservableProperty] private decimal? _editVadeGunu = 30;
    [ObservableProperty] private decimal? _editRiskLimiti;
    [ObservableProperty] private string _editAciklama = "";
    [ObservableProperty] private decimal _editAcilisBakiye;
    [ObservableProperty] private string _editIBAN = "";
    [ObservableProperty] private double? _editLatitude;
    [ObservableProperty] private double? _editLongitude;
    [ObservableProperty] private bool _editRiskTakibiYapilsin;
    [ObservableProperty] private bool _editVadeGecmisteEngelle;
    [ObservableProperty] private bool _editFaturadaRiskKontrolu = true;
    [ObservableProperty] private string _editOdemePlani = "Peşin";
    [ObservableProperty] private string _editTCNo = "";
    [ObservableProperty] private bool _isOnlyWithBalance;

    // Transaction Dialog Properties
    [ObservableProperty] private bool _isTransactionDialogVisible;
    [ObservableProperty] private string _transactionTitle = "";
    [ObservableProperty] private string _transactionType = ""; 
    [ObservableProperty] private decimal? _transactionAmount;
    [ObservableProperty] private string _transactionDescription = "";
    [ObservableProperty] private DateTime? _transactionDate = DateTime.Now;
    [ObservableProperty] private string _transactionDateStr = DateTime.Now.ToString("dd.MM.yyyy");
    [ObservableProperty] private string _transactionMethod = "Nakit";
    [ObservableProperty] private ObservableCollection<BankaKart> _availableKasalar = new();
    [ObservableProperty] private BankaKart? _selectedKasaForTransaction;
    [ObservableProperty] private int _selectedKasaId;
    [ObservableProperty] private bool _isKasaSelectionVisible;
    [ObservableProperty] private bool _isKrediKarti;
    [ObservableProperty] private bool _isHavaleEFT;
    [ObservableProperty] private bool _isStandardTransaction = true;
    
    // Kredi Kartı & Havale / EFT Ortak / Benzer Alanları
    [ObservableProperty] private string _kkBanka = "";
    [ObservableProperty] private string _kkKartNo = ""; // EFT için IBAN / Hesap No
    [ObservableProperty] private string _kkOnayKodu = ""; // EFT için Dekont No
    [ObservableProperty] private string _kkSlipPath = ""; // EFT için Dekont Path
    [ObservableProperty] private string _kkDurum = "Portföyde";
    [ObservableProperty] private string _kkYonlendirilenTedarikci = "";
    [ObservableProperty] private int? _kkYonlendirilenTedarikciId;

    [ObservableProperty]
    private ObservableCollection<CariKart> _filteredCarilerForSelection = new();

    [ObservableProperty] private bool _isCariSelectionVisible;
    private bool _isSelectingCustomer;
    [ObservableProperty] private bool _isAcilisFisi;
    [ObservableProperty] private bool _isEkstreOptionVisible;
    [ObservableProperty] private string _ekstreTitle = "Hesap Ekstresi";
    [ObservableProperty] private bool _isDetayliEkstre;
    [ObservableProperty] private string _statusMessage = "";
    
    protected CariHareket? _editingHareket;
    protected byte[]? _pendingPdfBytes;
    protected string _pendingPdfName = "";

    // Pagination Properties
    [ObservableProperty] private int _pageSize = 100;
    [ObservableProperty] private int _currentPageIndex = 0;
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private bool _canNextPage;
    [ObservableProperty] private bool _canPreviousPage;
    [ObservableProperty] private string _pagerInfo = "1 / 1 (0)";

    // ViewModels
    [ObservableProperty] private CekSenetListViewModel _checkHelper;

    // Statistics
    [ObservableProperty] private decimal _toplamBorc;
    [ObservableProperty] private decimal _toplamAlacak;
    [ObservableProperty] private decimal _bakiye;
    [ObservableProperty] private decimal _müşteriBakiye;
    [ObservableProperty] private decimal _tedarikçiBakiye;

    // Sorting Properties
    [ObservableProperty] private bool _isBakiyeSortActive;
    [ObservableProperty] private bool _isBakiyeDescending = true;
    [ObservableProperty] private string _bakiyeSortIcon = "ArrowSort";

    [RelayCommand]
    public async Task SortByBakiyeAsync()
    {
        if (!IsBakiyeSortActive)
        {
            IsBakiyeSortActive = true;
            IsBakiyeDescending = true; // İlk tıklandığında KESİNLİKLE en yüksek bakiyeden (+ dan - ye)
        }
        else
        {
            IsBakiyeDescending = !IsBakiyeDescending;
        }

        BakiyeSortIcon = IsBakiyeDescending ? "ArrowDown" : "ArrowUp";

        // Mevcut cariler varsa anında hafızada sırala
        if (Cariler != null && Cariler.Count > 0)
        {
            var inMemorySorted = IsBakiyeDescending 
                ? Cariler.OrderByDescending(c => c.Bakiye).ThenBy(c => c.Unvan).ToList()
                : Cariler.OrderBy(c => c.Bakiye).ThenBy(c => c.Unvan).ToList();
            Cariler = new ObservableCollection<CariKart>(inMemorySorted);
        }

        await LoadCarilerAsync(isSilent: true);
    }

    public CariListViewModel(IUnitOfWork uow, IPdfService pdfService, IExcelService excelService, CekSenetListViewModel checkHelper, ExternalApiService externalApi, IFinansService finansService)
    {
        _uow = uow;
        _pdfService = pdfService;
        _excelService = excelService;
        _externalApi = externalApi;
        _finansService = finansService;
        CheckHelper = checkHelper;
        
        WeakReferenceMessenger.Default.Register<CekSenetListViewModel.TransactionMethodChangedMessage>(this, (r, m) =>
        {
            InvokeOnUIThreadAsync(() => {
                if (CheckHelper != null)
                {
                    TransactionAmount = CheckHelper.EditTutar;
                }
                TransactionMethod = m.Value;
                IsTransactionDialogVisible = true;
            });
        });

        WeakReferenceMessenger.Default.Register<CekSenetListViewModel.TransactionSavedMessage>(this, (r, m) =>
        {
            InvokeOnUIThreadAsync(async () => {
                if (SelectedCari != null)
                {
                    await LoadHareketlerAsync(SelectedCari.Id);
                }
                await LoadCarilerAsync();
                NotifyFinancialDataChanged();
            });
        });

        _ = LoadCarilerAsync();
    }

    [RelayCommand]
    public void ClearStatusMessages()
    {
        ErrorMessage = "";
        StatusMessage = "";
        SuccessMessage = "";
    }

    public override void OnNavigatedTo()
    {
        _ = LoadCarilerAsync();
        if (SelectedCari != null)
             _ = LoadHareketlerAsync(SelectedCari.Id);
    }

    [RelayCommand]
    public async Task NextPageAsync()
    {
        if (CurrentPageIndex * PageSize + PageSize < TotalCount)
        {
            CurrentPageIndex++;
            await LoadCarilerAsync();
        }
    }

    [RelayCommand]
    public async Task PreviousPageAsync()
    {
        if (CurrentPageIndex > 0)
        {
            CurrentPageIndex--;
            await LoadCarilerAsync();
        }
    }

    private System.Threading.CancellationTokenSource? _searchCts;

    partial void OnSearchStringChanged(string value)
    {
        _searchCts?.Cancel();
        _searchCts = new System.Threading.CancellationTokenSource();
        var token = _searchCts.Token;

        Task.Delay(500, token).ContinueWith(t => 
        {
            if (!t.IsCanceled)
            {
                InvokeOnUIThreadAsync(async () => 
                {
                    CurrentPageIndex = 0;
                    await LoadCarilerAsync();
                });
            }
        }, token);
    }

    [RelayCommand]
    public async Task LoadCarilerAsync(bool isSilent = false)
    {
        try
        {
            if (!isSilent) IsLoading = true;
            var selectedId = SelectedCari?.Id;

            Expression<Func<CariKart, bool>>? filter = null;
            if (!string.IsNullOrEmpty(SearchString))
            {
                if (IsOnlyWithBalance)
                    filter = c => !c.IsDeleted && (c.Borc != c.Alacak) &&
                        ((c.Unvan != null && c.Unvan.Contains(SearchString)) || 
                         (c.CariKod != null && c.CariKod.Contains(SearchString)));
                else
                    filter = c => !c.IsDeleted && 
                        ((c.Unvan != null && c.Unvan.Contains(SearchString)) || 
                         (c.CariKod != null && c.CariKod.Contains(SearchString)));
            }
            else
            {
                if (IsOnlyWithBalance)
                    filter = c => !c.IsDeleted && (c.Borc != c.Alacak);
                else
                    filter = c => !c.IsDeleted;
            }

            List<CariKart> pagedList;
            if (IsBakiyeSortActive)
            {
                var allList = await _uow.Cariler.GetPagedAsync(0, 100000, filter);
                TotalCount = allList.Count;
                var sorted = IsBakiyeDescending 
                    ? allList.OrderByDescending(c => c.Bakiye).ThenBy(c => c.Unvan) 
                    : allList.OrderBy(c => c.Bakiye).ThenBy(c => c.Unvan);
                pagedList = sorted.Skip(CurrentPageIndex * PageSize).Take(PageSize).ToList();
            }
            else
            {
                TotalCount = await _uow.Cariler.GetCountAsync(filter);
                pagedList = await _uow.Cariler.GetPagedAsync(CurrentPageIndex * PageSize, PageSize, filter, c => c.Unvan!);
            }
            
            // Global Summary
            var summary = await _uow.Cariler.GetGlobalSummaryAsync(SearchString);
            
            await InvokeOnUIThreadAsync(() => 
            {
                Cariler = new ObservableCollection<CariKart>(pagedList);
                
                // Pager logic
                int totalPages = (int)Math.Ceiling((double)TotalCount / PageSize);
                if (totalPages == 0) totalPages = 1;
                PagerInfo = $"{CurrentPageIndex + 1} / {totalPages} ({TotalCount})";
                CanNextPage = (CurrentPageIndex + 1) < totalPages;
                CanPreviousPage = CurrentPageIndex > 0;

                // Update totals from global summary
                ToplamBorc = summary.ToplamBorc;
                ToplamAlacak = summary.ToplamAlacak;
                Bakiye = summary.ToplamBorc - summary.ToplamAlacak;
                MüşteriBakiye = summary.MusteriBakiye;
                TedarikçiBakiye = summary.TedarikciBakiye;

                if (selectedId.HasValue)
                {
                    SelectedCari = Cariler.FirstOrDefault(c => c.Id == selectedId.Value);
                }
            });
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Hata: {ex.Message}";
        }
        finally
        {
            if (!isSilent) IsLoading = false;
        }
    }

    partial void OnSelectedCariChanged(CariKart? value)
    {
        IsCariSelected = value != null;
        if (value != null)
        {
            _ = LoadHareketlerAsync(value.Id);
        }
        else
        {
            CariHareketler.Clear();
        }
    }

    public async Task LoadHareketlerAsync(int cariId)
    {
        var hareketler = await _uow.Cariler.GetHareketlerAsync(cariId);
        
        // Calculate Running Balance (KalanBakiye) chronologically
        var sorted = hareketler.OrderBy(h => h.Tarih).ThenBy(h => h.Id).ToList();
        decimal balance = 0;
        foreach (var h in sorted)
        {
            balance += (h.Borc - h.Alacak);
            h.KalanBakiye = balance;
        }

        await InvokeOnUIThreadAsync(() => 
        {
            CariHareketler = new ObservableCollection<CariHareket>(sorted.OrderByDescending(h => h.Tarih).ThenByDescending(h => h.Id));
        });
    }

    [RelayCommand]
    public void Search() => _ = LoadCarilerAsync();



    // ============ CARI CRUD OPERATIONS ============
    
    [RelayCommand]
    public void OpenAddCariDialog()
    {
        ClearStatusMessages();
        SelectedCari = null;
        DialogTitle = "Yeni Cari Kart";
        ClearEditForm();
        
        int nextId = (Cariler?.Count ?? 0) + 1;
        EditCariKod = $"C-{nextId:D4}";
        
        IsCariEkleVisible = true;
    }

    [RelayCommand]
    public void OpenEditCariDialog()
    {
        ClearStatusMessages();
        if (SelectedCari == null) return;
        DialogTitle = "Cari Düzenle";
        FillEditForm(SelectedCari);
        IsCariEkleVisible = true;
    }

    [RelayCommand]
    public void CloseDialog()
    {
        IsCariEkleVisible = false;
        ClearEditForm();
        ClearStatusMessages();
    }

    [RelayCommand]
    public async Task ValidateVatAsync()
    {
        ClearStatusMessages();
        if (string.IsNullOrEmpty(EditVergiNo)) { ErrorMessage = "Lütfen bir Vergi No giriniz."; return; }
        
        StatusMessage = "Doğrulanıyor...";
        bool result = EditVergiNo.Length >= 10;
        StatusMessage = "";
        
        if (result) 
            SuccessMessage = "Vergi Numarası formatı geçerli.";
        else 
            ErrorMessage = "Vergi Numarası Geçersiz (En az 10 hane olmalı).";

        await Task.CompletedTask;
    }


    [RelayCommand]
    public async Task ValidateEmailAsync()
    {
        ClearStatusMessages();
        if (string.IsNullOrEmpty(EditEmail)) { ErrorMessage = "E-posta alanı boş olamaz."; return; }
        
        StatusMessage = "E-posta doğrulanıyor...";
        bool isValid = await _externalApi.ValidateEmailAsync(EditEmail);
        StatusMessage = "";
        
        if (isValid) SuccessMessage = "E-posta Geçerli.";
        else ErrorMessage = "E-posta Geçersiz veya Ulaşılamaz.";
    }

    [RelayCommand]
    public void ValidatePhone()
    {
        ClearStatusMessages();
        if (string.IsNullOrEmpty(EditTelefon)) { ErrorMessage = "Telefon numarası giriniz."; return; }
        
        bool isValid = _externalApi.ValidatePhone(EditTelefon);
        if (isValid) SuccessMessage = "Telefon Formatı Geçerli.";
        else ErrorMessage = "Telefon Formatı Geçersiz.";
    }

    [RelayCommand]
    public void ValidateIban()
    {
        ClearStatusMessages();
        if (string.IsNullOrEmpty(EditIBAN)) { ErrorMessage = "IBAN alanı boş olamaz."; return; }
        
        bool isValid = _externalApi.ValidateIban(EditIBAN);
        if (isValid) SuccessMessage = "IBAN Doğrulandı.";
        else ErrorMessage = "IBAN Hatalı!";
    }

    [RelayCommand]
    public async Task FetchCoordinatesAsync()
    {
        ClearStatusMessages();
        if (string.IsNullOrWhiteSpace(EditAdres) && string.IsNullOrWhiteSpace(EditIl)) 
        { 
            ErrorMessage = "Lütfen önce bir adres veya şehir giriniz."; 
            return; 
        }

        string fullAddress = $"{EditAdres} {EditIlce} {EditIl} {EditUlke}";
        StatusMessage = "Koordinatlar alınıyor...";
        
        var coords = await _externalApi.GetCoordinatesAsync(fullAddress);
        StatusMessage = "";
        
        if (coords.HasValue)
        {
            EditLatitude = coords.Value.lat;
            EditLongitude = coords.Value.lon;
            SuccessMessage = $"Konum başarıyla çekildi: {EditLatitude}, {EditLongitude}";
        }
        else ErrorMessage = "Konum bulunamadı veya API anahtarı geçersiz/eksik.";
    }

    [RelayCommand]
    public async Task SaveCariAsync()
    {
        if (string.IsNullOrWhiteSpace(EditUnvan)) 
        {
            ErrorMessage = "Cari ünvanı boş olamaz!";
            return;
        }

        if ((EditVadeGunu ?? 0) < 0)
        {
            ErrorMessage = "Vade günü negatif olamaz!";
            return;
        }

        if ((EditRiskLimiti ?? 0) < 0)
        {
            ErrorMessage = "Risk limiti negatif olamaz!";
            return;
        }

        // TC No Validation
        if (!string.IsNullOrWhiteSpace(EditTCNo) && EditTCNo.Length != 11)
        {
            ErrorMessage = "TC Kimlik Numarası 11 hane olmalıdır!";
            return;
        }

        // Duplicate Vergi No Check
        if (!string.IsNullOrWhiteSpace(EditVergiNo))
        {
            var allCaris = await _uow.Cariler.GetAllAsync();
            var duplicate = allCaris.FirstOrDefault(c => c.VergiNo == EditVergiNo && !c.IsDeleted && (SelectedCari == null || c.Id != SelectedCari.Id));
            if (duplicate != null)
            {
                ErrorMessage = $"Bu Vergi Numarası ({EditVergiNo}) zaten {duplicate.Unvan} kaydında tanımlı!";
                return;
            }
        }

        IsLoading = true;
        try
        {
            if (SelectedCari != null)
            {
                SetCariFromEdit(SelectedCari);
                await _uow.Cariler.SaveAsync(SelectedCari);
            }
            else
            {
                var newCari = new CariKart
                {
                    Borc = EditAcilisBakiye > 0 ? EditAcilisBakiye : 0,
                    Alacak = EditAcilisBakiye < 0 ? Math.Abs(EditAcilisBakiye) : 0,
                    KayitTarihi = DateTime.Now
                };
                SetCariFromEdit(newCari);
                
                await _uow.Cariler.SaveAsync(newCari);
                
                if (EditAcilisBakiye != 0)
                {
                    await _uow.Cariler.SaveHareketAsync(new CariHareket
                    {
                        CariId = newCari.Id,
                        CariUnvan = newCari.Unvan ?? "",
                        Tarih = DateTime.Now,
                        IslemTuru = "Açılış",
                        Aciklama = "Açılış Bakiyesi",
                        Borc = newCari.Borc,
                        Alacak = newCari.Alacak
                    });
                }
            }

            IsCariEkleVisible = false;
            ClearEditForm();
            await LoadCarilerAsync();
            SuccessMessage = "Cari kart kaydedildi.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Hata: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    protected void SetCariFromEdit(CariKart cari)
    {
        cari.Unvan = EditUnvan;
        cari.CariKod = EditCariKod;
        cari.Grup = EditGrup;
        cari.Tur = (EditGrup == "Tedarikçi") ? "Satici" : "Alici";
        cari.VergiNo = EditVergiNo;
        cari.VergiDairesi = EditVergiDairesi;
        cari.TicaretSicilNo = EditTicaretSicilNo;
        cari.Telefon = EditTelefon;
        cari.CepTelefon = EditCepTelefon;
        cari.Email = EditEmail;
        cari.WebAdresi = EditWebAdresi;
        cari.Adres = EditAdres;
        cari.SevkAdresi = EditSevkAdresi;
        cari.Il = EditIl;
        cari.Ilce = EditIlce;
        cari.PostaKodu = EditPostaKodu;
        cari.Ulke = EditUlke;
        cari.Yetkili = EditYetkili;
        cari.VadeGunu = (int)(EditVadeGunu ?? 0);
        cari.RiskLimiti = EditRiskLimiti ?? 0;
        cari.Aciklama = EditAciklama;
        cari.IBAN = EditIBAN;
        cari.Latitude = EditLatitude;
        cari.Longitude = EditLongitude;
        cari.RiskTakibiYapilsin = EditRiskTakibiYapilsin;
        cari.VadeGecmisteEngelle = EditVadeGecmisteEngelle;
        cari.FaturadaRiskKontrolu = EditFaturadaRiskKontrolu;
        cari.OdemePlani = EditOdemePlani;
        cari.TCNo = EditTCNo;
    }


    partial void OnIsOnlyWithBalanceChanged(bool value)
    {
        _ = LoadCarilerAsync();
    }

    protected void ClearEditForm()
    {
        EditUnvan = "";
        EditCariKod = "";
        EditGrup = "Müşteri";
        EditVergiNo = "";
        EditVergiDairesi = "";
        EditTicaretSicilNo = "";
        EditTelefon = "";
        EditCepTelefon = "";
        EditEmail = "";
        EditWebAdresi = "";
        EditAdres = "";
        EditSevkAdresi = "";
        EditIl = "";
        EditIlce = "";
        EditPostaKodu = "";
        EditUlke = "Türkiye";
        EditYetkili = "";
        EditVadeGunu = 30;
        EditRiskLimiti = 0;
        EditAciklama = "";
        EditAcilisBakiye = 0;
        EditIBAN = "";
        EditLatitude = null;
        EditLongitude = null;
        EditRiskTakibiYapilsin = false;
        EditVadeGecmisteEngelle = false;
        EditFaturadaRiskKontrolu = true;
        EditOdemePlani = "Peşin";
        EditTCNo = "";
    }

    protected void FillEditForm(CariKart cari)
    {
        EditUnvan = cari.Unvan ?? "";
        EditCariKod = cari.CariKod ?? "";
        EditGrup = cari.Grup ?? "";
        EditVergiNo = cari.VergiNo ?? "";
        EditVergiDairesi = cari.VergiDairesi ?? "";
        EditTicaretSicilNo = cari.TicaretSicilNo ?? "";
        EditTelefon = cari.Telefon ?? "";
        EditCepTelefon = cari.CepTelefon ?? "";
        EditEmail = cari.Email ?? "";
        EditWebAdresi = cari.WebAdresi ?? "";
        EditAdres = cari.Adres ?? "";
        EditSevkAdresi = cari.SevkAdresi ?? "";
        EditIl = cari.Il ?? "";
        EditIlce = cari.Ilce ?? "";
        EditPostaKodu = cari.PostaKodu ?? "";
        EditUlke = cari.Ulke ?? "Türkiye";
        EditYetkili = cari.Yetkili ?? "";
        EditVadeGunu = cari.VadeGunu;
        EditRiskLimiti = cari.RiskLimiti;
        EditAciklama = cari.Aciklama ?? "";
        EditAcilisBakiye = 0;
        EditIBAN = cari.IBAN ?? "";
        EditLatitude = cari.Latitude;
        EditLongitude = cari.Longitude;
        EditRiskTakibiYapilsin = cari.RiskTakibiYapilsin;
        EditVadeGecmisteEngelle = cari.VadeGecmisteEngelle;
        EditFaturadaRiskKontrolu = cari.FaturadaRiskKontrolu;
        EditOdemePlani = cari.OdemePlani ?? "Peşin";
        EditTCNo = cari.TCNo ?? "";
    }


    [RelayCommand]
    public void DeleteCariConfirm()
    {
        if (SelectedCari == null) return;
        ShowConfirm("Cari Kartı Sil", $"{SelectedCari.Unvan} adlı cari kartı silmek istediğinize emin misiniz?", DeleteCariAsync);
    }

    public async Task DeleteCariAsync()
    {
        if (SelectedCari == null) return;
        try 
        {
            var hareketler = await _uow.Cariler.GetHareketlerAsync(SelectedCari.Id);
            if (hareketler.Any(h => h.Borc != 0 || h.Alacak != 0))
            {
                ErrorMessage = "Hareket görmüş bir cariyi silemezsiniz! Lütfen önce hareketleri silin veya başka bir cariyle birleştirin.";
                return;
            }

            var toDelete = SelectedCari;
            SelectedCari = null;
            await _uow.Cariler.DeleteAsync(toDelete);
            await LoadCarilerAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Hata: {ex.Message}";
        }
    }

    // ============ TRANSACTION OPERATIONS ============

    [RelayCommand]
    public void OpenTransactionDialog(string type)
    {
         if (SelectedCari == null) return;
         _editingHareket = null;
         TransactionType = type;
          ErrorMessage = "";
          SuccessMessage = "";
          WarningMessage = "";
          StatusMessage = "";
         TransactionTitle = $"{type} İşlemi - {SelectedCari.Unvan}";
         TransactionAmount = 0;
         TransactionDescription = "";
         TransactionDate = DateTime.Now;
         TransactionDateStr = DateTime.Now.ToString("dd.MM.yyyy");
         TransactionMethod = "Nakit";
         
         KkBanka = "";
         KkKartNo = "";
         KkOnayKodu = "";
         KkSlipPath = "";
         KkDurum = "Portföyde";
         KkYonlendirilenTedarikci = "";
         KkYonlendirilenTedarikciId = null;
         IsAcilisFisi = false;
         
         _ = LoadAvailableKasalarAsync("Nakit");
         IsTransactionDialogVisible = true;
    }

    partial void OnTransactionMethodChanged(string value)
    {
        IsKrediKarti = value == "Kredi Kartı";
        IsHavaleEFT = value == "Havale / EFT" || value == "Havale/EFT";
        IsStandardTransaction = value == "Nakit"; 
        
        IsKasaSelectionVisible = value == "Nakit" || value == "Havale / EFT" || value == "Havale/EFT" || value == "Kredi Kartı";
        
        _ = LoadAvailableKasalarAsync(value);
        
        if (value == "Çek")
        {
            if (!CheckHelper.IsEditFormVisible)
            {
                OpenNewCheckFromCari();
            }
        }
        else
        {
            // If we were in Check mode (CheckHelper visible), close it and show standard dialog
            if (CheckHelper != null && CheckHelper.IsEditFormVisible)
            {
                // Preserve the amount if it was changed in the check form
                TransactionAmount = CheckHelper.EditTutar;
                CheckHelper.CloseEditForm();
                IsTransactionDialogVisible = true;
            }
            // If neither is visible, we might be switching methods within the main dialog itself
        }
    }

    protected void OpenNewCheckFromCari()
    {
        if (SelectedCari == null) return;
        IsTransactionDialogVisible = false;
        
        CheckHelper.OpenNewCekForm();
        CheckHelper.EditPortfoyNo = "CK-" + DateTime.Now.ToString("yyMMddHHmmss");
        CheckHelper.EditCariId = SelectedCari.Id;
        CheckHelper.EditCariUnvan = SelectedCari.Unvan;
        CheckHelper.EditAsilBorclu = SelectedCari.Unvan;
        CheckHelper.EditTutar = TransactionAmount ?? 0;
        CheckHelper.EditCekTuru = TransactionType == "Tahsilat" ? "Alınan" : "Verilen";
        CheckHelper.EditIslemTuru = TransactionType;
        CheckHelper.SelectedCariGrup = SelectedCari.Grup;
        CheckHelper.EditTitle = $"{TransactionType} - Yeni Çek Kaydı";
        CheckHelper.IsEditFormVisible = true;
    }

    protected async Task LoadAvailableKasalarAsync(string? method = null)
    {
        if (method == null) method = TransactionMethod;
        var list = await _uow.Bankalar.GetAllAsync();
        List<BankaKart> filtered;

        if (method == "Nakit") filtered = list.Where(x => x.KartTuru == "Kasa").ToList();
        else if (method == "Havale / EFT" || method == "Havale/EFT") filtered = list.Where(x => x.KartTuru != "Kasa").ToList();
        else filtered = list;

        // Fallback: If no accounts found for specific type, show all
        if (!filtered.Any()) filtered = list;

        AvailableKasalar = new ObservableCollection<BankaKart>(filtered);
        
        // Auto-select first or maintain selection
        if (SelectedKasaForTransaction != null && AvailableKasalar.Any(k => k.Id == SelectedKasaForTransaction.Id))
        {
             // Already valid
             SelectedKasaId = SelectedKasaForTransaction.Id;
        }
        else
        {
             SelectedKasaForTransaction = AvailableKasalar.FirstOrDefault();
             SelectedKasaId = SelectedKasaForTransaction?.Id ?? 0;
        }
    }

    partial void OnSelectedKasaIdChanged(int value)
    {
        if (value > 0)
        {
            var k = AvailableKasalar.FirstOrDefault(x => x.Id == value);
            if (k != null) SelectedKasaForTransaction = k;
        }
    }

    [RelayCommand]
    public void CloseTransactionDialog()
    {
        IsTransactionDialogVisible = false;
        _editingHareket = null;
    }



    [RelayCommand]
    public async Task SaveTransactionAsync()
    {
         if (IsSaving) return;
         if (SelectedCari == null) return;
         try { IsSaving = true;

         if (TransactionAmount == null || TransactionAmount <= 0)
         {
             ErrorMessage = "İşlem tutarı 0'dan büyük olmalıdır!";
             return;
         }

         if (TransactionDate == null)
         {
             ErrorMessage = "İşlem tarihi geçersiz!";
             return;
         }

         // RISK LIMIT CHECK
         if (SelectedCari.RiskTakibiYapilsin || SelectedCari.FaturadaRiskKontrolu)
         {
              var freshCari = await _uow.Cariler.GetByIdAsync(SelectedCari.Id);
              if (freshCari != null)
              {
                   decimal currentBalance = freshCari.Borc - freshCari.Alacak;
                   if (_editingHareket != null)
                   {
                        currentBalance -= _editingHareket.Borc;
                        currentBalance += _editingHareket.Alacak;
                   }
                   
                   bool isSupplier = (freshCari.Tur ?? "").Contains("Sat", StringComparison.OrdinalIgnoreCase) || 
                                    (freshCari.Tur ?? "").Contains("Tedar", StringComparison.OrdinalIgnoreCase) ||
                                    (freshCari.Grup ?? "").Contains("Sat", StringComparison.OrdinalIgnoreCase) ||
                                    (freshCari.Grup ?? "").Contains("Tedar", StringComparison.OrdinalIgnoreCase);

                   decimal amt = TransactionAmount ?? 0;
                   decimal newImpact = (TransactionType == "Tahsilat" || TransactionType == "Alacak Dekontu") ? -amt : amt;
                   decimal projected = currentBalance + newImpact;
                   
                    bool isLimitExceeded = (!isSupplier && projected > freshCari.RiskLimiti) || (isSupplier && (-projected) > freshCari.RiskLimiti);
                    bool isRiskReducing = (!isSupplier && newImpact < 0) || (isSupplier && newImpact > 0);

                    if (isLimitExceeded && !isRiskReducing)
                    {
                        if (freshCari.FaturadaRiskKontrolu)
                        {
                            ErrorMessage = $"RİSK LİMİTİ AŞILDI! İşlem engellendi.\nLimit: {freshCari.RiskLimiti:N2}, Tahmin: {(isSupplier ? -projected : projected):N2}";
                            return;
                        }
                        else if (freshCari.RiskTakibiYapilsin)
                        {
                            WarningMessage = $"DİKKAT: Risk Limiti aşıldı! (Limit: {freshCari.RiskLimiti:N2})";
                        }
                    }
              }
         }

         if (IsKasaSelectionVisible && SelectedKasaForTransaction == null)
         {
             ErrorMessage = "İşlem hesabı seçilmelidir!";
             return;
         }

         CariKart? directedSupplier = null;
         if (KkYonlendirilenTedarikciId.HasValue)
         {
             directedSupplier = await _uow.Cariler.GetByIdAsync(KkYonlendirilenTedarikciId.Value);
         }

         var request = new FinancialTransactionRequest
         {
             Cari = SelectedCari,
             TransactionType = TransactionType,
             Amount = TransactionAmount ?? 0,
             Method = TransactionMethod,
             Description = TransactionDescription,
             Date = GetParsedTransactionDate(),
             SelectedKasaOrBanka = SelectedKasaForTransaction,
             DirectedSupplier = directedSupplier,
             YonlendirmeTarihi = GetParsedTransactionDate(),
             BankaAdi = KkBanka,
             KartHesapNo = KkKartNo,
             OnayDekontNo = KkOnayKodu,
             SlipDekontPath = KkSlipPath,
             ExistingHareketId = _editingHareket?.Id ?? 0,
             ExistingEvrakNo = _editingHareket?.EvrakNo,
             ExistingRefId = _editingHareket?.RefId
         };

         int? editedId = _editingHareket?.Id;
         var result = await _finansService.SaveTransactionAsync(request);

         if (result)
         {
             IsTransactionDialogVisible = false;
             _editingHareket = null;
             NotifyFinancialDataChanged();
             await LoadCarilerAsync(); 
             if (SelectedCari != null) 
             {
                 await LoadHareketlerAsync(SelectedCari.Id);
                 if (editedId.HasValue)
                 {
                     SelectedHareket = CariHareketler.FirstOrDefault(h => h.Id == editedId.Value);
                 }
             }
         }
         else
         {
             ErrorMessage = "İşlem kaydedilirken bir hata oluştu!";
         }
         }
         finally { IsSaving = false; }
    }

    [RelayCommand]
    public void OpenTedarikciSecimDialog()
    {
        _isSelectingCustomer = false;
        FilteredCarilerForSelection = new ObservableCollection<CariKart>(
            Cariler.Where(c => 
                (c.Tur?.ToLower() ?? "").Contains("sat") || 
                (c.Tur?.ToLower() ?? "").Contains("tedar") ||
                (c.Grup?.ToLower() ?? "").Contains("sat") ||
                (c.Grup?.ToLower() ?? "").Contains("tedar")
            ));
        IsCariSelectionVisible = true;
    }

    [RelayCommand]
    public void OpenMusteriSecimDialog()
    {
        _isSelectingCustomer = true;
        FilteredCarilerForSelection = new ObservableCollection<CariKart>(
            Cariler.Where(c => 
                (c.Tur?.ToLower() ?? "").Contains("al") || 
                (c.Tur?.ToLower() ?? "").Contains("mus") ||
                (c.Tur?.ToLower() ?? "").Contains("müş") ||
                (c.Grup?.ToLower() ?? "").Contains("al") || 
                (c.Grup?.ToLower() ?? "").Contains("mus") ||
                (c.Grup?.ToLower() ?? "").Contains("müş")
            ));
        IsCariSelectionVisible = true; 
    }

    [RelayCommand]
    public void CloseCariSelection()
    {
        IsCariSelectionVisible = false;
    }

    [RelayCommand]
    public void SelectCariForTransaction(CariKart cari)
    {
        if (cari != null)
        {
            if (_isSelectingCustomer)
            {
                SelectedCari = cari;
            }
            else
            {
                if (TransactionMethod == "Çek" && CheckHelper != null)
                {
                    CheckHelper.EditYonlendirilenCariId = cari.Id;
                    CheckHelper.EditYonlendirilenCariUnvan = cari.Unvan ?? "";
                }
                else
                {
                    KkYonlendirilenTedarikciId = cari.Id;
                    KkYonlendirilenTedarikci = cari.Unvan ?? "";
                    KkDurum = "Tedarikçiye Verildi";
                }
            }
        }
        IsCariSelectionVisible = false;
    }

    protected abstract void NotifyFinancialDataChanged();

    [RelayCommand]
    public void TahsilatYap() => OpenTransactionDialog("Tahsilat");

    [RelayCommand]
    public void OdemeYap() => OpenTransactionDialog("Ödeme");

    [RelayCommand]
    public void Alacaklandir() => OpenTransactionDialog("Alacak Dekontu");

    [RelayCommand]
    public void Borclandir() => OpenTransactionDialog("Borç Dekontu");

    // ============ REPORT OPERATIONS ============

    [RelayCommand]
    public virtual async Task HesapEkstresiAsync(CariKart? cari = null)
    {
        if (cari != null) SelectedCari = cari;
        if (SelectedCari == null) return;
        try 
        {
            StatusMessage = "Hesap ekstresi hazırlanıyor...";
            var hareketler = await _uow.Cariler.GetHareketlerAsync(SelectedCari.Id);
            _pendingPdfBytes = await _pdfService.GenerateCariEkstrePdfBytesAsync(SelectedCari, hareketler);
            _pendingPdfName = $"Ekstre_{SelectedCari.Unvan}_{DateTime.Now:ddMMyyyy}.pdf";
            EkstreTitle = "Hesap Ekstresi";
            IsDetayliEkstre = false;
            IsEkstreOptionVisible = true;
            StatusMessage = "";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Rapor hatası: {ex.Message}";
            StatusMessage = "";
        }
    }

    [RelayCommand]
    public virtual async Task DetayliHesapEkstresiAsync(CariKart? cari = null)
    {
        if (cari != null) SelectedCari = cari;
        if (SelectedCari == null) return;
        try 
        {
            IsLoading = true;
            StatusMessage = "Detaylı ekstre hazırlanıyor, bu işlem fatura sayısına göre zaman alabilir...";
            var hareketler = (await _uow.Cariler.GetHareketlerAsync(SelectedCari.Id)).OrderBy(h => h.Tarih).ToList();
            var detayDictionary = new Dictionary<int, List<FaturaDetay>>();

            foreach (var h in hareketler)
            {
                // Check if it's a Fatura transaction
                if (!string.IsNullOrEmpty(h.IslemTuru) && h.IslemTuru.Contains("Fatura", StringComparison.OrdinalIgnoreCase))
                {
                    int? targetFaturaId = h.FaturaId;

                    // Fallback: If FaturaId is null, try to find by EvrakNo
                    if (!targetFaturaId.HasValue && !string.IsNullOrEmpty(h.EvrakNo))
                    {
                         var fatura = await _uow.Faturalar.GetByNoAsync(h.EvrakNo);
                         if (fatura != null) 
                         {
                             targetFaturaId = fatura.Id;
                             h.FaturaId = fatura.Id; // Temporarily link in memory for PDF generation
                         }
                    }

                    // If we have an ID now, fetch and cache details
                    if (targetFaturaId.HasValue)
                    {
                         if (!detayDictionary.ContainsKey(targetFaturaId.Value))
                         {
                             var details = await _uow.Faturalar.GetDetaylarAsync(targetFaturaId.Value);
                             if(details != null)
                                detayDictionary.Add(targetFaturaId.Value, details);
                         }
                    }
                }
            }

            _pendingPdfBytes = await _pdfService.GenerateDetayliCariEkstrePdfBytesAsync(SelectedCari, hareketler, detayDictionary);
            _pendingPdfName = $"DetayliEkstre_{SelectedCari.Unvan}_{DateTime.Now:ddMMyyyy}.pdf";
            EkstreTitle = "Detaylı Ekstre";
            IsDetayliEkstre = true;
            IsEkstreOptionVisible = true;
            StatusMessage = "";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Rapor hatası: {ex.Message}";
            StatusMessage = "";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public virtual async Task CariYaslandirmaAsync(CariKart? cari = null)
    {
        if (cari != null) SelectedCari = cari;
        if (SelectedCari == null) return;
        try 
        {
            IsLoading = true;
            var hareketler = await _uow.Cariler.GetHareketlerAsync(SelectedCari.Id);
            
            string title = $"{SelectedCari.Unvan} - Yaşlandırma Raporu";
            string[] headers = new[] { "Vade Tarihi", "Açıklama", "0-30 Gün", "31-60 Gün", "61-90 Gün", "90+ Gün", "Borç Bakiyesi" };
            List<string[]> rows = new();

            var borcHareketler = hareketler.Where(h => (h.Borc - h.Alacak) > 0).ToList();
            var now = DateTime.Now;

            foreach (var h in borcHareketler.OrderBy(h => h.Tarih))
            {
                decimal bakiye = h.Borc - h.Alacak;
                var days = (now - h.Tarih).TotalDays;
                
                string d0_30 = days <= 30 ? bakiye.ToString("C2") : "-";
                string d31_60 = (days > 30 && days <= 60) ? bakiye.ToString("C2") : "-";
                string d61_90 = (days > 60 && days <= 90) ? bakiye.ToString("C2") : "-";
                string d90plus = days > 90 ? bakiye.ToString("C2") : "-";

                rows.Add(new[] { 
                    h.Tarih.ToString("dd.MM.yyyy"), 
                    h.Aciklama ?? "-", 
                    d0_30, d31_60, d61_90, d90plus, 
                    bakiye.ToString("C2") 
                });
            }

            // Summary row
            decimal t0_30 = borcHareketler.Where(h => (now - h.Tarih).TotalDays <= 30).Sum(h => h.Borc - h.Alacak);
            decimal t31_60 = borcHareketler.Where(h => (now - h.Tarih).TotalDays > 30 && (now - h.Tarih).TotalDays <= 60).Sum(h => h.Borc - h.Alacak);
            decimal t61_90 = borcHareketler.Where(h => (now - h.Tarih).TotalDays > 60 && (now - h.Tarih).TotalDays <= 90).Sum(h => h.Borc - h.Alacak);
            decimal t90plus = borcHareketler.Where(h => (now - h.Tarih).TotalDays > 90).Sum(h => h.Borc - h.Alacak);
            
            rows.Add(new[] { "TOPLAM", "", t0_30.ToString("C2"), t31_60.ToString("C2"), t61_90.ToString("C2"), t90plus.ToString("C2"), (t0_30+t31_60+t61_90+t90plus).ToString("C2") });

            _pendingPdfBytes = await _pdfService.GenerateGenericTablePdfBytesAsync(title, headers, rows);
            _pendingPdfName = $"Yaslandirma_{SelectedCari.Unvan}_{DateTime.Now:ddMMyyyy}.pdf";
            IsEkstreOptionVisible = true;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Yaşlandırma hatası: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task VadeFarkiHesaplaAsync()
    {
        if (SelectedCari == null) return;
        try 
        {
            // Simple interest calculation: %2 per month for overdue balance
            var hareketler = await _uow.Cariler.GetHareketlerAsync(SelectedCari.Id);
            var now = DateTime.Now;
            decimal totalInterest = 0;
            
            foreach (var h in hareketler.Where(h => (h.Borc - h.Alacak) > 0))
            {
                var days = (now - h.Tarih).TotalDays;
                if (days > 30) // Over 30 days
                {
                    decimal amount = h.Borc - h.Alacak;
                    decimal monthlyRate = 0.02m; // %2
                    decimal interest = amount * monthlyRate * (decimal)((days - 30) / 30.0);
                    totalInterest += interest;
                }
            }

            StatusMessage = $"Hesaplanan Vade Farkı (Aylık %2): {totalInterest:C2}";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Vade farkı hesaplama hatası: {ex.Message}";
        }
    }

    [RelayCommand]
    public virtual async Task DownloadEkstreAsync(object? parameter) => await Task.CompletedTask;

    [RelayCommand]
    public async Task PreviewEkstre()
    {
        if (_pendingPdfBytes == null || SelectedCari == null) return;
        IsEkstreOptionVisible = false;
        await HandleFileOpenAsync(_pendingPdfBytes, $"Ekstre_{SelectedCari.Unvan}_{DateTime.Now:ddMMyyyy}.pdf");
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    public async Task ShareEkstreWhatsAppAsync()
    {
        if (_pendingPdfBytes == null || SelectedCari == null) return;
        IsEkstreOptionVisible = false;
        
        string telefon = SelectedCari.Telefon ?? SelectedCari.CepTelefon ?? "";
        string mesaj = $"Sayın {SelectedCari.Unvan}, güncel hesap ekstreniz ekte sunulmuştur.";
        string dosyaAdi = $"Ekstre_{SelectedCari.Unvan}_{DateTime.Now:ddMMyyyy}.pdf";

        var shareService = ResolveShareService();
        if (shareService != null)
        {
            try
            {
                string digitsOnly = new string(telefon.Where(char.IsDigit).ToArray());
                string targetPhone = digitsOnly.StartsWith("0") && digitsOnly.Length == 11 ? "90" + digitsOnly.Substring(1) : (digitsOnly.Length == 10 ? "90" + digitsOnly : digitsOnly);
                
                StatusMessage = $"WhatsApp açılıyor... Hedef Numara: {targetPhone}";
                await shareService.ShareViaWhatsAppAsync(telefon, mesaj, _pendingPdfBytes, dosyaAdi);
                StatusMessage = $"WhatsApp otomatik gönderimi tetiklendi. Ekstre {targetPhone} numarasına iletiliyor...";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"WhatsApp Paylaşım Hatası: {ex.Message}";
                StatusMessage = ErrorMessage;
            }
        }
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    public async Task ShareEkstreEmailAsync()
    {
        if (_pendingPdfBytes == null || SelectedCari == null) return;
        IsEkstreOptionVisible = false;

        string aliciEposta = SelectedCari.Email ?? "";
        if (string.IsNullOrWhiteSpace(aliciEposta))
        {
            StatusMessage = "Cari karta tanımlı e-posta adresi bulunamadı.";
            return;
        }

        string konu = $"Cari Hesap Ekstresi - {SelectedCari.Unvan}";
        string mesaj = $"Sayın {SelectedCari.Unvan},\n\nGüncel cari hesap ekstreniz ekteki PDF dosyasında yer almaktadır.\n\nİyi çalışmalar.";
        string dosyaAdi = $"Ekstre_{SelectedCari.Unvan}_{DateTime.Now:ddMMyyyy}.pdf";

        var shareService = ResolveShareService();
        if (shareService != null)
        {
            try
            {
                StatusMessage = "E-posta gönderiliyor...";
                await shareService.SendPdfViaEmailAsync(aliciEposta, konu, mesaj, _pendingPdfBytes, dosyaAdi);
                StatusMessage = "E-posta başarıyla gönderildi.";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"E-posta gönderme hatası: {ex.Message}";
                StatusMessage = ErrorMessage;
            }
        }
    }

    private async Task<(byte[]? pdfBytes, string fileName)> GenerateTransactionPdfBytesAsync(CariHareket hareket)
    {
        string islemTuru = hareket.IslemTuru ?? "";
        byte[]? pdfBytes = null;
        string fileName = "Islem.pdf";

        bool isKk = islemTuru.Contains("Kredi", StringComparison.OrdinalIgnoreCase) || 
                    (hareket.Aciklama?.Contains("[Kredi Kartı]", StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (hareket.EvrakNo?.StartsWith("KK-") ?? false);

        bool isEft = islemTuru.Contains("Havale", StringComparison.OrdinalIgnoreCase) || 
                     islemTuru.Contains("EFT", StringComparison.OrdinalIgnoreCase) ||
                     (hareket.Aciklama?.Contains("[Havale / EFT]", StringComparison.OrdinalIgnoreCase) ?? false) ||
                     (hareket.EvrakNo?.StartsWith("EFT-") ?? false);

        bool isCek = islemTuru.Contains("Çek", StringComparison.OrdinalIgnoreCase) || 
                     islemTuru.Contains("Senet", StringComparison.OrdinalIgnoreCase) ||
                     (hareket.Aciklama?.Contains("[Çek]", StringComparison.OrdinalIgnoreCase) ?? false) ||
                     (hareket.Aciklama?.Contains("[Senet]", StringComparison.OrdinalIgnoreCase) ?? false) ||
                     (hareket.EvrakNo?.StartsWith("CK-") ?? false);

        if (isKk)
        {
            var allKk = await _uow.KrediKartlari.GetAllAsync();
            var searchKey = hareket.EvrakNo ?? "";
            var cleanKey = searchKey.StartsWith("KK-") ? searchKey.Substring(3) : searchKey;
            var refId = hareket.RefId ?? "";
            
            KrediKartiIslem? kkIslem = null;
            if (!string.IsNullOrWhiteSpace(refId))
                kkIslem = allKk.FirstOrDefault(x => x.OnayKodu == refId);

            if (kkIslem == null && !string.IsNullOrWhiteSpace(searchKey))
            {
                kkIslem = allKk.FirstOrDefault(x => 
                    x.OnayKodu == searchKey || 
                    x.OnayKodu == cleanKey || 
                    x.Id.ToString() == cleanKey ||
                    (searchKey.Length > 6 && x.OnayKodu != null && x.OnayKodu.Contains(searchKey.Substring(searchKey.Length - 6))));
            }

            if (kkIslem != null)
            {
                pdfBytes = await _pdfService.GenerateKrediKartiSlipPdfBytesAsync(kkIslem);
                fileName = $"KrediKartiSlip_{kkIslem.Id}.pdf";
            }
        }
        
        if (pdfBytes == null && isEft)
        {
            var allEft = await _uow.EftIslemleri.GetAllAsync();
            var searchKey = hareket.EvrakNo ?? "";
            var cleanKey = searchKey.StartsWith("EFT-") ? searchKey.Substring(4) : searchKey;
            var refId = hareket.RefId ?? "";

            EftIslem? eftIslem = null;
            if (!string.IsNullOrWhiteSpace(refId))
                eftIslem = allEft.FirstOrDefault(x => x.DekontNo == refId);

            if (eftIslem == null && !string.IsNullOrWhiteSpace(searchKey))
            {
                eftIslem = allEft.FirstOrDefault(x => 
                    x.DekontNo == searchKey || 
                    x.DekontNo == cleanKey || 
                    x.Id.ToString() == cleanKey);
            }

            if (eftIslem != null)
            {
                pdfBytes = await _pdfService.GenerateEftSlipPdfBytesAsync(eftIslem);
                fileName = $"EftSlip_{eftIslem.Id}.pdf";
            }
        }
        
        if (pdfBytes == null && isCek)
        {
             var allCeks = await _uow.Cekler.GetAllAsync();
             var searchKey = hareket.EvrakNo ?? "";
             var cek = allCeks.FirstOrDefault(c => (!string.IsNullOrEmpty(searchKey) && (c.PortfoyNo == searchKey || c.SeriNo == searchKey)));
             
             if (cek != null)
             {
                 pdfBytes = await _pdfService.GenerateCekPdfBytesAsync(cek);
                 string safeNo = (cek.PortfoyNo ?? "Bilinmeyen").Replace("/", "-").Replace("\\", "-");
                 fileName = $"Cek_{safeNo}.pdf";
             }
        }

        if (pdfBytes == null && (islemTuru.Contains("Fatura", StringComparison.OrdinalIgnoreCase) || 
                  islemTuru.Equals("Satış", StringComparison.OrdinalIgnoreCase) || 
                  islemTuru.Equals("Alış", StringComparison.OrdinalIgnoreCase)))
        {
            Fatura? fatura = null;
            if (hareket.FaturaId.HasValue && hareket.FaturaId.Value > 0)
                fatura = await _uow.Faturalar.GetByIdAsync(hareket.FaturaId.Value);
            
            if (fatura == null && !string.IsNullOrEmpty(hareket.EvrakNo))
                fatura = await _uow.Faturalar.GetByNoAsync(hareket.EvrakNo);

            if (fatura != null)
            {
                var detaylar = await _uow.Faturalar.GetDetaylarAsync(fatura.Id);
                pdfBytes = await _pdfService.GenerateFaturaPdfBytesAsync(fatura, detaylar);
                string safeNo = (fatura.FaturaNo ?? fatura.Id.ToString()).Replace("/", "-").Replace("\\", "-");
                fileName = $"Fatura_{safeNo}.pdf";
            }
        }

        if (pdfBytes == null)
        {
            decimal tutar = hareket.Borc > 0 ? hareket.Borc : hareket.Alacak;
            pdfBytes = await _pdfService.GenerateMakbuzPdfBytesAsync(hareket.IslemTuru ?? "Islem", hareket.CariUnvan ?? "Cari", hareket.Tarih, tutar, hareket.Aciklama);
            fileName = $"Makbuz_{DateTime.Now.Ticks}.pdf";
        }

        return (pdfBytes, fileName);
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    public async Task ShareTransactionWhatsAppAsync()
    {
        var hareket = SelectedHareket;
        if (hareket == null || SelectedCari == null) return;
        
        StatusMessage = "PDF hazırlanıyor, lütfen bekleyiniz...";
        var (pdfBytes, fileName) = await GenerateTransactionPdfBytesAsync(hareket);
        if (pdfBytes == null) 
        {
            StatusMessage = "İşlem belgesi oluşturulamadı.";
            return;
        }

        string telefon = SelectedCari.Telefon ?? SelectedCari.CepTelefon ?? "";
        string mesaj = $"Sayın {SelectedCari.Unvan}, {hareket.Tarih:dd.MM.yyyy} tarihli {hareket.IslemTuru} işleminizin belgesi ekte sunulmuştur.";

        var shareService = ResolveShareService();
        if (shareService != null)
        {
            try
            {
                string digitsOnly = new string(telefon.Where(char.IsDigit).ToArray());
                string targetPhone = digitsOnly.StartsWith("0") && digitsOnly.Length == 11 ? "90" + digitsOnly.Substring(1) : (digitsOnly.Length == 10 ? "90" + digitsOnly : digitsOnly);
                
                StatusMessage = $"WhatsApp açılıyor... Hedef Numara: {targetPhone}";
                await shareService.ShareViaWhatsAppAsync(telefon, mesaj, pdfBytes, fileName);
                StatusMessage = $"WhatsApp otomatik gönderimi tetiklendi. Belge {targetPhone} numarasına iletiliyor...";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"WhatsApp Paylaşım Hatası: {ex.Message}";
                StatusMessage = ErrorMessage;
            }
        }
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    public async Task ShareTransactionEmailAsync()
    {
        var hareket = SelectedHareket;
        if (hareket == null || SelectedCari == null) return;

        string aliciEposta = SelectedCari.Email ?? "";
        if (string.IsNullOrWhiteSpace(aliciEposta))
        {
            StatusMessage = "Cari karta tanımlı e-posta adresi bulunamadı.";
            return;
        }

        StatusMessage = "PDF hazırlanıyor, lütfen bekleyiniz...";
        var (pdfBytes, fileName) = await GenerateTransactionPdfBytesAsync(hareket);
        if (pdfBytes == null)
        {
            StatusMessage = "İşlem belgesi oluşturulamadı.";
            return;
        }

        string konu = $"İşlem Belgesi - {SelectedCari.Unvan}";
        string mesaj = $"Sayın {SelectedCari.Unvan},\n\n{hareket.Tarih:dd.MM.yyyy} tarihli {hareket.IslemTuru} işleminizin belgesi ekte yer almaktadır.\n\nİyi çalışmalar.";

        var shareService = ResolveShareService();
        if (shareService != null)
        {
            try
            {
                StatusMessage = "E-posta gönderiliyor...";
                await shareService.SendPdfViaEmailAsync(aliciEposta, konu, mesaj, pdfBytes, fileName);
                StatusMessage = "E-posta başarıyla gönderildi.";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"E-posta gönderme hatası: {ex.Message}";
                StatusMessage = ErrorMessage;
            }
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

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    public void CloseEkstreOption()
    {
        IsEkstreOptionVisible = false;
        _pendingPdfBytes = null;
        EkstreTitle = "Hesap Ekstresi";
        IsDetayliEkstre = false;
    }

    protected abstract Task HandleFileOpenAsync(byte[] content, string fileName);

    [RelayCommand]
    public virtual void OpenSatisFaturasi() { }

    [RelayCommand]
    public virtual void OpenAlisFaturasi() { }

    [RelayCommand]
    public virtual async Task ViewTransactionAsync()
    {
        var hareket = SelectedHareket;
        if (hareket == null || SelectedCari == null) 
        {
            StatusMessage = "Lütfen geçerli bir işlem seçiniz.";
            return;
        }
        StatusMessage = "PDF hazırlanıyor, lütfen bekleyiniz...";

        string islemTuru = hareket.IslemTuru ?? "";

        try 
        {
            byte[]? pdfBytes = null;
            string fileName = "İşlem.pdf";

            bool isKk = islemTuru.Contains("Kredi", StringComparison.OrdinalIgnoreCase) || 
                        (hareket.Aciklama?.Contains("[Kredi Kartı]", StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (hareket.EvrakNo?.StartsWith("KK-") ?? false);

            bool isEft = islemTuru.Contains("Havale", StringComparison.OrdinalIgnoreCase) || 
                         islemTuru.Contains("EFT", StringComparison.OrdinalIgnoreCase) ||
                         (hareket.Aciklama?.Contains("[Havale / EFT]", StringComparison.OrdinalIgnoreCase) ?? false) ||
                         (hareket.EvrakNo?.StartsWith("EFT-") ?? false);

            bool isCek = islemTuru.Contains("Çek", StringComparison.OrdinalIgnoreCase) || 
                         islemTuru.Contains("Senet", StringComparison.OrdinalIgnoreCase) ||
                         (hareket.Aciklama?.Contains("[Çek]", StringComparison.OrdinalIgnoreCase) ?? false) ||
                         (hareket.Aciklama?.Contains("[Senet]", StringComparison.OrdinalIgnoreCase) ?? false) ||
                         (hareket.EvrakNo?.StartsWith("CK-") ?? false);

            if (isKk)
            {
                var allKk = await _uow.KrediKartlari.GetAllAsync();
                var searchKey = hareket.EvrakNo ?? "";
                var cleanKey = searchKey.StartsWith("KK-") ? searchKey.Substring(3) : searchKey;
                var refId = hareket.RefId ?? "";
                
                KrediKartiIslem? kkIslem = null;
                if (!string.IsNullOrWhiteSpace(refId))
                {
                    kkIslem = allKk.FirstOrDefault(x => x.OnayKodu == refId);
                }

                if (kkIslem == null && !string.IsNullOrWhiteSpace(searchKey))
                {
                    kkIslem = allKk.FirstOrDefault(x => 
                        x.OnayKodu == searchKey || 
                        x.OnayKodu == cleanKey || 
                        x.Id.ToString() == cleanKey ||
                        (searchKey.Length > 6 && x.OnayKodu != null && x.OnayKodu.Contains(searchKey.Substring(searchKey.Length - 6))));
                }

                if (kkIslem != null)
                {
                    pdfBytes = await _pdfService.GenerateKrediKartiSlipPdfBytesAsync(kkIslem);
                    fileName = $"KrediKartiSlip_{kkIslem.Id}.pdf";
                }
            }
            
            if (pdfBytes == null && isEft)
            {
                var allEft = await _uow.EftIslemleri.GetAllAsync();
                var searchKey = hareket.EvrakNo ?? "";
                var cleanKey = searchKey.StartsWith("EFT-") ? searchKey.Substring(4) : searchKey;
                var refId = hareket.RefId ?? "";

                EftIslem? eftIslem = null;
                if (!string.IsNullOrWhiteSpace(refId))
                {
                    eftIslem = allEft.FirstOrDefault(x => x.DekontNo == refId);
                }

                if (eftIslem == null && !string.IsNullOrWhiteSpace(searchKey))
                {
                    eftIslem = allEft.FirstOrDefault(x => 
                        x.DekontNo == searchKey || 
                        x.DekontNo == cleanKey || 
                        x.Id.ToString() == cleanKey);
                }

                if (eftIslem != null)
                {
                    pdfBytes = await _pdfService.GenerateEftSlipPdfBytesAsync(eftIslem);
                    fileName = $"EftSlip_{eftIslem.Id}.pdf";
                }
            }
            
            if (pdfBytes == null && isCek)
            {
                 var allCeks = await _uow.Cekler.GetAllAsync();
                 var searchKey = hareket.EvrakNo ?? "";
                 var cek = allCeks.FirstOrDefault(c => (!string.IsNullOrEmpty(searchKey) && (c.PortfoyNo == searchKey || c.SeriNo == searchKey)));
                 
                 if (cek != null)
                 {
                     pdfBytes = await _pdfService.GenerateCekPdfBytesAsync(cek);
                     string safeNo = (cek.PortfoyNo ?? "Bilinmeyen").Replace("/", "-").Replace("\\", "-");
                     fileName = $"Cek_{safeNo}.pdf";
                 }
            }

            if (pdfBytes == null && (islemTuru.Contains("Fatura", StringComparison.OrdinalIgnoreCase) || 
                      islemTuru.Equals("Satış", StringComparison.OrdinalIgnoreCase) || 
                      islemTuru.Equals("Alış", StringComparison.OrdinalIgnoreCase)))
            {
                Fatura? fatura = null;
                if (hareket.FaturaId.HasValue && hareket.FaturaId.Value > 0)
                    fatura = await _uow.Faturalar.GetByIdAsync(hareket.FaturaId.Value);
                
                if (fatura == null && !string.IsNullOrEmpty(hareket.EvrakNo))
                    fatura = await _uow.Faturalar.GetByNoAsync(hareket.EvrakNo);

                if (fatura != null)
                {
                    var detaylar = await _uow.Faturalar.GetDetaylarAsync(fatura.Id);
                    pdfBytes = await _pdfService.GenerateFaturaPdfBytesAsync(fatura, detaylar);
                    string safeNo = (fatura.FaturaNo ?? fatura.Id.ToString()).Replace("/", "-").Replace("\\", "-");
                    fileName = $"Fatura_{safeNo}.pdf";
                }
                else
                {
                     // Fallback will happen below
                     ErrorMessage = "Fatura kaydı bulunamadı, işlem makbuzu oluşturuluyor...";
                }
            }

            if (pdfBytes == null)
            {
                decimal tutar = hareket.Borc > 0 ? hareket.Borc : hareket.Alacak;
                pdfBytes = await _pdfService.GenerateMakbuzPdfBytesAsync(hareket.IslemTuru ?? "İşlem", hareket.CariUnvan ?? "Cari", hareket.Tarih, tutar, hareket.Aciklama);
                fileName = $"Makbuz_{DateTime.Now.Ticks}.pdf";
            }

            if (pdfBytes != null) 
            {
                StatusMessage = "PDF dosyası açılıyor...";
                await HandleFileOpenAsync(pdfBytes, fileName);
                StatusMessage = "";
            }
            else if (string.IsNullOrEmpty(ErrorMessage)) 
            {
                ErrorMessage = "İşlem görüntülenebilir bir belgeye sahip değil.";
                StatusMessage = ErrorMessage; 
            }
        }
        catch (Exception ex)
        {
            var msg = ex.Message;
            if (ex.InnerException != null) msg += $" -> {ex.InnerException.Message}";
            ErrorMessage = $"PDF Görüntüleme Hatası: {msg}";
            StatusMessage = ErrorMessage;
        }
    }

    [RelayCommand]
    public virtual async Task EditTransactionAsync() { await Task.CompletedTask; }

    [RelayCommand]
    protected async Task<Fatura?> FindLinkedFaturaAsync(CariHareket hareket)
    {
        if (hareket == null) return null;

        if (hareket.FaturaId.HasValue && hareket.FaturaId.Value > 0)
        {
            var faturaById = await _uow.Faturalar.GetByIdAsync(hareket.FaturaId.Value);
            if (faturaById != null) return faturaById;
        }

        string cleanNo = (hareket.EvrakNo ?? "").Trim();
        if (cleanNo.StartsWith("KPL-", StringComparison.OrdinalIgnoreCase))
        {
            cleanNo = cleanNo.Substring(4).Trim();
        }

        // Extract from Aciklama if EvrakNo was truncated or empty
        string extractedNo = "";
        if (!string.IsNullOrWhiteSpace(hareket.Aciklama))
        {
            var match = System.Text.RegularExpressions.Regex.Match(hareket.Aciklama, @"(FTR-[\w\d]+|FAT-[\w\d]+|SF-[\w\d\-]+|AF-[\w\d\-]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (match.Success)
            {
                extractedNo = match.Groups[1].Value.Trim();
            }
        }

        if (!string.IsNullOrEmpty(cleanNo))
        {
            var directMatch = await _uow.Faturalar.GetByNoAsync(cleanNo);
            if (directMatch != null) return directMatch;
        }

        if (!string.IsNullOrEmpty(cleanNo) || !string.IsNullOrEmpty(extractedNo))
        {
            var allFaturalar = await _uow.Faturalar.GetAllAsync();
            var fatura = allFaturalar.FirstOrDefault(f => 
                (!string.IsNullOrEmpty(cleanNo) && (f.FaturaNo.Equals(cleanNo, StringComparison.OrdinalIgnoreCase) || f.FaturaNo.StartsWith(cleanNo, StringComparison.OrdinalIgnoreCase) || cleanNo.StartsWith(f.FaturaNo, StringComparison.OrdinalIgnoreCase))) ||
                (!string.IsNullOrEmpty(extractedNo) && (f.FaturaNo.Equals(extractedNo, StringComparison.OrdinalIgnoreCase) || f.FaturaNo.StartsWith(extractedNo, StringComparison.OrdinalIgnoreCase)))
            );
            if (fatura != null) return fatura;
        }

        // Fallback: If movement is explicitly an invoice type, match by CariId, Date and Amount
        string tur = hareket.IslemTuru ?? "";
        if (tur.Contains("Fatura", StringComparison.OrdinalIgnoreCase) || tur.Contains("Satış", StringComparison.OrdinalIgnoreCase) || tur.Contains("Alış", StringComparison.OrdinalIgnoreCase))
        {
            var allFaturalar = await _uow.Faturalar.GetAllAsync();
            decimal hareketTutar = hareket.Borc > 0 ? hareket.Borc : hareket.Alacak;
            var matchByAmount = allFaturalar.FirstOrDefault(f => 
                f.CariId == hareket.CariId && 
                f.Tarih.Date == hareket.Tarih.Date && 
                Math.Abs(f.GenelToplam - hareketTutar) < 0.01m
            );
            if (matchByAmount != null) return matchByAmount;
        }

        return null;
    }

    [RelayCommand]
    public void DeleteTransactionConfirm()
    {
        if (SelectedHareket == null) return;
        bool isFatura = (SelectedHareket.IslemTuru != null && SelectedHareket.IslemTuru.Contains("Fatura")) ||
                        (SelectedHareket.FaturaId.HasValue && SelectedHareket.FaturaId.Value > 0) ||
                        (!string.IsNullOrWhiteSpace(SelectedHareket.EvrakNo) && (SelectedHareket.EvrakNo.StartsWith("KPL-", StringComparison.OrdinalIgnoreCase) || SelectedHareket.EvrakNo.StartsWith("FTR", StringComparison.OrdinalIgnoreCase) || SelectedHareket.EvrakNo.StartsWith("FAT", StringComparison.OrdinalIgnoreCase))) ||
                        (!string.IsNullOrWhiteSpace(SelectedHareket.Aciklama) && (SelectedHareket.Aciklama.Contains("FTR-") || SelectedHareket.Aciklama.Contains("Fatura No")));
        
        string title = isFatura ? "Faturayı ve Hareketi Sil" : "İşlemi Sil";
        string msg = isFatura
            ? "Bu işlem bir faturaya aittir. Fatura, faturaya bağlı stok hareketleri ve stok bakiyeleri de geri alınıp silinecektir. Emin misiniz?"
            : "Seçili işlemi silmek istediğinize emin misiniz?";

        ShowConfirm(title, msg, DeleteTransactionAsync);
    }

    public async Task DeleteTransactionAsync()
    {
        if (SelectedHareket == null) return;
        var targetHareket = SelectedHareket;
        SelectedHareket = null;

        try 
        {
            bool isInvoiceMovement = (targetHareket.IslemTuru != null && targetHareket.IslemTuru.Contains("Fatura")) ||
                                     (targetHareket.FaturaId.HasValue && targetHareket.FaturaId.Value > 0) ||
                                     (!string.IsNullOrWhiteSpace(targetHareket.EvrakNo) && (targetHareket.EvrakNo.StartsWith("KPL-", StringComparison.OrdinalIgnoreCase) || targetHareket.EvrakNo.StartsWith("FTR", StringComparison.OrdinalIgnoreCase) || targetHareket.EvrakNo.StartsWith("FAT", StringComparison.OrdinalIgnoreCase))) ||
                                     (!string.IsNullOrWhiteSpace(targetHareket.Aciklama) && (targetHareket.Aciklama.Contains("FTR-") || targetHareket.Aciklama.Contains("Fatura No")));

            Fatura? fatura = isInvoiceMovement ? await FindLinkedFaturaAsync(targetHareket) : null;
            if (fatura != null)
            {
                await _uow.Faturalar.DeleteAsync(fatura);
            }
            else 
            {
                if (isInvoiceMovement)
                {
                    await CleanupOrphanInvoiceStockAsync(targetHareket);
                }
                await _finansService.DeleteTransactionAsync(targetHareket);
            }

            if (SelectedCari != null) 
            {
               await _uow.Cariler.RecalculateBalanceAsync(SelectedCari.Id);
               await LoadHareketlerAsync(SelectedCari.Id);
            }
            await LoadCarilerAsync();
            NotifyFinancialDataChanged();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Silme hatası: {ex.Message}";
            SelectedHareket = targetHareket;
        }
    }

    private async Task CleanupOrphanInvoiceStockAsync(CariHareket hareket)
    {
        try
        {
            string fNo = hareket.EvrakNo?.Trim() ?? "";
            if (fNo.StartsWith("KPL-", StringComparison.OrdinalIgnoreCase)) fNo = fNo.Substring(4).Trim();
            int? fId = hareket.FaturaId;

            string extractedNo = "";
            if (!string.IsNullOrWhiteSpace(hareket.Aciklama))
            {
                var match = System.Text.RegularExpressions.Regex.Match(hareket.Aciklama, @"(FTR-[\w\d]+|FAT-[\w\d]+|SF-[\w\d\-]+|AF-[\w\d\-]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (match.Success) extractedNo = match.Groups[1].Value.Trim();
            }

            var allHareketler = await _uow.Stoklar.GetAllHareketlerAsync();
            var orphanStokMoves = allHareketler.Where(s => 
                (fId.HasValue && fId.Value > 0 && s.FaturaId == fId.Value) || 
                (!string.IsNullOrEmpty(fNo) && !string.IsNullOrEmpty(s.EvrakNo) && (s.EvrakNo.Trim().Equals(fNo, StringComparison.OrdinalIgnoreCase) || s.EvrakNo.Trim().StartsWith(fNo, StringComparison.OrdinalIgnoreCase) || fNo.StartsWith(s.EvrakNo.Trim(), StringComparison.OrdinalIgnoreCase))) ||
                (!string.IsNullOrEmpty(extractedNo) && !string.IsNullOrEmpty(s.EvrakNo) && (s.EvrakNo.Trim().Equals(extractedNo, StringComparison.OrdinalIgnoreCase) || s.EvrakNo.Trim().StartsWith(extractedNo, StringComparison.OrdinalIgnoreCase)))
            ).ToList();

            if (orphanStokMoves.Any())
            {
                var affectedStokIds = orphanStokMoves.Select(s => s.StokId).Distinct().ToList();
                foreach (var m in orphanStokMoves)
                {
                    await _uow.Stoklar.DeleteHareketAsync(m);
                }

                foreach (var sId in affectedStokIds)
                {
                    await _uow.Stoklar.RecalculateCostsAsync(sId);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CariListViewModel] CleanupOrphanInvoiceStockAsync error: {ex.Message}");
        }
    }

    [RelayCommand]
    public void DeleteSelectedTransactionsConfirm()
    {
        var selected = CariHareketler.Where(x => x.IsSelected).ToList();
        if (!selected.Any()) { WarningMessage = "Herhangi bir işlem seçilmedi!"; return; }
        
        ShowConfirm("Seçilenleri Sil", $"{selected.Count} adet işlemi toplu silmek istediğinize emin misiniz? Fatura içeren işlemler faturasıyla ve stoklarıyla birlikte silinecektir.", DeleteSelectedTransactionsAsync);
    }

    public async Task DeleteSelectedTransactionsAsync()
    {
        try
        {
            var selected = CariHareketler.Where(x => x.IsSelected).ToList();
            if (!selected.Any()) return;

            IsLoading = true;
            var processedFaturaIds = new HashSet<int>();

            foreach (var h in selected)
            {
                bool isInvoiceMovement = (h.IslemTuru != null && h.IslemTuru.Contains("Fatura")) ||
                                         (h.FaturaId.HasValue && h.FaturaId.Value > 0) ||
                                         (!string.IsNullOrWhiteSpace(h.EvrakNo) && (h.EvrakNo.StartsWith("KPL-", StringComparison.OrdinalIgnoreCase) || h.EvrakNo.StartsWith("FTR", StringComparison.OrdinalIgnoreCase) || h.EvrakNo.StartsWith("FAT", StringComparison.OrdinalIgnoreCase))) ||
                                         (!string.IsNullOrWhiteSpace(h.Aciklama) && (h.Aciklama.Contains("FTR-") || h.Aciklama.Contains("Fatura No")));

                Fatura? fatura = isInvoiceMovement ? await FindLinkedFaturaAsync(h) : null;
                if (fatura != null)
                {
                    if (!processedFaturaIds.Contains(fatura.Id))
                    {
                        processedFaturaIds.Add(fatura.Id);
                        await _uow.Faturalar.DeleteAsync(fatura);
                    }
                }
                else 
                {
                    if (isInvoiceMovement)
                    {
                        await CleanupOrphanInvoiceStockAsync(h);
                    }
                    await _finansService.DeleteTransactionAsync(h);
                }
            }
            
            if (SelectedCari != null)
            {
                await _uow.Cariler.RecalculateBalanceAsync(SelectedCari.Id);
                await LoadHareketlerAsync(SelectedCari.Id);
            }
            
            await LoadCarilerAsync();
            SuccessMessage = $"{selected.Count} adet işlem silindi.";
            NotifyFinancialDataChanged();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Toplu silme hatası: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task ExportToExcelAsync()
    {
        if (!Cariler.Any()) return;
        try
        {
            var bytes = await _excelService.ExportListToMemoryAsync(Cariler, "Cari Listesi");
            await HandleFileOpenAsync(bytes, $"CariListesi_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Excel hatası: {ex.Message}";
        }
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

    public override void OnEscape()
    {
        if (IsConfirmVisible) { ConfirmNo(); return; }
        if (IsCariEkleVisible) { IsCariEkleVisible = false; return; }
        if (IsTransactionDialogVisible) { IsTransactionDialogVisible = false; return; }
        if (IsEkstreOptionVisible) { IsEkstreOptionVisible = false; return; }
        if (IsCariSelectionVisible) { IsCariSelectionVisible = false; return; }
        if (IsKasaSelectionVisible) { IsKasaSelectionVisible = false; return; }
        
        base.OnEscape();
    }

    private static string GetMethodAbbreviation(string method) => method switch
    {
        "Kredi Kartı" => "KK",
        "Havale / EFT" => "EFT",
        "Havale/EFT" => "EFT",
        "EFT" => "EFT",
        "Çek" => "Çek",
        "Nakit" => "Nakit",
        _ => method
    };
}
