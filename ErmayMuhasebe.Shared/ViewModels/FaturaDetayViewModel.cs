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

public partial class FaturaItemViewModel : ObservableObject
{
    private decimal? _miktar = 1;
    private decimal? _birimFiyat;
    private decimal? _kdvOrani = 10;
    private decimal? _iskontoOrani;

    private string _birim = "Adet";

    public StokKart Stok { get; }

    public FaturaItemViewModel(StokKart stok)
    {
        Stok = stok;
        BirimFiyat = 0;
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

    public decimal? IskontoOrani
    {
        get => _iskontoOrani;
        set { if (SetProperty(ref _iskontoOrani, value)) OnAmountChanged(); }
    }

    public decimal IskontoTutari => ((Miktar ?? 0) * (BirimFiyat ?? 0)) * ((IskontoOrani ?? 0) / 100m);

    public decimal? KdvOrani
    {
        get => _kdvOrani;
        set { if (SetProperty(ref _kdvOrani, value)) OnAmountChanged(); }
    }

    public decimal Tutar => ((Miktar ?? 0) * (BirimFiyat ?? 0)) - IskontoTutari;
    public decimal KdvTutari => Math.Max(0, Tutar) * ((KdvOrani ?? 0) / 100m);
    public decimal GenelToplam => Tutar + KdvTutari;
    
    public string? Aciklama { get; set; }

    public event Action? AmountChanged;
    private void OnAmountChanged() 
    {
        OnPropertyChanged(nameof(IskontoTutari));
        OnPropertyChanged(nameof(Tutar));
        OnPropertyChanged(nameof(KdvTutari));
        OnPropertyChanged(nameof(GenelToplam));
        AmountChanged?.Invoke();
    }
}

public abstract partial class FaturaDetayViewModel : ViewModelBase
{
    protected readonly IUnitOfWork _uow;
    protected readonly IPdfService _pdfService;

    [ObservableProperty] private CariKart? _cari;
    [ObservableProperty] private string _faturaTuru = "Satış";
    [ObservableProperty] private string _faturaNo = "";
    [ObservableProperty] private DateTime? _tarih = DateTime.Now;
    [ObservableProperty] private string _tarihStr = DateTime.Now.ToString("dd.MM.yyyy");
    [ObservableProperty] private DateTime? _vadeTarihi = DateTime.Now;
    [ObservableProperty] private string _vadeTarihiStr = DateTime.Now.ToString("dd.MM.yyyy");
    [ObservableProperty] private string _aciklama = "";
    
    partial void OnCariChanged(CariKart? value) => CalculateVadeFromOdemePlani();

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
        CalculateVadeFromOdemePlani();
    }

    partial void OnVadeTarihiChanged(DateTime? value)
    {
        if (value.HasValue)
        {
            var str = value.Value.ToString("dd.MM.yyyy");
            if (VadeTarihiStr != str)
            {
                VadeTarihiStr = str;
            }
        }
    }

    partial void OnVadeTarihiStrChanged(string value)
    {
        if (DateTime.TryParseExact(value, "dd.MM.yyyy", null, System.Globalization.DateTimeStyles.None, out var dt))
        {
            if (VadeTarihi != dt)
            {
                VadeTarihi = dt;
            }
        }
    }

    [ObservableProperty] private decimal _araToplam;
    [ObservableProperty] private decimal _kdvToplam;
    [ObservableProperty] private decimal _genelToplam;
    [ObservableProperty] private int _faturaId;

    [ObservableProperty] private string _odemeSekli = "Açık Hesap";
    [ObservableProperty] private int? _selectedKasaId;
    [ObservableProperty] private int? _selectedBankaId;
    [ObservableProperty] private ObservableCollection<BankaKart> _kasalar = new();
    [ObservableProperty] private ObservableCollection<BankaKart> _bankalar = new();

    public bool IsKasaVisible => OdemeSekli == "Nakit";
    public bool IsBankaVisible => OdemeSekli == "Kredi Kartı";

    partial void OnOdemeSekliChanged(string value)
    {
        OnPropertyChanged(nameof(IsKasaVisible));
        OnPropertyChanged(nameof(IsBankaVisible));
    }

