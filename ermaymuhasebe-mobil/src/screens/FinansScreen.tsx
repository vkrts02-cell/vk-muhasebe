import React, { useState, useEffect, useRef } from "react";
import {
  StyleSheet,
  Text,
  View,
  SafeAreaView,
  FlatList,
  TextInput,
  ActivityIndicator,
  TouchableOpacity,
  Modal,
  ScrollView,
  Alert,
  Image,
  PanResponder,
  Animated,
  Dimensions,
} from "react-native";
import {
  Search,
  CreditCard,
  Plus,
  X,
  Save,
  ArrowDownLeft,
  ArrowUpRight,
  ArrowLeft,
  Landmark,
  RefreshCw,
  Wallet,
  User,
  Clock,
  CalendarDays,
  Paperclip,
  Camera,
  Edit3,
  Trash2,
} from "lucide-react-native";
import { FlashList, ListRenderItemInfo } from "@shopify/flash-list";
import FadeInView from "../components/FadeInView";
import {
  KeyboardDoneAccessory,
  KEYBOARD_ACCESSORY_ID,
} from "../components/KeyboardDoneAccessory";
import { generateInt32Id } from "../utils/IdGenerator";
import Svg, { Path, Line, Text as SvgText } from "react-native-svg";
import {
  subscribeToPath,
  writeData,
  readData,
  deleteData,
  splitAccounts,
  mergeKasalar,
} from "../services/firebase";
import {
  saveFinancialTransaction,
  deleteFinancialTransaction,
} from "../services/transactionService";
import {
  hesapKarneler,
  hesapAlisFaturaKarnesi,
  hesapOrtalamaVadeler,
  hesapFinansTrend,
} from "../services/finansAnalizUtils";
import * as ImagePicker from "expo-image-picker";

const formatMoney = (val: number) => {
  return new Intl.NumberFormat("tr-TR", {
    style: "currency",
    currency: "TRY",
  }).format(val);
};

const { width: SCREEN_WIDTH } = Dimensions.get("window");

function SwipeableModal({
  visible,
  onClose,
  children,
}: {
  visible: boolean;
  onClose: () => void;
  children: React.ReactNode;
}) {
  const animX = useRef(new Animated.Value(SCREEN_WIDTH)).current;
  const [showModal, setShowModal] = useState(visible);

  const startX = useRef(0);
  const startY = useRef(0);
  const isSwiping = useRef(false);

  useEffect(() => {
    if (visible) {
      setShowModal(true);
      animX.setValue(SCREEN_WIDTH);
      Animated.timing(animX, {
        toValue: 0,
        duration: 250,
        useNativeDriver: true,
      }).start();
    } else if (showModal) {
      Animated.timing(animX, {
        toValue: SCREEN_WIDTH,
        duration: 220,
        useNativeDriver: true,
      }).start(() => {
        setShowModal(false);
      });
    }
  }, [visible]);

  const handleCloseWithAnim = () => {
    Animated.timing(animX, {
      toValue: SCREEN_WIDTH,
      duration: 220,
      useNativeDriver: true,
    }).start(() => {
      onClose();
    });
  };

  const panResponder = useRef(
    PanResponder.create({
      onStartShouldSetPanResponder: () => false,
      onStartShouldSetPanResponderCapture: () => false,
      onMoveShouldSetPanResponder: (evt, gestureState) => {
        return (
          gestureState.dx > 5 &&
          Math.abs(gestureState.dx) > Math.abs(gestureState.dy) * 1.1
        );
      },
      onMoveShouldSetPanResponderCapture: (evt, gestureState) => {
        return (
          gestureState.dx > 5 &&
          Math.abs(gestureState.dx) > Math.abs(gestureState.dy) * 1.1
        );
      },
      onPanResponderMove: (evt, gestureState) => {
        if (gestureState.dx > 0) {
          animX.setValue(gestureState.dx);
        }
      },
      onPanResponderRelease: (evt, gestureState) => {
        if (gestureState.dx > 35 || gestureState.vx > 0.2) {
          handleCloseWithAnim();
        } else {
          Animated.spring(animX, {
            toValue: 0,
            useNativeDriver: true,
            bounciness: 4,
          }).start();
        }
      },
      onPanResponderTerminate: (evt, gestureState) => {
        if (gestureState.dx > 35 || gestureState.vx > 0.2) {
          handleCloseWithAnim();
        } else {
          Animated.spring(animX, {
            toValue: 0,
            useNativeDriver: true,
            bounciness: 4,
          }).start();
        }
      },
    }),
  ).current;

  if (!showModal && !visible) return null;

  return (
    <Modal
      visible={showModal}
      animationType="none"
      transparent={true}
      onRequestClose={handleCloseWithAnim}
    >
      <SafeAreaView
        style={styles.modalOverlay}
        onTouchStart={(e) => {
          startX.current = e.nativeEvent.pageX;
          startY.current = e.nativeEvent.pageY;
          isSwiping.current = false;
        }}
        onTouchMove={(e) => {
          const dx = e.nativeEvent.pageX - startX.current;
          const dy = e.nativeEvent.pageY - startY.current;
          if (dx > 10 && Math.abs(dx) > Math.abs(dy) * 1.1) {
            isSwiping.current = true;
            animX.setValue(dx);
          }
        }}
        onTouchEnd={(e) => {
          const dx = e.nativeEvent.pageX - startX.current;
          const dy = e.nativeEvent.pageY - startY.current;
          if (dx > 35 && Math.abs(dx) > Math.abs(dy) * 1.1) {
            handleCloseWithAnim();
          } else if (isSwiping.current) {
            Animated.spring(animX, {
              toValue: 0,
              useNativeDriver: true,
              bounciness: 4,
            }).start();
          }
          isSwiping.current = false;
        }}
      >
        <Animated.View
          style={[
            styles.modalContent,
            { transform: [{ translateX: animX }] },
          ]}
          {...panResponder.panHandlers}
        >
          {children}
        </Animated.View>
      </SafeAreaView>
    </Modal>
  );
}

function SwipeableOverlay({
  visible,
  onClose,
  children,
}: {
  visible: boolean;
  onClose: () => void;
  children: React.ReactNode;
}) {
  const animX = useRef(new Animated.Value(SCREEN_WIDTH)).current;
  const [showOverlay, setShowOverlay] = useState(visible);

  const startX = useRef(0);
  const startY = useRef(0);
  const isSwiping = useRef(false);

  useEffect(() => {
    if (visible) {
      setShowOverlay(true);
      animX.setValue(SCREEN_WIDTH);
      Animated.timing(animX, {
        toValue: 0,
        duration: 250,
        useNativeDriver: true,
      }).start();
    } else if (showOverlay) {
      Animated.timing(animX, {
        toValue: SCREEN_WIDTH,
        duration: 220,
        useNativeDriver: true,
      }).start(() => {
        setShowOverlay(false);
      });
    }
  }, [visible]);

  const handleCloseWithAnim = () => {
    Animated.timing(animX, {
      toValue: SCREEN_WIDTH,
      duration: 220,
      useNativeDriver: true,
    }).start(() => {
      onClose();
    });
  };

  const panResponder = useRef(
    PanResponder.create({
      onStartShouldSetPanResponder: () => false,
      onStartShouldSetPanResponderCapture: () => false,
      onMoveShouldSetPanResponder: (evt, gestureState) => {
        return (
          gestureState.dx > 5 &&
          Math.abs(gestureState.dx) > Math.abs(gestureState.dy) * 1.1
        );
      },
      onMoveShouldSetPanResponderCapture: (evt, gestureState) => {
        return (
          gestureState.dx > 5 &&
          Math.abs(gestureState.dx) > Math.abs(gestureState.dy) * 1.1
        );
      },
      onPanResponderMove: (evt, gestureState) => {
        if (gestureState.dx > 0) {
          animX.setValue(gestureState.dx);
        }
      },
      onPanResponderRelease: (evt, gestureState) => {
        if (gestureState.dx > 35 || gestureState.vx > 0.2) {
          handleCloseWithAnim();
        } else {
          Animated.spring(animX, {
            toValue: 0,
            useNativeDriver: true,
            bounciness: 4,
          }).start();
        }
      },
      onPanResponderTerminate: (evt, gestureState) => {
        if (gestureState.dx > 35 || gestureState.vx > 0.2) {
          handleCloseWithAnim();
        } else {
          Animated.spring(animX, {
            toValue: 0,
            useNativeDriver: true,
            bounciness: 4,
          }).start();
        }
      },
    }),
  ).current;

  if (!showOverlay && !visible) return null;

  return (
    <Animated.View
      style={[
        styles.absoluteOverlay,
        { transform: [{ translateX: animX }] },
      ]}
      onTouchStart={(e) => {
        startX.current = e.nativeEvent.pageX;
        startY.current = e.nativeEvent.pageY;
        isSwiping.current = false;
      }}
      onTouchMove={(e) => {
        const dx = e.nativeEvent.pageX - startX.current;
        const dy = e.nativeEvent.pageY - startY.current;
        if (dx > 10 && Math.abs(dx) > Math.abs(dy) * 1.1) {
          isSwiping.current = true;
          animX.setValue(dx);
        }
      }}
      onTouchEnd={(e) => {
        const dx = e.nativeEvent.pageX - startX.current;
        const dy = e.nativeEvent.pageY - startY.current;
        if (dx > 35 && Math.abs(dx) > Math.abs(dy) * 1.1) {
          handleCloseWithAnim();
        } else if (isSwiping.current) {
          Animated.spring(animX, {
            toValue: 0,
            useNativeDriver: true,
            bounciness: 4,
          }).start();
        }
        isSwiping.current = false;
      }}
      {...panResponder.panHandlers}
    >
      {children}
    </Animated.View>
  );
}

