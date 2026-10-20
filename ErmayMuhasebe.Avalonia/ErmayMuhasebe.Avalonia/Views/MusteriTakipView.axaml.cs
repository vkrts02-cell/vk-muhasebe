using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using ErmayMuhasebe.Avalonia.ViewModels;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace ErmayMuhasebe.Avalonia.Views
{
    public partial class MusteriTakipView : UserControl
    {
        public MusteriTakipView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object? sender, EventArgs e)
        {
            if (DataContext is MusteriTakipViewModel vm)
            {
                vm.ImagePickerAction = OpenImagePickerAsync;
                vm.OpenImageExternalAction = OpenImageFileExternally;
            }
        }

        private async Task<string?> OpenImagePickerAsync()
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return null;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Müşteri İçin Görsel Seçin",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Görsel Dosyaları")
                    {
                        Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.webp", "*.pdf" }
                    },
                    FilePickerFileTypes.All
                }
            });

            if (files.Count > 0)
            {
                return files[0].Path.LocalPath;
            }

            return null;
        }

        private void OpenImageFileExternally(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    new Process
                    {
                        StartInfo = new ProcessStartInfo(path)
                        {
                            UseShellExecute = true
                        }
                    }.Start();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MusteriTakipView] OpenImageFileExternally Error: {ex.Message}");
            }
        }
        private void OnKlasorCardClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (e.Source is global::Avalonia.Visual visual)
            {
                var delBtn = visual.FindAncestorOfType<Button>(includeSelf: true);
                if (delBtn != null && delBtn.Classes.Contains("CardDeleteBtn"))
                {
                    e.Handled = true;
                    return;
                }
            }

            if (sender is Button btn && btn.Tag is MusteriTakipKlasorCardViewModel card)
            {
                if (DataContext is MusteriTakipViewModel vm)
                {
                    vm.OpenKlasorDetail(card);
                }
            }
        }

        private void OnDeleteCardClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        {
            e.Handled = true; // Klasörün açılmasını engelle
            if (sender is Button btn && btn.Tag is MusteriTakipKlasorCardViewModel card)
            {
                if (DataContext is MusteriTakipViewModel vm)
                {
                    vm.DeleteKlasorConfirm(card);
                }
            }
        }
    }
}
