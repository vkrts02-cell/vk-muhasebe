import sys

path = 'ErmayMuhasebe.Avalonia/ErmayMuhasebe.Avalonia/ViewModels/SettingsViewModel.cs'
with open(path, 'r', encoding='utf-8') as f:
    text = f.read()

# ChangeUsernameAsync & ChangePasswordAsync replacement block
target_auth = """                var profil = await _uow.GetFirmaProfiliAsync();
                var expectedPass = string.IsNullOrWhiteSpace(profil.FactoryResetPassword) ? "ERMAY2025" : profil.FactoryResetPassword;
                var connForAuth = db.GetGlobalConnection();
                var adminUserForAuth = await connForAuth.Table<Models.User>().FirstOrDefaultAsync(u => u.Role == "Admin" || u.Username == "admin");

                bool isAuthValid = !string.IsNullOrWhiteSpace(trimmedAuth) &&
                                   (trimmedAuth == expectedPass || 
                                    trimmedAuth == "ERMAY2025" || 
                                    trimmedAuth == "VK2026" || 
                                    trimmedAuth == "123" || 
                                    (adminUserForAuth != null && (AuthService.VerifyPassword(trimmedAuth, adminUserForAuth.Password, adminUserForAuth.PasswordSalt) || trimmedAuth == adminUserForAuth.Password)));"""

repl_auth = """                var profil = await _uow.GetFirmaProfiliAsync();
                var expectedPass = profil.FactoryResetPassword;
                var connForAuth = db.GetGlobalConnection();
                var adminUserForAuth = await connForAuth.Table<Models.User>().FirstOrDefaultAsync(u => u.Role == "Admin" || u.Username == "admin");

                bool isAuthValid = !string.IsNullOrWhiteSpace(trimmedAuth) &&
                                   ((!string.IsNullOrWhiteSpace(expectedPass) && trimmedAuth == expectedPass) || 
                                    (adminUserForAuth != null && (AuthService.VerifyPassword(trimmedAuth, adminUserForAuth.Password, adminUserForAuth.PasswordSalt) || trimmedAuth == adminUserForAuth.Password)));"""

text = text.replace(target_auth, repl_auth)

# FactoryResetAsync replacement block
target_reset = """        var profil = await _uow.GetFirmaProfiliAsync();
        string expectedPass = string.IsNullOrWhiteSpace(profil.FactoryResetPassword) ? "ERMAY2025" : profil.FactoryResetPassword;

        Models.User? adminUser = null;
        try
        {
            var db = ((ErmayMuhasebe.Avalonia.App)App.Current!).Services?.GetRequiredService<DatabaseService>();
            var conn = db?.GetGlobalConnection();
            if (conn != null)
            {
                adminUser = await conn.Table<Models.User>().FirstOrDefaultAsync(u => u.Role == "Admin" || u.Username == "admin");
            }
        }
        catch { }

        bool isPassValid = !string.IsNullOrWhiteSpace(ResetPassword) &&
                           (ResetPassword == expectedPass || 
                            ResetPassword == "ERMAY2025" || 
                            ResetPassword == "VK2026" || 
                            ResetPassword == "123" || 
                            (adminUser != null && (AuthService.VerifyPassword(ResetPassword, adminUser.Password, adminUser.PasswordSalt) || ResetPassword == adminUser.Password)));"""

repl_reset = """        var profil = await _uow.GetFirmaProfiliAsync();
        string expectedPass = profil.FactoryResetPassword;

        Models.User? adminUser = null;
        try
        {
            var db = ((ErmayMuhasebe.Avalonia.App)App.Current!).Services?.GetRequiredService<DatabaseService>();
            var conn = db?.GetGlobalConnection();
            if (conn != null)
            {
                adminUser = await conn.Table<Models.User>().FirstOrDefaultAsync(u => u.Role == "Admin" || u.Username == "admin");
            }
        }
        catch { }

        bool isPassValid = !string.IsNullOrWhiteSpace(ResetPassword) &&
                           ((!string.IsNullOrWhiteSpace(expectedPass) && ResetPassword == expectedPass) || 
                            (adminUser != null && (AuthService.VerifyPassword(ResetPassword, adminUser.Password, adminUser.PasswordSalt) || ResetPassword == adminUser.Password)));"""

text = text.replace(target_reset, repl_reset)

with open(path, 'w', encoding='utf-8') as f:
    f.write(text)

print("SettingsViewModel.cs cleaned up successfully!")
