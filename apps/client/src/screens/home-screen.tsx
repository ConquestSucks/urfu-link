import { StyleSheet, Text, View } from "react-native";
import { AppScreen } from "@urfu-link/ui";

export function HomeScreen() {
  return (
    <AppScreen>
      <View style={styles.container}>
        <View style={styles.content}>
          <View style={styles.badge}>
            <Text style={styles.badgeText}>URFU Link</Text>
          </View>

          <Text style={styles.heading}>Скоро здесь{"\n"}что-то будет</Text>

          <Text style={styles.subheading}>
            Мы работаем над чем-то новым.{"\n"}Следите за обновлениями.
          </Text>

          <View style={styles.divider} />

          <Text style={styles.footer}>urfu-link.ghjc.ru</Text>
        </View>
      </View>
    </AppScreen>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    alignItems: "center",
    justifyContent: "center",
    paddingHorizontal: 24
  },
  content: {
    alignItems: "center",
    gap: 20,
    maxWidth: 480,
    width: "100%"
  },
  badge: {
    borderWidth: 1,
    borderColor: "rgba(59, 130, 246, 0.4)",
    borderRadius: 100,
    paddingHorizontal: 14,
    paddingVertical: 5,
    backgroundColor: "rgba(59, 130, 246, 0.08)"
  },
  badgeText: {
    color: "#60a5fa",
    fontSize: 12,
    fontWeight: "600",
    letterSpacing: 1.5,
    textTransform: "uppercase"
  },
  heading: {
    color: "#f8fafc",
    fontSize: 48,
    fontWeight: "700",
    textAlign: "center",
    lineHeight: 60,
    letterSpacing: -0.5
  },
  subheading: {
    color: "#64748b",
    fontSize: 16,
    lineHeight: 26,
    textAlign: "center"
  },
  divider: {
    width: 40,
    height: 1,
    backgroundColor: "rgba(148, 163, 184, 0.2)",
    marginVertical: 4
  },
  footer: {
    color: "#334155",
    fontSize: 13,
    letterSpacing: 0.5
  }
});
