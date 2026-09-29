using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ErmayMuhasebe.Avalonia.Messages;
using ErmayMuhasebe.Services;
using ErmayMuhasebe.Repositories;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using LiveChartsCore.Kernel.Sketches;
using System;
using System.Linq;
using Avalonia.Threading;
using ErmayMuhasebe.Models;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Media;
using SVM = ErmayMuhasebe.Shared.ViewModels;
using AVM = ErmayMuhasebe.Avalonia.ViewModels;

namespace ErmayMuhasebe.Avalonia.ViewModels;

/// <summary>
/// Avalonia Dashboard ViewModel - Standalone (not extending Shared)
/// </summary>
public partial class DashboardViewModel : ViewModelBase
{
    private static readonly IBrush PositiveBrush = new SolidColorBrush(Color.Parse("#10B981"));
    private static readonly IBrush PositiveBgBrush = new SolidColorBrush(Color.Parse("#2A10B981"));
    private static readonly BoxShadows PositiveShadow = BoxShadows.Parse("0 4 15 0 #2010B981");

    private static readonly IBrush NegativeBrush = new SolidColorBrush(Color.Parse("#EF4444"));
    private static readonly IBrush NegativeBgBrush = new SolidColorBrush(Color.Parse("#2AEF4444"));
    private static readonly BoxShadows NegativeShadow = BoxShadows.Parse("0 4 15 0 #20EF4444");

    private readonly IUnitOfWork _uow;
    private readonly ThemeService _themeService;
    private readonly IPdfService _pdfService;
    private readonly IYearContext? _yearContext;

    // KPI Cards
    [ObservableProperty] private decimal _gunlukSatis;
    [ObservableProperty] private decimal _toplamTahsilat;
    [ObservableProperty] private decimal _toplamBorc;
    [ObservableProperty] private decimal _toplamAlacak;
    [ObservableProperty] private decimal _toplamNakitVarligi;
    [ObservableProperty] private decimal _bugunOdenecek;
    [ObservableProperty] private decimal _bugunTahsilat;
    [ObservableProperty] private int _stokSayisi;
    [ObservableProperty] private decimal _bekleyenOdeme;
    [ObservableProperty] private double _stokDevirHizi;
    [ObservableProperty] private int _cariTahsilatSuresi;
    [ObservableProperty] private decimal _karlilik;
    [ObservableProperty] private double _karlilikOrani;
    [ObservableProperty] private decimal _toplamMaliyet;

    // Net Bakiye Durumu (Toplam Alacak - Toplam Borç)
    [ObservableProperty] private decimal _netBakiye;
    [ObservableProperty] private string _netBakiyeText = "+0,00 ₺";
    [ObservableProperty] private IBrush _netColorBrush = PositiveBrush;
    [ObservableProperty] private IBrush _netBgBrush = PositiveBgBrush;
    [ObservableProperty] private BoxShadows _netBoxShadow = PositiveShadow;
    [ObservableProperty] private bool _isNetPozitif = true;

    // Lists
    [ObservableProperty] private ObservableCollection<RecentTransactionItem> _recentTransactions = new();
    [ObservableProperty] private ObservableCollection<CariAlertItem> _riskyCaris = new();
    [ObservableProperty] private ObservableCollection<CariAlertItem> _payableCaris = new();
    [ObservableProperty] private ObservableCollection<Note> _notes = new();
    [ObservableProperty] private bool _hasRecentTransactions;
    [ObservableProperty] private bool _hasRiskyCaris;
    [ObservableProperty] private bool _hasPayableCaris;
    [ObservableProperty] private bool _hasNotes;

    // Avalonia-specific Chart properties (LiveCharts)
    [ObservableProperty] private ISeries[] _incomeExpenseSeries;
    [ObservableProperty] private Axis[] _incomeExpenseXAxis;
    
    [ObservableProperty] private ISeries[] _targetVsActualSeries;
    [ObservableProperty] private ICartesianAxis[] _targetVsActualXAxis;
    [ObservableProperty] private ICartesianAxis[] _targetVsActualYAxis;

    // Legacy Chart (Keeping for compatibility)
    [ObservableProperty] private ISeries[] _gelirDaigilimSeries;
    [ObservableProperty] private ISeries[] _satisGrafigiSeries;
    [ObservableProperty] private Axis[] _xAxis;