    // Fatura Ayarları
    [ObservableProperty] private bool _settKasa = true;
    [ObservableProperty] private bool _settBilgiFisi = false;
    [ObservableProperty] private bool _settStokFiyat = false;
    [ObservableProperty] private bool _settCariIsle = true;
    [ObservableProperty] private bool _settStokIsle = true;
    [ObservableProperty] private bool _settUretim = false;
    public ObservableCollection<FaturaItemViewModel> Items { get; } = new();

    // Dialog flags
    [ObservableProperty] private bool _isStokSecimVisible;
    [ObservableProperty] private bool _isCariSecimVisible;
    [ObservableProperty] private ObservableCollection<StokKart> _stokListesi = new();
    [ObservableProperty] private ObservableCollection<CariKart> _cariListesi = new();
    [ObservableProperty] private string _stokSearchText = "";
    [ObservableProperty] private string _cariSearchText = "";
    [ObservableProperty] private StokKart? _selectedStokForAdd;
    [ObservableProperty] private CariKart? _selectedCariForAdd;
    [ObservableProperty] private FaturaItemViewModel? _selectedItem;

    public FaturaDetayViewModel(IUnitOfWork uow, IPdfService pdfService)
    {
        _uow = uow;
        _pdfService = pdfService;
        FaturaNo = $"FTR-{DateTime.Now:yyyyMMddHHmmss}";
    }

    public async Task InitializeAsync(int? faturaId, int? cariId, string tur)
    {
        FaturaTuru = (tur == "Satis" ? "Satış" : (tur == "Alis" ? "Alış" : tur));
        
        // Auto-enable price updates for Purchase Invoices
        if (FaturaTuru == "Alış") 
        {
            SettStokFiyat = true;
        }
        
        if (faturaId.HasValue && faturaId > 0)
        {
            var fatura = await _uow.Faturalar.GetByIdAsync(faturaId.Value);
            if (fatura != null)
            {
                var detaylar = await _uow.Faturalar.GetDetaylarAsync(faturaId.Value);
                LoadFromExisting(fatura, detaylar);
            }
        }
        else if (cariId.HasValue && cariId > 0)
        {
            Cari = await _uow.Cariler.GetByIdAsync(cariId.Value);
        }

        await LoadKasalarAndBankalarAsync();
    }

    public async Task LoadKasalarAndBankalarAsync()
    {
        var allAccounts = await _uow.Bankalar.GetAllAsync();
        Kasalar = new ObservableCollection<BankaKart>(allAccounts.Where(x => x.KartTuru == "Kasa"));
        Bankalar = new ObservableCollection<BankaKart>(allAccounts.Where(x => x.KartTuru != "Kasa"));
        
        if (SelectedKasaId == null && Kasalar.Any()) SelectedKasaId = Kasalar.First().Id;
        if (SelectedBankaId == null && Bankalar.Any()) SelectedBankaId = Bankalar.First().Id;
    }

    public void LoadFromExisting(Fatura fatura, List<FaturaDetay> detaylar)
    {
        FaturaId = fatura.Id;
        FaturaNo = fatura.FaturaNo ?? "";
        Tarih = fatura.Tarih;
        TarihStr = fatura.Tarih.ToString("dd.MM.yyyy");
        VadeTarihi = fatura.VadeTarihi;
        VadeTarihiStr = fatura.VadeTarihi.ToString("dd.MM.yyyy");
        Aciklama = fatura.Aciklama ?? "";
        Cari = new CariKart { Id = fatura.CariId, Unvan = fatura.CariUnvan };
        OdemeSekli = fatura.OdemeSekli ?? "Açık Hesap";
        SelectedKasaId = fatura.KasaId;
        SelectedBankaId = fatura.BankaId;
        
        Items.Clear();
        foreach(var d in detaylar)
        {
            var item = new FaturaItemViewModel(new StokKart { Id = d.StokId, StokKodu = d.StokKodu, StokAdi = d.StokAdi });
            item.Miktar = (decimal)d.Miktar;
            item.BirimFiyat = d.BirimFiyat;
            item.KdvOrani = d.KDVOrani;
            item.Aciklama = d.Aciklama;
            item.AmountChanged += CalculateTotals;
            Items.Add(item);
        }
        CalculateTotals();
    }

