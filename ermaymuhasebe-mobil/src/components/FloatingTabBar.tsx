import React, { useEffect, useRef, useState } from 'react';
import {
  View,
  Text,
  TouchableOpacity,
  StyleSheet,
  Animated,
  Platform,
  Keyboard,
  LayoutChangeEvent,
} from 'react-native';
import { BottomTabBarProps } from '@react-navigation/bottom-tabs';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import * as Haptics from 'expo-haptics';
import { Home, Users, FileText, Package, LayoutGrid } from 'lucide-react-native';
import { BlurView } from 'expo-blur';

interface TabItemProps {
  routeName: string;
  isFocused: boolean;
  onPress: () => void;
  onLongPress: () => void;
  label: string;
}

const TabButton: React.FC<TabItemProps> = ({
  routeName,
  isFocused,
  onPress,
  onLongPress,
}) => {
  const scaleAnim = useRef(new Animated.Value(1)).current;

  const handlePressIn = () => {
    Animated.spring(scaleAnim, {
      toValue: 0.9,
      useNativeDriver: true,
      speed: 50,
      bounciness: 4,
    }).start();
  };

  const handlePressOut = () => {
    Animated.spring(scaleAnim, {
      toValue: 1,
      useNativeDriver: true,
      speed: 40,
      bounciness: 8,
    }).start();
  };

  const renderIcon = () => {
    const iconColor = isFocused ? '#FFFFFF' : '#8E95A2';
    const strokeWidth = isFocused ? 2.4 : 1.9;
    const size = 22;

    switch (routeName) {
      case 'Dashboard':
        return (
          <Home
            size={size}
            color={iconColor}
            fill={isFocused ? '#FFFFFF' : 'none'}
            strokeWidth={isFocused ? 1.5 : 1.9}
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
    <TouchableOpacity
      activeOpacity={0.8}
      onPress={onPress}
      onLongPress={onLongPress}
      onPressIn={handlePressIn}
      onPressOut={handlePressOut}
      style={styles.tabItem}
    >
      <Animated.View style={[styles.iconWrapper, { transform: [{ scale: scaleAnim }] }]}>
        {renderIcon()}
      </Animated.View>
    </TouchableOpacity>
  );
};

export default function FloatingTabBar({
  state,
  descriptors,
  navigation,
}: BottomTabBarProps) {
  const insets = useSafeAreaInsets();
  const [containerWidth, setContainerWidth] = useState<number>(0);
  const [isKeyboardVisible, setIsKeyboardVisible] = useState(false);
  const slideAnim = useRef(new Animated.Value(0)).current;

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
  const pillWidth = Math.max(slotWidth - 10, 48);
  const pillOffset = slotWidth > 0 ? (slotWidth - pillWidth) / 2 : 0;

  useEffect(() => {
    if (slotWidth > 0) {
      Animated.spring(slideAnim, {
        toValue: state.index * slotWidth + pillOffset,
        useNativeDriver: true,
        tension: 68,
        friction: 10,
      }).start();
    }
  }, [state.index, slotWidth, pillOffset]);

  // Alt menünün gizlenmesi istenen ekran kontrolü (tabBarStyle display: none)
  const currentDescriptor = descriptors[state.routes[state.index].key];
  const tabBarStyle = currentDescriptor?.options?.tabBarStyle as any;
  if (tabBarStyle?.display === 'none' || isKeyboardVisible) {
    return null;
  }

  const handleContainerLayout = (e: LayoutChangeEvent) => {
    const width = e.nativeEvent.layout.width;
    if (width > 0 && width !== containerWidth) {
      setContainerWidth(width);
      slideAnim.setValue(state.index * (width / totalTabs) + ((width / totalTabs - Math.max(width / totalTabs - 10, 48)) / 2));
    }
  };

  const bottomOffset = Platform.OS === 'ios'
    ? (insets.bottom > 0 ? insets.bottom + 4 : 16)
    : 16;

  return (
    <View
      pointerEvents="box-none"
      style={[styles.outerContainer, { bottom: bottomOffset }]}
    >
      <View
        style={styles.pillContainer}
        onLayout={handleContainerLayout}
      >
        {/* iOS Frosted Glass Effect */}
        <BlurView intensity={ Platform.OS === 'ios' ? 70 : 100 } tint="dark" style={StyleSheet.absoluteFill} />

        {/* Floating Active Pill Sliding Highlight */}
        {slotWidth > 0 && (
          <Animated.View
            style={[
              styles.activePillIndicator,
              {
                width: pillWidth,
                transform: [{ translateX: slideAnim }],
              },
            ]}
          />
        )}

        {/* Tab Buttons */}
        <View style={styles.tabsRow}>
          {state.routes.map((route, index) => {
            const isFocused = state.index === index;
            const { options } = descriptors[route.key];
            const label =
              options.tabBarLabel !== undefined
                ? String(options.tabBarLabel)
                : options.title !== undefined
                ? options.title
                : route.name;

            const onPress = () => {
              Haptics.impactAsync(Haptics.ImpactFeedbackStyle.Light).catch(() => {});

              const event = navigation.emit({
                type: 'tabPress',
                target: route.key,
                canPreventDefault: true,
              });

              if (!isFocused && !event.defaultPrevented) {
                navigation.navigate(route.name, route.params);
              }
            };

            const onLongPress = () => {
              navigation.emit({
                type: 'tabLongPress',
                target: route.key,
              });
            };

            return (
              <TabButton
                key={route.key}
                routeName={route.name}
                isFocused={isFocused}
                onPress={onPress}
                onLongPress={onLongPress}
                label={label}
              />
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
    left: 18,
    right: 18,
    alignItems: 'center',
    zIndex: 9999,
  },
  pillContainer: {
    width: '100%',
    height: 64,
    borderRadius: 36,
    backgroundColor: 'rgba(18, 18, 22, 0.88)',
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.12)',
    overflow: 'hidden',
    justifyContent: 'center',
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 10 },
    shadowOpacity: 0.5,
    shadowRadius: 18,
    elevation: 12,
  },
  activePillIndicator: {
    position: 'absolute',
    height: 48,
    borderRadius: 24,
    backgroundColor: '#2A2C33',
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.09)',
    top: 8,
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
  },
  iconWrapper: {
    alignItems: 'center',
    justifyContent: 'center',
    width: 44,
    height: 44,
  },
});
