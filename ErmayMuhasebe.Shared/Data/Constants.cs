namespace ErmayMuhasebe.Data;

public static class Constants
{
    public const string DatabaseFilename = "ErmayV4_Stable.db3";
    private const string DefaultDatabasePassword = "ERMAY-SECURE-DB-KEY-2025-V2";
    public static string DatabasePassword
    {
        get
        {
            var envKey = Environment.GetEnvironmentVariable("ERMAY_DB_KEY");
            if (!string.IsNullOrWhiteSpace(envKey)) return envKey;

            try
            {
                if (OperatingSystem.IsWindows())
                {
                    string vaultPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "ErmayMuhasebe",
                        ".vault_key");
                    if (File.Exists(vaultPath))
                    {
                        var encBytes = File.ReadAllBytes(vaultPath);
                        var rawBytes = System.Security.Cryptography.ProtectedData.Unprotect(encBytes, null, System.Security.Cryptography.DataProtectionScope.CurrentUser);
                        var decryptedKey = System.Text.Encoding.UTF8.GetString(rawBytes).Trim();
                        if (!string.IsNullOrEmpty(decryptedKey)) return decryptedKey;
                    }
                }
            }
            catch { }

            return DefaultDatabasePassword;
        }
    }


    public const SQLite.SQLiteOpenFlags Flags =
        SQLite.SQLiteOpenFlags.ReadWrite |
        SQLite.SQLiteOpenFlags.Create;

    private static string? _databasePath;
    public static string DatabasePath
    {
        get
        {
            if (_databasePath != null) return _databasePath;
            
            string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ErmayMuhasebe");
            if (!Directory.Exists(appData)) Directory.CreateDirectory(appData);
            
            return Path.Combine(appData, DatabaseFilename);
        }
        set => _databasePath = value;
    }
}