    [RelayCommand]
    public async Task SearchStokAsync()
    {
        var list = await _uow.Stoklar.GetAllAsync();
        if(!string.IsNullOrEmpty(StokSearchText))
            list = list.Where(s => (s.StokAdi ?? "").Contains(StokSearchText, StringComparison.OrdinalIgnoreCase)).ToList();
        
        StokListesi = new ObservableCollection<StokKart>(list);
    }

    [RelayCommand]
    public void AddStokToGrid()
    {
        if(SelectedStokForAdd == null) return;
        var item = new FaturaItemViewModel(SelectedStokForAdd);
        item.AmountChanged += CalculateTotals;
        Items.Add(item);
        CalculateTotals();
        IsStokSecimVisible = false;
        SelectedStokForAdd = null;
    }

    [RelayCommand]
    public void RemoveItem(FaturaItemViewModel item)
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

    protected void CalculateTotals()
    {
        AraToplam = Items.Sum(i => i.Tutar);
        KdvToplam = Items.Sum(i => i.KdvTutari);
        GenelToplam = Items.Sum(i => i.GenelToplam);
    }

    public string HeaderTitle => $"{FaturaTuru?.ToUpper()} FATURASI";
    public string HeaderColor => FaturaTuru == "Satış" ? "#3b82f6" : "#f59e0b"; // Blue for Sales, Orange for Purchase (Alış) matching the UI requests

    partial void OnFaturaTuruChanged(string value)
    {
        OnPropertyChanged(nameof(HeaderTitle));
        OnPropertyChanged(nameof(HeaderColor));
    }

    [RelayCommand]
    public async Task SearchCariAsync()
    {
        var list = await _uow.Cariler.GetAllAsync();
        
        // Filter by Type
        if (FaturaTuru == "Alış")
            list = list.Where(c => c.Grup == "Tedarikçi").ToList();
        else if (FaturaTuru == "Satış")
            list = list.Where(c => c.Grup == "Müşteri").ToList();

        if (!string.IsNullOrEmpty(CariSearchText))
            list = list.Where(c => (c.Unvan ?? "").Contains(CariSearchText, StringComparison.OrdinalIgnoreCase)).ToList();

        CariListesi = new ObservableCollection<CariKart>(list);
    }

    [RelayCommand]
    public void SelectCari()
    {
        if (SelectedCariForAdd == null) return;
        Cari = SelectedCariForAdd;
        IsCariSecimVisible = false;
    }

