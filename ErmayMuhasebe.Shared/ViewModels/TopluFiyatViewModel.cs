using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ErmayMuhasebe.Models;
using ErmayMuhasebe.Repositories;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Linq;
using System;
using System.Collections.Generic;

namespace ErmayMuhasebe.Shared.ViewModels;

public partial class TopluFiyatItem : ObservableObject
{
    public StokKart Stok { get; }
    [ObservableProperty] private decimal _eskiFiyat;
    [ObservableProperty] private decimal _yeniFiyat;

    public TopluFiyatItem(StokKart stok)
    {
        Stok = stok;
        EskiFiyat = stok.SatisFiyati;
        YeniFiyat = stok.SatisFiyati;
    }
}

public partial class TopluFiyatViewModel : ViewModelBase
{
    private readonly IUnitOfWork _uow;
    [ObservableProperty] private ObservableCollection<TopluFiyatItem> _items = new();
    [ObservableProperty] private ObservableCollection<string> _kategoriler = new();
    [ObservableProperty] private string? _seciliKategori;
    
    [ObservableProperty] private decimal? _oran = 10;
    [ObservableProperty] private bool _isPercentage = true;
    [ObservableProperty] private string _islemTuru = "Zam"; // Zam / Indirim

    private List<StokKart> _allStoklar = new();

    public TopluFiyatViewModel(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task InitializeAsync()
    {
        await LoadDataAsync();
    }

    public async Task LoadDataAsync()
    {
        IsLoading = true;
        try 
        {
            _allStoklar = await _uow.Stoklar.GetAllAsync();
            var distinctKategoriler = _allStoklar.Select(x => x.Kategori ?? "Genel").Distinct().OrderBy(x => x).ToList();
            
            await InvokeOnUIThreadAsync(() => {
                Kategoriler = new ObservableCollection<string>(distinctKategoriler);
                Kategoriler.Insert(0, "Hepsi");
                SeciliKategori = "Hepsi";
                FilterAndPreview();
            });
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

    partial void OnSeciliKategoriChanged(string? value) => FilterAndPreview();
    partial void OnOranChanged(decimal? value) => FilterAndPreview();
    partial void OnIsPercentageChanged(bool value) => FilterAndPreview();
    partial void OnIslemTuruChanged(string value) => FilterAndPreview();

    private void FilterAndPreview()
    {
        var filtered = _allStoklar.AsEnumerable();
        if (SeciliKategori != "Hepsi" && !string.IsNullOrEmpty(SeciliKategori))
        {
            filtered = filtered.Where(x => (x.Kategori ?? "Genel") == SeciliKategori);
        }

        var newItems = filtered.Select(s => {
            var item = new TopluFiyatItem(s);
            decimal degisim = IsPercentage ? (s.SatisFiyati * (Oran ?? 0) / 100) : (Oran ?? 0);
            if (IslemTuru == "Zam") item.YeniFiyat = Math.Round(s.SatisFiyati + degisim, 2);
            else item.YeniFiyat = Math.Round(s.SatisFiyati - degisim, 2);
            return item;
        }).ToList();

        Items = new ObservableCollection<TopluFiyatItem>(newItems);
    }

    [RelayCommand]
    public async Task UygulaAsync()
    {
        IsLoading = true;
        try 
        {
            foreach (var item in Items)
            {
                item.Stok.SatisFiyati = item.YeniFiyat;
                await _uow.Stoklar.SaveAsync(item.Stok);
            }
            SuccessMessage = $"{Items.Count} adet ürünün fiyatı güncellendi.";
            await LoadDataAsync();
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
}
