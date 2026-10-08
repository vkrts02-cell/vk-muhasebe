import React, { useState, useEffect } from 'react';
import {
  StyleSheet,
  Text,
  View,
  SafeAreaView,
  TextInput,
  ActivityIndicator,
  TouchableOpacity,
  Modal,
  ScrollView,
  Alert,
  KeyboardAvoidingView,
  Platform
} from 'react-native';
import {
  Search,
  Plus,
  Save,
  Edit3,
  Trash2,
  Calendar,
  ChevronLeft,
  ChevronRight,
  Box,
  TrendingUp,
  AlertCircle,
  CheckCircle2,
  Info,
  Printer,
  FileText,
  Download,
  Upload,
  Table,
  X,
  PlusSquare,
  MinusSquare,
  CheckSquare,
  Square,
  Layers,
  Folder,
  FolderPlus,
  CornerDownRight
} from 'lucide-react-native';
import { useNavigation } from '@react-navigation/native';
import { subscribeToPath, writeData, deleteData, readData, isStockInflowMovement } from '../services/firebase';
import { IOSGlassCalendar } from '../components/IOSGlassCalendar';
import { OfflineNetworkBar } from '../components/OfflineNetworkBar';
import { AppleListRow } from '../components/AppleGroupedList';
import { ShimmerCardList } from '../components/Shimmer';
import { AppleTheme } from '../theme/appleDesign';
import { generateReportPdf } from '../services/pdfService';
import { exportToExcel, importFromExcel } from '../services/excelService';
import { generateInt32Id } from '../utils/IdGenerator';
import { KeyboardDoneAccessory, KEYBOARD_ACCESSORY_ID } from '../components/KeyboardDoneAccessory';

const formatMoney = (val: number) => {
  return new Intl.NumberFormat('tr-TR', { style: 'currency', currency: 'TRY', minimumFractionDigits: 2 }).format(val || 0);
};

