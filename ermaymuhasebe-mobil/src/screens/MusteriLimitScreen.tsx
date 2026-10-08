import React, { useState, useEffect } from 'react';
import { StyleSheet, Text, View, SafeAreaView, ScrollView, TouchableOpacity, ActivityIndicator, Alert, Modal, TextInput } from 'react-native';
import { ShieldAlert, TrendingUp, X, Save } from 'lucide-react-native';
import { subscribeToPath, writeData } from '../services/firebase';
import {
  KeyboardDoneAccessory,
  KEYBOARD_ACCESSORY_ID,
} from '../components/KeyboardDoneAccessory';

const formatMoney = (val: number) => {
  return new Intl.NumberFormat('tr-TR', { style: 'currency', currency: 'TRY' }).format(val);
};

export default function MusteriLimitScreen({ isTab = false }: { isTab?: boolean }) {
  const [cariler, setCariler] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  const [editCari, setEditCari] = useState<any | null>(null);
  const [editLimit, setEditLimit] = useState('');

  useEffect(() => {
    const unsub = subscribeToPath('Cariler', (data) => {
      if (data) {
        const list = Array.isArray(data) ? data.filter(Boolean) : Object.keys(data).map((key) => ({ ...data[key], firebaseKey: key }));
        setCariler(list.filter((c) => !c.isDeleted));
      }
      setLoading(false);
    });
    return () => unsub();
  }, []);

  const rows = cariler
    .map((c) => {
      const bakiye = (c.borc || 0) - (c.alacak || 0);
      const limit = c.riskLimiti || 0;
      const useAsim = limit > 0 && bakiye > limit;
      const oran = limit > 0 ? Math.min(4, (bakiye / limit)) * 100 : 0;
      return { c, limit, bakiye, asim: useAsim ? bakiye - limit : 0, oran };
    })
    .filter((r) => r.limit > 0)
    .sort((a, b) => (b.asim > 0 ? 1 : 0) - (a.asim > 0 ? 1 : 0) || b.bakiye - a.bakiye);

  const asimSayisi = rows.filter((r) => r.asim > 0).length;

  const handleSaveLimit = async () => {
    if (!editCari) return;
    const newLimit = parseFloat(editLimit) || 0;
    if (newLimit < 0) {
      Alert.alert('Hata', 'Limit sıfırdan küçük olamaz.');
      return;
    }
    const ok = await writeData(`Cariler/${editCari.id}`, { ...editCari, riskLimiti: newLimit });
    if (!ok) {
      Alert.alert('Hata', 'Risk limiti güncellenemedi. (Bağlantı sorunu — kayıt sıraya alındı.)');
      return;
    }
    setEditCari(null);
    Alert.alert('Başarılı', 'Risk limiti güncellendi.');
  };

  const Wrapper = isTab ? View : SafeAreaView;

  return (
    <Wrapper style={styles.container}>
      {!isTab && (
        <View style={styles.header}>
          <View style={{ flexDirection: 'row', alignItems: 'center', marginBottom: 12 }}>
            <ShieldAlert color="#F59E0B" size={24} style={{ marginRight: 8 }} />
            <Text style={styles.headerTitle}>Müşteri Limit Yönetimi</Text>
          </View>
          <Text style={styles.headerSub}>
            {rows.length} cari limitli • {asimSayisi > 0 ? `${asimSayisi} limit aşımı!` : 'Limit aşımı yok'}
          </Text>
        </View>
      )}

      {loading ? (
        <View style={styles.center}>
          <ActivityIndicator size="large" color="#F59E0B" />
        </View>
      ) : (
        <ScrollView contentContainerStyle={styles.content}>
          {rows.length === 0 ? (
            <Text style={styles.emptyText}>Risk limiti tanımlı cari bulunmuyor. (Cariler {'>'} Cari Düzenle {'>'} Risk Limiti)</Text>
          ) : (
            rows.map((r, idx) => (
              <View key={idx} style={[styles.card, r.asim > 0 && { borderColor: 'rgba(239,68,68,0.5)' }]}>
                <View style={styles.cardHeader}>
                  <View style={{ flex: 1 }}>
                    <Text style={styles.cardTitle}>{r.c.unvan}</Text>
                    <Text style={styles.cardSub}>
                      Kullanım: {formatMoney(Math.max(0, r.bakiye))} / {formatMoney(r.limit)} • Kullanım: %{r.oran.toFixed(0)}
                    </Text>
                  </View>
                  <TouchableOpacity
                    style={styles.editBtn}
                    onPress={() => { setEditCari(r.c); setEditLimit(String(r.limit || 0)); }}
                  >
                    <Text style={styles.editBtnText}>Düzenle</Text>
                  </TouchableOpacity>
                </View>
                <View style={[styles.bar, { width: `${Math.min(100, r.oran)}%`, backgroundColor: r.asim > 0 ? '#EF4444' : r.oran > 80 ? '#F59E0B' : '#00FF87' }]} />
                {r.asim > 0 && (
                  <Text style={styles.asimText}>⚠️ LİMİT AŞIMI: {formatMoney(r.asim)}</Text>
                )}
              </View>
            ))
          )}
        </ScrollView>
      )}

      {/* Limit Düzenleme Modalı */}
      <Modal visible={!!editCari} transparent animationType="slide">
        <SafeAreaView style={styles.modalOverlay}>
          <View style={styles.modalContent}>
            <View style={styles.modalHeader}>
              <Text style={styles.modalTitle}>Risk Limiti Düzenle</Text>
              <TouchableOpacity
                onPress={() => setEditCari(null)}
                hitSlop={{ top: 12, bottom: 12, left: 12, right: 12 }}
              >
                <X color="#FFF" size={24} />
              </TouchableOpacity>
            </View>
            <Text style={styles.label}>Cari</Text>
            <Text style={styles.cariName}>{editCari?.unvan}</Text>
            <Text style={styles.label}>Risk Limiti (₺)</Text>
            <TextInput
              style={styles.input}
              keyboardType="numeric"
              inputAccessoryViewID={KEYBOARD_ACCESSORY_ID}
              value={editLimit}
              onChangeText={setEditLimit}
              placeholderTextColor="#64748B"
            />
            <TouchableOpacity style={styles.saveButton} onPress={handleSaveLimit}>
              <Save color="#FFF" size={18} />
              <Text style={styles.saveText}>Kaydet</Text>
            </TouchableOpacity>
          </View>
        </SafeAreaView>
      </Modal>
      <KeyboardDoneAccessory />
    </Wrapper>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: '#0A0A0A' },
  header: { padding: 20, paddingTop: 40, borderBottomWidth: 1, borderBottomColor: 'rgba(255,255,255,0.08)' },
  headerTitle: { fontSize: 20, fontWeight: '900', color: '#FFF', flex: 1 },
  headerSub: { color: '#64748B', fontSize: 13, marginTop: 4 },
  center: { flex: 1, justifyContent: 'center', alignItems: 'center' },
  content: { padding: 20 },
  emptyText: { color: '#64748B', textAlign: 'center', marginTop: 40 },
  card: { backgroundColor: '#0F0F0F', borderRadius: 16, borderWidth: 1, borderColor: 'rgba(255,255,255,0.08)', padding: 16, marginBottom: 12 },
  cardHeader: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'flex-start' },
  cardTitle: { fontSize: 16, fontWeight: 'bold', color: '#FFF' },
  cardSub: { color: '#94A3B8', fontSize: 12, marginTop: 4 },
  editBtn: { backgroundColor: 'rgba(245,158,11,0.15)', paddingHorizontal: 12, paddingVertical: 6, borderRadius: 8 },
  editBtnText: { color: '#F59E0B', fontWeight: 'bold', fontSize: 12 },
  bar: { height: 6, borderRadius: 3, marginTop: 12 },
  asimText: { color: '#EF4444', fontSize: 12, fontWeight: 'bold', marginTop: 8 },
  modalOverlay: { flex: 1, backgroundColor: 'rgba(0,0,0,0.85)' },
  modalContent: { flex: 1, backgroundColor: '#0A0A0A', padding: 20 },
  modalHeader: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', marginBottom: 20 },
  modalTitle: { fontSize: 18, fontWeight: '900', color: '#FFF' },
  label: { color: '#94A3B8', fontSize: 13, marginBottom: 6, marginTop: 12 },
  cariName: { color: '#FFF', fontSize: 16, fontWeight: '600' },
  input: { backgroundColor: '#2A2A2A', borderRadius: 12, padding: 14, color: '#FFF', fontSize: 15, borderWidth: 1, borderColor: '#444' },
  saveButton: { flexDirection: 'row', alignItems: 'center', justifyContent: 'center', backgroundColor: '#F59E0B', borderRadius: 12, padding: 14, marginTop: 20 },
  saveText: { color: '#FFF', fontWeight: 'bold', fontSize: 15, marginLeft: 8 },
});
