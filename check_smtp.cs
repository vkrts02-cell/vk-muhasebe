using System;
using System.IO;
using SQLite;

namespace CheckSmtp
{
    class Program
    {
        static void Main(string[] args)
        {
            var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ErmayMuhasebe", "ErmayV4_Stable.db3");
            if (!File.Exists(dbPath))
            {
                Console.WriteLine("DB not found: " + dbPath);
                return;
            }

            try
            {
                // Simple raw query
                var conn = new SQLiteConnection(dbPath, "Ermay2024SecureKey!");
                var query = conn.Query<FirmaProfili>("SELECT * FROM FirmaProfili LIMIT 1");
                foreach (var p in query)
                {
                    Console.WriteLine($"SMTP Host: {p.SmtpHost}");
                    Console.WriteLine($"SMTP Port: {p.SmtpPort}");
                    Console.WriteLine($"SMTP User: {p.SmtpUser}");
                    Console.WriteLine($"SMTP Pass Length: {(p.SmtpPass != null ? p.SmtpPass.Length : 0)}");
                    Console.WriteLine($"SMTP SSL: {p.SmtpSsl}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public class FirmaProfili
        {
            public string SmtpHost { get; set; }
            public int SmtpPort { get; set; }
            public string SmtpUser { get; set; }
            public string SmtpPass { get; set; }
            public bool SmtpSsl { get; set; }
        }
    }
}
