import { AppProviders } from "@/providers";
import { Stack } from "expo-router";
import { StatusBar } from "expo-status-bar";
import "../global.css";

export default function RootLayout() {
    return (<AppProviders>
      <StatusBar style="light"/>
      <Stack>
        <Stack.Screen name="index" options={{ headerShown: false }}/>
        <Stack.Screen name="(authorized)" options={{ headerShown: false }}/>
      </Stack>
    </AppProviders>);
}