export default function StoklarScreen() {
  const navigation = useNavigation<any>();

  // Data State
  const [stoklar, setStoklar] = useState<any[]>([]);
  const [filteredStoklar, setFilteredStoklar] = useState<any[]>([]);
  const [stokHareketler, setStokHareketler] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  const [stokGruplar, setStokGruplar] = useState<string[]>([]);
  const [groupList, setGroupList] = useState<string[]>([]);
  const birimler = ['Adet', 'Kg', 'Mt', 'M2'];

  // Filter & Paging State
  const [filterKod, setFilterKod] = useState('');
  const [onlyWithBalance, setOnlyWithBalance] = useState(false);
  const [pageSize, setPageSize] = useState(50);
  const [currentPageIndex, setCurrentPageIndex] = useState(0);
  const [selectedStokIds, setSelectedStokIds] = useState<any[]>([]);

  // Selection State
  const [selectedStok, setSelectedStok] = useState<any | null>(null);
  const [selectedStokHareket, setSelectedStokHareket] = useState<any | null>(null);

  // Form (Edit) State
  const [editBarkod, setEditBarkod] = useState('');
  const [editStokKodu, setEditStokKodu] = useState('');
  const [editStokAdi, setEditStokAdi] = useState('');
  const [editKategori, setEditKategori] = useState('');
  const [editBirim, setEditBirim] = useState('Kg');
  const [editAlisFiyati, setEditAlisFiyati] = useState('0');
  const [editOrtalamaAlisFiyati, setEditOrtalamaAlisFiyati] = useState('0');
  const [editSatisFiyati, setEditSatisFiyati] = useState('0');
  const [editOrtalamaSatisFiyati, setEditOrtalamaSatisFiyati] = useState('0');
  const [editKdv, setEditKdv] = useState('20');
  const [editAcilisBakiye, setEditAcilisBakiye] = useState('0');

  // Messages (Banners & Status)
  const [errorMessage, setErrorMessage] = useState('');
  const [successMessage, setSuccessMessage] = useState('');
  const [statusMessage, setStatusMessage] = useState('');

  // Modals
  const [isCalendarVisible, setIsCalendarVisible] = useState(false);
  const [selectedCalendarDate, setSelectedCalendarDate] = useState(new Date().toISOString().split('T')[0]);
  const [isAddGroupVisible, setIsAddGroupVisible] = useState(false);
  const [newGroupName, setNewGroupName] = useState('');
  const [selectedParentGroup, setSelectedParentGroup] = useState('');
  const [groupSearchQuery, setGroupSearchQuery] = useState('');
  const [isGroupSelectVisible, setIsGroupSelectVisible] = useState(false);
  const [isBirimSelectVisible, setIsBirimSelectVisible] = useState(false);

  // Transaction Popup Logic
  const [isTransactionWindowVisible, setIsTransactionWindowVisible] = useState(false);
  const [transactionType, setTransactionType] = useState('GİRİŞ');
  const [isEditingTransaction, setIsEditingTransaction] = useState(false);
  const [islemTarihStr, setIslemTarihStr] = useState(new Date().toLocaleDateString('tr-TR'));
  const [islemMiktar, setIslemMiktar] = useState('1');
  const [islemFiyat, setIslemFiyat] = useState('0');
  const [islemAciklama, setIslemAciklama] = useState('');

  // Delete Confirm Modal
  const [isConfirmVisible, setIsConfirmVisible] = useState(false);
  const [confirmTitle, setConfirmTitle] = useState('');
  const [confirmMessage, setConfirmMessage] = useState('');
  const [confirmAction, setConfirmAction] = useState<(() => Promise<void>) | null>(null);

  // Menus for Reports & Excel
  const [isReportsMenuVisible, setIsReportsMenuVisible] = useState(false);
  const [isExcelMenuVisible, setIsExcelMenuVisible] = useState(false);

  // Normalization Helper for StokKart
  const normalizeStok = (raw: any, key: string) => {
    return {
      firebaseKey: key,
      id: raw.id ?? raw.Id ?? (isNaN(Number(key)) ? generateInt32Id() : Number(key)),
      stokKodu: raw.stokKodu ?? raw.StokKodu ?? '',
      stokAdi: raw.stokAdi ?? raw.StokAdi ?? '',
      barkod: raw.barkod ?? raw.Barkod ?? '',
      kategori: raw.kategori ?? raw.Kategori ?? raw.grup ?? raw.Grup ?? '',
      birim: raw.birim ?? raw.Birim ?? 'Kg',
      alisFiyati: Number(raw.alisFiyati ?? raw.AlisFiyati ?? 0),
      ortalamaAlisFiyati: Number(raw.ortalamaAlisFiyati ?? raw.ortAlisFiyati ?? raw.OrtalamaAlisFiyati ?? 0),
      satisFiyati: Number(raw.satisFiyati ?? raw.SatisFiyati ?? 0),
      ortalamaSatisFiyati: Number(raw.ortalamaSatisFiyati ?? raw.ortSatisFiyati ?? raw.OrtalamaSatisFiyati ?? 0),
      kdv: Number(raw.kdv ?? raw.KDV ?? raw.kdvOrani ?? 20),
      miktar: Number(raw.miktar ?? raw.Miktar ?? 0),
      isDeleted: raw.isDeleted === true || raw.IsDeleted === true,
      kayitTarihi: raw.kayitTarihi ?? raw.KayitTarihi ?? new Date().toISOString()
    };
  };

  // Normalization Helper for StokHareket
  const normalizeHareket = (raw: any, key: string) => {
    const islemTuru = raw.islemTuru ?? raw.IslemTuru ?? raw.hareketTuru ?? raw.hareket_turu ?? 'GİRİŞ';
    const miktar = Number(raw.miktar ?? raw.Miktar ?? 0);
    const isGiris = isStockInflowMovement(islemTuru) || (Number(raw.giren ?? raw.Giren ?? 0) > 0 && Number(raw.cikan ?? raw.Cikan ?? 0) <= 0);
    return {
      firebaseKey: key,
      id: raw.id ?? raw.Id ?? (isNaN(Number(key)) ? generateInt32Id() : Number(key)),
      stokId: raw.stokId ?? raw.StokId ?? raw.stokKartId ?? raw.StokKartId ?? 0,
      stokKodu: raw.stokKodu ?? raw.StokKodu ?? '',
      stokAdi: raw.stokAdi ?? raw.StokAdi ?? '',
      tarih: raw.tarih ?? raw.Tarih ?? new Date().toISOString(),
      islemTuru,
      hareketTuru: islemTuru,
      miktar,
      fiyat: Number(raw.fiyat ?? raw.Fiyat ?? 0),
      aciklama: raw.aciklama ?? raw.Aciklama ?? '',
      giren: isGiris ? miktar : 0,
      cikan: !isGiris ? miktar : 0,
      isGiris,
      isDeleted: raw.isDeleted === true || raw.IsDeleted === true
    };
  };

  // --- FIREBASE SYNC ---
  useEffect(() => {
    const unsubStok = subscribeToPath('Stoklar', (data) => {
      let list: any[] = [];
      if (data) {
        if (Array.isArray(data)) {
          list = data.filter(Boolean).map((item, idx) => normalizeStok(item, item.firebaseKey || item.id?.toString() || idx.toString()));
        } else if (typeof data === 'object') {
          list = Object.keys(data).map(k => normalizeStok(data[k], k));
        }
      }
      const activeList = list.filter(s => !s.isDeleted);
      setStoklar(activeList);
      setLoading(false);
    });

    const unsubGruplar = subscribeToPath('StokGruplar', (data) => {
      let list: string[] = [];
      if (data) {
        if (Array.isArray(data)) {
          list = data
            .filter(Boolean)
            .filter((x: any) => x.isDeleted !== true && x.IsDeleted !== true)
            .map((x: any) => (typeof x === 'string' ? x : (x.ad || x.Ad || x.grup || x.Grup || '')))
            .filter(Boolean);
        } else if (typeof data === 'object') {
          list = Object.keys(data)
            .map(k => {
              const item = data[k];
              if (!item || item.isDeleted === true || item.IsDeleted === true) return '';
              return typeof item === 'string' ? item : (item.ad || item.Ad || item.grup || item.Grup || k);
            })
            .filter(Boolean);
        }
      }
      setStokGruplar(Array.from(new Set(list)));
    });

    const unsubHareketler = subscribeToPath('StokHareketler', (data) => {
      let list: any[] = [];
      if (data) {
        if (Array.isArray(data)) {
          list = data.filter(Boolean).map((item, idx) => normalizeHareket(item, item.firebaseKey || item.id?.toString() || idx.toString()));
        } else if (typeof data === 'object') {
          list = Object.keys(data).map(k => normalizeHareket(data[k], k));
        }
      }
      setStokHareketler(list.filter(h => !h.isDeleted));
    });

    return () => {
      unsubStok();
      unsubGruplar();
      unsubHareketler();
    };
  }, []);

  // --- MERGE ALL UNIQUE GROUPS ---
  useEffect(() => {
    const fromStocks = stoklar.map(x => x.kategori).filter(Boolean);
    const combined = Array.from(
      new Set([...stokGruplar, ...fromStocks, ...(editKategori ? [editKategori] : [])])
    )
      .map(g => (g || '').trim())
      .filter(Boolean)
      .sort((a, b) => a.localeCompare(b, 'tr-TR'));
    setGroupList(combined);
  }, [stoklar, stokGruplar, editKategori]);

  // Update selected stok when data changes
  useEffect(() => {
    if (selectedStok) {
      const updated = stoklar.find(s => s.id === selectedStok.id || s.firebaseKey === selectedStok.firebaseKey);
      if (updated && JSON.stringify(updated) !== JSON.stringify(selectedStok)) {
        setSelectedStok(updated);
      }
    }
  }, [stoklar]);

  // --- FILTERING ---
  useEffect(() => {
    let result = stoklar;
    if (filterKod.trim()) {
      const lower = filterKod.toLocaleLowerCase('tr-TR').trim();
      result = result.filter(s =>
        (s.stokKodu && s.stokKodu.toLocaleLowerCase('tr-TR').includes(lower)) ||
        (s.stokAdi && s.stokAdi.toLocaleLowerCase('tr-TR').includes(lower)) ||
        (s.kategori && s.kategori.toLocaleLowerCase('tr-TR').includes(lower)) ||
        (s.barkod && s.barkod.toLocaleLowerCase('tr-TR').includes(lower))
      );
    }
    if (onlyWithBalance) {
      result = result.filter(s => Math.abs(Number(s.miktar || 0)) > 0.0001);
    }
    setFilteredStoklar(result);
    setCurrentPageIndex(0);
  }, [stoklar, filterKod, onlyWithBalance]);

  // Paging
  const totalPages = Math.max(1, Math.ceil(filteredStoklar.length / pageSize));
  const pagedStoklar = filteredStoklar.slice(currentPageIndex * pageSize, (currentPageIndex + 1) * pageSize);
  const pagerInfo = `${currentPageIndex + 1} / ${totalPages} (${filteredStoklar.length})`;

  // Derived state
  const isEditMode = selectedStok != null;
  const formTitle = selectedStok == null ? 'Yeni Stok Kartı Ekle' : `Stok Kartını Düzenle: ${selectedStok.stokAdi || selectedStok.stokKodu}`;
  const formTitleColor = selectedStok == null ? '#60A5FA' : '#F59E0B';

  // --- STOK HAREKETLERİ (Chronological Sort & Running Balance) ---
  const currentStokHareketler = stokHareketler.filter(
    h => (selectedStok?.id && h.stokId === selectedStok.id) || (selectedStok?.stokKodu && h.stokKodu === selectedStok.stokKodu)
  );
  const chronologicalHareketler = [...currentStokHareketler].sort(
    (a, b) => new Date(a.tarih || 0).getTime() - new Date(b.tarih || 0).getTime()
  );

  let runningBalance = 0;
  const processedHareketler = chronologicalHareketler.map(h => {
    const islemTuru = h.hareketTuru || h.islemTuru || '';
    const isGiris = isStockInflowMovement(islemTuru) || (Number(h.giren) > 0 && Number(h.cikan) <= 0);
    const miktar = Number(h.miktar || (isGiris ? h.giren : h.cikan) || 0);
    const giren = isGiris ? miktar : 0;
    const cikan = !isGiris ? miktar : 0;

    if (isGiris) {
      runningBalance += miktar;
    } else {
      runningBalance -= miktar;
    }

    return { ...h, islemTuru, hareketTuru: islemTuru, giren, cikan, isGiris, kalanMiktar: runningBalance };
  }).reverse();

  // --- FORM HELPERS ---
  const clearMessages = () => {
    setErrorMessage('');
    setSuccessMessage('');
    setStatusMessage('');
  };

  const fillEditForm = (stok: any) => {
    setEditBarkod(stok.barkod || '');
    setEditStokKodu(stok.stokKodu || '');
    setEditStokAdi(stok.stokAdi || '');
    setEditKategori(stok.kategori || '');
    setEditBirim(stok.birim || 'Kg');
    setEditAlisFiyati((stok.alisFiyati ?? 0).toString());
    setEditOrtalamaAlisFiyati((stok.ortalamaAlisFiyati ?? 0).toString());
    setEditSatisFiyati((stok.satisFiyati ?? 0).toString());
    setEditOrtalamaSatisFiyati((stok.ortalamaSatisFiyati ?? 0).toString());
    setEditKdv((stok.kdv ?? 20).toString());
    setEditAcilisBakiye((stok.miktar ?? 0).toString());
  };

  const clearEditForm = () => {
    setEditBarkod('');
    setEditStokKodu('');
    setEditStokAdi('');
    setEditKategori('');
    setEditBirim('Kg');
    setEditAlisFiyati('0');
    setEditOrtalamaAlisFiyati('0');
    setEditSatisFiyati('0');
    setEditOrtalamaSatisFiyati('0');
    setEditKdv('20');
    setEditAcilisBakiye('0');
  };

  const handleSelectStok = (stok: any) => {
    clearMessages();
    if (selectedStok?.id === stok.id) {
      setSelectedStok(null);
      clearEditForm();
      setSelectedStokHareket(null);
    } else {
      setSelectedStok(stok);
      fillEditForm(stok);
      setSelectedStokHareket(null);
    }
  };

  const handleCreateNew = () => {
    clearMessages();
    setSelectedStok(null);
    clearEditForm();
    setSelectedStokHareket(null);
  };

  const handleCancelEdit = () => {
    clearMessages();
    if (selectedStok) {
      fillEditForm(selectedStok);
    } else {
      clearEditForm();
    }
  };

  // --- RECALCULATE COSTS ---
  const recalculateCosts = async (stokId: number, excludeId?: number, excludeKey?: string) => {
    try {
      setStatusMessage('Maliyetler hesaplanıyor...');
      const har = stokHareketler.filter(h => 
        h.stokId === stokId && 
        !h.isDeleted &&
        (!excludeId || h.id !== excludeId) &&
        (!excludeKey || h.firebaseKey !== excludeKey)
      );
      har.sort((a, b) => new Date(a.tarih || 0).getTime() - new Date(b.tarih || 0).getTime());

      let totalGirenMiktar = 0;
      let totalGirenMaliyet = 0;
      let totalCikanMiktar = 0;
      let totalCikanMaliyet = 0;

      for (const h of har) {
        let m = Number(h.miktar || 0);
        let f = Number(h.fiyat || 0);
        let tur = (h.islemTuru || '').toUpperCase();

        let isGiris = (h.giren && Number(h.giren) > 0) || tur.includes('GİRİŞ') || tur.includes('ALIŞ') || tur.includes('AÇILIŞ');
        let isCikis = (h.cikan && Number(h.cikan) > 0) || tur.includes('ÇIKIŞ') || tur.includes('SATIŞ');

        if (isGiris) {
          totalGirenMiktar += m;
          totalGirenMaliyet += m * f;
        } else if (isCikis) {
          totalCikanMiktar += m;
          totalCikanMaliyet += m * f;
        }
      }

      const ortAlis = totalGirenMiktar > 0 ? totalGirenMaliyet / totalGirenMiktar : 0;
      const ortSatis = totalCikanMiktar > 0 ? totalCikanMaliyet / totalCikanMiktar : 0;

      const s = stoklar.find(x => x.id === stokId);
      if (s && s.firebaseKey) {
        await writeData(`Stoklar/${s.firebaseKey}/ortalamaAlisFiyati`, Number(ortAlis.toFixed(2)));
        await writeData(`Stoklar/${s.firebaseKey}/ortalamaSatisFiyati`, Number(ortSatis.toFixed(2)));
        if (har.length === 0) {
          await writeData(`Stoklar/${s.firebaseKey}/miktar`, 0);
          await writeData(`Stoklar/${s.firebaseKey}/alisFiyati`, 0);
          await writeData(`Stoklar/${s.firebaseKey}/satisFiyati`, 0);
        }
      }
      setStatusMessage('Maliyet hesaplama tamamlandı.');
    } catch (e: any) {
      console.warn('Recalculate error:', e);
    }
  };

  // --- SAVE STOK ---
  const handleSaveStok = async () => {
    clearMessages();
    setStatusMessage('Stok kartı kaydediliyor...');

    if (!editStokKodu.trim()) {
      setErrorMessage('Stok kodu boş olamaz.');
      setStatusMessage('Hata: Stok kodu boş olamaz.');
      return;
    }
    if (!editStokAdi.trim()) {
      setErrorMessage('Stok adı boş olamaz.');
      setStatusMessage('Hata: Stok adı boş olamaz.');
      return;
    }

    if (editKategori && editKategori.trim()) {
      const trimmedCat = editKategori.trim();
      if (!stokGruplar.includes(trimmedCat)) {
        const grpId = generateInt32Id();
        try {
          await writeData(`StokGruplar/${grpId}`, {
            id: grpId,
            ad: trimmedCat,
            updatedAt: new Date().toISOString(),
            isDeleted: false
          });
        } catch (err) {}
      }
    }

    try {
      if (selectedStok) {
        const eskiMiktar = Number(selectedStok.miktar || 0);
        const yeniMiktar = Number(editAcilisBakiye || 0);

        const payload = {
          ...selectedStok,
          barkod: editBarkod,
          stokKodu: editStokKodu,
          stokAdi: editStokAdi,
          kategori: editKategori,
          birim: editBirim,
          alisFiyati: Number(editAlisFiyati) || 0,
          ortalamaAlisFiyati: Number(editOrtalamaAlisFiyati) || 0,
          satisFiyati: Number(editSatisFiyati) || 0,
          ortalamaSatisFiyati: Number(editOrtalamaSatisFiyati) || 0,
          kdv: Number(editKdv) || 20,
          miktar: yeniMiktar,
          updatedAt: new Date().toISOString()
        };

        const targetKey = selectedStok.firebaseKey || selectedStok.id.toString();
        await writeData(`Stoklar/${targetKey}`, payload);

        if (Math.abs(eskiMiktar - yeniMiktar) > 0.0001) {
          const miktarFarki = Math.abs(yeniMiktar - eskiMiktar);
          const isGiris = yeniMiktar > eskiMiktar;
          const hareketKey = generateInt32Id().toString();

          await writeData(`StokHareketler/${hareketKey}`, {
            id: generateInt32Id(),
            stokId: selectedStok.id,
            stokKodu: editStokKodu,
            stokAdi: editStokAdi,
            tarih: new Date().toISOString(),
            islemTuru: isGiris ? 'GİRİŞ' : 'ÇIKIŞ',
            miktar: miktarFarki,
            aciklama: 'Envanter Düzenleme',
            giren: isGiris ? miktarFarki : 0,
            cikan: !isGiris ? miktarFarki : 0,
            fiyat: isGiris ? Number(editAlisFiyati) : Number(editSatisFiyati)
          });
        }

        await recalculateCosts(selectedStok.id);
        setSuccessMessage('Stok kartı başarıyla kaydedildi.');
        setStatusMessage(`Başarılı: Stok kartı '${editStokAdi}' kaydedildi.`);
      } else {
        const yeniId = generateInt32Id();
        const yMiktar = Number(editAcilisBakiye || 0);
        const payload = {
          id: yeniId,
          barkod: editBarkod,
          stokKodu: editStokKodu,
          stokAdi: editStokAdi,
          kategori: editKategori,
          birim: editBirim,
          alisFiyati: Number(editAlisFiyati) || 0,
          ortalamaAlisFiyati: Number(editOrtalamaAlisFiyati) || 0,
          satisFiyati: Number(editSatisFiyati) || 0,
          ortalamaSatisFiyati: Number(editOrtalamaSatisFiyati) || 0,
          kdv: Number(editKdv) || 20,
          miktar: yMiktar,
          kayitTarihi: new Date().toISOString()
        };

        const stKey = yeniId.toString();
        await writeData(`Stoklar/${stKey}`, payload);

        if (yMiktar !== 0) {
          const hk = generateInt32Id().toString();
          await writeData(`StokHareketler/${hk}`, {
            id: generateInt32Id(),
            stokId: yeniId,
            stokKodu: editStokKodu,
            stokAdi: editStokAdi,
            tarih: new Date().toISOString(),
            islemTuru: 'GİRİŞ',
            miktar: Math.abs(yMiktar),
            giren: yMiktar > 0 ? Math.abs(yMiktar) : 0,
            cikan: yMiktar < 0 ? Math.abs(yMiktar) : 0,
            aciklama: 'Açılış Bakiyesi',
            fiyat: Number(editAlisFiyati) || 0
          });
        }

        await recalculateCosts(yeniId);
        clearEditForm();
        setSuccessMessage('Stok kartı başarıyla eklendi.');
        setStatusMessage(`Başarılı: Yeni stok kartı '${editStokAdi}' eklendi.`);
      }
    } catch (e: any) {
      setErrorMessage(e.message || 'Kaydetme sırasında hata oluştu.');
      setStatusMessage('Kaydetme Başarısız.');
    }
  };

  // --- DELETE STOK ---
  const showDeleteStokConfirm = () => {
    if (!selectedStok) return;
    setConfirmTitle('Stok Kartını Sil');
    setConfirmMessage(`${selectedStok.stokAdi} adlı stoğu silmek istediğinize emin misiniz? Bu işlem geri alınamaz.`);
    setConfirmAction(() => async () => {
      const targetKey = selectedStok.firebaseKey || selectedStok.id.toString();
      await writeData(`Stoklar/${targetKey}/isDeleted`, true);
      setSelectedStok(null);
      clearEditForm();
      setIsConfirmVisible(false);
      setSuccessMessage('Stok kartı silindi.');
    });
    setIsConfirmVisible(true);
  };

  // --- DELETE STOK HAREKET ---
  const showDeleteStokHareketConfirm = () => {
    if (!selectedStokHareket || !selectedStok) {
      setErrorMessage('Lütfen tablodan silmek istediğiniz hareketi seçin.');
      return;
    }

    const rawFaturaId = selectedStokHareket.faturaId || selectedStokHareket.FaturaId;
    const isFatura = (selectedStokHareket.islemTuru && selectedStokHareket.islemTuru.includes('Fatura')) ||
                     (rawFaturaId && Number(rawFaturaId) > 0) ||
                     (selectedStokHareket.evrakNo && String(selectedStokHareket.evrakNo).startsWith('FAT'));

    if (isFatura) {
      setErrorMessage(`Bu stok hareketi "${selectedStokHareket.evrakNo || 'Fatura'}" numaralı faturaya aittir. Muhasebe ve cari bütünlüğü için lütfen işlemi Faturalar veya Cari Hareketleri ekranından siliniz.`);
      return;
    }

    setConfirmTitle('İşlemi Sil');
    setConfirmMessage('Seçili stok hareketini silmek istediğinize emin misiniz? Bakiye geri alınacaktır.');
    setConfirmAction(() => async () => {
      let tur = (selectedStokHareket.islemTuru || '').toUpperCase();
      const miktar = Number(selectedStokHareket.miktar || 0);
      let isGiris = (selectedStokHareket.giren && Number(selectedStokHareket.giren) > 0) || tur.includes('GİRİŞ') || tur.includes('ALIŞ');

      let yeniMiktar = Number(selectedStok.miktar || 0);
      if (isGiris) yeniMiktar -= miktar;
      else yeniMiktar += miktar;

      const stokKey = selectedStok.firebaseKey || selectedStok.id.toString();
      await writeData(`Stoklar/${stokKey}/miktar`, yeniMiktar);

      const harKey = selectedStokHareket.firebaseKey || selectedStokHareket.id.toString();
      const harId = selectedStokHareket.id;
      await deleteData(`StokHareketler/${harKey}`);

      await recalculateCosts(selectedStok.id, harId, harKey);
      setSelectedStok({ 
        ...selectedStok, 
        miktar: yeniMiktar,
        ortalamaAlisFiyati: 0,
        ortalamaSatisFiyati: 0
      });
      setEditAcilisBakiye(yeniMiktar.toString());
      setEditOrtalamaAlisFiyati('0');
      setEditOrtalamaSatisFiyati('0');
      setSelectedStokHareket(null);
      setIsConfirmVisible(false);
      setSuccessMessage('İşlem başarıyla silindi ve maliyetler güncellendi.');
    });
    setIsConfirmVisible(true);
  };

  // --- MANUAL ISLEM (F1/F2) ---
  const handleManualIslem = (tur: string) => {
    if (!selectedStok) return;
    setIsEditingTransaction(false);
    setTransactionType(tur);
    setIslemMiktar('1');
    setIslemFiyat(tur === 'GİRİŞ' ? (selectedStok.alisFiyati || 0).toString() : (selectedStok.satisFiyati || 0).toString());
    setIslemAciklama('');
    setIslemTarihStr(new Date().toLocaleDateString('tr-TR'));
    setIsTransactionWindowVisible(true);
  };

  // --- EDIT STOK HAREKET (F3) ---
  const handleEditStokHareket = () => {
    if (!selectedStok || !selectedStokHareket) {
      setErrorMessage('Lütfen tablodan düzenlemek istediğiniz hareketi seçin.');
      return;
    }
    setIsEditingTransaction(true);
    setTransactionType(selectedStokHareket.islemTuru || 'GİRİŞ');
    setIslemMiktar((selectedStokHareket.miktar || 1).toString());
    setIslemFiyat((selectedStokHareket.fiyat || 0).toString());
    setIslemAciklama(selectedStokHareket.aciklama || '');
    setIslemTarihStr(new Date(selectedStokHareket.tarih || Date.now()).toLocaleDateString('tr-TR'));
    setIsTransactionWindowVisible(true);
  };

  // Parse DD.MM.YYYY string
  const parseTarih = (str: string) => {
    const parts = str.split('.');
    if (parts.length === 3) {
      return new Date(Number(parts[2]), Number(parts[1]) - 1, Number(parts[0])).toISOString();
    }
    return new Date().toISOString();
  };

  // --- SAVE TRANSACTION ---
  const handleSaveTransaction = async () => {
    if (!selectedStok) return;
    try {
      const miktar = Number(islemMiktar);
      if (miktar <= 0) {
        Alert.alert('Uyarı', 'İşlem miktarı sıfırdan büyük olmalıdır.');
        return;
      }
      const tur = transactionType.toUpperCase();
      const tarih = parseTarih(islemTarihStr);
      const isGiris = tur === 'GİRİŞ';

      const stokKey = selectedStok.firebaseKey || selectedStok.id.toString();

      if (isEditingTransaction && selectedStokHareket) {
        let oldMiktar = Number(selectedStok.miktar || 0);
        let oldTur = (selectedStokHareket.islemTuru || '').toUpperCase();
        let wasGiris = (selectedStokHareket.giren && Number(selectedStokHareket.giren) > 0) || oldTur.includes('GİRİŞ');

        if (wasGiris) oldMiktar -= Number(selectedStokHareket.miktar || 0);
        else oldMiktar += Number(selectedStokHareket.miktar || 0);

        if (isGiris) oldMiktar += miktar;
        else oldMiktar -= miktar;

        await writeData(`Stoklar/${stokKey}/miktar`, oldMiktar);

        const harKey = selectedStokHareket.firebaseKey || selectedStokHareket.id.toString();
        await writeData(`StokHareketler/${harKey}`, {
          ...selectedStokHareket,
          tarih,
          islemTuru: tur,
          miktar,
          fiyat: Number(islemFiyat) || 0,
          aciklama: islemAciklama,
          giren: isGiris ? miktar : 0,
          cikan: !isGiris ? miktar : 0
        });

        setSelectedStok({ ...selectedStok, miktar: oldMiktar });
        setEditAcilisBakiye(oldMiktar.toString());
      } else {
        const hk = generateInt32Id().toString();
        await writeData(`StokHareketler/${hk}`, {
          id: generateInt32Id(),
          stokId: selectedStok.id,
          stokKodu: selectedStok.stokKodu || '',
          stokAdi: selectedStok.stokAdi || '',
          tarih,
          islemTuru: tur,
          miktar,
          fiyat: Number(islemFiyat) || 0,
          aciklama: islemAciklama || `Manuel ${tur}`,
          giren: isGiris ? miktar : 0,
          cikan: !isGiris ? miktar : 0
        });

        let yeniMiktar = Number(selectedStok.miktar || 0);
        if (isGiris) yeniMiktar += miktar;
        else yeniMiktar -= miktar;

        await writeData(`Stoklar/${stokKey}/miktar`, yeniMiktar);
        setSelectedStok({ ...selectedStok, miktar: yeniMiktar });
        setEditAcilisBakiye(yeniMiktar.toString());
      }

      await recalculateCosts(selectedStok.id);
      setIsTransactionWindowVisible(false);
      setSuccessMessage('Stok hareketi başarıyla kaydedildi.');
    } catch (e: any) {
      setErrorMessage(e.message || 'İşlem kaydedilemedi.');
      setIsTransactionWindowVisible(false);
    }
  };

  // --- REPORTS & PRINT ---
  const handlePrintStokList = async () => {
    try {
      setStatusMessage('Stok listesi PDF hazırlanıyor...');
      await generateReportPdf('stok-list', { Stoklar: filteredStoklar, ShowLogo: true }, `StokListesi_${Date.now()}`);
      setStatusMessage('');
    } catch (e: any) {
      setErrorMessage(e.message || 'Yazdırma hatası');
    }
  };

  const handleGenerateStokHareketleriReport = async () => {
    if (!selectedStok) {
      setErrorMessage('Lütfen önce hareket raporunu almak istediğiniz stok kartını seçiniz.');
      return;
    }
    setIsReportsMenuVisible(false);
    setTimeout(async () => {
      try {
        setStatusMessage('Stok hareket raporu hazırlanıyor...');
        await generateReportPdf(
          'stok-hareket',
          {
            Stok: selectedStok,
            Hareketler: currentStokHareketler,
            ShowLogo: true
          },
          `StokHareket_${selectedStok.stokKodu || 'rapor'}`
        );
        setStatusMessage('');
      } catch (e: any) {
        setErrorMessage(e.message || 'Rapor hatası');
      }
    }, 350);
  };

  const handleGenerateTopluStokReport = async () => {
    setIsReportsMenuVisible(false);
    setTimeout(async () => {
      try {
        setStatusMessage('Toplu stok raporu hazırlanıyor...');
        await generateReportPdf('stok-list', { Stoklar: stoklar, ShowLogo: true }, `TopluStokRaporu_${Date.now()}`);
        setStatusMessage('');
      } catch (e: any) {
        setErrorMessage(e.message || 'Rapor hatası');
      }
    }, 350);
  };

  const handleDownloadTemplate = async () => {
    setIsExcelMenuVisible(false);
    setTimeout(async () => {
      try {
        setStatusMessage('Şablon hazırlanıyor...');
        const templateRows = [
          {
            StokKodu: 'STK-001',
            StokAdi: 'Örnek Ürün Adı',
            Barkod: '8690000000001',
            Birim: 'Adet',
            Kategori: 'Genel',
            AlisFiyati: 50.0,
            SatisFiyati: 100.0,
            KDV: 20,
            Miktar: 10,
            MinSeviye: 5,
            Aciklama: 'Örnek açıklama'
          }
        ];
        const ok = await exportToExcel(templateRows, 'Stok_Sablon', 'Stok_Yukleme_Sablonu');
        if (ok) {
          setSuccessMessage('Şablon başarıyla oluşturuldu.');
        }
        setStatusMessage('');
      } catch (e: any) {
        setErrorMessage(e?.message || 'Şablon indirilemedi.');
        setStatusMessage('');
      }
    }, 350);
  };

  const handleExportToExcel = async () => {
    setIsExcelMenuVisible(false);
    setTimeout(async () => {
      try {
        let listToExport = stoklar;
        if (!listToExport || listToExport.length === 0) {
          try {
            const raw = await readData('Stoklar');
            if (raw) {
              if (Array.isArray(raw)) {
                listToExport = raw.filter(Boolean).map((item, idx) => normalizeStok(item, item.firebaseKey || item.id?.toString() || idx.toString())).filter(s => !s.isDeleted);
              } else if (typeof raw === 'object') {
                listToExport = Object.keys(raw).map(k => normalizeStok(raw[k], k)).filter(s => !s.isDeleted);
              }
            }
          } catch (err) {}
        }

        if (!listToExport || listToExport.length === 0) {
          Alert.alert('Uyarı', 'Dışa aktarılacak stok kartı bulunmamaktadır.');
          return;
        }

        setStatusMessage('Excel listesi hazırlanıyor...');
        const rows = listToExport.map(s => ({
          StokKodu: s.stokKodu || '',
          StokAdi: s.stokAdi || '',
          Barkod: s.barkod || '',
          Birim: s.birim || 'Adet',
          Kategori: s.kategori || '',
          AlisFiyati: s.alisFiyati || 0,
          OrtalamaAlisFiyati: s.ortalamaAlisFiyati || 0,
          SatisFiyati: s.satisFiyati || 0,
          OrtalamaSatisFiyati: s.ortalamaSatisFiyati || 0,
          KDV: s.kdv || 20,
          Miktar: s.miktar || 0,
          MinSeviye: s.minSeviye || 0,
          Aciklama: s.aciklama || ''
        }));
        await exportToExcel(rows, 'Stoklar', `StokListesi_${new Date().toISOString().split('T')[0]}`);
        setStatusMessage('');
      } catch (e: any) {
        setErrorMessage(e?.message || 'Excel aktarılamadı.');
      }
    }, 350);
  };

  const handleImportExcel = async () => {
    try {
      setStatusMessage('Excel dosyası seçiliyor...');
      const items = await importFromExcel();
      if (!items) {
        setStatusMessage('');
        return;
      }

      if (items.length === 0) {
        Alert.alert('Uyarı', 'Seçilen Excel dosyasında geçerli stok verisi bulunamadı.');
        setStatusMessage('');
        return;
      }

      setStatusMessage('Stoklar veritabanına aktarılıyor...');
      let addedCount = 0;
      let updatedCount = 0;

      for (const item of items) {
        const kod = (item.stokKodu || '').trim();
        if (!kod) continue;

        const existing = stoklar.find(s => (s.stokKodu || '').trim().toLocaleLowerCase('tr-TR') === kod.toLocaleLowerCase('tr-TR'));

        if (existing) {
          const targetKey = existing.firebaseKey || existing.id.toString();
          await writeData(`Stoklar/${targetKey}`, {
            ...existing,
            stokAdi: item.stokAdi || existing.stokAdi,
            birim: item.birim || existing.birim,
            kategori: item.kategori || existing.kategori,
            barkod: item.barkod || existing.barkod || '',
            alisFiyati: item.alisFiyati > 0 ? item.alisFiyati : existing.alisFiyati,
            satisFiyati: item.satisFiyati > 0 ? item.satisFiyati : existing.satisFiyati,
            kdv: item.kdv > 0 ? item.kdv : existing.kdv,
            miktar: item.miktar !== 0 ? item.miktar : existing.miktar,
            minSeviye: item.minSeviye > 0 ? item.minSeviye : (existing.minSeviye || 0),
            aciklama: item.aciklama || existing.aciklama || '',
            updatedAt: new Date().toISOString()
          });
          updatedCount++;
        } else {
          const yeniId = generateInt32Id();
          const targetKey = yeniId.toString();
          await writeData(`Stoklar/${targetKey}`, {
            id: yeniId,
            stokKodu: kod,
            stokAdi: item.stokAdi || 'İthal Stok',
            birim: item.birim || 'Adet',
            kategori: item.kategori || 'Genel',
            barkod: item.barkod || '',
            alisFiyati: item.alisFiyati || 0,
            ortalamaAlisFiyati: item.alisFiyati || 0,
            satisFiyati: item.satisFiyati || 0,
            ortalamaSatisFiyati: item.satisFiyati || 0,
            kdv: item.kdv || 20,
            miktar: item.miktar || 0,
            minSeviye: item.minSeviye || 0,
            aciklama: item.aciklama || '',
            kayitTarihi: new Date().toISOString()
          });
          addedCount++;
        }
      }

      const msg = `Excel İçe Aktarma Başarılı: ${addedCount} yeni stok eklendi, ${updatedCount} stok güncellendi.`;
      setSuccessMessage(msg);
      setStatusMessage(msg);
      Alert.alert('İçe Aktarma Tamamlandı', `${addedCount} yeni stok kartı eklendi.\n${updatedCount} mevcut stok kartı güncellendi.`);
    } catch (e: any) {
      console.error('handleImportExcel error:', e);
      setErrorMessage(`İçe aktarma sırasında hata: ${e?.message || 'Bilinmeyen hata'}`);
      setStatusMessage('İçe aktarma başarısız.');
    }
  };

  // --- GROUP ADD ---
  const handleSaveNewGroup = async () => {
    if (newGroupName.trim()) {
      const trimmed = newGroupName.trim();
      const finalGroupName = selectedParentGroup ? `${selectedParentGroup} > ${trimmed}` : trimmed;
      const grpId = generateInt32Id();
      try {
        await writeData(`StokGruplar/${grpId}`, {
          id: grpId,
          ad: finalGroupName,
          anaGrup: selectedParentGroup || '',
          altGrup: trimmed,
          updatedAt: new Date().toISOString(),
          isDeleted: false
        });
      } catch (err) {
        console.warn('StokGruplar write error:', err);
      }
      setStokGruplar(prev => Array.from(new Set([...prev, finalGroupName])));
      setEditKategori(finalGroupName);
    }
    setIsAddGroupVisible(false);
    setNewGroupName('');
    setSelectedParentGroup('');
  };

  // --- SELECT ALL TOGGLE ---
  const isAllSelected = filteredStoklar.length > 0 && selectedStokIds.length === filteredStoklar.length;
  const toggleSelectAll = () => {
    if (isAllSelected) {
      setSelectedStokIds([]);
    } else {
      setSelectedStokIds(filteredStoklar.map(s => s.id));
    }
  };

  const toggleSelectCard = (id: any) => {
    setSelectedStokIds(prev => (prev.includes(id) ? prev.filter(x => x !== id) : [...prev, id]));
  };

  return (
    <SafeAreaView style={styles.container}>
      <KeyboardAvoidingView 
        behavior={Platform.OS === 'ios' ? 'height' : undefined} 
        keyboardVerticalOffset={Platform.OS === 'ios' ? 44 : 0}
        style={{ flex: 1 }}
      >
        <ScrollView 
          contentContainerStyle={styles.scrollContent} 
          keyboardShouldPersistTaps="handled"
          automaticallyAdjustKeyboardInsets={true}
          showsVerticalScrollIndicator={false}
        >
          <OfflineNetworkBar />
          
          {/* ========================================================================= */}
          {/* 1. TOP LEFT: STOK LİSTESİ PANELİ (Exact Desktop Parity)                   */}
          {/* ========================================================================= */}
          <View style={styles.desktopPanel}>
            {/* Header */}
            <View style={styles.panelHeader}>
              <View style={styles.headerTitleGroup}>
                <Box color="#60A5FA" size={18} />
                <Text style={styles.panelTitle}>Stok Listesi</Text>
              </View>
              <View style={styles.totalBadge}>
                <Text style={styles.totalBadgeLabel}>Toplam: </Text>
                <Text style={styles.totalBadgeCount}>{filteredStoklar.length}</Text>
              </View>
            </View>

            {/* Filter & Pager Toolbar */}
            <View style={styles.toolbarContainer}>
              <View style={styles.searchRow}>
                <View style={styles.searchBox}>
                  <Search color="#888" size={16} style={{ marginLeft: 12 }} />
                  <TextInput
                    style={styles.searchInput}
                    placeholder="Stok Kodu / Adı / Barkodu..."
                    placeholderTextColor="#888"
                    value={filterKod}
                    onChangeText={(t) => { setFilterKod(t); setCurrentPageIndex(0); }}
                  />
                  {filterKod ? (
                    <TouchableOpacity onPress={() => setFilterKod('')} style={{ padding: 6 }}>
                      <X color="#888" size={14} />
                    </TouchableOpacity>
                  ) : null}
                </View>

                {/* Bakiye Göster Filter Checkbox */}
                <TouchableOpacity
                  style={[styles.checkboxBtn, onlyWithBalance && styles.checkboxBtnActive]}
                  onPress={() => setOnlyWithBalance(!onlyWithBalance)}
                >
                  <Text style={[styles.checkboxText, onlyWithBalance && styles.checkboxTextActive]}>Bakiye Göster</Text>
                </TouchableOpacity>
              </View>

              {/* Pager & Select All */}
              <View style={styles.pagerRow}>
                <TouchableOpacity style={styles.selectAllBtn} onPress={toggleSelectAll}>
                  {isAllSelected ? <CheckSquare color="#60A5FA" size={16} /> : <Square color="#64748B" size={16} />}
                  <Text style={styles.selectAllText}>Tümünü Seç</Text>
                </TouchableOpacity>

                <View style={styles.pagerBox}>
                  <TouchableOpacity
                    onPress={() => setCurrentPageIndex(p => Math.max(0, p - 1))}
                    disabled={currentPageIndex === 0}
                    style={{ opacity: currentPageIndex === 0 ? 0.3 : 1 }}
                  >
                    <ChevronLeft color="#FFF" size={18} />
                  </TouchableOpacity>

                  <Text style={styles.pagerText}>{pagerInfo}</Text>

                  <TouchableOpacity
                    onPress={() => setCurrentPageIndex(p => Math.min(totalPages - 1, p + 1))}
                    disabled={currentPageIndex >= totalPages - 1}
                    style={{ opacity: currentPageIndex >= totalPages - 1 ? 0.3 : 1 }}
                  >
                    <ChevronRight color="#FFF" size={18} />
                  </TouchableOpacity>
                </View>
              </View>
            </View>

            {/* Stock List Items */}
            <View style={styles.listContainer}>
              {loading ? (
                <ShimmerCardList count={5} />
              ) : pagedStoklar.length === 0 ? (
                <View style={styles.emptyContainer}>
                  <Box color="#475569" size={32} />
                  <Text style={styles.emptyText}>Kayıtlı stok bulunamadı.</Text>
                </View>
              ) : (
                <View style={styles.appleGroupedContainer}>
                  {filteredStoklar.map((item, index) => {
                    const isChecked = selectedStokIds.includes(item.id);
                    const isSelected = selectedStok?.id === item.id;
                    const miktar = Number(item.miktar || 0);

                    return (
                      <AppleListRow
                        key={item.firebaseKey || item.id}
                        icon={<Box color={AppleTheme.colors.primary} size={20} />}
                        title={item.stokAdi || 'İsimsiz Stok'}
                        subtitle={`${item.stokKodu || 'KODSUZ'} • ${item.kategori || 'Genel'}`}
                        tag={`${miktar.toFixed(2)} ${item.birim || 'Adet'}`}
                        tagColor={miktar <= 0 ? AppleTheme.colors.danger : AppleTheme.colors.success}
                        value={formatMoney(item.satisFiyati || 0)}
                        valueSub="Satış"
                        valueColor={AppleTheme.colors.textPrimary}
                        isFirst={index === 0}
                        isLast={index === filteredStoklar.length - 1}
                        onPress={() => handleSelectStok(item)}
                        style={isSelected ? { backgroundColor: 'rgba(10, 132, 255, 0.12)' } : undefined}
                      />
                    );
                  })}
                </View>
              )}
            </View>
          </View>

          {/* ========================================================================= */}
          {/* 2. BOTTOM LEFT: STOK HAREKETLERİ PANELİ (Exact Desktop Parity)            */}
          {/* ========================================================================= */}
          <View style={styles.desktopPanel}>
            <View style={[styles.panelHeader, { justifyContent: 'space-between' }]}>
              <View style={styles.headerTitleGroup}>
                <TrendingUp color="#60A5FA" size={18} />
                <Text style={styles.panelTitle}>Stok Hareket İşlemleri</Text>
              </View>
              <TouchableOpacity
                onPress={() => setIsCalendarVisible(!isCalendarVisible)}
                style={{ flexDirection: 'row', alignItems: 'center', gap: 4, backgroundColor: 'rgba(96, 165, 250, 0.15)', paddingHorizontal: 10, paddingVertical: 4, borderRadius: 8 }}
              >
                <Calendar color="#60A5FA" size={16} />
                <Text style={{ color: '#60A5FA', fontSize: 11, fontWeight: '700' }}>Takvim</Text>
              </TouchableOpacity>
            </View>

            {isCalendarVisible && (
              <IOSGlassCalendar
                selectedDate={selectedCalendarDate}
                onSelectDate={(dateStr) => {
                  setSelectedCalendarDate(dateStr);
                  setIsCalendarVisible(false);
                  Alert.alert('Takvim Seçimi', `${dateStr} tarihi başarıyla seçildi.`);
                }}
              />
            )}

            <View style={styles.hareketContainer}>
              {/* Header Row */}
              <View style={styles.hHeaderRow}>
                <Text style={[styles.hHeaderCol, { flex: 1.4 }]}>Tarih</Text>
                <Text style={[styles.hHeaderCol, { flex: 1.3 }]}>İşlem Türü</Text>
                <Text style={[styles.hHeaderCol, { flex: 2 }]}>Açıklama</Text>
                <Text style={[styles.hHeaderCol, { flex: 1.1, textAlign: 'right' }]}>Miktar</Text>
                <Text style={[styles.hHeaderCol, { flex: 1.1, textAlign: 'right' }]}>Kalan</Text>
              </View>

              <View style={{ flex: 1 }}>
                {!selectedStok ? (
                  <Text style={styles.hEmptyText}>Hareketleri görmek için lütfen bir stok kartı seçin.</Text>
                ) : processedHareketler.length === 0 ? (
                  <Text style={styles.hEmptyText}>Bu stok kartına ait hareket bulunamadı.</Text>
                ) : (
                  processedHareketler.map((h, i) => {
                    const isSelectedH = selectedStokHareket?.id === h.id || selectedStokHareket?.firebaseKey === h.firebaseKey;
                    const dateStr = h.tarih ? new Date(h.tarih).toLocaleDateString('tr-TR') : '-';
                    const isGiris = (h.giren && Number(h.giren) > 0) || (h.islemTuru || '').toUpperCase().includes('GİRİŞ');

                    return (
                      <TouchableOpacity
                        key={h.firebaseKey || h.id || i}
                        activeOpacity={0.7}
                        style={[styles.hRow, isSelectedH && styles.hRowSelected]}
                        onPress={() => {
                          clearMessages();
                          setSelectedStokHareket(isSelectedH ? null : h);
                        }}
                      >
                        <Text style={[styles.hCol, { flex: 1.4 }]}>{dateStr}</Text>
                        <Text style={[styles.hCol, { flex: 1.3, color: isGiris ? '#10B981' : '#F59E0B', fontWeight: 'bold' }]}>
                          {h.islemTuru || '-'}
                        </Text>
                        <Text style={[styles.hCol, { flex: 2 }]} numberOfLines={1}>
                          {h.aciklama || '-'}
                        </Text>
                        <Text style={[styles.hCol, { flex: 1.1, textAlign: 'right', fontWeight: '600' }]}>
                          {Number(h.miktar || 0).toFixed(2)}
                        </Text>
                        <Text style={[styles.hCol, { flex: 1.1, textAlign: 'right', color: '#60A5FA', fontWeight: 'bold' }]}>
                          {Number(h.kalanMiktar || 0).toFixed(2)}
                        </Text>
                      </TouchableOpacity>
                    );
                  })
                )}
              </View>
            </View>
          </View>

          {/* ========================================================================= */}
          {/* 3. RIGHT SIDE: FORM + ALL ACTIONS (Exact Desktop Parity)                  */}
          {/* ========================================================================= */}
          <View style={[styles.desktopPanel, { borderRadius: 16, padding: 14 }]}>
            {/* Notification Banners */}
            {errorMessage ? (
              <View style={[styles.banner, { backgroundColor: '#26EF4444', borderColor: '#EF4444' }]}>
                <AlertCircle color="#EF4444" size={16} />
                <Text style={[styles.bannerText, { color: '#EF4444' }]}>{errorMessage}</Text>
              </View>
            ) : null}

            {successMessage ? (
              <View style={[styles.banner, { backgroundColor: '#2610B981', borderColor: '#10B981' }]}>
                <CheckCircle2 color="#10B981" size={16} />
                <Text style={[styles.bannerText, { color: '#10B981' }]}>{successMessage}</Text>
              </View>
            ) : null}

            {/* Form Title */}
            <Text style={[styles.formTitle, { color: formTitleColor }]}>{formTitle}</Text>

            {/* Edit Mode Warning Banner */}
            {isEditMode && (
              <View style={[styles.banner, { backgroundColor: '#26F59E0B', borderColor: '#F59E0B', marginTop: 2, marginBottom: 10 }]}>
                <Info color="#F59E0B" size={16} />
                <Text style={[styles.bannerText, { color: '#F59E0B' }]}>
                  Seçili stok kartını düzenliyorsunuz. Yeni stok için 'Yeni' butonuna tıklayın.
                </Text>
              </View>
            )}

            {/* Input Grid (Desktop exact matching) */}
            <View style={styles.formGrid}>
              {/* Row 0: Kodu */}
              <View style={styles.fieldRow}>
                <Text style={styles.fLabel}>Kodu</Text>
                <TextInput
                  style={styles.fInput}
                  value={editStokKodu}
                  onChangeText={setEditStokKodu}
                  placeholder="Stok Kodu"
                  placeholderTextColor="#666"
                />
              </View>

              {/* Row 1: Stok Adı */}
              <View style={styles.fieldRow}>
                <Text style={styles.fLabel}>Stok Adı</Text>
                <TextInput
                  style={styles.fInput}
                  value={editStokAdi}
                  onChangeText={setEditStokAdi}
                  placeholder="Stok Adı"
                  placeholderTextColor="#666"
                />
              </View>

              {/* Row 2: Grup and Birim */}
              <View style={styles.twoColRow}>
                <View style={{ flex: 1.2 }}>
                  <View style={styles.labelWithAddBtn}>
                    <Text style={styles.fLabelInline}>Grup</Text>
                    <TouchableOpacity
                      style={styles.miniAddBtn}
                      onPress={() => {
                        setNewGroupName('');
                        setIsAddGroupVisible(true);
                      }}
                    >
                      <Plus color="#60A5FA" size={14} />
                    </TouchableOpacity>
                  </View>
                  <TouchableOpacity
                    style={styles.dropdownBtn}
                    onPress={() => setIsGroupSelectVisible(true)}
                  >
                    <Text style={styles.dropdownBtnText} numberOfLines={1}>
                      {editKategori || 'Grup Seçiniz'}
                    </Text>
                  </TouchableOpacity>
                </View>

                <View style={{ flex: 0.8 }}>
                  <Text style={styles.fLabel}>Birim</Text>
                  <TouchableOpacity
                    style={styles.dropdownBtn}
                    onPress={() => setIsBirimSelectVisible(true)}
                  >
                    <Text style={styles.dropdownBtnText}>{editBirim || 'Kg'}</Text>
                  </TouchableOpacity>
                </View>
              </View>

              {/* Row 3: Ort. Alış F. & Alış F. */}
              <View style={styles.twoColRow}>
                <View style={{ flex: 1 }}>
                  <Text style={styles.fLabel}>Ort. Alış F.</Text>
                  <TextInput
                    style={[styles.fInput, styles.fInputDisabled]}
                    keyboardType="numeric"
                    inputAccessoryViewID={KEYBOARD_ACCESSORY_ID}
                    value={editOrtalamaAlisFiyati}
                    editable={false}
                  />
                </View>
                <View style={{ flex: 1 }}>
                  <Text style={styles.fLabel}>Alış F.</Text>
                  <TextInput
                    style={styles.fInput}
                    keyboardType="numeric"
                    inputAccessoryViewID={KEYBOARD_ACCESSORY_ID}
                    value={editAlisFiyati}
                    onChangeText={setEditAlisFiyati}
                  />
                </View>
              </View>

              {/* Row 4: Ort. Satış F. & Satış F. */}
              <View style={styles.twoColRow}>
                <View style={{ flex: 1 }}>
                  <Text style={styles.fLabel}>Ort. Satış F.</Text>
                  <TextInput
                    style={[styles.fInput, styles.fInputDisabled]}
                    keyboardType="numeric"
                    inputAccessoryViewID={KEYBOARD_ACCESSORY_ID}
                    value={editOrtalamaSatisFiyati}
                    editable={false}
                  />
                </View>
                <View style={{ flex: 1 }}>
                  <Text style={styles.fLabel}>Satış F.</Text>
                  <TextInput
                    style={styles.fInput}
                    keyboardType="numeric"
                    inputAccessoryViewID={KEYBOARD_ACCESSORY_ID}
                    value={editSatisFiyati}
                    onChangeText={setEditSatisFiyati}
                  />
                </View>
              </View>

              {/* Row 5: KDV & Açılış */}
              <View style={styles.twoColRow}>
                <View style={{ flex: 1 }}>
                  <Text style={styles.fLabel}>KDV (%)</Text>
                  <TextInput
                    style={styles.fInput}
                    keyboardType="numeric"
                    inputAccessoryViewID={KEYBOARD_ACCESSORY_ID}
                    value={editKdv}
                    onChangeText={setEditKdv}
                  />
                </View>
                <View style={{ flex: 1 }}>
                  <Text style={styles.fLabel}>Açılış Bakiye</Text>
                  <TextInput
                    style={[styles.fInput, isEditMode && styles.fInputDisabled]}
                    keyboardType="numeric"
                    inputAccessoryViewID={KEYBOARD_ACCESSORY_ID}
                    value={editAcilisBakiye}
                    onChangeText={setEditAcilisBakiye}
                    editable={!isEditMode}
                  />
                </View>
              </View>
            </View>

            {/* ========================================================================= */}
            {/* ACTION BUTTONS (Exact Desktop Grid & Colors)                              */}
            {/* ========================================================================= */}
            <View style={styles.actionButtonGroup}>
              {/* Row 1: Yeni, Kaydet, Düzen, Sil */}
              <View style={styles.actionRow}>
                <TouchableOpacity
                  style={[styles.actionBtn, { borderColor: '#00C2FF' }]}
                  onPress={handleCreateNew}
                >
                  <Plus color="#00C2FF" size={14} />
                  <Text style={styles.actionBtnText}>Yeni</Text>
                </TouchableOpacity>

                <TouchableOpacity
                  style={[styles.actionBtn, { borderColor: '#10B981' }]}
                  onPress={handleSaveStok}
                >
                  <Save color="#10B981" size={14} />
                  <Text style={styles.actionBtnText}>Kaydet</Text>
                </TouchableOpacity>

                <TouchableOpacity
                  style={[styles.actionBtn, { borderColor: '#60A5FA', opacity: isEditMode ? 1 : 0.4 }]}
                  disabled={!isEditMode}
                >
                  <Edit3 color="#60A5FA" size={14} />
                  <Text style={styles.actionBtnText}>Düzen</Text>
                </TouchableOpacity>

                <TouchableOpacity
                  style={[styles.actionBtn, { borderColor: '#EF4444', opacity: isEditMode ? 1 : 0.4 }]}
                  onPress={showDeleteStokConfirm}
                  disabled={!isEditMode}
                >
                  <Trash2 color="#EF4444" size={14} />
                  <Text style={styles.actionBtnText}>Sil</Text>
                </TouchableOpacity>
              </View>

              {/* Row 2: Stok Girişi (F1), Stok Çıkışı (F2) */}
              <View style={styles.actionRow}>
                <TouchableOpacity
                  style={[styles.actionBtnFull, { borderColor: '#10B981', opacity: isEditMode ? 1 : 0.4 }]}
                  onPress={() => handleManualIslem('GİRİŞ')}
                  disabled={!isEditMode}
                >
                  <PlusSquare color="#10B981" size={14} />
                  <Text style={styles.actionBtnText}>Stok Girişi (F1)</Text>
                </TouchableOpacity>

                <TouchableOpacity
                  style={[styles.actionBtnFull, { borderColor: '#F59E0B', opacity: isEditMode ? 1 : 0.4 }]}
                  onPress={() => handleManualIslem('ÇIKIŞ')}
                  disabled={!isEditMode}
                >
                  <MinusSquare color="#F59E0B" size={14} />
                  <Text style={styles.actionBtnText}>Stok Çıkışı (F2)</Text>
                </TouchableOpacity>
              </View>

              {/* Row 3: Düzenle (F3), İşlemi Sil */}
              <View style={styles.actionRow}>
                <TouchableOpacity
                  style={[styles.actionBtnFull, { borderColor: '#06B6D4', opacity: isEditMode && selectedStokHareket ? 1 : 0.4 }]}
                  onPress={handleEditStokHareket}
                  disabled={!isEditMode || !selectedStokHareket}
                >
                  <Edit3 color="#06B6D4" size={14} />
                  <Text style={styles.actionBtnText}>Düzenle (F3)</Text>
                </TouchableOpacity>

                <TouchableOpacity
                  style={[styles.actionBtnFull, { borderColor: '#EF4444', opacity: isEditMode && selectedStokHareket ? 1 : 0.4 }]}
                  onPress={showDeleteStokHareketConfirm}
                  disabled={!isEditMode || !selectedStokHareket}
                >
                  <Trash2 color="#EF4444" size={14} />
                  <Text style={styles.actionBtnText}>İşlemi Sil</Text>
                </TouchableOpacity>
              </View>

              {/* Row 4: Raporlar, Yazdır */}
              <View style={styles.actionRow}>
                <TouchableOpacity
                  style={[styles.actionBtnFull, { borderColor: '#6366F1' }]}
                  onPress={() => setIsReportsMenuVisible(true)}
                >
                  <FileText color="#6366F1" size={14} />
                  <Text style={styles.actionBtnText}>Raporlar</Text>
                </TouchableOpacity>

                <TouchableOpacity
                  style={[styles.actionBtnFull, { borderColor: '#22D3EE' }]}
                  onPress={handlePrintStokList}
                >
                  <Printer color="#22D3EE" size={14} />
                  <Text style={styles.actionBtnText}>Yazdır</Text>
                </TouchableOpacity>
              </View>

              {/* Row 5: Dıştan Aktar, Excel */}
              <View style={styles.actionRow}>
                <TouchableOpacity
                  style={[styles.actionBtnFull, { borderColor: '#10B981' }]}
                  onPress={handleImportExcel}
                >
                  <Upload color="#10B981" size={14} />
                  <Text style={styles.actionBtnText}>Dıştan Aktar</Text>
                </TouchableOpacity>

                <TouchableOpacity
                  style={[styles.actionBtnFull, { borderColor: '#3B82F6' }]}
                  onPress={() => setIsExcelMenuVisible(true)}
                >
                  <Download color="#3B82F6" size={14} />
                  <Text style={styles.actionBtnText}>Excel</Text>
                </TouchableOpacity>
              </View>

              {/* Row 6: VAZGEÇ (Full Width) */}
              <TouchableOpacity
                style={[styles.actionBtnFull, { borderColor: '#EF4444', marginTop: 2, height: 34 }]}
                onPress={handleCancelEdit}
              >
                <X color="#EF4444" size={14} />
                <Text style={[styles.actionBtnText, { fontWeight: 'bold', color: '#FFF' }]}>VAZGEÇ</Text>
              </TouchableOpacity>
            </View>

            {/* Status Message */}
            {statusMessage ? <Text style={styles.statusMsg}>{statusMessage}</Text> : null}
            <View style={{ height: 95 }} />
          </View>
        </ScrollView>
      </KeyboardAvoidingView>

      {/* ========================================================================= */}
      {/* MODAL 1: GROUP ADD OVERLAY (Exact Desktop Parity)                         */}
      {/* ========================================================================= */}
      {/* ========================================================================= */}
      {/* MODAL 1: GROUP ADD OVERLAY (Hierarchical Parity)                          */}
      {/* ========================================================================= */}
      <Modal visible={isAddGroupVisible} transparent={true} animationType="fade">
        <View style={styles.modalOverlayCenter}>
          <View style={[styles.popupCardSmall, { maxHeight: 480 }]}>
            <Text style={styles.popupTitle}>Yeni Hiyerarşik Grup Ekle</Text>
            
            {/* Ana Grup Seçimi */}
            <Text style={{ color: '#94A3B8', fontSize: 11, marginBottom: 4, marginTop: 8 }}>
              Bağlı Olduğu Ana Grup (Opsiyonel):
            </Text>
            <ScrollView horizontal showsHorizontalScrollIndicator={false} style={{ marginBottom: 12 }}>
              <TouchableOpacity
                style={[
                  { paddingHorizontal: 12, paddingVertical: 6, borderRadius: 16, backgroundColor: '#262626', marginRight: 8, borderWidth: 1, borderColor: '#404040' },
                  !selectedParentGroup && { backgroundColor: '#2563EB', borderColor: '#3B82F6' }
                ]}
                onPress={() => setSelectedParentGroup('')}
              >
                <Text style={{ color: '#FFF', fontSize: 11, fontWeight: '700' }}>[Ana Grup Yok - Kök Seviye]</Text>
              </TouchableOpacity>
              {Array.from(new Set(groupList.map(g => g.split('>')[0].trim()).filter(Boolean))).map((pg, idx) => (
                <TouchableOpacity
                  key={idx}
                  style={[
                    { paddingHorizontal: 12, paddingVertical: 6, borderRadius: 16, backgroundColor: '#262626', marginRight: 8, borderWidth: 1, borderColor: '#404040' },
                    selectedParentGroup === pg && { backgroundColor: '#2563EB', borderColor: '#3B82F6' }
                  ]}
                  onPress={() => setSelectedParentGroup(pg)}
                >
                  <Text style={{ color: '#FFF', fontSize: 11, fontWeight: '600' }}>📁 {pg}</Text>
                </TouchableOpacity>
              ))}
            </ScrollView>

            <TextInput
              style={styles.popupInput}
              placeholder={selectedParentGroup ? `${selectedParentGroup} için Alt Grup Adı` : "Ana Grup Adı"}
              placeholderTextColor="#888"
              value={newGroupName}
              onChangeText={setNewGroupName}
              autoFocus={true}
            />
            <View style={styles.popupBtnRow}>
              <TouchableOpacity
                style={[styles.popupBtn, { backgroundColor: '#10B981' }]}
                onPress={handleSaveNewGroup}
              >
                <Text style={styles.popupBtnText}>EKLE</Text>
              </TouchableOpacity>
              <TouchableOpacity
                style={[styles.popupBtn, { backgroundColor: '#EF4444' }]}
                onPress={() => {
                  setIsAddGroupVisible(false);
                  setSelectedParentGroup('');
                  setNewGroupName('');
                }}
              >
                <Text style={styles.popupBtnText}>İPTAL</Text>
              </TouchableOpacity>
            </View>
          </View>
        </View>
      </Modal>

      {/* ========================================================================= */}
      {/* MODAL 2: HIERARCHICAL GROUP SELECT PICKER MODAL                           */}
      {/* ========================================================================= */}
      <Modal visible={isGroupSelectVisible} transparent={true} animationType="fade">
        <View style={styles.modalOverlayCenter}>
          <View style={[styles.popupCardSmall, { maxHeight: 520, width: '92%' }]}>
            <View style={styles.modalTopHeader}>
              <View style={{ flexDirection: 'row', alignItems: 'center', gap: 8 }}>
                <Folder color="#60A5FA" size={20} />
                <Text style={styles.popupTitle}>Hiyerarşik Stok Grupları</Text>
              </View>
              <TouchableOpacity onPress={() => setIsGroupSelectVisible(false)}>
                <X color="#FFF" size={20} />
              </TouchableOpacity>
            </View>

            {/* Arama ve Yeni Ekleme Çubuğu */}
            <View style={{ flexDirection: 'row', gap: 8, marginTop: 10, marginBottom: 8 }}>
              <TextInput
                style={[styles.popupInput, { flex: 1, marginBottom: 0, paddingVertical: 8, fontSize: 12 }]}
                placeholder="Grup ara..."
                placeholderTextColor="#64748B"
                value={groupSearchQuery}
                onChangeText={setGroupSearchQuery}
              />
              <TouchableOpacity
                style={{ backgroundColor: '#2563EB', borderRadius: 8, paddingHorizontal: 12, justifyContent: 'center', alignItems: 'center' }}
                onPress={() => setIsAddGroupVisible(true)}
              >
                <FolderPlus color="#FFF" size={18} />
              </TouchableOpacity>
            </View>

            <ScrollView style={{ marginTop: 6 }} showsVerticalScrollIndicator={false}>
              {/* Grup Yok seçeneği */}
              <TouchableOpacity
                style={[styles.pickerItem, !editKategori && styles.pickerItemSelected]}
                onPress={() => {
                  setEditKategori('');
                  setIsGroupSelectVisible(false);
                }}
              >
                <Text style={[styles.pickerItemText, { color: '#94A3B8' }]}>- Grup Yok (Genel Kategori) -</Text>
              </TouchableOpacity>

              {/* Hiyerarşik Ağaç Grupları */}
              {(() => {
                const query = groupSearchQuery.toLowerCase();
                const filtered = groupList.filter(g => g.toLowerCase().includes(query));

                // Ana grup -> alt gruplar haritası
                const hierarchyMap: Record<string, string[]> = {};
                filtered.forEach(g => {
                  const parts = g.split('>').map(p => p.trim());
                  const root = parts[0] || 'Genel';
                  if (!hierarchyMap[root]) hierarchyMap[root] = [];
                  if (parts.length > 1) {
                    hierarchyMap[root].push(g);
                  }
                });

                const rootKeys = Object.keys(hierarchyMap).sort((a, b) => a.localeCompare(b, 'tr-TR'));

                return rootKeys.map((rootKey, rIdx) => {
                  const subItems = hierarchyMap[rootKey];
                  const isRootSelected = editKategori === rootKey;

                  return (
                    <View key={rIdx} style={{ marginBottom: 6, backgroundColor: '#18181B', borderRadius: 8, padding: 4 }}>
                      {/* Ana Grup Satırı */}
                      <TouchableOpacity
                        style={[
                          styles.pickerItem,
                          { backgroundColor: 'transparent', borderBottomWidth: subItems.length > 0 ? 1 : 0, borderBottomColor: '#27272A' },
                          isRootSelected && styles.pickerItemSelected
                        ]}
                        onPress={() => {
                          setEditKategori(rootKey);
                          setIsGroupSelectVisible(false);
                        }}
                      >
                        <View style={{ flexDirection: 'row', alignItems: 'center', gap: 8, flex: 1 }}>
                          <Folder color={isRootSelected ? '#60A5FA' : '#93C5FD'} size={16} />
                          <Text style={[styles.pickerItemText, { fontWeight: '800', color: isRootSelected ? '#60A5FA' : '#FFF' }]}>
                            {rootKey}
                          </Text>
                        </View>
                        {subItems.length > 0 && (
                          <View style={{ backgroundColor: '#27272A', paddingHorizontal: 6, paddingVertical: 2, borderRadius: 10 }}>
                            <Text style={{ color: '#94A3B8', fontSize: 10, fontWeight: '700' }}>{subItems.length} Alt Grup</Text>
                          </View>
                        )}
                      </TouchableOpacity>

                      {/* Alt Gruplar */}
                      {subItems.map((sub, sIdx) => {
                        const isSubSelected = editKategori === sub;
                        const subName = sub.split('>').slice(1).join(' > ').trim();

                        return (
                          <TouchableOpacity
                            key={sIdx}
                            style={[
                              styles.pickerItem,
                              { paddingLeft: 24, backgroundColor: 'transparent' },
                              isSubSelected && styles.pickerItemSelected
                            ]}
                            onPress={() => {
                              setEditKategori(sub);
                              setIsGroupSelectVisible(false);
                            }}
                          >
                            <View style={{ flexDirection: 'row', alignItems: 'center', gap: 6, flex: 1 }}>
                              <CornerDownRight color={isSubSelected ? '#60A5FA' : '#64748B'} size={14} />
                              <Text style={[styles.pickerItemText, { fontSize: 13, color: isSubSelected ? '#60A5FA' : '#E2E8F0' }]}>
                                {subName}
                              </Text>
                            </View>
                          </TouchableOpacity>
                        );
                      })}
                    </View>
                  );
                });
              })()}
            </ScrollView>
          </View>
        </View>
      </Modal>

      {/* ========================================================================= */}
      {/* MODAL 3: BIRIM SELECT PICKER MODAL                                        */}
      {/* ========================================================================= */}
      <Modal visible={isBirimSelectVisible} transparent={true} animationType="fade">
        <View style={styles.modalOverlayCenter}>
          <View style={styles.popupCardSmall}>
            <View style={styles.modalTopHeader}>
              <Text style={styles.popupTitle}>Birim Seçin</Text>
              <TouchableOpacity onPress={() => setIsBirimSelectVisible(false)}>
                <X color="#FFF" size={20} />
              </TouchableOpacity>
            </View>
            <View style={{ marginTop: 10, gap: 8 }}>
              {birimler.map((b, i) => (
                <TouchableOpacity
                  key={i}
                  style={[styles.pickerItem, editBirim === b && styles.pickerItemSelected]}
                  onPress={() => {
                    setEditBirim(b);
                    setIsBirimSelectVisible(false);
                  }}
                >
                  <Text style={styles.pickerItemText}>{b}</Text>
                </TouchableOpacity>
              ))}
            </View>
          </View>
        </View>
      </Modal>

      {/* ========================================================================= */}
      {/* MODAL 4: TRANSACTION POPUP OVERLAY (Exact Desktop Parity)                 */}
      {/* ========================================================================= */}
      <Modal visible={isTransactionWindowVisible} transparent={true} animationType="fade">
        <View style={styles.modalOverlayCenter}>
          <KeyboardAvoidingView behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
            <View style={styles.transactionModalCard}>
              {/* Header */}
              <View style={styles.transactionHeader}>
                <View style={{ flexDirection: 'row', alignItems: 'center', flex: 1, marginRight: 10 }}>
                  <TrendingUp color="#FFF" size={20} />
                  <View style={{ marginLeft: 10, flex: 1 }}>
                    <Text style={styles.transactionHeaderTitle}>Stok Hareket İşlemi</Text>
                    <Text style={styles.transactionHeaderSubtitle} numberOfLines={1}>
                      {selectedStok?.stokAdi || selectedStok?.stokKodu}
                    </Text>
                  </View>
                </View>
                <TouchableOpacity onPress={() => setIsTransactionWindowVisible(false)}>
                  <X color="#FFF" size={20} />
                </TouchableOpacity>
              </View>

              {/* Form Content */}
              <View style={styles.transactionBody}>
                {/* İşlem Türü */}
                <View style={styles.tRow}>
                  <Text style={styles.tLabel}>İşlem Türü</Text>
                  <View style={styles.tToggleGroup}>
                    <TouchableOpacity
                      style={[styles.tToggleBtn, transactionType === 'GİRİŞ' && styles.tToggleBtnActiveGreen]}
                      onPress={() => !isEditingTransaction && setTransactionType('GİRİŞ')}
                    >
                      <Text style={[styles.tToggleText, transactionType === 'GİRİŞ' && styles.tToggleTextActive]}>GİRİŞ</Text>
                    </TouchableOpacity>
                    <TouchableOpacity
                      style={[styles.tToggleBtn, transactionType === 'ÇIKIŞ' && styles.tToggleBtnActiveOrange]}
                      onPress={() => !isEditingTransaction && setTransactionType('ÇIKIŞ')}
                    >
                      <Text style={[styles.tToggleText, transactionType === 'ÇIKIŞ' && styles.tToggleTextActive]}>ÇIKIŞ</Text>
                    </TouchableOpacity>
                  </View>
                </View>

                {/* Tarih */}
                <View style={styles.tRow}>
                  <Text style={styles.tLabel}>Tarih</Text>
                  <TextInput
                    style={styles.tInput}
                    value={islemTarihStr}
                    onChangeText={setIslemTarihStr}
                    placeholder="GG.AA.YYYY"
                    placeholderTextColor="#888"
                  />
                </View>

                {/* İşlem Miktarı */}
                <View style={styles.tRow}>
                  <Text style={styles.tLabel}>İşlem Miktarı</Text>
                  <TextInput
                    style={styles.tInput}
                    value={islemMiktar}
                    onChangeText={setIslemMiktar}
                    keyboardType="numeric"
                    inputAccessoryViewID={KEYBOARD_ACCESSORY_ID}
                  />
                </View>

                {/* Birim Fiyat */}
                <View style={styles.tRow}>
                  <Text style={styles.tLabel}>Birim Fiyat</Text>
                  <TextInput
                    style={styles.tInput}
                    value={islemFiyat}
                    onChangeText={setIslemFiyat}
                    keyboardType="numeric"
                    inputAccessoryViewID={KEYBOARD_ACCESSORY_ID}
                  />
                </View>

                {/* Açıklama */}
                <View style={styles.tRow}>
                  <Text style={[styles.tLabel, { alignSelf: 'flex-start', marginTop: 10 }]}>Açıklama</Text>
                  <TextInput
                    style={[styles.tInput, { height: 75, textAlignVertical: 'top' }]}
                    value={islemAciklama}
                    onChangeText={setIslemAciklama}
                    multiline={true}
                    placeholder="İşlem Açıklaması"
                    placeholderTextColor="#888"
                  />
                </View>

                {/* Submit Save Button */}
                <TouchableOpacity style={styles.tSaveBtn} onPress={handleSaveTransaction}>
                  <CheckCircle2 color="#FFF" size={20} />
                  <Text style={styles.tSaveBtnText}>İŞLEMİ KAYDET</Text>
                </TouchableOpacity>
              </View>
            </View>
          </KeyboardAvoidingView>
        </View>
      </Modal>

      {/* ========================================================================= */}
      {/* MODAL 5: DELETE CONFIRMATION OVERLAY (Exact Desktop Parity)               */}
      {/* ========================================================================= */}
      <Modal visible={isConfirmVisible} transparent={true} animationType="fade">
        <View style={styles.modalOverlayCenter}>
          <View style={styles.confirmModalCard}>
            <View style={styles.confirmHeader}>
              <Trash2 color="#FFF" size={18} />
              <Text style={styles.confirmHeaderTitle}>Onay Gerekiyor</Text>
            </View>

            <View style={styles.confirmBody}>
              <AlertCircle color="#FF416C" size={48} style={{ marginBottom: 15 }} />
              <Text style={styles.confirmTitle}>{confirmTitle}</Text>
              <Text style={styles.confirmMessage}>{confirmMessage}</Text>

              <View style={styles.confirmBtnRow}>
                <TouchableOpacity
                  style={[styles.popupBtn, { backgroundColor: '#FF416C' }]}
                  onPress={confirmAction as any}
                >
                  <Text style={styles.popupBtnText}>EVET, SİL</Text>
                </TouchableOpacity>
                <TouchableOpacity
                  style={[styles.popupBtn, { backgroundColor: '#334155' }]}
                  onPress={() => setIsConfirmVisible(false)}
                >
                  <Text style={styles.popupBtnText}>VAZGEÇ</Text>
                </TouchableOpacity>
              </View>
            </View>
          </View>
        </View>
      </Modal>

      {/* ========================================================================= */}
      {/* MODAL 6: RAPORLAR MENÜSÜ                                                  */}
      {/* ========================================================================= */}
      <Modal visible={isReportsMenuVisible} transparent={true} animationType="fade">
        <View style={styles.modalOverlayCenter}>
          <View style={styles.popupCardSmall}>
            <View style={styles.modalTopHeader}>
              <Text style={styles.popupTitle}>Raporlar</Text>
              <TouchableOpacity onPress={() => setIsReportsMenuVisible(false)}>
                <X color="#FFF" size={20} />
              </TouchableOpacity>
            </View>
            <View style={{ marginTop: 12, gap: 10 }}>
              <TouchableOpacity
                style={[styles.menuOptionBtn, !selectedStok && { opacity: 0.4 }]}
                onPress={handleGenerateStokHareketleriReport}
                disabled={!selectedStok}
              >
                <FileText color="#60A5FA" size={18} />
                <Text style={styles.menuOptionText}>Stok Hareketleri</Text>
              </TouchableOpacity>

              <TouchableOpacity style={styles.menuOptionBtn} onPress={handleGenerateTopluStokReport}>
                <Layers color="#10B981" size={18} />
                <Text style={styles.menuOptionText}>Toplu Stok Raporu</Text>
              </TouchableOpacity>
            </View>
          </View>
        </View>
      </Modal>

      {/* ========================================================================= */}
      {/* MODAL 7: EXCEL MENÜSÜ                                                     */}
      {/* ========================================================================= */}
      <Modal visible={isExcelMenuVisible} transparent={true} animationType="fade">
        <View style={styles.modalOverlayCenter}>
          <View style={styles.popupCardSmall}>
            <View style={styles.modalTopHeader}>
              <Text style={styles.popupTitle}>Excel İşlemleri</Text>
              <TouchableOpacity onPress={() => setIsExcelMenuVisible(false)}>
                <X color="#FFF" size={20} />
              </TouchableOpacity>
            </View>
            <View style={{ marginTop: 12, gap: 10 }}>
              <TouchableOpacity style={styles.menuOptionBtn} onPress={handleDownloadTemplate}>
                <Download color="#38BDF8" size={18} />
                <Text style={styles.menuOptionText}>Boş Şablon İndir</Text>
              </TouchableOpacity>

              <TouchableOpacity style={styles.menuOptionBtn} onPress={handleExportToExcel}>
                <Table color="#10B981" size={18} />
                <Text style={styles.menuOptionText}>Mevcut Stokları İndir (Excel)</Text>
              </TouchableOpacity>
            </View>
          </View>
        </View>
      </Modal>

      {/* iOS Sayısal Klavye Bitti Butonu */}
      <KeyboardDoneAccessory />
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#0A0A0A'
  },
  center: {
    justifyContent: 'center',
    alignItems: 'center',
    padding: 30
  },
  loadingText: {
    color: '#60A5FA',
    fontSize: 12,
    marginTop: 8
  },
  scrollContent: {
    padding: 12,
    paddingBottom: 40,
    gap: 12
  },

  // Desktop Panel Emulation
  desktopPanel: {
    backgroundColor: '#161616',
    borderRadius: 12,
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.12)',
    overflow: 'hidden'
  },
  panelHeader: {
    backgroundColor: 'rgba(255, 255, 255, 0.08)',
    borderBottomWidth: 1,
    borderBottomColor: 'rgba(255, 255, 255, 0.10)',
    paddingHorizontal: 14,
    paddingVertical: 10,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between'
  },
  headerTitleGroup: {
    flexDirection: 'row',
    alignItems: 'center'
  },
  panelTitle: {
    color: '#60A5FA',
    fontSize: 14,
    fontWeight: 'bold',
    marginLeft: 8
  },
  totalBadge: {
    flexDirection: 'row',
    alignItems: 'center'
  },
  totalBadgeLabel: {
    color: '#94A3B8',
    fontSize: 12
  },
  totalBadgeCount: {
    color: '#FFFFFF',
    fontSize: 13,
    fontWeight: 'bold'
  },

  // Toolbar & Search
  toolbarContainer: {
    paddingHorizontal: 12,
    paddingVertical: 10,
    backgroundColor: 'rgba(255, 255, 255, 0.02)',
    borderBottomWidth: 1,
    borderBottomColor: 'rgba(255, 255, 255, 0.06)'
  },
  searchRow: {
    flexDirection: 'row',
    gap: 8,
    alignItems: 'center'
  },
  searchBox: {
    flex: 1,
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#2A2A2A',
    borderRadius: 18,
    height: 36,
    borderWidth: 1,
    borderColor: '#444'
  },
  searchInput: {
    flex: 1,
    color: '#FFF',
    fontSize: 12,
    paddingHorizontal: 10
  },
  checkboxBtn: {
    paddingHorizontal: 10,
    height: 36,
    justifyContent: 'center',
    backgroundColor: 'rgba(255, 255, 255, 0.05)',
    borderRadius: 18,
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.12)'
  },
  checkboxBtnActive: {
    backgroundColor: '#0061FF',
    borderColor: '#0061FF'
  },
  checkboxText: {
    color: '#94A3B8',
    fontSize: 11,
    fontWeight: 'bold'
  },
  checkboxTextActive: {
    color: '#FFF'
  },
  pagerRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginTop: 8
  },
  selectAllBtn: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6
  },
  selectAllText: {
    color: '#94A3B8',
    fontSize: 11.5,
    fontWeight: '600'
  },
  pagerBox: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: 'rgba(255, 255, 255, 0.08)',
    borderRadius: 18,
    paddingHorizontal: 8,
    paddingVertical: 3
  },
  pagerText: {
    color: '#DDD',
    fontSize: 11.5,
    marginHorizontal: 8
  },

  // Stock List
  listContainer: {
    minHeight: 120
  },
  avaCard: {
    backgroundColor: '#0F0F0F',
    borderColor: 'rgba(255, 255, 255, 0.08)',
    borderWidth: 1.5,
    borderRadius: 12,
    marginBottom: 6,
    padding: 12
  },
  appleAvaCard: {
    marginBottom: 8,
    padding: 14,
  },
  appleAvaStokKodu: {
    fontSize: 12,
    color: '#0A84FF',
    fontWeight: '700',
    letterSpacing: -0.2,
    marginBottom: 2
  },
  appleAvaStokAdi: {
    fontSize: 14,
    color: '#FFFFFF',
    fontWeight: '600',
    letterSpacing: -0.2
  },
  appleAvaMiktar: {
    color: '#FFFFFF',
    fontWeight: '800',
    fontSize: 14,
    marginBottom: 2
  },
  appleAvaFiyat: {
    color: '#30D158',
    fontWeight: '700',
    fontSize: 13
  },
  avaCardSelected: {
    borderColor: '#60A5FA',
    backgroundColor: 'rgba(0, 97, 255, 0.18)'
  },
  cardRow: {
    flexDirection: 'row',
    alignItems: 'center'
  },
  cardCheckBtn: {
    marginRight: 10
  },
  cardRightCol: {
    alignItems: 'flex-end',
    justifyContent: 'center'
  },
  avaStokKodu: {
    fontSize: 11,
    color: '#60A5FA',
    fontWeight: 'bold',
    marginBottom: 2
  },
  avaStokAdi: {
    fontSize: 13,
    color: '#FFFFFF',
    fontWeight: '600'
  },
  avaGrup: {
    fontSize: 11,
    color: '#94A3B8',
    marginTop: 2
  },
  avaMiktar: {
    color: '#FFFFFF',
    fontWeight: 'bold',
    fontSize: 13,
    marginBottom: 2
  },
  avaFiyat: {
    color: '#10B981',
    fontWeight: 'bold',
    fontSize: 12
  },
  emptyContainer: {
    alignItems: 'center',
    justifyContent: 'center',
    paddingVertical: 30
  },
  emptyText: {
    color: '#64748B',
    fontSize: 12,
    marginTop: 8
  },

  // Hareketler
  hareketContainer: {
    minHeight: 100,
    padding: 10
  },
  hHeaderRow: {
    flexDirection: 'row',
    borderBottomWidth: 1.5,
    borderBottomColor: 'rgba(255, 255, 255, 0.1)',
    paddingBottom: 6,
    marginBottom: 4
  },
  hHeaderCol: {
    color: '#94A3B8',
    fontSize: 11,
    fontWeight: 'bold'
  },
  hRow: {
    flexDirection: 'row',
    borderBottomWidth: 1,
    borderBottomColor: 'rgba(255, 255, 255, 0.04)',
    paddingVertical: 7,
    alignItems: 'center'
  },
  hRowSelected: {
    backgroundColor: 'rgba(96, 165, 250, 0.20)',
    borderRadius: 6
  },
  hCol: {
    color: '#E2E8F0',
    fontSize: 11
  },
  hEmptyText: {
    color: '#64748B',
    textAlign: 'center',
    marginVertical: 20,
    fontSize: 12
  },

  // Form Elements
  banner: {
    flexDirection: 'row',
    alignItems: 'center',
    padding: 9,
    borderRadius: 8,
    borderWidth: 1,
    marginBottom: 8
  },
  bannerText: {
    fontSize: 11,
    marginLeft: 8,
    flex: 1
  },
  formTitle: {
    fontSize: 13,
    fontWeight: 'bold',
    marginBottom: 6,
    marginLeft: 2
  },
  formGrid: {
    marginBottom: 10,
    gap: 6
  },
  fieldRow: {
    gap: 3
  },
  twoColRow: {
    flexDirection: 'row',
    gap: 8
  },
  fLabel: {
    color: '#FFF',
    fontSize: 11.5,
    fontWeight: '600'
  },
  fLabelInline: {
    color: '#FFF',
    fontSize: 11.5,
    fontWeight: '600'
  },
  labelWithAddBtn: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: 3,
    gap: 4
  },
  miniAddBtn: {
    padding: 2
  },
  fInput: {
    backgroundColor: '#2A2A2A',
    borderRadius: 6,
    borderWidth: 1,
    borderColor: '#444',
    paddingHorizontal: 8,
    color: '#FFF',
    fontSize: 11.5,
    height: 30
  },
  fInputDisabled: {
    backgroundColor: '#1C1C1C',
    color: '#777777',
    borderColor: '#333'
  },
  dropdownBtn: {
    backgroundColor: '#2A2A2A',
    borderRadius: 6,
    borderWidth: 1,
    borderColor: '#444',
    paddingHorizontal: 8,
    justifyContent: 'center',
    height: 30
  },
  dropdownBtnText: {
    color: '#FFF',
    fontSize: 11.5
  },

  // Action Buttons Group
  actionButtonGroup: {
    marginTop: 4,
    gap: 5
  },
  actionRow: {
    flexDirection: 'row',
    gap: 4
  },
  actionBtn: {
    flex: 1,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    borderWidth: 1.2,
    borderRadius: 17,
    height: 34,
    backgroundColor: 'transparent'
  },
  actionBtnFull: {
    flex: 1,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    borderWidth: 1.2,
    borderRadius: 17,
    height: 34,
    backgroundColor: 'transparent'
  },
  actionBtnText: {
    color: '#FFF',
    fontSize: 10.5,
    fontWeight: '600',
    marginLeft: 4
  },
  statusMsg: {
    color: '#60A5FA',
    fontSize: 10.5,
    textAlign: 'center',
    marginTop: 6
  },

  // Modals Base
  modalOverlayCenter: {
    flex: 1,
    backgroundColor: 'rgba(0, 0, 0, 0.85)',
    justifyContent: 'center',
    alignItems: 'center',
    padding: 15
  },
  popupCardSmall: {
    backgroundColor: '#161616',
    borderRadius: 14,
    padding: 18,
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.12)',
    width: 320
  },
  modalTopHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center'
  },
  popupTitle: {
    color: '#FFF',
    fontSize: 15,
    fontWeight: 'bold',
    textAlign: 'center'
  },
  popupInput: {
    backgroundColor: '#2A2A2A',
    borderRadius: 8,
    borderWidth: 1,
    borderColor: '#444',
    padding: 10,
    color: '#FFF',
    fontSize: 13,
    height: 40,
    marginTop: 12
  },
  popupBtnRow: {
    flexDirection: 'row',
    gap: 8,
    marginTop: 15
  },
  popupBtn: {
    flex: 1,
    paddingVertical: 10,
    borderRadius: 8,
    alignItems: 'center',
    justifyContent: 'center'
  },
  popupBtnText: {
    color: '#FFF',
    fontWeight: 'bold',
    fontSize: 12
  },
  pickerItem: {
    paddingVertical: 10,
    paddingHorizontal: 12,
    borderRadius: 6,
    borderBottomWidth: 1,
    borderBottomColor: 'rgba(255, 255, 255, 0.05)'
  },
  pickerItemSelected: {
    backgroundColor: 'rgba(96, 165, 250, 0.2)'
  },
  pickerItemText: {
    color: '#FFF',
    fontSize: 13
  },
  menuOptionBtn: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#0A0A0A',
    padding: 14,
    borderRadius: 10,
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.08)',
    gap: 12
  },
  menuOptionText: {
    color: '#FFF',
    fontSize: 13,
    fontWeight: '600'
  },

  // Transaction Modal Card
  transactionModalCard: {
    backgroundColor: '#080A0C',
    borderRadius: 16,
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.2)',
    width: 350,
    overflow: 'hidden'
  },
  transactionHeader: {
    backgroundColor: 'rgba(96, 165, 250, 0.20)',
    paddingHorizontal: 16,
    paddingVertical: 12,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between'
  },
  transactionHeaderTitle: {
    color: '#FFF',
    fontWeight: 'bold',
    fontSize: 14
  },
  transactionHeaderSubtitle: {
    color: '#CCFFFFFF',
    fontSize: 11
  },
  transactionBody: {
    padding: 16,
    gap: 10
  },
  tRow: {
    flexDirection: 'row',
    alignItems: 'center'
  },
  tLabel: {
    width: 95,
    color: '#AAA',
    fontSize: 12,
    fontWeight: '600'
  },
  tInput: {
    flex: 1,
    backgroundColor: '#2A2A2A',
    borderRadius: 6,
    borderWidth: 1,
    borderColor: '#444',
    paddingHorizontal: 10,
    paddingVertical: 6,
    color: '#FFF',
    fontSize: 12,
    height: 34
  },
  tToggleGroup: {
    flex: 1,
    flexDirection: 'row',
    gap: 6
  },
  tToggleBtn: {
    flex: 1,
    paddingVertical: 7,
    borderRadius: 6,
    backgroundColor: '#2A2A2A',
    alignItems: 'center',
    borderWidth: 1,
    borderColor: '#444'
  },
  tToggleBtnActiveGreen: {
    backgroundColor: '#10B981',
    borderColor: '#10B981'
  },
  tToggleBtnActiveOrange: {
    backgroundColor: '#F59E0B',
    borderColor: '#F59E0B'
  },
  tToggleText: {
    color: '#AAA',
    fontSize: 11,
    fontWeight: 'bold'
  },
  tToggleTextActive: {
    color: '#FFF'
  },
  tSaveBtn: {
    backgroundColor: '#10B981',
    height: 44,
    borderRadius: 8,
    flexDirection: 'row',
    justifyContent: 'center',
    alignItems: 'center',
    marginTop: 6,
    gap: 8
  },
  tSaveBtnText: {
    color: '#FFF',
    fontWeight: 'bold',
    fontSize: 13
  },

  // Confirm Modal
  confirmModalCard: {
    backgroundColor: '#161616',
    borderRadius: 16,
    borderWidth: 1,
    borderColor: '#FF416C',
    width: 320,
    overflow: 'hidden'
  },
  confirmHeader: {
    backgroundColor: 'rgba(255, 65, 108, 0.20)',
    paddingHorizontal: 16,
    paddingVertical: 12,
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8
  },
  confirmHeaderTitle: {
    color: '#FFF',
    fontWeight: 'bold',
    fontSize: 14
  },
  confirmBody: {
    padding: 20,
    alignItems: 'center'
  },
  confirmTitle: {
    color: '#FFF',
    fontWeight: 'bold',
    fontSize: 15,
    textAlign: 'center',
    marginBottom: 6
  },
  confirmMessage: {
    color: '#BBB',
    fontSize: 12,
    textAlign: 'center',
    marginBottom: 16
  },
  confirmBtnRow: {
    flexDirection: 'row',
    gap: 8,
    width: '100%'
  },
  appleGroupedContainer: {
    backgroundColor: '#1C1C1E',
    borderRadius: 14,
    overflow: 'hidden',
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.06)',
    marginHorizontal: 10,
    marginBottom: 10,
  }
});
