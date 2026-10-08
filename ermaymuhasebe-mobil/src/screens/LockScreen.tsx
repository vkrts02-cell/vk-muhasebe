import React, { useState, useEffect } from 'react';
import { StyleSheet, Text, View, SafeAreaView, TextInput, TouchableOpacity, ActivityIndicator } from 'react-native';
import { Lock, KeyRound, ScanFace } from 'lucide-react-native';
import { verifyPin } from '../services/lockService';
import { authenticateWithBiometrics, checkBiometricsAvailability } from '../services/biometricService';
import {
  KeyboardDoneAccessory,
  KEYBOARD_ACCESSORY_ID,
} from '../components/KeyboardDoneAccessory';

interface LockScreenProps {
  onUnlock: () => void;
}

export default function LockScreen({ onUnlock }: LockScreenProps) {
  const [pin, setPin] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    handleBiometricUnlock();
  }, []);

  const handleBiometricUnlock = async () => {
    const check = await checkBiometricsAvailability();
    if (check.isAvailable) {
      const ok = await authenticateWithBiometrics('Ermay Muhasebe kilidini açın');
      if (ok) {
        onUnlock();
      }
    }
  };

  const handleUnlock = async () => {
    if (!pin.trim()) {
      setError('Lütfen kilit PIN\'ini girin.');
      return;
    }
    setLoading(true);
    setError('');
    const ok = await verifyPin(pin.trim());
    setLoading(false);
    if (ok) {
      onUnlock();
    } else {
      setError('Hatalı PIN. Tekrar deneyin.');
      setPin('');
    }
  };

  return (
    <SafeAreaView style={styles.container}>
      <View style={styles.content}>
        <View style={styles.iconBox}>
          <Lock color="#F59E0B" size={36} />
        </View>
        <Text style={styles.title}>Oturum Kilitli</Text>
        <Text style={styles.subtitle}>
          Devam etmek için Face ID doğrulayın veya PIN kodunuzu girin.
        </Text>

        {error ? (
          <View style={styles.errorBox}>
            <Text style={styles.errorText}>{error}</Text>
          </View>
        ) : null}

        {/* Face ID / Touch ID Button */}
        <TouchableOpacity
          style={styles.biometricBtn}
          onPress={handleBiometricUnlock}
        >
          <ScanFace color="#0061FF" size={20} />
          <Text style={styles.biometricBtnText}>Face ID / Touch ID ile Aç</Text>
        </TouchableOpacity>

        <View style={styles.inputGroup}>
          <View style={{ flexDirection: 'row', alignItems: 'center', marginBottom: 8 }}>
            <KeyRound color="#94A3B8" size={16} style={{ marginRight: 6 }} />
            <Text style={styles.label}>Kilit PIN</Text>
          </View>
          <TextInput
            style={styles.input}
            placeholder="4-6 haneli PIN"
            placeholderTextColor="#64748B"
            value={pin}
            onChangeText={setPin}
            secureTextEntry
            keyboardType="number-pad"
            inputAccessoryViewID={KEYBOARD_ACCESSORY_ID}
            maxLength={6}
          />
        </View>

        <TouchableOpacity
          style={[styles.button, loading && styles.buttonDisabled]}
          onPress={handleUnlock}
          disabled={loading}
        >
          {loading ? <ActivityIndicator color="#FFF" /> : <Text style={styles.buttonText}>Kilidi Aç</Text>}
        </TouchableOpacity>
      </View>
      <KeyboardDoneAccessory />
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#0A0A0A',
  },
  content: {
    flex: 1,
    justifyContent: 'center',
    padding: 24,
  },
  iconBox: {
    width: 72,
    height: 72,
    borderRadius: 20,
    backgroundColor: 'rgba(245,158,11,0.12)',
    alignItems: 'center',
    justifyContent: 'center',
    alignSelf: 'center',
    marginBottom: 20,
  },
  title: {
    fontSize: 24,
    fontWeight: '900',
    color: '#FFF',
    textAlign: 'center',
    marginBottom: 8,
  },
  subtitle: {
    fontSize: 14,
    color: '#94A3B8',
    textAlign: 'center',
    marginBottom: 28,
  },
  errorBox: {
    backgroundColor: 'rgba(239, 68, 68, 0.1)',
    borderWidth: 1,
    borderColor: 'rgba(239, 68, 68, 0.5)',
    padding: 12,
    borderRadius: 8,
    marginBottom: 16,
  },
  errorText: {
    color: '#EF4444',
    fontSize: 13,
    textAlign: 'center',
  },
  inputGroup: {
    marginBottom: 20,
  },
  label: {
    color: '#E2E8F0',
    fontSize: 13,
    fontWeight: '600',
  },
  input: {
    backgroundColor: '#2A2A2A',
    borderWidth: 1,
    borderColor: '#444',
    borderRadius: 12,
    padding: 16,
    color: '#FFF',
    fontSize: 18,
    letterSpacing: 4,
    textAlign: 'center',
  },
  button: {
    backgroundColor: '#0061FF',
    padding: 16,
    borderRadius: 12,
    alignItems: 'center',
    marginTop: 12,
  },
  buttonDisabled: {
    opacity: 0.7,
  },
  buttonText: {
    color: '#FFF',
    fontSize: 16,
    fontWeight: 'bold',
  },
  biometricBtn: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 10,
    backgroundColor: 'rgba(0, 97, 255, 0.12)',
    borderWidth: 1,
    borderColor: 'rgba(0, 97, 255, 0.35)',
    padding: 14,
    borderRadius: 12,
    marginBottom: 20,
  },
  biometricBtnText: {
    color: '#0061FF',
    fontSize: 15,
    fontWeight: '700',
  },
});
