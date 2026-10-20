using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ErmayMuhasebe.Shared.Helpers
{
    public class NaturalStringComparer : IComparer<string?>
    {
        private static readonly CultureInfo TurkishCulture = CultureInfo.GetCultureInfo("tr-TR");
        private static readonly Regex TokenRegex = new Regex(@"(\d+|\D+)", RegexOptions.Compiled);

        public static readonly NaturalStringComparer Instance = new NaturalStringComparer();

        public int Compare(string? x, string? y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            var xMatches = TokenRegex.Matches(x);
            var yMatches = TokenRegex.Matches(y);

            int minCount = Math.Min(xMatches.Count, yMatches.Count);

            for (int i = 0; i < minCount; i++)
            {
                string xToken = xMatches[i].Value;
                string yToken = yMatches[i].Value;

                bool xIsNum = char.IsDigit(xToken[0]);
                bool yIsNum = char.IsDigit(yToken[0]);

                if (xIsNum && yIsNum)
                {
                    // Sayısal blokları karşılaştır (örn: 10 vs 100)
                    if (decimal.TryParse(xToken, NumberStyles.None, CultureInfo.InvariantCulture, out decimal xNum) &&
                        decimal.TryParse(yToken, NumberStyles.None, CultureInfo.InvariantCulture, out decimal yNum))
                    {
                        int numComp = xNum.CompareTo(yNum);
                        if (numComp != 0) return numComp;
                    }
                    else
                    {
                        // Decimal sınırını aşan devasa sayılar için uzunluk karşılaştırması
                        int lenComp = xToken.Length.CompareTo(yToken.Length);
                        if (lenComp != 0) return lenComp;

                        int strNumComp = string.Compare(xToken, yToken, StringComparison.Ordinal);
                        if (strNumComp != 0) return strNumComp;
                    }
                }
                else
                {
                    // Metin bloklarını Türkçe alfabeye göre karşılaştır
                    int textComp = string.Compare(xToken, yToken, TurkishCulture, CompareOptions.IgnoreCase);
                    if (textComp != 0) return textComp;
                }
            }

            return xMatches.Count.CompareTo(yMatches.Count);
        }
    }
}
