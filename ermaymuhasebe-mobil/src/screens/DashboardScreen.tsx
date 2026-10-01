import React, { useState, useEffect } from 'react';
import { StyleSheet, Text, View, ScrollView, TouchableOpacity, StatusBar, Platform, ActivityIndicator } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { subscribeToPath } from '../services/firebase';
import { useNavigation } from '@react-navigation/native';
import Svg, { Path, Line, Rect, Text as SvgText, Defs, LinearGradient, Stop } from 'react-native-svg';
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
  FileSpreadsheet
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

  const mainStats = [
    { title: 'GÜNLÜK CİRO', value: formatMoney(stats.gunlukCiro), icon: <Banknote color="#FF9F0A" size={18} />, color: '#FF9F0A' },
    { title: 'TOPLAM ALACAK', value: formatMoney(stats.toplamAlacak), icon: <TrendingUp color="#30D158" size={18} />, color: '#30D158' },
    { title: 'TOPLAM BORÇ', value: formatMoney(stats.toplamBorc), icon: <TrendingDown color="#FF453A" size={18} />, color: '#FF453A' },
    { title: 'MALİYET', value: formatMoney(stats.toplamMaliyet), icon: <Package color="#64D2FF" size={18} />, color: '#64D2FF' }
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

  // 12 Aylık Hedef vs Gerçekleşen Satış Grafiği (Masaüstü GoalTrackingChart Paritesi)
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

    const maxVal = Math.max(...chartData.map(d => Math.max(d.target, d.actual)), 100000);
    const chartHeight = 120;
    const chartWidth = 330;
    const paddingLeft = 40;
    const paddingRight = 10;
    const paddingTop = 15;
    const paddingBottom = 20;

    const stepX = (chartWidth - paddingLeft - paddingRight) / 11;
    const getCoords = (val: number, index: number) => {
      const x = paddingLeft + index * stepX;
      const activeHeight = chartHeight - paddingTop - paddingBottom;
      const y = chartHeight - paddingBottom - (val / maxVal) * activeHeight;
      return { x, y };
    };

    let targetPath = '';
    let actualPath = '';
    let targetArea = '';
    let actualArea = '';

    chartData.forEach((d, i) => {
      const pT = getCoords(d.target, i);
      const pA = getCoords(d.actual, i);

      if (i === 0) {
        targetPath = `M ${pT.x} ${pT.y}`;
        actualPath = `M ${pA.x} ${pA.y}`;
        targetArea = `M ${pT.x} ${chartHeight - paddingBottom} L ${pT.x} ${pT.y}`;
        actualArea = `M ${pA.x} ${chartHeight - paddingBottom} L ${pA.x} ${pA.y}`;
      } else {
        targetPath += ` L ${pT.x} ${pT.y}`;
        actualPath += ` L ${pA.x} ${pA.y}`;
        targetArea += ` L ${pT.x} ${pT.y}`;
        actualArea += ` L ${pA.x} ${pA.y}`;
      }

      if (i === 11) {
        targetArea += ` L ${pT.x} ${chartHeight - paddingBottom} Z`;
        actualArea += ` L ${pA.x} ${chartHeight - paddingBottom} Z`;
      }
    });

    const yTicks = [0, maxVal / 2, maxVal];

    return (
      <View style={styles.chartCard}>
        <Text style={styles.chartTitle}>Aylık Tahsilat Performansı</Text>
        <View style={styles.chartLegend}>
          <View style={styles.legendItem}>
            <View style={[styles.legendIndicator, { backgroundColor: '#0061FF' }]} />
            <Text style={styles.legendText}>Aylık Hedef</Text>
          </View>
          <View style={styles.legendItem}>
            <View style={[styles.legendIndicator, { backgroundColor: '#00FF87' }]} />
            <Text style={styles.legendText}>Gerçekleşen Satış</Text>
          </View>
        </View>

        <View style={{ height: chartHeight, width: '100%', marginTop: 10 }} pointerEvents="none">
          <Svg height="100%" width="100%" viewBox={`0 0 ${chartWidth} ${chartHeight}`}>
            <Defs>
              <LinearGradient id="targetGrad" x1="0" y1="0" x2="0" y2="1">
                <Stop offset="0%" stopColor="#0061FF" stopOpacity="0.2" />
                <Stop offset="100%" stopColor="#0061FF" stopOpacity="0" />
              </LinearGradient>
              <LinearGradient id="actualGrad" x1="0" y1="0" x2="0" y2="1">
                <Stop offset="0%" stopColor="#00FF87" stopOpacity="0.2" />
                <Stop offset="100%" stopColor="#00FF87" stopOpacity="0" />
              </LinearGradient>
            </Defs>

            {/* Grid lines */}
            {yTicks.map((t, idx) => {
              const { y } = getCoords(t, 0);
              return (
                <React.Fragment key={idx}>
                  <Line x1={paddingLeft} y1={y} x2={chartWidth - paddingRight} y2={y} stroke="rgba(255,255,255,0.05)" strokeWidth={1} strokeDasharray="3,3" />
                  <SvgText x={paddingLeft - 8} y={y + 3} fill="#64748B" fontSize={8} fontWeight="bold" textAnchor="end">
                    {t >= 1000 ? `${(t / 1000).toFixed(0)}k` : t.toFixed(0)}
                  </SvgText>
                </React.Fragment>
              );
            })}

            {/* X Labels */}
            {chartData.map((d, i) => {
              const { x } = getCoords(0, i);
              return (
                <SvgText key={i} x={x} y={chartHeight - 4} fill="#64748B" fontSize={8} fontWeight="bold" textAnchor="middle">
                  {d.label}
                </SvgText>
              );
            })}

            {/* Areas */}
            <Path d={targetArea} fill="url(#targetGrad)" />
            <Path d={actualArea} fill="url(#actualGrad)" />

            {/* Lines */}
            <Path d={targetPath} fill="none" stroke="#0061FF" strokeWidth={2} />
            <Path d={actualPath} fill="none" stroke="#00FF87" strokeWidth={2} />
          </Svg>
        </View>
      </View>
    );
  };

  const renderHeatmap = () => {
    const days = [];
    for(let i = 29; i >= 0; i--) {
      const d = new Date();
      d.setDate(d.getDate() - i);
      days.push(d);
    }
    
    const activeFaturalar = faturalar.filter(f => !f.isDeleted);
    const activeKasa = kasaHareketler.filter(k => !k.isDeleted);
    
    let maxAbs = 1;
    const dayValues = days.map(d => {
      const dk = getSafeIsoDateStr(d);
      const satis = activeFaturalar.filter(f => (f.tur === 'Satış' || f.tur === 'Satis' || f.Tur === 'Satış') && getSafeIsoDateStr(f.tarih || f.Tarih) === dk).reduce((s, f) => s + (f.genelToplam || f.GenelToplam || 0), 0);
      const alis = activeFaturalar.filter(f => (f.tur === 'Alış' || f.tur === 'Alis' || f.Tur === 'Alış') && getSafeIsoDateStr(f.tarih || f.Tarih) === dk).reduce((s, f) => s + (f.genelToplam || f.GenelToplam || 0), 0);
      const kasaIn = activeKasa.filter(k => getSafeIsoDateStr(k.tarih || k.Tarih) === dk).reduce((s, k) => s + (k.giren || k.gelir || k.Giren || 0), 0);
      const kasaOut = activeKasa.filter(k => getSafeIsoDateStr(k.tarih || k.Tarih) === dk).reduce((s, k) => s + (k.cikan || k.gider || k.Cikan || 0), 0);
      
      const net = satis - alis + kasaIn - kasaOut;
      if (Math.abs(net) > maxAbs) maxAbs = Math.abs(net);
      return { date: d, net };
    });

    const boxSize = 14;
    
    return (
      <View style={styles.chartCard}>
        <Text style={styles.chartTitle}>Finansal Isı Haritası (Nakit Akışı)</Text>
        <Text style={{color: '#64748B', fontSize: 10, marginBottom: 12}}>Son 30 günün net nakit durumu</Text>
        <View style={{flexDirection: 'row', flexWrap: 'wrap', gap: 4, alignSelf: 'center', width: (boxSize + 4) * 10 }}>
          {dayValues.map((dv, i) => {
            let color = '#2A2A2A';
            if (dv.net > 0) {
              const intensity = Math.min(1, 0.3 + (dv.net / maxAbs) * 0.7);
              color = `rgba(0, 255, 135, ${intensity})`;
            } else if (dv.net < 0) {
              const intensity = Math.min(1, 0.3 + (Math.abs(dv.net) / maxAbs) * 0.7);
              color = `rgba(255, 65, 108, ${intensity})`;
            }
            
            return (
              <View key={i} style={{
                width: boxSize, height: boxSize, borderRadius: 3,
                backgroundColor: color, borderWidth: 0.5, borderColor: 'rgba(255,255,255,0.1)'
              }} />
            )
          })}
        </View>
        <View style={styles.chartLegend}>
          <View style={styles.legendItem}>
            <View style={[styles.legendIndicator, { backgroundColor: '#FF416C' }]} />
            <Text style={styles.legendText}>Gider</Text>
          </View>
          <View style={styles.legendItem}>
            <View style={[styles.legendIndicator, { backgroundColor: '#2A2A2A' }]} />
            <Text style={styles.legendText}>Durağan</Text>
          </View>
          <View style={styles.legendItem}>
            <View style={[styles.legendIndicator, { backgroundColor: '#00FF87' }]} />
            <Text style={styles.legendText}>Gelir</Text>
          </View>
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
                <View key={idx} style={styles.card}>
                  <View style={styles.cardHeader}>
                    <Text style={styles.cardTitle}>{stat.title}</Text>
                    <View style={[styles.iconContainer, { backgroundColor: `${stat.color}15` }]}>
                      {stat.icon}
                    </View>
                  </View>
                  <View style={styles.cardBody}>
                    <Text style={styles.cardValue}>{stat.value}</Text>
                    <Text style={styles.cardValueLabel}>Güncel Toplam</Text>
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

            {/* Finansal Isı Haritası */}
            {renderHeatmap()}

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
        <View style={{ height: 95 }} />
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
    backgroundColor: '#141416',
    borderColor: 'rgba(255, 255, 255, 0.08)',
    borderWidth: 1,
    borderRadius: 16,
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
    backgroundColor: '#141416',
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
    backgroundColor: 'rgba(255, 255, 255, 0.03)',
    borderColor: 'rgba(255, 255, 255, 0.06)',
    borderWidth: 1,
    borderRadius: 14,
    paddingVertical: 12,
    paddingHorizontal: 6,
    width: '23%',
  },
  actionIconBox: {
    width: 38,
    height: 38,
    borderRadius: 10,
    alignItems: 'center',
    justifyContent: 'center',
    marginBottom: 6,
  },
  quickActionText: {
    fontSize: 10,
    fontWeight: '600',
    color: '#E2E8F0',
    textAlign: 'center',
  },
  chartCard: {
    backgroundColor: '#141416',
    borderColor: 'rgba(255, 255, 255, 0.08)',
    borderWidth: 1,
    borderRadius: 20,
    padding: 16,
    marginBottom: 20,
  },
  chartTitle: {
    fontSize: 11,
    fontWeight: 'bold',
    color: '#94A3B8',
    letterSpacing: 1,
    marginBottom: 8,
  },
  chartLegend: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: 8,
  },
  legendItem: {
    flexDirection: 'row',
    alignItems: 'center',
    marginRight: 16,
  },
  legendIndicator: {
    width: 8,
    height: 8,
    borderRadius: 4,
    marginRight: 6,
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
  }
});
