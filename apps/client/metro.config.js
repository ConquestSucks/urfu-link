const { getDefaultConfig } = require("expo/metro-config");
const { withNativeWind } = require("nativewind/metro");
const config = getDefaultConfig(__dirname);
config.resolver.alias = {
    '@': './src',
};

// zustand v5 ships ESM (.mjs) files with `import.meta` which Metro cannot
// parse in a non-module bundle. Force CJS resolution by clearing isESMImport
// so Metro picks the "require" condition instead of "import".
config.resolver.resolveRequest = (context, moduleName, platform) => {
    if (moduleName === 'zustand' || moduleName.startsWith('zustand/')) {
        return context.resolveRequest(
            { ...context, isESMImport: false },
            moduleName,
            platform,
        );
    }
    return context.resolveRequest(context, moduleName, platform);
};

module.exports = withNativeWind(config, { input: "./global.css" });
