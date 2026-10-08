import React, { useState, useEffect } from 'react';
import { StyleSheet, Text, View, SafeAreaView, ScrollView, TextInput, TouchableOpacity } from 'react-native';
import { Calculator, Weight, DollarSign, Layers } from 'lucide-react-native';
import {
  KeyboardDoneAccessory,
  KEYBOARD_ACCESSORY_ID,
} from '../components/KeyboardDoneAccessory';

export default function MaliyetHesaplamaScreen() {
  // Section 1: Toplam KG Hesaplama
  const [gr, setGr] = useState<string>('0');
  const [en, setEn] = useState<string>('160');
  const [sarim, setSarim] = useState<string>('0');
  const [toplamKgSonuc, setToplamKgSonuc] = useState<string>('0.00 KG');

  // Section 2: Top Fiyatı Hesaplama
  const [toplamKg, setToplamKg] = useState<string>('0');
  const [kgFiyati, setKgFiyati] = useState<string>('0');
  const [topFiyatiSonuc, setTopFiyatiSonuc] = useState<string>('0.00 $');

  // Section 3: Metrekare Hesaplama
  const [topBoy, setTopBoy] = useState<string>('0');
  const [metrekareSonuc, setMetrekareSonuc] = useState<string>('0.00 m²');

  // Section 4: Birim Metrekare Maliyeti Hesaplama
  const [toplamMetrekare, setToplamMetrekare] = useState<string>('0');
  const [topFiyatiInput, setTopFiyatiInput] = useState<string>('0');
  const [birimMaliyetSonuc, setBirimMaliyetSonuc] = useState<string>('0.00 $');

  const parseNum = (val: string): number => {
    if (!val) return 0;
    const clean = val.replace(',', '.');
    const parsed = parseFloat(clean);
    return isNaN(parsed) ? 0 : parsed;
  };

  // Synchronize Sarim to TopBoy
  useEffect(() => {
    setTopBoy(sarim);
  }, [sarim]);

  // Section 1: Toplam KG Hesapla
  const hesaplaKg = () => {
    const g = parseNum(gr);
    const e = parseNum(en);
    const s = parseNum(sarim);
    const sonuc = (g * e * s) / 100000.0;
    const formatted = sonuc.toFixed(2);
    setToplamKgSonuc(`${formatted} KG`);
    setToplamKg(formatted);
  };

  // Section 2: Top Fiyatı Hesapla
  const hesaplaTopFiyati = () => {
    const kg = parseNum(toplamKg);
    const f = parseNum(kgFiyati);
    const sonuc = kg * f;
    const formatted = sonuc.toFixed(2);
    setTopFiyatiSonuc(`${formatted} $`);
    setTopFiyatiInput(formatted);
  };

  // Section 3: Metrekare Hesapla
  const hesaplaMetrekare = () => {
    const boy = parseNum(topBoy);
    const sonuc = boy * 1.6; // fixed width 1.6m (160cm)
    const formatted = sonuc.toFixed(2);
    setMetrekareSonuc(`${formatted} m²`);
    setToplamMetrekare(formatted);
  };

  // Section 4: Birim Metrekare Maliyeti Hesapla
  const hesaplaBirimMaliyet = () => {
    const m2 = parseNum(toplamMetrekare);
    const f = parseNum(topFiyatiInput);
    if (m2 === 0) {
      setBirimMaliyetSonuc('0.00 $');
      return;
    }
    const sonuc = f / m2;
    setBirimMaliyetSonuc(`${sonuc.toFixed(2)} $`);
  };

  return (
    <SafeAreaView style={styles.container}>
      <View style={styles.header}>
        <View style={{ flexDirection: 'row', alignItems: 'center' }}>
          <Calculator color="#0D9488" size={28} style={{ marginRight: 12 }} />
          <Text style={styles.headerTitle}>Hesap Makinası</Text>
        </View>
        <Text style={styles.headerSubtitle}>Reaktif Maliyet & Ölçü Hesaplayıcı</Text>
      </View>

      <ScrollView
        contentContainerStyle={styles.scrollContent}
        automaticallyAdjustKeyboardInsets={true}
        keyboardShouldPersistTaps="handled"
      >
        {/* 1. Toplam KG Hesaplama */}
        <View style={styles.card}>
          <View style={styles.cardHeader}>
            <Weight color="#0D9488" size={20} />
            <Text style={styles.cardTitle}>1. Toplam KG Hesaplama</Text>
          </View>

          <View style={styles.row}>
            <View style={[styles.inputGroup, { flex: 1, marginRight: 8 }]}>
              <Text style={styles.inputLabel}>Gramaj (g/m²)</Text>
              <TextInput 
                style={styles.input}
                keyboardType="numeric"
                inputAccessoryViewID={KEYBOARD_ACCESSORY_ID}
                value={gr}
                onChangeText={setGr}
              />
            </View>
            <View style={[styles.inputGroup, { flex: 1, marginRight: 8 }]}>
              <Text style={styles.inputLabel}>En (cm)</Text>
              <TextInput 
                style={styles.input}
                keyboardType="numeric"
                inputAccessoryViewID={KEYBOARD_ACCESSORY_ID}
                value={en}
                onChangeText={setEn}
              />
            </View>
            <View style={[styles.inputGroup, { flex: 1 }]}>
              <Text style={styles.inputLabel}>Sarım (mt)</Text>
              <TextInput 
                style={styles.input}
                keyboardType="numeric"
                inputAccessoryViewID={KEYBOARD_ACCESSORY_ID}
                value={sarim}
                onChangeText={setSarim}
              />
            </View>
          </View>

          <View style={styles.actionRow}>
            <TouchableOpacity style={styles.btn} onPress={hesaplaKg}>
              <Text style={styles.btnText}>KG Hesapla</Text>
            </TouchableOpacity>
            <Text style={styles.resultText}>{toplamKgSonuc}</Text>
          </View>
        </View>

        {/* 2. Top Fiyatı Hesaplama */}
        <View style={styles.card}>
          <View style={styles.cardHeader}>
            <DollarSign color="#0D9488" size={20} />
            <Text style={styles.cardTitle}>2. Top Fiyatı Hesaplama</Text>
          </View>

          <View style={styles.row}>
            <View style={[styles.inputGroup, { flex: 1, marginRight: 12 }]}>
              <Text style={styles.inputLabel}>Toplam KG</Text>
              <TextInput 
                style={styles.input}
                keyboardType="numeric"
                inputAccessoryViewID={KEYBOARD_ACCESSORY_ID}
                value={toplamKg}
                onChangeText={setToplamKg}
              />
            </View>
            <View style={[styles.inputGroup, { flex: 1 }]}>
              <Text style={styles.inputLabel}>KG Fiyatı ($)</Text>
              <TextInput 
                style={styles.input}
                keyboardType="numeric"
                inputAccessoryViewID={KEYBOARD_ACCESSORY_ID}
                value={kgFiyati}
                onChangeText={setKgFiyati}
              />
            </View>
          </View>

          <View style={styles.actionRow}>
            <TouchableOpacity style={styles.btn} onPress={hesaplaTopFiyati}>
              <Text style={styles.btnText}>Top Fiyatı Hesapla</Text>
            </TouchableOpacity>
            <Text style={styles.resultText}>{topFiyatiSonuc}</Text>
          </View>
        </View>

        {/* 3. Metrekare Hesaplama */}
        <View style={styles.card}>
          <View style={styles.cardHeader}>
            <Layers color="#0D9488" size={20} />
            <Text style={styles.cardTitle}>3. Metrekare (m²) Hesaplama</Text>
          </View>

          <View style={styles.inputGroup}>
            <Text style={styles.inputLabel}>Top Boyu (Metre - En 1.6mt Sabit)</Text>
            <TextInput 
              style={styles.input}
              keyboardType="numeric"
              inputAccessoryViewID={KEYBOARD_ACCESSORY_ID}
              value={topBoy}
              onChangeText={setTopBoy}
            />
          </View>

          <View style={styles.actionRow}>
            <TouchableOpacity style={styles.btn} onPress={hesaplaMetrekare}>
              <Text style={styles.btnText}>m² Hesapla</Text>
            </TouchableOpacity>
            <Text style={styles.resultText}>{metrekareSonuc}</Text>
          </View>
        </View>

        {/* 4. Birim Metrekare Maliyeti */}
        <View style={styles.card}>
          <View style={styles.cardHeader}>
            <Calculator color="#0D9488" size={20} />
            <Text style={styles.cardTitle}>4. Birim Metrekare Maliyeti</Text>
          </View>

          <View style={styles.row}>
            <View style={[styles.inputGroup, { flex: 1, marginRight: 12 }]}>
              <Text style={styles.inputLabel}>Toplam Metrekare (m²)</Text>
              <TextInput 
                style={styles.input}
                keyboardType="numeric"
                inputAccessoryViewID={KEYBOARD_ACCESSORY_ID}
                value={toplamMetrekare}
                onChangeText={setToplamMetrekare}
              />
            </View>
            <View style={[styles.inputGroup, { flex: 1 }]}>
              <Text style={styles.inputLabel}>Top Fiyatı ($)</Text>
              <TextInput 
                style={styles.input}
                keyboardType="numeric"
                inputAccessoryViewID={KEYBOARD_ACCESSORY_ID}
                value={topFiyatiInput}
                onChangeText={setTopFiyatiInput}
              />
            </View>
          </View>

          <View style={styles.actionRow}>
            <TouchableOpacity style={styles.btn} onPress={hesaplaBirimMaliyet}>
              <Text style={styles.btnText}>Maliyet Hesapla</Text>
            </TouchableOpacity>
            <Text style={styles.resultText}>{birimMaliyetSonuc}</Text>
          </View>
        </View>
      </ScrollView>
      <KeyboardDoneAccessory />
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#0A0A0A',
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
  scrollContent: {
    padding: 20,
    paddingBottom: 40,
  },
  card: {
    backgroundColor: '#161616',
    borderRadius: 20,
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.08)',
    padding: 20,
    marginBottom: 16,
  },
  cardHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: 16,
  },
  cardTitle: {
    color: '#FFF',
    fontSize: 15,
    fontWeight: 'bold',
    marginLeft: 10,
  },
  row: {
    flexDirection: 'row',
  },
  inputGroup: {
    marginBottom: 16,
  },
  inputLabel: {
    color: '#94A3B8',
    fontSize: 12,
    fontWeight: '600',
    marginBottom: 6,
  },
  input: {
    backgroundColor: '#2A2A2A',
    borderRadius: 12,
    padding: 12,
    color: '#FFF',
    fontSize: 14,
    borderWidth: 1,
    borderColor: '#444',
  },
  actionRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginTop: 8,
    borderTopWidth: 1,
    borderTopColor: 'rgba(255,255,255,0.05)',
    paddingTop: 12,
  },
  btn: {
    backgroundColor: 'rgba(13, 148, 136, 0.15)',
    borderColor: 'rgba(13, 148, 136, 0.3)',
    borderWidth: 1,
    borderRadius: 10,
    paddingVertical: 10,
    paddingHorizontal: 16,
  },
  btnText: {
    color: '#0D9488',
    fontWeight: 'bold',
    fontSize: 13,
  },
  resultText: {
    color: '#00FF87',
    fontSize: 18,
    fontWeight: '900',
  }
});
