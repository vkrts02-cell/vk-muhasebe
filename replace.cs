using System.IO;
using System.Text.RegularExpressions;

var path = @"e:\avalonia yedek\ermaymuhasebe\ErmayMuhasebe.Avalonia\ErmayMuhasebe.Avalonia\ViewModels\LoginViewModel.cs";
var content = File.ReadAllText(path);

var replacement = @"            // Supabase Auth kullanarak Şifre Sıfırlama Maili Gönder
            bool supabaseReset = await SupabaseAuthService.Instance.ResetPasswordForEmailAsync(user.Email.Trim());

            if (supabaseReset)
            {
                SuccessMessage = $""Supabase Auth üzerinden şifre sıfırlama bağlantısı {user.Email} adresine başarıyla gönderildi."";
                IsResetCodeSent = true;
                return;
            }

            var randomCode = new Random().Next(100000, 999999).ToString();";

content = Regex.Replace(content, @"var randomCode = new Random\(\)\.Next\(100000, 999999\)\.ToString\(\);", replacement);

File.WriteAllText(path, content);
System.Console.WriteLine("Done");
