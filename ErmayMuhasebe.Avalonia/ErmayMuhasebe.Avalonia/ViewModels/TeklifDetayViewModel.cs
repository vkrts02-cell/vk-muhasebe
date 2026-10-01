using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ErmayMuhasebe.Avalonia.Messages;
using ErmayMuhasebe.Models;
using ErmayMuhasebe.Services;
using ErmayMuhasebe.Repositories;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ErmayMuhasebe.Avalonia.ViewModels;

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

    public static readonly string[] BirimListesi = new[] { "Adet", "Kg", "Mt", "M2" };
    public string[] Birimler => BirimListesi;

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

public partial class TeklifDetayViewModel : ViewModelBase, IHandleBack
{
    public bool HandleBack()
    {
        if (IsStokSecimVisible)
        {
            CloseStokSecim();
            return true;
        }

        if (IsCariSecimVisible)
        {
            IsCariSecimVisible = false;
            return true;
        }
        
        return false; 
    }
    private readonly IUnitOfWork _uow;
    [ObservableProperty] private CariKart? _cari;
    
    public string Title => $"Teklif - {Cari?.Unvan ?? "Seçilmedi"}";
    public string HeaderTitle => "TEKLİF FORMU";

    [ObservableProperty] private string _teklifNo;
    [ObservableProperty] private DateTime? _tarih = DateTime.Now;
    [ObservableProperty] private string _tarihStr = DateTime.Now.ToString("dd.MM.yyyy");

    partial void OnTarihChanged(DateTime? value)
    {
        if (value.HasValue)
        {
            var str = value.Value.ToString("dd.MM.yyyy");
            if (TarihStr != str)
            {
                TarihStr = str;
            }
        }
    }

    partial void OnTarihStrChanged(string value)
    {
        if (DateTime.TryParseExact(value, "dd.MM.yyyy", null, System.Globalization.DateTimeStyles.None, out var dt))
        {
            if (Tarih != dt)
            {
                Tarih = dt;
            }
        }
    }
    
    [ObservableProperty] private string _aciklama = "";
    [ObservableProperty] private string _odemeBilgisi = "";

    [ObservableProperty] private decimal _araToplam;
    [ObservableProperty] private decimal _kdvToplam;
    [ObservableProperty] private decimal _genelToplam;

    public int TeklifId { get; set; } = 0;

    public ObservableCollection<TeklifItemViewModel> Items { get; } = new();
    [ObservableProperty] private TeklifItemViewModel? _selectedItem;

    // Stok Seçim Dialog
    [ObservableProperty] private bool _isStokSecimVisible;
    [ObservableProperty] private ObservableCollection<StokKart> _stokListesi = new();
    [ObservableProperty] private StokKart? _selectedStokForAdd;
    [ObservableProperty] private string _stokSearchText = "";

    // Cari Seçim Dialog
    [ObservableProperty] private bool _isCariSecimVisible;
    [ObservableProperty] private ObservableCollection<CariKart> _cariListesi = new();
    [ObservableProperty] private CariKart? _selectedCariForAdd;
    [ObservableProperty] private string _cariSearchText = "";

    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private bool _isSaving;


    public TeklifDetayViewModel(IUnitOfWork uow, CariKart? cari)
    {
        _uow = uow;
        Cari = cari ?? new CariKart();
        TeklifNo = $"TKF-{DateTime.Now:yyyyMMddHHmmss}";
        TarihStr = DateTime.Now.ToString("dd.MM.yyyy");
    }

    public async Task LoadFromExistingAsync(Teklif teklif, List<TeklifDetay> detaylar)
    {
        TeklifId = teklif.Id;
        TeklifNo = teklif.TeklifNo ?? "";
        Tarih = teklif.Tarih;
        TarihStr = teklif.Tarih.ToString("dd.MM.yyyy");
        Aciklama = teklif.Aciklama ?? "";
        OdemeBilgisi = teklif.OdemeBilgisi ?? "";
        Cari = new CariKart { Id = teklif.CariId, Unvan = teklif.CariUnvan };
        
        Items.Clear();
        foreach(var d in detaylar)
        {
            var stok = await _uow.Stoklar.GetByIdAsync(d.StokId);
            if (stok == null)
            {
                // Fallback if deleted
                stok = new StokKart { Id = d.StokId, StokKodu = "", StokAdi = d.StokAdi };
            }

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
        
        // Reset selection
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
            StatusMessage = "HATA: Cari seçilmeli!";
            return;
        }

        if (!Items.Any()) 
        {
            StatusMessage = "HATA: En az bir ürün olmalı!";
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
                Tarih = GetParsedDate(TarihStr),
                Durum = "Bekliyor",
                Aciklama = Aciklama,
                OdemeBilgisi = OdemeBilgisi,
                GenelToplam = GenelToplam
            };
            
            var detaylar = Items.Select(i => new TeklifDetay
            {
                StokId = i.Stok.Id,
                StokAdi = i.Ad,
                Miktar = (decimal)(i.Miktar ?? 0),
                Birim = i.Birim,
                BirimFiyat = i.BirimFiyat ?? 0,
                Tutar = i.Tutar,
                KdvOrani = (double)(i.KdvOrani ?? 0),
                Aciklama = i.Aciklama ?? ""
            }).ToList();
            
            // If Cari is new (Id=0), save it first!
            if (Cari.Id == 0)
            {
                await _uow.Cariler.SaveAsync(Cari);
                teklif.CariId = Cari.Id;
            }

            await _uow.Teklifler.SaveWithDetailsAsync(teklif, detaylar);
            StatusMessage = "Başarıyla kaydedildi.";
            
            // Wait a moment for UX so user sees "Saved" then go back
            await Task.Delay(700);

            // Success: Notify and Go Back
                OnRequestClose();
        }
        catch (Exception ex)
        {
            StatusMessage = $"HATA: {ex.Message}";
        }
        finally
        {
            IsSaving = false;
        }
    }
    
    [RelayCommand]
    public void Cancel() => OnRequestClose();

    private DateTime GetParsedDate(string str)
    {
        if (DateTime.TryParseExact(str, "dd.MM.yyyy", null, System.Globalization.DateTimeStyles.None, out var dt))
            return dt;
        return DateTime.Now;
    }
}
