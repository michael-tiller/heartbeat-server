// Utility to get or generate a persistent device ID
import AsyncStorage from '@react-native-async-storage/async-storage';

const DEVICE_ID_KEY = '@heartbeat_device_id';

/**
 * Generates a simple UUID-like string
 */
function generateDeviceId(): string {
  // Generate a simple UUID v4-like string
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
    const r = (Math.random() * 16) | 0;
    const v = c === 'x' ? r : (r & 0x3) | 0x8;
    return v.toString(16);
  });
}

/**
 * Gets the device ID from storage, or generates and stores a new one if it doesn't exist
 */
export async function getDeviceId(): Promise<string> {
  try {
    let deviceId = await AsyncStorage.getItem(DEVICE_ID_KEY);

    if (!deviceId) {
      deviceId = generateDeviceId();
      await AsyncStorage.setItem(DEVICE_ID_KEY, deviceId);
    }

    return deviceId;
  } catch (error) {
    // If storage fails, generate a temporary ID (won't persist across restarts)
    console.warn('Failed to access AsyncStorage, using temporary device ID:', error);
    return generateDeviceId();
  }
}

