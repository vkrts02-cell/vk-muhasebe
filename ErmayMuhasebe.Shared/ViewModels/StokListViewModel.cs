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

public abstract partial class StokListViewModel : ViewModelBase
{
    protected readonly IUnitOfWork _uow;
    protected readonly IPdfService _pdfService;
    protected readonly IExcelService _excelService;
    protected readonly IFileService _fileService;

    [ObservableProperty]
    private ObservableCollection<StokKart> _stoklar = new();

    [ObservableProperty]
    private StokKart? _selectedStok;

    [ObservableProperty]
    private ObservableCollection<StokHareket> _stokHareketleri = new();

    // Filters
    [ObservableProperty] private string _filterKod = "";
    [ObservableProperty] private string _filterStokAdi = "";
    [ObservableProperty] private string _filterGrup = "";
    [ObservableProperty] private bool _onlyWithBalance = false;

    partial void OnFilterKodChanged(string value) => _ = LoadStoklarAsync();
    partial void OnFilterStokAdiChanged(string value) => _ = LoadStoklarAsync();
    partial void OnFilterGrupChanged(string value) => _ = LoadStoklarAsync();
    partial void OnOnlyWithBalanceChanged(bool value) => _ = LoadStoklarAsync();

    // Groups
    [ObservableProperty] private ObservableCollection<string> _groupList = new();
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private bool _isSelectAll;

    partial void OnIsSelectAllChanged(bool value)
    {
        foreach (var s in Stoklar) s.IsSelected = value;
    }

    // Pagination Properties
    [ObservableProperty] private int _pageSize = 100;
    [ObservableProperty] private int _currentPageIndex = 0;
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private bool _canNextPage;
    [ObservableProperty] private bool _canPreviousPage;
    [ObservableProperty] private string _pagerInfo = "1 / 1 (0)";
    [ObservableProperty] private string _newGroupName = "";
    [ObservableProperty] private bool _isAddGroupVisible;

    public string FormTitle => SelectedStok == null ? "Yeni Stok Kartı Ekle" : $"Stok Kartını Düzenle: {SelectedStok.StokAdi}";
    public string FormTitleColor => SelectedStok == null ? "#60A5FA" : "#F59E0B";
    public bool IsEditMode => SelectedStok != null;


    // Edit Fields
    [ObservableProperty] private string _editBarkod = "";
    [ObservableProperty] private string _editStokKodu = "";
    [ObservableProperty] private string _editStokAdi = "";
    [ObservableProperty] private string _editKategori = "";
    [ObservableProperty] private string _editBirim = "Kg";
    [ObservableProperty] private ObservableCollection<string> _birimler = new() { "Kg", "Adet", "Mt", "Top" };
    [ObservableProperty] private decimal? _editAlisFiyati;
    [ObservableProperty] private decimal? _editOrtalamaAlisFiyati;
    [ObservableProperty] private decimal? _editSatisFiyati;
    [ObservableProperty] private decimal? _editOrtalamaSatisFiyati;
    [ObservableProperty] private decimal? _editKDV = 20;
    [ObservableProperty] private decimal? _editAcilisBakiye = 0;

    // Manual Transaction Fields
    [ObservableProperty] private string _islemTarihStr = DateTime.Now.ToString("dd.MM.yyyy");
    [ObservableProperty] private decimal? _islemMiktar;
    [ObservableProperty] private decimal? _islemFiyat;
    [ObservableProperty] private string _islemAciklama = "";
    [ObservableProperty] private StokHareket? _selectedStokHareket;

    // Transaction Popup Logic
    [ObservableProperty] private bool _isTransactionWindowVisible;
    [ObservableProperty] private string _transactionType = "GİRİŞ";
    [ObservableProperty] private ObservableCollection<string> _transactionTypes = new() { "GİRİŞ", "ÇIKIŞ" };
    [ObservableProperty] private bool _isEditingTransaction;

    [RelayCommand]
    public void DeleteStokConfirm()
    {
        if (SelectedStok == null) return;
        ShowConfirm("Stok Kartını Sil", $"{SelectedStok.StokAdi} adlı stoğu silmek istediğinize emin misiniz? Bu işlem geri alınamaz.", DeleteStokAsync);
    }

    [RelayCommand]
    public void DeleteStokHareketConfirm()
    {
        if (SelectedStokHareket == null) return;
        ShowConfirm("İşlemi Sil", $"Seçili stok hareketini silmek istediğinize emin misiniz? Bakiye geri alınacaktır.", DeleteStokHareketAsync);
    }

