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

public partial class SiparisListViewModel : ViewModelBase
{
    protected readonly IUnitOfWork _uow;
    protected readonly IPdfService _pdfService;
    protected readonly IFileService _fileService;

    [ObservableProperty]
    private ObservableCollection<Siparis> _siparisler = new();

    [ObservableProperty]
    private string _searchString = "";

    [ObservableProperty] private int _activeDateFilter = 30; // 0=Tümü, 7, 30, 90

    [ObservableProperty] private int _bekleyenSiparisCount;
    [ObservableProperty] private decimal _toplamCiro;

    partial void OnSearchStringChanged(string value) => _ = LoadSiparislerAsync();
    partial void OnActiveDateFilterChanged(int value) => _ = LoadSiparislerAsync();

    public SiparisListViewModel(IUnitOfWork uow, IPdfService pdfService, IFileService fileService)
    {
        _uow = uow;
        _pdfService = pdfService;
        _fileService = fileService;
    }

    public override void OnNavigatedTo()
    {
        base.OnNavigatedTo();
        _ = LoadSiparislerAsync();
    }

    [ObservableProperty]
    private Siparis? _selectedSiparis;

    [RelayCommand]
    public async Task LoadSiparislerAsync()
    {
        IsLoading = true;
        try
        {
            var all = await _uow.Siparisler.GetAllAsync();
            
            // Stats (based on all data)
            BekleyenSiparisCount = all.Count(s => s.Durum == "Bekliyor");
            ToplamCiro = all.Sum(s => s.GenelToplam);

            var list = all;

            // Date Filter
            if (ActiveDateFilter > 0)
            {
                var cutoff = DateTime.Now.Date.AddDays(-ActiveDateFilter);
                list = list.Where(s => s.Tarih >= cutoff).ToList();
            }
            
            // Search
            if (!string.IsNullOrWhiteSpace(SearchString))
            {
                list = list.Where(s => 
                    (s.SiparisNo != null && s.SiparisNo.Contains(SearchString, StringComparison.OrdinalIgnoreCase)) ||
                    (s.CariUnvan != null && s.CariUnvan.Contains(SearchString, StringComparison.OrdinalIgnoreCase)) ||
                    (s.Aciklama != null && s.Aciklama.Contains(SearchString, StringComparison.OrdinalIgnoreCase))
                ).ToList();
            }

            Siparisler = new ObservableCollection<Siparis>(list.OrderByDescending(x => x.Tarih));
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

    public event Action<Siparis?>? OnEditRequested;
    public event Action? OnCreateRequested;

    [RelayCommand]
    public void CreateNewOrder()
    {
        OnCreateRequested?.Invoke();
    }

    [RelayCommand]
    public void EditSiparis(Siparis siparis)
    {
        if (siparis == null) return;
        OnEditRequested?.Invoke(siparis);
    }

    [RelayCommand]
    public void DeleteSiparisConfirm(Siparis siparis)
    {
        if (siparis == null) return;
        ShowConfirm("Sipariş Sil", $"{siparis.SiparisNo} nolu siparişi silmek istediğinize emin misiniz?", async () => {
            await _uow.Siparisler.DeleteAsync(siparis);
            await LoadSiparislerAsync();
        });
    }

    [RelayCommand]
    public async Task ViewSiparisPdfAsync(Siparis siparis)
    {
        if (siparis == null) return;
        ErrorMessage = null;
        IsLoading = true;
        try
        {
            var details = await _uow.Siparisler.GetDetaylarAsync(siparis.Id);
            var cari = await _uow.Cariler.GetByIdAsync(siparis.CariId);
            var pdfBytes = await _pdfService.GenerateSiparisPdfBytesAsync(siparis, details, cari);

            if (pdfBytes == null || pdfBytes.Length == 0)
            {
                ErrorMessage = "PDF içeriği oluşturulamadı.";
                return;
            }

            string safeNo = string.Join("_", (siparis.SiparisNo ?? "NO").Split(System.IO.Path.GetInvalidFileNameChars()));
            string fileName = $"Siparis_{safeNo}.pdf";

            await _fileService.SaveAndOpenFileAsync(fileName, pdfBytes, "application/pdf");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"PDF Görüntüleme Hatası: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task ShareSiparisWhatsAppAsync(Siparis? siparis)
    {
        if (siparis == null) return;
        try
        {
            var details = await _uow.Siparisler.GetDetaylarAsync(siparis.Id);
            var cari = await _uow.Cariler.GetByIdAsync(siparis.CariId);
            var pdfBytes = await _pdfService.GenerateSiparisPdfBytesAsync(siparis, details, cari);

            if (pdfBytes == null || pdfBytes.Length == 0) return;

            string telefon = cari?.Telefon ?? cari?.CepTelefon ?? "";
            string mesaj = $"Sayın {cari?.Unvan}, {siparis.SiparisNo} numaralı sipariş belgeniz ekte sunulmuştur.";
            string safeNo = string.Join("_", (siparis.SiparisNo ?? "NO").Split(System.IO.Path.GetInvalidFileNameChars()));
            string fileName = $"Siparis_{safeNo}.pdf";

            var shareService = ResolveShareService();
            if (shareService != null)
            {
                await shareService.ShareViaWhatsAppAsync(telefon, mesaj, pdfBytes, fileName);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"WhatsApp Paylaşım Hatası: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task ShareSiparisEmailAsync(Siparis? siparis)
    {
        if (siparis == null) return;
        try
        {
            var details = await _uow.Siparisler.GetDetaylarAsync(siparis.Id);
            var cari = await _uow.Cariler.GetByIdAsync(siparis.CariId);
            
            if (cari == null || string.IsNullOrWhiteSpace(cari.Email))
            {
                ErrorMessage = "Cari karta tanımlı e-posta adresi bulunamadı.";
                return;
            }

            var pdfBytes = await _pdfService.GenerateSiparisPdfBytesAsync(siparis, details, cari);
            if (pdfBytes == null || pdfBytes.Length == 0) return;

            string aliciEposta = cari.Email;
            string konu = $"Sipariş - {siparis.SiparisNo}";
            string mesaj = $"Sayın {cari.Unvan},\n\n{siparis.SiparisNo} numaralı sipariş belgeniz ekte yer almaktadır.\n\nİyi çalışmalar.";
            string safeNo = string.Join("_", (siparis.SiparisNo ?? "NO").Split(System.IO.Path.GetInvalidFileNameChars()));
            string fileName = $"Siparis_{safeNo}.pdf";

            var shareService = ResolveShareService();
            if (shareService != null)
            {
                await shareService.SendPdfViaEmailAsync(aliciEposta, konu, mesaj, pdfBytes, fileName);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"E-posta Gönderim Hatası: {ex.Message}";
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

    [RelayCommand]
    public void ShowConvertToInvoiceConfirm()
    {
        if (SelectedSiparis == null) return;
        if (SelectedSiparis.Durum == "Faturalandı") return;

        ShowConfirm("Faturalandır", $"{SelectedSiparis.SiparisNo} nolu sipariş faturalandırılacak. Emin misiniz?", async () => {
            await ConvertToInvoiceAsync();
        });
    }

    [RelayCommand]
    public async Task ConvertToInvoiceAsync()
    {
        if (SelectedSiparis == null) return;
        if (SelectedSiparis.Durum == "Faturalandı") return;
        
        var cariId = SelectedSiparis.CariId;
        if (cariId == 0 && !string.IsNullOrEmpty(SelectedSiparis.CariUnvan))
        {
             var cariler = await _uow.Cariler.GetAllAsync();
             var found = cariler.FirstOrDefault(c => c.Unvan == SelectedSiparis.CariUnvan);
             if (found != null) cariId = found.Id;
        }

        if (cariId == 0)
        {
            ErrorMessage = "Cari hesap bulunamadığından faturalandırılamadı.";
            return; 
        }

        var details = await _uow.Siparisler.GetDetaylarAsync(SelectedSiparis.Id);
        
        var fatura = new Fatura
        {
            CariId = cariId,
            CariUnvan = SelectedSiparis.CariUnvan,
            Tarih = DateTime.Now,
            VadeTarihi = DateTime.Now.AddDays(30),
            Tur = "Satış",
            Aciklama = $"Sipariş Ref: {SelectedSiparis.SiparisNo}. {SelectedSiparis.Aciklama}",
            BaglantiEvrakNo = SelectedSiparis.SiparisNo,
            KayitTarihi = DateTime.Now
        };

        var faturaDetaylar = new List<FaturaDetay>();

        foreach (var d in details)
        {
            var stok = await _uow.Stoklar.GetByIdAsync(d.StokId);
            var fd = new FaturaDetay
            {
                StokId = d.StokId,
                StokKodu = stok?.StokKodu ?? "",
                StokAdi = d.StokAdi,
                Miktar = d.Miktar,
                Birim = d.Birim ?? stok?.Birim,
                BirimFiyat = d.BirimFiyat,
                ToplamTutar = d.Tutar,
                KDVOrani = stok?.KDV ?? 20,
                Aciklama = (string.IsNullOrEmpty(d.MiktarAciklama) ? "" : $"[{d.MiktarAciklama}] ") + d.Aciklama
            };
            fd.KDVTutari = fd.ToplamTutar * (decimal)fd.KDVOrani / 100m;
            faturaDetaylar.Add(fd);
        }

        fatura.AraToplam = faturaDetaylar.Sum(x => x.ToplamTutar);
        fatura.ToplamKDV = faturaDetaylar.Sum(x => x.KDVTutari);
        fatura.GenelToplam = fatura.AraToplam + fatura.ToplamKDV;
        fatura.FaturaNo = "FTRINV-" + DateTime.Now.Ticks;

        await _uow.Faturalar.SaveWithDetailsAndTransactionAsync(fatura, faturaDetaylar, true);

        SelectedSiparis.Durum = "Faturalandı";
        await _uow.Siparisler.SaveAsync(SelectedSiparis);

        await LoadSiparislerAsync();
        SuccessMessage = "Sipariş başarıyla faturalandırıldı.";
    }

    [RelayCommand]
    public async Task UpdateStatusAsync(string status)
    {
        if (SelectedSiparis == null || string.IsNullOrEmpty(status)) return;
        SelectedSiparis.Durum = status;
        await _uow.Siparisler.SaveAsync(SelectedSiparis);
        await LoadSiparislerAsync();
    }
}
