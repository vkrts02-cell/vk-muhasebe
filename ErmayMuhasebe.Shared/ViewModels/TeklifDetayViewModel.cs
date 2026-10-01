using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ErmayMuhasebe.Models;
using ErmayMuhasebe.Services;
using ErmayMuhasebe.Repositories;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ErmayMuhasebe.Shared.ViewModels;

public partial class TeklifItemViewModel : ObservableObject
{
    private decimal? _miktar = 1;
    private decimal? _birimFiyat;
    private decimal? _kdvOrani = 10;

    private string _birim = "Adet";

    public StokKart Stok { get; }

    public TeklifItemViewModel(StokKart stok)
    {
        Stok = stok;
        BirimFiyat = stok.SatisFiyati;
        KdvOrani = 10;
        _birim = string.IsNullOrWhiteSpace(stok?.Birim) ? "Adet" : stok.Birim;
    }

    public string Kod => Stok.StokKodu ?? "";
    public string Ad => Stok.StokAdi ?? "";
    public string Birim
    {
        get => _birim;
        set => SetProperty(ref _birim, value);
    }

    public decimal? Miktar
    {
        get => _miktar;
        set { if (SetProperty(ref _miktar, value)) OnAmountChanged(); }
    }

    public decimal? BirimFiyat
    {
        get => _birimFiyat;
        set { if (SetProperty(ref _birimFiyat, value)) OnAmountChanged(); }
    }

    public decimal? KdvOrani
    {
        get => _kdvOrani;
        set { if (SetProperty(ref _kdvOrani, value)) OnAmountChanged(); }
    }

    public decimal Tutar => (Miktar ?? 0) * (BirimFiyat ?? 0);
    public decimal KdvTutari => Tutar * ((KdvOrani ?? 0) / 100m);
    public decimal GenelToplam => Tutar + KdvTutari;

    public string? Aciklama { get; set; }

    public event Action? AmountChanged;
    private void OnAmountChanged() 
    {
        OnPropertyChanged(nameof(Tutar));
        OnPropertyChanged(nameof(KdvTutari));
        OnPropertyChanged(nameof(GenelToplam));
        AmountChanged?.Invoke();
    }
}

public partial class TeklifDetayViewModel : ViewModelBase
{
    protected readonly IUnitOfWork _uow;
    [ObservableProperty] private CariKart? _cari;
    
    public string Title => $"Teklif - {Cari?.Unvan ?? "Seçilmedi"}";
    public string HeaderTitle => "TEKLİF FORMU";

    [ObservableProperty] private string _teklifNo = "";
    [ObservableProperty] private DateTime _tarih = DateTime.Now;
    
    [ObservableProperty] private string _aciklama = "";
    [ObservableProperty] private string _odemeBilgisi = "";

    [ObservableProperty] private decimal _araToplam;
    [ObservableProperty] private decimal _kdvToplam;
    [ObservableProperty] private decimal _genelToplam;

    public int TeklifId { get; set; } = 0;

    public ObservableCollection<TeklifItemViewModel> Items { get; } = new();

    // Stok Seçim
    [ObservableProperty] private bool _isStokSecimVisible;
    [ObservableProperty] private ObservableCollection<StokKart> _stokListesi = new();
    [ObservableProperty] private StokKart? _selectedStokForAdd;
    [ObservableProperty] private string _stokSearchText = "";

    // Cari Seçim
    [ObservableProperty] private bool _isCariSecimVisible;
    [ObservableProperty] private ObservableCollection<CariKart> _cariListesi = new();
    [ObservableProperty] private CariKart? _selectedCariForAdd;
    [ObservableProperty] private string _cariSearchText = "";

    [ObservableProperty] private bool _isSaving;
    [ObservableProperty] private TeklifItemViewModel? _selectedItem;

    public TeklifDetayViewModel(IUnitOfWork uow)
    {
        _uow = uow;
        Cari = new CariKart();
        TeklifNo = $"TKF-{DateTime.Now:yyyyMMddHHmmss}";
    }

