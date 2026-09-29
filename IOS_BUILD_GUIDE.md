# iOS Build - GitHub Actions ile IPA Oluşturma

Bu workflow, Expo/EAS kullanarak iOS `.ipa` dosyası üretir. **Apple Developer Program üyeliği ($99/yıl) gerekir.**

## Gerekli GitHub Secrets

GitHub repo ayarlarından **Settings > Secrets and variables > Actions > New repository secret** ekleyin:

### Zorunlu (EAS Build için)
| Secret | Açıklama | Nasıl Alınır |
|--------|----------|--------------|
| `EXPO_TOKEN` | Expo/EAS erişim tokenı | `eas login` → `expo token:create` |

### Apple Developer Hesabı (İmzalama için)
| Secret | Açıklama |
|--------|----------|
| `APPLE_ID` | Apple Developer hesap e-postası |
| `APPLE_TEAM_ID` | Apple Developer Team ID (10 karakterli, örn: `ABCD1234EF`) |
| `ASC_API_KEY_ID` | App Store Connect API Key ID |
| `ASC_API_KEY_ISSUER_ID` | App Store Connect Issuer ID |
| `ASC_API_KEY_P8` | App Store Connect `.p8` private key içeriği (base64 encoded) |

### Fastlane Match (Sertifika yönetimi için - Opsiyonel ama önerilir)
| Secret | Açıklama |
|--------|----------|
| `MATCH_PASSWORD` | Match repo şifresi |
| `MATCH_GIT_URL` | Match git repo URL (örn: `https://github.com/kullanici/certificates.git`) |

## Apple Developer Hesabı Kurulumu

### 1. App Store Connect API Key Oluşturun
1. [App Store Connect > Users and Access > Keys](https://appstoreconnect.apple.com/access/api) gidin
2. "Generate API Key" → Name: "GitHub Actions" → Access: "Admin" → Generate
3. **Key ID** (ASC_API_KEY_ID) ve **Issuer ID** (ASC_API_KEY_ISSUER_ID) kopyalayın
4. `.p8` dosyasını indirin
5. `.p8` dosyasını base64 encode edin:
   ```bash
   base64 -i AuthKey_XXXXXXXXXX.p8 | pbcopy  # macOS
   # veya
   cat AuthKey_XXXXXXXXXX.p8 | base64 -w 0  # Linux
   ```
6. Sonucu `ASC_API_KEY_P8` secret'ına yapıştırın

### 2. Team ID Bulun
- [Apple Developer > Membership](https://developer.apple.com/account/#/membership/) → Team ID kopyalayın

### 3. Fastlane Match (Opsiyonel ama önerilir)
Sertifikaları paylaşmak için ayrı bir git repo oluşturun:
```bash
# Yeni repo oluşturun (private)
# Sonra local'de:
fastlane match init
# Git URL'ini girin, şifre belirleyin
fastlane match appstore --git_url "https://github.com/kullanici/certificates.git"
```
Bu işlem sertifikaları ve provisioning profillerini güvenli bir şekilde depolar.

## Build Çalıştırma

1. **GitHub Actions** sekmesine gidin
2. **"iOS Build & IPA"** workflow'unu seçin
3. **"Run workflow"** butonuna tıklayın
4. Build türünü seçin:
   - **production**: App Store / TestFlight / Ad Hoc dağıtım için `.ipa` (gerçek cihaz)
   - **preview**: Internal dağıtım için `.ipa` (gerçek cihaz)
   - **simulator**: iOS Simulator için `.app` (Mac bilgisayarda test)
5. İsteğe bağlı versiyon numarası girin (örn: `1.0.1`)
6. **"Run workflow"** butonuna basın

## Sonuçlar

Build tamamlandığında:
- **Artifacts** sekmesinden `vk-muhasebe-ipa` indirilebilir
- **Production** build için: GitHub Releases sayfasında `.ipa` dosyası bulunur
- **Simulator** build için: `vk-muhasebe-simulator-app` artifact olarak indirilebilir

## Telefonunuza Yükleme

### Yöntem 1: TestFlight (Önerilen)
1. IPA'yı App Store Connect'e yükleyin: `xcrun altool --upload-app -f vk-muhasebe.ipa -t ios --apiKey $ASC_API_KEY_ID --apiIssuer $ASC_API_KEY_ISSUER_ID`
2. TestFlight'ta test edin

### Yöntem 2: Ad Hoc / Enterprise (UDID gerekli)
1. Apple Developer portalda cihaz UDID'inizi ekleyin
2. Provisioning profiline cihazı ekleyin
3. IPA'yi diawi.com veya installonair.com gibi servislerle yükleyin

### Yöntem 3: Xcode (Mac bilgisayar gerekli)
1. IPA'yi indirin
2. Xcode > Window > Devices and Simulators
3. Telefonu bağlayın → "+" → IPA seçin

## Sorun Giderme

| Hata | Çözüm |
|------|-------|
| "No profile for team" | Fastlane Match çalıştırın veya Apple Developer portalda profil oluşturun |
| "Certificate not found" | `fastlane match appstore` çalıştırın |
| "Bundle ID already exists" | `app.json` içinde `bundleIdentifier` değiştirin |
| "EXPO_TOKEN invalid" | Yeni token oluşturun: `expo token:create` |

## Yerel Build (Mac bilgisayarınız varsa)

```bash
cd ermaymuhasebe-mobil
npm install -g eas-cli
eas login
eas build --platform ios --profile production
```

Build tamamlandığında EAS size indirme linki verecektir.