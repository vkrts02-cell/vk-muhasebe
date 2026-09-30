import React, { useState, useEffect } from 'react';
import { View, Text, TextInput, TouchableOpacity, StyleSheet, ActivityIndicator, SafeAreaView, KeyboardAvoidingView, Platform, ScrollView } from 'react-native';
import { Key, Link, Calendar, ShieldAlert, LogIn, Settings, CheckCircle2, User, History, ChevronRight, Send, Mail, Globe, Shield, Smartphone } from 'lucide-react-native';
import AsyncStorage from '../services/storage';
import { saveFirebaseConfig, saveActiveYear, readData, writeData, loginUser, logoutUser, fetchAvailableYears, loadConfigFromStorage, registerInitialUser, createNewMaliYil } from '../services/firebase';

interface LoginScreenProps {
  onLoginSuccess: () => void;
  mode?: 'config' | 'user' | 'year_selection';
}

export default function LoginScreen({ onLoginSuccess, mode: initialMode = 'config' }: LoginScreenProps) {
  const [mode, setMode] = useState<'config' | 'user' | 'year_selection'>(initialMode);
  
  // Config Mode States (Supabase Bulut ve E-Posta)
  const [activeConfigTab, setActiveConfigTab] = useState<'cloud' | 'smtp'>('cloud');
  const [url, setUrl] = useState('https://fqgbdymffknglqeqoogt.supabase.co');
  const [secret, setSecret] = useState('eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6ImZxZ2JkeW1mZmtuZ2xxZXFvb2d0Iiwicm9sZSI6ImFub24iLCJpYXQiOjE3ODk1ODUzMDEsImV4cCI6MjEwNTE2MTMwMX0.pBeE2ivWpbkAd8KSN1y2pXNZPIr_1mGMLXXHYPzjTDg');
  
  // Kurulumda Belirlenecek Yönetici Hesabı
  const [configUsername, setConfigUsername] = useState('');
  const [configPassword, setConfigPassword] = useState('');

  // SMTP / Gmail States
  const [smtpEmail, setSmtpEmail] = useState('');
  const [smtpPass, setSmtpPass] = useState('');

  // Çalışma Yılı
  const [year, setYear] = useState(new Date().getFullYear().toString());

  // User Mode States
  const [usernameOrEmail, setUsernameOrEmail] = useState('');
  const [password, setPassword] = useState('');
  const [rememberMe, setRememberMe] = useState(true);

  // Year Selection Mode States
  const [selectedYear, setSelectedYear] = useState<string>(new Date().getFullYear().toString());
  const [availableYears, setAvailableYears] = useState<string[]>([]);
  const [loggedInUser, setLoggedInUser] = useState<any>(null);
  const [showCustomInput, setShowCustomInput] = useState(false);

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  
  // Firebase Auth durumunu belirlemek için (UI açıklaması için)
  const [isAuthEnabled, setIsAuthEnabled] = useState(false);

  useEffect(() => {
    setMode(initialMode);
    
    const loadSavedSettings = async () => {
      try {
        const savedUrl = (await AsyncStorage.getItem('ermay_supabase_url')) || (await AsyncStorage.getItem('ermay_firebase_url'));
        const savedSecret = (await AsyncStorage.getItem('ermay_supabase_key')) || (await AsyncStorage.getItem('ermay_firebase_secret'));
        const savedSmtpEmail = await AsyncStorage.getItem('ermay_smtp_email');
        const savedSmtpPass = await AsyncStorage.getItem('ermay_smtp_pass');
        const savedYear = await AsyncStorage.getItem('ermay_active_year');

        if (savedUrl) setUrl(savedUrl);
        if (savedSecret) setSecret(savedSecret);
        if (savedSmtpEmail) setSmtpEmail(savedSmtpEmail);
        if (savedSmtpPass) setSmtpPass(savedSmtpPass);
        if (savedYear) setYear(savedYear);

        const savedUsername = await AsyncStorage.getItem('ermay_saved_username');
        const savedPassword = await AsyncStorage.getItem('ermay_saved_password');
        const isRemembered = await AsyncStorage.getItem('ermay_remember_me');
        
        if (isRemembered === 'true' && savedUsername && savedPassword) {
          setUsernameOrEmail(savedUsername);
          setPassword(savedPassword);
          setRememberMe(true);
        }
      } catch (err) {
        console.error('Ayarlar yüklenemedi:', err);
      }
    };

    loadSavedSettings();

    if (initialMode === 'user') {
      checkAuthStatus();
    }
  }, [initialMode]);

  // Canlı (Real-time) yıl listesi güncellemesi için polling
  useEffect(() => {
    let interval: any = null;
    if (mode === 'year_selection') {
      interval = setInterval(async () => {
        try {
          const years = await fetchAvailableYears();
          setAvailableYears(prev => {
            if (JSON.stringify(prev) !== JSON.stringify(years)) {
              return years;
            }
            return prev;
          });
        } catch (err) {
          // Sessiz hata yakalama
        }
      }, 5000);
    }
    return () => {
      if (interval) clearInterval(interval);
    };
  }, [mode]);

  const checkAuthStatus = async () => {
    try {
      const profil = await readData('FirmaProfili/1');
      if (profil) {
        setIsAuthEnabled(!!(profil.isFirebaseAuthEnabled || profil.IsFirebaseAuthEnabled));
      }
    } catch {}
  };

  const loadYearSelectionData = async () => {
    setLoading(true);
    setError('');
    try {
      await loadConfigFromStorage();
      const years = await fetchAvailableYears();
      setAvailableYears(years);
      
      const storedYear = await AsyncStorage.getItem('ermay_active_year');
      if (storedYear && years.includes(storedYear)) {
        setSelectedYear(storedYear);
      } else if (years.length > 0) {
        setSelectedYear(years[0]);
      }
    } catch (err) {
      console.error('Years load error:', err);
    } finally {
      setLoading(false);
    }
  };

  const handleConfigSave = async () => {
    if (!url.trim()) {
      setError('Lütfen Supabase Proje URL\'sini girin.');
      return;
    }
    if (!secret.trim()) {
      setError('Lütfen Supabase Anon / API Anahtarını girin.');
      return;
    }
    if (!configUsername.trim()) {
      setError('Lütfen bir yönetici kullanıcı adı belirleyin.');
      return;
    }
    if (!configPassword.trim()) {
      setError('Lütfen bir yönetici şifresi belirleyin.');
      return;
    }
    if (!year.trim()) {
      setError('Lütfen çalışılacak mali yılı girin (Örn: 2026).');
      return;
    }

    setLoading(true);
    setError('');

    try {
      await saveFirebaseConfig(url.trim(), secret.trim(), 'default', {
        smtpEmail: smtpEmail.trim(),
        smtpPass: smtpPass.trim(),
      });
      await saveActiveYear(year.trim());
      await AsyncStorage.setItem('ermay_supabase_url', url.trim());
      await AsyncStorage.setItem('ermay_supabase_key', secret.trim());
      await AsyncStorage.setItem('ermay_firebase_url', url.trim());
      await AsyncStorage.setItem('ermay_firebase_secret', secret.trim());
      if (smtpEmail.trim()) await AsyncStorage.setItem('ermay_smtp_email', smtpEmail.trim());
      if (smtpPass.trim()) await AsyncStorage.setItem('ermay_smtp_pass', smtpPass.trim());

      // Belirlenen Yönetici Hesabını Kaydet
      await registerInitialUser(configUsername.trim(), configPassword.trim(), smtpEmail.trim());

      // Giriş ekranı bilgilerini doldur
      setUsernameOrEmail(configUsername.trim());
      setPassword(configPassword.trim());
      setRememberMe(true);

      // Kurulum tamamlandı -> Doğrudan kullanıcı giriş ekranına geç
      setMode('user');
      checkAuthStatus();
    } catch (err: any) {
      setError('Veritabanına bağlanılamadı. Bilgilerinizi kontrol edin: ' + (err?.message || ''));
      console.error(err);
    } finally {
      setLoading(false);
    }
  };

  const handleUserLogin = async () => {
    if (!usernameOrEmail.trim()) {
      setError('Lütfen kullanıcı adı veya e-posta girin.');
      return;
    }
    if (!password) {
      setError('Lütfen şifrenizi girin.');
      return;
    }

    setLoading(true);
    setError('');

    try {
      console.log('[LOGIN] loginUser çağrılıyor...', usernameOrEmail.trim());
      const res = await loginUser(usernameOrEmail.trim(), password);
      console.log('[LOGIN] loginUser sonucu:', JSON.stringify(res));
      if (res.success) {
        if (rememberMe) {
          await AsyncStorage.setItem('ermay_remember_me', 'true');
        } else {
          await AsyncStorage.removeItem('ermay_remember_me');
        }
        await AsyncStorage.setItem('ermay_saved_username', usernameOrEmail.trim());
        await AsyncStorage.setItem('ermay_saved_password', password);

        console.log('[LOGIN] Başarılı, çalışma yılı seçimine geçiliyor...');
        setLoggedInUser(res.user);
        setMode('year_selection');
        await loadYearSelectionData();
      } else {
        console.log('[LOGIN] Login başarısız:', res.error);
        setError(res.error || 'Giriş yapılamadı.');
      }
    } catch (err: any) {
      console.error('[LOGIN] Hata:', err?.message || err);
      setError('Sistem hatası. Lütfen daha sonra tekrar deneyin.');
    } finally {
      setLoading(false);
    }
  };


  const handleYearConfirm = async () => {
    if (!selectedYear || !/^\d{4}$/.test(selectedYear.trim())) {
      setError('Lütfen geçerli 4 haneli bir mali yıl girin (Örn: 2026).');
      return;
    }

    setLoading(true);
    setError('');

    try {
      const yr = selectedYear.trim();
      await createNewMaliYil(yr);
      await saveActiveYear(yr);
      onLoginSuccess();
    } catch (err) {
      setError('Çalışma yılı kaydedilirken hata oluştu.');
    } finally {
      setLoading(false);
    }
  };

  const handleSwitchUser = async () => {
    await logoutUser();
    setLoggedInUser(null);
    setMode('user');
    setError('');
  };

  return (
    <SafeAreaView style={styles.container}>
      <KeyboardAvoidingView 
        style={styles.keyboardView} 
        behavior={Platform.OS === 'ios' ? 'padding' : 'height'}
      >
        <ScrollView contentContainerStyle={styles.scrollContent}>
          <View style={styles.formContainer}>
            
            {/* Header section based on mode */}
            {mode === 'year_selection' ? (
              <View style={styles.header}>
                <View style={styles.historyIconBadge}>
                  <History color="#3B82F6" size={44} />
                </View>
                <Text style={styles.title}>Çalışma Yılı Seçimi</Text>
                <Text style={styles.subtitle}>Devam etmek için aşağıdaki listeden bir yıl seçiniz</Text>
              </View>
            ) : (
              <View style={styles.header}>
                <Text style={styles.title}>VK</Text>
                <Text style={styles.subtitle}>
                  {mode === 'config' 
                    ? 'Sistem veritabanı bağlantı ayarlarını yapılandırın' 
                    : 'Kullanıcı hesabı bilgilerinizle giriş yapın'}
                </Text>
              </View>
            )}

            {error ? (
              <View style={styles.errorBox}>
                <Text style={styles.errorText}>{error}</Text>
              </View>
            ) : null}

            {mode === 'config' ? (
              /* CONFIG MODE FORM (Supabase Bulut Kurulumu) */
              <View>
                {/* SUPABASE BULUT VE KULLANICI BİLGİLERİ */}
                <View>
                    <View style={styles.inputGroup}>
                      <View style={styles.labelRow}>
                        <Link color="#64748B" size={16} />
                        <Text style={styles.label}>Supabase Proje URL *</Text>
                      </View>
                      <TextInput
                        style={styles.input}
                        placeholder="https://projeniz.supabase.co"
                        placeholderTextColor="#64748B"
                        value={url}
                        onChangeText={setUrl}
                        autoCapitalize="none"
                        keyboardType="url"
                      />
                    </View>

                    <View style={styles.inputGroup}>
                      <View style={styles.labelRow}>
                        <Key color="#64748B" size={16} />
                        <Text style={styles.label}>Supabase Anon / API Anahtarı *</Text>
                      </View>
                      <TextInput
                        style={styles.input}
                        placeholder="eyJhbGciOi..."
                        placeholderTextColor="#64748B"
                        value={secret}
                        onChangeText={setSecret}
                        secureTextEntry
                        autoCapitalize="none"
                      />
                    </View>

                    <View style={styles.inputGroup}>
                      <View style={styles.labelRow}>
                        <User color="#64748B" size={16} />
                        <Text style={styles.label}>Yönetici Kullanıcı Adı *</Text>
                      </View>
                      <TextInput
                        style={styles.input}
                        placeholder="Kullanıcı adınızı belirleyin"
                        placeholderTextColor="#64748B"
                        value={configUsername}
                        onChangeText={setConfigUsername}
                        autoCapitalize="none"
                      />
                    </View>

                    <View style={styles.inputGroup}>
                      <View style={styles.labelRow}>
                        <Key color="#64748B" size={16} />
                        <Text style={styles.label}>Yönetici Giriş Şifresi *</Text>
                      </View>
                      <TextInput
                        style={styles.input}
                        placeholder="Şifrenizi belirleyin"
                        placeholderTextColor="#64748B"
                        value={configPassword}
                        onChangeText={setConfigPassword}
                        secureTextEntry
                        autoCapitalize="none"
                      />
                    </View>
                  </View>

                {/* HER ZAMAN GÖRÜNEN: ÇALIŞMA YILI & KAYDET */}
                <View style={styles.inputGroup}>
                  <View style={styles.labelRow}>
                    <Calendar color="#64748B" size={16} />
                    <Text style={styles.label}>Varsayılan Çalışma Yılı *</Text>
                  </View>
                  <TextInput
                    style={styles.input}
                    placeholder="Örn: 2026"
                    placeholderTextColor="#64748B"
                    value={year}
                    onChangeText={setYear}
                    keyboardType="numeric"
                    maxLength={4}
                  />
                </View>

                <TouchableOpacity 
                  style={[styles.button, loading && styles.buttonDisabled]} 
                  onPress={handleConfigSave}
                  disabled={loading}
                >
                  {loading ? (
                    <ActivityIndicator color="#FFF" />
                  ) : (
                    <>
                      <Settings color="#FFF" size={18} style={styles.buttonIcon} />
                      <Text style={styles.buttonText}>Kurulumu Tamamla ve Girişe Geç</Text>
                    </>
                  )}
                </TouchableOpacity>
              </View>
            ) : mode === 'user' ? (
              /* USER LOGIN MODE FORM */
              <View>
                <View style={styles.inputGroup}>
                  <View style={styles.labelRow}>
                    <ShieldAlert color="#64748B" size={16} />
                    <Text style={styles.label}>{isAuthEnabled ? 'E-Posta Adresi' : 'Kullanıcı Adı / E-Posta'}</Text>
                  </View>
                  <TextInput
                    style={styles.input}
                    placeholder={isAuthEnabled ? "kullanici@ermay.com" : "admin"}
                    placeholderTextColor="#64748B"
                    value={usernameOrEmail}
                    onChangeText={setUsernameOrEmail}
                    autoCapitalize="none"
                    keyboardType={isAuthEnabled ? "email-address" : "default"}
                  />
                </View>

                <View style={styles.inputGroup}>
                  <View style={styles.labelRow}>
                    <Key color="#64748B" size={16} />
                    <Text style={styles.label}>Şifre</Text>
                  </View>
                  <TextInput
                    style={styles.input}
                    placeholder="••••••••"
                    placeholderTextColor="#64748B"
                    value={password}
                    onChangeText={setPassword}
                    secureTextEntry
                    autoCapitalize="none"
                  />
                </View>

                <TouchableOpacity 
                  style={styles.rememberMeContainer} 
                  onPress={() => setRememberMe(!rememberMe)}
                  disabled={loading}
                >
                  <View style={[styles.checkbox, rememberMe && styles.checkboxChecked]}>
                    {rememberMe && <CheckCircle2 color="#FFFFFF" size={14} />}
                  </View>
                  <Text style={styles.rememberMeText}>Beni Hatırla</Text>
                </TouchableOpacity>

                <TouchableOpacity 
                  style={[styles.button, loading && styles.buttonDisabled]} 
                  onPress={handleUserLogin}
                  disabled={loading}
                >
                  {loading ? (
                    <ActivityIndicator color="#FFF" />
                  ) : (
                    <>
                      <LogIn color="#FFF" size={18} style={styles.buttonIcon} />
                      <Text style={styles.buttonText}>Giriş Yap</Text>
                    </>
                  )}
                </TouchableOpacity>


                <TouchableOpacity 
                  style={styles.switchModeButton}
                  onPress={() => setMode('config')}
                  disabled={loading}
                >
                  <Text style={styles.switchModeText}>Veritabanı Ayarlarını Düzenle</Text>
                </TouchableOpacity>
              </View>
            ) : (
              /* YEAR SELECTION MODE FORM (MATCHES DESKTOP VIEW) */
              <View>
                {/* User Info Badge */}
                <View style={styles.userBadgeCard}>
                  <User color="#3B82F6" size={18} style={{ marginRight: 8 }} />
                  <Text style={styles.userBadgeText}>
                    Hesap: <Text style={{ color: '#FFF', fontWeight: 'bold' }}>{loggedInUser?.unvan || loggedInUser?.name || loggedInUser?.username || usernameOrEmail}</Text>
                  </Text>
                </View>

                {/* Years List Container */}
                <View style={styles.yearsCardContainer}>
                  {loading && availableYears.length === 0 ? (
                    <View style={{ padding: 24, alignItems: 'center' }}>
                      <ActivityIndicator color="#3B82F6" size="large" />
                      <Text style={{ color: '#94A3B8', marginTop: 8, fontSize: 13 }}>Mali yıllar canlı veritabanından kontrol ediliyor...</Text>
                    </View>
                  ) : availableYears.length === 0 ? (
                    <View style={{ padding: 20, alignItems: 'center' }}>
                      <Calendar color="#3B82F6" size={40} style={{ marginBottom: 12 }} />
                      <Text style={{ color: '#FFFFFF', fontSize: 17, fontWeight: 'bold', marginBottom: 6, textAlign: 'center' }}>
                        Kayıtlı Mali Yıl Bulunamadı
                      </Text>
                      <Text style={{ color: '#94A3B8', fontSize: 13, textAlign: 'center', marginBottom: 16, lineHeight: 18 }}>
                        Sistemde henüz bir çalışma yılı tanımlanmamış. Başlamak için lütfen ilk mali yılınızı oluşturun:
                      </Text>
                      <View style={{ width: '100%', marginBottom: 12 }}>
                        <TextInput
                          style={[styles.input, { textAlign: 'center', fontSize: 22, fontWeight: 'bold', letterSpacing: 2 }]}
                          placeholder={new Date().getFullYear().toString()}
                          placeholderTextColor="#64748B"
                          value={selectedYear}
                          onChangeText={setSelectedYear}
                          keyboardType="numeric"
                          maxLength={4}
                        />
                      </View>
                      <TouchableOpacity 
                        style={[styles.startAppButton, { width: '100%', marginTop: 0 }]} 
                        onPress={handleYearConfirm}
                        disabled={loading}
                      >
                        <Text style={styles.startAppButtonText}>Mali Yılı Oluştur ve Başlat</Text>
                      </TouchableOpacity>
                    </View>
                  ) : (
                    availableYears.map((y) => {
                      const isSelected = selectedYear === y;
                      return (
                        <TouchableOpacity
                          key={y}
                          style={[styles.yearListItem, isSelected && styles.yearListItemSelected]}
                          onPress={() => setSelectedYear(y)}
                        >
                          <Calendar color={isSelected ? "#FFFFFF" : "#3B82F6"} size={26} />
                          <Text style={[styles.yearListText, isSelected && styles.yearListTextSelected]}>
                            {y}
                          </Text>
                          <ChevronRight color={isSelected ? "#FFFFFF" : "rgba(255,255,255,0.4)"} size={20} />
                        </TouchableOpacity>
                      );
                    })
                  )}
                </View>

                {/* Custom Year Option Toggle & Primary Button (Listedeki yıllar varken) */}
                {availableYears.length > 0 && (
                  <>
                    {!showCustomInput ? (
                      <TouchableOpacity style={styles.customYearToggle} onPress={() => setShowCustomInput(true)}>
                        <Text style={styles.customYearToggleText}>+ Listede olmayan farklı bir yıl yazın</Text>
                      </TouchableOpacity>
                    ) : (
                      <View style={styles.inputGroup}>
                        <Text style={styles.label}>Özel Mali Yıl Girin</Text>
                        <TextInput
                          style={[styles.input, { marginTop: 6 }]}
                          placeholder="Örn: 2026"
                          placeholderTextColor="#64748B"
                          value={selectedYear}
                          onChangeText={setSelectedYear}
                          keyboardType="numeric"
                          maxLength={4}
                        />
                      </View>
                    )}

                    <TouchableOpacity 
                      style={[styles.startAppButton, loading && styles.buttonDisabled]} 
                      onPress={handleYearConfirm}
                      disabled={loading}
                    >
                      {loading ? (
                        <ActivityIndicator color="#FFF" />
                      ) : (
                        <Text style={styles.startAppButtonText}>Uygulamayı Başlat</Text>
                      )}
                    </TouchableOpacity>
                  </>
                )}

                {/* Footer Note */}
                <Text style={styles.footerNoteText}>
                  Yeni yıl devir işlemleri artık Ayarlar menüsü altından yönetilebilir.
                </Text>

                {/* Switch User */}
                <TouchableOpacity 
                  style={styles.switchModeButton}
                  onPress={handleSwitchUser}
                  disabled={loading}
                >
                  <Text style={styles.switchModeText}>Farklı Kullanıcı ile Giriş Yap</Text>
                </TouchableOpacity>
              </View>
            )}
          </View>
        </ScrollView>
      </KeyboardAvoidingView>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#0A0A0A',
  },
  keyboardView: {
    flex: 1,
  },
  scrollContent: {
    flexGrow: 1,
    justifyContent: 'center',
    padding: 20,
  },
  formContainer: {
    width: '100%',
    maxWidth: 450,
    alignSelf: 'center',
  },
  header: {
    marginBottom: 28,
    alignItems: 'center',
  },
  historyIconBadge: {
    width: 80,
    height: 80,
    borderRadius: 24,
    backgroundColor: 'rgba(59, 130, 246, 0.1)',
    justifyContent: 'center',
    alignItems: 'center',
    marginBottom: 16,
  },
  title: {
    fontSize: 28,
    fontWeight: '900',
    color: '#FFFFFF',
    marginBottom: 6,
    letterSpacing: 0.5,
    textAlign: 'center',
  },
  subtitle: {
    fontSize: 14,
    color: '#94A3B8',
    textAlign: 'center',
    paddingHorizontal: 10,
    lineHeight: 20,
    opacity: 0.8,
  },
  userBadgeCard: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: 'rgba(255, 255, 255, 0.04)',
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.08)',
    paddingVertical: 10,
    paddingHorizontal: 16,
    borderRadius: 99,
    marginBottom: 20,
    alignSelf: 'center',
  },
  userBadgeText: {
    color: '#94A3B8',
    fontSize: 13,
  },
  yearsCardContainer: {
    backgroundColor: '#161616',
    borderRadius: 20,
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.08)',
    padding: 12,
    marginBottom: 16,
    gap: 8,
  },
  yearListItem: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingVertical: 14,
    paddingHorizontal: 18,
    borderRadius: 14,
    backgroundColor: 'rgba(255, 255, 255, 0.02)',
  },
  yearListItemSelected: {
    backgroundColor: '#2563EB',
  },
  yearListText: {
    flex: 1,
    fontSize: 22,
    fontWeight: '900',
    color: '#FFFFFF',
    marginLeft: 16,
  },
  yearListTextSelected: {
    color: '#FFFFFF',
  },
  customYearToggle: {
    alignItems: 'center',
    paddingVertical: 8,
    marginBottom: 12,
  },
  customYearToggleText: {
    color: '#60A5FA',
    fontSize: 13,
    fontWeight: '600',
  },
  startAppButton: {
    backgroundColor: '#2563EB',
    height: 56,
    borderRadius: 16,
    alignItems: 'center',
    justifyContent: 'center',
    marginTop: 8,
    shadowColor: '#2563EB',
    shadowOffset: { width: 0, height: 4 },
    shadowOpacity: 0.4,
    shadowRadius: 10,
    elevation: 4,
  },
  startAppButtonText: {
    color: '#FFFFFF',
    fontSize: 17,
    fontWeight: '900',
  },
  footerNoteText: {
    color: '#64748B',
    fontSize: 12,
    textAlign: 'center',
    marginTop: 14,
    opacity: 0.7,
  },
  errorBox: {
    backgroundColor: 'rgba(239, 68, 68, 0.1)',
    borderWidth: 1,
    borderColor: 'rgba(239, 68, 68, 0.3)',
    padding: 14,
    borderRadius: 12,
    marginBottom: 20,
  },
  errorText: {
    color: '#EF4444',
    fontSize: 13,
    textAlign: 'center',
    fontWeight: '600',
  },
  inputGroup: {
    marginBottom: 20,
  },
  labelRow: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: 8,
  },
  label: {
    color: '#94A3B8',
    fontSize: 13,
    fontWeight: '600',
    marginLeft: 6,
  },
  input: {
    backgroundColor: '#2A2A2A',
    borderWidth: 1,
    borderColor: '#444',
    borderRadius: 12,
    padding: 16,
    color: '#FFFFFF',
    fontSize: 15,
  },
  button: {
    flexDirection: 'row',
    backgroundColor: '#0061FF',
    padding: 16,
    borderRadius: 12,
    alignItems: 'center',
    justifyContent: 'center',
    marginTop: 12,
    shadowColor: '#0061FF',
    shadowOffset: { width: 0, height: 4 },
    shadowOpacity: 0.3,
    shadowRadius: 8,
    elevation: 4,
  },
  buttonIcon: {
    marginRight: 8,
  },
  buttonDisabled: {
    opacity: 0.7,
  },
  buttonText: {
    color: '#FFFFFF',
    fontSize: 16,
    fontWeight: 'bold',
  },
  switchModeButton: {
    alignItems: 'center',
    marginTop: 18,
    paddingVertical: 10,
  },
  switchModeText: {
    color: '#64748B',
    fontSize: 14,
    fontWeight: '600',
  },
  checkbox: {
    width: 20,
    height: 20,
    borderRadius: 6,
    borderWidth: 2,
    borderColor: '#444',
    marginRight: 10,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: '#2A2A2A',
  },
  checkboxChecked: {
    backgroundColor: '#0061FF',
    borderColor: '#0061FF',
  },
  rememberMeContainer: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: 20,
    marginTop: -8,
  },
  rememberMeText: {
    color: '#94A3B8',
    fontSize: 14,
    fontWeight: '500',
  },
  dividerRow: {
    flexDirection: 'row',
    alignItems: 'center',
    marginVertical: 14,
  },
  dividerLine: {
    flex: 1,
    height: 1,
    backgroundColor: '#334155',
  },
  dividerText: {
    color: '#64748B',
    fontSize: 11,
    fontWeight: '700',
    marginHorizontal: 12,
  },
  googleButton: {
    flexDirection: 'row',
    backgroundColor: '#1E293B',
    borderWidth: 1,
    borderColor: '#334155',
    padding: 15,
    borderRadius: 12,
    alignItems: 'center',
    justifyContent: 'center',
  },
  googleIconText: {
    color: '#EA4335',
    fontSize: 18,
    fontWeight: '900',
    marginRight: 10,
  },
  googleButtonText: {
    color: '#F8FAFC',
    fontSize: 15,
    fontWeight: '600',
  },
  tabContainer: {
    flexDirection: 'row',
    backgroundColor: '#1E293B',
    borderRadius: 12,
    padding: 4,
    marginBottom: 20,
  },
  tabButton: {
    flex: 1,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    paddingVertical: 10,
    borderRadius: 8,
  },
  tabButtonActive: {
    backgroundColor: '#0F172A',
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.2,
    shadowRadius: 4,
    elevation: 2,
  },
  tabButtonText: {
    color: '#94A3B8',
    fontSize: 13,
    fontWeight: '600',
  },
  tabButtonTextActive: {
    color: '#38BDF8',
    fontWeight: '700',
  },
  infoBox: {
    backgroundColor: 'rgba(59, 130, 246, 0.1)',
    borderWidth: 1,
    borderColor: 'rgba(59, 130, 246, 0.25)',
    borderRadius: 10,
    padding: 12,
    marginBottom: 16,
  },
  infoBoxText: {
    color: '#93C5FD',
    fontSize: 12,
    lineHeight: 18,
  },
});

