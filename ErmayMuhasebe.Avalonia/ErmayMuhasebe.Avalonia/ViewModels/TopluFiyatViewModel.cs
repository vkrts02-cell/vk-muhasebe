using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ErmayMuhasebe.Models;
using ErmayMuhasebe.Services;
using ErmayMuhasebe.Repositories;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Linq;
using System;
using System.Collections.Generic;

namespace ErmayMuhasebe.Avalonia.ViewModels;

public partial class TopluFiyatItem : ObservableObject
{
    public StokKart Stok { get; }
    [ObservableProperty] private decimal _eskiFiyat;
    [ObservableProperty] private decimal _yeniFiyat;
    [ObservableProperty] private bool _isSelected = true;

    private readonly Action? _onSelectionChanged;

    public TopluFiyatItem(StokKart stok, Action? onSelectionChanged = null)
    {
        Stok = stok;
        EskiFiyat = stok.SatisFiyati;
        YeniFiyat = stok.SatisFiyati;
        _onSelectionChanged = onSelectionChanged;
    }

    partial void OnIsSelectedChanged(bool value) => _onSelectionChanged?.Invoke();
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
    [ObservableProperty] private int _selectedCount;
    [ObservableProperty] private bool _isAllSelected = true;

    private List<StokKart> _allStoklar = new();

    public TopluFiyatViewModel(IUnitOfWork uow)
    {
        _uow = uow;
        _ = LoadDataAsync();
    }

    public async Task LoadDataAsync()
    {
        var rawStoklar = await _uow.Stoklar.GetAllAsync();
        // Filter out empty/blank records that shouldn't be edited
        _allStoklar = rawStoklar.Where(x => !string.IsNullOrEmpty(x.StokAdi) || !string.IsNullOrEmpty(x.StokKodu)).ToList();
        
        Kategoriler = new ObservableCollection<string>(_allStoklar.Select(x => x.Kategori ?? "Genel").Distinct().OrderBy(x => x));
        Kategoriler.Insert(0, "Hepsi");
        SeciliKategori = "Hepsi";
        FilterAndPreview();
    }

    partial void OnSeciliKategoriChanged(string? value) => FilterAndPreview();
    partial void OnOranChanged(decimal? value) => FilterAndPreview();
    partial void OnIsPercentageChanged(bool value) => FilterAndPreview();
    partial void OnIslemTuruChanged(string value) => FilterAndPreview();
    
    partial void OnIsAllSelectedChanged(bool value)
    {
        if (Items == null) return;
        foreach (var item in Items)
        {
            item.IsSelected = value;
        }
    }

    private void UpdateSelectedCount()
    {
        SelectedCount = Items.Count(x => x.IsSelected);
    }

    private void FilterAndPreview()
    {
        var filtered = _allStoklar.AsEnumerable();
        if (SeciliKategori != "Hepsi" && !string.IsNullOrEmpty(SeciliKategori))
        {
            filtered = filtered.Where(x => (x.Kategori ?? "Genel") == SeciliKategori);
        }

        var newItems = filtered.Select(s => {
            var item = new TopluFiyatItem(s, UpdateSelectedCount);
            decimal degisim = IsPercentage ? (s.SatisFiyati * (Oran ?? 0) / 100) : (Oran ?? 0);
            if (IslemTuru == "Zam") item.YeniFiyat = s.SatisFiyati + degisim;
            else item.YeniFiyat = s.SatisFiyati - degisim;
            item.IsSelected = IsAllSelected;
            return item;
        }).ToList();

        Items = new ObservableCollection<TopluFiyatItem>(newItems);
        UpdateSelectedCount();
    }

    [RelayCommand]
    public async Task UygulaAsync()
    {
        var selectedItems = Items.Where(x => x.IsSelected).ToList();
        if (selectedItems.Count == 0) return;

        foreach (var item in selectedItems)
        {
            item.Stok.SatisFiyati = item.YeniFiyat;
            await _uow.Stoklar.SaveAsync(item.Stok);
        }
        await LoadDataAsync();
    }
}
