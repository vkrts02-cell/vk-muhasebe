using Avalonia.Controls;
using Avalonia.Input;
using ErmayMuhasebe.Avalonia.ViewModels;

namespace ErmayMuhasebe.Avalonia.Views;

public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();
    }

    private void OnGoalRingPointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is not Control ctrl || !ctrl.IsPointerOver) return;

        if (DataContext is DashboardViewModel vm)
        {
            _ = vm.PlayGoalRingAnimationAsync();
        }
    }

    private void OnGoalRingPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control ctrl || !ctrl.IsPointerOver) return;

        if (DataContext is DashboardViewModel vm)
        {
            _ = vm.PlayGoalRingAnimationAsync();
        }
    }
}
