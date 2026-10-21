import React, { useState, useEffect, useRef } from 'react';
import { StyleSheet, Text, View, ScrollView, TouchableOpacity, StatusBar, Platform, ActivityIndicator, Animated, Easing } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { subscribeToPath } from '../services/firebase';
import { useNavigation } from '@react-navigation/native';
import Svg, { Path, Line, Rect, Text as SvgText, Defs, LinearGradient, Stop, Circle, G } from 'react-native-svg';
import { 
  ArrowUpRight, 
  Banknote, 
  TrendingUp, 
  TrendingDown, 
  Package, 
  Activity, 
  PieChart, 
  Clock, 
  RefreshCw,
  PlusCircle,
  FilePlus,
  ShoppingBag,
  FileSpreadsheet,
  Wallet
} from 'lucide-react-native';
import { getUiScale, scaleFont, UiScale } from '../services/themeService';
import { ShimmerItem } from '../components/Shimmer';
import { AppleTheme } from '../theme/appleDesign';

const formatMoney = (val: any) => {
  const num = typeof val === 'number' ? val : (parseFloat(val) || 0);
  try {
    return new Intl.NumberFormat('tr-TR', { style: 'currency', currency: 'TRY' }).format(num);
  } catch {
    return `₺${num.toFixed(2)}`;
  }
};

const parseSafeDate = (val: any): Date | null => {
  if (!val) return null;
  if (val instanceof Date) return isNaN(val.getTime()) ? null : val;
  if (typeof val === 'string') {
    const trimmed = val.trim();
    if (!trimmed) return null;
    if (trimmed.includes('.') || trimmed.includes('/')) {
      const parts = trimmed.split(/[\/.]/);
      if (parts.length >= 3) {
        const day = parseInt(parts[0], 10);
        const month = parseInt(parts[1], 10) - 1;
        const year = parseInt(parts[2], 10);
        const parsed = new Date(year, month, day);
        return isNaN(parsed.getTime()) ? null : parsed;
      }
    }
    const d = new Date(trimmed);
    return isNaN(d.getTime()) ? null : d;
  }
  return null;
};

const getSafeIsoDateStr = (val: any): string => {
  const d = parseSafeDate(val);
  if (!d) return '';
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, '0');
  const day = String(d.getDate()).padStart(2, '0');
  return `${y}-${m}-${day}`;
};

