using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ErmayMuhasebe.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using ErmayMuhasebe.Models;

namespace ErmayMuhasebe.Shared.ViewModels;

public partial class YearSelectionViewModel : ViewModelBase
{
    private readonly IYearContext _yearContext;
    private readonly DatabaseService _dbService;

    [ObservableProperty] private ObservableCollection<int> _years = new();
    [ObservableProperty] private int? _selectedYear;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusMessage = "";

    public YearSelectionViewModel(IYearContext yearContext, DatabaseService dbService)
    {
        _yearContext = yearContext;
        _dbService = dbService;
        _ = LoadYearsAsync();
    }

    private async Task LoadYearsAsync()
    {
        int startingYear = 2026;
        try
        {
            var db = _dbService.GetConnection();
            var profil = await db.Table<FirmaProfili>().FirstOrDefaultAsync();
            if (profil != null && profil.StartingYear > 2000)
            {
                startingYear = profil.StartingYear;
            }
        }
        catch { }

        int currentSysYear = DateTime.Today.Year;
        // Eğer cihaz tarihi StartingYear'dan küçükse (ör: test için), aralığı güvene alalım
        int maxYear = Math.Max(currentSysYear, startingYear);

        var list = new ObservableCollection<int>();
        for (int y = maxYear; y >= startingYear; y--)
        {
            list.Add(y);
        }
        Years = list;

        SelectedYear = _yearContext.CurrentYear > 0 ? _yearContext.CurrentYear : DateTime.Today.Year;
    }

    [RelayCommand]
    public async Task SelectYearAsync()
    {
        if (SelectedYear == null) return;

        IsBusy = true;
        StatusMessage = $"{SelectedYear} yılı veritabanı yükleniyor...";
        
        try
        {
            await Task.Delay(800); // UI feel
            _yearContext.CurrentYear = SelectedYear.Value;
            StatusMessage = "Yıl seçimi başarılı.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Hata: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
