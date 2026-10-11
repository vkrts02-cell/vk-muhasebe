using ErmayMuhasebe.Repositories;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using ErmayMuhasebe.Models;

namespace ErmayMuhasebe.Shared.ViewModels;

/// <summary>
/// Dashboard data model for goal tracking
/// </summary>
public class GoalTrackingItem
{
    public string Title { get; set; } = string.Empty;
    public decimal WeeklyTarget { get; set; }
    public decimal WeeklyActual { get; set; }
    public decimal MonthlyTarget { get; set; }
    public decimal MonthlyActual { get; set; }
    
    public double WeeklyProgress => (double)(WeeklyTarget == 0 ? 0 : (WeeklyActual / WeeklyTarget) * 100);
    public double MonthlyProgress => (double)(MonthlyTarget == 0 ? 0 : (MonthlyActual / MonthlyTarget) * 100);
    
    public string WeeklyProgressColor => WeeklyProgress >= 100 ? "#34D399" : (WeeklyProgress >= 70 ? "#FBBF24" : "#F87171");
    public string MonthlyProgressColor => MonthlyProgress >= 100 ? "#34D399" : (MonthlyProgress >= 70 ? "#FBBF24" : "#F87171");
}

/// <summary>
/// Chart data model for dashboard charts
/// Platform-agnostic data structure
/// </summary>
public class ChartDataSet
{
    public string Name { get; set; } = string.Empty;
    public decimal[] Values { get; set; } = Array.Empty<decimal>();
    public string[] Labels { get; set; } = Array.Empty<string>();
    public string Color { get; set; } = "#FFFFFF";
}

/// <summary>
/// Shared Dashboard ViewModel
/// Works with both Avalonia and Blazor
/// </summary>
public partial class DashboardViewModel : ViewModelBase
{
    protected readonly IUnitOfWork _uow;

    // KPI Cards
    [ObservableProperty] private decimal _gunlukSatis;
    [ObservableProperty] private decimal _toplamTahsilat;
    [ObservableProperty] private decimal _toplamBorc;
    [ObservableProperty] private decimal _toplamAlacak;
    [ObservableProperty] private decimal _toplamNakitVarligi;
    [ObservableProperty] private decimal _bugunOdenecek;
    [ObservableProperty] private decimal _bugunTahsilat;
    [ObservableProperty] private int _stokSayisi;
    
    // New KPIs
    [ObservableProperty] private double _stokDevirHizi;
    [ObservableProperty] private int _cariTahsilatSuresi;
    [ObservableProperty] private decimal _karlilik;
    [ObservableProperty] private double _karlilikOrani;

    // Lists
    [ObservableProperty] private ObservableCollection<RecentTransactionItem> _recentTransactions = new();
    [ObservableProperty] private ObservableCollection<CariAlertItem> _riskyCaris = new();
    [ObservableProperty] private ObservableCollection<CariAlertItem> _payableCaris = new();
    [ObservableProperty] private ObservableCollection<GoalTrackingItem> _goals = new();
    
    // Drill-down Command
    [RelayCommand]
    protected virtual void NavigateToDetail(string target)
    {
        // Platform specific navigation will be handled in derived classes
    }

    [ObservableProperty] private bool _hasRecentTransactions;
    [ObservableProperty] private bool _hasRiskyCaris;

    // Chart Data (Platform-agnostic)
    [ObservableProperty] private ChartDataSet[] _incomeExpenseData = Array.Empty<ChartDataSet>();
    [ObservableProperty] private ChartDataSet[] _targetVsActualData = Array.Empty<ChartDataSet>();
    [ObservableProperty] private ChartDataSet[] _stockDistributionData = Array.Empty<ChartDataSet>();
    
    // Trend Data Cache
    protected decimal[] _chartMonthlyTargets = Array.Empty<decimal>();
    protected decimal[] _chartMonthlyActuals = Array.Empty<decimal>();
    protected string[] _chartMonthlyLabels = Array.Empty<string>();
    
    protected decimal[] _chartWeeklyTargets = Array.Empty<decimal>();
    protected decimal[] _chartWeeklyActuals = Array.Empty<decimal>();
    protected string[] _chartWeeklyLabels = Array.Empty<string>();

    // State for Goal Toggling
    [ObservableProperty] private bool _isWeeklyGoal = true;