export default function DashboardScreen() {
  const navigation = useNavigation<any>();
  const [activeTab, setActiveTab] = useState<'recents' | 'receivables' | 'payables'>('recents');
  const [loading, setLoading] = useState(true);
  const [uiScale, setUiScale] = useState<UiScale>('orta');

  // Data states
  const [faturalar, setFaturalar] = useState<any[]>([]);
  const [cariler, setCariler] = useState<any[]>([]);
  const [stoklar, setStoklar] = useState<any[]>([]);
  const [kasaHareketler, setKasaHareketler] = useState<any[]>([]);
  const [bankaHareketler, setBankaHareketler] = useState<any[]>([]);
  const [aylikHedefler, setAylikHedefler] = useState<any[]>([]);

  // Chart Animation State (0 -> 1)
  const animValue = useRef(new Animated.Value(0)).current;
  const [chartAnim, setChartAnim] = useState(0);

  useEffect(() => {
    if (!loading) {
      animValue.setValue(0);
      const listenerId = animValue.addListener(({ value }) => {
        setChartAnim(value);
      });
      Animated.timing(animValue, {
        toValue: 1,
        duration: 900,
        easing: Easing.out(Easing.cubic),
        useNativeDriver: false,
      }).start();

      return () => {
        animValue.removeListener(listenerId);
      };
    }
  }, [loading, aylikHedefler.length, faturalar.length]);

  useEffect(() => {
    const listHelper = (path: string, setter: React.Dispatch<React.SetStateAction<any[]>>) => {
      return subscribeToPath(path, (data) => {
        if (!data) {
          setter([]);
          return;
        }
        const list = Array.isArray(data) 
          ? data.filter(Boolean) 
          : Object.keys(data).map(key => ({ ...data[key], firebaseKey: key }));
        setter(list);
      });
    };

    const unsubFaturalar = listHelper('Faturalar', setFaturalar);
    const unsubCariler = listHelper('Cariler', setCariler);
    const unsubStoklar = listHelper('Stoklar', setStoklar);
    const unsubKasa = listHelper('KasaHareketler', setKasaHareketler);
    const unsubBanka = listHelper('BankaHareketler', setBankaHareketler);
    const unsubHedefler = listHelper('SatisHedefleri', setAylikHedefler);

    getUiScale().then(setUiScale);

    setTimeout(() => setLoading(false), 800);

    return () => {
      unsubFaturalar();
      unsubCariler();
      unsubStoklar();
      unsubKasa();
      unsubBanka();
      unsubHedefler();
    };
  }, []);

  // Compute stats
  const getStats = () => {
    const today = new Date();
    const todayStr = getSafeIsoDateStr(today);
    const startOfMonth = new Date(today.getFullYear(), today.getMonth(), 1);
    const oneYearAgo = new Date();
    oneYearAgo.setFullYear(today.getFullYear() - 1);

    const activeFaturalar = faturalar.filter(f => !f.isDeleted);
    const activeCariler = cariler.filter(c => !c.isDeleted);
    const activeStoklar = stoklar.filter(s => !s.isDeleted);

    const gunlukCiro = activeFaturalar
      .filter(f => {
        const dStr = getSafeIsoDateStr(f.tarih || f.Tarih);
        return dStr === todayStr && (f.tur === 'Satış' || f.tur === 'Satis' || f.Tur === 'Satış' || f.Tur === 'Satis');
      })
      .reduce((sum, f) => sum + (f.genelToplam || f.GenelToplam || 0), 0);

    const toplamBorc = activeCariler
      .filter(c => (c.alacak || c.Alacak || 0) > (c.borc || c.Borc || 0))
      .reduce((sum, c) => sum + ((c.alacak || c.Alacak || 0) - (c.borc || c.Borc || 0)), 0);

    const toplamAlacak = activeCariler
      .filter(c => (c.borc || c.Borc || 0) > (c.alacak || c.Alacak || 0))
      .reduce((sum, c) => sum + ((c.borc || c.Borc || 0) - (c.alacak || c.Alacak || 0)), 0);

    const toplamMaliyet = activeStoklar
      .reduce((sum, s) => sum + (s.miktar || s.Miktar || 0) * (s.alisFiyati || s.AlisFiyati || 0), 0);

    const yearlyPurchase = activeFaturalar
      .filter(f => {
        const d = parseSafeDate(f.tarih || f.Tarih);
        return d && d >= oneYearAgo && (f.tur === 'Alış' || f.tur === 'Alis' || f.Tur === 'Alış' || f.Tur === 'Alis');
      })
      .reduce((sum, f) => sum + (f.genelToplam || f.GenelToplam || 0), 0);

    const stokDevirHizi = toplamMaliyet > 0 ? parseFloat((yearlyPurchase / toplamMaliyet).toFixed(2)) : 0;

    const yearlySales = activeFaturalar
      .filter(f => {
        const d = parseSafeDate(f.tarih || f.Tarih);
        return d && d >= oneYearAgo && (f.tur === 'Satış' || f.tur === 'Satis' || f.Tur === 'Satış' || f.Tur === 'Satis');
      })
      .reduce((sum, f) => sum + (f.genelToplam || f.GenelToplam || 0), 0);
    const dso = yearlySales > 0 ? Math.round((toplamAlacak / yearlySales) * 365) : 0;

    const aylikCiro = activeFaturalar
      .filter(f => {
        const d = parseSafeDate(f.tarih || f.Tarih);
        return d && d >= startOfMonth && (f.tur === 'Satış' || f.tur === 'Satis' || f.Tur === 'Satış' || f.Tur === 'Satis');
      })
      .reduce((sum, f) => sum + (f.genelToplam || f.GenelToplam || 0), 0);

    const aylikAlis = activeFaturalar
      .filter(f => {
        const d = parseSafeDate(f.tarih || f.Tarih);
        return d && d >= startOfMonth && (f.tur === 'Alış' || f.tur === 'Alis' || f.Tur === 'Alış' || f.Tur === 'Alis');
      })
      .reduce((sum, f) => sum + (f.genelToplam || f.GenelToplam || 0), 0);

    const karlilik = aylikCiro - aylikAlis;
    const karlilikOrani = aylikCiro > 0 ? parseFloat(((karlilik / aylikCiro) * 100).toFixed(2)) : (aylikAlis > 0 ? -100 : 0);

    return {
      gunlukCiro, toplamBorc, toplamAlacak, toplamMaliyet,
      stokDevirHizi, dso, karlilik, karlilikOrani
    };
  };

  const stats = getStats();

  // Masaüstü (DashboardView.axaml) ile birebir aynı: Net Bakiye Durumu (ToplamAlacak - ToplamBorc)
  const netBakiye = stats.toplamAlacak - stats.toplamBorc;
  const isNetPozitif = netBakiye >= 0;
  const netColor = isNetPozitif ? '#30D158' : '#FF453A';
  const netFormatted = `${isNetPozitif ? '+' : '-'}${formatMoney(Math.abs(netBakiye))}`;
  const netSubtitle = isNetPozitif ? 'Alacak Fazlası' : 'Borç Fazlası';

  const mainStats = [
    { title: 'GÜNLÜK CİRO', value: formatMoney(stats.gunlukCiro), subtitle: 'Güncel Toplam', icon: <Banknote color="#FF9F0A" size={18} />, color: '#FF9F0A', valueColor: '#FFFFFF' },
    { title: 'TOPLAM ALACAK', value: formatMoney(stats.toplamAlacak), subtitle: 'Güncel Toplam', icon: <TrendingUp color="#30D158" size={18} />, color: '#30D158', valueColor: '#FFFFFF' },
    { title: 'TOPLAM BORÇ', value: formatMoney(stats.toplamBorc), subtitle: 'Güncel Toplam', icon: <TrendingDown color="#FF453A" size={18} />, color: '#FF453A', valueColor: '#FFFFFF' },
    { title: 'NET', value: netFormatted, subtitle: netSubtitle, icon: <Wallet color={netColor} size={18} />, color: netColor, valueColor: netColor }
  ];

  const advancedStats = [
    { title: 'KÂRLILIK (BU AY)', value: formatMoney(stats.karlilik), icon: <Activity color="#30D158" size={18} />, color: '#30D158' },
    { title: 'KÂR MARJI', value: `%${stats.karlilikOrani}`, icon: <PieChart color="#BF5AF2" size={18} />, color: '#BF5AF2' },
    { title: 'TAHSİLAT SÜRESİ (DSO)', value: `${stats.dso} Gün`, icon: <Clock color="#FF9F0A" size={18} />, color: '#FF9F0A' },
    { title: 'STOK DEVİR HIZI', value: `${stats.stokDevirHizi}x`, icon: <RefreshCw color="#FF375F" size={18} />, color: '#FF375F' }
  ];

  const getRecentTransactions = () => {
    const list: any[] = [];
    faturalar.filter(f => !f.isDeleted).forEach(f => {
      list.push({
        title: f.cariUnvan || 'Bilinmeyen Cari',
        description: `${f.tur} Faturası`,
        amount: f.genelToplam || 0,
        date: f.tarih,
        type: f.tur === 'Satış' || f.tur === 'Satis' ? 'In' : 'Out'
      });
    });
    kasaHareketler.forEach(k => {
      if (k.islemTuru === 'Tahsilat' || k.islemTuru === 'Ödeme') {
        list.push({
          title: k.cariUnvan || k.aciklama || 'Kasa İşlemi',
          description: k.islemTuru,
          amount: k.islemTuru === 'Tahsilat' ? (k.giren || 0) : (k.cikan || 0),
          date: k.tarih,
          type: k.islemTuru === 'Tahsilat' ? 'In' : 'Out'
        });
      }
    });
    bankaHareketler.forEach(b => {
      if (b.islemTuru === 'Tahsilat' || b.islemTuru === 'Ödeme') {
        list.push({
          title: b.cariUnvan || b.aciklama || 'Banka İşlemi',
          description: `Banka: ${b.islemTuru}`,
          amount: b.islemTuru === 'Tahsilat' ? (b.giren || 0) : (b.cikan || 0),
          date: b.tarih,
          type: b.islemTuru === 'Tahsilat' ? 'In' : 'Out'
        });
      }
    });
    return list.sort((a, b) => {
      const timeB = parseSafeDate(b.date)?.getTime() || 0;
      const timeA = parseSafeDate(a.date)?.getTime() || 0;
      return timeB - timeA;
    }).slice(0, 5);
  };

  // En eski vadesi geçmiş açık faturaya göre gün farkı (masaüstü OrtalamaVade paritesi)
  const getVadeGun = (cariUnvan: string, tur: string) => {
    const todayMs = new Date().setHours(0, 0, 0, 0);
    const vadeFaturalar = faturalar
      .filter(f => !f.isDeleted && f.cariUnvan === cariUnvan && f.vadeTarihi && (f.tur === tur || f.tur === tur.replace('ış', 'is').replace('ş', 's')))
      .map(f => {
        const vDate = parseSafeDate(f.vadeTarihi);
        return {
          vade: vDate ? vDate.setHours(0, 0, 0, 0) : 0,
          odenen: f.odenen || 0,
          toplam: f.genelToplam || 0
        };
      })
      .filter(f => f.vade > 0 && f.odenen < f.toplam);
    if (vadeFaturalar.length === 0) return 0;
    const enEski = Math.min(...vadeFaturalar.map(f => f.vade));
    return Math.round((enEski - todayMs) / 86400000);
  };

  const getReceivables = () => {
    return cariler.filter(c => !c.isDeleted && c.borc > c.alacak).map(c => ({
      title: c.unvan,
      description: 'Müşteri',
      amount: ((c.borc || 0) - (c.alacak || 0)),
      date: 'Alacak',
      vade: getVadeGun(c.unvan, 'Satış'),
      type: 'In'
    })).sort((a, b) => b.amount - a.amount).slice(0, 5);
  };

  const getPayables = () => {
    return cariler.filter(c => !c.isDeleted && c.alacak > c.borc).map(c => ({
      title: c.unvan,
      description: 'Tedarikçi',
      amount: c.alacak - c.borc,
      date: 'Borç',
      vade: getVadeGun(c.unvan, 'Alış'),
      type: 'Out'
    })).sort((a, b) => b.amount - a.amount).slice(0, 5);
  };

  let listData = [];
  if (activeTab === 'recents') listData = getRecentTransactions();
  if (activeTab === 'receivables') listData = getReceivables();
  if (activeTab === 'payables') listData = getPayables();

  // Harman Model 2: Apple Tarzı Radyal Başarı Halkası + Türkiye Muhasebe Standardı 12 Aylık Çift Kolon Barları
  const renderGoalChart = () => {
    const currentYear = new Date().getFullYear();
    const months = ["Oca", "Şub", "Mar", "Nis", "May", "Haz", "Tem", "Ağu", "Eyl", "Eki", "Kas", "Ara"];
    
    const chartData = months.map((mLabel, idx) => {
      const monthNum = idx + 1;
      const targetObj = aylikHedefler.find(h => h.yil === currentYear && Number(h.ay) === monthNum)
        || aylikHedefler.find(h => h.yil === currentYear || h.id === 1); // fallback: yıllık veya legacy ID 1
      const target = targetObj ? (Number(targetObj.hedefTutari ?? targetObj.tutar) || 250000) : 250000;
      
      const actual = faturalar
        .filter(f => !f.isDeleted && (f.tur === 'Satış' || f.tur === 'Satis'))
        .filter(f => {
          const fd = parseSafeDate(f.tarih);
          return fd ? (fd.getMonth() === idx && fd.getFullYear() === currentYear) : false;
        })
        .reduce((sum, f) => sum + (f.genelToplam || 0), 0);

      return { label: mLabel, target, actual };
    });

    const annualTarget = chartData.reduce((sum, d) => sum + d.target, 0);
    const annualActual = chartData.reduce((sum, d) => sum + d.actual, 0);
    const annualRate = annualTarget > 0 ? Math.min(100, Math.round((annualActual / annualTarget) * 100)) : 0;
    const remainingTarget = Math.max(0, annualTarget - annualActual);

    // Apple Radial Ring ölçüleri (animasyonlu)
    const ringRadius = 38;
    const ringStroke = 8;
    const ringCircumference = 2 * Math.PI * ringRadius;
    const currentRate = annualRate * chartAnim;
    const ringOffset = ringCircumference - (ringCircumference * (Math.min(100, Math.max(0, currentRate)) / 100));

    // 12 Aylık Çift Kolon Bar Grafiği ölçüleri
    const maxVal = Math.max(...chartData.map(d => Math.max(d.target, d.actual)), 100000);
    const chartHeight = 140;
    const chartWidth = 330;
    const paddingLeft = 38;
    const paddingRight = 10;
    const paddingTop = 12;
    const paddingBottom = 22;
    const availableHeight = chartHeight - paddingTop - paddingBottom;
    const colGroupWidth = (chartWidth - paddingLeft - paddingRight) / 12;
    const barW = Math.max(5, colGroupWidth * 0.35);

    const yTicks = [0, maxVal / 2, maxVal];

    return (
      <View style={styles.chartCard}>
        {/* Üst Başlık & Lejant (Taşmayan, Mobil Uyumlu Düzen) */}
        <View style={styles.chartHeaderWrapper}>
          <View style={{ flex: 1, paddingRight: 6 }}>
            <Text style={styles.chartTitle} numberOfLines={1}>HEDEF & GERÇEKLEŞME</Text>
            <Text style={styles.chartSubtitle}>12 Aylık Satış ve Hedef Performansı</Text>
          </View>
          <View style={styles.chartLegendRow}>
            <View style={styles.legendItem}>
              <View style={[styles.legendIndicator, { backgroundColor: '#6366F1' }]} />
              <Text style={styles.legendText}>Hedef</Text>
            </View>
            <View style={styles.legendItem}>
              <View style={[styles.legendIndicator, { backgroundColor: '#00FF87' }]} />
              <Text style={styles.legendText}>Satış</Text>
            </View>
          </View>
        </View>

        {/* Üst Kısım: Apple Tarzı Radyal Halka + 3'lü İstatistik Kutusu */}
        <View style={styles.harmanTopRow}>
          {/* Sol: Apple Dairesel Progress Ring (Animasyonlu) */}
          <View style={styles.radialRingContainer}>
            <Svg width={96} height={96} viewBox="0 0 96 96">
              <Defs>
                <LinearGradient id="ringGrad" x1="0" y1="0" x2="1" y2="1">
                  <Stop offset="0%" stopColor="#00FF87" stopOpacity="1" />
                  <Stop offset="100%" stopColor="#06B6D4" stopOpacity="1" />
                </LinearGradient>
              </Defs>
              <G rotation="-90" origin="48, 48">
                {/* Arka plan çemberi */}
                <Circle
                  cx="48"
                  cy="48"
                  r={ringRadius}
                  stroke="rgba(255, 255, 255, 0.08)"
                  strokeWidth={ringStroke}
                  fill="none"
                />
                {/* İlerleme çemberi */}
                <Circle
                  cx="48"
                  cy="48"
                  r={ringRadius}
                  stroke="url(#ringGrad)"
                  strokeWidth={ringStroke}
                  strokeDasharray={`${ringCircumference}`}
                  strokeDashoffset={ringOffset}
                  strokeLinecap="round"
                  fill="none"
                />
              </G>
            </Svg>
            <View style={styles.ringCenterTextContainer}>
              <Text style={styles.ringPercentText}>%{Math.round(currentRate)}</Text>
              <Text style={styles.ringLabelText}>BAŞARI</Text>
            </View>
          </View>

          {/* Sağ: 3 Mini İstatistik Kartı */}
          <View style={styles.harmanStatsColumn}>
            <View style={[styles.harmanStatBadge, { borderLeftColor: '#6366F1' }]}>
              <Text style={styles.harmanStatLabel}>🎯 Yıllık Hedef</Text>
              <Text style={styles.harmanStatValue}>{formatMoney(annualTarget)}</Text>
            </View>
            <View style={[styles.harmanStatBadge, { borderLeftColor: '#00FF87' }]}>
              <Text style={styles.harmanStatLabel}>🚀 Gerçekleşen Satış</Text>
              <Text style={[styles.harmanStatValue, { color: '#00FF87' }]}>{formatMoney(annualActual)}</Text>
            </View>
            <View style={[styles.harmanStatBadge, { borderLeftColor: '#F59E0B' }]}>
              <Text style={styles.harmanStatLabel}>⏳ Kalan Hedef</Text>
              <Text style={[styles.harmanStatValue, { color: '#FCD34D' }]}>{formatMoney(remainingTarget)}</Text>
            </View>
          </View>
        </View>

        {/* Alt Kısım: Türkiye Muhasebe Standardı 12 Aylık Çift Kolon Barları (Tabandan Yukarı Animasyonlu) */}
        <View style={{ height: chartHeight, width: '100%', marginTop: 14 }} pointerEvents="none">
          <Svg height="100%" width="100%" viewBox={`0 0 ${chartWidth} ${chartHeight}`}>
            <Defs>
              <LinearGradient id="barTargetGrad" x1="0" y1="0" x2="0" y2="1">
                <Stop offset="0%" stopColor="#818CF8" stopOpacity="0.9" />
                <Stop offset="100%" stopColor="#4F46E5" stopOpacity="0.6" />
              </LinearGradient>
              <LinearGradient id="barActualGrad" x1="0" y1="0" x2="0" y2="1">
                <Stop offset="0%" stopColor="#00FF87" stopOpacity="1" />
                <Stop offset="100%" stopColor="#06B6D4" stopOpacity="0.75" />
              </LinearGradient>
            </Defs>

            {/* Yatay Grid ve Sol Değer Etiketleri */}
            {yTicks.map((t, idx) => {
              const y = chartHeight - paddingBottom - (t / maxVal) * availableHeight;
              return (
                <React.Fragment key={idx}>
                  <Line x1={paddingLeft} y1={y} x2={chartWidth - paddingRight} y2={y} stroke="rgba(255,255,255,0.06)" strokeWidth={1} strokeDasharray="3,3" />
                  <SvgText x={paddingLeft - 6} y={y + 3} fill="#64748B" fontSize={8} fontWeight="600" textAnchor="end">
                    {t >= 1000000 ? `${(t / 1000000).toFixed(1)}M` : t >= 1000 ? `${(t / 1000).toFixed(0)}k` : t.toFixed(0)}
                  </SvgText>
                </React.Fragment>
              );
            })}

            {/* 12 Ay Çift Kolon Barları ve X Etiketleri */}
            {chartData.map((d, i) => {
              const groupX = paddingLeft + i * colGroupWidth;
              const targetH = Math.max(2, (d.target / maxVal) * availableHeight * chartAnim);
              const actualH = Math.max(2, (d.actual / maxVal) * availableHeight * chartAnim);
              const targetY = chartHeight - paddingBottom - targetH;
              const actualY = chartHeight - paddingBottom - actualH;
              const targetBarX = groupX + 1;
              const actualBarX = groupX + barW + 2;

              return (
                <React.Fragment key={i}>
                  {/* Hedef Barı (Sol kolon) */}
                  <Rect
                    x={targetBarX}
                    y={targetY}
                    width={barW}
                    height={targetH}
                    rx={2.5}
                    ry={2.5}
                    fill="url(#barTargetGrad)"
                  />
                  {/* Gerçekleşen Barı (Sağ kolon) */}
                  <Rect
                    x={actualBarX}
                    y={actualY}
                    width={barW}
                    height={actualH}
                    rx={2.5}
                    ry={2.5}
                    fill="url(#barActualGrad)"
                  />
                  {/* Ay Etiketi */}
                  <SvgText
                    x={groupX + barW + 1}
                    y={chartHeight - 6}
                    fill="#94A3B8"
                    fontSize={8.5}
                    fontWeight="600"
                    textAnchor="middle"
                  >
                    {d.label}
                  </SvgText>
                </React.Fragment>
              );
            })}
          </Svg>
        </View>
      </View>
    );
  };
  return (
    <SafeAreaView style={styles.container}>
      <StatusBar barStyle="light-content" />
      <ScrollView contentContainerStyle={styles.scrollContent} showsVerticalScrollIndicator={false}>
        
        {/* Header */}
        <View style={styles.headerContainer}>
          <View>
            <Text style={[styles.headerTitle, { fontSize: scaleFont(24, uiScale) }]}>Gösterge Paneli</Text>
            <Text style={[styles.headerSubtitle, { fontSize: scaleFont(13, uiScale) }]}>Masaüstü sürümü ile anlık senkronize</Text>
          </View>
        </View>

        {loading ? (
          <View style={{ paddingHorizontal: 16, gap: 12, marginVertical: 20 }}>
            <ShimmerItem height={140} borderRadius={18} />
            <View style={{ flexDirection: 'row', gap: 12 }}>
              <ShimmerItem width="48%" height={100} borderRadius={18} />
              <ShimmerItem width="48%" height={100} borderRadius={18} />
            </View>
            <ShimmerItem height={180} borderRadius={18} />
          </View>
        ) : (
          <>
            {/* Masaüstü ile Birebir 4'lü Dashboard Kartları */}
            <View style={styles.gridContainer}>
              {mainStats.map((stat, idx) => (
                <View key={idx} style={[styles.card, stat.title === 'NET' && { borderColor: `${stat.color}40`, backgroundColor: '#131419' }]}>
                  <View style={styles.cardHeader}>
                    <Text style={[styles.cardTitle, stat.title === 'NET' && { color: stat.color }]}>{stat.title}</Text>
                    <View style={[styles.iconContainer, { backgroundColor: `${stat.color}15` }]}>
                      {stat.icon}
                    </View>
                  </View>
                  <View style={styles.cardBody}>
                    <Text style={[styles.cardValue, stat.valueColor ? { color: stat.valueColor } : {}]}>{stat.value}</Text>
                    <Text style={[styles.cardValueLabel, stat.title === 'NET' && { color: stat.color, opacity: 0.85 }]}>{stat.subtitle}</Text>
                  </View>
                </View>
              ))}
            </View>

            {/* Quick Actions */}
            <View style={styles.sectionCard}>
              <Text style={styles.sectionTitle}>HIZLI İŞLEMLER</Text>
              <View style={styles.quickActionRow}>
                <TouchableOpacity style={styles.quickActionButton} onPress={() => navigation.navigate('FaturaForm', { initialTur: 'Alış' })} activeOpacity={0.7}>
                  <View style={[styles.actionIconBox, { backgroundColor: 'rgba(255, 159, 10, 0.12)' }]}>
                    <FilePlus color="#FF9F0A" size={20} />
                  </View>
                  <Text style={styles.quickActionText}>Alış Faturası</Text>
                </TouchableOpacity>
                <TouchableOpacity style={styles.quickActionButton} onPress={() => navigation.navigate('FaturaForm', { initialTur: 'Satış' })} activeOpacity={0.7}>
                  <View style={[styles.actionIconBox, { backgroundColor: 'rgba(48, 209, 88, 0.12)' }]}>
                    <FileSpreadsheet color="#30D158" size={20} />
                  </View>
                  <Text style={styles.quickActionText}>Satış Faturası</Text>
                </TouchableOpacity>
                <TouchableOpacity style={styles.quickActionButton} onPress={() => navigation.navigate('SiparisForm')} activeOpacity={0.7}>
                  <View style={[styles.actionIconBox, { backgroundColor: 'rgba(10, 132, 255, 0.12)' }]}>
                    <ShoppingBag color="#0A84FF" size={20} />
                  </View>
                  <Text style={styles.quickActionText}>Yeni Sipariş</Text>
                </TouchableOpacity>
                <TouchableOpacity style={styles.quickActionButton} onPress={() => navigation.navigate('TeklifForm')} activeOpacity={0.7}>
                  <View style={[styles.actionIconBox, { backgroundColor: 'rgba(191, 90, 242, 0.12)' }]}>
                    <PlusCircle color="#BF5AF2" size={20} />
                  </View>
                  <Text style={styles.quickActionText}>Yeni Teklif</Text>
                </TouchableOpacity>
              </View>
            </View>

            {/* SVG Ciro Hedef Grafiği */}
            {renderGoalChart()}

            {/* Dynamic Lists */}
            <View style={styles.sectionCard}>
              <View style={styles.tabContainer}>
                <TouchableOpacity 
                  style={[styles.tabButton, activeTab === 'recents' && styles.tabButtonActive]}
                  onPress={() => setActiveTab('recents')}
                >
                  <Text style={[styles.tabText, activeTab === 'recents' && styles.tabTextActive]}>Son İşlemler</Text>
                </TouchableOpacity>
                <TouchableOpacity 
                  style={[styles.tabButton, activeTab === 'receivables' && styles.tabButtonActive]}
                  onPress={() => setActiveTab('receivables')}
                >
                  <Text style={[styles.tabText, activeTab === 'receivables' && styles.tabTextActive]}>Alacak Takibi</Text>
                </TouchableOpacity>
                <TouchableOpacity 
                  style={[styles.tabButton, activeTab === 'payables' && styles.tabButtonActive]}
                  onPress={() => setActiveTab('payables')}
                >
                  <Text style={[styles.tabText, activeTab === 'payables' && styles.tabTextActive]}>Borç Takibi</Text>
                </TouchableOpacity>
              </View>

              <View style={styles.listContainer}>
                {listData.length === 0 ? (
                  <Text style={{ color: '#64748B', textAlign: 'center', padding: 20 }}>Kayıt bulunamadı.</Text>
                ) : (
                  listData.map((tx, idx) => (
                    <View key={idx} style={[styles.listItem, idx === listData.length - 1 && { borderBottomWidth: 0 }]}>
                      <View style={styles.listItemLeft}>
                        <View style={[styles.listIcon, { backgroundColor: tx.type === 'In' ? '#00FF8715' : '#FF416C15' }]}>
                          <Text style={{ color: tx.type === 'In' ? '#00FF87' : '#FF416C', fontWeight: 'bold' }}>
                            {tx.type === 'In' ? 'G' : 'Ç'}
                          </Text>
                        </View>
                        <View>
                          <Text style={styles.listItemTitle}>{tx.title}</Text>
                          <Text style={styles.listItemSubtitle}>{tx.description} • {tx.date}{tx.vade !== undefined ? ` • ${tx.vade} G` : ''}</Text>
                        </View>
                      </View>
                      <Text style={[styles.listItemAmount, { color: tx.type === 'In' ? '#00FF87' : '#FF416C' }]}>
                        {tx.type === 'In' ? '+' : '-'}{formatMoney(tx.amount)}
                      </Text>
                    </View>
                  ))
                )}
              </View>
            </View>
          </>
        )}
        <View style={{ height: 110 }} />
      </ScrollView>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#000000',
  },
  scrollContent: {
    padding: 16,
    paddingTop: Platform.OS === 'android' ? 36 : 16,
  },
  headerContainer: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 20,
    paddingHorizontal: 4,
  },
  headerTitle: {
    fontSize: 24,
    fontWeight: '900',
    color: '#FFFFFF',
    letterSpacing: -0.5,
  },
  headerSubtitle: {
    fontSize: 12,
    color: '#8E8E93',
    fontWeight: '500',
    marginTop: 4,
  },
  gridContainer: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    justifyContent: 'space-between',
    marginBottom: 14,
  },
  card: {
    width: '48%',
    backgroundColor: '#121216',
    borderColor: 'rgba(255, 255, 255, 0.08)',
    borderWidth: 1,
    borderRadius: 18,
    padding: 14,
    marginBottom: 12,
    justifyContent: 'space-between',
    minHeight: 110,
  },
  cardHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  cardTitle: {
    fontSize: 10,
    fontWeight: 'bold',
    color: '#8E8E93',
  },
  iconContainer: {
    width: 32,
    height: 32,
    borderRadius: 10,
    alignItems: 'center',
    justifyContent: 'center',
  },
  cardBody: {
    marginTop: 12,
  },
  cardValue: {
    fontSize: 18,
    fontWeight: '900',
    color: '#FFFFFF',
    letterSpacing: -0.5,
  },
  cardValueLabel: {
    fontSize: 10,
    color: '#636366',
    fontWeight: '500',
    marginTop: 4,
  },
  sectionCard: {
    backgroundColor: '#121216',
    borderColor: 'rgba(255, 255, 255, 0.08)',
    borderWidth: 1,
    borderRadius: 20,
    padding: 16,
    marginBottom: 20,
  },
  sectionTitle: {
    fontSize: 11,
    fontWeight: 'bold',
    color: '#8E8E93',
    letterSpacing: 1,
    marginBottom: 16,
  },
  quickActionRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
  },
  quickActionButton: {
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: 'rgba(255, 255, 255, 0.035)',
    borderColor: 'rgba(255, 255, 255, 0.07)',
    borderWidth: 1,
    borderRadius: 16,
    paddingVertical: 12,
    paddingHorizontal: 4,
    width: '23%',
    minHeight: 88,
  },
  actionIconBox: {
    width: 40,
    height: 40,
    borderRadius: 12,
    alignItems: 'center',
    justifyContent: 'center',
    marginBottom: 6,
  },
  quickActionText: {
    fontSize: 10.5,
    fontWeight: '600',
    color: '#E2E8F0',
    textAlign: 'center',
    lineHeight: 13,
  },
  chartCard: {
    backgroundColor: '#121216',
    borderColor: 'rgba(255, 255, 255, 0.08)',
    borderWidth: 1,
    borderRadius: 20,
    padding: 16,
    marginBottom: 20,
  },
  chartHeaderWrapper: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 14,
  },
  chartTitle: {
    fontSize: 12,
    fontWeight: '800',
    color: '#F1F5F9',
    letterSpacing: 0.5,
  },
  chartSubtitle: {
    color: '#64748B',
    fontSize: 10,
    marginTop: 2,
    fontWeight: '500',
  },
  chartLegendRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 10,
  },
  chartLegend: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: 8,
  },
  legendItem: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  legendIndicator: {
    width: 8,
    height: 8,
    borderRadius: 4,
    marginRight: 5,
  },
  legendText: {
    color: '#CBD5E1',
    fontSize: 10,
    fontWeight: 'bold',
  },
  tabContainer: {
    flexDirection: 'row',
    backgroundColor: 'rgba(255, 255, 255, 0.04)',
    borderColor: 'rgba(255, 255, 255, 0.08)',
    borderWidth: 1,
    borderRadius: 12,
    padding: 4,
    marginBottom: 16,
  },
  tabButton: {
    flex: 1,
    paddingVertical: 8,
    alignItems: 'center',
    borderRadius: 8,
  },
  tabButtonActive: {
    backgroundColor: '#0061FF',
  },
  tabText: {
    fontSize: 11,
    fontWeight: 'bold',
    color: '#94A3B8',
  },
  tabTextActive: {
    color: '#FFFFFF',
  },
  listContainer: {
    marginTop: 8,
  },
  listItem: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingVertical: 12,
    borderBottomWidth: 1,
    borderBottomColor: 'rgba(255, 255, 255, 0.05)',
  },
  listItemLeft: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  listIcon: {
    width: 36,
    height: 36,
    borderRadius: 10,
    alignItems: 'center',
    justifyContent: 'center',
    marginRight: 12,
  },
  listItemTitle: {
    fontSize: 13,
    fontWeight: '900',
    color: '#FFFFFF',
  },
  listItemSubtitle: {
    fontSize: 10,
    color: '#64748B',
    marginTop: 4,
  },
  listItemAmount: {
    fontSize: 13,
    fontWeight: '900',
  },
  harmanTopRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    backgroundColor: 'rgba(255, 255, 255, 0.025)',
    borderRadius: 16,
    padding: 12,
    marginTop: 4,
  },
  radialRingContainer: {
    width: 96,
    height: 96,
    position: 'relative',
    alignItems: 'center',
    justifyContent: 'center',
  },
  ringCenterTextContainer: {
    position: 'absolute',
    alignItems: 'center',
    justifyContent: 'center',
  },
  ringPercentText: {
    color: '#FFFFFF',
    fontSize: 16,
    fontWeight: '900',
    letterSpacing: -0.5,
  },
  ringLabelText: {
    color: '#00FF87',
    fontSize: 8,
    fontWeight: '800',
    letterSpacing: 0.8,
    marginTop: 1,
  },
  harmanStatsColumn: {
    flex: 1,
    marginLeft: 14,
    gap: 6,
  },
  harmanStatBadge: {
    backgroundColor: 'rgba(255, 255, 255, 0.04)',
    borderLeftWidth: 3,
    borderRadius: 8,
    paddingVertical: 5,
    paddingHorizontal: 8,
  },
  harmanStatLabel: {
    color: '#94A3B8',
    fontSize: 9.5,
    fontWeight: '600',
  },
  harmanStatValue: {
    color: '#FFFFFF',
    fontSize: 11.5,
    fontWeight: '800',
    marginTop: 1,
  }
});
