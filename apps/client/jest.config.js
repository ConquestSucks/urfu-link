const expoPreset = require("jest-expo/jest-preset");
const sourceTransform = expoPreset.transform["\\.[jt]sx?$"];
const transformModules = [
    ".pnpm",
    "react-native",
    "@react-native",
    "@react-native-community",
    "expo",
    "@expo",
    "@expo-google-fonts",
    "react-navigation",
    "@react-navigation",
    "@sentry/react-native",
    "native-base",
    "msw",
    "@mswjs",
    "@open-draft",
    "rettime",
    "headers-polyfill",
    "outvariant",
    "strict-event-emitter",
    "until-async",
    "is-node-process",
].join("|");

module.exports = {
    ...expoPreset,
    transform: {
        ...expoPreset.transform,
        "^.+\\.mjs$": sourceTransform,
    },
    transformIgnorePatterns: [
        `/node_modules/(?!(${transformModules}))`,
        "/node_modules/react-native-reanimated/plugin/",
        "/node_modules/@react-native/babel-preset/",
    ],
    setupFilesAfterEnv: [
        ...(expoPreset.setupFilesAfterEnv ?? []),
        "<rootDir>/jest.setup.ts",
    ],
    testPathIgnorePatterns: [
        ...(expoPreset.testPathIgnorePatterns ?? []),
        "/dist/",
        "/.expo/",
    ],
    moduleNameMapper: {
        ...(expoPreset.moduleNameMapper ?? {}),
        "^msw/node$": "<rootDir>/node_modules/msw/lib/node/index.js",
        "^@/(.*)$": "<rootDir>/src/$1",
    },
};
