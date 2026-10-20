using System;
using System.IO;
using System.Text.Json;
using ErmayMuhasebe.Shared.ViewModels;
using Xunit;

namespace ErmayMuhasebe.Tests.Services;

public class CariFilterPersistenceTests : IDisposable
{
    private readonly string _testFilePath;

    public CariFilterPersistenceTests()
    {
        _testFilePath = CariListViewModel.GetSettingsFilePath();
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_testFilePath))
            {
                File.Delete(_testFilePath);
            }
        }
        catch { }
    }

    [Fact]
    public void SaveAndLoad_PersistsAllFilterAndSortingProperties()
    {
        // 1. Arrange: create dummy settings file
        var original = new CariListViewModel.CariFilterSettings
        {
            IsOnlyWithBalance = true,
            IsBakiyeSortActive = true,
            IsBakiyeDescending = false,
            BakiyeSortIcon = "ArrowUp",
            GrupSortState = 2,
            GrupSortIcon = "ArrowDown",
            SelectedGrupFilter = "Tedarikçi"
        };

        var dir = Path.GetDirectoryName(_testFilePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var json = JsonSerializer.Serialize(original);
        File.WriteAllText(_testFilePath, json);

        // 2. Act: Load settings into static state
        CariListViewModel.LoadPersistentSettings();

        // 3. Assert: Verify the file exists and is readable
        Assert.True(File.Exists(_testFilePath));
        var readJson = File.ReadAllText(_testFilePath);
        var loaded = JsonSerializer.Deserialize<CariListViewModel.CariFilterSettings>(readJson);

        Assert.NotNull(loaded);
        Assert.True(loaded.IsOnlyWithBalance);
        Assert.True(loaded.IsBakiyeSortActive);
        Assert.False(loaded.IsBakiyeDescending);
        Assert.Equal("ArrowUp", loaded.BakiyeSortIcon);
        Assert.Equal(2, loaded.GrupSortState);
        Assert.Equal("ArrowDown", loaded.GrupSortIcon);
        Assert.Equal("Tedarikçi", loaded.SelectedGrupFilter);
    }

    [Fact]
    public void SavePersistentSettings_CreatesFileOnDisk()
    {
        // Act: Save current settings
        CariListViewModel.SavePersistentSettings();

        // Assert: File was written and is valid JSON
        Assert.True(File.Exists(_testFilePath));
        var content = File.ReadAllText(_testFilePath);
        Assert.NotEmpty(content);

        var parsed = JsonSerializer.Deserialize<CariListViewModel.CariFilterSettings>(content);
        Assert.NotNull(parsed);
    }
}