    public DashboardViewModel(IUnitOfWork uow)
    {
        System.Diagnostics.Debug.WriteLine("=== Shared DashboardViewModel Constructor START ===");
        
        _uow = uow;
        
        // Initial Data for UI feedback
        RecentTransactions = new ObservableCollection<RecentTransactionItem>
        {
            new RecentTransactionItem { Title = "Veri Yükleniyor...", Description = "Son işlemler hazırlanıyor", Amount = 0, Date = DateTime.Now }
        };
        RiskyCaris = new ObservableCollection<CariAlertItem>
        {
            new CariAlertItem { Unvan = "Veri Yükleniyor...", Bakiye = 0, OrtalamaVade = 0 }
        };
        
        HasRecentTransactions = true;
        HasRiskyCaris = true;
        IsLoading = false; // Changed to false to show UI immediately

        // Don't call LoadStatsAsync in constructor - it will be called in OnNavigatedTo
        System.Diagnostics.Debug.WriteLine("=== Shared DashboardViewModel Constructor END ===");
    }

    public bool DisableAutoRefresh { get; set; } = false;

    public override void OnNavigatedTo()
    {
        if (!DisableAutoRefresh)
        {
            _ = LoadStatsAsync();
        }
    }

    [RelayCommand]
    protected virtual void QuickAction(string action)
    {
        // Platform-specific navigation will be overridden
        // in platform-specific derived classes
    }

    [RelayCommand]
    private void ToggleGoalPeriod(string period)
    {
        IsWeeklyGoal = period == "Weekly";
        UpdateGoalChart();
    }

    protected virtual void UpdateGoalChart()
    {
        var targets = IsWeeklyGoal ? _chartWeeklyTargets : _chartMonthlyTargets;
        var actuals = IsWeeklyGoal ? _chartWeeklyActuals : _chartMonthlyActuals;
        var labels = IsWeeklyGoal ? _chartWeeklyLabels : _chartMonthlyLabels;
        
        // Create platform-agnostic chart data
        TargetVsActualData = new ChartDataSet[]
        {
            new ChartDataSet
            {
                Name = "Hedef",
                Values = targets,
                Labels = labels,
                Color = "#FF6B00" // Vivid Orange
            },
            new ChartDataSet
            {
                Name = "Gerçekleşen",
                Values = actuals,
                Labels = labels,
                Color = "#00E676" // Vivid Green
            }
        };
    }

    public async Task LoadStatsAsync(bool isSilent = false)
    {
        if (!isSilent && Goals.Count == 0 && TargetVsActualData.Length == 0)
        {
            IsLoading = true;
        }
        
        // 1. Goal Tracking Data
        try 
        {
            await LoadGoalTrackingDataAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Dashboard Goals Error: {ex}");
            ErrorMessage = "Hedef verileri yüklenirken hata oluştu.";
        }

        // 2. Main KPI Cards
        try
        {
            await LoadKPICardsAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Dashboard KPI Error: {ex}");
            ErrorMessage = "KPI verileri yüklenirken hata oluştu.";
        }

        // 3. Lists
        try
        {
            await LoadListsAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Dashboard Lists Error: {ex}");
            ErrorMessage = "Liste verileri yüklenirken hata oluştu.";
        }

        // 4. Charts - Income Expense
        try
        {
            await LoadIncomeExpenseChartAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Dashboard Chart Error: {ex}");
            ErrorMessage = "Grafik verileri yüklenirken hata oluştu.";
        }

        // 5. Stock Distribution
        try
        {
            await LoadStockChartAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Dashboard Stock Chart Error: {ex}");
        }

        IsLoading = false;
    }

