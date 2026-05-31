module.exports = function (api) {
    const isTest = api.env("test");

    return {
        presets: [
            ["babel-preset-expo", { jsxImportSource: "nativewind" }],
            "nativewind/babel",
        ],
        plugins: [
            isTest && "babel-plugin-dynamic-import-node",
        ].filter(Boolean),
    };
};
