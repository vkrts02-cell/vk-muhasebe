using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ErmayMuhasebe.Services;
using ErmayMuhasebe.Repositories.DataProviders;

namespace ErmayMuhasebe.Avalonia.ViewModels;

public partial class YearSelectionViewModel : ViewModelBase
{
    private readonly IYearContext _yearContext;
    private readonly IDataProvider _dataProvider;
    private readonly DatabaseService _dbService;
    private readonly YearRolloverService _rolloverService;
    private readonly Action<int> _onYearSelected;
    private readonly Action? _onBackToLogin;

    [ObservableProperty]
    private bool _isAlreadyAuthenticated;

    public string ActionButtonText => IsAlreadyAuthenticated ? "Seçilen Yıla Geç" : "Uygulamayı Başlat";

    [ObservableProperty]
    private ObservableCollection<int> _years = new();

    [ObservableProperty]
    private int? _selectedYear;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private string _feedbackMessage = "";

    [ObservableProperty]
    private bool _isFeedbackSuccess = true;

    [ObservableProperty]
    private int _newYearToCreate = DateTime.Now.Year;

    [ObservableProperty]
    private string _newYearToCreateText = DateTime.Now.Year.ToString();

    [ObservableProperty]
    private bool _hasNoYears;

    // Rollover Dialog State
    [ObservableProperty]
    private bool _showRolloverDialog;

    [ObservableProperty]
    private int _rolloverSourceYear;

    partial void OnRolloverSourceYearChanged(int value)
    {
        if (value > 0)
        {
            RolloverTargetYear = value + 1;
        }
    }

    [ObservableProperty]
    private int _rolloverTargetYear;

    [ObservableProperty]
    private bool _rolloverCariler = true;

    [ObservableProperty]
    private bool _rolloverStoklar = true;

    [ObservableProperty]
    private bool _rolloverKasaBanka = true;

    [ObservableProperty]
    private bool _rolloverCekSenet = true;

    [ObservableProperty]
    private bool _rolloverTanimlar = true;

    [ObservableProperty]
    private string _rolloverProgressText = "";

    public YearSelectionViewModel(
        IYearContext yearContext, 
        IDataProvider dataProvider, 
        DatabaseService dbService,
        Action<int> onYearSelected,
        Action? onBackToLogin = null,
        bool isAlreadyAuthenticated = false)
    {
        _yearContext = yearContext;
        _dataProvider = dataProvider;
        _dbService = dbService;
        _rolloverService = new YearRolloverService(dbService, yearContext);
        _onYearSelected = onYearSelected;
        _onBackToLogin = onBackToLogin;
        _isAlreadyAuthenticated = isAlreadyAuthenticated;
        
        LoadYears();
    }

