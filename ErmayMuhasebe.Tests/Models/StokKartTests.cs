using ErmayMuhasebe.Models;
using Xunit;

namespace ErmayMuhasebe.Tests.Models;

/// <summary>
/// StokKart modeli için unit testler
/// Stok özelliklerini ve hesaplamalarını test eder
/// </summary>
public class StokKartTests
{
    [Fact]
    public void StokKart_DefaultBirim_ShouldBeAdet()
    {
        // Arrange & Act
        var stok = new StokKart();
        
        // Assert
        Assert.Equal("Adet", stok.Birim); // Varsayılan birim "Adet" olmalı
    }

    [Fact]
    public void StokKart_DefaultKDV_ShouldBe20()
    {
        // Arrange & Act
        var stok = new StokKart();
        
        // Assert
        Assert.Equal(20, stok.KDV); // Varsayılan KDV %20 olmalı
    }

    [Fact]
    public void StokKart_KdvOrani_ShouldEqualKDV()
    {
        // Arrange
        var stok = new StokKart
        {
            KDV = 18
        };
        
        // Act
        int kdvOrani = stok.KdvOrani;
        
        // Assert
        Assert.Equal(18, kdvOrani);
        Assert.Equal(stok.KDV, kdvOrani); // KdvOrani ve KDV aynı olmalı
    }

    [Fact]
    public void StokKart_KritikSeviye_ShouldEqualMinSeviye()
    {
        // Arrange
        var stok = new StokKart
        {
            MinSeviye = 10.5m
        };
        
        // Act
        decimal kritikSeviye = stok.KritikSeviye;
        
        // Assert
        Assert.Equal(10.5m, kritikSeviye);
        Assert.Equal(stok.MinSeviye, kritikSeviye); // KritikSeviye ve MinSeviye aynı olmalı
    }

    [Fact]
    public void StokKart_Grup_ShouldEqualKategori()
    {
        // Arrange
        var stok = new StokKart
        {
            Kategori = "Elektronik"
        };
        
        // Act
        string grup = stok.Grup;
        
        // Assert
        Assert.Equal("Elektronik", grup);
        Assert.Equal(stok.Kategori, grup); // Grup ve Kategori aynı olmalı
    }

    [Fact]
    public void StokKart_ToString_ShouldReturnStokAdi()
    {
        // Arrange
        var stok = new StokKart
        {
            StokAdi = "Test Ürün"
        };
        
        // Act
        string result = stok.ToString();
        
        // Assert
        Assert.Equal("Test Ürün", result);
    }

    [Fact]
    public void StokKart_ToString_EmptyStokAdi_ShouldReturnEmpty()
    {
        // Arrange
        var stok = new StokKart
        {
            StokAdi = null!
        };
        
        // Act
        string result = stok.ToString();
        
        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void StokKart_PropertyChanged_ShouldFireOnChange()
    {
        // Arrange
        var stok = new StokKart();
        bool eventFired = false;
        stok.PropertyChanged += (s, e) => eventFired = true;
        
        // Act
        stok.StokAdi = "Yeni Ürün";
        
        // Assert
        Assert.True(eventFired); // Property değiştiğinde event tetiklenmeli
    }
}

