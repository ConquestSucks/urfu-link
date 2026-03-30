import { StyleSheet, Text, View } from "react-native";
import { AppScreen } from "@urfu-link/ui";
export function HomeScreen() {
    return (<AppScreen>
      <View style={styles.container}>
        <View style={styles.content}>
          <View className="border border-effects-brandOverlay40 rounded-full px-3.5 py-[5px] bg-effects-brandOverlay08">
            <Text style={styles.badgeText} className="text-brand-300">URFU Link</Text>
          </View>

          <Text style={styles.heading} className="text-slate-50">Скоро здесь{"\n"}что-то будет</Text>

          <Text style={styles.subheading} className="text-slate-500">
            Мы работаем над чем-то новым.{"\n"}Следите за обновлениями.
          </Text>

          <View style={styles.divider} className="bg-effects-slateOverlay20"/>

          <Text style={styles.footer} className="text-slate-700">urfu-link.ghjc.ru</Text>
        </View>
      </View>
    </AppScreen>);
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
    badgeText: {
        fontSize: 12,
        fontWeight: "600",
        letterSpacing: 1.5,
        textTransform: "uppercase"
    },
    heading: {
        fontSize: 48,
        fontWeight: "700",
        textAlign: "center",
        lineHeight: 60,
        letterSpacing: -0.5
    },
    subheading: {
        fontSize: 16,
        lineHeight: 26,
        textAlign: "center"
    },
    divider: {
        width: 40,
        height: 1,
        marginVertical: 4
    },
    footer: {
        fontSize: 13,
        letterSpacing: 0.5
    }
});
