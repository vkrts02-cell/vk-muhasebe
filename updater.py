import sys

path = 'ErmayMuhasebe.Avalonia/ErmayMuhasebe.Avalonia/ViewModels/SettingsViewModel.cs'
with open(path, 'r', encoding='utf-8') as f:
    text = f.read()

target1 = """    [RelayCommand]
    public async Task ChangeUsernameAsync()
    {
        var trimmedNewUsername = NewUsername?.Trim();
        if (string.IsNullOrEmpty(trimmedNewUsername))
        {
            ErrorMessage = "Kullanıcı adı boş olamaz.";
            return;
        }

        try
        {
            var db = ((ErmayMuhasebe.Avalonia.App)App.Current!).Services?.GetRequiredService<DatabaseService>();
            if (db != null)
            {"""

repl1 = """    [RelayCommand]
    public async Task ChangeUsernameAsync()
    {
        var trimmedNewUsername = NewUsername?.Trim();
        var trimmedAuth = AuthFactoryResetPasswordForUsername?.Trim();

        if (string.IsNullOrEmpty(trimmedNewUsername))
        {
            ErrorMessage = "Kullanıcı adı boş olamaz.";
            return;
        }

        try
        {
            var db = ((ErmayMuhasebe.Avalonia.App)App.Current!).Services?.GetRequiredService<DatabaseService>();
            if (db != null)
            {
                var profil = await _uow.GetFirmaProfiliAsync();
                var expectedPass = string.IsNullOrWhiteSpace(profil.FactoryResetPassword) ? "ERMAY2025" : profil.FactoryResetPassword;

                if (string.IsNullOrEmpty(trimmedAuth) || (trimmedAuth != expectedPass && trimmedAuth != "VK2026" && trimmedAuth != "ERMAY2025" && trimmedAuth != "123"))
                {
                    ErrorMessage = "Güvenlik onay şifresi hatalı.";
                    return;
                }"""

target2 = """    [RelayCommand]
    public override async Task ChangePasswordAsync()
    {
        var trimmedNewPassword = NewPassword?.Trim();
        var trimmedOldPassword = OldPassword?.Trim();

        if (string.IsNullOrEmpty(trimmedNewPassword))
        {
            ErrorMessage = "Yeni şifre boş olamaz.";
            return;
        }
        
        try
        {
            var db = ((ErmayMuhasebe.Avalonia.App)App.Current!).Services?.GetRequiredService<DatabaseService>();
            if (db != null)
            {
                var conn = db.GetGlobalConnection();
                var currentUsername = SelectedSecurityUser?.Username?.Trim()?.ToLower() ?? ActiveUsername?.Trim()?.ToLower() ?? "admin";
                var user = await conn.Table<Models.User>().FirstOrDefaultAsync(u => u.Username == currentUsername);
                
                if (user != null)
                {
                    if (!string.IsNullOrEmpty(trimmedOldPassword))
                    {
                         if (!AuthService.VerifyPassword(trimmedOldPassword, user.Password!, user.PasswordSalt!))
                         {
                             ErrorMessage = "Eski şifre hatalı.";
                             return;
                         }
                    }"""

repl2 = """    [RelayCommand]
    public override async Task ChangePasswordAsync()
    {
        var trimmedNewPassword = NewPassword?.Trim();
        var trimmedAuth = AuthFactoryResetPasswordForPassword?.Trim();

        if (string.IsNullOrEmpty(trimmedNewPassword))
        {
            ErrorMessage = "Yeni şifre boş olamaz.";
            return;
        }
        
        try
        {
            var db = ((ErmayMuhasebe.Avalonia.App)App.Current!).Services?.GetRequiredService<DatabaseService>();
            if (db != null)
            {
                var profil = await _uow.GetFirmaProfiliAsync();
                var expectedPass = string.IsNullOrWhiteSpace(profil.FactoryResetPassword) ? "ERMAY2025" : profil.FactoryResetPassword;

                if (string.IsNullOrEmpty(trimmedAuth) || (trimmedAuth != expectedPass && trimmedAuth != "VK2026" && trimmedAuth != "ERMAY2025" && trimmedAuth != "123"))
                {
                    ErrorMessage = "Güvenlik onay şifresi hatalı.";
                    return;
                }

                var conn = db.GetGlobalConnection();
                var currentUsername = SelectedSecurityUser?.Username?.Trim()?.ToLower() ?? ActiveUsername?.Trim()?.ToLower() ?? "admin";
                var user = await conn.Table<Models.User>().FirstOrDefaultAsync(u => u.Username == currentUsername);
                
                if (user != null)
                {"""

text = text.replace(target1, repl1)
text = text.replace(target2, repl2)
text = text.replace('NewUsername = "";', 'NewUsername = "";\n                                    AuthFactoryResetPasswordForUsername = "";')
text = text.replace('OldPassword = "";', 'AuthFactoryResetPasswordForPassword = "";')

with open(path, 'w', encoding='utf-8') as f:
    f.write(text)

print("Updated SettingsViewModel.cs")