    // Theme switching
    [ObservableProperty] 
    [NotifyPropertyChangedFor(nameof(IsModernSaaS))]
    [NotifyPropertyChangedFor(nameof(IsEnterprise))]
    [NotifyPropertyChangedFor(nameof(IsWindowsFluent))]
    private string _currentTheme = "ModernSaaS";

    public bool IsModernSaaS => CurrentTheme == "ModernSaaS" || CurrentTheme == "IDEProfessional";
    public bool IsEnterprise => CurrentTheme == "Enterprise";
    public bool IsWindowsFluent => CurrentTheme == "WindowsFluent";

    // Trend Data Cache
    private decimal[] _chartMonthlyTargets = Array.Empty<decimal>();
    private decimal[] _chartMonthlyActuals = Array.Empty<decimal>();
    private string[] _chartMonthlyLabels = Array.Empty<string>();
    
    private decimal[] _chartWeeklyTargets = Array.Empty<decimal>();
    private decimal[] _chartWeeklyActuals = Array.Empty<decimal>();
    private string[] _chartWeeklyLabels = Array.Empty<string>();

    // State for Goal Toggling
    [ObservableProperty] private bool _isWeeklyGoal = true;
    public bool DisableAutoRefresh { get; set; } = false;

    partial void OnCurrentThemeChanged(string value)
    {
        OnPropertyChanged(nameof(IsModernSaaS));
        OnPropertyChanged(nameof(IsEnterprise));
        OnPropertyChanged(nameof(IsWindowsFluent));
    }

    public DashboardViewModel(
        IUnitOfWork uow, 
        ThemeService themeService, 
        IPdfService pdfService, 
        IYearContext? yearContext = null, 
        bool disableAutoRefresh = false)
    {
        DisableAutoRefresh = disableAutoRefresh;
        System.Diagnostics.Debug.WriteLine("=== DashboardViewModel Constructor START ===");
        
        _uow = uow;
        _themeService = themeService;
        _pdfService = pdfService;
        _yearContext = yearContext;

        if (_yearContext != null)
        {
            _yearContext.YearChanged += (newYear) =>
            {
                if (!DisableAutoRefresh)
                {
                    _ = LoadStatsAsync();
                }
            };
        }
        
        // Set initial theme
        CurrentTheme = _themeService.CurrentTheme.ToString();
        System.Diagnostics.Debug.WriteLine($"CurrentTheme: {CurrentTheme}");
        
        _themeService.ThemeChanged += OnThemeChanged;
        WeakReferenceMessenger.Default.Register<FinancialDataChangedMessage>(this, (r, m) =>
        {
            if (!DisableAutoRefresh)
                _ = LoadStatsAsync();
        });

        // Initialize LiveCharts series
        IncomeExpenseSeries = Array.Empty<ISeries>();
        IncomeExpenseXAxis = new Axis[] { new Axis { Labels = Array.Empty<string>() } };
        TargetVsActualSeries = Array.Empty<ISeries>();
        TargetVsActualXAxis = Array.Empty<ICartesianAxis>();
        TargetVsActualYAxis = new ICartesianAxis[] { new Axis { MinLimit = 0 } };
        GelirDaigilimSeries = Array.Empty<ISeries>();
        SatisGrafigiSeries = Array.Empty<ISeries>();
        XAxis = Array.Empty<Axis>();
        
        // Initial data
        RecentTransactions = new ObservableCollection<RecentTransactionItem>();
        RiskyCaris = new ObservableCollection<CariAlertItem>();
        PayableCaris = new ObservableCollection<CariAlertItem>();
        
        System.Diagnostics.Debug.WriteLine("=== DashboardViewModel Constructor END ===");
    }

    protected override async Task InvokeOnUIThreadAsync(Action action)
    {
        await Dispatcher.UIThread.InvokeAsync(action);
    }

    private void OnThemeChanged(ThemeService.AppTheme theme)
    {
        CurrentTheme = theme.ToString();
    }

    public override void OnNavigatedTo()
    {
        if (!DisableAutoRefresh)
        {
            _ = LoadStatsAsync();
        }
    }

