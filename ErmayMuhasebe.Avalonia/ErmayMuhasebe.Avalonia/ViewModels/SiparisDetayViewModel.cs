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

public partial class SiparisItemViewModel : ObservableObject
{
    private decimal? _miktar = 1;
    private decimal? _birimFiyat;
    private decimal? _kdvOrani = 10;

    private string _birim = "Adet";

    public StokKart Stok { get; }

    public SiparisItemViewModel(StokKart stok)
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

    public string? Aciklama { get; set; } // Added Property
    public string? MiktarAciklama { get; set; } // Added Property

    public event Action? AmountChanged;
    private void OnAmountChanged() 
    {
        OnPropertyChanged(nameof(Tutar));
        OnPropertyChanged(nameof(KdvTutari));
        OnPropertyChanged(nameof(GenelToplam));
        AmountChanged?.Invoke();
    }
}

public partial class SiparisDetayViewModel : ViewModelBase, IHandleBack
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
    
    public string Title => $"Sipariş - {Cari?.Unvan ?? "Seçilmedi"}";
    public string HeaderTitle => "SİPARİŞ FORMU";

    [ObservableProperty] private string _siparisNo;
    [ObservableProperty] private DateTime? _tarih = DateTime.Now;
    [ObservableProperty] private string _tarihStr = DateTime.Now.ToString("dd.MM.yyyy");
    
    [ObservableProperty] private DateTime? _teslimatTarihi = DateTime.Now.AddDays(7);
    [ObservableProperty] private string _teslimatTarihiStr = DateTime.Now.AddDays(7).ToString("dd.MM.yyyy");

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

    partial void OnTeslimatTarihiChanged(DateTime? value)
    {
        if (value.HasValue)
        {
            var str = value.Value.ToString("dd.MM.yyyy");
            if (TeslimatTarihiStr != str)
            {
                TeslimatTarihiStr = str;
            }
        }
    }

    partial void OnTeslimatTarihiStrChanged(string value)
    {
        if (DateTime.TryParseExact(value, "dd.MM.yyyy", null, System.Globalization.DateTimeStyles.None, out var dt))
        {
            if (TeslimatTarihi != dt)
            {
                TeslimatTarihi = dt;
            }
        }
    }
    
    [ObservableProperty] private string _aciklama = "";
    [ObservableProperty] private string _pdfNotlar = "";
    [ObservableProperty] private string _odemeBilgisi = "";

    [ObservableProperty] private decimal _araToplam;
    [ObservableProperty] private decimal _kdvToplam;
    [ObservableProperty] private decimal _genelToplam;

    public int SiparisId { get; set; } = 0;

    public ObservableCollection<SiparisItemViewModel> Items { get; } = new();
    [ObservableProperty] private SiparisItemViewModel? _selectedItem;

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


    public SiparisDetayViewModel(IUnitOfWork uow, CariKart? cari)
    {
        _uow = uow;
        Cari = cari ?? new CariKart();
        SiparisNo = $"SIP-{DateTime.Now:yyyyMMddHHmmss}";
        TarihStr = DateTime.Now.ToString("dd.MM.yyyy");
        TeslimatTarihiStr = DateTime.Now.AddDays(7).ToString("dd.MM.yyyy");
    }

    public async Task LoadFromExistingAsync(Siparis siparis, List<SiparisDetay> detaylar)
    {
        SiparisId = siparis.Id;
        SiparisNo = siparis.SiparisNo ?? "";
        Tarih = siparis.Tarih;
        TarihStr = siparis.Tarih.ToString("dd.MM.yyyy");
        
        if (siparis.TeslimatTarihi.HasValue)
        {
            TeslimatTarihi = siparis.TeslimatTarihi.Value;
            TeslimatTarihiStr = siparis.TeslimatTarihi.Value.ToString("dd.MM.yyyy");
        }
        
        Aciklama = siparis.Aciklama ?? "";
        PdfNotlar = siparis.PdfNotlar ?? "";
        OdemeBilgisi = siparis.OdemeBilgisi ?? "";
        Cari = new CariKart { Id = siparis.CariId, Unvan = siparis.CariUnvan };
        
        Items.Clear();
        foreach(var d in detaylar)
        {
            var stok = await _uow.Stoklar.GetByIdAsync(d.StokId);
            if(stok == null)
            {
               // Fallback if deleted
               stok = new StokKart { Id = d.StokId, StokKodu = "", StokAdi = d.StokAdi, Birim = d.Birim };
            }

            var item = new SiparisItemViewModel(stok);
            item.Miktar = (decimal)d.Miktar;
            if (!string.IsNullOrWhiteSpace(d.Birim)) item.Birim = d.Birim;
            item.BirimFiyat = d.BirimFiyat;
            item.Aciklama = d.Aciklama; // Map Aciklama
            item.MiktarAciklama = d.MiktarAciklama; // Map MiktarAciklama
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
        
        var item = new SiparisItemViewModel(SelectedStokForAdd);
        item.AmountChanged += CalculateTotals;
        Items.Add(item);
        CalculateTotals();
        IsStokSecimVisible = false;
        
        // Reset selection
        SelectedStokForAdd = null;
    }

    [RelayCommand]
    public void RemoveItem(SiparisItemViewModel item)
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
    public async Task SaveSiparisAsync()
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
            var siparis = new Siparis
            {
                Id = SiparisId,
                CariId = Cari.Id,
                CariUnvan = Cari.Unvan,
                SiparisNo = SiparisNo,
                Tarih = GetParsedDate(TarihStr),
                TeslimatTarihi = GetParsedDate(TeslimatTarihiStr),
                Durum = "Bekliyor",
                Aciklama = Aciklama,
                PdfNotlar = PdfNotlar,
                OdemeBilgisi = OdemeBilgisi,
                GenelToplam = GenelToplam
            };
            
            var detaylar = Items.Select(i => new SiparisDetay
            {
                StokId = i.Stok.Id,
                StokAdi = i.Ad,
                Miktar = (decimal)(i.Miktar ?? 0),
                Birim = i.Birim,
                BirimFiyat = i.BirimFiyat ?? 0,
                Tutar = i.Tutar,
                KdvOrani = (double)(i.KdvOrani ?? 0),
                Aciklama = i.Aciklama ?? "", // Map formatted Aciklama
                MiktarAciklama = i.MiktarAciklama ?? "" // Map MiktarAciklama
            }).ToList();
            
            // If Cari is new (Id=0), save it first!
            if (Cari.Id == 0)
            {
                await _uow.Cariler.SaveAsync(Cari);
                siparis.CariId = Cari.Id;
            }

            await _uow.Siparisler.SaveWithDetailsAsync(siparis, detaylar);
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
