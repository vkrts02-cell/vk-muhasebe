using System;
using System.Text;

class Program
{
    static void Main()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        string[] samples = { ""Ã–deme"", ""Ã‡ek"", ""AlÄ±nan"", ""PortfÃ¶y"", ""Ã¼zerinden"" };
        foreach(var s in samples)
        {
            byte[] bytes = Encoding.GetEncoding(1252).GetBytes(s);
            string result = Encoding.UTF8.GetString(bytes);
            Console.WriteLine(result);
        }
    }
}