    [RelayCommand]
    private void QuickAction(string action)
    {
        switch (action)
        {
            case "Alis":
                // Open Alış Faturası form directly
                var alisVm = new FaturaDetayViewModel(_uow, _pdfService, null, "Alış");
                alisVm.RequestClose += () => WeakReferenceMessenger.Default.Send(new NavigationRequestMessage(typeof(DashboardViewModel)));
                WeakReferenceMessenger.Default.Send(new NavigateViewModelMessage(alisVm));
                break;

            case "Satis":
                // Open Satış Faturası form directly
                var satisVm = new FaturaDetayViewModel(_uow, _pdfService, null, "Satış");
                satisVm.RequestClose += () => WeakReferenceMessenger.Default.Send(new NavigationRequestMessage(typeof(DashboardViewModel)));
                WeakReferenceMessenger.Default.Send(new NavigateViewModelMessage(satisVm));
                break;

            case "Tahsilat":
            case "Odeme":
                WeakReferenceMessenger.Default.Send(new NavigationRequestMessage(typeof(SVM.FinansViewModel)));
                break;

            case "Siparis":
                // Open Sipariş Formu directly
                var siparisVm = new SiparisDetayViewModel(_uow, null);
                siparisVm.RequestClose += () => WeakReferenceMessenger.Default.Send(new NavigationRequestMessage(typeof(DashboardViewModel)));
                WeakReferenceMessenger.Default.Send(new NavigateViewModelMessage(siparisVm));
                break;

            case "Teklif":
                // Open Teklif Formu directly
                var teklifVm = new TeklifDetayViewModel(_uow, null);
                teklifVm.RequestClose += () => WeakReferenceMessenger.Default.Send(new NavigationRequestMessage(typeof(DashboardViewModel)));
                WeakReferenceMessenger.Default.Send(new NavigateViewModelMessage(teklifVm));
                break;

            case "Maliyet":
                WeakReferenceMessenger.Default.Send(new NavigationRequestMessage(typeof(AVM.MaliyetHesaplamaViewModel)));
                break;
            case "Refresh":
                _ = LoadStatsAsync();
                break;
            case "AddNote":
                _ = AddNoteAsync();
                break;
        }
    }

    [RelayCommand]
    private async Task AddNoteAsync()
    {
        // Simple Input Box for Title and Content could be complex in Avalonia without custom view.
        // For now, let's just add a placeholder and the user can edit or we can use a message to trigger UI.
        var newNote = new Note { Title = "Yeni Not", Content = "İçerik girin...", RelatedType = "General", Color = "#FBBF24" };
        await _uow.Notes.SaveAsync(newNote);
        await LoadNotesAsync();
    }

    [RelayCommand]
    private async Task DeleteNote(Note note)
    {
        if (note == null) return;
        await _uow.Notes.DeleteAsync(note.Id);
        await LoadNotesAsync();
    }

    [RelayCommand]
    private async Task PinNote(Note note)
    {
        if (note == null) return;
        note.IsPinned = !note.IsPinned;
        await _uow.Notes.SaveAsync(note);
        await LoadNotesAsync();
    }

    [RelayCommand]
    private void ToggleGoalPeriod(string period)
    {
        IsWeeklyGoal = period == "Weekly";
        UpdateGoalChart();
    }

