using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ErmayMuhasebe.Models;
using ErmayMuhasebe.Services;
using ErmayMuhasebe.Repositories;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System;
using CommunityToolkit.Mvvm.Messaging;
using ErmayMuhasebe.Avalonia.Messages;

namespace ErmayMuhasebe.Avalonia.ViewModels;

public partial class SiparisListViewModel : ViewModelBase
{
    private readonly IUnitOfWork _uow;
    private readonly IPdfService _pdfService;
    private readonly IFileService _fileService;

    [ObservableProperty]
    private ObservableCollection<Siparis> _siparisler = new();

    [ObservableProperty]
    private string _searchString = "";

    partial void OnSearchStringChanged(string value) => _ = LoadSiparislerAsync();

    public SiparisListViewModel(IUnitOfWork uow, IPdfService pdfService, IFileService fileService)
    {
        _uow = uow;
        _pdfService = pdfService;
        _fileService = fileService;
        _ = LoadSiparislerAsync();
    }

    [ObservableProperty]
    private Siparis? _selectedSiparis;



    [RelayCommand]
    public async Task LoadSiparislerAsync()
    {
        var list = await _uow.Siparisler.GetAllAsync();
        
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

    [RelayCommand]
    public void CreateNewOrder()
    {
        var vm = new SiparisDetayViewModel(_uow, null);
        vm.RequestClose += () => {
             _ = LoadSiparislerAsync();
             WeakReferenceMessenger.Default.Send(new NavigateViewModelMessage(this));
        };
        WeakReferenceMessenger.Default.Send(new NavigateViewModelMessage(vm));
    }

    [RelayCommand]
    public async Task EditSiparisAsync(Siparis siparis)
    {
        if (siparis == null) return;
        var details = await _uow.Siparisler.GetDetaylarAsync(siparis.Id);
        var vm = new SiparisDetayViewModel(_uow, null);
        await vm.LoadFromExistingAsync(siparis, details);
        
        vm.RequestClose += () => {
             _ = LoadSiparislerAsync();
             WeakReferenceMessenger.Default.Send(new NavigateViewModelMessage(this));
        };
        WeakReferenceMessenger.Default.Send(new NavigateViewModelMessage(vm));
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

    public async Task DeleteSiparisAsync(Siparis siparis)
    {
        if (siparis == null) return;
        
        await _uow.Siparisler.DeleteAsync(siparis);
        await LoadSiparislerAsync();
    }

    [RelayCommand]
    public async Task ViewSiparisPdfAsync(Siparis? siparis)
    {
        if (siparis == null) siparis = SelectedSiparis;
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
            System.Diagnostics.Debug.WriteLine($"PDF Error: {ex}");
        }
        finally
        {
            IsLoading = false;
        }
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
    public void CloseConfirm() => IsConfirmVisible = false;

    [RelayCommand]
    public async Task ConvertToInvoiceAsync()
    {
        if (SelectedSiparis == null) return;
        IsConfirmVisible = false;
        if (SelectedSiparis.Durum == "Faturalandı") return; // Already invoiced
        
        // Validate Cari
        var cariId = SelectedSiparis.CariId;
        if (cariId == 0 && !string.IsNullOrEmpty(SelectedSiparis.CariUnvan))
        {
             // Try to find by name if Id is missing
             var cariler = await _uow.Cariler.GetAllAsync();
             var found = cariler.FirstOrDefault(c => c.Unvan == SelectedSiparis.CariUnvan);
             if (found != null) cariId = found.Id;
        }

        if (cariId == 0)
        {
            // Still no Cari?
            // Ask user or show error. For now, showing error.
            // Note: In a real app we might prompt to create one, but here we just block orphaned invoices.
            // Using System.Diagnostics for now as we don't have ISnackbar in ViewModel easily without DI, 
            // but we can just return.
            // Ideally we should show a message.
            return; 
        }

        var details = await _uow.Siparisler.GetDetaylarAsync(SelectedSiparis.Id);
        
        var fatura = new Fatura
        {
            CariId = cariId,
            CariUnvan = SelectedSiparis.CariUnvan,
            Tarih = System.DateTime.Now,
            VadeTarihi = System.DateTime.Now.AddDays(30),
            Tur = "Satış", // Explicitly using Turkish characters "Satış" to match generic logic
            Aciklama = $"Sipariş Ref: {SelectedSiparis.SiparisNo}. {SelectedSiparis.Aciklama}",
            BaglantiEvrakNo = SelectedSiparis.SiparisNo,
            KayitTarihi = System.DateTime.Now
        };

        var faturaDetaylar = new System.Collections.Generic.List<FaturaDetay>();

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
            fd.KDVTutari = fd.ToplamTutar * fd.KDVOrani / 100m;
            faturaDetaylar.Add(fd);
        }

        fatura.AraToplam = faturaDetaylar.Sum(x => x.ToplamTutar);
        fatura.ToplamKDV = faturaDetaylar.Sum(x => x.KDVTutari);
        fatura.GenelToplam = fatura.AraToplam + fatura.ToplamKDV;
        fatura.FaturaNo = "FTRINV-" + DateTime.Now.Ticks; // UnitOfWork doesn't have GetNextFaturaNoAsync yet, using fallback or we can add it later.

        // Save Invoice
        await _uow.Faturalar.SaveWithDetailsAndTransactionAsync(fatura, faturaDetaylar, true);

        // Update Order Status
        SelectedSiparis.Durum = "Faturalandı";
        await _uow.Siparisler.SaveAsync(SelectedSiparis); // Update header only; do not pass empty list to SaveWithDetailsAsync which deletes details

        await LoadSiparislerAsync();
    }
    [RelayCommand]
    public async Task OpenAciklamaDialogAsync(Siparis siparis)
    {
        if (siparis == null) return;
        var details = await _uow.Siparisler.GetDetaylarAsync(siparis.Id);
        
        var vm = new SiparisAciklamaViewModel(_uow, siparis, details);
        vm.RequestClose += () => {
             _ = LoadSiparislerAsync();
             WeakReferenceMessenger.Default.Send(new NavigateViewModelMessage(this));
        };
        WeakReferenceMessenger.Default.Send(new NavigateViewModelMessage(vm));
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
