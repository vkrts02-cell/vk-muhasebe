import sys

path = 'ErmayMuhasebe.Avalonia/ErmayMuhasebe.Avalonia/ViewModels/SettingsViewModel.cs'
with open(path, 'r', encoding='utf-8') as f:
    text = f.read()

target_username = """                var profil = await _uow.GetFirmaProfiliAsync();
                var expectedPass = string.IsNullOrWhiteSpace(profil.FactoryResetPassword) ? "ERMAY2025" : profil.FactoryResetPassword;

                if (string.IsNullOrEmpty(trimmedAuth) || (trimmedAuth != expectedPass && trimmedAuth != "VK2026" && trimmedAuth != "ERMAY2025" && trimmedAuth != "123"))
                {
                    ErrorMessage = "Güvenlik onay şifresi hatalı.";
                    return;
                }"""

replacement_username = """                var profil = await _uow.GetFirmaProfiliAsync();
                var expectedPass = string.IsNullOrWhiteSpace(profil.FactoryResetPassword) ? "ERMAY2025" : profil.FactoryResetPassword;
                var connForAuth = db.GetGlobalConnection();
                var adminUserForAuth = await connForAuth.Table<Models.User>().FirstOrDefaultAsync(u => u.Role == "Admin" || u.Username == "admin");

                bool isAuthValid = !string.IsNullOrWhiteSpace(trimmedAuth) &&
                                   (trimmedAuth == expectedPass || 
                                    trimmedAuth == "ERMAY2025" || 
                                    trimmedAuth == "VK2026" || 
                                    trimmedAuth == "123" || 
                                    (adminUserForAuth != null && (AuthService.VerifyPassword(trimmedAuth, adminUserForAuth.Password, adminUserForAuth.PasswordSalt) || trimmedAuth == adminUserForAuth.Password)));

                if (!isAuthValid)
                {
                    ErrorMessage = "Güvenlik onay şifresi hatalı. Fabrika onay şifrenizi veya admin giriş şifrenizi giriniz.";
                    return;
                }"""

if target_username in text:
    text = text.replace(target_username, replacement_username)
    with open(path, 'w', encoding='utf-8') as f:
        f.write(text)
    print("SettingsViewModel.cs updated successfully!")
else:
    print("Target string not found in SettingsViewModel.cs")
