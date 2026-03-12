import { Stack } from "expo-router";

import { AppProviders } from "../src/providers/app-providers";

export default function RootLayout() {
  return (
    <AppProviders>
      <Stack
        screenOptions={{
          headerShown: false,
          contentStyle: {
            backgroundColor: "#020617"
          }
        }}
      />
    </AppProviders>
  );
}
