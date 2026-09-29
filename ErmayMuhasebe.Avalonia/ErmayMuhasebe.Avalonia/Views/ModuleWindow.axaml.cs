using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using FluentIcons.Avalonia.Fluent;
using FluentIcons.Common;
using System;

namespace ErmayMuhasebe.Avalonia.Views;

public partial class ModuleWindow : Window
{
    public ModuleWindow()
    {
        InitializeComponent();
        this.DataContextChanged += (s, e) =>
        {
            if (DataContext is ErmayMuhasebe.Shared.ViewModels.ViewModelBase vm)
            {
                vm.RequestClose += () => Close();
            }
        };
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public void SetModuleInfo(string title, Symbol icon)
    {
        this.Title = string.IsNullOrWhiteSpace(title) ? "VK Muhasebe" : title;

        var titleBlock = this.FindControl<TextBlock>("ModuleTitle");
        if (titleBlock != null) titleBlock.Text = title;

        var iconSymbol = this.FindControl<SymbolIcon>("ModuleIcon");
        if (iconSymbol != null) iconSymbol.Symbol = icon;
    }

    public void SetContent(object content)
    {
        var contentControl = this.FindControl<ContentControl>("ModuleContent");
        if (contentControl != null) contentControl.Content = content;
    }

    protected override void OnPointerPressed(global::Avalonia.Input.PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var point = e.GetCurrentPoint(this);
        if (point.Properties.IsLeftButtonPressed && point.Position.Y <= 50)
        {
            BeginMoveDrag(e);
        }
    }

    protected override void OnKeyDown(global::Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key == global::Avalonia.Input.Key.Escape)
        {
            Close();
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }

    private void MinimizeButton_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void CloseButton_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }
}
