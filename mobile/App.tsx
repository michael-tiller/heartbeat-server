// mobile/HeartbeatMobile/App.tsx
import React, { useCallback, useEffect, useState } from "react";
import { StyleSheet, Text, TouchableOpacity, View } from "react-native";
import { SafeAreaView } from "react-native-safe-area-context";
import { apiClient } from "./src/api/client";
import { getDeviceId } from "./src/utils/deviceId";

export default function App(): React.JSX.Element {
  const [status, setStatus] = useState<string>("(initializing...)");
  const [details, setDetails] = useState<string>("");
  const [pairCode, setPairCode] = useState<string>("");
  const [deviceId, setDeviceId] = useState<string>("");
  const [currentStreak, setCurrentStreak] = useState<number | null>(null);
  const [longestStreak, setLongestStreak] = useState<number | null>(null);

  // Register on app startup
  useEffect(() => {
    const initializeApp = async () => {
      try {
        setStatus("(getting device ID...)");
        const id = await getDeviceId();
        setDeviceId(id);

        setStatus("(registering...)");
        const response = await apiClient.register({ deviceId: id });
        setPairCode(response.pairCode ?? "");
        setCurrentStreak(response.currentStreak ?? null);
        setLongestStreak(response.longestStreak ?? null);
        setStatus("(registered)");
        setDetails(`Device ID: ${id}\nPair Code: ${response.pairCode ?? ""}`);
      } catch (e: any) {
        setStatus("(registration failed)");
        setDetails(e?.message ?? String(e));
      }
    };

    initializeApp();
  }, []);

  const onCheckHealth = useCallback(async () => {
    setDetails("");
    setStatus("(checking...)");

    try {
      const response = await apiClient.checkHealth();
      setStatus(response.status ?? "(missing status)");
    } catch (e: any) {
      setStatus("(failed)");
      setDetails(e?.message ?? String(e));
    }
  }, []);

  return (
    <SafeAreaView style={styles.root}>
      <View style={styles.card}>
        <Text style={styles.title}>Heartbeat</Text>

        <Text style={styles.label}>Status</Text>
        <Text style={styles.value}>{status}</Text>

        {pairCode.length > 0 && (
          <>
            <Text style={styles.label}>Pair Code</Text>
            <Text style={styles.value}>{pairCode}</Text>
          </>
        )}

        {currentStreak !== null && (
          <>
            <Text style={styles.label}>Current Streak</Text>
            <Text style={styles.value}>{currentStreak} days</Text>
          </>
        )}

        {longestStreak !== null && (
          <>
            <Text style={styles.label}>Longest Streak</Text>
            <Text style={styles.value}>{longestStreak} days</Text>
          </>
        )}

        {details.length > 0 && (
          <>
            <Text style={styles.label}>Details</Text>
            <Text style={styles.mono}>{details}</Text>
          </>
        )}

        <TouchableOpacity style={styles.button} onPress={onCheckHealth}>
          <Text style={styles.buttonText}>Check /health</Text>
        </TouchableOpacity>
      </View>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  root: { flex: 1, backgroundColor: "#0b0b0b", justifyContent: "center" },
  card: { margin: 16, padding: 16, borderRadius: 12, backgroundColor: "#141414" },
  title: { fontSize: 24, fontWeight: "700", color: "white", marginBottom: 12 },
  label: { color: "#aaa", marginTop: 8 },
  value: { color: "white", fontSize: 18, marginTop: 4 },
  mono: { color: "#ddd", fontFamily: "monospace", marginTop: 4 },
  button: { marginTop: 12, paddingVertical: 12, borderRadius: 10, backgroundColor: "#2a2a2a", alignItems: "center" },
  buttonText: { color: "white", fontWeight: "600" },
});