    private void UpdateGoalChart()
    {
        var targets = IsWeeklyGoal ? _chartWeeklyTargets : _chartMonthlyTargets;
        var actuals = IsWeeklyGoal ? _chartWeeklyActuals : _chartMonthlyActuals;
        var labels = IsWeeklyGoal ? _chartWeeklyLabels : _chartMonthlyLabels;
        
        var targetColor = new SKColor(255, 107, 0); // Vivid Orange
        var realizedColor = new SKColor(0, 230, 118); // Vivid Green

        TargetVsActualSeries = new ISeries[]
        {
            new LineSeries<decimal>
            {
                Name = "Hedef",
                Values = targets,
                Stroke = new SolidColorPaint(targetColor, 3),
                Fill = new LinearGradientPaint(
                    new SKColor[] { targetColor.WithAlpha(60), targetColor.WithAlpha(0) },
                    new SKPoint(0.5f, 0), new SKPoint(0.5f, 1)),
                GeometrySize = 0,
                LineSmoothness = 0.6
            },
            new LineSeries<decimal>
            {
                Name = "Gerçekleşen",
                Values = actuals,
                Stroke = new SolidColorPaint(realizedColor, 3),
                Fill = new LinearGradientPaint(
                    new SKColor[] { realizedColor.WithAlpha(80), realizedColor.WithAlpha(0) },
                    new SKPoint(0.5f, 0), new SKPoint(0.5f, 1)),
                GeometrySize = 8,
                GeometryFill = new SolidColorPaint(SKColors.White),
                GeometryStroke = new SolidColorPaint(realizedColor, 3),
                LineSmoothness = 0.6
            }
        };

        var maxT = targets.Any() ? targets.Max() : 0;
        var maxA = actuals.Any() ? actuals.Max() : 0;
        var maxValue = Math.Max(maxT, maxA);
        var maxY = maxValue < 10 ? 100 : (decimal?)null;

        TargetVsActualYAxis = new ICartesianAxis[]
        {
            new Axis
            {
                MinLimit = 0,
                MaxLimit = (double?)maxY,
                MinStep = 1,
                SeparatorsPaint = new SolidColorPaint(SKColors.Gray.WithAlpha(20)) { StrokeThickness = 1 },
                LabelsPaint = new SolidColorPaint(SKColors.Gray),
                TextSize = 14
            }
        };

        TargetVsActualXAxis = new ICartesianAxis[]
        {
            new Axis
            {
                Labels = labels,
                LabelsPaint = new SolidColorPaint(SKColors.Gray),
                TextSize = 14
            }
        };
    }

    public async Task LoadStatsAsync()
    {
        try
        {
            await LoadGoalTrackingDataAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LoadStatsAsync GoalTracking Error: {ex.Message}");
        }

        try
        {
            await LoadKPICardsAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LoadStatsAsync KPI Error: {ex.Message}");
        }

        try
        {
            await LoadListsAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LoadStatsAsync Lists Error: {ex.Message}");
        }
    }

    private async Task LoadGoalTrackingDataAsync()
    {
        var today = DateTime.Now;
        var currentYear = _yearContext?.CurrentYear ?? today.Year;
        var currentMonth = today.Month;

        var monthlyTargets = await _uow.GetSatisHedefleriAsync(currentYear);
        var weeklyTargets = await _uow.GetHaftalikSatisHedefleriAsync();

        // Monthly
        var mTargets = new List<decimal>();
        var mActuals = new List<decimal>();
        var mLabels = new List<string>();
        var monthNames = CultureInfo.GetCultureInfo("tr-TR").DateTimeFormat.AbbreviatedMonthNames;

        for (int m = 1; m <= 12; m++)
        {
            var mt = monthlyTargets.FirstOrDefault(x => x.Yil == currentYear && x.Ay == m);
            mTargets.Add(mt?.HedefTutari ?? 0);

            var startOfMonthDate = new DateTime(currentYear, m, 1);
            var endOfMonthDate = startOfMonthDate.AddMonths(1).AddDays(-1);
            
            var ma = await _uow.Faturalar.GetSumAsync(startOfMonthDate, endOfMonthDate, "Satış");
            mActuals.Add(ma);
            mLabels.Add(monthNames[m - 1]);
        }
        _chartMonthlyTargets = mTargets.ToArray();
        _chartMonthlyActuals = mActuals.ToArray();
        _chartMonthlyLabels = mLabels.ToArray();

        // Weekly
        var wTargets = new List<decimal>();
        var wActuals = new List<decimal>();
        var wLabels = new List<string>();
        
        var weeksOfMonth = new List<int>();
        int totalWeeksInYear = ISOWeek.GetWeeksInYear(currentYear);
        for (int w = 1; w <= totalWeeksInYear; w++)
        {
            try {
                var th = ISOWeek.ToDateTime(currentYear, w, DayOfWeek.Thursday);
                if (th.Month == currentMonth) weeksOfMonth.Add(w);
            } catch { }
        }

        decimal fallbackWeekly = 0;
        var currentMonthTargetObj = monthlyTargets.FirstOrDefault(x => x.Yil == currentYear && x.Ay == currentMonth);
        if (currentMonthTargetObj != null && weeksOfMonth.Count > 0)
        {
             fallbackWeekly = Math.Round(currentMonthTargetObj.HedefTutari / weeksOfMonth.Count, 2);
        }

        foreach (var w in weeksOfMonth)
        {
            var wtObj = weeklyTargets.FirstOrDefault(x => x.Yil == currentYear && x.Hafta == w);
            decimal wt = wtObj?.HedefTutari ?? 0;
            if (wt == 0) wt = fallbackWeekly;

            wTargets.Add(wt);

            var startOfWeek = ISOWeek.ToDateTime(currentYear, w, DayOfWeek.Monday);
            var endOfWeek = startOfWeek.AddDays(7).AddSeconds(-1);

            var wa = await _uow.Faturalar.GetSumAsync(startOfWeek, endOfWeek, "Satış");
            wActuals.Add(wa);
            wLabels.Add($"{w}.H");
        }
        _chartWeeklyTargets = wTargets.ToArray();
        _chartWeeklyActuals = wActuals.ToArray();
        _chartWeeklyLabels = wLabels.ToArray();

        await InvokeOnUIThreadAsync(() => UpdateGoalChart());
    }

