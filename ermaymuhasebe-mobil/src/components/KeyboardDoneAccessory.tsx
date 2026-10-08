import React from 'react';
import { View, Text, TouchableOpacity, StyleSheet, Platform, InputAccessoryView, Keyboard } from 'react-native';

export const KEYBOARD_ACCESSORY_ID = 'appNumericDoneBar';

interface KeyboardDoneAccessoryProps {
  nativeID?: string;
  onDone?: () => void;
}

export const KeyboardDoneAccessory: React.FC<KeyboardDoneAccessoryProps> = ({
  nativeID = KEYBOARD_ACCESSORY_ID,
  onDone,
}) => {
  if (Platform.OS !== 'ios') return null;

  const handleDone = () => {
    Keyboard.dismiss();
    if (onDone) onDone();
  };

  return (
    <InputAccessoryView nativeID={nativeID}>
      <View style={styles.accessoryContainer}>
        <View style={{ flex: 1 }} />
        <TouchableOpacity 
          style={styles.doneButton} 
          onPress={handleDone}
          activeOpacity={0.7}
          hitSlop={{ top: 10, bottom: 10, left: 16, right: 16 }}
        >
          <Text style={styles.doneText}>Bitti</Text>
        </TouchableOpacity>
      </View>
    </InputAccessoryView>
  );
};

const styles = StyleSheet.create({
  accessoryContainer: {
    height: 44,
    backgroundColor: '#1E1E22',
    borderTopWidth: StyleSheet.hairlineWidth,
    borderTopColor: 'rgba(255, 255, 255, 0.15)',
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'flex-end',
    paddingHorizontal: 16,
  },
  doneButton: {
    paddingVertical: 6,
    paddingHorizontal: 14,
    borderRadius: 8,
    backgroundColor: '#0061FF',
  },
  doneText: {
    color: '#FFFFFF',
    fontSize: 14,
    fontWeight: '700',
  },
});

export default KeyboardDoneAccessory;
