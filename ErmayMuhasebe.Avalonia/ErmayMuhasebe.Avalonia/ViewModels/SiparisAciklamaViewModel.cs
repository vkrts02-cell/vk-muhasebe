using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ErmayMuhasebe.Models;
using ErmayMuhasebe.Services;
using ErmayMuhasebe.Repositories;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ErmayMuhasebe.Avalonia.ViewModels;

public partial class SiparisAciklamaItemViewModel : ObservableObject
{
    public int StokId { get; set; }
    public string StokAdi { get; set; } = "";
    
    // We need to keep other properties to preserve them when saving back!
    public decimal Miktar { get; set; }
    public decimal TopMiktari { get; set; }
    public string? Birim { get; set; }
    public decimal BirimFiyat { get; set; }
    public decimal Tutar { get; set; }
    public double KdvOrani { get; set; }
    public string? ParaBirimi { get; set; }

    [ObservableProperty]
    private string? _aciklama;
    [ObservableProperty]
    private string? _miktarAciklama;

    public SiparisAciklamaItemViewModel(SiparisDetay d)
    {
        StokId = d.StokId;
        StokAdi = d.StokAdi ?? "";
        Miktar = d.Miktar;
        TopMiktari = d.TopMiktari;
        Birim = d.Birim;
        BirimFiyat = d.BirimFiyat;
        Tutar = d.Tutar;
        Aciklama = d.Aciklama;
        MiktarAciklama = d.MiktarAciklama;
        KdvOrani = d.KdvOrani;
        ParaBirimi = d.ParaBirimi;
    }
}

public partial class SiparisAciklamaViewModel : ViewModelBase
{
    private readonly IUnitOfWork _uow;
    private readonly Siparis _siparis;
    
    public string Title => $"Sipariş Açıklamaları - {_siparis.SiparisNo}";

    [ObservableProperty]
    private ObservableCollection<SiparisAciklamaItemViewModel> _items = new();


    public SiparisAciklamaViewModel(IUnitOfWork uow, Siparis siparis, List<SiparisDetay> details)
    {
        _uow = uow;
        _siparis = siparis;

        foreach (var d in details)
        {
            Items.Add(new SiparisAciklamaItemViewModel(d));
        }
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        try
        {
            var detailsToSave = Items.Select(i => new SiparisDetay
            {
                SiparisId = _siparis.Id,
                StokId = i.StokId,
                StokAdi = i.StokAdi,
                Miktar = i.Miktar,
                TopMiktari = i.TopMiktari,
                Birim = i.Birim,
                BirimFiyat = i.BirimFiyat,
                Tutar = i.Tutar,
                Aciklama = i.Aciklama ?? "",
                MiktarAciklama = i.MiktarAciklama ?? "",
                KdvOrani = i.KdvOrani,
                ParaBirimi = i.ParaBirimi
            }).ToList();

            await _uow.Siparisler.SaveWithDetailsAsync(_siparis, detailsToSave);
            
            OnRequestClose();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving descriptions: {ex.Message}");
        }
    }

    [RelayCommand]
    public void Cancel()
    {
        OnRequestClose();
    }
}