    private async Task LoadGoalTrackingDataAsync()
    {
        var today = DateTime.Now;
        var currentYear = today.Year;
        var currentMonth = today.Month;

        // Fetch Data
        var allFaturalar = await _uow.Faturalar.GetAllAsync();
        var monthlyTargets = await _uow.GetSatisHedefleriAsync(currentYear);
        var weeklyTargets = await _uow.GetHaftalikSatisHedefleriAsync();

        // 1. Monthly Trend Calculation (Jan-Dec)
        var mTargets = new List<decimal>();
        var mActuals = new List<decimal>();
        var mLabels = new List<string>();
        string[] monthNames;
        try 
        {
            monthNames = System.Globalization.CultureInfo.GetCultureInfo("tr-TR").DateTimeFormat.AbbreviatedMonthNames;
        }
        catch
        {
            monthNames = System.Globalization.CultureInfo.InvariantCulture.DateTimeFormat.AbbreviatedMonthNames;
        }

        for (int m = 1; m <= 12; m++)
        {
            var mt = monthlyTargets.FirstOrDefault(x => x.Yil == currentYear && x.Ay == m);
            mTargets.Add(mt?.HedefTutari ?? 0);

            var ma = allFaturalar?
                .Where(f => !string.IsNullOrEmpty(f.Tur) && (f.Tur.Equals("Satış", StringComparison.OrdinalIgnoreCase) || f.Tur.Equals("Satis", StringComparison.OrdinalIgnoreCase)) && f.Tarih.Year == currentYear && f.Tarih.Month == m)
                .Sum(x => x.GenelToplam) ?? 0;
            mActuals.Add(ma);
            mLabels.Add(monthNames[m - 1]);
        }
        _chartMonthlyTargets = mTargets.ToArray();
        _chartMonthlyActuals = mActuals.ToArray();
        _chartMonthlyLabels = mLabels.ToArray();

        // 2. Weekly Trend Calculation (Weeks of Current Month)
        var wTargets = new List<decimal>();
        var wActuals = new List<decimal>();
        var wLabels = new List<string>();
        
        // Find weeks for this month
        var weeksOfMonth = new List<int>();
        int totalWeeks = System.Globalization.ISOWeek.GetWeeksInYear(currentYear);
        for (int w = 1; w <= totalWeeks; w++)
        {
            try {
                var th = System.Globalization.ISOWeek.ToDateTime(currentYear, w, DayOfWeek.Thursday);
                if (th.Month == currentMonth) weeksOfMonth.Add(w);
            } catch { }
        }

        // Fallback weekly target
        decimal fallbackWeekly = 0;
        var currentMonthTargarObj = monthlyTargets.FirstOrDefault(x => x.Yil == currentYear && x.Ay == currentMonth);
        if (currentMonthTargarObj != null && weeksOfMonth.Count > 0)
        {
             fallbackWeekly = Math.Round(currentMonthTargarObj.HedefTutari / weeksOfMonth.Count, 2);
        }

        foreach (var w in weeksOfMonth)
        {
            var wtObj = weeklyTargets.FirstOrDefault(x => x.Yil == currentYear && x.Hafta == w);
            decimal wt = wtObj?.HedefTutari ?? 0;
            if (wt == 0) wt = fallbackWeekly;

            wTargets.Add(wt);

            var wa = allFaturalar?
                .Where(f => !string.IsNullOrEmpty(f.Tur) && (f.Tur.Equals("Satış", StringComparison.OrdinalIgnoreCase) || f.Tur.Equals("Satis", StringComparison.OrdinalIgnoreCase)) && f.Tarih.Year == currentYear && System.Globalization.ISOWeek.GetWeekOfYear(f.Tarih) == w)
                .Sum(x => x.GenelToplam) ?? 0;
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
            StokDevirHizi = stats.StokDevirHizi;
            CariTahsilatSuresi = stats.CariTahsilatSuresi;
            Karlilik = stats.Karlilik;
            KarlilikOrani = stats.KarlilikOrani;
        });
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
        });
    }

    private async Task LoadIncomeExpenseChartAsync()
    {
        var incExpData = await _uow.GetMonthlyIncomeExpenseAsync();
        
        var incValues = incExpData.Select(x => x.Income).ToArray();
        var expValues = incExpData.Select(x => x.Expense).ToArray();
        var labels = incExpData.Select(x => x.Month).ToArray();

        await InvokeOnUIThreadAsync(() => {
            IncomeExpenseData = new ChartDataSet[]
            {
                new ChartDataSet 
                { 
                    Name = "Gelir",
                    Values = incValues,
                    Labels = labels,
                    Color = "#00E676" // Vivid Green
                },
                new ChartDataSet 
                { 
                    Name = "Gider",
                    Values = expValues,
                    Labels = labels,
                    Color = "#FF5252" // Vivid Red
                }
            };
        });
    }

    private async Task LoadStockChartAsync()
    {
        var stocks = await _uow.Stoklar.GetAllAsync();
        if (stocks == null || !stocks.Any()) return;

        var groups = stocks
            .GroupBy(s => s.Kategori ?? "Diğer")
            .Select(g => new { Label = g.Key, Value = (decimal)g.Count() })
            .OrderByDescending(x => x.Value)
            .Take(5)
            .ToList();

        var values = groups.Select(x => x.Value).ToArray();
        var labels = groups.Select(x => x.Label).ToArray();

        await InvokeOnUIThreadAsync(() => {
            StockDistributionData = new ChartDataSet[]
            {
                new ChartDataSet 
                { 
                    Name = "Stok Dağılımı",
                    Values = values,
                    Labels = labels,
                    Color = "#60A5FA"
                }
            };
        });
    }

    /// <summary>
    /// Platform-specific UI thread invocation
    /// Override this in platform-specific implementations
    /// </summary>
    protected override Task InvokeOnUIThreadAsync(Action action)
    {
        // Default: just execute synchronously
        action();
        return Task.CompletedTask;
    }
}
