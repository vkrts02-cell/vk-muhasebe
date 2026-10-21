import React from 'react';
import { StyleSheet, Text, View, SafeAreaView, TouchableOpacity, FlatList, Share } from 'react-native';
import { X, FileSpreadsheet, Share2 } from 'lucide-react-native';
import { useRoute, useNavigation } from '@react-navigation/native';
import { generateReportPdf } from '../services/pdfService';

export default function RaporDetayScreen() {
  const route = useRoute();
  const navigation = useNavigation();
  const { reportResult, pdfConfig } = route.params as any;

  const handleShareReport = async () => {
    if (!reportResult) return;
    await generatePdfAndShare();
  };

  const handleGetPdfReport = async () => {
    if (!reportResult) return;
    await generatePdfAndShare();
  };

  const generatePdfAndShare = async () => {
    try {
      const endpoint = pdfConfig?.endpoint || 'generic';
      const payload = pdfConfig?.payload || {
        title: reportResult.title,
        subtitle: '',
        headers: reportResult.headers,
        rows: reportResult.rows,
      };
      const reportFileName = reportResult.title.replace(/\s+/g, '_') + '.pdf';
      await generateReportPdf(endpoint, payload, reportFileName);
    } catch (err) {
      console.log('PDF Error:', err);
    }
  };

  const isHeatmap = reportResult.title?.includes('Isı Haritası') || reportResult.title?.includes('Heatmap');
  const [selectedDay, setSelectedDay] = React.useState<any | null>(null);

  // Heatmap KPI hesaplamaları
  const heatmapStats = React.useMemo(() => {
    if (!isHeatmap || !reportResult.rows) return null;
    let posCount = 0;
    let negCount = 0;
    let totalFlow = 0;

    const days = reportResult.rows.map((r: string[]) => {
      const dateStr = r[0] || '';
      const satisStr = r[1] || '0';
      const alisStr = r[2] || '0';
      const netStr = r[5] || '0';
      const isNeg = netStr.includes('-');
      const netVal = parseFloat(netStr.replace(/[^0-9.-]+/g, '')) * (isNeg ? -1 : 1) || 0;

      if (netVal > 0) posCount++;
      else if (netVal < 0) negCount++;
      totalFlow += netVal;

      return {
        date: dateStr,
        satis: satisStr,
        alis: alisStr,
        kasaIn: r[3] || '0',
        kasaOut: r[4] || '0',
        net: netStr,
        netVal,
        isNeg
      };
    });

    return { days, posCount, negCount, totalFlow };
  }, [isHeatmap, reportResult]);

  return (
    <SafeAreaView style={styles.container}>
      <View style={styles.modalContent}>
        <View style={styles.modalHeader}>
          <View style={{ flex: 1, marginRight: 12 }}>
            <Text style={styles.modalTitle} numberOfLines={1}>{reportResult.title}</Text>
            {reportResult.subtitle ? (
              <Text style={styles.modalSubtitle} numberOfLines={1}>📅 {reportResult.subtitle}</Text>
            ) : null}
          </View>
          <TouchableOpacity onPress={() => navigation.goBack()} hitSlop={{ top: 10, bottom: 10, left: 10, right: 10 }}>
            <X color="#FFF" size={24} />
          </TouchableOpacity>
        </View>

        <View style={{ flex: 1 }}>
          {isHeatmap && heatmapStats && (
            <View style={styles.heatmapContainer}>
              <View style={styles.kpiRow}>
                <View style={[styles.kpiCard, { borderColor: '#10B981' }]}>
                  <Text style={styles.kpiTitle}>Pozitif Günler</Text>
                  <Text style={[styles.kpiValue, { color: '#10B981' }]}>{heatmapStats.posCount} Gün</Text>
                </View>
                <View style={[styles.kpiCard, { borderColor: '#EF4444' }]}>
                  <Text style={styles.kpiTitle}>Negatif Günler</Text>
                  <Text style={[styles.kpiValue, { color: '#EF4444' }]}>{heatmapStats.negCount} Gün</Text>
                </View>
                <View style={[styles.kpiCard, { borderColor: '#3B82F6' }]}>
                  <Text style={styles.kpiTitle}>Net Nakit Akışı</Text>
                  <Text style={[styles.kpiValue, { color: heatmapStats.totalFlow >= 0 ? '#10B981' : '#EF4444' }]}>
                    {new Intl.NumberFormat('tr-TR', { style: 'currency', currency: 'TRY' }).format(heatmapStats.totalFlow)}
                  </Text>
                </View>
              </View>

              <Text style={styles.heatmapSectionTitle}>Son 30 Günlük Yoğunluk Hücreleri</Text>
              <View style={styles.heatmapGrid}>
                {heatmapStats.days.map((d: any, idx: number) => {
                  const isSelected = selectedDay?.date === d.date;
                  return (
                    <TouchableOpacity
                      key={idx}
                      onPress={() => setSelectedDay(d)}
                      style={[
                        styles.heatmapCell,
                        d.netVal > 0 ? styles.heatmapCellPos : d.netVal < 0 ? styles.heatmapCellNeg : styles.heatmapCellZero,
                        isSelected && styles.heatmapCellSelected
                      ]}
                    >
                      <Text style={styles.heatmapCellDay}>{d.date.split('.')[0]}</Text>
                    </TouchableOpacity>
                  );
                })}
              </View>

              {selectedDay && (
                <View style={styles.dayDetailCard}>
                  <Text style={styles.dayDetailDate}>📅 {selectedDay.date} Detayı</Text>
                  <View style={styles.dayDetailRow}>
                    <Text style={styles.dayDetailLabel}>Satış: <Text style={styles.dayDetailVal}>{selectedDay.satis}</Text></Text>
                    <Text style={styles.dayDetailLabel}>Alış: <Text style={styles.dayDetailVal}>{selectedDay.alis}</Text></Text>
                  </View>
                  <View style={styles.dayDetailRow}>
                    <Text style={styles.dayDetailLabel}>Giren Kasa: <Text style={[styles.dayDetailVal, { color: '#10B981' }]}>{selectedDay.kasaIn}</Text></Text>
                    <Text style={styles.dayDetailLabel}>Çıkan Kasa: <Text style={[styles.dayDetailVal, { color: '#EF4444' }]}>{selectedDay.kasaOut}</Text></Text>
                  </View>
                  <View style={[styles.dayDetailRow, { marginTop: 4, borderTopWidth: 1, borderTopColor: '#333', paddingTop: 4 }]}>
                    <Text style={{ color: '#94A3B8', fontWeight: '700' }}>Net Akış:</Text>
                    <Text style={{ fontWeight: '800', color: selectedDay.isNeg ? '#EF4444' : '#10B981' }}>{selectedDay.net}</Text>
                  </View>
                </View>
              )}
            </View>
          )}

          <View style={styles.tableHeaderRow}>
            {reportResult.headers.map((h: string, i: number) => (
              <Text key={i} style={styles.tableHeaderCell}>{h}</Text>
            ))}
          </View>

          <FlatList 
            initialNumToRender={20} 
            maxToRenderPerBatch={20} 
            windowSize={5} 
            data={reportResult.rows}
            keyExtractor={(_, idx) => idx.toString()}
            renderItem={({ item }) => (
              <View style={styles.tableRow}>
                {item.map((cell: string, idx: number) => (
                  <Text key={idx} style={styles.tableCell}>{cell}</Text>
                ))}
              </View>
            )}
          />

          <View style={{ flexDirection: 'row', gap: 10, marginTop: 16 }}>
            <TouchableOpacity style={[styles.shareBtn, { flex: 1, backgroundColor: '#10B981' }]} onPress={handleGetPdfReport}>
              <FileSpreadsheet color="#FFF" size={20} style={{ marginRight: 8 }} />
              <Text style={styles.shareBtnText}>PDF Rapor</Text>
            </TouchableOpacity>

            <TouchableOpacity style={[styles.shareBtn, { flex: 1 }]} onPress={handleShareReport}>
              <Share2 color="#FFF" size={20} style={{ marginRight: 8 }} />
              <Text style={styles.shareBtnText}>Paylaş</Text>
            </TouchableOpacity>
          </View>
        </View>
      </View>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#0A0A0A',
  },
  modalContent: {
    backgroundColor: '#0A0A0A',
    padding: 20,
    paddingBottom: 110,
    flex: 1,
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
    fontSize: 17,
    fontWeight: '700',
    letterSpacing: -0.3,
  },
  modalSubtitle: {
    color: '#38BDF8',
    fontSize: 11.5,
    fontWeight: '700',
    marginTop: 3,
    letterSpacing: 0.2,
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
  heatmapContainer: {
    backgroundColor: '#141414',
    borderRadius: 12,
    padding: 14,
    marginBottom: 16,
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.08)',
  },
  kpiRow: {
    flexDirection: 'row',
    gap: 8,
    marginBottom: 14,
  },
  kpiCard: {
    flex: 1,
    backgroundColor: '#1E1E1E',
    borderRadius: 8,
    padding: 8,
    borderLeftWidth: 3,
  },
  kpiTitle: {
    color: '#94A3B8',
    fontSize: 10,
    fontWeight: '600',
    textTransform: 'uppercase',
  },
  kpiValue: {
    fontSize: 13,
    fontWeight: '800',
    marginTop: 2,
  },
  heatmapSectionTitle: {
    color: '#CBD5E1',
    fontSize: 12,
    fontWeight: '700',
    marginBottom: 8,
  },
  heatmapGrid: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 6,
    marginBottom: 10,
  },
  heatmapCell: {
    width: 32,
    height: 32,
    borderRadius: 6,
    alignItems: 'center',
    justifyContent: 'center',
  },
  heatmapCellPos: {
    backgroundColor: '#064E3B',
    borderWidth: 1,
    borderColor: '#059669',
  },
  heatmapCellNeg: {
    backgroundColor: '#7F1D1D',
    borderWidth: 1,
    borderColor: '#DC2626',
  },
  heatmapCellZero: {
    backgroundColor: '#262626',
    borderWidth: 1,
    borderColor: '#404040',
  },
  heatmapCellSelected: {
    borderColor: '#60A5FA',
    borderWidth: 2,
  },
  heatmapCellDay: {
    color: '#FFF',
    fontSize: 11,
    fontWeight: '700',
  },
  dayDetailCard: {
    backgroundColor: '#1A2234',
    borderRadius: 8,
    padding: 10,
    borderWidth: 1,
    borderColor: '#2563EB',
    marginTop: 4,
  },
  dayDetailDate: {
    color: '#60A5FA',
    fontSize: 12,
    fontWeight: '800',
    marginBottom: 6,
  },
  dayDetailRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    marginBottom: 3,
  },
  dayDetailLabel: {
    color: '#94A3B8',
    fontSize: 11,
  },
  dayDetailVal: {
    color: '#F8FAFC',
    fontWeight: '700',
  }
});