    public StokListViewModel(IUnitOfWork uow, IExcelService excelService, IPdfService pdfService, IFileService fileService)
    {
        _uow = uow;
        _excelService = excelService;
        _pdfService = pdfService;
        _fileService = fileService;
        
        _ = LoadStoklarAsync();
    }

    public override void OnNavigatedTo() => _ = LoadStoklarAsync();

    [RelayCommand]
    public async Task NextPageAsync()
    {
        if (CurrentPageIndex * PageSize + PageSize < TotalCount)
        {
            CurrentPageIndex++;
            await LoadStoklarAsync();
        }
    }

    [RelayCommand]
    public async Task PreviousPageAsync()
    {
        if (CurrentPageIndex > 0)
        {
            CurrentPageIndex--;
            await LoadStoklarAsync();
        }
    }

    public async Task LoadStoklarAsync(int? selectId = null, bool isSilent = false)
    {
        try 
        {
            if (!isSilent) IsLoading = true;
            int? targetSelectId = selectId ?? SelectedStok?.Id;
            
            Expression<Func<StokKart, bool>>? filter = null;
            bool hasKod = !string.IsNullOrWhiteSpace(FilterKod);
            bool hasAd = !string.IsNullOrWhiteSpace(FilterStokAdi);
            bool hasGrup = !string.IsNullOrWhiteSpace(FilterGrup);
            string kod = FilterKod ?? "";
            string ad = FilterStokAdi ?? "";
            string grup = FilterGrup ?? "";

            if (hasKod || hasAd || hasGrup || OnlyWithBalance)
            {
                filter = s => !s.IsDeleted && 
                    (!hasKod || (s.StokKodu != null && s.StokKodu.Contains(kod)) || (s.StokAdi != null && s.StokAdi.Contains(kod)) || (s.Barkod != null && s.Barkod.Contains(kod))) &&
                    (!hasAd || (s.StokAdi != null && s.StokAdi.Contains(ad))) &&
                    (!hasGrup || (s.Kategori != null && s.Kategori.Contains(grup))) &&
                    (!OnlyWithBalance || (s.Miktar > 0.0001m || s.Miktar < -0.0001m));
            }
            else
            {
                filter = s => !s.IsDeleted;
            }

            TotalCount = await _uow.Stoklar.GetCountAsync(filter);
            var pagedList = await _uow.Stoklar.GetPagedAsync(CurrentPageIndex * PageSize, PageSize, filter, s => s.StokAdi!);
            
            var distinctGroups = await _uow.Stoklar.GetGruplarAsync();

            await InvokeOnUIThreadAsync(() => 
            {
                Stoklar = new ObservableCollection<StokKart>(pagedList);
                
                var currentSelected = EditKategori;
                GroupList.Clear();
                foreach (var g in distinctGroups) GroupList.Add(g);
                if (!string.IsNullOrEmpty(currentSelected) && !GroupList.Contains(currentSelected))
                {
                    GroupList.Add(currentSelected);
                }
                EditKategori = currentSelected;
                
                // Pager logic
                int totalPages = (int)Math.Ceiling((double)TotalCount / PageSize);
                if (totalPages == 0) totalPages = 1;
                PagerInfo = $"{CurrentPageIndex + 1} / {totalPages} ({TotalCount})";
                CanNextPage = (CurrentPageIndex + 1) < totalPages;
                CanPreviousPage = CurrentPageIndex > 0;

                if (targetSelectId.HasValue) 
                {
                    SelectedStok = Stoklar.FirstOrDefault(x => x.Id == targetSelectId.Value);
                }
            });
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Stok yükleme hatası: {ex.Message}";
        }
        finally
        {
            if (!isSilent) IsLoading = false;
        }
    }

    partial void OnSelectedStokChanged(StokKart? value)
    {
        OnPropertyChanged(nameof(FormTitle));
        OnPropertyChanged(nameof(FormTitleColor));
        OnPropertyChanged(nameof(IsEditMode));

        if (value != null)
        {
            FillEditForm(value);
            _ = LoadStokHareketleriAsync(value.Id);
        }
        else
        {
            ClearEditForm();
            StokHareketleri.Clear();
        }
    }

    private void FillEditForm(StokKart stok)
    {
        EditBarkod = stok.Barkod ?? "";
        EditStokKodu = stok.StokKodu ?? "";
        EditStokAdi = stok.StokAdi ?? "";
        EditKategori = stok.Kategori ?? "";
        EditBirim = stok.Birim ?? "Kg";
        EditAlisFiyati = stok.AlisFiyati;
        EditOrtalamaAlisFiyati = stok.OrtalamaAlisFiyati;
        EditSatisFiyati = stok.SatisFiyati;
        EditOrtalamaSatisFiyati = stok.OrtalamaSatisFiyati;
        EditKDV = stok.KDV;
        EditAcilisBakiye = (decimal)stok.Miktar;
    }

