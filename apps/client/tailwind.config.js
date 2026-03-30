module.exports = {
  darkMode: "class",
  content: [
    "./app/**/*.{js,jsx,ts,tsx}",
    "./src/**/*.{js,jsx,ts,tsx}",
  ],
  presets: [require("nativewind/preset")],
  theme: {
    extend: {
      colors: {
        brand: {
          300: "#60A5FA",
          400: "#51A2FF",
          450: "#3D8BFF",
          500: "#3B82F6",
          600: "#2B7FFF",
          650: "#2563EB",
          700: "#1A6EEB",
        },
        app: {
          bg: "#080D1D",
          card: "#0B1225",
          panel: "#0F172B",
          elevated: "#1A1D27",
        },
        text: {
          primary: "#FFFFFF",
          secondary: "#CAD5E2",
          muted: "#90A1B9",
          subtle: "#8B8FA8",
          placeholder: "#62748E",
          disabled: "#45556C",
        },
        success: {
          500: "#00D492",
          600: "#00BC7D",
        },
        warning: {
          500: "#FFB900",
          600: "#FF8904",
        },
        danger: {
          300: "#FF637E",
          400: "#FF6467",
          500: "#FB2C36",
          600: "#EF4444",
        },
        slate: {
          50: "#F8FAFC",
          100: "#F8FAFC",
          300: "#CBD5E1",
          400: "#94A3B8",
          500: "#64748B",
          600: "#45556C",
          700: "#334155",
          800: "#1D293D",
          850: "#0F172B",
        },
        zinc: {
          900: "#111827",
        },
        neutral: {
          0: "#000",
        },
        effects: {
          white05: "rgba(255,255,255,0.05)",
          brand20: "rgba(43,127,255,0.2)",
          black10: "rgba(0,0,0,0.1)",
          black50: "rgba(0,0,0,0.5)",
          brandOverlay40: "rgba(59, 130, 246, 0.4)",
          brandOverlay08: "rgba(59, 130, 246, 0.08)",
          slateOverlay20: "rgba(148, 163, 184, 0.2)",
        },
      },
    },
  },
  plugins: [
    function ({ addUtilities, theme }) {
      addUtilities({
        ".stop-brand-500": { stopColor: theme("colors.brand.500") },
        ".stop-brand-300": { stopColor: theme("colors.brand.300") },
      });
    },
  ],
};
