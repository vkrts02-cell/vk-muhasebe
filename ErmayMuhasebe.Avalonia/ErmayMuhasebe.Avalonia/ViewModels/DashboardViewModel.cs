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
using Avalonia;
using Avalonia.Media;
using SVM = ErmayMuhasebe.Shared.ViewModels;
using AVM = ErmayMuhasebe.Avalonia.ViewModels;

using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView.Extensions;

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

    private static readonly IBrush GoalGreenGradient = new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
        GradientStops = new GradientStops
        {
            new GradientStop(Color.Parse("#34D399"), 0),
            new GradientStop(Color.Parse("#10B981"), 1)
        }
    };

    private static readonly IBrush GoalRedGradient = new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
        GradientStops = new GradientStops
        {
            new GradientStop(Color.Parse("#F87171"), 0),
            new GradientStop(Color.Parse("#EF4444"), 1)
        }
    };

    private static readonly IBrush GoalGreenSolid = new SolidColorBrush(Color.Parse("#10B981"));
    private static readonly IBrush GoalRedSolid = new SolidColorBrush(Color.Parse("#EF4444"));

    private static readonly BoxShadows GoalGreenShadow = BoxShadows.Parse("0 0 18 3 #4010B981");
    private static readonly BoxShadows GoalRedShadow = BoxShadows.Parse("0 0 18 3 #40EF4444");

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

    // Harman Model 2 Apple Radial Ring & Metrikler
    private readonly ObservableValue _goalRadialObservableValue = new(0);
    [ObservableProperty] private ISeries[] _goalRadialSeries = Array.Empty<ISeries>();
    [ObservableProperty] private double _goalSweepAngle = 0;
    [ObservableProperty] private IBrush _goalProgressBrush = GoalRedGradient;
    [ObservableProperty] private IBrush _goalProgressColor = GoalRedSolid;
    [ObservableProperty] private BoxShadows _goalPulseShadow = GoalRedShadow;
    [ObservableProperty] private decimal _totalGoalTarget;
    [ObservableProperty] private decimal _totalGoalActual;
    [ObservableProperty] private int _goalCompletionRate;
    [ObservableProperty] private string _goalCompletionRateText = "0%";
    [ObservableProperty] private string _totalGoalTargetText = "₺ 0";
    [ObservableProperty] private string _totalGoalActualText = "₺ 0";

    // Legacy Chart (Keeping for compatibility)
    [ObservableProperty] private ISeries[] _gelirDaigilimSeries;
    [ObservableProperty] private ISeries[] _satisGrafigiSeries;
    [ObservableProperty] private Axis[] _xAxis;

    // Theme switching
    [ObservableProperty] 
    [NotifyPropertyChangedFor(nameof(IsModernSaaS))]
    [NotifyPropertyChangedFor(nameof(IsObsidianGlow))]
    [NotifyPropertyChangedFor(nameof(IsCupertinoGlass))]
    [NotifyPropertyChangedFor(nameof(IsEnterprise))]
    [NotifyPropertyChangedFor(nameof(IsWindowsFluent))]
    [NotifyPropertyChangedFor(nameof(IsPane))]
    [NotifyPropertyChangedFor(nameof(IsRivet))]
    private string _currentTheme = "ModernSaaS";

    public bool IsModernSaaS => CurrentTheme == "ModernSaaS" || CurrentTheme == "IDEProfessional";
    public bool IsObsidianGlow => CurrentTheme == "ObsidianGlow";
    public bool IsCupertinoGlass => CurrentTheme == "CupertinoGlass";
    public bool IsEnterprise => CurrentTheme == "Enterprise";
    public bool IsWindowsFluent => CurrentTheme == "WindowsFluent";
    public bool IsPane => CurrentTheme == "Pane";
    public bool IsRivet => CurrentTheme == "Rivet";

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
        OnPropertyChanged(nameof(IsObsidianGlow));
        OnPropertyChanged(nameof(IsCupertinoGlass));
        OnPropertyChanged(nameof(IsEnterprise));
        OnPropertyChanged(nameof(IsWindowsFluent));
        OnPropertyChanged(nameof(IsPane));
        OnPropertyChanged(nameof(IsRivet));
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
        TargetVsActualXAxis = new ICartesianAxis[]
        {
            new Axis
            {
                Labels = Array.Empty<string>(),
                LabelsPaint = new SolidColorPaint(new SKColor(148, 163, 184)),
                TextSize = 11,
                SeparatorsPaint = null,
                Padding = new LiveChartsCore.Drawing.Padding(0, 12, 0, 0)
            }
        };
        TargetVsActualYAxis = new ICartesianAxis[]
        {
            new Axis
            {
                MinLimit = 0,
                MinStep = 1,
                Labeler = val => val >= 1_000_000 ? $"{val / 1_000_000:0.#}M ₺" : (val >= 1_000 ? $"{val / 1_000:0}K ₺" : $"{val:0} ₺"),
                SeparatorsPaint = new SolidColorPaint(new SKColor(0, 122, 204, 130)) { StrokeThickness = 1 },
                LabelsPaint = new SolidColorPaint(new SKColor(148, 163, 184)),
                TextSize = 11,
                Padding = new LiveChartsCore.Drawing.Padding(0, 0, 14, 0)
            }
        };
        GelirDaigilimSeries = Array.Empty<ISeries>();
        SatisGrafigiSeries = Array.Empty<ISeries>();
        XAxis = Array.Empty<Axis>();
        InitGoalRadialSeries();
        
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

    private void InitGoalRadialSeries()
    {
        var ringProgressColor = new SKColor(239, 68, 68); // #EF4444 Rose Red
        var ringProgressGlow = new SKColor(248, 113, 113); // #F87171
        var ringBgColor = new SKColor(255, 255, 255, 18);

        GoalRadialSeries = GaugeGenerator.BuildSolidGauge(
            new GaugeItem(_goalRadialObservableValue, series =>
            {
                series.Name = "Gerçekleşen";
                series.Fill = new LinearGradientPaint(
                    new SKColor[] { ringProgressGlow, ringProgressColor },
                    new SKPoint(0, 0), new SKPoint(1, 1));
                series.InnerRadius = 45;
                series.MaxRadialColumnWidth = 12;
                series.Pushout = 0;
                series.AnimationsSpeed = TimeSpan.FromMilliseconds(800);
                series.EasingFunction = LiveChartsCore.EasingFunctions.CubicOut;
            }),
            new GaugeItem(GaugeItem.Background, series =>
            {
                series.InnerRadius = 45;
                series.MaxRadialColumnWidth = 12;
                series.Fill = new SolidColorPaint(ringBgColor);
                series.Pushout = 0;
                series.AnimationsSpeed = TimeSpan.Zero;
            })
        ).ToArray();
    }

    private decimal[] _lastRenderedTargets = Array.Empty<decimal>();
    private decimal[] _lastRenderedActuals = Array.Empty<decimal>();
    private string[] _lastRenderedLabels = Array.Empty<string>();
    private bool? _lastRenderedIsWeekly = null;

    private void UpdateGoalChart()
    {
        var targets = IsWeeklyGoal ? _chartWeeklyTargets : _chartMonthlyTargets;
        var actuals = IsWeeklyGoal ? _chartWeeklyActuals : _chartMonthlyActuals;
        var labels = IsWeeklyGoal ? _chartWeeklyLabels : _chartMonthlyLabels;

        // Toplamları ve Apple radyal halkasını hesapla
        var totalTarget = targets.Sum();
        var totalActual = actuals.Sum();
        TotalGoalTarget = totalTarget;
        TotalGoalActual = totalActual;
        TotalGoalTargetText = $"₺ {totalTarget:N0}";
        TotalGoalActualText = $"₺ {totalActual:N0}";

        var completionRate = totalTarget > 0 ? (int)Math.Min(999, Math.Round((totalActual / totalTarget) * 100m)) : 0;
        GoalCompletionRate = completionRate;
        GoalCompletionRateText = $"{completionRate}%";

        var ringProgressColor = completionRate >= 100 
            ? new SKColor(16, 185, 129) // #10B981 Emerald Green
            : new SKColor(239, 68, 68); // #EF4444 Rose Red
        var ringProgressGlow = completionRate >= 100 
            ? new SKColor(52, 211, 153) // #34D399
            : new SKColor(248, 113, 113); // #F87171

        if (GoalRadialSeries == null || GoalRadialSeries.Length == 0)
        {
            InitGoalRadialSeries();
        }
        else if (GoalRadialSeries[0] is PieSeries<ObservableValue> activeSeries)
        {
            activeSeries.Fill = new LinearGradientPaint(
                new SKColor[] { ringProgressGlow, ringProgressColor },
                new SKPoint(0, 0), new SKPoint(1, 1));
        }

        if (completionRate >= 100)
        {
            GoalProgressBrush = GoalGreenGradient;
            GoalProgressColor = GoalGreenSolid;
            GoalPulseShadow = GoalGreenShadow;
        }
        else
        {
            GoalProgressBrush = GoalRedGradient;
            GoalProgressColor = GoalRedSolid;
            GoalPulseShadow = GoalRedShadow;
        }

        double targetAngle = completionRate >= 100 
            ? 360.0 
            : (completionRate > 0 ? Math.Min(360.0, (completionRate / 100.0) * 360.0) : 0.0);

        // Otomatik veya arka plan güncellemelerinde animasyonu çalıştırma; sadece hover durumunda çalışacak
        if (!_isRingAnimating)
        {
            GoalSweepAngle = targetAngle;
        }

        _goalRadialObservableValue.Value = (double)Math.Min(100, Math.Max(0, completionRate));

        // Veriler tamamen aynıysa ve seri zaten ekrandaysa yeniden çizim yapıp animasyonu sıfırlama
        if (_lastRenderedIsWeekly == IsWeeklyGoal &&
            TargetVsActualSeries != null && TargetVsActualSeries.Length == 2 &&
            _lastRenderedTargets.SequenceEqual(targets) &&
            _lastRenderedActuals.SequenceEqual(actuals) &&
            _lastRenderedLabels.SequenceEqual(labels))
        {
            return;
        }

        // Seri zaten oluşturulmuşsa ve aynı periyotta sadece veri güncelleniyorsa (arka plan yenilemesi),
        // seriyi sıfırdan oluşturmak yerine sadece Values ve Eksen güncellemesi yap
        if (_lastRenderedIsWeekly == IsWeeklyGoal &&
            TargetVsActualSeries != null && TargetVsActualSeries.Length == 2 &&
            TargetVsActualSeries[0] is ColumnSeries<decimal> sTarget &&
            TargetVsActualSeries[1] is ColumnSeries<decimal> sActual)
        {
            sTarget.Values = targets;
            sActual.Values = actuals;

            if (TargetVsActualXAxis != null && TargetVsActualXAxis.Length > 0 && TargetVsActualXAxis[0] is Axis xAxis)
            {
                xAxis.Labels = labels;
            }

            var maxT = targets.Any() ? targets.Max() : 0;
            var maxA = actuals.Any() ? actuals.Max() : 0;
            var maxValue = Math.Max(maxT, maxA);
            var maxY = maxValue < 10 ? 100 : (decimal?)null;

            if (TargetVsActualYAxis != null && TargetVsActualYAxis.Length > 0 && TargetVsActualYAxis[0] is Axis yAxis)
            {
                yAxis.MaxLimit = (double?)maxY;
            }

            _lastRenderedTargets = (decimal[])targets.Clone();
            _lastRenderedActuals = (decimal[])actuals.Clone();
            _lastRenderedLabels = (string[])labels.Clone();
            _lastRenderedIsWeekly = IsWeeklyGoal;
            return;
        }

        // Yeşil (Hedef) & Kırmızı (Gerçekleşen) Çift Kolon Barları + #007ACC Mavi Izgara Çizgileri
        var targetGreen = new SKColor(16, 185, 129); // #10B981 Emerald Green
        var targetGreenGlow = new SKColor(52, 211, 153); // #34D399 Light Green Glow
        var realizedRed = new SKColor(239, 68, 68); // #EF4444 Rose Red
        var realizedRedGlow = new SKColor(248, 113, 113); // #F87171 Light Red Glow

        TargetVsActualSeries = new ISeries[]
        {
            new ColumnSeries<decimal>
            {
                Name = "Hedef",
                Values = targets,
                Fill = new LinearGradientPaint(
                    new SKColor[] { targetGreenGlow, targetGreen },
                    new SKPoint(0.5f, 0), new SKPoint(0.5f, 1)),
                MaxBarWidth = 16,
                Rx = 4,
                Ry = 4,
                Padding = 3
            },
            new ColumnSeries<decimal>
            {
                Name = "Gerçekleşen",
                Values = actuals,
                Fill = new LinearGradientPaint(
                    new SKColor[] { realizedRedGlow, realizedRed },
                    new SKPoint(0.5f, 0), new SKPoint(0.5f, 1)),
                MaxBarWidth = 16,
                Rx = 4,
                Ry = 4,
                Padding = 3
            }
        };

        var maxTarget = targets.Any() ? targets.Max() : 0;
        var maxActual = actuals.Any() ? actuals.Max() : 0;
        var maxVal = Math.Max(maxTarget, maxActual);
        var maxYVal = maxVal < 10 ? 100 : (decimal?)null;

        TargetVsActualYAxis = new ICartesianAxis[]
        {
            new Axis
            {
                MinLimit = 0,
                MaxLimit = (double?)maxYVal,
                MinStep = 1,
                Labeler = val => val >= 1_000_000 ? $"{val / 1_000_000:0.#}M ₺" : (val >= 1_000 ? $"{val / 1_000:0}K ₺" : $"{val:0} ₺"),
                SeparatorsPaint = new SolidColorPaint(new SKColor(0, 122, 204, 130)) { StrokeThickness = 1 },
                LabelsPaint = new SolidColorPaint(new SKColor(148, 163, 184)),
                TextSize = 11,
                Padding = new LiveChartsCore.Drawing.Padding(0, 0, 14, 0)
            }
        };

        TargetVsActualXAxis = new ICartesianAxis[]
        {
            new Axis
            {
                Labels = labels,
                LabelsPaint = new SolidColorPaint(new SKColor(148, 163, 184)),
                TextSize = 11,
                SeparatorsPaint = null,
                Padding = new LiveChartsCore.Drawing.Padding(0, 12, 0, 0)
            }
        };

        _lastRenderedTargets = (decimal[])targets.Clone();
        _lastRenderedActuals = (decimal[])actuals.Clone();
        _lastRenderedLabels = (string[])labels.Clone();
        _lastRenderedIsWeekly = IsWeeklyGoal;
    }

    private bool _isRingAnimating = false;

    /// <summary>
    /// Mouse üzerine geldiğinde veya veri yüklendiğinde dairesel hedef halkası animasyonunu oynatır
    /// </summary>
    [RelayCommand]
    public async Task PlayGoalRingAnimationAsync()
    {
        if (_isRingAnimating) return;
        _isRingAnimating = true;

        try
        {
            double targetAngle = GoalCompletionRate >= 100 
                ? 360.0 
                : (GoalCompletionRate > 0 ? Math.Min(360.0, (GoalCompletionRate / 100.0) * 360.0) : 0.0);

            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                GoalSweepAngle = 0;
                await Task.Delay(40);

                if (targetAngle <= 0)
                {
                    // %0 tamamlanma durumunda etkileyici bir tam tur radar taraması yapıp sıfıra dönsün
                    GoalSweepAngle = 360;
                    await Task.Delay(850);
                    GoalSweepAngle = 0;
                    await Task.Delay(850);
                }
                else
                {
                    GoalSweepAngle = targetAngle;
                    await Task.Delay(850);
                }
            });
        }
        catch
        {
            GoalSweepAngle = GoalCompletionRate >= 100 
                ? 360.0 
                : (GoalCompletionRate > 0 ? Math.Min(360.0, (GoalCompletionRate / 100.0) * 360.0) : 0.0);
        }
        finally
        {
            _isRingAnimating = false;
        }
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
            if (GunlukSatis != stats.GunlukCiro) GunlukSatis = stats.GunlukCiro;
            if (ToplamTahsilat != stats.ToplamTahsilat) ToplamTahsilat = stats.ToplamTahsilat; 
            if (ToplamBorc != stats.ToplamBorc) ToplamBorc = stats.ToplamBorc;
            if (ToplamAlacak != stats.ToplamAlacak) ToplamAlacak = stats.ToplamAlacak;
            if (ToplamNakitVarligi != stats.ToplamNakitVarligi) ToplamNakitVarligi = stats.ToplamNakitVarligi;
            if (BugunOdenecek != stats.BugunOdenecek) BugunOdenecek = stats.BugunOdenecek;
            if (BugunTahsilat != stats.BugunTahsilat) BugunTahsilat = stats.BugunTahsilat;
            if (StokSayisi != stats.KritikStokSayisi) StokSayisi = stats.KritikStokSayisi;
            if (BekleyenOdeme != stats.BekleyenOdeme) BekleyenOdeme = stats.BekleyenOdeme;
            if (StokDevirHizi != stats.StokDevirHizi) StokDevirHizi = stats.StokDevirHizi;
            if (CariTahsilatSuresi != stats.CariTahsilatSuresi) CariTahsilatSuresi = stats.CariTahsilatSuresi;
            if (Karlilik != stats.Karlilik) Karlilik = stats.Karlilik;
            if (KarlilikOrani != stats.KarlilikOrani) KarlilikOrani = stats.KarlilikOrani;
            if (ToplamMaliyet != stats.ToplamMaliyet) ToplamMaliyet = stats.ToplamMaliyet;

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
            if (!AreRecentTransactionsEqual(RecentTransactions, recents))
            {
                RecentTransactions = new ObservableCollection<RecentTransactionItem>(recents ?? new List<RecentTransactionItem>());
                HasRecentTransactions = RecentTransactions.Any();
            }

            if (!AreCariAlertsEqual(RiskyCaris, risks))
            {
                RiskyCaris = new ObservableCollection<CariAlertItem>(risks ?? new List<CariAlertItem>());
                HasRiskyCaris = RiskyCaris.Any();
            }

            if (!AreCariAlertsEqual(PayableCaris, payables))
            {
                PayableCaris = new ObservableCollection<CariAlertItem>(payables ?? new List<CariAlertItem>());
                HasPayableCaris = PayableCaris.Any();
            }
        });

        await LoadNotesAsync();
    }

    private static bool AreRecentTransactionsEqual(IList<RecentTransactionItem> list1, IList<RecentTransactionItem>? list2)
    {
        if (list2 == null) return list1.Count == 0;
        if (list1.Count != list2.Count) return false;
        for (int i = 0; i < list1.Count; i++)
        {
            if (list1[i].Title != list2[i].Title ||
                list1[i].Description != list2[i].Description ||
                list1[i].Amount != list2[i].Amount ||
                list1[i].Date != list2[i].Date)
                return false;
        }
        return true;
    }

    private static bool AreCariAlertsEqual(IList<CariAlertItem> list1, IList<CariAlertItem>? list2)
    {
        if (list2 == null) return list1.Count == 0;
        if (list1.Count != list2.Count) return false;
        for (int i = 0; i < list1.Count; i++)
        {
            if (list1[i].CariId != list2[i].CariId ||
                list1[i].Bakiye != list2[i].Bakiye ||
                list1[i].OrtalamaVade != list2[i].OrtalamaVade)
                return false;
        }
        return true;
    }

    private async Task LoadNotesAsync()
    {
        var notesList = await _uow.Notes.GetByRelatedAsync("General", 0);
        await InvokeOnUIThreadAsync(() =>
        {
            if (!AreNotesEqual(Notes, notesList))
            {
                Notes = new ObservableCollection<Note>(notesList);
                HasNotes = Notes.Any();
            }
        });
    }

    private static bool AreNotesEqual(IList<Note> list1, IList<Note>? list2)
    {
        if (list2 == null) return list1.Count == 0;
        if (list1.Count != list2.Count) return false;
        for (int i = 0; i < list1.Count; i++)
        {
            if (list1[i].Id != list2[i].Id ||
                list1[i].Title != list2[i].Title ||
                list1[i].Content != list2[i].Content ||
                list1[i].IsPinned != list2[i].IsPinned)
                return false;
        }
        return true;
    }
}