    private void ClearEditForm()
    {
        EditBarkod = "";
        EditStokKodu = "";
        EditStokAdi = "";
        EditKategori = "";
        EditBirim = "Kg";
        EditAlisFiyati = 0;
        EditOrtalamaAlisFiyati = 0;
        EditSatisFiyati = 0;
        EditOrtalamaSatisFiyati = 0;
        EditKDV = 20;
        EditAcilisBakiye = 0;
    }

    [RelayCommand]
    public void CreateNew()
    {
        SelectedStok = null;
        ClearEditForm();
        OnPropertyChanged(nameof(FormTitle));
        OnPropertyChanged(nameof(FormTitleColor));
        OnPropertyChanged(nameof(IsEditMode));
    }

    [RelayCommand]
    public void CancelEdit()
    {
        if (SelectedStok != null) FillEditForm(SelectedStok);
        else ClearEditForm();
    }

    public async Task LoadStokHareketleriAsync(int stokId)
    {
        try 
        {
            var hareketler = await _uow.Stoklar.GetHareketlerAsync(stokId);
            if (!hareketler.Any())
            {
                if (SelectedStok != null && (SelectedStok.OrtalamaAlisFiyati != 0 || SelectedStok.OrtalamaSatisFiyati != 0 || SelectedStok.Miktar != 0 || SelectedStok.AlisFiyati != 0 || SelectedStok.SatisFiyati != 0))
                {
                    SelectedStok.OrtalamaAlisFiyati = 0;
                    SelectedStok.OrtalamaSatisFiyati = 0;
                    SelectedStok.Miktar = 0;
                    SelectedStok.AlisFiyati = 0;
                    SelectedStok.SatisFiyati = 0;
                    EditOrtalamaAlisFiyati = 0;
                    EditOrtalamaSatisFiyati = 0;
                    EditAlisFiyati = 0;
                    EditSatisFiyati = 0;
                    EditAcilisBakiye = 0;
                    await _uow.Stoklar.SaveAsync(SelectedStok);
                }
            }
            var chronological = hareketler.OrderBy(h => h.Tarih).ThenBy(h => h.Id).ToList();
            decimal runningBalance = 0;
            foreach (var h in chronological)
            {
                // Prioritize numeric flags over fragile string matching
                bool isGiris = h.Giren > 0;
                bool isCikis = h.Cikan > 0;

                // Fallback for older manual entries that might not have Giren/Cikan set
                if (!isGiris && !isCikis)
                {
                    string tur = (h.IslemTuru ?? "").ToUpperInvariant();
                    isGiris = tur.Contains("GİRİŞ") || tur.Contains("ALIS") || tur.Contains("ALIŞ") || tur.Contains("ACILIS") || tur.Contains("AÇILIŞ") || tur.Contains("GİREN");
                    isCikis = tur.Contains("ÇIKIŞ") || tur.Contains("CIKIS") || tur.Contains("SATIS") || tur.Contains("SATIŞ") || tur.Contains("ÇIKAN");
                }

                if (isGiris) runningBalance += h.Miktar;
                else if (isCikis) runningBalance -= h.Miktar;
                
                h.KalanMiktar = runningBalance;
            }

            var sorted = chronological.OrderByDescending(h => h.Tarih).ThenByDescending(h => h.Id).ToList();
            await InvokeOnUIThreadAsync(() => 
            {
                StokHareketleri = new ObservableCollection<StokHareket>(sorted);
            });
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    [RelayCommand]
    public async Task SaveStokAsync()
    {
        try 
        {
            bool isNew = SelectedStok == null;
            ErrorMessage = null;
            SuccessMessage = null;
            StatusMessage = "Stok kartı kaydediliyor...";
            
            if (string.IsNullOrWhiteSpace(EditStokKodu)) 
            { 
                ErrorMessage = "Stok kodu boş olamaz."; 
                StatusMessage = "Hata: Stok kodu boş olamaz.";
                return; 
            }
            if (string.IsNullOrWhiteSpace(EditStokAdi)) 
            { 
                ErrorMessage = "Stok adı boş olamaz."; 
                StatusMessage = "Hata: Stok adı boş olamaz.";
                return; 
            }

            if (!string.IsNullOrWhiteSpace(EditKategori))
            {
                await _uow.Stoklar.SaveGrupAsync(EditKategori);
            }
 
            int? lastId = null;
            if (SelectedStok != null)
            {
                lastId = SelectedStok.Id;
                decimal eskiMiktar = SelectedStok.Miktar;
                decimal yeniMiktar = EditAcilisBakiye ?? 0m;
 
                SelectedStok.Barkod = EditBarkod;
                SelectedStok.StokKodu = EditStokKodu;
                SelectedStok.StokAdi = EditStokAdi;
                SelectedStok.Kategori = EditKategori;
                SelectedStok.Birim = EditBirim;
                SelectedStok.AlisFiyati = EditAlisFiyati ?? 0;
                SelectedStok.OrtalamaAlisFiyati = EditOrtalamaAlisFiyati ?? 0;
                SelectedStok.SatisFiyati = EditSatisFiyati ?? 0;
                SelectedStok.OrtalamaSatisFiyati = EditOrtalamaSatisFiyati ?? 0;
                SelectedStok.KDV = (int)(EditKDV ?? 0);
                SelectedStok.Miktar = yeniMiktar;
 
                await _uow.Stoklar.SaveAsync(SelectedStok);
                if (Math.Abs(eskiMiktar - yeniMiktar) > 0.0001m)
                {
                    await _uow.Stoklar.SaveHareketAsync(new StokHareket
                    {
                        StokId = SelectedStok.Id,
                        StokKodu = SelectedStok.StokKodu ?? "",
                        StokAdi = SelectedStok.StokAdi ?? "",
                        Tarih = DateTime.Now,
                        IslemTuru = yeniMiktar > eskiMiktar ? "GİRİŞ" : "ÇIKIŞ",
                        Miktar = (decimal)Math.Abs(yeniMiktar - eskiMiktar),
                        Aciklama = "Envanter Düzenleme",
                        Giren = yeniMiktar > eskiMiktar ? (decimal)Math.Abs(yeniMiktar - eskiMiktar) : 0,
                        Cikan = yeniMiktar < eskiMiktar ? (decimal)Math.Abs(yeniMiktar - eskiMiktar) : 0,
                        Fiyat = yeniMiktar > eskiMiktar ? SelectedStok.AlisFiyati : SelectedStok.SatisFiyati
                    });
                }
                await _uow.Stoklar.RecalculateCostsAsync(SelectedStok.Id);
            }
            else
            {
                var yeniStok = new StokKart
                {
                    Barkod = EditBarkod,
                    StokKodu = EditStokKodu,
                    StokAdi = EditStokAdi,
                    AlisFiyati = EditAlisFiyati ?? 0,
                    OrtalamaAlisFiyati = EditOrtalamaAlisFiyati ?? 0,
                    SatisFiyati = EditSatisFiyati ?? 0,
                    KDV = (int)(EditKDV ?? 0),
                    Miktar = EditAcilisBakiye ?? 0m,
                    Birim = EditBirim,
                    Kategori = EditKategori,
                    KayitTarihi = DateTime.Now
                };
                await _uow.Stoklar.SaveAsync(yeniStok);
                lastId = yeniStok.Id;
  
                if (EditAcilisBakiye != 0 && EditAcilisBakiye != null)
                {
                    await _uow.Stoklar.SaveHareketAsync(new StokHareket
                    {
                        StokId = yeniStok.Id,
                        StokKodu = yeniStok.StokKodu ?? "",
                        StokAdi = yeniStok.StokAdi ?? "",
                        Tarih = DateTime.Now,
                        IslemTuru = "GİRİŞ",
                        Miktar = Math.Abs(EditAcilisBakiye.Value),
                        Giren = EditAcilisBakiye.Value > 0 ? Math.Abs(EditAcilisBakiye.Value) : 0,
                        Cikan = EditAcilisBakiye.Value < 0 ? Math.Abs(EditAcilisBakiye.Value) : 0,
                        Aciklama = "Açılış Bakiyesi",
                        Fiyat = EditAlisFiyati ?? 0
                    });
                }
                await _uow.Stoklar.RecalculateCostsAsync(yeniStok.Id);
            }
            
            SuccessMessage = "Stok kartı başarıyla kaydedildi.";
            StatusMessage = $"Başarılı: Stok kartı '{EditStokAdi}' kaydedildi.";
            
            if (isNew)
            {
                SelectedStok = null;
                ClearEditForm();
                OnPropertyChanged(nameof(FormTitle));
                OnPropertyChanged(nameof(FormTitleColor));
                OnPropertyChanged(nameof(IsEditMode));
                await LoadStoklarAsync();
            }
            else
            {
                // Eğer bakiye 0 ise ve 'Bakiye Göster' filtresi açıksa, kullanıcının yeni eklediği kartı görebilmesi için filtreyi kapatıyoruz.
                if (OnlyWithBalance && EditAcilisBakiye == 0)
                {
                    OnlyWithBalance = false; // Bu işlem OnOnlyWithBalanceChanged üzerinden LoadStoklarAsync()'ı zaten tetikler.
                    await LoadStoklarAsync(lastId);
                }
                else
                {
                    await LoadStoklarAsync(lastId);
                }
            }
        }
        catch (Exception ex) 
        { 
            ErrorMessage = $"Hata: {ex.Message}";
            StatusMessage = $"Kaydetme Başarısız: {ex.Message}";
        }
    }

    [RelayCommand]
    public void OpenAddGroup() { NewGroupName = ""; IsAddGroupVisible = true; }

    [RelayCommand]
    public async Task SaveNewGroupAsync()
    {
        if (!string.IsNullOrWhiteSpace(NewGroupName))
        {
            var trimmed = NewGroupName.Trim();
            await _uow.Stoklar.SaveGrupAsync(trimmed);
            if (!GroupList.Contains(trimmed)) GroupList.Add(trimmed);
            EditKategori = trimmed;
        }
        IsAddGroupVisible = false;
    }

    [RelayCommand]
    public void CancelAddGroup() => IsAddGroupVisible = false;

    [RelayCommand]
    public void ManualIslem(string tur)
    {
        if (SelectedStok == null) return;
        IsEditingTransaction = false;
        TransactionType = tur?.ToUpper() ?? "GİRİŞ";
        IslemMiktar = 1;
        IslemFiyat = tur?.ToUpper() == "GİRİŞ" ? SelectedStok.AlisFiyati : SelectedStok.SatisFiyati;
        IslemAciklama = "";
        IslemTarihStr = DateTime.Now.ToString("dd.MM.yyyy");
        IsTransactionWindowVisible = true;
    }

    [RelayCommand]
    public void EditStokHareket()
    {
        if (SelectedStok == null || SelectedStokHareket == null) return;
        IsEditingTransaction = true;
        TransactionType = SelectedStokHareket.IslemTuru ?? "GİRİŞ";
        IslemMiktar = SelectedStokHareket.Miktar;
        IslemFiyat = SelectedStokHareket.Fiyat;
        IslemAciklama = SelectedStokHareket.Aciklama ?? "";
        IslemTarihStr = SelectedStokHareket.Tarih.ToString("dd.MM.yyyy");
        IsTransactionWindowVisible = true;
    }

    [RelayCommand]
    public async Task SaveTransactionAsync()
    {
        try 
        {
            if (SelectedStok == null) return;
            decimal miktar = IslemMiktar ?? 0;
            if (miktar <= 0) return;

            int selectedId = SelectedStok.Id;
            string tur = (TransactionType ?? "GİRİŞ").ToUpper();
            DateTime parsedTarih = GetParsedIslemTarih();

            if (IsEditingTransaction && SelectedStokHareket != null)
            {
                if (SelectedStokHareket.IslemTuru == "GİRİŞ") SelectedStok.Miktar -= SelectedStokHareket.Miktar;
                else SelectedStok.Miktar += SelectedStokHareket.Miktar;

                SelectedStokHareket.Tarih = parsedTarih;
                SelectedStokHareket.IslemTuru = tur;
                SelectedStokHareket.Miktar = miktar;
                SelectedStokHareket.Fiyat = IslemFiyat ?? 0;
                SelectedStokHareket.Aciklama = IslemAciklama;
                SelectedStokHareket.Giren = tur == "GİRİŞ" ? miktar : 0;
                SelectedStokHareket.Cikan = tur == "ÇIKIŞ" ? miktar : 0;

                if (tur == "GİRİŞ") SelectedStok.Miktar += miktar;
                else SelectedStok.Miktar -= miktar;

                await _uow.Stoklar.SaveHareketAsync(SelectedStokHareket);
            }
            else
            {
                var hareket = new StokHareket
                {
                    StokId = selectedId,
                    StokKodu = SelectedStok.StokKodu ?? "",
                    StokAdi = SelectedStok.StokAdi ?? "",
                    Tarih = parsedTarih,
                    IslemTuru = tur,
                    Miktar = miktar,
                    Fiyat = IslemFiyat ?? 0,
                    Aciklama = string.IsNullOrEmpty(IslemAciklama) ? $"Manuel {tur}" : IslemAciklama,
                    Giren = tur == "GİRİŞ" ? miktar : 0,
                    Cikan = tur == "ÇIKIŞ" ? miktar : 0
                };
                if (tur == "GİRİŞ") SelectedStok.Miktar += miktar;
                else SelectedStok.Miktar -= miktar;

                await _uow.Stoklar.SaveHareketAsync(hareket);
            }

            await _uow.Stoklar.SaveAsync(SelectedStok);
            await _uow.Stoklar.RecalculateCostsAsync(selectedId);
            IsTransactionWindowVisible = false;
            await LoadStoklarAsync(selectedId);
            await LoadStokHareketleriAsync(selectedId);
            FillEditForm(SelectedStok);
        }
        catch (Exception ex) { ErrorMessage = ex.Message; IsTransactionWindowVisible = false; }
    }

    [RelayCommand]
    public virtual async Task PrintStokList()
    {
        try
        {
            var pdfBytes = await _pdfService.GenerateStokListPdfBytesAsync(Stoklar.ToList());
            await HandleFileOpenAsync(pdfBytes, $"StokListesi_{DateTime.Now:ddMMyyyy_HHmmss}.pdf");
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    protected abstract Task HandleFileOpenAsync(byte[] content, string fileName);

    [RelayCommand]
    public void CloseTransactionWindow() => IsTransactionWindowVisible = false;

    [RelayCommand]
    public virtual async Task DeleteStokHareketAsync()
    {
        var hareket = SelectedStokHareket;
        var stok = SelectedStok;
        if (stok == null || hareket == null) return;
        
        int stokId = stok.Id;

        try
        {
            IsLoading = true;
            ErrorMessage = null;

            // 0. If linked to an invoice, cascade delete the invoice
            bool isInvoiceMovement = (hareket.IslemTuru != null && hareket.IslemTuru.Contains("Fatura")) ||
                                     (hareket.FaturaId.HasValue && hareket.FaturaId.Value > 0) ||
                                     (!string.IsNullOrEmpty(hareket.EvrakNo) && (hareket.EvrakNo.StartsWith("FTR") || hareket.EvrakNo.StartsWith("FAT")));

            if (isInvoiceMovement)
            {
                Fatura? linkedFatura = null;
                if (hareket.FaturaId.HasValue && hareket.FaturaId.Value > 0)
                {
                    linkedFatura = await _uow.Faturalar.GetByIdAsync(hareket.FaturaId.Value);
                }
                if (linkedFatura == null && !string.IsNullOrEmpty(hareket.EvrakNo))
                {
                    var allFaturalar = await _uow.Faturalar.GetAllAsync();
                    string eNo = hareket.EvrakNo.Trim();
                    linkedFatura = allFaturalar.FirstOrDefault(f => f.FaturaNo == eNo || f.FaturaNo.StartsWith(eNo) || eNo.StartsWith(f.FaturaNo));
                }

                if (linkedFatura != null)
                {
                    await _uow.Faturalar.DeleteAsync(linkedFatura);
                }
                else
                {
                    // Clean up any orphan CariHareket with matching EvrakNo or FaturaId
                    string eNo = hareket.EvrakNo?.Trim() ?? "";
                    int? fId = hareket.FaturaId;
                    var allCH = await _uow.Cariler.GetAllHareketlerAsync();
                    var orphanCH = allCH.Where(c => (fId.HasValue && fId.Value > 0 && c.FaturaId == fId.Value) || (!string.IsNullOrEmpty(eNo) && (c.EvrakNo == eNo || (c.EvrakNo != null && c.EvrakNo.Replace("KPL-", "") == eNo)))).ToList();
                    foreach (var ch in orphanCH)
                    {
                        await _uow.Cariler.DeleteHareketAsync(ch);
                    }
                }
            }

            // 1. Delete the movement
            await _uow.Stoklar.DeleteHareketAsync(hareket);

            // 2. Adjust stock quantity accurately from remaining movements
            var currentStok = await _uow.Stoklar.GetByIdAsync(stokId);
            if (currentStok != null)
            {
                var remainingMovements = await _uow.Stoklar.GetHareketlerAsync(stokId);
                if (!remainingMovements.Any())
                {
                    currentStok.Miktar = 0;
                    currentStok.OrtalamaAlisFiyati = 0;
                    currentStok.OrtalamaSatisFiyati = 0;
                    currentStok.AlisFiyati = 0;
                    currentStok.SatisFiyati = 0;
                }
                else
                {
                    decimal sumGiren = remainingMovements.Sum(h => h.Giren > 0 ? h.Giren : (h.Miktar > 0 && ((h.IslemTuru ?? "").Contains("Giriş", StringComparison.OrdinalIgnoreCase) || (h.IslemTuru ?? "").Contains("Alış", StringComparison.OrdinalIgnoreCase) || (h.IslemTuru ?? "").Contains("Açılış", StringComparison.OrdinalIgnoreCase)) ? h.Miktar : 0));
                    decimal sumCikan = remainingMovements.Sum(h => h.Cikan > 0 ? h.Cikan : (h.Miktar > 0 && ((h.IslemTuru ?? "").Contains("Çıkış", StringComparison.OrdinalIgnoreCase) || (h.IslemTuru ?? "").Contains("Satış", StringComparison.OrdinalIgnoreCase)) ? h.Miktar : 0));
                    currentStok.Miktar = sumGiren - sumCikan;
                }

                await _uow.Stoklar.SaveAsync(currentStok);
            }
            
            // 3. Trigger full cost recalculation to fix Average Price
            await _uow.Stoklar.RecalculateCostsAsync(stokId);

            SelectedStokHareket = null;
            await LoadStoklarAsync(stokId);
            await LoadStokHareketleriAsync(stokId);
            currentStok = await _uow.Stoklar.GetByIdAsync(stokId);
            if (currentStok != null)
            {
                SelectedStok = currentStok;
                FillEditForm(currentStok);
            }
            SuccessMessage = "Stok hareketi başarıyla silindi.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Stok hareketi silinirken hata oluştu: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public virtual async Task DeleteStokAsync()
    {
        if (SelectedStok == null) return;
        await _uow.Stoklar.DeleteAsync(SelectedStok);
        SelectedStok = null;
        await LoadStoklarAsync();
    }

    [RelayCommand]
    public async Task ExportToExcelAsync()
    {
        try
        {
            StatusMessage = "Excel listesi hazırlanıyor...";
            var allStoklar = await _uow.Stoklar.GetAllAsync();
            var listToExport = (allStoklar != null && allStoklar.Any()) ? allStoklar : Stoklar.ToList();
            
            if (!listToExport.Any())
            {
                ErrorMessage = "Aktarılacak stok kartı bulunamadı.";
                StatusMessage = "";
                return;
            }

            var dtoList = listToExport.Select(s => new StokExcelDto
            {
                StokKodu = s.StokKodu,
                StokAdi = s.StokAdi,
                Barkod = s.Barkod,
                Birim = s.Birim ?? "Adet",
                Kategori = s.Kategori ?? s.Grup ?? "",
                AlisFiyati = s.AlisFiyati,
                SatisFiyati = s.SatisFiyati,
                KDV = s.KDV,
                Miktar = s.Miktar,
                MinSeviye = s.MinSeviye,
                Aciklama = s.Aciklama
            }).ToList();

            var bytes = await _excelService.ExportListToMemoryAsync(dtoList, "Stok Listesi");
            await HandleFileOpenAsync(bytes, $"StokListesi_{DateTime.Now:ddMMyyyy_HHmmss}.xlsx");
            SuccessMessage = "Stok listesi başarıyla Excel olarak aktarıldı.";
            StatusMessage = SuccessMessage;
        }
        catch (Exception ex) 
        { 
            ErrorMessage = $"Excel aktarma hatası: {ex.Message}"; 
            StatusMessage = "";
        }
    }

    [RelayCommand]
    public virtual async Task DownloadTemplateAsync()
    {
        try
        {
            StatusMessage = "Şablon hazırlanıyor...";
            var template = new List<StokExcelDto>
            {
                new StokExcelDto
                {
                    StokKodu = "STK-001",
                    StokAdi = "Örnek Ürün Adı",
                    Barkod = "8690000000001",
                    Birim = "Adet",
                    Kategori = "Genel",
                    AlisFiyati = 50.00m,
                    SatisFiyati = 100.00m,
                    KDV = 20,
                    Miktar = 10,
                    MinSeviye = 5,
                    Aciklama = "Örnek stok açıklaması"
                }
            };
            var bytes = await _excelService.ExportListToMemoryAsync(template, "Stok_Sablon");
            
            await HandleFileOpenAsync(bytes, "Stok_Yukleme_Sablonu.xlsx");
            SuccessMessage = "Şablon başarıyla hazırlandı ve açıldı.";
            StatusMessage = SuccessMessage;
        }
        catch (Exception ex) 
        { 
            ErrorMessage = $"Şablon oluşturma hatası: {ex.Message}"; 
            StatusMessage = "";
        }
    }

    [RelayCommand]
    public async Task ImportExcelAsync()
    {
        try 
        {
             StatusMessage = "Excel dosyası seçiliyor...";
             var file = await _fileService.OpenFilePickerAsync("Excel Dosyası Seçin", new[] { "xlsx", "xls" });
             if (file == null)
             {
                 StatusMessage = "";
                 return;
             }

             StatusMessage = "Excel verileri okunuyor...";
             using (var stream = await file.OpenReadAsync())
             {
                 var list = await _excelService.ReadExcelAsync<StokKart>(stream);
                 if (list == null || !list.Any())
                 {
                     ErrorMessage = "Excel dosyasında geçerli stok verisi bulunamadı.";
                     StatusMessage = "";
                     return;
                 }

                 int addedCount = 0;
                 int updatedCount = 0;

                 foreach (var item in list)
                 {
                     if (string.IsNullOrWhiteSpace(item.StokKodu)) continue;
                     
                     var existing = await _uow.Stoklar.GetByKodAsync(item.StokKodu.Trim());
                     if (existing != null)
                     {
                         existing.StokAdi = !string.IsNullOrWhiteSpace(item.StokAdi) ? item.StokAdi : existing.StokAdi;
                         existing.Birim = !string.IsNullOrWhiteSpace(item.Birim) ? item.Birim : existing.Birim;
                         existing.Kategori = !string.IsNullOrWhiteSpace(item.Kategori) ? item.Kategori : existing.Kategori;
                         if (item.AlisFiyati > 0) existing.AlisFiyati = item.AlisFiyati;
                         if (item.SatisFiyati > 0) existing.SatisFiyati = item.SatisFiyati;
                         if (item.KDV > 0) existing.KDV = item.KDV;
                         if (item.Miktar != 0) existing.Miktar = item.Miktar;
                         if (item.MinSeviye > 0) existing.MinSeviye = item.MinSeviye;
                         if (!string.IsNullOrWhiteSpace(item.Aciklama)) existing.Aciklama = item.Aciklama;
                         if (!string.IsNullOrWhiteSpace(item.Barkod)) existing.Barkod = item.Barkod;
                         
                         await _uow.Stoklar.SaveAsync(existing);
                         updatedCount++;
                     }
                     else
                     {
                         item.StokKodu = item.StokKodu.Trim();
                         item.KayitTarihi = DateTime.Now;
                         await _uow.Stoklar.SaveAsync(item);
                         addedCount++;
                     }
                 }
                 
                 SuccessMessage = $"{addedCount} yeni stok eklendi, {updatedCount} stok güncellendi.";
                 StatusMessage = SuccessMessage;
             }
             await LoadStoklarAsync();
        }
        catch (Exception ex) 
        { 
            ErrorMessage = $"İçe Aktarma Hatası: {ex.Message}"; 
            StatusMessage = "";
        }
    }

    protected DateTime GetParsedIslemTarih()
    {
        if (DateTime.TryParseExact(IslemTarihStr, "dd.MM.yyyy", null, System.Globalization.DateTimeStyles.None, out var dt))
            return dt;
        return DateTime.Now;
    }

    [RelayCommand]
    public virtual async Task GenerateStokHareketleriReportAsync()
    {
        if (SelectedStok == null)
        {
            ErrorMessage = "Lütfen önce hareket raporunu almak istediğiniz stok kartını seçiniz.";
            return;
        }
        try
        {
            var list = await _uow.Stoklar.GetHareketlerAsync(SelectedStok.Id);
            var pdfBytes = await _pdfService.GenerateStokHareketleriPdfBytesAsync(SelectedStok, list);
            await HandleFileOpenAsync(pdfBytes, $"StokHareket_{SelectedStok.StokKodu}.pdf");
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    [RelayCommand]
    public async Task GenerateTopluStokReportAsync()
    {
        try
        {
            StatusMessage = "Rapor hazırlanıyor...";
            var allStoklar = await _uow.Stoklar.GetAllAsync();
            var pdfBytes = await _pdfService.GenerateStokListPdfBytesAsync(allStoklar);
            await HandleFileOpenAsync(pdfBytes, $"TopluStokRaporu_{DateTime.Now:ddMMyyyy_HHmmss}.pdf");
            StatusMessage = "";
        }
        catch (Exception ex) 
        { 
            ErrorMessage = $"Rapor oluşturma hatası: {ex.Message}";
        }
    }


    [RelayCommand]
    public async Task RecalculateCostsAsync()
    {
         if (IsLoading) return;
         try 
         {
             StatusMessage = "Maliyetler hesaplanıyor...";
             IsLoading = true;
             await _uow.Stoklar.RecalculateCostsAsync();
             await LoadStoklarAsync();
             StatusMessage = "Maliyet hesaplama tamamlandı.";
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

    protected override Task InvokeOnUIThreadAsync(Action action)
    {
        action();
        return Task.CompletedTask;
    }
}
