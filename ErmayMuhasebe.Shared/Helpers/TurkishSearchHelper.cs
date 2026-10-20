using System;
using System.Text;

namespace ErmayMuhasebe.Shared.Helpers;

public static class TurkishSearchHelper
{
    public static readonly System.Globalization.CultureInfo Culture = new("tr-TR");

    /// <summary>
    /// Türkçe karakter ve büyük/küçük harf duyarsız arama için metni normalize eder.
    /// I/ı/İ/i -> i, Ğ/ğ -> g, Ü/ü -> u, Ş/ş -> s, Ö/ö -> o, Ç/ç -> c dönüşümü yapar.
    /// Böylece kullanıcı 'ASARLIK' kelimesini 'asarlık' veya 'asarlik' ile,
    /// 'LAMİNASYON' kelimesini 'laminasyon' veya 'lamınasyon' ile sorunsuz bulabilir.
    /// </summary>
    public static string NormalizeForSearch(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        var text = input.Trim();
        var sb = new StringBuilder(text.Length);

        foreach (char ch in text)
        {
            switch (ch)
            {
                case 'I':
                case 'ı':
                case 'İ':
                case 'i':
                    sb.Append('i');
                    break;
                case 'Ğ':
                case 'ğ':
                    sb.Append('g');
                    break;
                case 'Ü':
                case 'ü':
                    sb.Append('u');
                    break;
                case 'Ş':
                case 'ş':
                    sb.Append('s');
                    break;
                case 'Ö':
                case 'ö':
                    sb.Append('o');
                    break;
                case 'Ç':
                case 'ç':
                    sb.Append('c');
                    break;
                default:
                    sb.Append(char.ToLowerInvariant(ch));
                    break;
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Kaynak metin içinde aranan terimin veya harflerin geçip geçmediğini Türkçe harf ve büyük/küçük harf duyarsız kontrol eder.
    /// Birden fazla kelime/harf öbeği girilirse her bir öbeğin kaynak metinde geçmesini şart koşar.
    /// </summary>
    public static bool MatchSearch(string? source, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return true;
        if (string.IsNullOrWhiteSpace(source)) return false;

        string normSource = NormalizeForSearch(source);
        string normSearch = NormalizeForSearch(search);

        var tokens = normSearch.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0) return true;

        foreach (var token in tokens)
        {
            if (!normSource.Contains(token, StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Extension method: source.ContainsTurkish(search)
    /// </summary>
    public static bool ContainsTurkish(this string? source, string? search)
    {
        return MatchSearch(source, search);
    }
}
