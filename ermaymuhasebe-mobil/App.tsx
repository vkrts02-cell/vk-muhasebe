import React, { useState, useEffect, useRef } from 'react';
import { View, ActivityIndicator, Text, AppState, TouchableOpacity } from 'react-native';
import { GestureHandlerRootView } from 'react-native-gesture-handler';
import AppNavigator from './src/navigation/AppNavigator';
import LoginScreen from './src/screens/LoginScreen';
import AsyncStorage from './src/services/storage';
import LockScreen from './src/screens/LockScreen';
import { loadConfigFromStorage, getFirebaseConfig, addConfigListener, getLoggedUser } from './src/services/firebase';
import { recordBackground, shouldLock, clearBackgroundRecord } from './src/services/lockService';
import { loadFirmaProfili } from './src/services/pdfService';
import { initNotifications } from './src/services/alertService';


class ErrorBoundary extends React.Component<{ children: React.ReactNode }, { hasError: boolean, errorText: string }> {
  constructor(props: any) {
    super(props);
    this.state = { hasError: false, errorText: '' };
  }

  static getDerivedStateFromError(error: any) {
    return { hasError: true, errorText: error?.message || 'Bilinmeyen hata' };
  }

  componentDidCatch(error: any, errorInfo: any) {
    console.warn('[ErrorBoundary] Caught:', error, errorInfo);
  }

  render() {
    if (this.state.hasError) {
      return (
        <View style={{ flex: 1, backgroundColor: '#0A0A0A', justifyContent: 'center', alignItems: 'center', padding: 20 }}>
          <Text style={{ color: '#EF4444', fontSize: 18, fontWeight: 'bold', marginBottom: 8 }}>Geçici Bir Sorun Oluştu</Text>
          <Text style={{ color: '#94A3B8', textAlign: 'center', fontSize: 13, marginBottom: 16 }}>{this.state.errorText}</Text>
          <TouchableOpacity 
            onPress={() => this.setState({ hasError: false, errorText: '' })}
            style={{ backgroundColor: '#0061FF', paddingHorizontal: 20, paddingVertical: 10, borderRadius: 8 }}
          >
            <Text style={{ color: '#FFFFFF', fontWeight: 'bold' }}>Yeniden Dene</Text>
          </TouchableOpacity>
        </View>
      );
    }
    return this.props.children;
  }
}

export default function App() {
  const [isConfigured, setIsConfigured] = useState<boolean>(false);
  const [loggedUser, setLoggedUser] = useState<any | null>(null);
  const [loading, setLoading] = useState<boolean>(true);
  const [locked, setLocked] = useState<boolean>(false);
  const [isRooted, setIsRooted] = useState<boolean>(false);
  const prevState = useRef(AppState.currentState);

  const checkConfigAndUser = async () => {
    try {
      await loadConfigFromStorage();
      const config = getFirebaseConfig();
      setIsConfigured(!!(config && config.url));
      
      const user = await getLoggedUser();
      setLoggedUser(user);

      // Şirket profilini ve logosunu arka planda önbelleğe al
      loadFirmaProfili().catch(() => {});
      initNotifications().catch(() => {});
    } catch (e) {
      console.warn("Error in checkConfigAndUser:", e);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    checkConfigAndUser();

    const unsubscribe = addConfigListener(async () => {
      const config = getFirebaseConfig();
      setIsConfigured(!!(config && config.url));
      const user = await getLoggedUser();
      setLoggedUser(user);
    });

    const appStateSub = AppState.addEventListener('change', async (nextState) => {
      if (nextState === 'background' || nextState === 'inactive') {
        await recordBackground();
      } else if (nextState === 'active' && (prevState.current === 'background' || prevState.current === 'inactive')) {
        const needLock = await shouldLock();
        if (needLock) {
          setLocked(true);
          clearBackgroundRecord();
        }
      }
      prevState.current = nextState;
    });

    return () => {
      unsubscribe();
      appStateSub.remove();
    };
  }, []);

  const handleLoginSuccess = async () => {
    console.log('[APP] handleLoginSuccess çağrıldı');
    // Giriş başarılı olunca kullanıcıyı yeniden yükle
    let user = await getLoggedUser();
    if (!user) {
      const savedUser = await AsyncStorage.getItem('ermay_saved_username');
      if (savedUser) {
        user = { id: savedUser, username: savedUser, email: `${savedUser}@ermay.local`, user_metadata: { role: 'Admin', fullName: savedUser } };
      }
    }
    console.log('[APP] getLoggedUser sonucu:', user ? JSON.stringify(user) : 'NULL');
    setLoggedUser(user);
    const config = getFirebaseConfig();
    console.log('[APP] isConfigured:', !!(config && config.url));
    setIsConfigured(!!(config && config.url));
    clearBackgroundRecord();
    console.log('[APP] handleLoginSuccess tamamlandı');
  };

  const handleUnlock = () => {
    setLocked(false);
    clearBackgroundRecord();
  };

  if (loading) {
    return (
      <View style={{ flex: 1, backgroundColor: '#0B0F19', justifyContent: 'center', alignItems: 'center' }}>
        <ActivityIndicator size="large" color="#0061FF" />
        <Text style={{ color: '#94A3B8', marginTop: 12 }}>Sistem kontrol ediliyor...</Text>
      </View>
    );
  }

  if (isRooted) {
    return (
      <View style={{ flex: 1, backgroundColor: '#0B0F19', justifyContent: 'center', alignItems: 'center', padding: 20 }}>
        <Text style={{ color: '#EF4444', fontSize: 24, fontWeight: 'bold', marginBottom: 12 }}>Güvenlik Uyarısı</Text>
        <Text style={{ color: '#94A3B8', textAlign: 'center', fontSize: 16 }}>
          Bu cihazın işletim sistemi (Root / Jailbreak) değiştirilmiş. Finansal veri güvenliği standartları gereği bu cihazda uygulamaya erişilemez.
        </Text>
      </View>
    );
  }

  let content;
  // 1. Veritabanı URL'i hiç ayarlanmamışsa config modunu göster
  if (!isConfigured) {
    content = <LoginScreen onLoginSuccess={handleLoginSuccess} mode="config" />;
  }
  // 2. Veritabanı var ama oturum açılmamışsa giriş ekranını göster
  else if (!loggedUser) {
    content = <LoginScreen onLoginSuccess={handleLoginSuccess} mode="user" />;
  }
  // 3. Oturum kilitliyse kilit ekranını göster
  else if (locked) {
    content = <LockScreen onUnlock={handleUnlock} />;
  }
  // 4. Her şey tamamsa uygulamayı aç
  else {
    content = <AppNavigator />;
  }

  return (
    <GestureHandlerRootView style={{ flex: 1 }}>
      <ErrorBoundary>
        {content}
      </ErrorBoundary>
    </GestureHandlerRootView>
  );
}