    public void LoadYears()
    {
        Years.Clear();
        var available = _rolloverService.GetAvailableYears();
        foreach (var y in available)
        {
            Years.Add(y);
        }

        HasNoYears = !Years.Any();

        SelectedYear = _yearContext.CurrentYear > 0 && Years.Contains(_yearContext.CurrentYear) 
            ? _yearContext.CurrentYear 
            : (Years.Any() ? Years.FirstOrDefault() : null);

        RolloverSourceYear = SelectedYear ?? DateTime.Now.Year;
        RolloverTargetYear = RolloverSourceYear + 1;

        if (available.Any())
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _dbService.SyncService.SyncMaliYillarAsync(available);
                }
                catch { }
            });
        }
    }

    [RelayCommand]
    private async Task SelectYearAsync()
    {
        if (SelectedYear == null) return;

        IsBusy = true;
        StatusMessage = $"{SelectedYear} yılı veritabanı yükleniyor...";
        FeedbackMessage = "";
        
        try
        {
            _yearContext.CurrentYear = SelectedYear.Value;
            var dbName = $"ermay_{SelectedYear.Value}.db";
            await _dataProvider.InitializeAsync(dbName);
            
            _onYearSelected?.Invoke(SelectedYear.Value);
        }
        catch (Exception ex)
        {
            StatusMessage = "Hata: " + ex.Message;
            FeedbackMessage = "Veritabanı açılamadı: " + ex.Message;
            IsFeedbackSuccess = false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CreateNewYearAsync()
    {
        if (!int.TryParse(NewYearToCreateText?.Trim(), out int yearToCreate) || yearToCreate < 2000 || yearToCreate > 2100)
        {
            FeedbackMessage = "Lütfen geçerli bir mali yıl giriniz (Örn: 2026).";
            IsFeedbackSuccess = false;
            return;
        }
        
        if (Years.Contains(yearToCreate))
        {
            FeedbackMessage = $"{yearToCreate} yılı zaten mevcut!";
            IsFeedbackSuccess = false;
            return;
        }

        IsBusy = true;
        StatusMessage = $"{yearToCreate} yılı oluşturuluyor...";
        FeedbackMessage = "";
        
        try
        {
            NewYearToCreate = yearToCreate;
            _yearContext.CurrentYear = yearToCreate;
            var dbName = $"ermay_{yearToCreate}.db";
            await _dataProvider.InitializeAsync(dbName);

            try
            {
                await _dbService.SyncService.SyncMaliYilAsync(yearToCreate);
            }
            catch { }

            // Automatically switch to the newly created year and pass back to login/main
            _onYearSelected?.Invoke(yearToCreate);
        }
        catch (Exception ex)
        {
            StatusMessage = "Hata: " + ex.Message;
            FeedbackMessage = "Yıl oluşturulamadı: " + ex.Message;
            IsFeedbackSuccess = false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void SwitchUser()
    {
        _onBackToLogin?.Invoke();
    }

    [RelayCommand]
    private void Cancel()
    {
        _onBackToLogin?.Invoke();
    }

    [RelayCommand]
    private void OpenRolloverDialog()
    {
        RolloverSourceYear = SelectedYear ?? DateTime.Now.Year;
        RolloverTargetYear = RolloverSourceYear + 1;
        ShowRolloverDialog = true;
        FeedbackMessage = "";
    }

    [RelayCommand]
    private void CancelRolloverDialog()
    {
        ShowRolloverDialog = false;
    }

    [RelayCommand]
    private async Task ExecuteRolloverAsync()
    {
        if (RolloverSourceYear == RolloverTargetYear)
        {
            FeedbackMessage = "Kaynak yıl ile hedef yıl aynı olamaz.";
            IsFeedbackSuccess = false;
            return;
        }

        IsBusy = true;
        StatusMessage = $"{RolloverSourceYear} yılından {RolloverTargetYear} yılına devir yapılıyor...";
        RolloverProgressText = "Devir başlatılıyor...";

        var progress = new Progress<string>(msg =>
        {
            RolloverProgressText = msg;
            StatusMessage = msg;
        });

        try
        {
            var options = new RolloverOptions
            {
                TransferCariler = RolloverCariler,
                TransferStoklar = RolloverStoklar,
                TransferKasaBanka = RolloverKasaBanka,
                TransferCekSenet = RolloverCekSenet,
                TransferTanimlar = RolloverTanimlar
            };

            var result = await _rolloverService.RolloverYearAsync(RolloverSourceYear, RolloverTargetYear, options, progress);

            if (result.Success)
            {
                LoadYears();
                SelectedYear = RolloverTargetYear;
                ShowRolloverDialog = false;
                FeedbackMessage = result.Message;
                IsFeedbackSuccess = true;
            }
            else
            {
                FeedbackMessage = result.Message;
                IsFeedbackSuccess = false;
            }
        }
        catch (Exception ex)
        {
            FeedbackMessage = "Devir Hatası: " + ex.Message;
            IsFeedbackSuccess = false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ReflectBalancesForwardAsync()
    {
        if (SelectedYear == null) return;

        IsBusy = true;
        StatusMessage = $"{SelectedYear} yılı bakiyeleri sonraki yıla yansıtılıyor...";
        FeedbackMessage = "";

        var progress = new Progress<string>(msg =>
        {
            StatusMessage = msg;
        });

        try
        {
            var result = await _rolloverService.ReflectBalancesForwardAsync(SelectedYear.Value, progress);
            FeedbackMessage = result.Message;
            IsFeedbackSuccess = result.Success;
        }
        catch (Exception ex)
        {
            FeedbackMessage = "Yansıtma Hatası: " + ex.Message;
            IsFeedbackSuccess = false;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
