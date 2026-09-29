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

public abstract partial class KasaListViewModel : ViewModelBase
{
    public event Action<BankaKart>? KasaSelected;
    protected readonly IUnitOfWork _uow;
    protected readonly IPdfService _pdfService;

    [ObservableProperty] private bool _isEditDialogVisible;
    [ObservableProperty] private string _dialogTitle = "Yeni Kasa Tanımla";
    
    [ObservableProperty] private int _editId;
    [ObservableProperty] private string _editKasaAdi = "";
    [ObservableProperty] private string _editDovizTuru = "TL";
    [ObservableProperty] private string _editYetkili = "";
    [ObservableProperty] private decimal _editAcilisBakiyesi;

    [ObservableProperty] private ObservableCollection<BankaKart> _kasalar = new();
    [ObservableProperty] private BankaKart? _selectedKasa;

    // Filtering
    [ObservableProperty] private string _filterText = "";
    [ObservableProperty] private DateTime _filterBaslangicTarihi = DateTime.Now.AddMonths(-1);
    [ObservableProperty] private DateTime _filterBitisTarihi = DateTime.Now;

    partial void OnFilterTextChanged(string value) => _ = LoadKasalarAsync();

    public KasaListViewModel(IUnitOfWork uow, IPdfService pdfService)
    {
        _uow = uow;
        _pdfService = pdfService;
        
        _ = LoadKasalarAsync();
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
    public async Task LoadKasalarAsync()
    {
        try 
        {
            IsLoading = true;
            
            var list = await _uow.Bankalar.GetAllAsync();
            var filtered = list.Where(k => k.KartTuru == "Kasa").ToList();
            
            foreach (var k in filtered)
            {
                k.DovizTuru = GetCleanDoviz(k.DovizTuru);
            }

            if (!string.IsNullOrWhiteSpace(FilterText))
            {
                filtered = filtered.Where(k => (k.BankaAdi ?? "").Contains(FilterText, StringComparison.OrdinalIgnoreCase) || 
                                             (k.Yetkili ?? "").Contains(FilterText, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            await InvokeOnUIThreadAsync(() => 
            {
                Kasalar = new ObservableCollection<BankaKart>(filtered);
            });
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Kasa listesi yükleme hatası: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public override void OnNavigatedTo() => _ = LoadKasalarAsync();

    [RelayCommand]
    public void OpenAddKasaDialog()
    {
        EditId = 0;
        EditKasaAdi = "";
        EditDovizTuru = "TL";
        EditYetkili = "";
        EditAcilisBakiyesi = 0;
        DialogTitle = "Yeni Kasa Tanımla";
        IsEditDialogVisible = true;
    }

    [RelayCommand]
    public void OpenEditKasaDialog(BankaKart? kasa)
    {
        if (kasa == null) kasa = SelectedKasa;
        if (kasa == null) return;
        
        EditId = kasa.Id;
        EditKasaAdi = kasa.BankaAdi ?? "";
        EditDovizTuru = GetCleanDoviz(kasa.DovizTuru);
        EditYetkili = kasa.Yetkili ?? "";
        EditAcilisBakiyesi = kasa.AcilisBakiyesi;
        DialogTitle = "Kasa Bilgilerini Düzenle";
        IsEditDialogVisible = true;
    }

    [RelayCommand]
    public async Task SaveKasaAsync()
    {
        if (string.IsNullOrWhiteSpace(EditKasaAdi)) return;

        IsLoading = true;
        try
        {
            var existingMoves = EditId != 0 ? await _uow.Kasalar.GetHareketlerAsync(EditId) : new List<KasaHareket>();
            decimal guncel = EditAcilisBakiyesi + existingMoves.Sum(h => h.Giren - h.Cikan);

            var kasa = new BankaKart
            {
                Id = EditId,
                BankaAdi = EditKasaAdi,
                DovizTuru = GetCleanDoviz(EditDovizTuru),
                Yetkili = EditYetkili,
                AcilisBakiyesi = EditAcilisBakiyesi,
                GuncelBakiye = guncel, 
                KartTuru = "Kasa"
            };

            await _uow.Bankalar.SaveAsync(kasa);
            IsEditDialogVisible = false;
            await LoadKasalarAsync();
            NotifyFinancialDataChanged();
            SuccessMessage = "Kasa kaydedildi.";
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

    protected abstract void NotifyFinancialDataChanged();

    [RelayCommand]
    public void CloseDialog() => IsEditDialogVisible = false;

    [RelayCommand]
    public virtual void NavigateToDetay(BankaKart? kasa) 
    {
        if (kasa != null) KasaSelected?.Invoke(kasa);
    }

    [RelayCommand]
    public void DeleteKasaConfirm(BankaKart? kasa)
    {
        if (kasa == null) kasa = SelectedKasa;
        if (kasa == null) return;
        ShowConfirm("Kasayı Sil", $"{kasa.BankaAdi} adlı kasayı silmek istediğinize emin misiniz?", () => DeleteKasaAsync(kasa));
    }

    public async Task DeleteKasaAsync(BankaKart? kasa)
    {
        try 
        {
            await _uow.Bankalar.DeleteAsync(kasa!.Id);
            await LoadKasalarAsync();
            NotifyFinancialDataChanged();
            SuccessMessage = "Kasa silindi.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Silme hatası: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task GenerateReportAsync(BankaKart? kasa)
    {
        if (kasa == null) kasa = SelectedKasa;
        if (kasa == null) return;

        try
        {
            var hareketler = await _uow.Kasalar.GetHareketlerAsync(kasa.Id);
            var pdfBytes = await _pdfService.GenerateKasaEkstrePdfBytesAsync(kasa, hareketler.ToList());
            await HandleFileOpenAsync(pdfBytes, $"KasaEkstre_{kasa.BankaAdi}_{DateTime.Now:ddMMyyyy}.pdf");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Rapor hatası: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task GenerateFilteredReportAsync()
    {
        try 
        {
            var pdfBytes = await _pdfService.GenerateKasaListesiPdfBytesAsync(Kasalar.ToList());
            await HandleFileOpenAsync(pdfBytes, "KasaListesi.pdf");
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    protected abstract Task HandleFileOpenAsync(byte[] content, string fileName);

    protected override Task InvokeOnUIThreadAsync(Action action)
    {
        action();
        return Task.CompletedTask;
    }
}
