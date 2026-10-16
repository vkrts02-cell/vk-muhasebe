using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System;
using System.Collections.Generic;

namespace ErmayMuhasebe.Services
{
    public class ThemeService
    {
        public enum AppTheme
        {
            ModernSaaS,      // Standart / Mevcut Tasarım (Orijinal)
            ObsidianGlow,    // 1. Konsept: Obsidian Glow (Ultra Modern Koyu Lüks, Neon Turkuaz & Çivit Mavi)
            CupertinoGlass,  // 2. Konsept: Cupertino Frosted Glass (Apple macOS Akrilik Buzlu Cam & Safir Mavi)
            IDEProfessional  // IDE Profesyonel (VS Code Stili)
        }

        public AppTheme CurrentTheme { get; private set; } = AppTheme.ModernSaaS;
        private readonly string _configPath;

        public ThemeService()
        {
            _configPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "theme_settings.json");
            LoadSettings();
        }

        public event Action<AppTheme>? ThemeChanged;

        private void SaveSettings()
        {
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(new { Theme = CurrentTheme.ToString() });
                System.IO.File.WriteAllText(_configPath, json);
            }
            catch { }
        }

        private void LoadSettings()
        {
            try
            {
                if (System.IO.File.Exists(_configPath))
                {
                    var json = System.IO.File.ReadAllText(_configPath);
                    var data = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(json);
                    if (data.TryGetProperty("Theme", out var themeProp) && Enum.TryParse<AppTheme>(themeProp.GetString(), out var theme))
                    {
                        CurrentTheme = theme;
                    }
                }
            }
            catch { }
        }

        public void SetTheme(AppTheme theme)
        {
            CurrentTheme = theme;
            SaveSettings();
            var app = Application.Current;
            if (app == null) return;

            // Define Theme Variables
            Color primary, background, surface, panelColor, solidPanelColor, sidebarColor, text, secondaryText, borderColor;
            LinearGradientBrush fluidGradient;
            double controlCorner, cardCorner, layoutPadding;

            switch (theme)
            {
                case AppTheme.ObsidianGlow:
                    // 1. Konsept: Obsidian Glow (Ultra Modern Koyu Lüks, Neon Turkuaz & Çivit Mavi)
                    primary = Color.Parse("#00E5FF");           // Neon Turkuaz
                    background = Color.Parse("#07090E");        // Derin Obsidyen Siyahı
                    surface = Color.Parse("#0D1117");           // Obsidyen Üst Bar
                    panelColor = Color.Parse("#0E141E");        // Koyu Lüks Panel
                    solidPanelColor = Color.Parse("#131926");   // Yükseltilmiş Lüks Kart
                    sidebarColor = Color.Parse("#05070B");      // Derin Menü Dock
                    text = Color.Parse("#F0F6FC");              // Berrak Beyaz
                    secondaryText = Color.Parse("#8B949E");
                    borderColor = Color.Parse("#5500E5FF");     // Işıltılı Neon Turkuaz Kenarlık

                    fluidGradient = new LinearGradientBrush
                    {
                        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                        EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                        GradientStops = new GradientStops
                        {
                            new GradientStop(Color.Parse("#00E5FF"), 0),
                            new GradientStop(Color.Parse("#6366F1"), 1)
                        }
                    };
                    controlCorner = 10;
                    cardCorner = 18;
                    layoutPadding = 16;
                    break;

                case AppTheme.CupertinoGlass:
                    // 2. Konsept: Cupertino Frosted Glass (Apple macOS Gerçek Akrilik Buzlu Cam & Safir Mavi)
                    primary = Color.Parse("#0A84FF");           // Apple System Mavi
                    background = Color.Parse("#E60B1220");      // Yarı Saydam Gece Mavisi Akrilik Zemin (Mica & Blur Geçirgen)
                    surface = Color.Parse("#8C0F172A");         // %55 Saydam Buzlu Cam Başlık
                    panelColor = Color.Parse("#731E293B");      // %45 Saydam Akrilik Buzlu Cam Panel (Gerçek Glassmorphism!)
                    solidPanelColor = Color.Parse("#B3131F33"); // %70 Saydam Yükseltilmiş Cam Kart
                    sidebarColor = Color.Parse("#990A101D");    // %60 Saydam Buzlu Cam Yan Menü
                    text = Color.Parse("#F8FAFC");              // Parlak Beyaz
                    secondaryText = Color.Parse("#94A3B8");
                    borderColor = Color.Parse("#4538BDF8");     // Buzlu Cam Işıltılı Kenarlık (Translucent Icy Blue)

                    fluidGradient = new LinearGradientBrush
                    {
                        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                        EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                        GradientStops = new GradientStops
                        {
                            new GradientStop(Color.Parse("#0A84FF"), 0),
                            new GradientStop(Color.Parse("#38BDF8"), 1)
                        }
                    };
                    controlCorner = 14;
                    cardCorner = 22;
                    layoutPadding = 18;
                    break;

                case AppTheme.IDEProfessional:
                    // VS Code Style (Pixel-Perfect Parity)
                    primary = Color.Parse("#007acc");           // VS Code Blue
                    background = Color.Parse("#1e1e1e");        // Editor Background
                    surface = Color.Parse("#252526");           // Side Bar/Panel
                    panelColor = Color.Parse("#252526");
                    solidPanelColor = Color.Parse("#1e1e1e");
                    sidebarColor = Color.Parse("#333333");
                    text = Color.Parse("#FFFFFF");              // White Text
                    secondaryText = Color.Parse("#858585");
                    borderColor = Color.Parse("#25FFFFFF");

                    fluidGradient = new LinearGradientBrush
                    {
                        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                        EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                        GradientStops = new GradientStops
                        {
                            new GradientStop(Color.Parse("#007acc"), 0),
                            new GradientStop(Color.Parse("#0098ff"), 1)
                        }
                    };
                    controlCorner = 0;
                    cardCorner = 4;
                    layoutPadding = 8;
                    break;

                case AppTheme.ModernSaaS:
                default:
                    // Standart / Orijinal Tasarım (Mevcut yapı %100 korunur)
                    primary = Color.Parse("#0061FF");           
                    background = Color.Parse("#0A0A0A");        
                    surface = Color.Parse("#0A0A0A");           
                    panelColor = Color.Parse("#161616");
                    solidPanelColor = Color.Parse("#0A0A0A");
                    sidebarColor = Color.Parse("#0F0F0F");      
                    text = Color.Parse("#FFFFFF");              
                    secondaryText = Color.Parse("#AAAAAA");
                    borderColor = Color.Parse("#1FFFFFFF");

                    fluidGradient = new LinearGradientBrush
                    {
                        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                        EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                        GradientStops = new GradientStops
                        {
                            new GradientStop(Color.Parse("#0061FF"), 0),
                            new GradientStop(Color.Parse("#60A5FA"), 1)
                        }
                    };
                    controlCorner = 8;
                    cardCorner = 16;
                    layoutPadding = 16;
                    break;
            }

            // Update Resources dynamically across all views
            var res = app.Resources;

            UpdateResource(res, "PrimaryColor", primary);
            UpdateResource(res, "PrimaryBrush", new SolidColorBrush(primary));
            
            UpdateResource(res, "BackgroundColor", background);
            UpdateResource(res, "BackgroundBrush", new SolidColorBrush(background));
            
            UpdateResource(res, "SurfaceColor", surface);
            UpdateResource(res, "SurfaceBrush", new SolidColorBrush(surface));

            UpdateResource(res, "PanelColor", panelColor);
            UpdateResource(res, "PanelBrush", new SolidColorBrush(panelColor));

            UpdateResource(res, "SolidPanelColor", solidPanelColor);
            UpdateResource(res, "SolidPanelBrush", new SolidColorBrush(solidPanelColor));

            UpdateResource(res, "SidebarColor", sidebarColor);
            UpdateResource(res, "SidebarBackground", sidebarColor);
            UpdateResource(res, "SidebarBrush", new SolidColorBrush(sidebarColor));

            UpdateResource(res, "BorderColor", new SolidColorBrush(borderColor));
            UpdateResource(res, "BorderBrush", new SolidColorBrush(borderColor));
            
            UpdateResource(res, "TextColor", text);
            UpdateResource(res, "TextBrush", new SolidColorBrush(text));

            UpdateResource(res, "SecondaryTextBrush", new SolidColorBrush(secondaryText));
            UpdateResource(res, "FluidBlueGradient", fluidGradient);

            // Update Structure
            UpdateResource(res, "ControlCornerRadius", new CornerRadius(controlCorner));
            UpdateResource(res, "ControlCornerValue", controlCorner);
            UpdateResource(res, "CardCornerValue", cardCorner);
            UpdateResource(res, "LayoutPaddingValue", layoutPadding);
            
            // Notify subscribers
            ThemeChanged?.Invoke(theme);
            

        }

        private void UpdateResource(IResourceDictionary resources, object key, object value)
        {
            if (resources.ContainsKey(key))
            {
                resources[key] = value;
            }
            else
            {
                resources.Add(key, value);
            }
        }
    }
}