    public async Task LoadFromExistingAsync(int id)
    {
        IsLoading = true;
        try
        {
            var teklif = await _uow.Teklifler.GetByIdAsync(id);
            if (teklif == null) return;

            var detaylar = await _uow.Teklifler.GetDetaylarAsync(id);

            TeklifId = teklif.Id;
            TeklifNo = teklif.TeklifNo ?? "";
            Tarih = teklif.Tarih;
            Aciklama = teklif.Aciklama ?? "";
            OdemeBilgisi = teklif.OdemeBilgisi ?? "";
            Cari = new CariKart { Id = teklif.CariId, Unvan = teklif.CariUnvan };
            
            Items.Clear();
            foreach(var d in detaylar)
            {
                var stok = await _uow.Stoklar.GetByIdAsync(d.StokId);
                if (stok == null)
                    stok = new StokKart { Id = d.StokId, StokKodu = "", StokAdi = d.StokAdi };

                var item = new TeklifItemViewModel(stok);
                item.Miktar = (decimal)d.Miktar;
                if (!string.IsNullOrWhiteSpace(d.Birim)) item.Birim = d.Birim;
                item.BirimFiyat = d.BirimFiyat;
                item.Aciklama = d.Aciklama;
                item.AmountChanged += CalculateTotals;
                Items.Add(item);
            }
            CalculateTotals();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Yükleme Hatası: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public void OpenStokSecim()
    {
        _ = LoadStoklarAsync();
        IsStokSecimVisible = true;
    }

    [RelayCommand]
    public void CloseStokSecim() => IsStokSecimVisible = false;

    private async Task LoadStoklarAsync()
    {
        var list = await _uow.Stoklar.GetAllAsync();
        if(!string.IsNullOrEmpty(StokSearchText))
            list = list.Where(s => (s.StokAdi ?? "").Contains(StokSearchText, StringComparison.OrdinalIgnoreCase)).ToList();
        
        StokListesi = new ObservableCollection<StokKart>(list);
    }
    
    [RelayCommand]
    public void SearchStok() => _ = LoadStoklarAsync();

    [RelayCommand]
    public void AddStokToGrid()
    {
        if(SelectedStokForAdd == null) return;
        
        var item = new TeklifItemViewModel(SelectedStokForAdd);
        item.AmountChanged += CalculateTotals;
        Items.Add(item);
        CalculateTotals();
        IsStokSecimVisible = false;
        SelectedStokForAdd = null;
    }

    [RelayCommand]
    public void RemoveItem(TeklifItemViewModel item)
    {
        item.AmountChanged -= CalculateTotals;
        Items.Remove(item);
        CalculateTotals();
    }

    [RelayCommand]
    public void RemoveSelectedItem()
    {
        var itemToRemove = SelectedItem ?? Items.LastOrDefault();
        if (itemToRemove != null)
        {
            RemoveItem(itemToRemove);
            SelectedItem = null;
        }
    }

    private void CalculateTotals()
    {
        AraToplam = Items.Sum(i => i.Tutar);
        KdvToplam = Items.Sum(i => i.KdvTutari);
        GenelToplam = Items.Sum(i => i.GenelToplam);
    }

    [RelayCommand]
    public void OpenCariSecim()
    {
        _ = LoadCarilerAsync();
        IsCariSecimVisible = true;
    }

    [RelayCommand]
    public void CloseCariSecim() => IsCariSecimVisible = false;

    private async Task LoadCarilerAsync()
    {
        var list = await _uow.Cariler.GetAllAsync();
        if (!string.IsNullOrEmpty(CariSearchText))
            list = list.Where(c => (c.Unvan ?? "").Contains(CariSearchText, StringComparison.OrdinalIgnoreCase)).ToList();

        CariListesi = new ObservableCollection<CariKart>(list);
    }

    [RelayCommand]
    public void SearchCari() => _ = LoadCarilerAsync();

    [RelayCommand]
    public void SelectCari()
    {
        if (SelectedCariForAdd == null) return;
        Cari = SelectedCariForAdd;
        IsCariSecimVisible = false;
        OnPropertyChanged(nameof(Title));
    }

    [RelayCommand]
    public async Task SaveTeklifAsync()
    {
        if (IsSaving) return;
        if (Cari == null || string.IsNullOrWhiteSpace(Cari.Unvan))
        {
            ErrorMessage = "HATA: Cari seçilmeli!";
            return;
        }

        if (!Items.Any()) 
        {
            ErrorMessage = "HATA: En az bir ürün olmalı!";
            return;
        }

        IsSaving = true;
        try
        {
            var teklif = new Teklif
            {
                Id = TeklifId,
                CariId = Cari.Id,
                CariUnvan = Cari.Unvan,
                TeklifNo = TeklifNo,
                Tarih = Tarih,
                Durum = "Bekliyor",
                Aciklama = Aciklama,
                OdemeBilgisi = OdemeBilgisi,
                GenelToplam = GenelToplam,
                KayitTarihi = DateTime.Now
            };
            
            var detaylar = Items.Select(i => new TeklifDetay
            {
                StokId = i.Stok.Id,
                StokAdi = i.Ad,
                Miktar = i.Miktar ?? 0m,
                Birim = i.Birim,
                BirimFiyat = i.BirimFiyat ?? 0,
                Tutar = i.Tutar,
                KdvOrani = (double)(i.KdvOrani ?? 0),
                Aciklama = i.Aciklama ?? ""
            }).ToList();
            
            if (Cari.Id == 0)
            {
                await _uow.Cariler.SaveAsync(Cari);
                teklif.CariId = Cari.Id;
            }

            await _uow.Teklifler.SaveWithDetailsAsync(teklif, detaylar);
            SuccessMessage = "Başarıyla kaydedildi.";
            
            await Task.Delay(700);
            OnRequestClose();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"HATA: {ex.Message}";
        }
        finally
        {
            IsSaving = false;
        }
    }
    
    [RelayCommand]
    public void Cancel() => OnRequestClose();
}
