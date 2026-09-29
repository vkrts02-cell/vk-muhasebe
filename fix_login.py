import os

file_path = r"e:\avalonia yedek\ermaymuhasebe\ErmayMuhasebe.Avalonia\ErmayMuhasebe.Avalonia\ViewModels\LoginViewModel.cs"

with open(file_path, "r", encoding="utf-8") as f:
    content = f.read()

replacements = {
    "Ä°": "İ",
    "ÅŸ": "ş",
    "Ã§": "ç",
    "Ä±": "ı",
    "ÄŸ": "ğ",
    "Ã¶": "ö",
    "Ã¼": "ü",
    "Ã–": "Ö",
    "Ã‡": "Ç",
    "Åž": "Ş",
    "ğŸ” ": "🔑",
    "ğŸ“Œ": "📌"
}

for old, new in replacements.items():
    content = content.replace(old, new)

# Also fix the initial setup bug
old_code = """                    // JSON veritabanı kaydı işlemleri DatabaseService'e taşınmıştır.
                    // İlk kullanıcıyı form alanlarına doldur
                    if (isNewSetup && doc.RootElement.TryGetProperty("Users", out var uArr) && uArr.GetArrayLength() > 0)
                    {
                        var first = uArr[0];
                        Username = first.GetProperty("Username").GetString() ?? "";
                        Password = first.GetProperty("Password").GetString() ?? "";
                        RememberMe = true;
                        SaveCredentials();
                    }"""

new_code = """                    // JSON veritabanı kaydı işlemleri DatabaseService'e taşınmıştır.
                    // İlk kullanıcıyı form alanlarına doldur
                    if (isNewSetup && doc.RootElement.TryGetProperty("Users", out var uArr) && uArr.GetArrayLength() > 0)
                    {
                        var first = uArr[0];
                        Username = first.GetProperty("Username").GetString() ?? "";
                        Password = first.GetProperty("Password").GetString() ?? "";
                        RememberMe = true;
                        SaveCredentials();
                        
                        // FIX: Actually create the users in the local database for clean install
                        Task.Run(async () =>
                        {
                            try
                            {
                                var conn = _dbService.GetGlobalConnection();
                                foreach (var uElement in uArr.EnumerateArray())
                                {
                                    var uName = uElement.GetProperty("Username").GetString()?.Trim().ToLower();
                                    var uPass = uElement.GetProperty("Password").GetString()?.Trim();
                                    var uEmail = uElement.TryGetProperty("Email", out var emailProp) ? emailProp.GetString() : "";
                                    
                                    if (!string.IsNullOrEmpty(uName) && !string.IsNullOrEmpty(uPass))
                                    {
                                        var existing = await conn.Table<Models.User>().FirstOrDefaultAsync(x => x.Username == uName);
                                        if (existing == null)
                                        {
                                            var salt = AuthService.GenerateSalt();
                                            var hash = AuthService.HashPassword(uPass, salt);
                                            await conn.InsertAsync(new Models.User
                                            {
                                                Id = uName,
                                                Username = uName,
                                                Password = hash,
                                                PasswordSalt = salt,
                                                Email = uEmail,
                                                Role = "Admin",
                                                IsActive = true,
                                                CreatedAt = DateTime.UtcNow,
                                                UpdatedAt = DateTime.UtcNow
                                            });
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"[LoginVM] User creation error: {ex.Message}");
                            }
                        }).Wait();
                    }"""

content = content.replace(old_code, new_code)

with open(file_path, "w", encoding="utf-8") as f:
    f.write(content)

print("Done")
