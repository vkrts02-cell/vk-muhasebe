using System;
using System.Threading.Tasks;
using ErmayMuhasebe.Models;

namespace ErmayMuhasebe.Services
{
    public interface IFinansService
    {
        /// <summary>
        /// Tüm finansal ödeme, tahsilat ve ciro (yönlendirme) işlemlerini tek transaction altında kaydeder.
        /// </summary>
        Task<bool> SaveTransactionAsync(FinancialTransactionRequest request);

        /// <summary>
        /// Belirtilen cari hareketi ve ilişkili tüm finansal kayıtları (Kasa, Banka, KK, EFT vb.) siler.
        /// </summary>
        Task<bool> DeleteTransactionAsync(CariHareket hareket);
    }

    public class FinancialTransactionRequest
    {
        public CariKart Cari { get; set; } = null!;
        public string TransactionType { get; set; } = null!; // Tahsilat, Ödeme, Alacak Dekontu, Borç Dekontu
        public decimal Amount { get; set; }
        public string Method { get; set; } = null!; // Nakit, Kredi Kartı, Havale / EFT, Çek
        public string? Description { get; set; }
        public DateTime Date { get; set; } = DateTime.Now;
        public BankaKart? SelectedKasaOrBanka { get; set; }
        
        // Ciro / Yönlendirme Alanları
        public CariKart? DirectedSupplier { get; set; }
        public DateTime? YonlendirmeTarihi { get; set; }

        // Kredi Kartı / EFT Detayları
        public string? BankaAdi { get; set; }
        public string? KartHesapNo { get; set; } // Kart No veya Hesap No
        public string? OnayDekontNo { get; set; } // Onay Kodu veya Dekont No
        public string? SlipDekontPath { get; set; } // Slip veya Dekont Dosya Yolu

        // Düzenleme (In-place Update) Alanları
        public int ExistingHareketId { get; set; }
        public string? ExistingEvrakNo { get; set; }
        public string? ExistingRefId { get; set; }
    }
}
