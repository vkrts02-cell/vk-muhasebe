using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using ErmayMuhasebe.Avalonia.ViewModels;
using ErmayMuhasebe.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;

namespace ErmayMuhasebe.Avalonia.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
        // Ctrl+MouseWheel zoom support
        PointerWheelChanged += OnPointerWheelChanged;
        
        // Elastic UI: Listen for window resize to adjust scaling automatically
        this.SizeChanged += (s, e) => 
        {
            if (Application.Current is App app && app.Services != null)
            {
                var displayService = app.Services.GetService<DisplayService>();
                if (displayService != null && displayService.IsAutoScalingEnabled)
                {
                    displayService.AutoDetectScaling(true);
                }
            }
        };

        InitializeSidebarClickSupport();
        InitializeMobileSwipeBackSupport();
    }

    private Point _swipeStartPoint;
    private bool _isSwiping;

    private void InitializeMobileSwipeBackSupport()
    {
        // Mobile/Tablet için Swipe to Back Desteği
        if (global::Avalonia.Application.Current?.ApplicationLifetime is global::Avalonia.Controls.ApplicationLifetimes.ISingleViewApplicationLifetime)
        {
            this.AddHandler(PointerPressedEvent, (s, e) =>
            {
                _swipeStartPoint = e.GetPosition(this);
                _isSwiping = true;
            }, RoutingStrategies.Tunnel);

            this.AddHandler(PointerMovedEvent, (s, e) =>
            {
                if (_isSwiping)
                {
                    var currentPoint = e.GetPosition(this);
                    var deltaX = currentPoint.X - _swipeStartPoint.X;
                    var deltaY = System.Math.Abs(currentPoint.Y - _swipeStartPoint.Y);

                    // Sağa doğru en az 80 piksel kaydırılmışsa ve dikeyde fazla sapmamışsa geri dön
                    if (deltaX > 80 && deltaY < 60)
                    {
                        _isSwiping = false; // Tetiklenmeyi durdur
                        if (DataContext is MainViewModel vm && vm.GoBackCommand.CanExecute(null))
                        {
                            vm.GoBackCommand.Execute(null);
                            e.Handled = true;
                        }
                    }
                }
            }, RoutingStrategies.Tunnel);
            
            this.AddHandler(PointerReleasedEvent, (s, e) =>
            {
                _isSwiping = false;
            }, RoutingStrategies.Tunnel);
        }
    }

    private void InitializeSidebarClickSupport()
    {
        // Sol menüdeki tıklamaları (özellikle ikonlara tıklamayı) garantilemek için Tunneling kullanıyoruz.
        // Bu sayede ListBoxItem olayı yutsa bile biz en tepede yakalayıp menüyü açabiliyoruz.
        this.AddHandler(PointerPressedEvent, (s, e) => {
            if (DataContext is MainViewModel vm)
            {
                // Tıklanan yerin sol menü (Sidebar) içinde olup olmadığını kontrol et
                var visual = e.Source as Visual;
                bool isSidebarClick = false;
                while (visual != null)
                {
                    if (visual is ListBox lb && (lb.Name == "SidebarListBox" || lb.Name == "IDESidebarListBox" || lb.Name == "PaneSidebarListBox" || lb.Name == "RivetSidebarListBox"))
                    {
                        isSidebarClick = true;
                        break;
                    }
                    visual = visual.GetVisualParent() as Visual;
                }

                if (isSidebarClick)
                {
                    vm.IsPaneOpen = true;
                }
            }
        }, RoutingStrategies.Tunnel);
    }



    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        // Only zoom when Ctrl is held
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            if (Application.Current is App app && app.Services != null)
            {
                var displayService = app.Services.GetService<DisplayService>();
                if (displayService != null)
                {
                    double delta = e.Delta.Y > 0 ? 0.05 : -0.05;
                    double newScale = displayService.Scaling + delta;
                    displayService.SetScaling(newScale);
                    e.Handled = true;
                }
            }
        }
    }

    private void TogglePaneButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.IsPaneOpen = !vm.IsPaneOpen;
        }
    }

    private void MinimizeButton_Click(object? sender, RoutedEventArgs e)
    {
        var window = (Window)VisualRoot!;
        window.WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object? sender, RoutedEventArgs e)
    {
        var window = (Window)VisualRoot!;
        window.WindowState = window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        var window = (Window)VisualRoot!;
        window.Close();
    }

    private System.Threading.CancellationTokenSource? _closePaneCts;

    private void IDESideBar_PointerEntered(object? sender, PointerEventArgs e)
    {
        // Hover ile otomatik açılmayı iptal ediyoruz, sadece kapanma iptalini koruyoruz
        _closePaneCts?.Cancel();
    }

    private void IDESideBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            // Sidebar üzerine tıklandığında açılır
            vm.IsPaneOpen = true;
        }
    }

    private async void IDESideBar_PointerExited(object? sender, PointerEventArgs e)
    {
        if (DataContext is MainViewModel vm && vm.CurrentTheme == "IDEProfessional")
        {
            _closePaneCts?.Cancel();
            _closePaneCts = new System.Threading.CancellationTokenSource();
            var token = _closePaneCts.Token;

            try
            {
                // Kapanma süresini biraz daha bekletiyoruz (400ms) ki yanlışlıkla çıkışlarda hemen kapanmasın
                await System.Threading.Tasks.Task.Delay(400, token);
                if (!token.IsCancellationRequested)
                {
                    vm.IsPaneOpen = false;
                }
            }
            catch (System.Threading.Tasks.TaskCanceledException)
            {
                // Ignored
            }
        }
    }
}
