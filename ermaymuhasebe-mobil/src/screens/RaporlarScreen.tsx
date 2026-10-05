import React, { useState, useEffect } from 'react';
import { StyleSheet, Text, View, SafeAreaView, ScrollView, TouchableOpacity, ActivityIndicator, Alert, FlatList, Modal, TextInput, Share } from 'react-native';
import { useNavigation } from '@react-navigation/native';
import { BarChart3, TrendingUp, Package, ChevronRight, X, Search, Calendar, FileText, Share2, Award, ShieldAlert, Database, FileSpreadsheet } from 'lucide-react-native';
import { subscribeToPath } from '../services/firebase';
import { generateReportPdf } from '../services/pdfService';
import { hesapFifo, cekRiskPuani, hesapLtv } from '../services/analizUtils';
import { GlassCard } from '../components/GlassCard';
import { AppleTheme } from '../theme/appleDesign';

const formatMoney = (val: number) => {
  return new Intl.NumberFormat('tr-TR', { style: 'currency', currency: 'TRY' }).format(val);
};

// Fatura kaynaklı stok hareketleri ('Satış Faturası'/'Alış Faturası') ve manuel işlemleri birlikte sayar
const isSatisHareketi = (turu: string | undefined) => turu === 'Satış' || turu === 'Satış Faturası';
const isAlisHareketi = (turu: string | undefined) => turu === 'Alış' || turu === 'Alış Faturası';

interface ReportItem {
  title: string;
  category: string;
  description: string;
}