export default function FinansScreen({ route, navigation }: any) {
  const loadedEditIdRef = useRef<any>(null);
  const [bankalar, setBankalar] = useState<any[]>([]);
  const [kasalar, setKasalar] = useState<any[]>([]);
  const [kasaHareketler, setKasaHareketler] = useState<any[]>([]);
  const [bankaHareketler, setBankaHareketler] = useState<any[]>([]);
  const [cariler, setCariler] = useState<any[]>([]);
  const [cekler, setCekler] = useState<any[]>([]);
  const [senetler, setSenetler] = useState<any[]>([]);
  const [faturalar, setFaturalar] = useState<any[]>([]);
  const [cariHareketler, setCariHareketler] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  const [activeTab, setActiveTab] = useState<
    "dashboard" | "nakit" | "cek" | "kk" | "eft"
  >("dashboard");
  const bankKasalarRef = useRef<any[]>([]);
  const legacyKasalarRef = useRef<any[]>([]);

  // --- Desktop parite: Dashboard dönem filtresi (Haftalık/Aylık/6 Aylık/Yıllık) ---
  const [dashboardPeriod, setDashboardPeriod] = useState<
    "weekly" | "monthly" | "6month" | "yearly"
  >("monthly");
  const [trendPeriod, setTrendPeriod] = useState<
    "Daily" | "Weekly" | "Monthly" | "Yearly"
  >("Monthly");

  // --- KK / EFT işlemleri (desktop KrediKartiListViewModel / EFTListViewModel eşleniği) ---
  const [kkIslemler, setKkIslemler] = useState<any[]>([]);
  const [eftIslemler, setEftIslemler] = useState<any[]>([]);

  // Çek/Senet filtreleri (desktop: vade -3 ay/+3 ay, durum, metin)
  const [cekDurumFilter, setCekDurumFilter] = useState("Hepsi");
  const [cekSearch, setCekSearch] = useState("");

  // Çek ciro (yönlendirilen tedarikçi) seçimi
  const [ciroCek, setCiroCek] = useState<any | null>(null);
  const [isCiroPickerOpen, setIsCiroPickerOpen] = useState(false);

  // KK / EFT işlem formu (desktop KrediKartiListViewModel / EFTListViewModel)
  const [isKkEftFormOpen, setIsKkEftFormOpen] = useState(false);
  const [kkEftMode, setKkEftMode] = useState<"kk" | "eft">("kk");
  const [kkEftEditingId, setKkEftEditingId] = useState<string | number | null>(
    null,
  );
  const [kkEftTutar, setKkEftTutar] = useState("");
  const [kkEftTarih, setKkEftTarih] = useState(
    new Date().toISOString().split("T")[0],
  );
  const [kkEftMusteri, setKkEftMusteri] = useState<any | null>(null);
  const [kkEftBanka, setKkEftBanka] = useState("");
  const [kkEftKartNo, setKkEftKartNo] = useState("");
  const [kkEftHesapNo, setKkEftHesapNo] = useState("");
  const [kkEftDurum, setKkEftDurum] = useState("Portföyde");
  const [kkEftOnayDekontNo, setKkEftOnayDekontNo] = useState("");
  const [kkEftSlipPath, setKkEftSlipPath] = useState("");
  const [kkEftAciklama, setKkEftAciklama] = useState("");
  const [kkEftYonlendirilen, setKkEftYonlendirilen] = useState<any | null>(
    null,
  );
  const [isKkEftMusteriPickerOpen, setIsKkEftMusteriPickerOpen] =
    useState(false);
  const [isKkEftSupPickerOpen, setIsKkEftSupPickerOpen] = useState(false);

  // Modals state
  const [isFormOpen, setIsFormOpen] = useState(false);
  const [isCariOverlayOpen, setIsCariOverlayOpen] = useState(false);

  // Saving guard to prevent double-tap double-save
  const [isSaving, setIsSaving] = useState(false);

  // Kasa / Banka hesap tanımlama
  const [isHesapFormOpen, setIsHesapFormOpen] = useState(false);
  const [hesapEditingId, setHesapEditingId] = useState<number | null>(null);
  const [hesapTur, setHesapTur] = useState<"Kasa" | "Banka">("Kasa");
  const [hesapAdi, setHesapAdi] = useState("");
  const [hesapDoviz, setHesapDoviz] = useState("TL");
  const [hesapYetkili, setHesapYetkili] = useState("");
  const [hesapAcilisBakiye, setHesapAcilisBakiye] = useState("0");
  const [hesapSube, setHesapSube] = useState("");
  const [hesapNo, setHesapNo] = useState("");
  const [hesapIban, setHesapIban] = useState("");
  const [hesapKartTuru, setHesapKartTuru] = useState("");

  // Kasa Detayı ve Hareketleri (Desktop paritesi)
  const [selectedKasaForDetay, setSelectedKasaForDetay] = useState<any | null>(null);
  const [isKasaDetayOpen, setIsKasaDetayOpen] = useState(false);
  const [kasaDetaySearch, setKasaDetaySearch] = useState("");
  const touchStartX = useRef(0);
  const touchStartY = useRef(0);

  const handleOpenKasaDetay = (kasa: any) => {
    setSelectedKasaForDetay(kasa);
    setKasaDetaySearch("");
    setIsKasaDetayOpen(true);
  };

  const handleCloseKasaDetay = () => {
    setSelectedKasaForDetay(null);
    setIsKasaDetayOpen(false);
  };

  const kasaDetayPanResponder = useRef(
    PanResponder.create({
      onStartShouldSetPanResponder: () => false,
      onStartShouldSetPanResponderCapture: () => false,
      onMoveShouldSetPanResponder: (evt, gestureState) => {
        return (
          gestureState.dx > 15 &&
          Math.abs(gestureState.dx) > Math.abs(gestureState.dy) * 1.2
        );
      },
      onMoveShouldSetPanResponderCapture: (evt, gestureState) => {
        return (
          gestureState.dx > 15 &&
          Math.abs(gestureState.dx) > Math.abs(gestureState.dy) * 1.2
        );
      },
      onPanResponderRelease: (evt, gestureState) => {
        if (gestureState.dx > 35 || gestureState.vx > 0.2) {
          handleCloseKasaDetay();
        }
      },
      onPanResponderTerminate: (evt, gestureState) => {
        if (gestureState.dx > 35 || gestureState.vx > 0.2) {
          handleCloseKasaDetay();
        }
      },
    }),
  ).current;

  const handleTouchStart = (e: any) => {
    touchStartX.current = e.nativeEvent.pageX;
    touchStartY.current = e.nativeEvent.pageY;
  };

  const handleTouchEnd = (e: any, closeFn: () => void) => {
    const dx = e.nativeEvent.pageX - touchStartX.current;
    const dy = e.nativeEvent.pageY - touchStartY.current;
    if (dx > 35 && Math.abs(dx) > Math.abs(dy) * 1.2) {
      closeFn();
    }
  };

  const createSwipePanResponder = (closeFn: () => void) =>
    PanResponder.create({
      onStartShouldSetPanResponder: () => false,
      onStartShouldSetPanResponderCapture: () => false,
      onMoveShouldSetPanResponder: (evt, gestureState) => {
        return (
          gestureState.dx > 15 &&
          Math.abs(gestureState.dx) > Math.abs(gestureState.dy) * 1.2
        );
      },
      onMoveShouldSetPanResponderCapture: (evt, gestureState) => {
        return (
          gestureState.dx > 15 &&
          Math.abs(gestureState.dx) > Math.abs(gestureState.dy) * 1.2
        );
      },
      onPanResponderRelease: (evt, gestureState) => {
        if (gestureState.dx > 35 || gestureState.vx > 0.2) {
          closeFn();
        }
      },
      onPanResponderTerminate: (evt, gestureState) => {
        if (gestureState.dx > 35 || gestureState.vx > 0.2) {
          closeFn();
        }
      },
    });

  const formPanResponder = useRef(createSwipePanResponder(() => setIsFormOpen(false))).current;
  const hesapFormPanResponder = useRef(createSwipePanResponder(() => setIsHesapFormOpen(false))).current;
  const moveEditPanResponder = useRef(createSwipePanResponder(() => setIsMoveEditOpen(false))).current;
  const cekEditPanResponder = useRef(createSwipePanResponder(() => setIsCekEditOpen(false))).current;
  const kkEftFormPanResponder = useRef(createSwipePanResponder(() => setIsKkEftFormOpen(false))).current;
  const cariOverlayPanResponder = useRef(createSwipePanResponder(() => setIsCariOverlayOpen(false))).current;
  const kkEftMusteriPickerPanResponder = useRef(createSwipePanResponder(() => setIsKkEftMusteriPickerOpen(false))).current;
  const kkEftSupPickerPanResponder = useRef(createSwipePanResponder(() => setIsKkEftSupPickerOpen(false))).current;
  const ciroPickerPanResponder = useRef(createSwipePanResponder(() => { setIsCiroPickerOpen(false); setCiroCek(null); })).current;

  // Hareket düzenleme/silme
  const [moveEdit, setMoveEdit] = useState<any | null>(null);
  const [isMoveEditOpen, setIsMoveEditOpen] = useState(false);

  // Çek düzenleme
  const [cekEditId, setCekEditId] = useState<number | null>(null);
  const [isCekEditOpen, setIsCekEditOpen] = useState(false);

  // Form fields state
  const [editingId, setEditingId] = useState<number | null>(null);
  const [islemTuru, setIslemTuru] = useState<
    "Tahsilat" | "Ödeme" | "Virman" | "Alacak Dekontu" | "Borç Dekontu"
  >("Tahsilat");
  const [odemeYontemi, setOdemeYontemi] = useState<
    "Nakit" | "Kredi Kartı" | "Havale/EFT" | "Çek"
  >("Nakit");
  const [selectedCari, setSelectedCari] = useState<any | null>(null);
  const [tutar, setTutar] = useState("");
  const [tarih, setTarih] = useState(new Date().toISOString().split("T")[0]);
  const [aciklama, setAciklama] = useState("");

  // Account selections
  const [selectedKasaId, setSelectedKasaId] = useState<number | null>(null);
  const [selectedBankaId, setSelectedBankaId] = useState<number | null>(null);
  const [sourceKasaId, setSourceKasaId] = useState<number | null>(null);
  const [destKasaId, setDestKasaId] = useState<number | null>(null);

  // Kredi Kartı / Havale Ek Alanları ve Görselleri (Base64 / URL)
  const [kkBanka, setKkBanka] = useState("");
  const [kkKartNo, setKkKartNo] = useState("");
  const [kkOnayKodu, setKkOnayKodu] = useState("");
  const [kkSlipPath, setKkSlipPath] = useState("");

  // Çek Ek Alanları ve Görselleri
  const [cekTuru, setCekTuru] = useState<"Alınan" | "Verilen">("Alınan");
  const [asilBorclu, setAsilBorclu] = useState("");
  const [cekBanka, setCekBanka] = useState("");
  const [cekSube, setCekSube] = useState("");
  const [cekSeriNo, setCekSeriNo] = useState("");
  const [cekVadeTarihi, setCekVadeTarihi] = useState(
    new Date().toISOString().split("T")[0],
  );
  const [cekPortfoyNo, setCekPortfoyNo] = useState("");
  const [cekGorselYoluOn, setCekGorselYoluOn] = useState("");
  const [cekGorselYoluArka, setCekGorselYoluArka] = useState("");
  const [cekDurum, setCekDurum] = useState("Portföyde");

  // Selector searches
  const [cariSearch, setCariSearch] = useState("");

  useEffect(() => {
    const listHelper = (
      path: string,
      setter: React.Dispatch<React.SetStateAction<any[]>>,
    ) => {
      return subscribeToPath(path, (data) => {
        if (!data) {
          setter([]);
        } else {
          const list = Array.isArray(data)
            ? data.map((item, idx) => item ? ({ ...item, firebaseKey: String(item?.id ?? idx) }) : null).filter(Boolean)
            : Object.keys(data).map((key) => ({
                ...data[key],
                firebaseKey: key,
              }));
          setter(list.filter((x: any) => x && x.isDeleted !== true));
        }
      });
    };

    const unsubBankalar = listHelper("Bankalar", (data: any) => {
      const raw = Array.isArray(data)
        ? data.map((item, idx) => item ? ({ ...item, firebaseKey: String(item?.id ?? idx) }) : null).filter(Boolean)
        : Object.keys(data || {}).map((key) => ({
            ...data[key],
            firebaseKey: key,
          }));
      const activeRaw = raw.filter((x) => x && x.isDeleted !== true);
      const { kasalar, bankalar } = splitAccounts(activeRaw);
      bankKasalarRef.current = kasalar;
      setKasalar(mergeKasalar(bankKasalarRef.current, legacyKasalarRef.current));
      setBankalar(bankalar);
    });
    const unsubKasalar = listHelper("Kasalar", (data: any) => {
      const raw = Array.isArray(data)
        ? data.map((item, idx) => item ? ({ ...item, firebaseKey: String(item?.id ?? idx) }) : null).filter(Boolean)
        : Object.keys(data || {}).map((key) => ({
            ...data[key],
            firebaseKey: key,
          }));
      legacyKasalarRef.current = raw.filter((k) => k && k.isDeleted !== true);
      setKasalar(mergeKasalar(bankKasalarRef.current, legacyKasalarRef.current));
    });
    const unsubKasaH = listHelper("KasaHareketler", setKasaHareketler);
    const unsubBankaH = listHelper("BankaHareketler", setBankaHareketler);
    const unsubCariler = listHelper("Cariler", setCariler);
    const unsubCekler = listHelper("Cekler", setCekler);
    const unsubSenetler = listHelper("Senetler", setSenetler);
    const unsubFaturalar = listHelper("Faturalar", setFaturalar);
    const unsubCariH = listHelper("CariHareketler", setCariHareketler);
    const unsubKk = listHelper("KrediKartlari", setKkIslemler);
    const unsubEft = listHelper("EftIslemleri", setEftIslemler);

    setTimeout(() => setLoading(false), 1000);

    return () => {
      unsubBankalar();
      unsubKasalar();
      unsubKasaH();
      unsubBankaH();
      unsubCariler();
      unsubCekler();
      unsubSenetler();
      unsubFaturalar();
      unsubCariH();
      unsubKk();
      unsubEft();
    };
  }, []);

  useEffect(() => {
    if (route?.params?.initialCari) {
      const selected = route.params.initialCari;
      setSelectedCari(selected);
      if (route.params.initialIslemTuru) {
        setIslemTuru(route.params.initialIslemTuru);
      }
      setTutar("");
      setTarih(new Date().toISOString().split("T")[0]);
      setAciklama("");
      setSelectedKasaId(null);
      setSelectedBankaId(null);
      setIsFormOpen(true);
    } else if (route?.params?.editHareketId) {
      if (loadedEditIdRef.current === route.params.editHareketId) return;
      loadedEditIdRef.current = route.params.editHareketId;
      const loadHareketForEdit = async () => {
        setLoading(true);
        try {
          const hId = route.params.editHareketId;
          const hData =
            cariHareketler.find((h) => String(h.id) === String(hId)) ||
            (await readData(`CariHareketler/${hId}`));
          if (hData) {
            setEditingId(hId);

            // İşlem türünü ve ödeme yöntemini ayrıştır
            const rawTur = String(hData.islemTuru || "");
            let baseTur: "Tahsilat" | "Ödeme" | "Virman" | "Alacak Dekontu" | "Borç Dekontu" = "Tahsilat";
            if (rawTur.includes("Ödeme") || rawTur.includes("Odeme")) {
              baseTur = "Ödeme";
            } else if (rawTur.includes("Alacak Dekont") || rawTur.includes("Alacaklandır") || rawTur.includes("Alacaklandir") || rawTur.includes("Alacak")) {
              baseTur = "Alacak Dekontu";
            } else if (rawTur.includes("Borç Dekont") || rawTur.includes("Borc Dekont") || rawTur.includes("Borçlandır") || rawTur.includes("Borclandir") || rawTur.includes("Borç") || rawTur.includes("Borc")) {
              baseTur = "Borç Dekontu";
            } else if (rawTur.includes("Virman")) {
              baseTur = "Virman";
            } else {
              baseTur = ((parseFloat(hData.borc) || 0) > 0) ? "Borç Dekontu" : "Tahsilat";
            }
            setIslemTuru(baseTur);

            let detectedMethod: "Nakit" | "Kredi Kartı" | "Havale/EFT" | "Çek" = "Nakit";
            if (rawTur.includes("KK") || rawTur.includes("Kredi")) {
              detectedMethod = "Kredi Kartı";
            } else if (rawTur.includes("EFT") || rawTur.includes("Havale")) {
              detectedMethod = "Havale/EFT";
            } else if (rawTur.includes("Çek") || rawTur.includes("Cek")) {
              detectedMethod = "Çek";
            }
            setOdemeYontemi(detectedMethod);

            // Bağlı hesap (kasa/banka) tespit et
            const evrak = String(hData.evrakNo || "").trim();
            const ref = String(hData.refId || "").trim();
            let linkedKasaId = hData.kasaId ?? null;
            let linkedBankaId = hData.bankaId ?? null;

            if (!linkedKasaId && !linkedBankaId) {
              const khRaw = (await readData("KasaHareketler")) || {};
              const khList = Array.isArray(khRaw) ? khRaw.filter(Boolean) : Object.values(khRaw);
              const matchedKh: any = khList.find((kh: any) => 
                (ref && (kh.refId === ref || kh.RefId === ref)) ||
                (evrak && (String(kh.evrakNo || kh.EvrakNo).trim() === evrak))
              );
              if (matchedKh) {
                linkedKasaId = matchedKh.kasaId || matchedKh.hesapId || matchedKh.KasaId;
              } else {
                const bhRaw = (await readData("BankaHareketler")) || {};
                const bhList = Array.isArray(bhRaw) ? bhRaw.filter(Boolean) : Object.values(bhRaw);
                const matchedBh: any = bhList.find((bh: any) =>
                  (ref && (bh.refId === ref || bh.RefId === ref)) ||
                  (evrak && (String(bh.evrakNo || bh.EvrakNo).trim() === evrak))
                );
                if (matchedBh) {
                  linkedBankaId = matchedBh.bankaId || matchedBh.hesapId || matchedBh.BankaId;
                }
              }
            }

            if (linkedKasaId) {
              setSelectedKasaId(linkedKasaId);
            }
            if (linkedBankaId) {
              setSelectedBankaId(linkedBankaId);
            }

            const cariRef =
              cariler.find((c) => String(c.id) === String(hData.cariId)) ||
              (await readData(`Cariler/${hData.cariId}`));
            setSelectedCari(cariRef);

            const amountVal = parseFloat(hData.borc || hData.alacak || hData.Borc || hData.Alacak) || 0;
            setTutar(amountVal > 0 ? amountVal.toString() : "");
            setTarih(hData.tarih || new Date().toISOString().split("T")[0]);

            let cleanAciklama = String(hData.aciklama || "");
            cleanAciklama = cleanAciklama.replace(/^\[(Nakit|Kredi Kartı|Havale\/EFT|Çek)\]\s*/i, "");
            setAciklama(cleanAciklama);

            setIsFormOpen(true);
          }
        } catch (err) {
          Alert.alert("Hata", "Cari işlem düzenleme modunda yüklenemedi.");
        } finally {
          setLoading(false);
        }
      };
      loadHareketForEdit();
    }
  }, [route?.params, cariHareketler, cariler]);

  // Set Cek Türü automatically based on islemTuru
  useEffect(() => {
    if (islemTuru === "Tahsilat" || islemTuru === "Alacak Dekontu") {
      setCekTuru("Alınan");
    } else if (islemTuru === "Ödeme" || islemTuru === "Borç Dekontu") {
      setCekTuru("Verilen");
    }
  }, [islemTuru]);

  useEffect(() => {
    if (odemeYontemi === "Çek" && !cekPortfoyNo) {
      setCekPortfoyNo(`CK-${Date.now().toString().substring(6)}`);
    }
  }, [odemeYontemi, cekPortfoyNo]);

  useEffect(() => {
    if (selectedCari) {
      setAsilBorclu(selectedCari.unvan || "");
    } else {
      setAsilBorclu("");
    }
  }, [selectedCari]);

  const resetForm = () => {
    loadedEditIdRef.current = null;
    setEditingId(null);
    if (navigation?.setParams) {
      navigation.setParams({ editHareketId: undefined, initialIslemTuru: undefined });
    }
    setIslemTuru("Tahsilat");
    setOdemeYontemi("Nakit");
    setSelectedCari(null);
    setTutar("");
    setTarih(new Date().toISOString().split("T")[0]);
    setAciklama("");
    setSelectedKasaId(null);
    setSelectedBankaId(null);
    setSourceKasaId(null);
    setDestKasaId(null);
    setKkBanka("");
    setKkKartNo("");
    setKkOnayKodu("");
    setKkSlipPath("");
    setAsilBorclu("");
    setCekBanka("");
    setCekSube("");
    setCekSeriNo("");
    setCekVadeTarihi(new Date().toISOString().split("T")[0]);
    setCekPortfoyNo("");
    setCekGorselYoluOn("");
    setCekGorselYoluArka("");
    setCekDurum("Portföyde");
  };

  const resetFormButKeepType = (type: string) => {
    setOdemeYontemi("Nakit");
    setSelectedCari(null);
    setTutar("");
    setTarih(new Date().toISOString().split("T")[0]);
    setAciklama("");
    setSelectedKasaId(null);
    setSelectedBankaId(null);
    setSourceKasaId(null);
    setDestKasaId(null);
    setKkBanka("");
    setKkKartNo("");
    setKkOnayKodu("");
    setKkSlipPath("");
    setAsilBorclu("");
    setCekBanka("");
    setCekSube("");
    setCekSeriNo("");
    setCekVadeTarihi(new Date().toISOString().split("T")[0]);
    setCekPortfoyNo("");
    setCekGorselYoluOn("");
    setCekGorselYoluArka("");
    setCekDurum("Portföyde");
  };

  const pickImage = async (setter: (uri: string) => void) => {
    try {
      const { status } =
        await ImagePicker.requestMediaLibraryPermissionsAsync();
      if (status !== "granted") {
        Alert.alert(
          "İzin Gerekli",
          "Galeriye erişim izni vermeniz gerekmektedir.",
        );
        return;
      }

      const result = await ImagePicker.launchImageLibraryAsync({
        mediaTypes: ImagePicker.MediaTypeOptions.Images,
        allowsEditing: true,
        quality: 0.7,
        base64: true,
      });

      if (!result.canceled && result.assets && result.assets.length > 0) {
        const asset = result.assets[0];
        const base64Data = `data:${asset.mimeType || "image/jpeg"};base64,${asset.base64}`;
        setter(base64Data);
      }
    } catch (error) {
      console.error("Görsel seçilemedi:", error);
      Alert.alert("Hata", "Görsel seçilirken bir sorun oluştu.");
    }
  };

  const handleOpenAdd = () => {
    resetForm();
    setIsFormOpen(true);
  };

  const resetHesapForm = () => {
    setHesapEditingId(null);
    setHesapTur("Kasa");
    setHesapAdi("");
    setHesapDoviz("TL");
    setHesapYetkili("");
    setHesapAcilisBakiye("0");
    setHesapSube("");
    setHesapNo("");
    setHesapIban("");
    setHesapKartTuru("");
  };

  const handleOpenHesapAdd = (tur: "Kasa" | "Banka") => {
    resetHesapForm();
    setHesapTur(tur);
    setIsHesapFormOpen(true);
  };

  const handleOpenHesapEdit = (acc: any, tip: "Kasa" | "Banka") => {
    setHesapEditingId(acc.id);
    setHesapTur(tip);
    setHesapAdi(acc.ad || acc.isim || acc.hesapAdi || acc.bankaAdi || "");
    setHesapDoviz(acc.dovizTuru || acc.doviz || "TL");
    setHesapYetkili(acc.yetkili || "");
    setHesapAcilisBakiye(
      (acc.acilisBakiyesi ?? acc.acilisBakiye ?? 0).toString(),
    );
    setHesapSube(acc.subeAdi || acc.sube || "");
    setHesapNo(acc.hesapNo || acc.hesapNo || "");
    setHesapIban(acc.iban || "");
    setHesapKartTuru(acc.kartTuru || (tip === "Kasa" ? "Kasa" : ""));
    setIsHesapFormOpen(true);
  };

  const saveHesap = async () => {
    if (!hesapAdi.trim()) {
      Alert.alert("Hata", "Hesap adı boş olamaz.");
      return;
    }
    const acilis = parseFloat(hesapAcilisBakiye) || 0;
    const hesapId =
      hesapEditingId ||
      Math.max(
        0,
        ...[
          ...kasalar.map((k) => k.id || 0),
          ...bankalar.map((b) => b.id || 0),
        ],
      ) + 1;
    const existing = [...kasalar, ...bankalar].find((x) => x.id === hesapId);
    const dok = {
      id: hesapId,
      hesapAdi: hesapAdi.trim(),
      bankaAdi: hesapAdi.trim(),
      ad: hesapAdi.trim(),
      dovizTuru: hesapDoviz,
      yetkili: hesapYetkili.trim(),
      acilisBakiyesi: acilis,
      subeAdi: hesapSube.trim(),
      sube: hesapSube.trim(),
      hesapNo: hesapNo.trim(),
      iban: hesapIban.trim(),
      kartTuru: hesapTur === "Kasa" ? "Kasa" : "Vadesiz",
      bakiye: existing ? existing.bakiye || 0 : acilis,
      guncelBakiye: existing ? existing.bakiye || 0 : acilis,
      isDeleted: false,
    };
    const ok = await writeData(`Bankalar/${hesapId}`, dok);
    if (hesapTur === "Kasa") {
      await writeData(`Kasalar/${hesapId}`, {
        id: hesapId,
        kasaKodu: hesapNo.trim() || `KAS-${hesapId}`,
        kasaAdi: hesapAdi.trim(),
        bakiye: existing ? existing.bakiye || 0 : acilis,
        paraBirimi: hesapDoviz,
        aciklama: hesapYetkili.trim(),
        isDeleted: false,
        isActive: true,
      });
    }
    if (ok) {
      setIsHesapFormOpen(false);
      resetHesapForm();
      Alert.alert("Başarılı", `${hesapTur} hesabı kaydedildi.`);
    } else {
      Alert.alert("Hata", "Hesap kaydedilemedi.");
    }
  };

  const handleDeleteHesap = (acc: any) => {
    const accName = acc.ad || acc.hesapAdi || acc.bankaAdi || "Hesap";
    Alert.alert(
      "Hesabı Sil",
      `"${accName}" hesabını silmek istediğinize emin misiniz?`,
      [
        { text: "İptal", style: "cancel" },
        {
          text: "Evet, Sil",
          style: "destructive",
          onPress: async () => {
            try {
              const targetId = acc.id !== undefined && acc.id !== null ? acc.id : acc.firebaseKey;
              await writeData(`Bankalar/${targetId}`, { ...acc, isDeleted: true });
              await writeData(`Kasalar/${targetId}`, { ...acc, isDeleted: true });
              await deleteData(`Bankalar/${targetId}`);
              await deleteData(`Kasalar/${targetId}`);

              bankKasalarRef.current = bankKasalarRef.current.filter((x) => x.id !== acc.id && x.firebaseKey !== acc.firebaseKey);
              legacyKasalarRef.current = legacyKasalarRef.current.filter((x) => x.id !== acc.id && x.firebaseKey !== acc.firebaseKey);
              setKasalar(mergeKasalar(bankKasalarRef.current, legacyKasalarRef.current));
              setBankalar((prev) => prev.filter((b) => b.id !== acc.id && b.firebaseKey !== acc.firebaseKey));

              Alert.alert("Başarılı", `${accName} hesabı silindi.`);
            } catch (e) {
              console.error("Hesap silme hatası:", e);
              Alert.alert("Hata", "Hesap silinemedi.");
            }
          },
        },
      ],
    );
  };

  const findLinkedCariHareket = (move: any) => {
    return cariHareketler.find(
      (h) =>
        (move.refId && h.refId === move.refId) ||
        (move.evrakNo && h.evrakNo && String(h.evrakNo) === String(move.evrakNo)) ||
        (h.cariId === move.cariId &&
          h.evrakNo &&
          String(h.evrakNo) === String(move.evrakNo || move.id)),
    );
  };

  const handleDeleteMovement = (move: any) => {
    const linked = findLinkedCariHareket(move);
    const rawFaturaId = move.faturaId || move.FaturaId || linked?.faturaId || linked?.FaturaId;
    const isFatura = rawFaturaId ||
      (move.islemTuru && move.islemTuru.includes("Fatura")) ||
      (move.evrakNo && (String(move.evrakNo).startsWith("FAT") || String(move.evrakNo).startsWith("FTR") || String(move.evrakNo).startsWith("KPL-"))) ||
      (linked?.islemTuru && linked.islemTuru.includes("Fatura"));

    const confirmTitle = isFatura ? "Faturayı ve Hareketi Sil" : "Finans Hareketini Sil";
    const confirmMsg = isFatura
      ? "Bu hareket bir faturaya bağlıdır. Faturayı, stok hareketlerini ve finans hareketini tamamen silmek istediğinize emin misiniz?"
      : "Bu hareketi silmek istediğinize emin misiniz? Hesap ve cari bakiyeleri geri alınacaktır.";

    Alert.alert(
      confirmTitle,
      confirmMsg,
      [
        { text: "İptal", style: "cancel" },
        {
          text: "Evet, Sil",
          style: "destructive",
          onPress: async () => {
            try {
              if (linked) {
                const ok = await deleteFinancialTransaction(linked);
                if (ok) {
                  Alert.alert("Başarılı", isFatura ? "Fatura ve ilişkili hareketler silindi." : "Finans hareketi başarıyla silindi.");
                  return;
                }
              }

              const isKasaMove =
                move.kasaId !== undefined && move.kasaId !== null;
              const hesapId = isKasaMove ? move.kasaId : move.bankaId;
              const hesapPath = isKasaMove
                ? "KasaHareketler"
                : "BankaHareketler";
              const etki = (move.giren || 0) - (move.cikan || 0);

              if (hesapId !== undefined && hesapId !== null) {
                const hesapRef = await readData(`Bankalar/${hesapId}`);
                if (hesapRef) {
                  const ok = await writeData(`Bankalar/${hesapId}`, {
                    ...hesapRef,
                    bakiye: (hesapRef.bakiye || 0) - etki,
                  });
                  if (!ok) {
                    Alert.alert(
                      "Uyarı",
                      "Hesap bakiyesi geri alınamadı. (Bağlantı sorunu — bakiye sıraya alındı.)",
                    );
                  }
                }
              }

              if (move.refId) {
                try {
                  const kkRaw = (await readData("KrediKartlari")) || {};
                  const kkList = Array.isArray(kkRaw)
                    ? kkRaw.map((k, idx) => k ? ({ ...k, firebaseKey: String(k?.id ?? idx) }) : null).filter(Boolean)
                    : Object.keys(kkRaw).map((k) => ({
                        ...kkRaw[k],
                        firebaseKey: k,
                      }));
                  for (const k of kkList.filter(
                    (x: any) => x.onayKodu === move.refId,
                  )) {
                    const kKey = k.firebaseKey || k.id;
                    if (kKey)
                      await deleteData(`KrediKartlari/${kKey}`);
                  }
                  const eftRaw = (await readData("EftIslemleri")) || {};
                  const eftList = Array.isArray(eftRaw)
                    ? eftRaw.map((e, idx) => e ? ({ ...e, firebaseKey: String(e?.id ?? idx) }) : null).filter(Boolean)
                    : Object.keys(eftRaw).map((k) => ({
                        ...eftRaw[k],
                        firebaseKey: k,
                      }));
                  for (const e of eftList.filter(
                    (x: any) => x.dekontNo === move.refId,
                  )) {
                    const eKey = e.firebaseKey || e.id;
                    if (eKey)
                      await deleteData(`EftIslemleri/${eKey}`);
                  }
                } catch {}
              }

              const moveKey = move.firebaseKey || move.id;
              if (moveKey) {
                try {
                  await deleteData(`${hesapPath}/${moveKey}`);
                } catch {}
              }
              if (move.id && String(move.id) !== String(moveKey)) {
                try {
                  await deleteData(`${hesapPath}/${move.id}`);
                } catch {}
              }
              Alert.alert("Başarılı", "Finans hareketi silindi.");
            } catch (e) {
              console.error("Hareket silme hatası:", e);
              Alert.alert("Hata", "Finans hareketi silinemedi.");
            }
          },
        },
      ],
    );
  };

  const handleOpenEditMovement = (move: any) => {
    setMoveEdit(move);
    setTutar(Math.max(move.giren || 0, move.cikan || 0).toString());
    setTarih(move.tarih || new Date().toISOString().split("T")[0]);
    setAciklama(
      (move.aciklama || "").replace(
        / \((Nakit|Kredi Kartı|Havale\/EFT)\)$/,
        "",
      ),
    );
    setIsMoveEditOpen(true);
  };

  const saveMovementEdit = async () => {
    if (!moveEdit) return;
    const valTutar = parseFloat(tutar);
    if (isNaN(valTutar) || valTutar <= 0) {
      Alert.alert("Hata", "Lütfen geçerli bir tutar giriniz.");
      return;
    }
    try {
      const isKasaMove =
        moveEdit.kasaId !== undefined && moveEdit.kasaId !== null;
      const hesapId = isKasaMove ? moveEdit.kasaId : moveEdit.bankaId;
      const hesapPath = isKasaMove ? "KasaHareketler" : "BankaHareketler";
      const oldEtki = (moveEdit.giren || 0) - (moveEdit.cikan || 0);
      const isGiris = (moveEdit.giren || 0) > 0;
      const newEtki = isGiris ? valTutar : -valTutar;

      if (hesapId !== undefined && hesapId !== null) {
        const hesapRef = await readData(`Bankalar/${hesapId}`);
        if (hesapRef) {
          const ok = await writeData(`Bankalar/${hesapId}`, {
            ...hesapRef,
            bakiye: (hesapRef.bakiye || 0) - oldEtki + newEtki,
          });
          if (!ok) {
            Alert.alert(
              "Hata",
              "Hesap bakiyesi güncellenemedi. (Bağlantı sorunu — bakiye sıraya alındı.)",
            );
            return;
          }
        }
      }

      const updatedMove = {
        ...moveEdit,
        giren: isGiris ? valTutar : 0,
        cikan: isGiris ? 0 : valTutar,
        tutar: valTutar,
        tarih,
        aciklama:
          aciklama +
          (moveEdit.aciklama?.includes("Kredi Kartı")
            ? " (Kredi Kartı)"
            : moveEdit.aciklama?.includes("Havale") ||
                moveEdit.aciklama?.includes("EFT")
              ? " (Havale/EFT)"
              : ""),
      };
      if (moveEdit.firebaseKey) {
        const okMove = await writeData(
          `${hesapPath}/${moveEdit.firebaseKey}`,
          updatedMove,
        );
        if (!okMove) {
          Alert.alert(
            "Hata",
            "Finans hareketi güncellenemedi. (Bağlantı sorunu — kayıt sıraya alındı.)",
          );
          return;
        }
      }

      const linked = findLinkedCariHareket(moveEdit);
      if (linked) {
        const cariRef = await readData(`Cariler/${linked.cariId}`);
        if (cariRef) {
          const okCari = await writeData(`Cariler/${linked.cariId}`, {
            ...cariRef,
            borc: Math.max(
              0,
              (cariRef.borc || 0) -
                (linked.borc || 0) +
                (isGiris ? 0 : valTutar),
            ),
            alacak: Math.max(
              0,
              (cariRef.alacak || 0) -
                (linked.alacak || 0) +
                (isGiris ? valTutar : 0),
            ),
          });
          if (!okCari) {
            Alert.alert(
              "Hata",
              "Cari bakiye güncellenemedi. (Bağlantı sorunu — bakiye sıraya alındı.)",
            );
          }
        }
        if (linked.firebaseKey) {
          const okLinked = await writeData(
            `CariHareketler/${linked.firebaseKey}`,
            {
              ...linked,
              tarih,
              borc: isGiris ? 0 : valTutar,
              alacak: isGiris ? valTutar : 0,
              aciklama,
            },
          );
          if (!okLinked) {
            Alert.alert(
              "Hata",
              "Cari hareketi güncellenemedi. (Bağlantı sorunu — kayıt sıraya alındı.)",
            );
            return;
          }
        }
      }

      setIsMoveEditOpen(false);
      setMoveEdit(null);
      Alert.alert("Başarılı", "Finans hareketi güncellendi.");
    } catch (e) {
      console.error("Hareket düzenleme hatası:", e);
      Alert.alert("Hata", "Finans hareketi güncellenemedi.");
    }
  };

  const handleOpenCekEdit = (cek: any) => {
    setCekEditId(cek.id);
    setTutar(cek.tutar?.toString() || "");
    setCekTuru(cek.cekTuru || "Alınan");
    setCekBanka(cek.banka || "");
    setCekSube(cek.sube || "");
    setCekSeriNo(cek.seriNo || "");
    setCekVadeTarihi(cek.vadeTarihi || new Date().toISOString().split("T")[0]);
    setCekPortfoyNo(cek.portfoyNo || "");
    setCekGorselYoluOn(cek.gorselYoluOn || "");
    setCekGorselYoluArka(cek.gorselYoluArka || "");
    setCekDurum(cek.durum || "Portföyde");
    setIsCekEditOpen(true);
  };

  const saveCekEdit = async () => {
    if (cekEditId === null) return;
    const valTutar = parseFloat(tutar);
    if (isNaN(valTutar) || valTutar <= 0) {
      Alert.alert("Hata", "Lütfen geçerli bir tutar giriniz.");
      return;
    }
    try {
      const current = await readData(`Cekler/${cekEditId}`);
      const ok = await writeData(`Cekler/${cekEditId}`, {
        ...(current || {}),
        id: cekEditId,
        tutar: valTutar,
        vadeTarihi: cekVadeTarihi,
        banka: cekBanka,
        sube: cekSube,
        seriNo: cekSeriNo,
        portfoyNo: cekPortfoyNo,
        cekTuru,
        durum: cekDurum,
        gorselYoluOn: cekGorselYoluOn,
        gorselYoluArka: cekGorselYoluArka,
        isDeleted: false,
      });
      if (!ok) {
        Alert.alert(
          "Hata",
          "Çek güncellenemedi. (Bağlantı sorunu — kayıt eşitlenemedi.)",
        );
        return;
      }
      setIsCekEditOpen(false);
      setCekEditId(null);
      Alert.alert("Başarılı", "Çek bilgileri güncellendi.");
    } catch (e) {
      console.error("Çek düzenleme hatası:", e);
      Alert.alert("Hata", "Çek güncellenemedi.");
    }
  };

  const handleDeleteCek = (cek: any) => {
    Alert.alert(
      "Çeki Sil",
      `"${cek.portfoyNo || "Çek"}" (${cek.cariUnvan || ""}) kaydını silmek istediğinize emin misiniz?`,
      [
        { text: "İptal", style: "cancel" },
        {
          text: "Evet, Sil",
          style: "destructive",
          onPress: async () => {
            try {
              const current = await readData(`Cekler/${cek.id}`);
              const ok = await writeData(`Cekler/${cek.id}`, {
                ...(current || cek),
                isDeleted: true,
              });
              if (!ok) {
                Alert.alert(
                  "Hata",
                  "Çek silinemedi. (Bağlantı sorunu — kayıt eşitlenemedi.)",
                );
                return;
              }
              Alert.alert("Başarılı", "Çek silindi.");
            } catch (e) {
              console.error("Çek silme hatası:", e);
              Alert.alert("Hata", "Çek silinemedi.");
            }
          },
        },
      ],
    );
  };

  const handleSave = async () => {
    if (isSaving) return;
    const valTutar = parseFloat(tutar);
    if (isNaN(valTutar) || valTutar <= 0) {
      Alert.alert("Hata", "Lütfen geçerli bir tutar giriniz.");
      return;
    }

    if (islemTuru === "Virman") {
      const sourceAcc =
        kasalar.find((k) => k.id === sourceKasaId) ||
        bankalar.find((b) => b.id === sourceKasaId);
      const destAcc =
        kasalar.find((k) => k.id === destKasaId) ||
        bankalar.find((b) => b.id === destKasaId);

      if (!sourceAcc || !destAcc) {
        Alert.alert("Hata", "Kaynak veya hedef hesap bulunamadı.");
        return;
      }
      if (sourceKasaId === destKasaId) {
        Alert.alert("Hata", "Kaynak ve hedef hesap aynı olamaz.");
        return;
      }

      const moveId1 = generateInt32Id();
      const moveId2 = generateInt32Id();

      setIsSaving(true);
      try {
        const isSourceKasa = kasalar.some((k) => k.id === sourceKasaId);
        const isDestKasa = kasalar.some((k) => k.id === destKasaId);

        // Decrease from source account
        const updatedSource = {
          ...sourceAcc,
          kartTuru: isSourceKasa ? "Kasa" : sourceAcc.kartTuru,
          bakiye: (sourceAcc.bakiye || 0) - valTutar,
        };
        const okSource = await writeData(
          `Bankalar/${sourceKasaId}`,
          updatedSource,
        );

        const path1 = isSourceKasa ? "KasaHareketler" : "BankaHareketler";
        const payload1 = {
          id: moveId1,
          [isSourceKasa ? "kasaId" : "bankaId"]: sourceKasaId,
          giren: 0,
          cikan: valTutar,
          islemTuru: "Virman",
          tarih,
          aciklama: `${aciklama} (Virman Alacak - Hedef: ${destAcc.ad || destAcc.hesapAdi})`,
        };
        const okMove1 = await writeData(`${path1}/${moveId1}`, payload1);

        // Increase to dest account
        const updatedDest = {
          ...destAcc,
          kartTuru: isDestKasa ? "Kasa" : destAcc.kartTuru,
          bakiye: (destAcc.bakiye || 0) + valTutar,
        };
        const okDestWrite = await writeData(
          `Bankalar/${destKasaId}`,
          updatedDest,
        );

        const path2 = isDestKasa ? "KasaHareketler" : "BankaHareketler";
        const payload2 = {
          id: moveId2,
          [isDestKasa ? "kasaId" : "bankaId"]: destKasaId,
          giren: valTutar,
          cikan: 0,
          islemTuru: "Virman",
          tarih,
          aciklama: `${aciklama} (Virman Borç - Kaynak: ${sourceAcc.ad || sourceAcc.hesapAdi})`,
        };
        const okMove2 = await writeData(`${path2}/${moveId2}`, payload2);

        if (!okSource || !okMove1 || !okDestWrite || !okMove2) {
          Alert.alert(
            "Hata",
            "Virman işlemi kaydedilemedi. (Bağlantı sorunu — işlem sıraya alındı.)",
          );
          return;
        }

        setIsFormOpen(false);
        resetForm();
        Alert.alert("Başarılı", "Virman işlemi tamamlandı.");
      } catch (e) {
        console.error("Virman kayıt hatası:", e);
        Alert.alert("Hata", "Virman işlemi sırasında bir hata oluştu.");
      } finally {
        setIsSaving(false);
      }
      return;
    }

    if (!selectedCari) {
      Alert.alert("Hata", "Lütfen cari seçiniz.");
      return;
    }

    const isDekont =
      islemTuru === "Alacak Dekontu" || islemTuru === "Borç Dekontu";
    const currentAccId =
      odemeYontemi === "Nakit" ? selectedKasaId : selectedBankaId;

    if (!isDekont && !currentAccId) {
      Alert.alert("Hata", "Lütfen ilgili kasa/banka hesabını seçiniz.");
      return;
    }

    const isKasa = odemeYontemi === "Nakit";
    const currentAcc =
      !isDekont && currentAccId
        ? isKasa
          ? kasalar.find((k) => k.id === currentAccId)
          : bankalar.find((b) => b.id === currentAccId)
        : null;

    // --- Atomik kayıt: transactionService (masaüstü FinansService eşleniği, rollback destekli) ---
    setIsSaving(true);
    try {
      if (editingId) {
        // Düzenleme durumu: Önceki hareketin tüm etkilerini (kasa, banka, cari bakiye, detaylar) atomik olarak geri al
        const eskiHareket =
          cariHareketler.find((h) => String(h.id) === String(editingId)) ||
          (await readData(`CariHareketler/${editingId}`));
        if (eskiHareket) {
          await deleteFinancialTransaction(eskiHareket);
        }
      }

      const ok = await saveFinancialTransaction({
        id: editingId || undefined,
        cari: selectedCari,
        amount: valTutar,
        date: tarih,
        transactionType: islemTuru as any,
        method: odemeYontemi,
        description: aciklama,
        selectedHesap: currentAcc
          ? {
              id: currentAcc.id,
              kartTuru: currentAcc.kartTuru || (isKasa ? "Kasa" : undefined),
              bakiye: currentAcc.bakiye,
            }
          : null,
        bankaAdi: kkBanka,
        kartHesapNo:
          odemeYontemi === "Kredi Kartı" || odemeYontemi === "Havale/EFT"
            ? kkKartNo
            : "",
        slipDekontPath: kkSlipPath,
      });
      if (!ok) {
        Alert.alert(
          "Hata",
          "Finans hareketi kaydedilemedi. (Bağlantı sorunu — işlem geri alındı.)",
        );
        return;
      }

      // Otomatik Çek Entegrasyonu (Sadece yeni kayıtta çek üretir, düzenlemede mükerrer üretmez)
      if (odemeYontemi === "Çek" && !isDekont && !editingId) {
        const nextCekId = generateInt32Id();
        const newCek = {
          id: nextCekId,
          cariId: selectedCari.id,
          cariUnvan: selectedCari.unvan,
          tutar: valTutar,
          vadeTarihi: cekVadeTarihi,
          banka: cekBanka,
          sube: cekSube,
          seriNo: cekSeriNo,
          portfoyNo: cekPortfoyNo,
          durum: "Portföyde",
          cekTuru: cekTuru,
          gorselYoluOn: cekGorselYoluOn,
          gorselYoluArka: cekGorselYoluArka,
          isDeleted: false,
        };
        const okCek = await writeData(`Cekler/${nextCekId}`, newCek);
        if (!okCek) {
          Alert.alert(
            "Uyarı",
            "Finans hareketi kaydedildi, ancak çek kaydı eşitlenemedi. (Bağlantı sorunu — çek sıraya alındı.)",
          );
        }
      }

      setIsFormOpen(false);
      resetForm();
      Alert.alert("Başarılı", "Finans hareketi kaydedildi.");
    } catch (e) {
      console.error("Finans işlem kayıt hatası:", e);
      Alert.alert(
        "Hata",
        "Finans hareketi kaydedilemedi. (Beklenmeyen bir hata oluştu.)",
      );
    } finally {
      setIsSaving(false);
    }
  };

  const getFilteredMovements = () => {
    const list: any[] = [];
    kasaHareketler.forEach((h) => {
      const k = kasalar.find((b) => b.id === h.kasaId);
      list.push({ ...h, accountName: k?.ad || "Kasa", type: "nakit" });
    });
    bankaHareketler.forEach((h) => {
      const b = bankalar.find((x) => x.id === h.bankaId);
      const isKK = h.aciklama?.includes("Kredi Kartı");
      list.push({
        ...h,
        accountName: b?.hesapAdi || "Banka",
        type: isKK ? "kk" : "eft",
      });
    });
    return list.sort(
      (a, b) => new Date(b.tarih).getTime() - new Date(a.tarih).getTime(),
    );
  };

  const movements = getFilteredMovements();
  const tumHesaplar = [
    ...kasalar.map((k) => ({
      ...k,
      hesapTipi: "Kasa",
      isim: `[Kasa] ${k.hesapAdi || k.bankaAdi || k.ad || k.isim || 'İsimsiz Kasa'}`,
    })),
    ...bankalar.map((b) => ({
      ...b,
      hesapTipi: "Banka",
      isim: `[Banka] ${b.hesapAdi || b.bankaAdi}`,
    })),
  ];

  // Desktop Dashboard Calculations
  const calculateDashboardKarneleri = () => {
    const karneler = hesapKarneler(cariHareketler, cekler, dashboardPeriod);
    const alisKarnesi = hesapAlisFaturaKarnesi(faturalar);
    const ortalamaVadeler = hesapOrtalamaVadeler(faturalar, cariHareketler);
    const trend = hesapFinansTrend(
      kasaHareketler,
      bankaHareketler,
      trendPeriod,
    );

    return {
      // Kendi Kasamızda Kalan Net Paralar (dönem filtresi uygulanır)
      kendiNakit: karneler.kendiNakit,
      kendiKK: karneler.kendiKK,
      kendiEFT: karneler.kendiEFT,
      kendiCek: karneler.kendiCek,
      netKendiToplam:
        karneler.kendiNakit +
        karneler.kendiKK +
        karneler.kendiEFT +
        karneler.kendiCek,
      // Tedarikçiye Yönlendirilen Paralar (Ciro)
      yonlendirilenNakit: karneler.yonlendirilenNakit,
      yonlendirilenKK: karneler.yonlendirilenKK,
      yonlendirilenEFT: karneler.yonlendirilenEFT,
      yonlendirilenCek: karneler.yonlendirilenCek,
      netYonlendirilenToplam:
        karneler.yonlendirilenNakit +
        karneler.yonlendirilenKK +
        karneler.yonlendirilenEFT +
        karneler.yonlendirilenCek,
      // Alış Faturaları Kapatma Karnesi
      alisFaturaToplam: alisKarnesi.toplam,
      alisFaturaOdenen: alisKarnesi.odenen,
      alisFaturaKalan: alisKarnesi.kalan,
      alisFaturaKapatmaOrani: alisKarnesi.oran,
      alisFaturaGecikmis: alisKarnesi.gecikmis,
      alisFaturaOndenOdenen: alisKarnesi.ondenOdenen,
      // Ortalama Vadeler
      ortalamaTahsilatGunu: ortalamaVadeler.tahsilatGunu,
      ortalamaOdemeGunu: ortalamaVadeler.odemeGunu,
      // Finansal Akış Trendi
      trend,
      trendLabels: trend.map((t) => t.label),
      trendIncome: trend.map((t) => t.income),
      trendExpense: trend.map((t) => t.expense),
      trendRedirected: trend.map((t) => t.redirected),
    };
  };

  const karneler = calculateDashboardKarneleri();

  // 30 Günlük Gelecek Tahmini Listesi
  const getGelecekTahminiList = () => {
    const list: any[] = [];
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    const endLimit = new Date();
    endLimit.setDate(today.getDate() + 30);
    endLimit.setHours(23, 59, 59, 999);

    faturalar
      .filter((f) => !f.isDeleted)
      .forEach((f) => {
        const vDate = new Date(f.vadeTarihi || f.tarih);
        const kalan = f.genelToplam - (f.odenen || 0);
        if (kalan > 0 && vDate >= today && vDate <= endLimit) {
          list.push({
            type: "Fatura",
            cariAdi: f.cariUnvan,
            vadeTarihi: f.vadeTarihi || f.tarih,
            tutar: kalan,
            isIncoming: f.tur === "Satış" || f.tur === "Satis",
          });
        }
      });

    cekler
      .filter((c) => !c.isDeleted && c.durum === "Portföyde")
      .forEach((c) => {
        const vDate = new Date(c.vadeTarihi);
        if (vDate >= today && vDate <= endLimit) {
          list.push({
            type: "Cek",
            cariAdi: c.cariUnvan,
            vadeTarihi: c.vadeTarihi,
            tutar: c.tutar,
            isIncoming: c.cekTuru !== "Verilen",
          });
        }
      });

    senetler
      .filter((s) => !s.isDeleted && s.durum === "Portföyde")
      .forEach((s) => {
        const vDate = new Date(s.vadeTarihi);
        if (vDate >= today && vDate <= endLimit) {
          list.push({
            type: "Senet",
            cariAdi: s.cariUnvan,
            vadeTarihi: s.vadeTarihi,
            tutar: s.tutar,
            isIncoming: s.cekTuru !== "Verilen",
          });
        }
      });

    return list
      .map((item) => {
        const v = new Date(item.vadeTarihi);
        const gun = Math.round((v.getTime() - today.getTime()) / 86400000);
        return {
          ...item,
          kalanGunText: gun > 0 ? `${gun} Gün Kaldı` : "Vade Bugün",
          renkliUyari: gun <= 3,
        };
      })
      .sort(
        (a, b) =>
          new Date(a.vadeTarihi).getTime() - new Date(b.vadeTarihi).getTime(),
      )
      .slice(0, 7);
  };

  const gelecekTahmini = getGelecekTahminiList();

  const renderImage = (uri: string) => {
    if (
      !uri ||
      typeof uri !== "string" ||
      (!uri.startsWith("http") && !uri.startsWith("data:image"))
    ) {
      return null;
    }
    return (
      <Image
        source={{ uri }}
        style={{
          width: "100%",
          height: 100,
          borderRadius: 12,
          marginTop: 10,
          resizeMode: "contain",
        }}
      />
    );
  };

  const renderDashboard = () => {
    // SVG Finansal Akış Trendi (desktop FinansalAkış kartı)
    const trend = karneler.trend || [];
    const chartWidth = 330;
    const chartHeight = 160;
    const padL = 44;
    const padR = 10;
    const padT = 12;
    const padB = 22;
    const maxVal = Math.max(
      ...trend.map((t) => Math.max(t.income, t.expense, t.redirected)),
      1000,
    );
    const stepX =
      trend.length > 1 ? (chartWidth - padL - padR) / (trend.length - 1) : 0;

    const points = (key: "income" | "expense" | "redirected") => {
      const activeH = chartHeight - padT - padB;
      return trend.map((t, i) => {
        const x = padL + stepX * i;
        const y = chartHeight - padB - (t[key] / maxVal) * activeH;
        return { x, y };
      });
    };
    const buildPath = (pts: { x: number; y: number }[]) => {
      return pts
        .map(
          (p, i) =>
            `${i === 0 ? "M" : "L"} ${p.x.toFixed(1)} ${p.y.toFixed(1)}`,
        )
        .join(" ");
    };
    const areaPath = (pts: { x: number; y: number }[]) =>
      `${buildPath(pts)} L ${pts[pts.length - 1]?.x ?? padL} ${chartHeight - padB} L ${pts[0]?.x ?? padL} ${chartHeight - padB} Z`;

    const incomePts = points("income");
    const expensePts = points("expense");
    const redirectPts = points("redirected");
    const axisSteps = 4;

    return (
      <ScrollView contentContainerStyle={{ padding: 20 }}>
        {/* Dönem Filtresi (desktop: Haftalık/Aylık/6 Aylık/Yıllık) */}
        <Text style={styles.sectionTitle}>DÖNEM FİLTRESİ</Text>
        <View style={styles.periodRow}>
          {(
            [
              ["weekly", "Haftalık"],
              ["monthly", "Aylık"],
              ["6month", "6 Aylık"],
              ["yearly", "Yıllık"],
            ] as const
          ).map(([val, label]) => (
            <TouchableOpacity
              key={val}
              style={[
                styles.periodBtn,
                dashboardPeriod === val && styles.periodBtnActive,
              ]}
              onPress={() => setDashboardPeriod(val)}
            >
              <Text
                style={[
                  styles.periodBtnText,
                  dashboardPeriod === val && styles.periodBtnTextActive,
                ]}
              >
                {label}
              </Text>
            </TouchableOpacity>
          ))}
        </View>

        {/* Kendi Kasamızda Kalan Net Paralar */}
        <Text style={styles.sectionTitle}>
          KENDİ KASAMIZDA KALAN NET PARALAR
        </Text>
        <View style={styles.dashboardGrid}>
          <View style={styles.dashCard}>
            <Text style={styles.dashVal}>
              {formatMoney(karneler.kendiNakit)}
            </Text>
            <Text style={styles.dashLabel}>Nakit Kasa</Text>
          </View>
          <View style={styles.dashCard}>
            <Text style={styles.dashVal}>{formatMoney(karneler.kendiEFT)}</Text>
            <Text style={styles.dashLabel}>Banka EFT/Havale</Text>
          </View>
          <View style={styles.dashCard}>
            <Text style={styles.dashVal}>{formatMoney(karneler.kendiKK)}</Text>
            <Text style={styles.dashLabel}>Kredi Kartı</Text>
          </View>
          <View style={styles.dashCard}>
            <Text style={styles.dashVal}>{formatMoney(karneler.kendiCek)}</Text>
            <Text style={styles.dashLabel}>Portföy Çek/Senet</Text>
          </View>
        </View>

        {/* Tedarikçiye Yönlendirilen Paralar (Ciro) */}
        <Text style={styles.sectionTitle}>
          TEDARİKÇİYE YÖNLENDİRİLEN PARALAR (CİRO)
        </Text>
        <View style={styles.dashboardGrid}>
          <View style={styles.dashCard}>
            <Text style={styles.dashVal}>
              {formatMoney(karneler.yonlendirilenNakit)}
            </Text>
            <Text style={styles.dashLabel}>Nakit (Çıkan)</Text>
          </View>
          <View style={styles.dashCard}>
            <Text style={styles.dashVal}>
              {formatMoney(karneler.yonlendirilenEFT)}
            </Text>
            <Text style={styles.dashLabel}>Banka EFT</Text>
          </View>
          <View style={styles.dashCard}>
            <Text style={styles.dashVal}>
              {formatMoney(karneler.yonlendirilenKK)}
            </Text>
            <Text style={styles.dashLabel}>Kredi Kartı</Text>
          </View>
          <View style={styles.dashCard}>
            <Text style={styles.dashVal}>
              {formatMoney(karneler.yonlendirilenCek)}
            </Text>
            <Text style={styles.dashLabel}>Ciro Edilen Çek</Text>
          </View>
        </View>

        {/* Alış Faturaları Kapatma Karnesi */}
        <Text style={styles.sectionTitle}>ALIŞ FATURALARI KAPATMA KARNESİ</Text>
        <View style={styles.summaryBox}>
          <View style={styles.summaryRow}>
            <Text style={styles.summaryLbl}>Toplam Alış Faturaları:</Text>
            <Text style={styles.summaryVal}>
              {formatMoney(karneler.alisFaturaToplam)}
            </Text>
          </View>
          <View style={styles.summaryRow}>
            <Text style={styles.summaryLbl}>Kapatılan (Ödenen) Alışlar:</Text>
            <Text style={[styles.summaryVal, { color: "#00FF87" }]}>
              {formatMoney(karneler.alisFaturaOdenen)}
            </Text>
          </View>
          <View style={styles.summaryRow}>
            <Text style={styles.summaryLbl}>Kalan Fatura Borçları:</Text>
            <Text style={[styles.summaryVal, { color: "#FF416C" }]}>
              {formatMoney(karneler.alisFaturaKalan)}
            </Text>
          </View>
          <View style={styles.summaryRow}>
            <Text style={styles.summaryLbl}>Gecikmiş (Geriden):</Text>
            <Text style={[styles.summaryVal, { color: "#FF416C" }]}>
              {formatMoney(karneler.alisFaturaGecikmis)}
            </Text>
          </View>
          <View style={styles.summaryRow}>
            <Text style={styles.summaryLbl}>Gelecek Ödemeler (Önden):</Text>
            <Text style={[styles.summaryVal, { color: "#00FF87" }]}>
              {formatMoney(karneler.alisFaturaOndenOdenen)}
            </Text>
          </View>
          <View style={styles.progressContainer}>
            <View
              style={[
                styles.progressBar,
                { width: `${Math.min(karneler.alisFaturaKapatmaOrani, 100)}%` },
              ]}
            />
          </View>
          <Text style={styles.progressTxt}>
            Kapatma Oranı: %{karneler.alisFaturaKapatmaOrani.toFixed(1)}
          </Text>
        </View>

        {/* Ortalama Vadeler */}

        <Text style={styles.sectionTitle}>ORTALAMA VADELER</Text>
        <View style={styles.summaryBox}>
          <View style={styles.summaryRow}>
            <Text style={styles.summaryLbl}>
              Ortalama Tahsilat / Müşterilerden Para Girişi:
            </Text>
            <Text style={[styles.summaryVal, { color: "#00FF87" }]}>
              {karneler.ortalamaTahsilatGunu.toFixed(1)} Gün
            </Text>
          </View>
          <View style={styles.summaryRow}>
            <Text style={styles.summaryLbl}>
              Ortalama Ödeme / Tedarikçilere Para Çıkışı:
            </Text>
            <Text style={[styles.summaryVal, { color: "#FF416C" }]}>
              {karneler.ortalamaOdemeGunu.toFixed(1)} Gün
            </Text>
          </View>
        </View>

        {/* 30 Günlük Gelecek Tahmini */}
        <Text style={styles.sectionTitle}>30 GÜNLÜK GELECEK TAHMİNİ</Text>
        {gelecekTahmini.length === 0 ? (
          <Text style={styles.emptyTxt}>
            Gelecek 30 gün içinde vadesi gelen evrak bulunmuyor.
          </Text>
        ) : (
          gelecekTahmini.map((item, idx) => (
            <View key={idx} style={styles.tahminRow}>
              <View style={{ flex: 1 }}>
                <Text style={styles.tahminTitle}>{item.cariAdi}</Text>
                <Text style={styles.tahminSub}>
                  {item.type} • Vade: {item.vadeTarihi} •{" "}
                  <Text
                    style={{
                      color: item.renkliUyari ? "#F59E0B" : "#64748B",
                      fontWeight: "700",
                    }}
                  >
                    {item.kalanGunText}
                  </Text>
                </Text>
              </View>
              <Text
                style={[
                  styles.tahminAmt,
                  { color: item.isIncoming ? "#00FF87" : "#FF416C" },
                ]}
              >
                {item.isIncoming ? "+" : "-"}
                {formatMoney(item.tutar)}
              </Text>
            </View>
          ))
        )}

        {/* Finansal Akış Trendi (desktop: Gelir/Gider/akış özeti grafiği) */}
        <Text style={styles.sectionTitle}>FİNANSAL AKIŞ TRENDİ</Text>
        <View style={styles.summaryBox}>
          <View
            style={{
              flexDirection: "row",
              justifyContent: "space-between",
              alignItems: "center",
              marginBottom: 8,
            }}
          >
            <Text style={{ color: "#94A3B8", fontSize: 11 }}>
              Gelir, gider ve akış özeti
            </Text>
          </View>
          <View style={styles.periodRow}>
            {(["Daily", "Weekly", "Monthly", "Yearly"] as const).map((p) => (
              <TouchableOpacity
                key={p}
                style={[
                  styles.periodBtn,
                  trendPeriod === p && styles.periodBtnActive,
                ]}
                onPress={() => setTrendPeriod(p)}
              >
                <Text
                  style={[
                    styles.periodBtnText,
                    trendPeriod === p && styles.periodBtnTextActive,
                  ]}
                >
                  {p === "Daily"
                    ? "Günlük"
                    : p === "Weekly"
                      ? "Haftalık"
                      : p === "Monthly"
                        ? "Aylık"
                        : "Yıllık"}
                </Text>
              </TouchableOpacity>
            ))}
          </View>
          <View style={{ marginTop: 6 }}>
            <Svg
              height={chartHeight}
              width="100%"
              viewBox={`0 0 ${chartWidth} ${chartHeight}`}
            >
              {Array.from({ length: axisSteps + 1 }).map((_, i) => {
                const y = padT + ((chartHeight - padT - padB) / axisSteps) * i;
                const val = maxVal - (maxVal / axisSteps) * i;
                return (
                  <Line
                    key={i}
                    x1={padL}
                    y1={y}
                    x2={chartWidth - padR}
                    y2={y}
                    stroke="rgba(255,255,255,0.05)"
                    strokeWidth={1}
                    strokeDasharray="3,3"
                  />
                );
              })}
              {Array.from({ length: axisSteps + 1 }).map((_, i) => {
                const y = padT + ((chartHeight - padT - padB) / axisSteps) * i;
                const val = maxVal - (maxVal / axisSteps) * i;
                return (
                  <SvgText
                    key={`l${i}`}
                    x={padL - 8}
                    y={y + 3}
                    fill="#64748B"
                    fontSize={8}
                    fontWeight="bold"
                    textAnchor="end"
                  >
                    {val >= 1000000
                      ? `${(val / 1000000).toFixed(1)}M`
                      : val >= 1000
                        ? `${(val / 1000).toFixed(0)}K`
                        : val.toFixed(0)}
                  </SvgText>
                );
              })}
              {trend.map((t, i) => {
                const x = padL + stepX * i;
                const label =
                  String(t.label).length > 6
                    ? String(t.label).slice(0, 6)
                    : t.label;
                return (
                  <SvgText
                    key={`x${i}`}
                    x={x}
                    y={chartHeight - 6}
                    fill="#64748B"
                    fontSize={7.5}
                    fontWeight="bold"
                    textAnchor="middle"
                  >
                    {label}
                  </SvgText>
                );
              })}
              <Path d={areaPath(incomePts)} fill="rgba(0,255,163,0.08)" />
              <Path d={areaPath(expensePts)} fill="rgba(255,77,77,0.08)" />
              <Path d={areaPath(redirectPts)} fill="rgba(0,212,255,0.08)" />
              <Path
                d={buildPath(incomePts)}
                fill="none"
                stroke="#00FFA3"
                strokeWidth={2}
              />
              <Path
                d={buildPath(expensePts)}
                fill="none"
                stroke="#FF4D4D"
                strokeWidth={2}
              />
              <Path
                d={buildPath(redirectPts)}
                fill="none"
                stroke="#00D4FF"
                strokeWidth={2}
              />
            </Svg>
            <View style={styles.trendLegend}>
              <View style={styles.trendLegendItem}>
                <View
                  style={[styles.trendDot, { backgroundColor: "#00FFA3" }]}
                />
                <Text style={styles.trendLegendText}>Gelen Tahsilatlar</Text>
              </View>
              <View style={styles.trendLegendItem}>
                <View
                  style={[styles.trendDot, { backgroundColor: "#00D4FF" }]}
                />
                <Text style={styles.trendLegendText}>
                  Yönlendirilen Tahsilatlar
                </Text>
              </View>
              <View style={styles.trendLegendItem}>
                <View
                  style={[styles.trendDot, { backgroundColor: "#FF4D4D" }]}
                />
                <Text style={styles.trendLegendText}>Ödemeler</Text>
              </View>
            </View>
          </View>
        </View>
      </ScrollView>
    );
  };

  const renderKasaDetayView = (kasa: any) => {
    const allKasaMoves = kasaHareketler
      .filter(
        (h) =>
          h.kasaId === kasa.id ||
          h.kasaId === kasa.firebaseKey ||
          String(h.kasaId) === String(kasa.id),
      )
      .sort(
        (a, b) => new Date(b.tarih).getTime() - new Date(a.tarih).getTime(),
      );

    const guncelBakiye = kasa.bakiye ?? kasa.guncelBakiye ?? 0;
    const doviz = kasa.dovizTuru || "TL";

    return (
      <View
        style={{ flex: 1, paddingHorizontal: 16, paddingTop: 12 }}
        onTouchStart={(e) => {
          touchStartX.current = e.nativeEvent.pageX;
          touchStartY.current = e.nativeEvent.pageY;
        }}
        onTouchEnd={(e) => {
          const dx = e.nativeEvent.pageX - touchStartX.current;
          const dy = e.nativeEvent.pageY - touchStartY.current;
          if (dx > 35 && Math.abs(dx) > Math.abs(dy) * 1.2) {
            handleCloseKasaDetay();
          }
        }}
        {...kasaDetayPanResponder.panHandlers}
      >
        {/* Desktop Header Parity (Image 1 Birebir) */}
        <View
          style={{
            flexDirection: "row",
            justifyContent: "space-between",
            alignItems: "center",
            marginBottom: 20,
          }}
        >
          {/* Left Block: Back Arrow, Blue Clock Icon, Title & Subtitle */}
          <View style={{ flexDirection: "row", alignItems: "center", flex: 1 }}>
            <TouchableOpacity
              style={{
                width: 40,
                height: 40,
                borderRadius: 20,
                backgroundColor: "rgba(255,255,255,0.08)",
                alignItems: "center",
                justifyContent: "center",
                marginRight: 12,
              }}
              onPress={handleCloseKasaDetay}
            >
              <ArrowLeft color="#FFF" size={20} />
            </TouchableOpacity>

            <View
              style={{
                width: 44,
                height: 44,
                borderRadius: 12,
                backgroundColor: "#0061FF",
                alignItems: "center",
                justifyContent: "center",
                marginRight: 12,
              }}
            >
              <Clock color="#FFF" size={22} />
            </View>

            <View style={{ flex: 1 }}>
              <Text
                style={{
                  color: "#FFFFFF",
                  fontSize: 20,
                  fontWeight: "800",
                }}
                numberOfLines={1}
              >
                {kasa.hesapAdi || kasa.bankaAdi || kasa.ad || kasa.isim || "Kasa"}
              </Text>
              <Text
                style={{
                  color: "rgba(255,255,255,0.5)",
                  fontSize: 12,
                  marginTop: 2,
                }}
              >
                Kasa Hareketleri ve Detaylı Ekstre
              </Text>
            </View>
          </View>

          {/* Right Block: Güncel Bakiye (Image 1 Birebir) */}
          <View style={{ alignItems: "flex-end", marginLeft: 12 }}>
            <Text
              style={{
                color: "rgba(255,255,255,0.5)",
                fontSize: 11,
                fontWeight: "600",
                marginBottom: 2,
              }}
            >
              Güncel Bakiye
            </Text>
            <View style={{ flexDirection: "row", alignItems: "baseline" }}>
              <Text
                style={{
                  color: "#00FF87",
                  fontSize: 22,
                  fontWeight: "900",
                  letterSpacing: -0.5,
                }}
              >
                {formatMoney(guncelBakiye).replace("₺", "").trim()}
              </Text>
              <Text
                style={{
                  color: "#00FF87",
                  fontSize: 13,
                  fontWeight: "700",
                  marginLeft: 4,
                }}
              >
                {doviz}
              </Text>
            </View>
          </View>
        </View>

        {/* Desktop DataGrid Table Container (Image 1 Birebir) */}
        <View
          style={{
            flex: 1,
            backgroundColor: "#161616",
            borderRadius: 16,
            borderWidth: 1,
            borderColor: "rgba(255,255,255,0.08)",
            overflow: "hidden",
            marginBottom: 20,
          }}
        >
          {/* Table Header Bar (Image 1: Tarih | İşlem Türü | Cari / Açıklama | Giren | Çıkan | İşlemler) */}
          <View
            style={{
              flexDirection: "row",
              backgroundColor: "#1A1A1A",
              paddingVertical: 12,
              paddingHorizontal: 14,
              borderBottomWidth: 1,
              borderBottomColor: "rgba(255,255,255,0.08)",
              alignItems: "center",
            }}
          >
            <Text
              style={{
                width: 90,
                color: "#FFFFFF",
                fontSize: 11,
                fontWeight: "800",
              }}
            >
              Tarih
            </Text>
            <Text
              style={{
                width: 85,
                color: "#FFFFFF",
                fontSize: 11,
                fontWeight: "800",
              }}
            >
              İşlem Türü
            </Text>
            <Text
              style={{
                flex: 1,
                color: "#FFFFFF",
                fontSize: 11,
                fontWeight: "800",
              }}
            >
              Cari / Açıklama
            </Text>
            <Text
              style={{
                width: 70,
                color: "#FFFFFF",
                fontSize: 11,
                fontWeight: "800",
                textAlign: "right",
              }}
            >
              Giren
            </Text>
            <Text
              style={{
                width: 70,
                color: "#FFFFFF",
                fontSize: 11,
                fontWeight: "800",
                textAlign: "right",
              }}
            >
              Çıkan
            </Text>
            <Text
              style={{
                width: 55,
                color: "#FFFFFF",
                fontSize: 11,
                fontWeight: "800",
                textAlign: "center",
              }}
            >
              İşlemler
            </Text>
          </View>

          {/* Table Rows */}
          <FlashList
            data={allKasaMoves}
            keyExtractor={(item, idx) =>
              item.id?.toString() || item.firebaseKey || idx.toString()
            }
            ListEmptyComponent={
              <View
                style={{
                  padding: 40,
                  alignItems: "center",
                  justifyContent: "center",
                }}
              >
                <Text style={{ color: "rgba(255,255,255,0.4)", fontSize: 13 }}>
                  Bu kasada henüz bir işlem hareketi yok.
                </Text>
              </View>
            }
            renderItem={({ item, index }) => {
              const isGiren = (item.giren || 0) > 0;
              const isCikan = (item.cikan || 0) > 0;
              return (
                <FadeInView delay={index * 50}>
                  <View
                    style={{
                      flexDirection: "row",
                      paddingVertical: 12,
                      paddingHorizontal: 14,
                      borderBottomWidth: 1,
                      borderBottomColor: "rgba(255,255,255,0.05)",
                      alignItems: "center",
                    }}
                  >
                    {/* Tarih */}
                    <Text
                      style={{
                        width: 90,
                        color: "#94A3B8",
                        fontSize: 11,
                        fontWeight: "500",
                      }}
                      numberOfLines={1}
                    >
                      {item.tarih}
                    </Text>

                    {/* İşlem Türü Pill Badge */}
                    <View style={{ width: 85 }}>
                      <View
                        style={{
                          backgroundColor: isGiren
                            ? "rgba(0,255,135,0.15)"
                            : item.islemTuru === "Virman"
                              ? "rgba(0,97,255,0.15)"
                              : "rgba(239,68,68,0.15)",
                          paddingHorizontal: 8,
                          paddingVertical: 3,
                          borderRadius: 999,
                          alignSelf: "flex-start",
                        }}
                      >
                        <Text
                          style={{
                            color: isGiren
                              ? "#00FF87"
                              : item.islemTuru === "Virman"
                                ? "#0061FF"
                                : "#EF4444",
                            fontSize: 10,
                            fontWeight: "800",
                          }}
                        >
                          {item.islemTuru || (isGiren ? "Tahsilat" : "Ödeme")}
                        </Text>
                      </View>
                    </View>

                    {/* Cari / Açıklama */}
                    <View style={{ flex: 1, paddingRight: 6 }}>
                      <Text
                        style={{
                          color: "#FFFFFF",
                          fontSize: 12,
                          fontWeight: "600",
                        }}
                        numberOfLines={1}
                      >
                        {item.cariUnvan || item.aciklama || "Finans Hareketi"}
                      </Text>
                      {item.aciklama && item.cariUnvan ? (
                        <Text
                          style={{
                            color: "#64748B",
                            fontSize: 10,
                            marginTop: 1,
                          }}
                          numberOfLines={1}
                        >
                          {item.aciklama}
                        </Text>
                      ) : null}
                    </View>

                    {/* Giren */}
                    <Text
                      style={{
                        width: 70,
                        color: "#00FF87",
                        fontSize: 12,
                        fontWeight: "700",
                        textAlign: "right",
                      }}
                    >
                      {isGiren ? formatMoney(item.giren) : "-"}
                    </Text>

                    {/* Çıkan */}
                    <Text
                      style={{
                        width: 70,
                        color: "#EF4444",
                        fontSize: 12,
                        fontWeight: "700",
                        textAlign: "right",
                      }}
                    >
                      {isCikan ? formatMoney(item.cikan) : "-"}
                    </Text>

                    {/* İşlemler */}
                    <View
                      style={{
                        width: 55,
                        flexDirection: "row",
                        justifyContent: "center",
                        gap: 4,
                      }}
                    >
                      <TouchableOpacity
                        onPress={() => handleOpenEditMovement(item)}
                        style={{ padding: 2 }}
                      >
                        <Edit3 color="#94A3B8" size={14} />
                      </TouchableOpacity>
                      <TouchableOpacity
                        onPress={() => handleDeleteMovement(item)}
                        style={{ padding: 2 }}
                      >
                        <Trash2 color="#EF4444" size={14} />
                      </TouchableOpacity>
                    </View>
                  </View>
                </FadeInView>
              );
            }}
          />
        </View>
      </View>
    );
  };

  const renderNakitList = () => {
    if (selectedKasaForDetay) {
      return renderKasaDetayView(selectedKasaForDetay);
    }
    const list = movements.filter((m) => m.type === "nakit");
    return (
      <FlashList
        data={list}
        keyExtractor={(item, idx) =>
          item.id?.toString() || item.firebaseKey || idx.toString()
        }
        contentContainerStyle={{ padding: 20 }}
        ListHeaderComponent={
          <>
            <View style={{ marginBottom: 24 }}>
              <View
                style={{
                  flexDirection: "row",
                  justifyContent: "space-between",
                  alignItems: "center",
                  marginBottom: 12,
                }}
              >
                <View style={{ flexDirection: "row", alignItems: "center" }}>
                  <Text style={styles.sectionTitle}>KASALARIM</Text>
                  <View
                    style={{
                      backgroundColor: "rgba(0,255,135,0.15)",
                      paddingHorizontal: 8,
                      paddingVertical: 2,
                      borderRadius: 12,
                      marginLeft: 8,
                    }}
                  >
                    <Text
                      style={{ color: "#00FF87", fontSize: 11, fontWeight: "800" }}
                    >
                      {kasalar.length}
                    </Text>
                  </View>
                </View>

                <TouchableOpacity
                  style={{
                    flexDirection: "row",
                    alignItems: "center",
                    backgroundColor: "rgba(0,255,135,0.1)",
                    paddingHorizontal: 12,
                    paddingVertical: 6,
                    borderRadius: 10,
                    borderWidth: 1,
                    borderColor: "rgba(0,255,135,0.25)",
                  }}
                  onPress={() => handleOpenHesapAdd("Kasa")}
                >
                  <Plus color="#00FF87" size={14} style={{ marginRight: 4 }} />
                  <Text
                    style={{
                      color: "#00FF87",
                      fontSize: 12,
                      fontWeight: "700",
                    }}
                  >
                    Yeni Kasa
                  </Text>
                </TouchableOpacity>
              </View>

              {kasalar.length === 0 ? (
                <View
                  style={{
                    backgroundColor: "#161616",
                    borderRadius: 16,
                    borderWidth: 1,
                    borderColor: "rgba(255,255,255,0.08)",
                    padding: 20,
                    alignItems: "center",
                    justifyContent: "center",
                  }}
                >
                  <Wallet color="#64748B" size={32} style={{ marginBottom: 8 }} />
                  <Text
                    style={{ color: "#94A3B8", fontSize: 13, fontWeight: "600" }}
                  >
                    Henüz tanımlanmış bir kasa bulunmuyor.
                  </Text>
                  <TouchableOpacity
                    style={{
                      marginTop: 10,
                      backgroundColor: "#0061FF",
                      paddingHorizontal: 14,
                      paddingVertical: 8,
                      borderRadius: 8,
                    }}
                    onPress={() => handleOpenHesapAdd("Kasa")}
                  >
                    <Text
                      style={{ color: "#FFF", fontSize: 12, fontWeight: "700" }}
                    >
                      + İlk Kasayı Ekle
                    </Text>
                  </TouchableOpacity>
                </View>
              ) : (
                <ScrollView
                  horizontal
                  showsHorizontalScrollIndicator={false}
                  contentContainerStyle={{ paddingRight: 10 }}
                >
                  {kasalar.map((k) => (
                    <View
                      key={k.id || k.firebaseKey}
                      style={{
                        width: 180,
                        marginRight: 12,
                        backgroundColor: "#1E293B",
                        borderRadius: 16,
                        borderWidth: 1,
                        borderColor: "rgba(255,255,255,0.08)",
                        padding: 14,
                        shadowColor: "#000",
                        shadowOffset: { width: 0, height: 4 },
                        shadowOpacity: 0.3,
                        shadowRadius: 6,
                        elevation: 4,
                      }}
                    >
                      <View
                        style={{
                          flexDirection: "row",
                          justifyContent: "space-between",
                          alignItems: "center",
                          marginBottom: 12,
                        }}
                      >
                        <View
                          style={{
                            width: 34,
                            height: 34,
                            borderRadius: 10,
                            backgroundColor: "rgba(0,255,135,0.12)",
                            alignItems: "center",
                            justifyContent: "center",
                          }}
                        >
                          <Wallet color="#00FF87" size={18} />
                        </View>
                        <View style={{ flexDirection: "row", alignItems: "center" }}>
                          <TouchableOpacity
                            style={{
                              width: 30,
                              height: 30,
                              borderRadius: 8,
                              backgroundColor: "rgba(255,255,255,0.06)",
                              alignItems: "center",
                              justifyContent: "center",
                              marginRight: 6,
                            }}
                            onPress={() => handleOpenHesapEdit(k, "Kasa")}
                          >
                            <Edit3 color="#94A3B8" size={14} />
                          </TouchableOpacity>
                          <TouchableOpacity
                            style={{
                              width: 30,
                              height: 30,
                              borderRadius: 8,
                              backgroundColor: "rgba(239,68,68,0.12)",
                              alignItems: "center",
                              justifyContent: "center",
                            }}
                            onPress={() => handleDeleteHesap(k)}
                          >
                            <Trash2 color="#EF4444" size={14} />
                          </TouchableOpacity>
                        </View>
                      </View>

                      <TouchableOpacity
                        activeOpacity={0.7}
                        onPress={() => handleOpenKasaDetay(k)}
                      >
                        <Text
                          style={{
                            color: "#F8FAFC",
                            fontSize: 14,
                            fontWeight: "700",
                            marginBottom: 4,
                          }}
                          numberOfLines={1}
                        >
                          {k.hesapAdi || k.bankaAdi || k.ad || k.isim || "İsimsiz Kasa"}
                        </Text>
                        <Text
                          style={{
                            color: "#00FF87",
                            fontSize: 19,
                            fontWeight: "800",
                            letterSpacing: -0.5,
                          }}
                        >
                          {formatMoney(k.bakiye ?? k.guncelBakiye ?? 0)}
                        </Text>
                        <View
                          style={{
                            flexDirection: "row",
                            alignItems: "center",
                            justifyContent: "space-between",
                            marginTop: 10,
                          }}
                        >
                          <View
                            style={{
                              backgroundColor: "rgba(255,255,255,0.06)",
                              paddingHorizontal: 8,
                              paddingVertical: 3,
                              borderRadius: 6,
                            }}
                          >
                            <Text
                              style={{
                                color: "#94A3B8",
                                fontSize: 10,
                                fontWeight: "700",
                              }}
                            >
                              {k.dovizTuru || "TL"}
                            </Text>
                          </View>
                          {k.yetkili ? (
                            <Text
                              style={{
                                color: "#64748B",
                                fontSize: 10,
                                fontWeight: "500",
                              }}
                              numberOfLines={1}
                            >
                              {k.yetkili}
                            </Text>
                          ) : null}
                        </View>
                      </TouchableOpacity>
                    </View>
                  ))}

                  <TouchableOpacity
                    style={{
                      width: 140,
                      marginRight: 12,
                      backgroundColor: "rgba(30,41,59,0.4)",
                      borderRadius: 16,
                      borderWidth: 1.5,
                      borderColor: "rgba(0,255,135,0.25)",
                      borderStyle: "dashed",
                      padding: 14,
                      alignItems: "center",
                      justifyContent: "center",
                    }}
                    onPress={() => handleOpenHesapAdd("Kasa")}
                  >
                    <View
                      style={{
                        width: 38,
                        height: 38,
                        borderRadius: 19,
                        backgroundColor: "rgba(0,255,135,0.12)",
                        alignItems: "center",
                        justifyContent: "center",
                        marginBottom: 8,
                      }}
                    >
                      <Plus color="#00FF87" size={20} />
                    </View>
                    <Text
                      style={{
                        color: "#00FF87",
                        fontSize: 12,
                        fontWeight: "700",
                        textAlign: "center",
                      }}
                    >
                      + Kasa Ekle
                    </Text>
                  </TouchableOpacity>
                </ScrollView>
              )}
            </View>
            <Text style={styles.sectionTitle}>NAKİT HAREKETLERİ</Text>
          </>
        }
        renderItem={({ item }) => (
          <View style={styles.moveRow}>
            <View style={{ flex: 1 }}>
              <Text style={styles.moveTitle}>
                {item.cariUnvan || item.aciklama}
              </Text>
              <Text style={styles.moveSub}>
                {item.accountName} • {item.tarih}
              </Text>
            </View>
            <Text
              style={[
                styles.moveAmt,
                { color: item.giren > 0 ? "#00FF87" : "#EF4444" },
              ]}
            >
              {item.giren > 0
                ? `+${formatMoney(item.giren)}`
                : `-${formatMoney(item.cikan)}`}
            </Text>
            <View style={{ marginLeft: 10 }}>
              <TouchableOpacity
                onPress={() => handleOpenEditMovement(item)}
                style={{ padding: 4 }}
              >
                <Edit3 color="#94A3B8" size={15} />
              </TouchableOpacity>
              <TouchableOpacity
                onPress={() => handleDeleteMovement(item)}
                style={{ padding: 4 }}
              >
                <Trash2 color="#EF4444" size={15} />
              </TouchableOpacity>
            </View>
          </View>
        )}
      />
    );
  };

  const setCekDurumTh = (cek: any, durum: string) => {
    Alert.alert(
      "Çek Durumu",
      `"${cek.portfoyNo || "Çek"}" durumu "${durum}" olarak güncellensin mi?`,
      [
        { text: "İptal", style: "cancel" },
        {
          text: "Evet",
          onPress: async () => {
            try {
              const current = await readData(`Cekler/${cek.id}`);
              const ok = await writeData(`Cekler/${cek.id}`, {
                ...(current || cek),
                durum,
                isDeleted: false,
              });
              if (!ok) {
                Alert.alert(
                  "Hata",
                  "Çek durumu güncellenemedi. (Bağlantı sorunu — kayıt eşitlenemedi.)",
                );
                return;
              }
              Alert.alert("Başarılı", `Çek "${durum}" olarak güncellendi.`);
            } catch (e) {
              console.error("Çek durum güncelleme hatası:", e);
              Alert.alert("Hata", "Çek durumu güncellenemedi.");
            }
          },
        },
      ],
    );
  };

  const openCiroCariPicker = (cek: any) => {
    setCiroCek(cek);
    setIsCiroPickerOpen(true);
  };

  const saveCekCiro = async (sup: any) => {
    if (!ciroCek) return;
    try {
      const current = await readData(`Cekler/${ciroCek.id}`);
      const ok = await writeData(`Cekler/${ciroCek.id}`, {
        ...(current || ciroCek),
        durum: "Ciro Edildi",
        yonlendirilenCariId: sup.id,
        yonlendirilenCariUnvan: sup.unvan,
        isDeleted: false,
      });
      if (!ok) {
        Alert.alert(
          "Hata",
          "Çek ciro edilemedi. (Bağlantı sorunu — kayıt eşitlenemedi.)",
        );
        return;
      }
      setIsCiroPickerOpen(false);
      setCiroCek(null);
      Alert.alert("Başarılı", `Çek "${sup.unvan}" firmasına ciro edildi.`);
    } catch (e) {
      console.error("Çek ciro hatası:", e);
      Alert.alert("Hata", "Çek ciro edilemedi.");
    }
  };

  const getCekRisk = (cek: any) => {
    let puan = 0;
    const banka = (cek.banka || "").toLocaleLowerCase('tr-TR');
    const iyiBankalar = [
      "ziraat",
      "iş",
      "isbank",
      "garanti",
      "akbank",
      "yapı",
      "yapi",
      "qnb",
      "finans",
      "halk",
      "vakıf",
      "vakif",
      "denizbank",
      "teb",
    ];
    if (iyiBankalar.some((b) => banka.includes(b))) puan += 10;
    else if (banka) puan += 35;
    else puan += 40;
    if (cek.tutar > 500000) puan += 15;
    else if (cek.tutar > 100000) puan += 8;
    const bugun = new Date().toISOString().split("T")[0];
    if (cek.vadeTarihi && cek.vadeTarihi < bugun) puan += 35;
    if (
      (cek.durum || "Portföyde") === "Ciro Edildi" ||
      (cek.durum || "") === "Tahsil Edildi"
    )
      puan = Math.min(puan, 30);
    const label =
      puan >= 70 ? "Yüksek Risk" : puan >= 40 ? "Orta Risk" : "Düşük Risk";
    return { puan, label };
  };

  // Desktop CekDurumToTextConverter / CekDurumToColorConverter eşleniği
  const getCekDurumLabel = (durum?: string): string => {
    switch (durum) {
      case "Tahsil":
      case "Tahsil Edildi":
      case "Tamamlandı":
        return "Tahsil Edildi";
      case "Karsiliksiz":
      case "Karşılıksız":
      case "Karşılıksız Çıktı":
        return "Karşılıksız";
      case "Portfoyde":
      case "Portföyde":
      case "Bankaya Geçti":
        return "Portföyde";
      case "Tedarikçiye Verildi":
      case "Tedarikçiye Yönlendirildi":
        return "Ciro Edildi";
      case "İptal":
      case "Iptal":
        return "İptal";
      default:
        return durum || "Portföyde";
    }
  };

  const getCekDurumColor = (durum?: string): string => {
    switch (durum) {
      case "Tahsil":
      case "Tahsil Edildi":
      case "Tamamlandı":
        return "#10B981";
      case "Karsiliksiz":
      case "Karşılıksız":
      case "Karşılıksız Çıktı":
        return "#EF4444";
      case "Portfoyde":
      case "Portföyde":
      case "Bankaya Geçti":
        return "#2F6FED";
      case "Tedarikçiye Verildi":
      case "Ciro Edildi":
      case "Tedarikçiye Yönlendirildi":
        return "#F59E0B";
      case "İptal":
      case "Iptal":
        return "#6B7280";
      default:
        return "#4B5563";
    }
  };

  const renderCekList = () => {
    const riskler = cekler
      .filter((c) => !c.isDeleted)
      .map((c) => ({ cek: c, risk: getCekRisk(c) }));
    const yuksek = riskler.filter((r) => r.risk.puan >= 70).length;
    const toplam = riskler.length;

    const header = (
      <View
        style={{
          backgroundColor: "rgba(245,158,11,0.08)",
          borderRadius: 14,
          borderWidth: 1,
          borderColor: "rgba(245,158,11,0.25)",
          padding: 14,
          marginBottom: 14,
        }}
      >
        <Text
          style={{
            color: "#F59E0B",
            fontWeight: "900",
            fontSize: 15,
            marginBottom: 6,
          }}
        >
          ÇEK RİSK ANALİZİ
        </Text>
        <Text style={{ color: "#E2E8F0", fontSize: 13 }}>
          Toplam {toplam} çek • {yuksek} yüksek riskli • Portföy toplamı{" "}
          {formatMoney(
            cekler
              .filter((c) => !c.isDeleted && c.durum === "Portföyde")
              .reduce((s, c) => s + (c.tutar || 0), 0),
          )}
        </Text>
      </View>
    );

    return (
      <FlashList
        data={riskler.filter((r) => {
          const s = cekSearch.toLocaleLowerCase('tr-TR');
          if (!s) return true;
          return [
            r.cek.portfoyNo,
            r.cek.asilBorclu,
            r.cek.seriNo,
            r.cek.banka,
          ].some((v) => (v || "").toLocaleLowerCase('tr-TR').includes(s));
        })}
        keyExtractor={(item, idx) => item.cek.id?.toString() || idx.toString()}
        contentContainerStyle={{ padding: 20 }}
        ListHeaderComponent={
          <>
            <View style={styles.searchBox}>
              <Search color="#64748B" size={18} />
              <TextInput
                style={styles.searchInput}
                placeholder="Portföy No / Asıl Borçlu / Banka ara..."
                placeholderTextColor="#64748B"
                value={cekSearch}
                onChangeText={setCekSearch}
              />
            </View>
            {header}
          </>
        }
        ListFooterComponent={
          <View style={styles.cekFooter}>
            <Text style={{ color: "#94A3B8", fontSize: 12, fontWeight: "700" }}>
              TOPLAM ÇEK TUTARI:
            </Text>
            <Text style={{ color: "#00FF87", fontSize: 14, fontWeight: "900" }}>
              {formatMoney(riskler.reduce((s, r) => s + (r.cek.tutar || 0), 0))}
            </Text>
          </View>
        }
        renderItem={({ item }) => (
          <View style={styles.cekCard}>
            <View
              style={{
                flexDirection: "row",
                justifyContent: "space-between",
                marginBottom: 8,
              }}
            >
              <Text style={styles.cekNo}>{item.cek.portfoyNo || "Çek"}</Text>
              <View
                style={{ flexDirection: "row", alignItems: "center", gap: 6 }}
              >
                <View
                  style={[
                    styles.cekDurumBadge,
                    {
                      backgroundColor: `${getCekDurumColor(item.cek.durum)}22`,
                      borderColor: getCekDurumColor(item.cek.durum),
                    },
                  ]}
                >
                  <Text
                    style={[
                      styles.cekDurumText,
                      { color: getCekDurumColor(item.cek.durum) },
                    ]}
                  >
                    {getCekDurumLabel(item.cek.durum)}
                  </Text>
                </View>
                <View
                  style={{
                    backgroundColor:
                      item.risk.puan >= 70
                        ? "rgba(239,68,68,0.15)"
                        : item.risk.puan >= 40
                          ? "rgba(245,158,11,0.15)"
                          : "rgba(0,255,135,0.12)",
                    paddingHorizontal: 8,
                    paddingVertical: 2,
                    borderRadius: 6,
                  }}
                >
                  <Text
                    style={{
                      color:
                        item.risk.puan >= 70
                          ? "#EF4444"
                          : item.risk.puan >= 40
                            ? "#F59E0B"
                            : "#00FF87",
                      fontSize: 10,
                      fontWeight: "900",
                    }}
                  >
                    {item.risk.label} ({item.risk.puan})
                  </Text>
                </View>
              </View>
            </View>
            <Text style={styles.cekCari}>{item.cek.cariUnvan}</Text>
            <Text style={styles.cekDetail}>
              Vade: {item.cek.vadeTarihi} • {item.cek.banka} / {item.cek.sube}
            </Text>
            <Text style={styles.cekAmt}>{formatMoney(item.cek.tutar)}</Text>

            <View
              style={{
                flexDirection: "row",
                justifyContent: "flex-end",
                gap: 12,
                marginTop: 6,
              }}
            >
              <TouchableOpacity
                onPress={() => handleOpenCekEdit(item.cek)}
                style={{ flexDirection: "row", alignItems: "center", gap: 4 }}
              >
                <Edit3 color="#94A3B8" size={14} />
                <Text style={{ color: "#94A3B8", fontSize: 11 }}>Düzenle</Text>
              </TouchableOpacity>
              <TouchableOpacity
                onPress={() => handleDeleteCek(item.cek)}
                style={{ flexDirection: "row", alignItems: "center", gap: 4 }}
              >
                <Trash2 color="#EF4444" size={14} />
                <Text style={{ color: "#EF4444", fontSize: 11 }}>Sil</Text>
              </TouchableOpacity>
            </View>
            {renderImage(item.cek.gorselYoluOn)}
          </View>
        )}
      />
    );
  };

  const resetKkEftForm = () => {
    setKkEftEditingId(null);
    setKkEftTutar("");
    setKkEftTarih(new Date().toISOString().split("T")[0]);
    setKkEftMusteri(null);
    setKkEftBanka("");
    setKkEftKartNo("");
    setKkEftHesapNo("");
    setKkEftDurum("Portföyde");
    setKkEftOnayDekontNo("");
    setKkEftSlipPath("");
    setKkEftAciklama("");
    setKkEftYonlendirilen(null);
  };

  const openKkNew = () => {
    resetKkEftForm();
    setKkEftMode("kk");
    setKkEftDurum("Portföyde");
    setIsKkEftFormOpen(true);
  };

  const openKkEdit = (item: any) => {
    resetKkEftForm();
    setKkEftMode("kk");
    setKkEftEditingId(item.id ?? item.firebaseKey ?? null);
    setKkEftTutar((item.tutar || 0).toString());
    setKkEftTarih(item.tarih || new Date().toISOString().split("T")[0]);
    setKkEftMusteri(
      item.musteriId != null
        ? { id: item.musteriId, unvan: item.musteriUnvan || item.musteriAdi }
        : null,
    );
    setKkEftBanka(item.banka || "");
    setKkEftKartNo(item.kartNo || "");
    setKkEftDurum(item.durum || "Portföyde");
    setKkEftOnayDekontNo(item.onayKodu || "");
    setKkEftSlipPath(item.slipDosyaYolu || item.slipPath || "");
    setKkEftAciklama(item.aciklama || "");
    setKkEftYonlendirilen(
      item.yonlendirilenCariId != null
        ? { id: item.yonlendirilenCariId, unvan: item.yonlendirilenCariUnvan }
        : null,
    );
    setIsKkEftFormOpen(true);
  };

  const openEftNew = () => {
    resetKkEftForm();
    setKkEftMode("eft");
    setKkEftDurum("Portföyde");
    setIsKkEftFormOpen(true);
  };

  const openEftEdit = (item: any) => {
    resetKkEftForm();
    setKkEftMode("eft");
    setKkEftEditingId(item.id ?? item.firebaseKey ?? null);
    setKkEftTutar((item.tutar || 0).toString());
    setKkEftTarih(item.tarih || new Date().toISOString().split("T")[0]);
    setKkEftMusteri(
      item.musteriId != null
        ? { id: item.musteriId, unvan: item.musteriUnvan || item.musteriAdi }
        : null,
    );
    setKkEftBanka(item.banka || "");
    setKkEftHesapNo(item.hesapNo || item.hesapNoIban || item.iban || "");
    setKkEftDurum(item.durum || "Portföyde");
    setKkEftOnayDekontNo(item.dekontNo || "");
    setKkEftSlipPath(item.dekontPath || "");
    setKkEftAciklama(item.aciklama || "");
    setKkEftYonlendirilen(
      item.yonlendirilenCariId != null
        ? { id: item.yonlendirilenCariId, unvan: item.yonlendirilenCariUnvan }
        : null,
    );
    setIsKkEftFormOpen(true);
  };

  const saveKkEft = async () => {
    const valTutar = parseFloat(kkEftTutar);
    if (isNaN(valTutar) || valTutar <= 0) {
      Alert.alert("Hata", "Lütfen geçerli bir tutar giriniz.");
      return;
    }
    if (!kkEftMusteri) {
      Alert.alert("Hata", "Lütfen müşteri seçiniz.");
      return;
    }
    try {
      const path = kkEftMode === "kk" ? "KrediKartlari" : "EftIslemleri";
      const nextId =
        kkEftEditingId != null ? kkEftEditingId : generateInt32Id();
      const current =
        kkEftEditingId != null
          ? await readData(`${path}/${kkEftEditingId}`)
          : null;
      const isKk = kkEftMode === "kk";
      const durum = kkEftYonlendirilen
        ? isKk
          ? "Tedarikçiye Verildi"
          : "Tedarikçiye Yönlendirildi"
        : kkEftDurum;
      const payload = {
        id: nextId,
        musteriId: kkEftMusteri.id,
        musteriUnvan: kkEftMusteri.unvan,
        tarih: kkEftTarih,
        tutar: valTutar,
        banka: kkEftBanka,
        ...(isKk
          ? {
              kartNo: kkEftKartNo,
              onayKodu:
                kkEftOnayDekontNo || `KK-${Date.now().toString().slice(-8)}`,
              slipDosyaYolu: kkEftSlipPath,
            }
          : {
              hesapNo: kkEftHesapNo,
              bankaId: current?.bankaId,
              dekontNo:
                kkEftOnayDekontNo || `EFT-${Date.now().toString().slice(-8)}`,
              dekontPath: kkEftSlipPath,
            }),
        durum,
        islemTuru: isKk ? "Tahsilat (KK)" : "Tahsilat (EFT)",
        aciklama: kkEftAciklama,
        yonlendirilenCariId: kkEftYonlendirilen?.id,
        yonlendirilenCariUnvan: kkEftYonlendirilen?.unvan,
        yonlendirmeTarihi: kkEftYonlendirilen
          ? new Date().toISOString().split("T")[0]
          : undefined,
        isDeleted: false,
      };
      const ok = await writeData(`${path}/${nextId}`, payload);
      if (!ok) {
        Alert.alert(
          "Hata",
          isKk
            ? "KK işlemi kaydedilemedi. (Bağlantı sorunu — işlem sıraya alındı.)"
            : "EFT işlemi kaydedilemedi. (Bağlantı sorunu — işlem sıraya alındı.)",
        );
        return;
      }
      setIsKkEftFormOpen(false);
      resetKkEftForm();
      Alert.alert(
        "Başarılı",
        isKk
          ? "Kredi kartı işlemi kaydedildi."
          : "Havale/EFT işlemi kaydedildi.",
      );
    } catch (e) {
      console.error("KK/EFT kayıt hatası:", e);
      Alert.alert("Hata", "İşlem kaydedilemedi.");
    }
  };

  const setKkDurum = (item: any, durum: string) => {
    Alert.alert(
      "KK Durumu",
      `"${item.musteriUnvan || "Müşteri"}" işlemi "${durum}" olarak güncellensin mi?`,
      [
        { text: "İptal", style: "cancel" },
        {
          text: "Evet",
          onPress: async () => {
            try {
              const id = item.id ?? item.firebaseKey;
              const current = await readData(`KrediKartlari/${id}`);
              const ok = await writeData(`KrediKartlari/${id}`, {
                ...(current || item),
                durum,
                isDeleted: false,
              });
              if (!ok) {
                Alert.alert(
                  "Hata",
                  "KK işlemi güncellenemedi. (Bağlantı sorunu — kayıt eşitlenemedi.)",
                );
                return;
              }
              Alert.alert("Başarılı", "KK işlemi güncellendi.");
            } catch (e) {
              console.error("KK durum hatası:", e);
              Alert.alert("Hata", "Durum güncellenemedi.");
            }
          },
        },
      ],
    );
  };

  const setEftDurum = (item: any, durum: string) => {
    Alert.alert(
      "EFT Durumu",
      `"${item.musteriUnvan || "Müşteri"}" işlemi "${durum}" olarak güncellensin mi?`,
      [
        { text: "İptal", style: "cancel" },
        {
          text: "Evet",
          onPress: async () => {
            try {
              const id = item.id ?? item.firebaseKey;
              const current = await readData(`EftIslemleri/${id}`);
              const ok = await writeData(`EftIslemleri/${id}`, {
                ...(current || item),
                durum,
                isDeleted: false,
              });
              if (!ok) {
                Alert.alert(
                  "Hata",
                  "EFT işlemi güncellenemedi. (Bağlantı sorunu — kayıt eşitlenemedi.)",
                );
                return;
              }
              Alert.alert("Başarılı", "EFT işlemi güncellendi.");
            } catch (e) {
              console.error("EFT durum hatası:", e);
              Alert.alert("Hata", "Durum güncellenemedi.");
            }
          },
        },
      ],
    );
  };

  const deleteKk = (item: any) => {
    Alert.alert(
      "KK İşlemini Sil",
      `"${item.musteriUnvan || "Müşteri"}" işlemini silmek istediğinize emin misiniz?`,
      [
        { text: "İptal", style: "cancel" },
        {
          text: "Evet, Sil",
          style: "destructive",
          onPress: async () => {
            try {
              const id = item.id ?? item.firebaseKey;
              const current = await readData(`KrediKartlari/${id}`);
              const ok = await writeData(`KrediKartlari/${id}`, {
                ...(current || item),
                isDeleted: true,
              });
              if (!ok) {
                Alert.alert(
                  "Hata",
                  "KK işlemi silinemedi. (Bağlantı sorunu — kayıt eşitlenemedi.)",
                );
                return;
              }
              Alert.alert("Başarılı", "KK işlemi silindi.");
            } catch (e) {
              console.error("KK silme hatası:", e);
              Alert.alert("Hata", "İşlem silinemedi.");
            }
          },
        },
      ],
    );
  };

  const deleteEft = (item: any) => {
    Alert.alert(
      "EFT İşlemini Sil",
      `"${item.musteriUnvan || "Müşteri"}" işlemini silmek istediğinize emin misiniz?`,
      [
        { text: "İptal", style: "cancel" },
        {
          text: "Evet, Sil",
          style: "destructive",
          onPress: async () => {
            try {
              const id = item.id ?? item.firebaseKey;
              const current = await readData(`EftIslemleri/${id}`);
              const ok = await writeData(`EftIslemleri/${id}`, {
                ...(current || item),
                isDeleted: true,
              });
              if (!ok) {
                Alert.alert(
                  "Hata",
                  "EFT işlemi silinemedi. (Bağlantı sorunu — kayıt eşitlenemedi.)",
                );
                return;
              }
              Alert.alert("Başarılı", "EFT işlemi silindi.");
            } catch (e) {
              console.error("EFT silme hatası:", e);
              Alert.alert("Hata", "İşlem silinemedi.");
            }
          },
        },
      ],
    );
  };

  const renderKkList = () => {
    const list = kkIslemler
      .filter((i) => !i.isDeleted)
      .sort(
        (a, b) => new Date(b.tarih).getTime() - new Date(a.tarih).getTime(),
      );
    return (
      <FlashList
        data={list}
        keyExtractor={(item, idx) =>
          item.id?.toString() || item.firebaseKey || idx.toString()
        }
        contentContainerStyle={{ padding: 20 }}
        ListHeaderComponent={
          <TouchableOpacity
            style={[
              styles.saveButton,
              { marginTop: 0, marginBottom: 14, backgroundColor: "#0061FF" },
            ]}
            onPress={openKkNew}
          >
            <Plus color="#FFF" size={20} />
            <Text style={[styles.saveButtonText, { color: "#FFF" }]}>
              Yeni Kredi Kartı İşlemi
            </Text>
          </TouchableOpacity>
        }
        ListFooterComponent={
          <View style={styles.cekFooter}>
            <Text style={{ color: "#94A3B8", fontSize: 12, fontWeight: "700" }}>
              TOPLAM KK TUTARI:
            </Text>
            <Text style={{ color: "#00FF87", fontSize: 14, fontWeight: "900" }}>
              {formatMoney(list.reduce((s, i) => s + (i.tutar || 0), 0))}
            </Text>
          </View>
        }
        renderItem={({ item }) => (
          <View style={styles.cekCard}>
            <View
              style={{
                flexDirection: "row",
                justifyContent: "space-between",
                marginBottom: 6,
              }}
            >
              <Text style={styles.cekNo}>
                {item.musteriUnvan || item.musteriAdi || "Müşteri"}
              </Text>
              <Text style={styles.cekDurumText}>
                {item.durum || "Portföyde"}
              </Text>
            </View>
            <Text style={styles.cekDetail}>
              Tarih: {item.tarih} • {item.banka || "-"} • Kart:{" "}
              {item.kartNo ? `****${item.kartNo.slice(-4)}` : "-"}
            </Text>
            {item.yonlendirilenCariUnvan ? (
              <Text style={[styles.cekDetail, { color: "#00D4FF" }]}>
                Ciro → {item.yonlendirilenCariUnvan}
              </Text>
            ) : null}
            <Text style={styles.cekAmt}>{formatMoney(item.tutar)}</Text>

            {/* Durum geçişleri (desktop KrediKartiListViewModel durumları) */}
            <View
              style={{
                flexDirection: "row",
                flexWrap: "wrap",
                gap: 6,
                marginTop: 10,
              }}
            >
              {[
                "Tahsil Edildi",
                "Bankaya Geçti",
                "Tedarikçiye Verildi",
                "İptal",
              ]
                .filter((d) => d !== item.durum)
                .map((d) => (
                  <TouchableOpacity
                    key={d}
                    style={[
                      styles.quickStatusBtn,
                      {
                        backgroundColor: "rgba(0,212,255,0.12)",
                        borderColor: "#00D4FF",
                      },
                    ]}
                    onPress={() => setKkDurum(item, d)}
                  >
                    <Text
                      style={{
                        color: "#00D4FF",
                        fontSize: 10,
                        fontWeight: "800",
                      }}
                    >
                      {d}
                    </Text>
                  </TouchableOpacity>
                ))}
            </View>

            <View
              style={{
                flexDirection: "row",
                justifyContent: "flex-end",
                gap: 12,
                marginTop: 6,
              }}
            >
              <TouchableOpacity
                onPress={() => openKkEdit(item)}
                style={{ flexDirection: "row", alignItems: "center", gap: 4 }}
              >
                <Edit3 color="#94A3B8" size={14} />
                <Text style={{ color: "#94A3B8", fontSize: 11 }}>Düzenle</Text>
              </TouchableOpacity>
              <TouchableOpacity
                onPress={() => deleteKk(item)}
                style={{ flexDirection: "row", alignItems: "center", gap: 4 }}
              >
                <Trash2 color="#EF4444" size={14} />
                <Text style={{ color: "#EF4444", fontSize: 11 }}>Sil</Text>
              </TouchableOpacity>
            </View>
            {renderImage(item.slipDosyaYolu || item.slipPath)}
          </View>
        )}
      />
    );
  };

  const renderEftList = () => {
    const list = eftIslemler
      .filter((i) => !i.isDeleted)
      .sort(
        (a, b) => new Date(b.tarih).getTime() - new Date(a.tarih).getTime(),
      );
    return (
      <FlashList
        data={list}
        keyExtractor={(item, idx) =>
          item.id?.toString() || item.firebaseKey || idx.toString()
        }
        contentContainerStyle={{ padding: 20 }}
        ListHeaderComponent={
          <>
            {bankalar.length > 0 && (
              <View style={{ marginBottom: 20 }}>
                <Text style={styles.sectionTitle}>BANKALARIM</Text>
                <ScrollView horizontal showsHorizontalScrollIndicator={false}>
                  {bankalar.map((b) => (
                    <TouchableOpacity
                      key={b.id}
                      style={[styles.dashCard, { marginRight: 10, width: 160 }]}
                      onPress={() => handleOpenHesapEdit(b, "Banka")}
                    >
                      <View
                        style={{
                          flexDirection: "row",
                          justifyContent: "space-between",
                          marginBottom: 4,
                        }}
                      >
                        <Text style={styles.dashLabel} numberOfLines={1}>
                          {b.hesapAdi || b.bankaAdi}
                        </Text>
                        <TouchableOpacity onPress={() => handleDeleteHesap(b)}>
                          <Trash2 color="#EF4444" size={14} />
                        </TouchableOpacity>
                      </View>
                      <Text style={[styles.dashVal, { fontSize: 18 }]}>
                        {formatMoney(b.bakiye ?? b.guncelBakiye ?? 0)}
                      </Text>
                      <Text
                        style={{ color: "#94A3B8", fontSize: 10, marginTop: 4 }}
                      >
                        IBAN: {b.iban || b.hesapNo || "-"}
                      </Text>
                    </TouchableOpacity>
                  ))}
                </ScrollView>
              </View>
            )}
            <TouchableOpacity
              style={[
                styles.saveButton,
                { marginTop: 0, marginBottom: 14, backgroundColor: "#0061FF" },
              ]}
              onPress={openEftNew}
            >
              <Plus color="#FFF" size={20} />
              <Text style={[styles.saveButtonText, { color: "#FFF" }]}>
                Yeni Havale/EFT İşlemi
              </Text>
            </TouchableOpacity>
            <Text style={styles.sectionTitle}>HAVALE / EFT İŞLEMLERİ</Text>
          </>
        }
        ListFooterComponent={
          <View style={styles.cekFooter}>
            <Text style={{ color: "#94A3B8", fontSize: 12, fontWeight: "700" }}>
              TOPLAM EFT TUTARI:
            </Text>
            <Text style={{ color: "#00FF87", fontSize: 14, fontWeight: "900" }}>
              {formatMoney(list.reduce((s, i) => s + (i.tutar || 0), 0))}
            </Text>
          </View>
        }
        renderItem={({ item }) => (
          <View style={styles.cekCard}>
            <View
              style={{
                flexDirection: "row",
                justifyContent: "space-between",
                marginBottom: 6,
              }}
            >
              <Text style={styles.cekNo}>
                {item.musteriUnvan || item.musteriAdi || "Müşteri"}
              </Text>
              <Text style={styles.cekDurumText}>
                {item.durum || "Portföyde"}
              </Text>
            </View>
            <Text style={styles.cekDetail}>
              Tarih: {item.tarih} • {item.banka || "-"} •{" "}
              {item.hesapNo || item.hesapNoIban || "-"}
            </Text>
            {item.yonlendirilenCariUnvan ? (
              <Text style={[styles.cekDetail, { color: "#00D4FF" }]}>
                Ciro → {item.yonlendirilenCariUnvan}
              </Text>
            ) : null}
            <Text style={styles.cekAmt}>{formatMoney(item.tutar)}</Text>

            {/* Durum geçişleri (desktop EFTListViewModel durumları) */}
            <View
              style={{
                flexDirection: "row",
                flexWrap: "wrap",
                gap: 6,
                marginTop: 10,
              }}
            >
              {[
                "Tamamlandı",
                "Tahsil Edildi",
                "Tedarikçiye Yönlendirildi",
                "İptal",
              ]
                .filter((d) => d !== item.durum)
                .map((d) => (
                  <TouchableOpacity
                    key={d}
                    style={[
                      styles.quickStatusBtn,
                      {
                        backgroundColor: "rgba(0,212,255,0.12)",
                        borderColor: "#00D4FF",
                      },
                    ]}
                    onPress={() => setEftDurum(item, d)}
                  >
                    <Text
                      style={{
                        color: "#00D4FF",
                        fontSize: 10,
                        fontWeight: "800",
                      }}
                    >
                      {d}
                    </Text>
                  </TouchableOpacity>
                ))}
            </View>

            <View
              style={{
                flexDirection: "row",
                justifyContent: "flex-end",
                gap: 12,
                marginTop: 6,
              }}
            >
              <TouchableOpacity
                onPress={() => openEftEdit(item)}
                style={{ flexDirection: "row", alignItems: "center", gap: 4 }}
              >
                <Edit3 color="#94A3B8" size={14} />
                <Text style={{ color: "#94A3B8", fontSize: 11 }}>Düzenle</Text>
              </TouchableOpacity>
              <TouchableOpacity
                onPress={() => deleteEft(item)}
                style={{ flexDirection: "row", alignItems: "center", gap: 4 }}
              >
                <Trash2 color="#EF4444" size={14} />
                <Text style={{ color: "#EF4444", fontSize: 11 }}>Sil</Text>
              </TouchableOpacity>
            </View>
            {renderImage(item.dekontPath)}
          </View>
        )}
      />
    );
  };

  return (
    <SafeAreaView style={styles.container}>
      <View style={styles.header}>
        <View
          style={{
            flexDirection: "row",
            justifyContent: "space-between",
            alignItems: "center",
          }}
        >
          <Text style={styles.headerTitle}>Finans İşlemleri</Text>
          <View style={{ flexDirection: "row", gap: 8 }}>
            {activeTab === "nakit" && (
              <TouchableOpacity
                style={[styles.addButton, { backgroundColor: "#334155" }]}
                onPress={() => handleOpenHesapAdd("Kasa")}
              >
                <Wallet color="#FFF" size={16} />
                <Text style={styles.addButtonText}>Hesap</Text>
              </TouchableOpacity>
            )}
            <TouchableOpacity style={styles.addButton} onPress={handleOpenAdd}>
              <Plus color="#FFF" size={20} />
              <Text style={styles.addButtonText}>Yeni İşlem</Text>
            </TouchableOpacity>
          </View>
        </View>
      </View>

      {/* 5 Sekmeli Menü */}
      <View style={styles.tabScrollBox}>
        <ScrollView
          horizontal
          showsHorizontalScrollIndicator={false}
          contentContainerStyle={styles.tabContainer}
        >
          <TouchableOpacity
            style={[
              styles.tabButton,
              activeTab === "dashboard" && styles.tabButtonActive,
            ]}
            onPress={() => setActiveTab("dashboard")}
          >
            <Text
              style={[
                styles.tabText,
                activeTab === "dashboard" && styles.tabTextActive,
              ]}
            >
              Dashboard
            </Text>
          </TouchableOpacity>
          <TouchableOpacity
            style={[
              styles.tabButton,
              activeTab === "nakit" && styles.tabButtonActive,
            ]}
            onPress={() => setActiveTab("nakit")}
          >
            <Text
              style={[
                styles.tabText,
                activeTab === "nakit" && styles.tabTextActive,
              ]}
            >
              Nakit
            </Text>
          </TouchableOpacity>
          <TouchableOpacity
            style={[
              styles.tabButton,
              activeTab === "cek" && styles.tabButtonActive,
            ]}
            onPress={() => setActiveTab("cek")}
          >
            <Text
              style={[
                styles.tabText,
                activeTab === "cek" && styles.tabTextActive,
              ]}
            >
              Çekler
            </Text>
          </TouchableOpacity>
          <TouchableOpacity
            style={[
              styles.tabButton,
              activeTab === "kk" && styles.tabButtonActive,
            ]}
            onPress={() => setActiveTab("kk")}
          >
            <Text
              style={[
                styles.tabText,
                activeTab === "kk" && styles.tabTextActive,
              ]}
            >
              Kredi Kartı
            </Text>
          </TouchableOpacity>
          <TouchableOpacity
            style={[
              styles.tabButton,
              activeTab === "eft" && styles.tabButtonActive,
            ]}
            onPress={() => setActiveTab("eft")}
          >
            <Text
              style={[
                styles.tabText,
                activeTab === "eft" && styles.tabTextActive,
              ]}
            >
              Havale/EFT
            </Text>
          </TouchableOpacity>
        </ScrollView>
      </View>

      {loading ? (
        <View style={styles.center}>
          <ActivityIndicator size="large" color="#0061FF" />
        </View>
      ) : (
        <View style={{ flex: 1 }}>
          {activeTab === "dashboard" && renderDashboard()}
          {activeTab === "nakit" && renderNakitList()}
          {activeTab === "cek" && renderCekList()}
          {activeTab === "kk" && renderKkList()}
          {activeTab === "eft" && renderEftList()}
        </View>
      )}

      {/* Yeni Finansal İşlem Modalı */}
      <SwipeableModal visible={isFormOpen} onClose={() => setIsFormOpen(false)}>
        <View style={styles.modalHeader}>
          <Text style={styles.modalTitle}>Yeni Finansal İşlem</Text>
          <TouchableOpacity onPress={() => setIsFormOpen(false)}>
            <X color="#FFF" size={24} />
          </TouchableOpacity>
        </View>

            <ScrollView contentContainerStyle={{ paddingBottom: 40 }}>
              {/* İşlem Türü */}
              <Text style={styles.label}>İşlem Türü</Text>
              <ScrollView
                horizontal
                showsHorizontalScrollIndicator={false}
                contentContainerStyle={{
                  flexDirection: "row",
                  paddingVertical: 4,
                }}
              >
                {[
                  "Tahsilat",
                  "Ödeme",
                  "Virman",
                  "Alacak Dekontu",
                  "Borç Dekontu",
                ].map((t: any) => (
                  <TouchableOpacity
                    key={t}
                    style={[
                      styles.segmentBtn,
                      islemTuru === t && styles.segmentBtnActive,
                      { marginRight: 8, paddingHorizontal: 12 },
                    ]}
                    onPress={() => {
                      setIslemTuru(t);
                      resetFormButKeepType(t);
                    }}
                  >
                    <Text
                      style={[
                        styles.segmentBtnText,
                        islemTuru === t && styles.segmentBtnTextActive,
                      ]}
                    >
                      {t}
                    </Text>
                  </TouchableOpacity>
                ))}
              </ScrollView>

              {islemTuru === "Virman" && (
                <>
                  <Text style={styles.label}>Kaynak Hesap *</Text>
                  <ScrollView
                    horizontal
                    showsHorizontalScrollIndicator={false}
                    contentContainerStyle={{
                      flexDirection: "row",
                      paddingVertical: 4,
                    }}
                  >
                    {tumHesaplar.map((h) => (
                      <TouchableOpacity
                        key={`src-${h.id}`}
                        style={[
                          styles.miniSelectCard,
                          sourceKasaId === h.id && styles.miniSelectCardActive,
                        ]}
                        onPress={() => setSourceKasaId(h.id)}
                      >
                        <Text
                          style={[
                            styles.miniSelectText,
                            sourceKasaId === h.id &&
                              styles.miniSelectTextActive,
                          ]}
                        >
                          {h.isim}
                        </Text>
                      </TouchableOpacity>
                    ))}
                  </ScrollView>

                  <Text style={styles.label}>Hedef Hesap *</Text>
                  <ScrollView
                    horizontal
                    showsHorizontalScrollIndicator={false}
                    contentContainerStyle={{
                      flexDirection: "row",
                      paddingVertical: 4,
                    }}
                  >
                    {tumHesaplar.map((h) => (
                      <TouchableOpacity
                        key={`dest-${h.id}`}
                        style={[
                          styles.miniSelectCard,
                          destKasaId === h.id && styles.miniSelectCardActive,
                        ]}
                        onPress={() => setDestKasaId(h.id)}
                      >
                        <Text
                          style={[
                            styles.miniSelectText,
                            destKasaId === h.id && styles.miniSelectTextActive,
                          ]}
                        >
                          {h.isim}
                        </Text>
                      </TouchableOpacity>
                    ))}
                  </ScrollView>
                </>
              )}

              {islemTuru !== "Virman" && (
                <>
                  {/* Ödeme Yöntemi (Dekont Değilse) */}
                  {islemTuru !== "Alacak Dekontu" &&
                    islemTuru !== "Borç Dekontu" && (
                      <>
                        <Text style={styles.label}>Ödeme Yöntemi</Text>
                        <View style={styles.segmentRow}>
                          {["Nakit", "Kredi Kartı", "Havale/EFT", "Çek"].map(
                            (m: any) => (
                              <TouchableOpacity
                                key={m}
                                style={[
                                  styles.segmentBtn,
                                  odemeYontemi === m && styles.segmentBtnActive,
                                ]}
                                onPress={() => setOdemeYontemi(m)}
                              >
                                <Text
                                  style={[
                                    styles.segmentBtnText,
                                    odemeYontemi === m &&
                                      styles.segmentBtnTextActive,
                                  ]}
                                >
                                  {m}
                                </Text>
                              </TouchableOpacity>
                            ),
                          )}
                        </View>
                      </>
                    )}

                  {/* Cari Seçici */}
                  <Text style={styles.label}>Cari Hesap</Text>
                  <TouchableOpacity
                    style={styles.selector}
                    onPress={() => setIsCariOverlayOpen(true)}
                  >
                    <Text style={styles.selectorText}>
                      {selectedCari ? selectedCari.unvan : "Cari Seçin..."}
                    </Text>
                  </TouchableOpacity>

                  {/* Kasa/Banka Seçimi */}
                  {islemTuru !== "Alacak Dekontu" &&
                    islemTuru !== "Borç Dekontu" && (
                      <>
                        {odemeYontemi === "Nakit" ? (
                          <>
                            <Text style={styles.label}>Kasa Hesabı *</Text>
                            <View style={styles.accountRow}>
                              {kasalar.map((k) => (
                                <TouchableOpacity
                                  key={k.id}
                                  style={[
                                    styles.accBtn,
                                    selectedKasaId === k.id &&
                                      styles.accBtnActive,
                                  ]}
                                  onPress={() => setSelectedKasaId(k.id)}
                                >
                                  <Text style={styles.accBtnText}>
                                    {k.hesapAdi || k.bankaAdi || k.ad || k.isim || 'İsimsiz Kasa'}
                                  </Text>
                                </TouchableOpacity>
                              ))}
                            </View>
                          </>
                        ) : (
                          <>
                            <Text style={styles.label}>Banka Hesabı *</Text>
                            <View style={styles.accountRow}>
                              {bankalar.map((b) => (
                                <TouchableOpacity
                                  key={b.id}
                                  style={[
                                    styles.accBtn,
                                    selectedBankaId === b.id &&
                                      styles.accBtnActive,
                                  ]}
                                  onPress={() => setSelectedBankaId(b.id)}
                                >
                                  <Text style={styles.accBtnText}>
                                    {b.hesapAdi || b.bankaAdi || b.isim || 'İsimsiz Banka'}
                                  </Text>
                                </TouchableOpacity>
                              ))}
                            </View>
                          </>
                        )}
                      </>
                    )}

                  {/* Çek Detay Formu ve Görsel Yolu */}
                  {odemeYontemi === "Çek" &&
                    islemTuru !== "Alacak Dekontu" &&
                    islemTuru !== "Borç Dekontu" && (
                      <View style={styles.extraBox}>
                        <Text style={styles.extraTitle}>Çek Detayları</Text>
                        <TextInput
                          style={styles.input}
                          placeholder="Asıl Borçlu"
                          placeholderTextColor="#64748B"
                          value={asilBorclu}
                          onChangeText={setAsilBorclu}
                        />
                        <TextInput
                          style={styles.input}
                          placeholder="Banka"
                          placeholderTextColor="#64748B"
                          value={cekBanka}
                          onChangeText={setCekBanka}
                        />
                        <TextInput
                          style={styles.input}
                          placeholder="Şube"
                          placeholderTextColor="#64748B"
                          value={cekSube}
                          onChangeText={setCekSube}
                        />
                        <TextInput
                          style={styles.input}
                          placeholder="Seri No"
                          placeholderTextColor="#64748B"
                          value={cekSeriNo}
                          onChangeText={setCekSeriNo}
                        />
                        <TextInput
                          style={styles.input}
                          placeholder="Vade Tarihi (YYYY-MM-DD)"
                          placeholderTextColor="#64748B"
                          value={cekVadeTarihi}
                          onChangeText={setCekVadeTarihi}
                        />

                        {/* Görsel ekleme alanları */}
                        <Text style={styles.label}>
                          Çek Görsel Eki (Ön Yüz URL / Base64)
                        </Text>
                        <View
                          style={{
                            flexDirection: "row",
                            gap: 8,
                            alignItems: "center",
                          }}
                        >
                          <TextInput
                            style={[styles.input, { flex: 1, marginTop: 0 }]}
                            placeholder="http://... veya seçin..."
                            placeholderTextColor="#64748B"
                            value={cekGorselYoluOn}
                            onChangeText={setCekGorselYoluOn}
                          />
                          <TouchableOpacity
                            style={styles.pickImageBtn}
                            onPress={() => pickImage(setCekGorselYoluOn)}
                          >
                            <Camera color="#FFF" size={18} />
                          </TouchableOpacity>
                        </View>
                        {cekGorselYoluOn ? (
                          <Image
                            source={{ uri: cekGorselYoluOn }}
                            style={{
                              width: 80,
                              height: 80,
                              borderRadius: 12,
                              marginTop: 8,
                            }}
                          />
                        ) : null}

                        <Text style={styles.label}>
                          Çek Görsel Eki (Arka Yüz URL / Base64)
                        </Text>
                        <View
                          style={{
                            flexDirection: "row",
                            gap: 8,
                            alignItems: "center",
                          }}
                        >
                          <TextInput
                            style={[styles.input, { flex: 1, marginTop: 0 }]}
                            placeholder="http://... veya seçin..."
                            placeholderTextColor="#64748B"
                            value={cekGorselYoluArka}
                            onChangeText={setCekGorselYoluArka}
                          />
                          <TouchableOpacity
                            style={styles.pickImageBtn}
                            onPress={() => pickImage(setCekGorselYoluArka)}
                          >
                            <Camera color="#FFF" size={18} />
                          </TouchableOpacity>
                        </View>
                        {cekGorselYoluArka ? (
                          <Image
                            source={{ uri: cekGorselYoluArka }}
                            style={{
                              width: 80,
                              height: 80,
                              borderRadius: 12,
                              marginTop: 8,
                            }}
                          />
                        ) : null}
                      </View>
                    )}

                  {/* Kredi Kartı / EFT Görsel Eki (Slip) */}
                  {(odemeYontemi === "Kredi Kartı" ||
                    odemeYontemi === "Havale/EFT") && (
                    <View style={styles.extraBox}>
                      <Text style={styles.extraTitle}>
                        Kredi Kartı / EFT Ekleri
                      </Text>
                      <TextInput
                        style={styles.input}
                        placeholder="Banka Adı"
                        placeholderTextColor="#64748B"
                        value={kkBanka}
                        onChangeText={setKkBanka}
                      />
                      <TextInput
                        style={styles.input}
                        placeholder="Kart No / Son 4 Hane"
                        placeholderTextColor="#64748B"
                        value={kkKartNo}
                        onChangeText={setKkKartNo}
                      />
                      <TextInput
                        style={styles.input}
                        placeholder="Dekont / Onay No"
                        placeholderTextColor="#64748B"
                        value={kkOnayKodu}
                        onChangeText={setKkOnayKodu}
                      />

                      <Text style={styles.label}>Slip / Dekont Görsel Eki</Text>
                      <View
                        style={{
                          flexDirection: "row",
                          gap: 8,
                          alignItems: "center",
                        }}
                      >
                        <TextInput
                          style={[styles.input, { flex: 1, marginTop: 0 }]}
                          placeholder="http://... veya seçin..."
                          placeholderTextColor="#64748B"
                          value={kkSlipPath}
                          onChangeText={setKkSlipPath}
                        />
                        <TouchableOpacity
                          style={styles.pickImageBtn}
                          onPress={() => pickImage(setKkSlipPath)}
                        >
                          <Camera color="#FFF" size={18} />
                        </TouchableOpacity>
                      </View>
                      {kkSlipPath ? (
                        <Image
                          source={{ uri: kkSlipPath }}
                          style={{
                            width: 80,
                            height: 80,
                            borderRadius: 12,
                            marginTop: 8,
                          }}
                        />
                      ) : null}
                    </View>
                  )}
                </>
              )}

              {/* Tutar, Tarih, Açıklama */}
              <Text style={styles.label}>Tutar (₺)</Text>
              <TextInput
                style={styles.input}
                keyboardType="numeric"
                inputAccessoryViewID={KEYBOARD_ACCESSORY_ID}
                placeholder="0.00"
                placeholderTextColor="#64748B"
                value={tutar}
                onChangeText={setTutar}
              />

              <Text style={styles.label}>İşlem Tarihi</Text>
              <TextInput
                style={styles.input}
                placeholder="YYYY-MM-DD"
                placeholderTextColor="#64748B"
                value={tarih}
                onChangeText={setTarih}
              />

              <Text style={styles.label}>Açıklama</Text>
              <TextInput
                style={[styles.input, { height: 60 }]}
                multiline={true}
                placeholder="Açıklama notu..."
                placeholderTextColor="#64748B"
                value={aciklama}
                onChangeText={setAciklama}
              />

              <TouchableOpacity
                style={[styles.saveButton, isSaving && { opacity: 0.7 }]}
                disabled={isSaving}
                onPress={handleSave}
              >
                {isSaving ? (
                  <ActivityIndicator color="#FFF" size="small" />
                ) : (
                  <Save color="#FFF" size={20} />
                )}
                <Text style={styles.saveButtonText}>
                  {isSaving ? "Kaydediliyor..." : "Kaydet ve İşle"}
                </Text>
              </TouchableOpacity>
            </ScrollView>

            {/* Cari Seçici Absolute Overlay (Nested Modal yerine) */}
            <SwipeableOverlay
              visible={isCariOverlayOpen}
              onClose={() => setIsCariOverlayOpen(false)}
            >
              <View style={styles.modalHeader}>
                <Text style={styles.modalTitle}>Cari Seçin</Text>
                <TouchableOpacity onPress={() => setIsCariOverlayOpen(false)}>
                  <X color="#FFF" size={24} />
                </TouchableOpacity>
              </View>
                <View style={[styles.searchBox, { marginBottom: 16 }]}>
                  <Search color="#64748B" size={20} />
                  <TextInput
                    style={styles.searchInput}
                    placeholder="Cari ara..."
                    placeholderTextColor="#64748B"
                    value={cariSearch}
                    onChangeText={setCariSearch}
                  />
                </View>
                <FlashList
                  data={cariler.filter((c) =>
                    (c.unvan || "")
                      .toLocaleLowerCase('tr-TR')
                      .includes(cariSearch.toLocaleLowerCase('tr-TR')),
                  )}
                  keyExtractor={(item, idx) =>
                    item.id?.toString() || idx.toString()
                  }
                  renderItem={({ item }) => (
                    <TouchableOpacity
                      style={styles.selectorItem}
                      onPress={() => {
                        setSelectedCari(item);
                        setIsCariOverlayOpen(false);
                      }}
                    >
                      <Text style={styles.selectorItemText}>{item.unvan}</Text>
                      <Text style={styles.selectorItemSub}>{item.grup}</Text>
                    </TouchableOpacity>
                  )}
                />
            </SwipeableOverlay>
        </SwipeableModal>

      {/* Kasa / Banka Hesap Tanımlama Modalı */}
      <SwipeableModal visible={isHesapFormOpen} onClose={() => setIsHesapFormOpen(false)}>
        <View style={styles.modalHeader}>
          <Text style={styles.modalTitle}>
            {hesapEditingId ? "Kasa Düzenle" : "Yeni Kasa Tanımla"}
          </Text>
          <TouchableOpacity onPress={() => setIsHesapFormOpen(false)}>
            <X color="#FFF" size={24} />
          </TouchableOpacity>
        </View>

            <ScrollView contentContainerStyle={{ paddingBottom: 40 }}>
              <Text style={styles.label}>Kasa Adı *</Text>
              <TextInput
                style={styles.input}
                placeholder="örn. Merkez Kasa, Mağaza Kasası..."
                placeholderTextColor="#64748B"
                value={hesapAdi}
                onChangeText={setHesapAdi}
              />

              <Text style={styles.label}>Döviz Türü</Text>
              <View style={{ flexDirection: "row", marginBottom: 12 }}>
                {["TL", "USD", "EUR", "GBP"].map((d) => (
                  <TouchableOpacity
                    key={d}
                    style={[
                      styles.segmentBtn,
                      hesapDoviz === d && styles.segmentBtnActive,
                      { flex: 1, marginRight: d === "GBP" ? 0 : 8 },
                    ]}
                    onPress={() => setHesapDoviz(d)}
                  >
                    <Text
                      style={[
                        styles.segmentBtnText,
                        hesapDoviz === d && styles.segmentBtnTextActive,
                      ]}
                    >
                      {d}
                    </Text>
                  </TouchableOpacity>
                ))}
              </View>

              <Text style={styles.label}>Yetkili Kişi</Text>
              <TextInput
                style={styles.input}
                placeholder="Kasa sorumlusunun adı..."
                placeholderTextColor="#64748B"
                value={hesapYetkili}
                onChangeText={setHesapYetkili}
              />

              <Text style={styles.label}>Açılış Bakiyesi ({hesapDoviz})</Text>
              <TextInput
                style={styles.input}
                keyboardType="numeric"
                inputAccessoryViewID={KEYBOARD_ACCESSORY_ID}
                placeholder="0.00"
                placeholderTextColor="#64748B"
                value={hesapAcilisBakiye}
                onChangeText={setHesapAcilisBakiye}
              />

              <TouchableOpacity style={styles.saveButton} onPress={saveHesap}>
                <Save color="#FFF" size={20} />
                <Text style={styles.saveButtonText}>Kaydet</Text>
              </TouchableOpacity>
            </ScrollView>
      </SwipeableModal>

      {/* Finans Hareketi Düzenleme Modalı */}
      <SwipeableModal visible={isMoveEditOpen} onClose={() => setIsMoveEditOpen(false)}>
        <View style={styles.modalHeader}>
          <Text style={styles.modalTitle}>Finans Hareketi Düzenle</Text>
          <TouchableOpacity onPress={() => setIsMoveEditOpen(false)}>
            <X color="#FFF" size={24} />
          </TouchableOpacity>
        </View>

            <ScrollView contentContainerStyle={{ paddingBottom: 40 }}>
              {moveEdit && (
                <>
                  <Text style={styles.label}>Cari</Text>
                  <Text style={styles.selectorText}>
                    {moveEdit.cariUnvan || "-"}
                  </Text>

                  <Text style={styles.label}>İşlem Tarihi</Text>
                  <TextInput
                    style={styles.input}
                    placeholder="YYYY-MM-DD"
                    placeholderTextColor="#64748B"
                    value={tarih}
                    onChangeText={setTarih}
                  />

                  <Text style={styles.label}>
                    Tutar ({moveEdit.giren > 0 ? "Giriş" : "Çıkış"})
                  </Text>
                  <TextInput
                    style={styles.input}
                    keyboardType="numeric"
                    inputAccessoryViewID={KEYBOARD_ACCESSORY_ID}
                    placeholder="0.00"
                    placeholderTextColor="#64748B"
                    value={tutar}
                    onChangeText={setTutar}
                  />

                  <Text style={styles.label}>Açıklama</Text>
                  <TextInput
                    style={[styles.input, { height: 60 }]}
                    multiline={true}
                    placeholder="Açıklama..."
                    placeholderTextColor="#64748B"
                    value={aciklama}
                    onChangeText={setAciklama}
                  />

                  <TouchableOpacity
                    style={styles.saveButton}
                    onPress={saveMovementEdit}
                  >
                    <Save color="#FFF" size={20} />
                    <Text style={styles.saveButtonText}>Güncelle</Text>
                  </TouchableOpacity>
                </>
              )}
            </ScrollView>
      </SwipeableModal>

      {/* Çek Düzenleme Modalı */}
      <SwipeableModal visible={isCekEditOpen} onClose={() => setIsCekEditOpen(false)}>
        <View style={styles.modalHeader}>
          <Text style={styles.modalTitle}>Çek Düzenle</Text>
          <TouchableOpacity onPress={() => setIsCekEditOpen(false)}>
            <X color="#FFF" size={24} />
          </TouchableOpacity>
        </View>

            <ScrollView contentContainerStyle={{ paddingBottom: 40 }}>
              <Text style={styles.label}>Çek Türü</Text>
              <View style={{ flexDirection: "row", marginBottom: 12 }}>
                <TouchableOpacity
                  style={[
                    styles.segmentBtn,
                    cekTuru === "Alınan" && styles.segmentBtnActive,
                    { flex: 1, marginRight: 8 },
                  ]}
                  onPress={() => setCekTuru("Alınan")}
                >
                  <Text
                    style={[
                      styles.segmentBtnText,
                      cekTuru === "Alınan" && styles.segmentBtnTextActive,
                    ]}
                  >
                    Alınan
                  </Text>
                </TouchableOpacity>
                <TouchableOpacity
                  style={[
                    styles.segmentBtn,
                    cekTuru === "Verilen" && styles.segmentBtnActive,
                    { flex: 1 },
                  ]}
                  onPress={() => setCekTuru("Verilen")}
                >
                  <Text
                    style={[
                      styles.segmentBtnText,
                      cekTuru === "Verilen" && styles.segmentBtnTextActive,
                    ]}
                  >
                    Verilen
                  </Text>
                </TouchableOpacity>
              </View>

              <Text style={styles.label}>Tutar</Text>
              <TextInput
                style={styles.input}
                keyboardType="numeric"
                inputAccessoryViewID={KEYBOARD_ACCESSORY_ID}
                placeholder="0.00"
                placeholderTextColor="#64748B"
                value={tutar}
                onChangeText={setTutar}
              />

              <Text style={styles.label}>Banka</Text>
              <TextInput
                style={styles.input}
                placeholder="Banka adı"
                placeholderTextColor="#64748B"
                value={cekBanka}
                onChangeText={setCekBanka}
              />

              <Text style={styles.label}>Şube</Text>
              <TextInput
                style={styles.input}
                placeholder="Şube"
                placeholderTextColor="#64748B"
                value={cekSube}
                onChangeText={setCekSube}
              />

              <Text style={styles.label}>Seri No</Text>
              <TextInput
                style={styles.input}
                placeholder="Seri No"
                placeholderTextColor="#64748B"
                value={cekSeriNo}
                onChangeText={setCekSeriNo}
              />

              <Text style={styles.label}>Vade Tarihi</Text>
              <TextInput
                style={styles.input}
                placeholder="YYYY-MM-DD"
                placeholderTextColor="#64748B"
                value={cekVadeTarihi}
                onChangeText={setCekVadeTarihi}
              />

              <Text style={styles.label}>Portföy No</Text>
              <TextInput
                style={styles.input}
                placeholder="Portföy No"
                placeholderTextColor="#64748B"
                value={cekPortfoyNo}
                onChangeText={setCekPortfoyNo}
              />

              <TouchableOpacity style={styles.saveButton} onPress={saveCekEdit}>
                <Save color="#FFF" size={20} />
                <Text style={styles.saveButtonText}>Güncelle</Text>
              </TouchableOpacity>
            </ScrollView>
      </SwipeableModal>

      {/* KK / EFT İşlem Formu (desktop KrediKartiListViewModel / EFTListViewModel) */}
      <SwipeableModal visible={isKkEftFormOpen} onClose={() => setIsKkEftFormOpen(false)}>
        <View style={styles.modalHeader}>
          <Text style={styles.modalTitle}>
            {kkEftMode === "kk"
              ? kkEftEditingId != null
                ? "Kredi Kartı İşlemi Düzenle"
                : "Yeni Kredi Kartı İşlemi"
              : kkEftEditingId != null
                ? "Havale/EFT İşlemi Düzenle"
                : "Yeni Havale/EFT İşlemi"}
          </Text>
          <TouchableOpacity onPress={() => setIsKkEftFormOpen(false)}>
            <X color="#FFF" size={24} />
          </TouchableOpacity>
        </View>

            <ScrollView contentContainerStyle={{ paddingBottom: 40 }}>
              <Text style={styles.label}>Müşteri (Tahsil Edilen) *</Text>
              <TouchableOpacity
                style={styles.selector}
                onPress={() => setIsKkEftMusteriPickerOpen(true)}
              >
                <Text style={styles.selectorText}>
                  {kkEftMusteri ? kkEftMusteri.unvan : "Müşteri Seçin..."}
                </Text>
              </TouchableOpacity>

              <Text style={styles.label}>İşlem Tarihi</Text>
              <TextInput
                style={styles.input}
                placeholder="YYYY-MM-DD"
                placeholderTextColor="#64748B"
                value={kkEftTarih}
                onChangeText={setKkEftTarih}
              />

              <Text style={styles.label}>Tutar (₺)</Text>
              <TextInput
                style={styles.input}
                keyboardType="numeric"
                inputAccessoryViewID={KEYBOARD_ACCESSORY_ID}
                placeholder="0.00"
                placeholderTextColor="#64748B"
                value={kkEftTutar}
                onChangeText={setKkEftTutar}
              />

              <Text style={styles.label}>Banka Adı</Text>
              <TextInput
                style={styles.input}
                placeholder="Banka adı"
                placeholderTextColor="#64748B"
                value={kkEftBanka}
                onChangeText={setKkEftBanka}
              />

              {kkEftMode === "kk" ? (
                <>
                  <Text style={styles.label}>Kart No (Son 4 Hane)</Text>
                  <TextInput
                    style={styles.input}
                    placeholder="Son 4 hane"
                    placeholderTextColor="#64748B"
                    maxLength={4}
                    value={kkEftKartNo}
                    onChangeText={setKkEftKartNo}
                  />
                  <Text style={styles.label}>Onay Kodu</Text>
                  <TextInput
                    style={styles.input}
                    placeholder="Provizyon kodu"
                    placeholderTextColor="#64748B"
                    value={kkEftOnayDekontNo}
                    onChangeText={setKkEftOnayDekontNo}
                  />
                </>
              ) : (
                <>
                  <Text style={styles.label}>Hesap No / IBAN</Text>
                  <TextInput
                    style={styles.input}
                    placeholder="TR..."
                    placeholderTextColor="#64748B"
                    value={kkEftHesapNo}
                    onChangeText={setKkEftHesapNo}
                  />
                  <Text style={styles.label}>Dekont No</Text>
                  <TextInput
                    style={styles.input}
                    placeholder="Dekont numarası"
                    placeholderTextColor="#64748B"
                    value={kkEftOnayDekontNo}
                    onChangeText={setKkEftOnayDekontNo}
                  />
                </>
              )}

              <Text style={styles.label}>
                Yönlendirilen Tedarikçi (Opsiyonel — Ciro)
              </Text>
              <TouchableOpacity
                style={styles.selector}
                onPress={() => setIsKkEftSupPickerOpen(true)}
              >
                <Text style={styles.selectorText}>
                  {kkEftYonlendirilen
                    ? kkEftYonlendirilen.unvan
                    : "Tedarikçi Seçin..."}
                </Text>
              </TouchableOpacity>
              {kkEftYonlendirilen && (
                <TouchableOpacity
                  onPress={() => setKkEftYonlendirilen(null)}
                  style={{ marginTop: 6 }}
                >
                  <Text
                    style={{
                      color: "#EF4444",
                      fontSize: 11,
                      fontWeight: "700",
                    }}
                  >
                    Yönlendirmeyi Kaldır
                  </Text>
                </TouchableOpacity>
              )}

              <Text style={styles.label}>Durum</Text>
              <ScrollView
                horizontal
                showsHorizontalScrollIndicator={false}
                contentContainerStyle={{
                  flexDirection: "row",
                  paddingVertical: 4,
                }}
              >
                {(kkEftMode === "kk"
                  ? [
                      "Portföyde",
                      "Bankaya Geçti",
                      "Tedarikçiye Verildi",
                      "Tahsil Edildi",
                      "İptal",
                    ]
                  : [
                      "Portföyde",
                      "Tedarikçiye Yönlendirildi",
                      "Tahsil Edildi",
                      "Tamamlandı",
                      "İptal",
                    ]
                ).map((d) => (
                  <TouchableOpacity
                    key={d}
                    style={[
                      styles.miniSelectCard,
                      kkEftDurum === d && styles.miniSelectCardActive,
                    ]}
                    onPress={() => setKkEftDurum(d)}
                  >
                    <Text
                      style={[
                        styles.miniSelectText,
                        kkEftDurum === d && styles.miniSelectTextActive,
                      ]}
                      numberOfLines={1}
                    >
                      {d}
                    </Text>
                  </TouchableOpacity>
                ))}
              </ScrollView>

              <Text style={styles.label}>
                {kkEftMode === "kk" ? "Slip Görseli" : "Dekont Görseli"}
              </Text>
              <View
                style={{ flexDirection: "row", gap: 8, alignItems: "center" }}
              >
                <TextInput
                  style={[styles.input, { flex: 1, marginTop: 0 }]}
                  placeholder="http://... veya seçin..."
                  placeholderTextColor="#64748B"
                  value={kkEftSlipPath}
                  onChangeText={setKkEftSlipPath}
                />
                <TouchableOpacity
                  style={styles.pickImageBtn}
                  onPress={() => pickImage(setKkEftSlipPath)}
                >
                  <Camera color="#FFF" size={18} />
                </TouchableOpacity>
              </View>
              {kkEftSlipPath ? (
                <Image
                  source={{ uri: kkEftSlipPath }}
                  style={{
                    width: 80,
                    height: 80,
                    borderRadius: 12,
                    marginTop: 8,
                  }}
                />
              ) : null}

              <Text style={styles.label}>Açıklama</Text>
              <TextInput
                style={[styles.input, { height: 60 }]}
                multiline={true}
                placeholder="Açıklama..."
                placeholderTextColor="#64748B"
                value={kkEftAciklama}
                onChangeText={setKkEftAciklama}
              />

              <TouchableOpacity style={styles.saveButton} onPress={saveKkEft}>
                <Save color="#FFF" size={20} />
                <Text style={styles.saveButtonText}>
                  {kkEftEditingId != null
                    ? "Değişiklikleri Kaydet"
                    : "İşlemi Kaydet"}
                </Text>
              </TouchableOpacity>
            </ScrollView>

            {/* Müşteri Seçimi */}
            <SwipeableOverlay
              visible={isKkEftMusteriPickerOpen}
              onClose={() => setIsKkEftMusteriPickerOpen(false)}
            >
              <View style={styles.modalHeader}>
                <Text style={styles.modalTitle}>Müşteri Seçin</Text>
                <TouchableOpacity
                  onPress={() => setIsKkEftMusteriPickerOpen(false)}
                >
                  <X color="#FFF" size={24} />
                </TouchableOpacity>
              </View>
                <View style={[styles.searchBox, { marginBottom: 16 }]}>
                  <Search color="#64748B" size={20} />
                  <TextInput
                    style={styles.searchInput}
                    placeholder="Müşteri ara..."
                    placeholderTextColor="#64748B"
                    value={cariSearch}
                    onChangeText={setCariSearch}
                  />
                </View>
                <FlashList
                  data={cariler.filter(
                    (c) =>
                      (c.grup || "").toLocaleLowerCase('tr-TR').includes("müşteri") ||
                      (c.grup || "").toLocaleLowerCase('tr-TR').includes("musteri") ||
                      (c.grup || "").toLocaleLowerCase('tr-TR').includes("alıcı") ||
                      (c.grup || "").toLocaleLowerCase('tr-TR').includes("alici") ||
                      (c.unvan || "")
                        .toLocaleLowerCase('tr-TR')
                        .includes(cariSearch.toLocaleLowerCase('tr-TR')),
                  )}
                  keyExtractor={(item, idx) =>
                    item.id?.toString() || idx.toString()
                  }
                  renderItem={({ item }) => (
                    <TouchableOpacity
                      style={styles.selectorItem}
                      onPress={() => {
                        setKkEftMusteri(item);
                        setIsKkEftMusteriPickerOpen(false);
                        setCariSearch("");
                      }}
                    >
                      <Text style={styles.selectorItemText}>{item.unvan}</Text>
                      <Text style={styles.selectorItemSub}>
                        {item.grup} • Bakiye:{" "}
                        {formatMoney((item.borc || 0) - (item.alacak || 0))}
                      </Text>
                    </TouchableOpacity>
                  )}
                />
            </SwipeableOverlay>

            {/* Tedarikçi Seçimi */}
            <SwipeableOverlay
              visible={isKkEftSupPickerOpen}
              onClose={() => setIsKkEftSupPickerOpen(false)}
            >
              <View style={styles.modalHeader}>
                <Text style={styles.modalTitle}>Tedarikçi Seçin</Text>
                <TouchableOpacity
                  onPress={() => setIsKkEftSupPickerOpen(false)}
                >
                  <X color="#FFF" size={24} />
                </TouchableOpacity>
              </View>
              <View style={[styles.searchBox, { marginBottom: 16 }]}>
                <Search color="#64748B" size={20} />
                <TextInput
                  style={styles.searchInput}
                  placeholder="Tedarikçi ara..."
                  placeholderTextColor="#64748B"
                  value={cariSearch}
                  onChangeText={setCariSearch}
                />
              </View>
              <FlashList
                data={cariler.filter(
                  (c) =>
                    (c.grup || "").toLocaleLowerCase('tr-TR').includes("tedarik") ||
                    (c.grup || "").toLocaleLowerCase('tr-TR').includes("ted") ||
                    (c.grup || "").toLocaleLowerCase('tr-TR').includes("satıcı") ||
                    (c.grup || "").toLocaleLowerCase('tr-TR').includes("satici") ||
                    (c.unvan || "")
                      .toLocaleLowerCase('tr-TR')
                      .includes(cariSearch.toLocaleLowerCase('tr-TR')),
                )}
                keyExtractor={(item, idx) =>
                  item.id?.toString() || idx.toString()
                }
                renderItem={({ item }) => (
                  <TouchableOpacity
                    style={styles.selectorItem}
                    onPress={() => {
                      setKkEftYonlendirilen(item);
                      setIsKkEftSupPickerOpen(false);
                      setCariSearch("");
                    }}
                  >
                    <Text style={styles.selectorItemText}>{item.unvan}</Text>
                    <Text style={styles.selectorItemSub}>
                      {item.grup} • Bakiye:{" "}
                      {formatMoney((item.borc || 0) - (item.alacak || 0))}
                    </Text>
                  </TouchableOpacity>
                )}
              />
            </SwipeableOverlay>
        </SwipeableModal>

      {/* Çek Ciro — Tedarikçi Seçimi */}
      <SwipeableOverlay
        visible={isCiroPickerOpen}
        onClose={() => {
          setIsCiroPickerOpen(false);
          setCiroCek(null);
        }}
      >
        <View style={styles.modalHeader}>
          <Text style={styles.modalTitle}>Ciro için Tedarikçi Seçin</Text>
          <TouchableOpacity
            onPress={() => {
              setIsCiroPickerOpen(false);
              setCiroCek(null);
            }}
          >
            <X color="#FFF" size={24} />
          </TouchableOpacity>
        </View>
          <View style={[styles.searchBox, { marginBottom: 16 }]}>
            <Search color="#64748B" size={20} />
            <TextInput
              style={styles.searchInput}
              placeholder="Tedarikçi ara..."
              placeholderTextColor="#64748B"
              value={cariSearch}
              onChangeText={setCariSearch}
            />
          </View>
          <FlashList
            data={cariler.filter(
              (c) =>
                (c.grup || "").toLocaleLowerCase('tr-TR').includes("tedarik") ||
                (c.grup || "").toLocaleLowerCase('tr-TR').includes("alıcı") ||
                (c.grup || "").toLocaleLowerCase('tr-TR').includes("alici") ||
                (c.grup || "").toLocaleLowerCase('tr-TR').includes("ted") ||
                (c.unvan || "")
                  .toLocaleLowerCase('tr-TR')
                  .includes(cariSearch.toLocaleLowerCase('tr-TR')),
            )}
            keyExtractor={(item, idx) => item.id?.toString() || idx.toString()}
            renderItem={({ item }) => (
              <TouchableOpacity
                style={styles.selectorItem}
                onPress={() => saveCekCiro(item)}
              >
                <Text style={styles.selectorItemText}>{item.unvan}</Text>
                <Text style={styles.selectorItemSub}>
                  {item.grup} • Bakiye:{" "}
                  {formatMoney((item.borc || 0) - (item.alacak || 0))}
                </Text>
              </TouchableOpacity>
            )}
          />
      </SwipeableOverlay>

      {/* KASA DETAYI VE HAREKETLERİ MODALI (Desktop KasaDetayView & KasaDetayViewModel Birebir Paritesi) */}
      <SwipeableModal visible={isKasaDetayOpen} onClose={handleCloseKasaDetay}>
        <View style={{ paddingHorizontal: 0, paddingTop: 16, flex: 1 }}>
          {selectedKasaForDetay && renderKasaDetayView(selectedKasaForDetay)}
        </View>
      </SwipeableModal>
      <KeyboardDoneAccessory />
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: "#0A0A0A",
  },
  center: {
    flex: 1,
    justifyContent: "center",
    alignItems: "center",
  },
  header: {
    padding: 20,
    paddingTop: 40,
    borderBottomWidth: 1,
    borderBottomColor: "rgba(255, 255, 255, 0.08)",
  },
  headerTitle: {
    fontSize: 22,
    fontWeight: "900",
    color: "#FFFFFF",
  },
  addButton: {
    flexDirection: "row",
    alignItems: "center",
    backgroundColor: "#0061FF",
    paddingHorizontal: 16,
    paddingVertical: 8,
    borderRadius: 12,
  },
  addButtonText: {
    color: "#FFF",
    fontWeight: "bold",
    fontSize: 12,
    marginLeft: 6,
  },
  tabScrollBox: {
    backgroundColor: "#161616",
    borderBottomWidth: 1,
    borderBottomColor: "rgba(255,255,255,0.08)",
  },
  tabContainer: {
    paddingHorizontal: 12,
    paddingVertical: 10,
  },
  tabButton: {
    paddingHorizontal: 16,
    paddingVertical: 8,
    borderRadius: 10,
    marginRight: 8,
    backgroundColor: "rgba(255,255,255,0.04)",
  },
  tabButtonActive: {
    backgroundColor: "#0061FF",
  },
  tabText: {
    color: "#94A3B8",
    fontWeight: "bold",
    fontSize: 12,
  },
  tabTextActive: {
    color: "#FFF",
  },
  dashboardGrid: {
    flexDirection: "row",
    flexWrap: "wrap",
    justifyContent: "space-between",
    gap: 8,
    marginBottom: 16,
  },
  dashCard: {
    width: "48%",
    backgroundColor: "#161616",
    borderRadius: 16,
    borderColor: "rgba(255, 255, 255, 0.08)",
    borderWidth: 1,
    padding: 14,
    alignItems: "center",
  },
  dashVal: {
    color: "#FFF",
    fontSize: 14,
    fontWeight: "900",
  },
  dashLabel: {
    color: "#64748B",
    fontSize: 10,
    marginTop: 4,
    fontWeight: "bold",
  },
  sectionTitle: {
    fontSize: 10,
    fontWeight: "bold",
    color: "#94A3B8",
    letterSpacing: 1,
    marginBottom: 10,
    marginTop: 14,
  },
  moveRow: {
    flexDirection: "row",
    justifyContent: "space-between",
    alignItems: "center",
    paddingVertical: 12,
    borderBottomWidth: 1,
    borderBottomColor: "rgba(255,255,255,0.05)",
  },
  moveTitle: {
    color: "#FFF",
    fontSize: 13,
    fontWeight: "bold",
  },
  moveSub: {
    color: "#64748B",
    fontSize: 11,
    marginTop: 2,
  },
  moveAmt: {
    fontSize: 13,
    fontWeight: "900",
  },
  cekCard: {
    backgroundColor: "#0F0F0F",
    borderRadius: 16,
    borderColor: "rgba(255,255,255,0.08)",
    borderWidth: 1,
    padding: 16,
    marginBottom: 10,
  },
  cekNo: {
    color: "#FFF",
    fontSize: 13,
    fontWeight: "bold",
  },
  cekDurumBadge: {
    paddingHorizontal: 10,
    paddingVertical: 3,
    borderRadius: 999,
    borderWidth: 1,
  },
  cekDurumText: {
    fontSize: 11,
    fontWeight: "900",
  },
  cekCari: {
    color: "#94A3B8",
    fontSize: 12,
    marginVertical: 4,
  },
  cekDetail: {
    color: "#64748B",
    fontSize: 11,
  },
  cekAmt: {
    color: "#00FF87",
    fontSize: 14,
    fontWeight: "900",
    marginTop: 8,
    textAlign: "right",
  },
  modalOverlay: {
    flex: 1,
    backgroundColor: "rgba(0, 0, 0, 0.85)",
  },
  modalContent: {
    flex: 1,
    backgroundColor: "#0A0A0A",
    padding: 20,
  },
  modalHeader: {
    flexDirection: "row",
    justifyContent: "space-between",
    alignItems: "center",
    marginBottom: 20,
    borderBottomWidth: 1,
    borderBottomColor: "rgba(255,255,255,0.08)",
    paddingBottom: 16,
  },
  modalTitle: {
    fontSize: 18,
    fontWeight: "bold",
    color: "#FFF",
  },
  label: {
    color: "#94A3B8",
    fontSize: 11,
    fontWeight: "bold",
    marginTop: 14,
    marginBottom: 6,
    letterSpacing: 0.5,
  },
  segmentRow: {
    flexDirection: "row",
    justifyContent: "space-between",
    backgroundColor: "#2A2A2A",
    borderRadius: 12,
    padding: 4,
  },
  segmentBtn: {
    flex: 1,
    paddingVertical: 10,
    alignItems: "center",
    borderRadius: 8,
  },
  segmentBtnActive: {
    backgroundColor: "#0061FF",
  },
  segmentBtnText: {
    color: "#64748B",
    fontSize: 11,
    fontWeight: "bold",
  },
  segmentBtnTextActive: {
    color: "#FFF",
  },
  selector: {
    backgroundColor: "#2A2A2A",
    borderColor: "#444",
    borderWidth: 1,
    borderRadius: 12,
    padding: 14,
  },
  selectorText: {
    color: "#FFF",
    fontSize: 14,
  },
  accountRow: {
    flexDirection: "row",
    flexWrap: "wrap",
    gap: 6,
  },
  accBtn: {
    backgroundColor: "#2A2A2A",
    borderWidth: 1,
    borderColor: "#444",
    paddingHorizontal: 12,
    paddingVertical: 8,
    borderRadius: 10,
  },
  accBtnActive: {
    backgroundColor: "#0061FF",
    borderColor: "#0061FF",
  },
  accBtnText: {
    color: "#FFF",
    fontSize: 11,
    fontWeight: "bold",
  },
  input: {
    backgroundColor: "#2A2A2A",
    borderColor: "#444",
    borderWidth: 1,
    borderRadius: 12,
    color: "#FFF",
    padding: 12,
    fontSize: 14,
    marginTop: 4,
  },
  saveButton: {
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "center",
    backgroundColor: "#10B981",
    padding: 16,
    borderRadius: 12,
    marginTop: 24,
  },
  saveButtonText: {
    color: "#FFF",
    fontWeight: "bold",
    fontSize: 14,
    marginLeft: 8,
  },
  searchBox: {
    flexDirection: "row",
    alignItems: "center",
    backgroundColor: "#2A2A2A",
    borderRadius: 12,
    borderWidth: 1,
    borderColor: "#444",
    paddingHorizontal: 12,
  },
  searchInput: {
    flex: 1,
    paddingVertical: 12,
    paddingHorizontal: 8,
    color: "#FFFFFF",
    fontSize: 15,
  },
  selectorItem: {
    backgroundColor: "#161616",
    padding: 16,
    borderRadius: 12,
    marginBottom: 8,
    borderWidth: 1,
    borderColor: "rgba(255,255,255,0.08)",
  },
  selectorItemText: {
    color: "#FFF",
    fontSize: 15,
    fontWeight: "bold",
  },
  selectorItemSub: {
    color: "#64748B",
    fontSize: 12,
    marginTop: 4,
  },
  extraBox: {
    backgroundColor: "#161616",
    borderRadius: 16,
    padding: 14,
    marginVertical: 10,
    borderWidth: 1,
    borderColor: "rgba(255,255,255,0.08)",
  },
  extraTitle: {
    color: "#FFF",
    fontSize: 12,
    fontWeight: "bold",
    marginBottom: 8,
  },
  summaryBox: {
    backgroundColor: "#161616",
    borderColor: "rgba(255,255,255,0.08)",
    borderWidth: 1,
    borderRadius: 20,
    padding: 16,
    marginBottom: 16,
  },
  summaryRow: {
    flexDirection: "row",
    justifyContent: "space-between",
    marginBottom: 10,
  },
  summaryLbl: {
    color: "#94A3B8",
    fontSize: 12,
  },
  summaryVal: {
    color: "#FFF",
    fontSize: 12,
    fontWeight: "bold",
  },
  progressContainer: {
    height: 6,
    backgroundColor: "rgba(255,255,255,0.05)",
    borderRadius: 3,
    marginVertical: 10,
    overflow: "hidden",
  },
  progressBar: {
    height: "100%",
    backgroundColor: "#00FF87",
  },
  progressTxt: {
    color: "#64748B",
    fontSize: 11,
    fontWeight: "bold",
    textAlign: "right",
  },
  tahminRow: {
    flexDirection: "row",
    justifyContent: "space-between",
    alignItems: "center",
    paddingVertical: 10,
    borderBottomWidth: 1,
    borderBottomColor: "rgba(255,255,255,0.05)",
  },
  tahminTitle: {
    color: "#FFF",
    fontSize: 12,
    fontWeight: "bold",
  },
  tahminSub: {
    color: "#64748B",
    fontSize: 10,
    marginTop: 2,
  },
  tahminAmt: {
    fontSize: 12,
    fontWeight: "900",
  },
  emptyTxt: {
    color: "#64748B",
    fontSize: 11,
    textAlign: "center",
    paddingVertical: 16,
  },
  absoluteOverlay: {
    position: "absolute",
    top: 0,
    left: 0,
    right: 0,
    bottom: 0,
    backgroundColor: "#0A0A0A",
    zIndex: 99,
    padding: 20,
  },
  pickImageBtn: {
    backgroundColor: "#0061FF",
    padding: 12,
    borderRadius: 12,
    justifyContent: "center",
    alignItems: "center",
    marginTop: 4,
  },
  miniSelectCard: {
    backgroundColor: "#2A2A2A",
    paddingHorizontal: 12,
    paddingVertical: 8,
    borderRadius: 8,
    marginRight: 8,
    borderWidth: 1,
    borderColor: "#444",
  },
  miniSelectCardActive: {
    backgroundColor: "#0061FF",
    borderColor: "#0061FF",
  },
  miniSelectText: {
    color: "#94A3B8",
    fontSize: 12,
    fontWeight: "600",
  },
  miniSelectTextActive: {
    color: "#FFF",
  },
  hesapRow: {
    flexDirection: "row",
    alignItems: "center",
    backgroundColor: "#161616",
    borderWidth: 1,
    borderColor: "rgba(255,255,255,0.08)",
    borderRadius: 12,
    padding: 12,
    marginBottom: 8,
  },
  hesapAdi: {
    color: "#FFF",
    fontSize: 13,
    fontWeight: "700",
  },
  hesapSub: {
    color: "#64748B",
    fontSize: 11,
    marginTop: 2,
  },
  hesapBakiye: {
    color: "#00FF87",
    fontSize: 13,
    fontWeight: "bold",
    marginRight: 6,
  },
  periodRow: {
    flexDirection: "row",
    flexWrap: "wrap",
    gap: 6,
    marginBottom: 12,
  },
  periodBtn: {
    backgroundColor: "#2A2A2A",
    borderWidth: 1,
    borderColor: "#444",
    paddingHorizontal: 12,
    paddingVertical: 7,
    borderRadius: 9,
  },
  periodBtnActive: {
    backgroundColor: "rgba(0,212,255,0.15)",
    borderColor: "#00D4FF",
  },
  periodBtnText: {
    color: "#94A3B8",
    fontSize: 11,
    fontWeight: "700",
  },
  periodBtnTextActive: {
    color: "#00D4FF",
  },
  trendLegend: {
    flexDirection: "row",
    justifyContent: "center",
    flexWrap: "wrap",
    gap: 12,
    marginTop: 8,
  },
  trendLegendItem: {
    flexDirection: "row",
    alignItems: "center",
    gap: 5,
  },
  trendDot: {
    width: 8,
    height: 8,
    borderRadius: 4,
  },
  trendLegendText: {
    color: "#64748B",
    fontSize: 10,
    fontWeight: "600",
  },
  quickStatusBtn: {
    borderWidth: 1,
    paddingHorizontal: 10,
    paddingVertical: 6,
    borderRadius: 8,
  },
  cekFooter: {
    flexDirection: "row",
    justifyContent: "space-between",
    alignItems: "center",
    backgroundColor: "#161616",
    borderWidth: 1,
    borderColor: "rgba(255,255,255,0.08)",
    borderRadius: 12,
    padding: 14,
    marginTop: 12,
  },
});
