import React, { useEffect, useRef, useState } from 'react';
import {
  View,
  Text,
  StyleSheet,
  Animated,
  Platform,
  Keyboard,
  LayoutChangeEvent,
  PanResponder,
} from 'react-native';
import { BottomTabBarProps } from '@react-navigation/bottom-tabs';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import * as Haptics from 'expo-haptics';
import { Home, Users, FileText, Package, LayoutGrid } from 'lucide-react-native';
import { BlurView } from 'expo-blur';

export default function FloatingTabBar({
  state,
  descriptors,
  navigation,
}: BottomTabBarProps) {
  const insets = useSafeAreaInsets();
  const [containerWidth, setContainerWidth] = useState<number>(0);
  const [isKeyboardVisible, setIsKeyboardVisible] = useState(false);
  const [activeHoverIndex, setActiveHoverIndex] = useState<number>(state.index);

  const containerWidthRef = useRef<number>(0);
  const stateRef = useRef(state);
  stateRef.current = state;
  const navigationRef = useRef(navigation);
  navigationRef.current = navigation;
  const hoverIndexRef = useRef<number>(state.index);
  hoverIndexRef.current = activeHoverIndex;

  const slideAnim = useRef(new Animated.Value(0)).current;
  const scaleAnim = useRef(new Animated.Value(1)).current;

  // Klavye açıldığında barı gizle
  useEffect(() => {
    const showEvent = Platform.OS === 'ios' ? 'keyboardWillShow' : 'keyboardDidShow';
    const hideEvent = Platform.OS === 'ios' ? 'keyboardWillHide' : 'keyboardDidHide';

    const showSub = Keyboard.addListener(showEvent, () => setIsKeyboardVisible(true));
    const hideSub = Keyboard.addListener(hideEvent, () => setIsKeyboardVisible(false));

    return () => {
      showSub.remove();
      hideSub.remove();
    };
  }, []);

  const totalTabs = state.routes.length;
  const slotWidth = containerWidth > 0 ? containerWidth / totalTabs : 0;
  const pillWidth = Math.max(slotWidth - 8, 48);
  const pillOffset = slotWidth > 0 ? (slotWidth - pillWidth) / 2 : 0;

  // Sekme değiştiğinde kapsülü animasyonla hedef sekmeye kaydır
  useEffect(() => {
    setActiveHoverIndex(state.index);
    if (slotWidth > 0) {
      Animated.spring(slideAnim, {
        toValue: state.index * slotWidth + pillOffset,
        useNativeDriver: true,
        tension: 80,
        friction: 11,
      }).start();
    }
  }, [state.index, slotWidth, pillOffset]);

  // Parmakla basılı tutup menüler arasında kaydırma (Pan / Drag Scrubbing) mekanizması
  const panResponder = useRef(
    PanResponder.create({
      onStartShouldSetPanResponder: () => true,
      onMoveShouldSetPanResponder: () => true,

      onPanResponderGrant: (evt) => {
        const width = containerWidthRef.current;
        if (width <= 0) return;
        const currentSlotWidth = width / stateRef.current.routes.length;
        const currentPillWidth = Math.max(currentSlotWidth - 8, 48);
        const currentPillOffset = (currentSlotWidth - currentPillWidth) / 2;

        const touchX = evt.nativeEvent.locationX;
        const targetIndex = Math.min(
          Math.max(Math.floor(touchX / currentSlotWidth), 0),
          stateRef.current.routes.length - 1
        );

        setActiveHoverIndex(targetIndex);
        Haptics.impactAsync(Haptics.ImpactFeedbackStyle.Light).catch(() => {});

        // Basma anında hafif içe yaylanma
        Animated.parallel([
          Animated.spring(scaleAnim, {
            toValue: 0.94,
            useNativeDriver: true,
            speed: 50,
            bounciness: 4,
          }),
          Animated.spring(slideAnim, {
            toValue: targetIndex * currentSlotWidth + currentPillOffset,
            useNativeDriver: true,
            tension: 130,
            friction: 12,
          }),
        ]).start();
      },

      onPanResponderMove: (evt) => {
        const width = containerWidthRef.current;
        if (width <= 0) return;
        const currentSlotWidth = width / stateRef.current.routes.length;
        const currentPillWidth = Math.max(currentSlotWidth - 8, 48);
        const currentPillOffset = (currentSlotWidth - currentPillWidth) / 2;

        const touchX = evt.nativeEvent.locationX;
        const targetIndex = Math.min(
          Math.max(Math.floor(touchX / currentSlotWidth), 0),
          stateRef.current.routes.length - 1
        );

        // Parmağın bulunduğu sekme değiştikçe anlık haptic ve pürüzsüz kayma
        if (targetIndex !== hoverIndexRef.current) {
          setActiveHoverIndex(targetIndex);
          Haptics.selectionAsync().catch(() => {});

          Animated.spring(slideAnim, {
            toValue: targetIndex * currentSlotWidth + currentPillOffset,
            useNativeDriver: true,
            tension: 140,
            friction: 12,
          }).start();
        }
      },

      onPanResponderRelease: (evt) => {
        const width = containerWidthRef.current;
        const finalIndex = hoverIndexRef.current;

        Animated.spring(scaleAnim, {
          toValue: 1,
          useNativeDriver: true,
          speed: 40,
          bounciness: 8,
        }).start();

        if (width > 0) {
          const currentSlotWidth = width / stateRef.current.routes.length;
          const currentPillWidth = Math.max(currentSlotWidth - 8, 48);
          const currentPillOffset = (currentSlotWidth - currentPillWidth) / 2;

          Animated.spring(slideAnim, {
            toValue: finalIndex * currentSlotWidth + currentPillOffset,
            useNativeDriver: true,
            tension: 90,
            friction: 10,
          }).start();
        }

        const currentState = stateRef.current;
        const currentNav = navigationRef.current;
        const targetRoute = currentState.routes[finalIndex];

        if (targetRoute) {
          const isFocused = currentState.index === finalIndex;
          Haptics.impactAsync(Haptics.ImpactFeedbackStyle.Medium).catch(() => {});

          const event = currentNav.emit({
            type: 'tabPress',
            target: targetRoute.key,
            canPreventDefault: true,
          });

          if (!isFocused && !event.defaultPrevented) {
            currentNav.navigate(targetRoute.name, targetRoute.params);
          }
        }
      },

      onPanResponderTerminate: () => {
        Animated.spring(scaleAnim, {
          toValue: 1,
          useNativeDriver: true,
        }).start();

        // İptal olursa mevcut aktif sekmeye geri dön
        setActiveHoverIndex(stateRef.current.index);
        const width = containerWidthRef.current;
        if (width > 0) {
          const currentSlotWidth = width / stateRef.current.routes.length;
          const currentPillWidth = Math.max(currentSlotWidth - 8, 48);
          const currentPillOffset = (currentSlotWidth - currentPillWidth) / 2;

          Animated.spring(slideAnim, {
            toValue: stateRef.current.index * currentSlotWidth + currentPillOffset,
            useNativeDriver: true,
            tension: 80,
            friction: 10,
          }).start();
        }
      },
    })
  ).current;

  // Alt menünün gizlenmesi istenen ekran kontrolü (tabBarStyle display: none)
  const currentDescriptor = descriptors[state.routes[state.index].key];
  const tabBarStyle = currentDescriptor?.options?.tabBarStyle as any;
  if (tabBarStyle?.display === 'none' || isKeyboardVisible) {
    return null;
  }

  const handleContainerLayout = (e: LayoutChangeEvent) => {
    const width = e.nativeEvent.layout.width;
    if (width > 0 && width !== containerWidth) {
      containerWidthRef.current = width;
      setContainerWidth(width);
      const currentSlotWidth = width / totalTabs;
      const currentPillWidth = Math.max(currentSlotWidth - 8, 48);
      const currentPillOffset = (currentSlotWidth - currentPillWidth) / 2;
      slideAnim.setValue(state.index * currentSlotWidth + currentPillOffset);
    }
  };

  const bottomOffset = Platform.OS === 'ios'
    ? (insets.bottom > 0 ? insets.bottom + 4 : 16)
    : 16;

  const renderIcon = (routeName: string, isHighlighted: boolean) => {
    const iconColor = isHighlighted ? '#FFFFFF' : '#8E95A2';
    const strokeWidth = isHighlighted ? 2.4 : 1.9;
    const size = 20;

    switch (routeName) {
      case 'Dashboard':
        return (
          <Home
            size={size}
            color={iconColor}
            fill={isHighlighted ? '#FFFFFF' : 'none'}
            strokeWidth={isHighlighted ? 1.5 : 1.9}
          />
        );
      case 'Cariler':
        return <Users size={size} color={iconColor} strokeWidth={strokeWidth} />;
      case 'Faturalar':
        return <FileText size={size} color={iconColor} strokeWidth={strokeWidth} />;
      case 'Stoklar':
        return <Package size={size} color={iconColor} strokeWidth={strokeWidth} />;
      case 'DahaFazla':
        return <LayoutGrid size={size} color={iconColor} strokeWidth={strokeWidth} />;
      default:
        return <Home size={size} color={iconColor} strokeWidth={strokeWidth} />;
    }
  };

  return (
    <View
      pointerEvents="box-none"
      style={[styles.outerContainer, { bottom: bottomOffset }]}
    >
      <View
        style={styles.pillContainer}
        onLayout={handleContainerLayout}
        {...panResponder.panHandlers}
      >
        {/* iOS Frosted Glass Effect */}
        <BlurView intensity={Platform.OS === 'ios' ? 70 : 100} tint="dark" style={StyleSheet.absoluteFill} />

        {/* Floating Active Pill Sliding Highlight */}
        {slotWidth > 0 && (
          <Animated.View
            style={[
              styles.activePillIndicator,
              {
                width: pillWidth,
                transform: [
                  { translateX: slideAnim },
                  { scale: scaleAnim },
                ],
              },
            ]}
          />
        )}

        {/* Tab Items with Icons & Labels */}
        <View style={styles.tabsRow} pointerEvents="none">
          {state.routes.map((route, index) => {
            const isHighlighted = activeHoverIndex === index;
            const { options } = descriptors[route.key];
            const label =
              options.tabBarLabel !== undefined
                ? String(options.tabBarLabel)
                : options.title !== undefined
                ? options.title
                : route.name;

            return (
              <View key={route.key} style={styles.tabItem}>
                <View style={styles.iconWrapper}>
                  {renderIcon(route.name, isHighlighted)}
                </View>
                <Text
                  style={[
                    styles.tabLabel,
                    isHighlighted ? styles.tabLabelActive : styles.tabLabelInactive,
                  ]}
                  numberOfLines={1}
                >
                  {label}
                </Text>
              </View>
            );
          })}
        </View>
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  outerContainer: {
    position: 'absolute',
    left: 14,
    right: 14,
    alignItems: 'center',
    zIndex: 9999,
  },
  pillContainer: {
    width: '100%',
    height: 68,
    borderRadius: 36,
    backgroundColor: 'rgba(18, 18, 22, 0.90)',
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.12)',
    overflow: 'hidden',
    justifyContent: 'center',
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 10 },
    shadowOpacity: 0.52,
    shadowRadius: 18,
    elevation: 12,
  },
  activePillIndicator: {
    position: 'absolute',
    height: 52,
    borderRadius: 26,
    backgroundColor: '#2A2C33',
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.10)',
    top: 7,
  },
  tabsRow: {
    flexDirection: 'row',
    alignItems: 'center',
    height: '100%',
    width: '100%',
  },
  tabItem: {
    flex: 1,
    height: '100%',
    alignItems: 'center',
    justifyContent: 'center',
    paddingHorizontal: 2,
  },
  iconWrapper: {
    alignItems: 'center',
    justifyContent: 'center',
    height: 24,
    marginBottom: 3,
  },
  tabLabel: {
    fontSize: 10,
    letterSpacing: -0.2,
    textAlign: 'center',
  },
  tabLabelActive: {
    color: '#FFFFFF',
    fontWeight: '700',
  },
  tabLabelInactive: {
    color: '#8E95A2',
    fontWeight: '500',
  },
});
