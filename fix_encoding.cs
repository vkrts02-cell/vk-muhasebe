using System;
using System.IO;
using System.Text;

class Program
{
    static void Main()
    {
        string path = @"e:\avalonia yedek\ermaymuhasebe\ErmayMuhasebe.Avalonia\ErmayMuhasebe.Avalonia\ViewModels\LoginViewModel.cs";
        string content = File.ReadAllText(path, Encoding.UTF8);

        content = content.Replace("Ä°", "İ");
        content = content.Replace("ÅŸ", "ş");
        content = content.Replace("Ã§", "ç");
        content = content.Replace("Ä±", "ı");
        content = content.Replace("ÄŸ", "ğ");
        content = content.Replace("Ã¶", "ö");
        content = content.Replace("Ã¼", "ü");
        content = content.Replace("Ã–", "Ö");
        content = content.Replace("Ã‡", "Ç");
        content = content.Replace("Åž", "Ş");
        content = content.Replace("ğŸ” ", "🔑");
        content = content.Replace("ğŸ“Œ", "📌");

        File.WriteAllText(path, content, Encoding.UTF8);
    }
}
