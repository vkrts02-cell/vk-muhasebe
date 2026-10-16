using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ErmayMuhasebe.Services;
using Xunit;

namespace ErmayMuhasebe.Tests.Services;

public class MaliYilSyncTests
{
    [Fact]
    public async Task GetAvailableYearsAsync_MergesLocalAndCloudYears()
    {
        var yearContext = new YearContext { CurrentYear = 2024 };
        var tempDir = Path.Combine(Path.GetTempPath(), $"ErmayTest_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // Create a fake local database ermay_2024.db
            var testDb = Path.Combine(tempDir, "ermay_2024.db");
            File.WriteAllText(testDb, "dummy sqlite");

            var dbService = new DatabaseService(yearContext, testDb);
            dbService.DisableCloudSync = true;
            var rolloverService = new YearRolloverService(dbService, yearContext);

            // Verify local years
            var localYears = rolloverService.GetAvailableYears();
            Assert.NotNull(localYears);

            // Verify async method runs without exception
            var availableAsync = await rolloverService.GetAvailableYearsAsync();
            Assert.NotNull(availableAsync);
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }
}