export default function RaporlarScreen() {
  const navigation = useNavigation<any>();

  const [faturalar, setFaturalar] = useState<any[]>([]);
  const [stoklar, setStoklar] = useState<any[]>([]);
  const [cariler, setCariler] = useState<any[]>([]);
  const [kasaHareketler, setKasaHareketler] = useState<any[]>([]);
  const [bankaHareketler, setBankaHareketler] = useState<any[]>([]);
  const [cariHareketler, setCariHareketler] = useState<any[]>([]);
  const [stokHareketler, setStokHareketler] = useState<any[]>([]);
  const [bankalar, setBankalar] = useState<any[]>([]);
  const [cekler, setCekler] = useState<any[]>([]);
  const [senetler, setSenetler] = useState<any[]>([]);
  const [kkIslemler, setKkIslemler] = useState<any[]>([]);
  const [eftIslemler, setEftIslemler] = useState<any[]>([]);
  const [aylikHedef, setAylikHedef] = useState<number>(0);
  const [loading, setLoading] = useState(true);
  const [searchQuery, setSearchQuery] = useState('');

  // Modals state
  const [selectedReport, setSelectedReport] = useState<ReportItem | null>(null);
  const [isCariOverlayOpen, setIsCariOverlayOpen] = useState(false);
  const [cariSearch, setCariSearch] = useState('');
  const [selectedCariObject, setSelectedCariObject] = useState<any | null>(null);

  useEffect(() => {
    const listHelper = (path: string, setter: React.Dispatch<React.SetStateAction<any[]>>) => {
      return subscribeToPath(path, (data) => {
        if (!data) {
          setter([]);
        } else {
          const list = Array.isArray(data) ? data.filter(Boolean) : Object.keys(data).map(key => ({ ...data[key], firebaseKey: key }));
          setter(list.filter((x: any) => !x || (!x.isDeleted && x.is_deleted !== true && x.is_deleted !== 1)));
        }
      });
    };

    const unsubFaturalar = listHelper('Faturalar', setFaturalar);
    const unsubStoklar = listHelper('Stoklar', setStoklar);
    const unsubCariler = listHelper('Cariler', setCariler);
    const unsubKasa = listHelper('KasaHareketler', setKasaHareketler);
    const unsubBanka = listHelper('BankaHareketler', setBankaHareketler);
    const unsubCariH = listHelper('CariHareketler', setCariHareketler);
    const unsubStokH = listHelper('StokHareketler', setStokHareketler);
    const unsubBankalar = listHelper('Bankalar', setBankalar);
    const unsubCekler = listHelper('Cekler', setCekler);
    const unsubSenetler = listHelper('Senetler', setSenetler);
    const unsubKk = listHelper('KrediKartlari', setKkIslemler);
    const unsubEft = listHelper('EftIslemleri', setEftIslemler);
    const unsubHedef = subscribeToPath('SatisHedefleri', (data) => {
      if (data) {
        const simdi = new Date();
        const id = `${simdi.getFullYear()}-${String(simdi.getMonth() + 1).padStart(2, '0')}`;
        const obj = (data as any)[id] || (data as any)[Object.keys(data)[0]];
        setAylikHedef(Number(obj?.hedefTutari ?? obj?.tutar) || 0);
      } else {
        setAylikHedef(0);
      }
    });

    setTimeout(() => setLoading(false), 1000);

    return () => {
      unsubFaturalar();
      unsubStoklar();
      unsubCariler();
      unsubKasa();
      unsubBanka();
      unsubCariH();
      unsubStokH();
      unsubBankalar();
      unsubCekler();
      unsubSenetler();
      unsubKk();
      unsubEft();
      unsubHedef();
    };
  }, []);

  const reports: ReportItem[] = [
    { title: 'Genel Özet', category: 'Genel Durum', description: 'İşletmenin genel mali özetini gösterir.' },
    { title: 'Aylık Tahsilat ve Ödeme Analizi', category: 'Finans', description: 'Ay bazlı ödeme türlerine göre tahsilat ve yönlendirme özeti.' },
    { title: 'Kredi Kartı Detay Raporu', category: 'Finans', description: 'Kaydedilen ve yönlendirilen kredi kartı işlem detayları.' },
    { title: 'Nakit İşlem Detay Raporu', category: 'Finans', description: 'Tüm nakit kasa hareketlerinin detaylı dökümü.' },
    { title: 'Çek Detay Raporu', category: 'Finans', description: 'Portföydeki ve işlem görmüş çeklerin detayları.' },
    { title: 'Havale / EFT Detay Raporu', category: 'Finans', description: 'Banka havale ve EFT işlemlerinin detaylı listesi.' },
    { title: 'Cari Bakiye Raporu', category: 'Cari Hesap', description: 'Müşteri ve tedarikçi bakiyeleri.' },
    { title: 'Cari Hareket Dökümü', category: 'Cari Hesap', description: 'Cari hesapların işlem detayları.' },
    { title: 'Yaşlandırma Raporu', category: 'Cari Hesap', description: 'Borç/alacak yaşlandırma analizi.' },
    { title: 'Hareketsiz Cariler', category: 'Cari Hesap', description: 'İşlem görmeyen cari hesaplar.' },
    { title: 'Stok Mevcudu', category: 'Stok', description: 'Güncel stok miktarları ve değerleri.' },
    { title: 'Stok Hareketleri', category: 'Stok', description: 'Giriş-çıkış stok hareket dökümü.' },
    { title: 'Kritik Stok Seviyesi', category: 'Stok', description: 'Minimum seviyenin altındaki ürünler.' },
    { title: 'Ölü Stok Raporu', category: 'Stok', description: 'Çakışan stok kartlarının listesi (Birleştirilmesi Gerekenler).' },
    { title: 'Stok Devir Hızı', category: 'Stok', description: 'Stokların dönüşüm hızı analizi.' },
    { title: 'Satış Faturası Dökümü', category: 'Satış', description: 'Kesilen satış faturalarının listesi.' },
    { title: 'Ürün Karlılık Raporu', category: 'Karlılık', description: 'Ürün bazlı kar/zarar analizi.' },
    { title: 'Müşteri Karlılık Analizi', category: 'Karlılık', description: 'Müşteri bazlı kar/zarar analizi.' },
    { title: 'En Çok Satan Ürünler', category: 'Satış', description: 'Satış adedine göre top listeler.' },
    { title: 'Gelir Tablosu', category: 'Finans', description: 'Dönemsel gelir ve gider özeti.' },
    { title: 'Detaylı Gelir ve Maliyet Analizi', category: 'Karlılık', description: 'Tarih bazlı ürün satışlarının kime yapıldığı, en son kimden ne kadara alındığı ve satır bazlı kâr analizi.' },
    { title: 'Nakit Akış Tablosu', category: 'Finans', description: 'Nakit giriş ve çıkışlarının takibi.' },
    { title: 'Müşteri ABC Analizi', category: 'Stratejik', description: 'Ciroya göre müşteri sınıflandırması. A Sınıfı: En yüksek %80, B Sınıfı: Sonraki %15, C Sınıfı: Kalan %5' },
    { title: 'Kar-Zarar Mukayesesi', category: 'Stratejik', description: 'Yıllık, aylık ve haftalık bazda performans analizi.' },
    { title: 'Vadesi Geçmiş Alacaklar', category: 'Cari Hesap', description: 'Ödeme süresi geçmiş tahsilatlar.' },
    { title: 'Müşteri Kayıp (Churn)', category: 'Stratejik', description: 'Kayıp riski olan pasif müşteriler.' },
    { title: 'Fiyat Dalgalanma Raporu', category: 'Stratejik', description: 'Ürün fiyat değişim trendleri.' },
    { title: 'Müşteri Sadakat (LTV)', category: 'Stratejik', description: 'Müşteri yaşam boyu değer analizi.' },
    { title: 'Finansal Isı Haritası', category: 'Stratejik', description: 'Günlük finansal aktivite yoğunluğu.' },
    { title: 'Tahsilat Süresi (DSO)', category: 'Stratejik', description: 'Ortalama tahsilat hızı (gün).' },
    { title: 'Bütçe / Hedef Takibi', category: 'Stratejik', description: 'Aylık hedeflere ulaşma durumu.' }
  ];

  const getReportData = (reportTitle: string, selectedCari?: any) => {
    let headers: string[] = [];
    let rows: string[][] = [];
    
    const activeFaturalar = faturalar.filter(f => !f.isDeleted);
    const activeCariler = cariler.filter(c => !c.isDeleted);
    const activeStoklar = stoklar.filter(s => !s.isDeleted);

    if (reportTitle === 'Genel Özet') {
      headers = ['Rapor Kalemi', 'Detay'];
      const totalRecv = activeCariler.filter(c => c.borc > c.alacak).reduce((sum, c) => sum + ((c.borc || 0) - (c.alacak || 0)), 0);
      const totalPay = activeCariler.filter(c => c.alacak > c.borc).reduce((sum, c) => sum + (c.alacak - c.borc), 0);
      const bankaTotal = bankalar.reduce((sum, b) => sum + ((b.acilisBakiyesi || 0) + (b.borc || 0) - (b.alacak || 0)), 0);
      const stockQty = activeStoklar.reduce((sum, s) => sum + s.miktar, 0);
      const stockVal = activeStoklar.reduce((sum, s) => sum + s.miktar * s.alisFiyati, 0);

      rows = [
        ['Toplam Alacaklar', formatMoney(totalRecv)],
        ['Toplam Borçlar', formatMoney(totalPay)],
        ['Kasa / Banka Mevcudu', formatMoney(bankaTotal)],
        ['Toplam Stok Miktarı', `${stockQty.toFixed(2)}`],
        ['Stok Maliyet Değeri', formatMoney(stockVal)]
      ];
    }
    else if (reportTitle === 'Cari Bakiye Raporu') {
      headers = ['Ünvan', 'Şehir', 'Borç', 'Alacak', 'Bakiye', 'Durum'];
      rows = activeCariler.map(c => {
        const bakiye = (c.borc || 0) - (c.alacak || 0);
        const durum = bakiye > 0 ? 'Borçlu' : (bakiye < 0 ? 'Alacaklı' : 'Dengede');
        return [c.unvan, c.il || '-', formatMoney(c.borc || 0), formatMoney(c.alacak || 0), formatMoney(bakiye), durum];
      });
    }
    else if (reportTitle === 'Cari Hareket Dökümü' && selectedCari) {
      headers = ['Tarih', 'İşlem', 'Evrak No', 'Borç', 'Alacak'];
      const list = cariHareketler
        .filter(h => h.cariId === selectedCari.id)
        .sort((a, b) => new Date(b.tarih).getTime() - new Date(a.tarih).getTime());
      
      rows = list.map(h => [
        h.tarih,
        h.islemTuru || '-',
        h.evrakNo || '-',
        formatMoney(h.borc || 0),
        formatMoney(h.alacak || 0)
      ]);
    }
    else if (reportTitle === 'Stok Mevcudu') {
      headers = ['Stok Kodu', 'Stok Adı', 'Kategori', 'Miktar', 'Ort. Alış F.', 'Ort. Satış F.', 'Satış F.'];
      rows = activeStoklar.map(s => [
        s.stokKodu || '-',
        s.stokAdi,
        s.kategori || '-',
        (s.miktar || 0).toFixed(2),
        formatMoney(s.ortAlisFiyati || s.alisFiyati || 0),
        formatMoney(s.ortSatisFiyati || s.satisFiyati || 0),
        formatMoney(s.satisFiyati || 0)
      ]);
    }
    else if (reportTitle === 'Stok Hareketleri') {
      headers = ['Tarih', 'Stok Adı', 'İşlem', 'Giren', 'Çıkan', 'Birim Fiyat', 'Kalan'];
      const listed = [...stokHareketler].sort((a, b) => new Date(a.tarih).getTime() - new Date(b.tarih).getTime());
      const balances: Record<string, number> = {};
      const withKalan = listed.map(h => {
        balances[h.stokId] = (balances[h.stokId] || 0) + (h.giren || 0) - (h.cikan || 0);
        return { h, kalan: balances[h.stokId] };
      });
      rows = withKalan.sort((a, b) => new Date(b.h.tarih).getTime() - new Date(a.h.tarih).getTime()).slice(0, 1000).map(({ h, kalan }) => [
        h.tarih || '-',
        h.stokAdi || '-',
        h.islemTuru || '-',
        (h.giren || 0).toFixed(2),
        (h.cikan || 0).toFixed(2),
        formatMoney(h.fiyat || 0),
        formatMoney(kalan)
      ]);
    }
    else if (reportTitle === 'Kritik Stok Seviyesi') {
      headers = ['Stok Kodu', 'Stok Adı', 'Mevcut', 'Min. Seviye', 'Durum', 'Satış Fiyatı'];
      rows = activeStoklar.filter(s => s.miktar < 0 || ((s.minSeviye || 0) > 0 && s.miktar <= (s.minSeviye || 0))).map(s => [
        s.stokKodu || '-',
        s.stokAdi,
        (s.miktar || 0).toFixed(2),
        (s.minSeviye || 0).toFixed(2),
        s.miktar < 0 ? 'Eksiye Düşmüş!' : s.miktar === 0 ? 'Tükenmiş' : 'Kritik',
        formatMoney(s.satisFiyati || 0)
      ]);
    }
    else if (reportTitle === 'Stok Devir Hızı') {
      headers = ['Stok Adı', 'Mevcut Stok', 'Toplam Satış (Çıkan)', 'Stok Devir Hızı', 'Kategori'];
      rows = activeStoklar.map(s => {
        const satis = stokHareketler.filter(h => h.stokId === s.id && (h.cikan || 0) > 0).reduce((sum, h) => sum + (h.cikan || 0), 0);
        const devir = (s.miktar || 0) > 0 ? satis / (s.miktar || 0) : 0;
        return [s.stokAdi, (s.miktar || 0).toFixed(2), satis.toFixed(2), devir.toFixed(2), s.kategori || '-'];
      });
    }
    else if (reportTitle === 'Ürün Karlılık Raporu') {
      headers = ['Ürün Adı', 'Alış Fiyatı', 'Satış Fiyatı', 'Kar', 'Kar Marjı %'];
      rows = activeStoklar.filter(s => (s.satisFiyati || 0) > 0).map(s => {
        const profit = (s.satisFiyati || 0) - (s.alisFiyati || 0);
        const margin = (s.satisFiyati || 0) > 0 ? (profit / (s.satisFiyati || 0) * 100) : 0;
        return [s.stokAdi, formatMoney(s.alisFiyati || 0), formatMoney(s.satisFiyati || 0), formatMoney(profit), `${margin.toFixed(2)}%`];
      });
    }
    else if (reportTitle === 'Müşteri Karlılık Analizi') {
      headers = ['Müşteri', 'Toplam Satış', 'Tahmini Maliyet', 'Net Kar', 'Kar Marjı (%)', 'Fatura Sayısı'];
      const satisFaturalar = activeFaturalar.filter(f => f.tur === 'Satış' || f.tur === 'Satis');
      const rowsMap = new Map<number, { unvan: string; total: number; count: number; faturaIds: Set<number> }>();
      satisFaturalar.forEach(f => {
        if (f.cariId === undefined || f.cariId === null) return;
        const cur = rowsMap.get(f.cariId) || { unvan: f.cariUnvan || 'Bilinmeyen', total: 0, count: 0, faturaIds: new Set<number>() };
        cur.total += f.genelToplam || 0;
        cur.count += 1;
        cur.faturaIds.add(f.id);
        rowsMap.set(f.cariId, cur);
      });
      rows = [...rowsMap.values()].sort((a, b) => b.total - a.total).slice(0, 100).map(g => {
        const maliyet = stokHareketler
          .filter(h => isSatisHareketi(h.islemTuru) && g.faturaIds.has(h.faturaId))
          .reduce((sum, h) => sum + (h.miktar || 0) * (activeStoklar.find(x => x.id === h.stokId)?.ortAlisFiyati || 0), 0);
        const netKar = g.total - maliyet;
        const marj = g.total > 0 ? (netKar / g.total * 100) : 0;
        return [g.unvan, formatMoney(g.total), formatMoney(maliyet), formatMoney(netKar), `${marj.toFixed(1)}%`, String(g.count)];
      });
    }
    else if (reportTitle === 'En Çok Satan Ürünler') {
      headers = ['Ürün Adı', 'Toplam Satış Adeti'];
      rows = activeStoklar.map(s => {
        const sold = stokHareketler.filter(h => h.stokId === s.id && isSatisHareketi(h.islemTuru)).reduce((sum, h) => sum + h.miktar, 0);
        return [s.stokAdi, `${sold.toFixed(0)} Adet`];
      }).sort((a, b) => parseInt(b[1]) - parseInt(a[1])).slice(0, 10);
    }
    else if (reportTitle === 'Nakit Akış Tablosu') {
      headers = ['Tarih', 'Açıklama', 'Giren', 'Çıkan', 'Bakiye'];
      const now = new Date();
      const combined = [
        ...kasaHareketler.map(h => ({ ...h })),
        ...bankaHareketler.map(h => ({ ...h }))
      ].filter(h => {
        const d = new Date(h.tarih);
        return d.getFullYear() === now.getFullYear() && d.getMonth() === now.getMonth();
      }).sort((a, b) => new Date(a.tarih).getTime() - new Date(b.tarih).getTime());

      let running = 0;
      rows = combined.map(h => {
        running += (h.giren || 0) - (h.cikan || 0);
        return [h.tarih, h.aciklama || '-', formatMoney(h.giren || 0), formatMoney(h.cikan || 0), formatMoney(running)];
      });
    }
    else if (reportTitle === 'Nakit İşlem Detay Raporu') {
      headers = ['Tarih', 'Cari Ünvan', 'İşlem', 'Açıklama', 'Giren', 'Çıkan'];
      const combined = [
        ...kasaHareketler.map(h => ({ ...h })),
        ...bankaHareketler.map(h => ({ ...h }))
      ].sort((a, b) => new Date(b.tarih).getTime() - new Date(a.tarih).getTime());

      rows = combined.map(h => [
        h.tarih,
        h.cariUnvan || '-',
        h.islemTuru || 'Nakit',
        h.aciklama || 'Nakit İşlem',
        formatMoney(h.giren || 0),
        formatMoney(h.cikan || 0)
      ]);
    }
    else if (reportTitle === 'Ölü Stok Raporu') {
      headers = ['Stok Adı', 'Barkod', 'Stok Kodu', 'Mevcut Miktar', 'Çakışma Nedeni'];
      const key = (v: string) => (v || '').trim().toLocaleLowerCase('tr-TR');
      const groupBy = (list: any[], field: string) => {
        const m: Record<string, any[]> = {};
        list.filter(s => key(s[field])).forEach(s => { const k = key(s[field]); (m[k] = m[k] || []).push(s); });
        return Object.values(m).filter(g => g.length > 1);
      };
      const done = new Set<string>();
      const dups: string[][] = [];
      const pushDup = (s: any, nedeni: string) => {
        if (done.has(String(s.id))) return;
        done.add(String(s.id));
        dups.push([s.stokAdi || '', s.barkod || '-', s.stokKodu || '-', (s.miktar || 0).toFixed(2), nedeni]);
      };
      groupBy(activeStoklar, 'stokAdi').forEach(g => g.forEach(s => pushDup(s, 'Aynı İsim')));
      groupBy(activeStoklar, 'barkod').forEach(g => g.forEach(s => pushDup(s, 'Aynı Barkod')));
      groupBy(activeStoklar, 'stokKodu').forEach(g => g.forEach(s => pushDup(s, 'Aynı Stok Kodu')));
      rows = dups;
    }
    else if (reportTitle === 'Hareketsiz Cariler') {
      headers = ['Ünvan', 'Grup', 'Telefon', 'Son İşlem', 'Gün'];
      const cutoff = new Date();
      cutoff.setMonth(cutoff.getMonth() - 6);
      rows = activeCariler.filter(c => {
        const moves = cariHareketler.filter(h => h.cariId === c.id);
        if (!moves.length) return true;
        const last = Math.max(...moves.map(h => new Date(h.tarih || h.kayitTarihi || 0).getTime()));
        return last < cutoff.getTime();
      }).map(c => {
        const moves = cariHareketler.filter(h => h.cariId === c.id);
        const lastT = moves.length ? Math.max(...moves.map(h => new Date(h.tarih || h.kayitTarihi || 0).getTime())) : null;
        const gun = lastT ? String(Math.floor((Date.now() - lastT) / 86400000)) : (c.kayitTarihi ? String(Math.floor((Date.now() - new Date(c.kayitTarihi).getTime()) / 86400000)) : 'İşlem Yok');
        const lastStr = lastT ? new Date(lastT).toISOString().split('T')[0] : (c.kayitTarihi ? new Date(c.kayitTarihi).toISOString().split('T')[0] : 'Hiç');
        return [c.unvan, c.grup || '-', c.telefon || '-', lastStr, gun];
      });
    }
    else if (reportTitle === 'Yaşlandırma Raporu') {
      headers = ['Cari Ünvan', '0-30 Gün', '31-60 Gün', '61-90 Gün', '90+ Gün', 'Toplam'];
      const today = new Date();
      rows = activeCariler.map(c => {
        const invs = activeFaturalar.filter(f => f.cariId === c.id && f.tur === 'Satış' && !f.isDeleted);
        let b1 = 0, b2 = 0, b3 = 0, b4 = 0;
        invs.forEach(f => {
          const kalan = (f.genelToplam || 0) - (f.odenen || 0);
          if (kalan <= 0) return;
          const vade = f.vadeTarihi ? new Date(f.vadeTarihi) : new Date(f.tarih || f.kayitTarihi);
          const gun = Math.max(0, Math.floor((today.getTime() - vade.getTime()) / 86400000));
          if (gun <= 30) b1 += kalan;
          else if (gun <= 60) b2 += kalan;
          else if (gun <= 90) b3 += kalan;
          else b4 += kalan;
        });
        if (b1 + b2 + b3 + b4 === 0) return null;
        return [c.unvan, formatMoney(b1), formatMoney(b2), formatMoney(b3), formatMoney(b4), formatMoney(b1 + b2 + b3 + b4)];
      }).filter(Boolean) as string[][];
    }
    else if (reportTitle === 'Müşteri Kayıp (Churn)') {
      headers = ['Müşteri', 'Son İşlem', 'Geçen Gün', 'Kayıp Riski'];
      const cutoff = new Date();
      cutoff.setDate(cutoff.getDate() - 30);
      rows = activeCariler.filter(c => c.tur !== 'Satici' && c.tur !== 'Satıcı').map(c => {
        const moves = cariHareketler.filter(h => h.cariId === c.id);
        const lastT = moves.length ? Math.max(...moves.map(h => new Date(h.tarih || h.kayitTarihi || 0).getTime())) : (c.kayitTarihi ? new Date(c.kayitTarihi).getTime() : 0);
        if (lastT >= cutoff.getTime()) return null;
        const daysSince = Math.floor((Date.now() - lastT) / 86400000);
        const risk = daysSince > 180 ? 'Kritik Kayıp (180+ Gün)' : daysSince > 90 ? 'Yüksek Risk (90+ Gün)' : daysSince > 30 ? 'Orta Risk (30+ Gün)' : 'Düşük Risk';
        return [c.unvan, lastT ? new Date(lastT).toISOString().split('T')[0] : 'İşlem Yok', String(daysSince), risk];
      }).filter(Boolean) as string[][];
    }
    else if (reportTitle === 'Müşteri ABC Analizi') {
      headers = ['Müşteri', 'Toplam Satış', 'Yüzde', 'Sınıf'];
      const groupedMap = new Map<string, number>();
      activeFaturalar.filter(f => f.tur === 'Satış' || f.tur === 'Satis').forEach(f => {
        const unvan = f.cariUnvan || 'Bilinmeyen';
        groupedMap.set(unvan, (groupedMap.get(unvan) || 0) + (f.genelToplam || 0));
      });
      const grouped = [...groupedMap.entries()].map(([Musteri, Total]) => ({ Musteri, Total })).sort((a, b) => b.Total - a.Total);
      const grandTotal = grouped.reduce((s, x) => s + x.Total, 0);
      let cumulative = 0;
      rows = grouped.map(x => {
        cumulative += x.Total;
        const pct = grandTotal > 0 ? (x.Total / grandTotal * 100) : 0;
        const cumPct = grandTotal > 0 ? (cumulative / grandTotal * 100) : 0;
        return [x.Musteri, formatMoney(x.Total), `${pct.toFixed(2)}%`, cumPct <= 80 ? 'A' : cumPct <= 95 ? 'B' : 'C'];
      });
      rows.push(['', '', '', '']);
      rows.push(['A Sınıfı: %80 Ciro', 'B Sınıfı: %15 Ciro', 'C Sınıfı: %5 Ciro', '']);
    }
    else if (reportTitle === 'Müşteri Sadakat (LTV)') {
      headers = ['Müşteri', 'Ciro', 'Sipariş', 'Ort. Sepet', 'İlk-İşlem', 'Son-İşlem', 'LTV'];
      rows = hesapLtv(activeFaturalar).map(r => [
        r.musteri,
        formatMoney(r.ciro),
        String(r.adet),
        formatMoney(r.sepet),
        r.ilk || '-',
        r.son || '-',
        formatMoney(r.ltv)
      ]);
    }
    else if (reportTitle === 'Gelir Tablosu') {
      headers = ['Kalem', 'Tutar'];
      const satis = activeFaturalar.filter(f => f.tur === 'Satış' || f.tur === 'Satis').reduce((s, f) => s + (f.genelToplam || 0), 0);
      const alis = activeFaturalar.filter(f => f.tur === 'Alış' || f.tur === 'Alis').reduce((s, f) => s + (f.genelToplam || 0), 0);
      const malMaliyeti = stokHareketler.filter(h => isSatisHareketi(h.islemTuru)).reduce((s, h) => s + (h.miktar || 0) * (activeStoklar.find(x => x.id === h.stokId)?.ortAlisFiyati || 0), 0);
      const brutKar = satis - malMaliyeti;
      rows = [
        ['Satış Gelirleri (Toplam Satış)', formatMoney(satis)],
        ['Satılan Malın Maliyeti (SMM)', formatMoney(malMaliyeti)],
        ['Brüt Kar (Kazanılan Net Para)', formatMoney(brutKar)],
        ['Dönem İçi Toplam Alış (Satın Alma)', formatMoney(alis)],
        ['Net Dönem Karı', formatMoney(brutKar)]
      ];
    }
    else if (reportTitle === 'Detaylı Gelir ve Maliyet Analizi') {
      headers = ['Ürün', 'Satış Tutarı', 'Maliyet', 'Net Kâr'];
      rows = activeStoklar.map(s => {
        const satis = stokHareketler.filter(h => h.stokId === s.id && isSatisHareketi(h.islemTuru)).reduce((sum, h) => sum + (h.miktar || 0) * (s.satisFiyati || 0), 0);
        const maliyet = stokHareketler.filter(h => h.stokId === s.id && isSatisHareketi(h.islemTuru)).reduce((sum, h) => sum + (h.miktar || 0) * (s.ortAlisFiyati || s.alisFiyati || 0), 0);
        return [s.stokAdi, formatMoney(satis), formatMoney(maliyet), formatMoney(satis - maliyet)];
      }).filter(r => parseFloat(r[1].replace(/\D/g, '')) > 0 || parseFloat(r[2].replace(/\D/g, '')) > 0);
    }
    else if (reportTitle === 'Kar-Zarar Mukayesesi') {
      headers = ['Dönem', 'Satış Tutarı', 'Alış Tutarı', 'Net Kar/Zarar'];
      const rowsList: string[][] = [];
      rowsList.push(['--- YILLIK ANALİZ ---', '', '', '']);
      for (let i = 4; i >= 0; i--) {
        const yil = new Date().getFullYear() - i;
        const s = activeFaturalar.filter(f => (f.tur === 'Satış' || f.tur === 'Satis') && new Date(f.tarih).getFullYear() === yil).reduce((x, f) => x + (f.genelToplam || 0), 0);
        const a = activeFaturalar.filter(f => (f.tur === 'Alış' || f.tur === 'Alis') && new Date(f.tarih).getFullYear() === yil).reduce((x, f) => x + (f.genelToplam || 0), 0);
        rowsList.push([String(yil), formatMoney(s), formatMoney(a), formatMoney(s - a)]);
      }
      rowsList.push(['--- AYLIK ANALİZ (SON 6 AY) ---', '', '', '']);
      for (let i = 5; i >= 0; i--) {
        const d = new Date();
        d.setMonth(d.getMonth() - i);
        const s = activeFaturalar.filter(f => (f.tur === 'Satış' || f.tur === 'Satis') && new Date(f.tarih).getFullYear() === d.getFullYear() && new Date(f.tarih).getMonth() === d.getMonth()).reduce((x, f) => x + (f.genelToplam || 0), 0);
        const a = activeFaturalar.filter(f => (f.tur === 'Alış' || f.tur === 'Alis') && new Date(f.tarih).getFullYear() === d.getFullYear() && new Date(f.tarih).getMonth() === d.getMonth()).reduce((x, f) => x + (f.genelToplam || 0), 0);
        rowsList.push([`${String(d.getMonth() + 1).padStart(2, '0')}/${d.getFullYear()}`, formatMoney(s), formatMoney(a), formatMoney(s - a)]);
      }
      rowsList.push(['--- HAFTALIK ANALİZ (SON 4 HAFTA) ---', '', '', '']);
      for (let i = 3; i >= 0; i--) {
        const start = new Date();
        start.setDate(start.getDate() - (i * 7 + 7));
        const end = new Date();
        end.setDate(end.getDate() - (i * 7));
        const s = activeFaturalar.filter(f => (f.tur === 'Satış' || f.tur === 'Satis') && new Date(f.tarih) >= start && new Date(f.tarih) < end).reduce((x, f) => x + (f.genelToplam || 0), 0);
        const a = activeFaturalar.filter(f => (f.tur === 'Alış' || f.tur === 'Alis') && new Date(f.tarih) >= start && new Date(f.tarih) < end).reduce((x, f) => x + (f.genelToplam || 0), 0);
        rowsList.push([`${start.getDate()}.${start.getMonth() + 1} - ${end.getDate()}.${end.getMonth() + 1}`, formatMoney(s), formatMoney(a), formatMoney(s - a)]);
      }
      rows = rowsList;
    }
    else if (reportTitle === 'Satış Faturası Dökümü') {
      headers = ['Fatura No', 'Tarih', 'Müşteri', 'Ara Toplam', 'KDV', 'Genel Toplam'];
      rows = activeFaturalar.filter(f => f.tur === 'Satış' || f.tur === 'Satis').sort((a, b) => new Date(b.tarih).getTime() - new Date(a.tarih).getTime()).slice(0, 500).map(f => [
        f.faturaNo || f.id?.toString() || '-',
        f.tarih || '-',
        f.cariUnvan || '-',
        formatMoney(f.araToplam || 0),
        formatMoney(f.kdvTutari || f.kdvOrani || 0),
        formatMoney(f.genelToplam || 0)
      ]);
    }
    else if (reportTitle === 'Aylık Tahsilat ve Ödeme Analizi') {
      headers = ['Ay/Yıl', 'Nakit (Alınan)', 'Nakit (Yönl.)', 'K.Kartı (Alınan)', 'K.Kartı (Yönl.)', 'Havale (Alınan)', 'Havale (Yönl.)', 'Çek (Alınan)', 'Çek (Yönl.)'];
      const monthOf = (d: any) => {
        const dt = d ? new Date(d) : null;
        if (!dt || isNaN(dt.getTime())) return null;
        return `${dt.getFullYear()}-${String(dt.getMonth() + 1).padStart(2, '0')}`;
      };
      const monthSet = new Set<string>();
      [...kasaHareketler, ...kkIslemler, ...eftIslemler].forEach(x => { const m = monthOf(x.tarih); if (m) monthSet.add(m); });
      cekler.forEach(x => { const m = monthOf(x.vadeTarihi); if (m) monthSet.add(m); });
      const months = [...monthSet].sort().slice(-24).reverse();
      rows = months.map(m => {
        const inMonth = (d: any) => monthOf(d) === m;
        const nAlinan = kasaHareketler.filter(h => inMonth(h.tarih)).reduce((s, h) => s + (h.giren || 0), 0);
        const nYonl = kasaHareketler.filter(h => inMonth(h.tarih)).reduce((s, h) => s + (h.cikan || 0), 0);
        const kkAlinan = kkIslemler.filter(x => inMonth(x.tarih)).reduce((s, x) => s + (x.tutar || 0), 0);
        const kkYonl = kkIslemler.filter(x => x.yonlendirilenCariId && inMonth(x.tarih)).reduce((s, x) => s + (x.tutar || 0), 0);
        const efAlinan = eftIslemler.filter(x => inMonth(x.tarih)).reduce((s, x) => s + (x.tutar || 0), 0);
        const efYonl = eftIslemler.filter(x => x.yonlendirilenCariId && inMonth(x.tarih)).reduce((s, x) => s + (x.tutar || 0), 0);
        const cAlinan = cekler.filter(x => inMonth(x.vadeTarihi)).reduce((s, x) => s + (x.tutar || 0), 0);
        const cYonl = cekler.filter(x => x.durum && (x.durum.includes('Ciro') || x.durum.includes('Tedarikçi')) && inMonth(x.vadeTarihi)).reduce((s, x) => s + (x.tutar || 0), 0);
        const [y, mo] = m.split('-');
        return [`${mo}/${y}`, formatMoney(nAlinan), formatMoney(nYonl), formatMoney(kkAlinan), formatMoney(kkYonl), formatMoney(efAlinan), formatMoney(efYonl), formatMoney(cAlinan), formatMoney(cYonl)];
      });
    }
    else if (reportTitle === 'Vadesi Geçmiş Alacaklar') {
      headers = ['Cari', 'Fatura No', 'Vade Tarihi', 'Geciken Gün', 'Kalan'];
      const today = new Date();
      rows = activeFaturalar.filter(f => f.tur === 'Satış' && !f.isDeleted)
        .map(f => {
          const kalan = (f.genelToplam || 0) - (f.odenen || 0);
          const vade = f.vadeTarihi ? new Date(f.vadeTarihi) : new Date(f.tarih || f.kayitTarihi);
          const geciken = Math.floor((today.getTime() - vade.getTime()) / 86400000);
          return { f, kalan, geciken };
        })
        .filter(x => x.kalan > 0 && x.geciken > 0)
        .sort((a, b) => b.geciken - a.geciken)
        .map(x => [
          x.f.cariUnvan || '-',
          x.f.faturaNo || x.f.id?.toString() || '-',
          x.f.vadeTarihi || x.f.tarih || '-',
          `${x.geciken} gün`,
          formatMoney(x.kalan)
        ]);
    }
    else if (reportTitle === 'Tahsilat Süresi (DSO)') {
      headers = ['Müşteri', 'Ortalama Tahsilat Süresi (Gün)', 'Toplam Alacak', 'Durum'];
      rows = activeCariler.filter(c => (c.borc || 0) - (c.alacak || 0) > 0).map(c => {
        const invs = activeFaturalar.filter(f => f.cariId === c.id && (f.tur === 'Satış' || f.tur === 'Satis') && f.vadeTarihi && f.tarih);
        if (!invs.length) return null;
        const toplamGun = invs.reduce((s, f) => s + Math.max(0, Math.floor((new Date(f.vadeTarihi).getTime() - new Date(f.tarih).getTime()) / 86400000)), 0);
        const ort = toplamGun / invs.length;
        const durum = ort > 60 ? 'Riskli' : ort > 30 ? 'Normal' : 'İyi';
        return [c.unvan, ort.toFixed(0), formatMoney((c.borc || 0) - (c.alacak || 0)), durum];
      }).filter(Boolean) as string[][];
    }
    else if (reportTitle === 'Kredi Kartı Detay Raporu') {
      headers = ['Tarih', 'Kimden Alındı', 'Banka/Kart', 'Kime Yönlendirildi', 'Tutar', 'Durum'];
      rows = kkIslemler.filter(x => !x.isDeleted).sort((a, b) => new Date(b.tarih).getTime() - new Date(a.tarih).getTime()).map(x => [
        x.tarih || '-',
        x.musteriUnvan || x.cariUnvan || '-',
        `${x.banka || ''}${x.kartNo ? '/' + x.kartNo : ''}` || '-',
        x.yonlendirilenCariUnvan || '-',
        formatMoney(x.tutar || 0),
        x.durum || 'Portföyde'
      ]);
    }
    else if (reportTitle === 'Çek Detay Raporu') {
      headers = ['Vade', 'Çek No', 'Cari Ünvan', 'Banka/Şube', 'Tutar', 'Durum'];
      rows = cekler.filter(x => !x.isDeleted).sort((a, b) => new Date(a.vadeTarihi).getTime() - new Date(b.vadeTarihi).getTime()).map(x => [
        x.vadeTarihi || '-',
        x.cekNo || x.seriNo || x.portfoyNo || x.id?.toString() || '-',
        x.cariUnvan || x.borclu || '-',
        `${x.banka || ''}/${x.sube || ''}`,
        formatMoney(x.tutar || 0),
        x.durum || '-'
      ]);
    }
    else if (reportTitle === 'Havale / EFT Detay Raporu') {
      headers = ['Tarih', 'Müşteri', 'Banka/Hesap', 'Dekont No', 'Tutar', 'Durum'];
      rows = eftIslemler.filter(x => !x.isDeleted).sort((a, b) => new Date(b.tarih).getTime() - new Date(a.tarih).getTime()).map(x => [
        x.tarih || '-',
        x.musteriUnvan || x.cariUnvan || '-',
        `${x.banka || ''}/${x.hesapNo || ''}`,
        x.dekontNo || '-',
        formatMoney(x.tutar || 0),
        x.durum || '-'
      ]);
    }
    else if (reportTitle === 'Fiyat Dalgalanma Raporu') {
      headers = ['Ürün', 'Min Fiyat', 'Max Fiyat', 'Ort Fiyat', 'Fark %'];
      const pulseData = stokHareketler.filter(h => (h.fiyat || 0) > 0 && (h.islemTuru === 'Alış Faturası' || h.islemTuru === 'GİRİŞ'));
      const grouped: Record<string, number[]> = {};
      pulseData.forEach(h => { (grouped[h.stokAdi || '-'] = grouped[h.stokAdi || '-'] || []).push(h.fiyat); });
      rows = Object.entries(grouped).map(([ad, fiyatlar]) => {
        const min = Math.min(...fiyatlar);
        const max = Math.max(...fiyatlar);
        const avg = fiyatlar.reduce((x, f) => x + f, 0) / fiyatlar.length;
        const variance = min > 0 ? ((max - min) / min * 100) : 0;
        return [ad, formatMoney(min), formatMoney(max), formatMoney(avg), `${variance.toFixed(2)}%`];
      });
    }
    else if (reportTitle === 'Finansal Isı Haritası') {
      headers = ['Tarih', 'Satış', 'Alış', 'Kasa Giren', 'Kasa Çıkan', 'Net Akış'];
      const dateKey = (d: Date) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
      const rowsByDay: Record<string, string[]> = {};
      for (let i = 29; i >= 0; i--) {
        const d = new Date();
        d.setDate(d.getDate() - i);
        const satis = activeFaturalar.filter(f => (f.tur === 'Satış' || f.tur === 'Satis') && dateKey(new Date(f.tarih)) === dateKey(d)).reduce((s, f) => s + (f.genelToplam || 0), 0);
        const alis = activeFaturalar.filter(f => (f.tur === 'Alış' || f.tur === 'Alis') && dateKey(new Date(f.tarih)) === dateKey(d)).reduce((s, f) => s + (f.genelToplam || 0), 0);
        const kasaIn = kasaHareketler.filter(h => dateKey(new Date(h.tarih)) === dateKey(d)).reduce((s, h) => s + (h.giren || 0), 0);
        const kasaOut = kasaHareketler.filter(h => dateKey(new Date(h.tarih)) === dateKey(d)).reduce((s, h) => s + (h.cikan || 0), 0);
        const netFlow = satis - alis + kasaIn - kasaOut;
        const fmt = `${String(d.getDate()).padStart(2, '0')}.${String(d.getMonth() + 1).padStart(2, '0')}.${d.getFullYear()}`;
        rowsByDay[dateKey(d)] = [fmt, formatMoney(satis), formatMoney(alis), formatMoney(kasaIn), formatMoney(kasaOut), formatMoney(netFlow)];
      }
      rows = Object.values(rowsByDay);
    }
    else if (reportTitle === 'Bütçe / Hedef Takibi') {
      headers = ['Gösterge', 'Değer'];
      const gerceklesen = activeFaturalar.filter(f => f.tur === 'Satış').reduce((s, f) => s + (f.genelToplam || 0), 0);
      const oran = aylikHedef && aylikHedef > 0 ? (gerceklesen / aylikHedef) * 100 : 0;
      rows = [
        ['Aylık Hedef', formatMoney(aylikHedef || 0)],
        ['Gerçekleşen (Toplam Satış)', formatMoney(gerceklesen)],
        ['Hedefe Ulaşma Oranı', `${oran.toFixed(1)}%`]
      ];
    }

    return { headers, rows };
  };

  const getBudgetData = () => {
    const satis = faturalar.filter(f => f.tur === 'Satış' || f.tur === 'Satis');
    const annual = [];
    const currentYear = new Date().getFullYear();
    const annualActual = satis.filter(f => new Date(f.tarih).getFullYear() === currentYear).reduce((s, f) => s + (f.genelToplam || 0), 0);
    annual.push({ Label: currentYear.toString(), Target: aylikHedef * 12, Actual: annualActual });

    const monthly = [];
    for (let m = 1; m <= 12; m++) {
      const a = satis.filter(f => new Date(f.tarih).getFullYear() === currentYear && (new Date(f.tarih).getMonth() + 1) === m).reduce((s, f) => s + (f.genelToplam || 0), 0);
      monthly.push({ Label: m.toString(), Target: aylikHedef, Actual: a });
    }

    const weekly: any[] = [];
    return { Annual: annual, Monthly: monthly, Weekly: weekly };
  };

  const calculateReport = (reportTitle: string, selectedCari?: any) => {
    const { headers, rows } = getReportData(reportTitle, selectedCari);
    
    let pdfConfig: any = null;

    if (reportTitle === 'Genel Özet') {
      const sections: any[] = [];
      const summaryMetrics = [];
      for (let i = 0; i < Math.min(4, rows.length); i++) {
        summaryMetrics.push({ Label: rows[i][0], Actual: parseFloat(rows[i][1].replace(/[^\d,-]/g, '').replace(',', '.')) || 0 });
      }
      sections.push({
        Title: reportTitle,
        Headers: headers,
        Rows: rows,
        KeyMetrics: summaryMetrics,
        ChartData: summaryMetrics.filter(x => !x.Label.includes('Miktar')),
        LeftChartTitle: 'VARLIK VE YÜKÜMLÜLÜK DAĞILIMI',
        NewPage: false
      });

      const budgetRes = getReportData('Bütçe / Hedef Takibi');
      const budgetGraph = getBudgetData();
      sections.push({
        Title: 'BÜTÇE VE HEDEF ANALİZİ (AYLIK)',
        Headers: budgetRes.headers,
        Rows: budgetRes.rows,
        ChartData: budgetGraph.Monthly,
        KeyMetrics: budgetGraph.Monthly.sort((a, b) => b.Actual - a.Actual).slice(0, 4),
        NewPage: true
      });

      reports.filter(r => r.title !== 'Genel Özet' && r.title !== 'Bütçe / Hedef Takibi').forEach(r => {
        try {
          const res = getReportData(r.title);
          if (res.rows && res.rows.length > 0) {
            sections.push({
              Title: r.title,
              Headers: res.headers,
              Rows: res.rows,
              NewPage: true
            });
          }
        } catch(e) {}
      });

      pdfConfig = {
        endpoint: 'consolidated',
        payload: { Title: 'ERMAY MUHASEBE - GENEL RAPOR PAKETİ', Sections: sections }
      };
    } else if (reportTitle === 'Kar-Zarar Mukayesesi') {
      pdfConfig = {
        endpoint: 'consolidated',
        payload: {
          Title: 'KAR-ZARAR MUKAYESE RAPORU',
          Sections: [{
            Title: reportTitle,
            Headers: headers,
            Rows: rows,
            NewPage: false,
            ChartData: rows.filter(r => !r[0].includes('---')).map(r => ({
              Label: r[0],
              Actual: parseFloat(r[3]?.replace(/[^\d,-]/g, '').replace(',', '.')) || 0
            }))
          }]
        }
      };
    } else if (reportTitle === 'Finansal Isı Haritası') {
      pdfConfig = {
        endpoint: 'generic',
        payload: {
          title: 'FİNANSAL ISI HARİTASI (SON 30 GÜN)',
          subtitle: 'Günlük Gelir/Gider ve Net Nakit Akış Dağılımı',
          headers: headers,
          rows: rows
        }
      };
    } else if (reportTitle === 'Bütçe / Hedef Takibi') {
      const budgetGraph = getBudgetData();
      pdfConfig = {
        endpoint: 'budget',
        payload: { Title: 'BÜTÇE VE HEDEF ANALİZ RAPORU', Annual: budgetGraph.Annual, Monthly: budgetGraph.Monthly, Weekly: budgetGraph.Weekly }
      };
    }

    if (headers.length > 0) {
      navigation.navigate('RaporDetay', { reportResult: { headers, rows, title: reportTitle }, pdfConfig });
    } else {
      Alert.alert('Bilgi', 'Bu rapor henüz uygulanmadı.');
    }
  };

  const handleRunReport = (report: ReportItem) => {
    setSelectedReport(report);
    if (report.title === 'Cari Hareket Dökümü') {
      setIsCariOverlayOpen(true);
    } else {
      calculateReport(report.title);
    }
  };

  const filteredReports = reports.filter(r => 
    r.title.toLocaleLowerCase('tr-TR').includes(searchQuery.toLocaleLowerCase('tr-TR')) ||
    r.description.toLocaleLowerCase('tr-TR').includes(searchQuery.toLocaleLowerCase('tr-TR'))
  );

  return (
    <SafeAreaView style={styles.container}>
      <View style={styles.header}>
        <View style={{ flexDirection: 'row', alignItems: 'center' }}>
          <BarChart3 color="#0061FF" size={28} style={{ marginRight: 12 }} />
          <Text style={styles.headerTitle}>Raporlar</Text>
        </View>
        <Text style={styles.headerSubtitle}>Tüm finansal, stok ve cari analiz raporları</Text>
      </View>

      <View style={[styles.searchBox, { marginHorizontal: 20, marginTop: 16 }]}>
        <Search color="#64748B" size={20} />
        <TextInput 
          style={styles.searchInput} 
          placeholder="Rapor ara..." 
          placeholderTextColor="#64748B"
          value={searchQuery}
          onChangeText={setSearchQuery}
        />
      </View>

      {loading ? (
        <View style={styles.center}>
          <ActivityIndicator size="large" color="#0061FF" />
        </View>
      ) : (
        <FlatList initialNumToRender={20} maxToRenderPerBatch={20} windowSize={5} 
          data={filteredReports}
          keyExtractor={(item, index) => index.toString()}
          contentContainerStyle={styles.listContent}
          renderItem={({ item }) => (
            <GlassCard style={[styles.card, { marginBottom: 10 }]} onPress={() => handleRunReport(item)}>
              <View style={styles.cardHeader}>
                <View style={[styles.iconBox, { backgroundColor: item.category === 'Stok' ? 'rgba(48, 209, 88, 0.12)' : 'rgba(10, 132, 255, 0.12)' }]}>
                  <FileText color={item.category === 'Stok' ? '#30D158' : '#0A84FF'} size={20} />
                </View>
                <View style={{ flex: 1, marginLeft: 12 }}>
                  <Text style={styles.cardTitle}>{item.title}</Text>
                  <Text style={styles.cardDesc}>{item.description}</Text>
                </View>
                <ChevronRight color="rgba(235, 235, 245, 0.4)" size={20} />
              </View>
            </GlassCard>
          )}
        />
      )}

      {/* Cari Seçici Absolute Overlay (Nested Modal yerine) */}
      {isCariOverlayOpen && (
        <View style={styles.absoluteOverlay}>
          <View style={styles.modalHeader}>
            <Text style={styles.modalTitle}>Cari Seçin</Text>
            <TouchableOpacity onPress={() => setIsCariOverlayOpen(false)}>
              <X color="#FFF" size={24} />
            </TouchableOpacity>
          </View>
          <View style={[styles.searchBox, { marginBottom: 16 }]}>
            <Search color="#64748B" size={20} />
            <TextInput 
              style={styles.searchInput} 
              placeholder="Cari ara..." 
              placeholderTextColor="#64748B" 
              value={cariSearch} 
              onChangeText={setCariSearch} 
            />
          </View>
          <FlatList initialNumToRender={20} maxToRenderPerBatch={20} windowSize={5} 
            data={cariler.filter(c => (c.unvan || '').toLocaleLowerCase('tr-TR').includes(cariSearch.toLocaleLowerCase('tr-TR')))}
            keyExtractor={(item, idx) => item.id?.toString() || idx.toString()}
            renderItem={({ item }) => (
              <TouchableOpacity 
                style={styles.selectorItem}
                onPress={() => {
                  setSelectedCariObject(item);
                  setIsCariOverlayOpen(false);
                  calculateReport(selectedReport?.title || '', item);
                }}
              >
                <Text style={styles.selectorItemText}>{item.unvan}</Text>
                <Text style={styles.selectorItemSub}>{item.grup}</Text>
              </TouchableOpacity>
            )}
          />
        </View>
      )}
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#0A0A0A',
  },
  center: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
  },
  header: {
    padding: 20,
    paddingTop: 40,
    borderBottomWidth: 1,
    borderBottomColor: 'rgba(255, 255, 255, 0.08)',
  },
  headerTitle: {
    fontSize: 22,
    fontWeight: '900',
    color: '#FFFFFF',
  },
  headerSubtitle: {
    color: '#64748B',
    fontSize: 13,
    marginTop: 4,
  },
  searchBox: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#2A2A2A',
    borderRadius: 12,
    borderWidth: 1,
    borderColor: 'rgba(255,255,255,0.1)',
    paddingHorizontal: 12,
    height: 44,
  },
  searchInput: {
    flex: 1,
    color: '#FFF',
    fontSize: 15,
    marginLeft: 8,
  },
  listContent: {
    padding: 20,
    paddingBottom: 40,
  },
  card: {
    backgroundColor: '#1A1A1A',
    borderRadius: 16,
    padding: 16,
    marginBottom: 12,
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.05)',
  },
  cardHeader: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  iconBox: {
    width: 48,
    height: 48,
    borderRadius: 12,
    justifyContent: 'center',
    alignItems: 'center',
  },
  cardTitle: {
    color: '#FFF',
    fontSize: 16,
    fontWeight: '600',
  },
  cardDesc: {
    color: '#94A3B8',
    fontSize: 12,
    marginTop: 4,
  },
  animatedOverlay: {
    position: 'absolute',
    top: 0,
    left: 0,
    right: 0,
    bottom: 0,
    zIndex: 999,
    backgroundColor: '#0A0A0A',
  },
  modalOverlay: {
    flex: 1,
    backgroundColor: '#0A0A0A',
    justifyContent: 'flex-start',
    padding: 0,
  },
  modalContent: {
    backgroundColor: '#0A0A0A',
    borderRadius: 0,
    padding: 20,
    flex: 1,
    maxHeight: '100%',
    borderWidth: 0,
    borderColor: 'transparent',
  },
  modalHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    borderBottomWidth: 1,
    borderBottomColor: 'rgba(255, 255, 255, 0.1)',
    paddingBottom: 12,
    marginBottom: 12,
  },
  modalTitle: {
    color: '#FFF',
    fontSize: 18,
    fontWeight: '600',
    flex: 1,
    marginRight: 10,
  },
  tableHeaderRow: {
    flexDirection: 'row',
    backgroundColor: '#2A2A2A',
    paddingVertical: 10,
    borderRadius: 8,
    marginBottom: 8,
  },
  tableHeaderCell: {
    flex: 1,
    color: '#94A3B8',
    fontSize: 12,
    fontWeight: '600',
    textAlign: 'center',
  },
  tableRow: {
    flexDirection: 'row',
    borderBottomWidth: 1,
    borderBottomColor: 'rgba(255, 255, 255, 0.05)',
    paddingVertical: 12,
  },
  tableCell: {
    flex: 1,
    color: '#FFF',
    fontSize: 13,
    textAlign: 'center',
  },
  shareBtn: {
    backgroundColor: '#0061FF',
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    paddingVertical: 14,
    borderRadius: 12,
  },
  shareBtnText: {
    color: '#FFF',
    fontSize: 15,
    fontWeight: '600',
  },
  absoluteOverlay: {
    position: 'absolute',
    top: 0,
    left: 0,
    right: 0,
    bottom: 0,
    backgroundColor: '#0A0A0A',
    padding: 20,
    paddingTop: 50,
    zIndex: 999,
  },
  selectorItem: {
    padding: 16,
    backgroundColor: '#1A1A1A',
    borderRadius: 12,
    marginBottom: 8,
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.05)',
  },
  selectorItemText: {
    color: '#FFF',
    fontSize: 15,
    fontWeight: '500',
  },
  selectorItemSub: {
    color: '#64748B',
    fontSize: 13,
    marginTop: 4,
  }
});