    [RelayCommand]
    public async Task SaveFaturaAsync()
    {
        if (Cari == null) { ErrorMessage = "Lütfen bir cari hesap seçiniz."; return; }
        if (!Items.Any()) { ErrorMessage = "Faturada en az bir ürün olmalı!"; return; }

        // RISK LIMIT CHECK
        if (Cari.RiskTakibiYapilsin || Cari.FaturadaRiskKontrolu)
        {
            var freshCari = await _uow.Cariler.GetByIdAsync(Cari.Id);
            if (freshCari != null && freshCari.RiskLimiti > 0)
            {
                decimal currentBalance = freshCari.Borc - freshCari.Alacak;
                bool isSatis = (FaturaTuru ?? "").Equals("Satış", StringComparison.OrdinalIgnoreCase);
                decimal impact = isSatis ? GenelToplam : -GenelToplam;
                decimal projected = currentBalance + impact;

                bool isSupplier = (freshCari.Tur ?? "").Contains("Sat", StringComparison.OrdinalIgnoreCase) || 
                                 (freshCari.Tur ?? "").Contains("Tedar", StringComparison.OrdinalIgnoreCase);

                bool isLimitExceeded = (!isSupplier && projected > freshCari.RiskLimiti) || 
                                      (isSupplier && (-projected) > freshCari.RiskLimiti);

                if (isLimitExceeded)
                {
                    if (freshCari.FaturadaRiskKontrolu)
                    {
                        ErrorMessage = $"RİSK LİMİTİ AŞILDI! İşlem engellendi.\nLimit: {freshCari.RiskLimiti:N2}, Tahmin: {(isSupplier ? -projected : projected):N2}";
                        return;
                    }
                    else if (freshCari.RiskTakibiYapilsin)
                    {
                        WarningMessage = $"UYARI: Risk Limiti aşıldı! (Limit: {freshCari.RiskLimiti:N2})";
                        // Don't return, allow save
                    }
                }
            }
        }

        try
        {
            IsLoading = true;
            var fatura = new Fatura
            {
                Id = FaturaId,
                CariId = Cari.Id,
                CariUnvan = Cari.Unvan,
                FaturaNo = FaturaNo,
                Tarih = GetParsedDate(TarihStr),
                VadeTarihi = GetParsedDate(VadeTarihiStr),
                Tur = FaturaTuru,
                Aciklama = Aciklama,
                AraToplam = AraToplam,
                ToplamKDV = KdvToplam,
                GenelToplam = GenelToplam,
                OdemeSekli = OdemeSekli,
                KasaId = OdemeSekli == "Nakit" ? SelectedKasaId : null,
                BankaId = OdemeSekli == "Kredi Kartı" ? SelectedBankaId : null
            };
            
            var detaylar = Items.Select(i => new FaturaDetay
            {
                StokId = i.Stok.Id,
                StokKodu = i.Kod,
                StokAdi = i.Ad,
                Miktar = (double)(i.Miktar ?? 0),
                Birim = i.Birim,
                BirimFiyat = i.BirimFiyat ?? 0,
                KDVOrani = (int)(i.KdvOrani ?? 0),
                ToplamTutar = i.Tutar,
                KDVTutari = i.KdvTutari,
                Aciklama = i.Aciklama ?? ""
            }).ToList();

            await _uow.Faturalar.SaveWithTransactionAsync(fatura, detaylar, Cari, SettCariIsle, SettStokIsle, SettStokFiyat);
            NotifyFinancialDataChanged();
            
            if (SettBilgiFisi)
            {
                var pdfBytes = await _pdfService.GenerateFaturaPdfBytesAsync(fatura, detaylar);
                await HandleFileOpenAsync(pdfBytes, $"Fatura_{fatura.FaturaNo}.pdf");
            }

            SuccessMessage = "Fatura başarıyla kaydedildi.";
            await Task.Delay(500);
            OnRequestClose();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Kaydetme hatası: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public void Cancel() => OnRequestClose();

    protected abstract void NotifyFinancialDataChanged();

    protected DateTime GetParsedDate(string str)
    {
        if (DateTime.TryParseExact(str, "dd.MM.yyyy", null, System.Globalization.DateTimeStyles.None, out var dt))
            return dt;
        return DateTime.Now;
    }

    protected abstract Task HandleFileOpenAsync(byte[] content, string fileName);
    
    private void CalculateVadeFromOdemePlani()
    {
        if (Cari == null) return;
        
        DateTime baseDate = GetParsedDate(TarihStr);
        int daysToAdd = 0;

        if (!string.IsNullOrEmpty(Cari.OdemePlani))
        {
            string plan = Cari.OdemePlani.ToLowerInvariant();
            if (plan.Contains("7 gün") || plan.Contains("haftalık")) daysToAdd = 7;
            else if (plan.Contains("15 gün")) daysToAdd = 15;
            else if (plan.Contains("30 gün") || plan.Contains("aylık")) daysToAdd = 30;
            else if (plan.Contains("45 gün")) daysToAdd = 45;
            else if (plan.Contains("60 gün")) daysToAdd = 60;
            else if (plan.Contains("90 gün")) daysToAdd = 90;
        }
        
        // Card specific VadeGunu takes precedence if set and non-zero (optional logic)
        if (daysToAdd == 0 && Cari.VadeGunu > 0)
        {
            daysToAdd = Cari.VadeGunu;
        }

        var targetDate = baseDate.AddDays(daysToAdd);
        VadeTarihi = targetDate;
        VadeTarihiStr = targetDate.ToString("dd.MM.yyyy");
    }

    protected override Task InvokeOnUIThreadAsync(Action action)
    {
        action();
        return Task.CompletedTask;
    }
}