    private async Task LoadKPICardsAsync()
    {
        var stats = await _uow.GetDashboardStatsAsync();
        await InvokeOnUIThreadAsync(() => 
        {
            GunlukSatis = stats.GunlukCiro;
            ToplamTahsilat = stats.ToplamTahsilat; 
            ToplamBorc = stats.ToplamBorc;
            ToplamAlacak = stats.ToplamAlacak;
            ToplamNakitVarligi = stats.ToplamNakitVarligi;
            BugunOdenecek = stats.BugunOdenecek;
            BugunTahsilat = stats.BugunTahsilat;
            StokSayisi = stats.KritikStokSayisi;
            BekleyenOdeme = stats.BekleyenOdeme;
            StokDevirHizi = stats.StokDevirHizi;
            CariTahsilatSuresi = stats.CariTahsilatSuresi;
            Karlilik = stats.Karlilik;
            KarlilikOrani = stats.KarlilikOrani;
            ToplamMaliyet = stats.ToplamMaliyet;

            UpdateNetDurum();
        });
    }

    partial void OnToplamAlacakChanged(decimal value) => UpdateNetDurum();
    partial void OnToplamBorcChanged(decimal value) => UpdateNetDurum();

    private void UpdateNetDurum()
    {
        var fark = ToplamAlacak - ToplamBorc;
        NetBakiye = fark;
        if (fark >= 0)
        {
            NetBakiyeText = $"+{fark:N2} ₺";
            NetColorBrush = PositiveBrush;
            NetBgBrush = PositiveBgBrush;
            NetBoxShadow = PositiveShadow;
            IsNetPozitif = true;
        }
        else
        {
            NetBakiyeText = $"-{Math.Abs(fark):N2} ₺";
            NetColorBrush = NegativeBrush;
            NetBgBrush = NegativeBgBrush;
            NetBoxShadow = NegativeShadow;
            IsNetPozitif = false;
        }
    }

    private async Task LoadListsAsync()
    {
        var recents = await _uow.GetRecentTransactionsAsync();
        var risks = await _uow.GetRiskyCarisAsync();
        var payables = await _uow.GetPayableCarisAsync();

        await InvokeOnUIThreadAsync(() =>
        {
            RecentTransactions = new ObservableCollection<RecentTransactionItem>(recents ?? new List<RecentTransactionItem>());
            RiskyCaris = new ObservableCollection<CariAlertItem>(risks ?? new List<CariAlertItem>());
            PayableCaris = new ObservableCollection<CariAlertItem>(payables ?? new List<CariAlertItem>());
            
            HasRecentTransactions = RecentTransactions.Any();
            HasRiskyCaris = RiskyCaris.Any();
            HasPayableCaris = PayableCaris.Any();
        });

        await LoadNotesAsync();
    }

    private async Task LoadNotesAsync()
    {
        var notesList = await _uow.Notes.GetByRelatedAsync("General", 0);
        await InvokeOnUIThreadAsync(() =>
        {
            Notes = new ObservableCollection<Note>(notesList);
            HasNotes = Notes.Any();
        });
    }
}
