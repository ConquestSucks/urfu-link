import type { PropsWithChildren } from "react";
import { ScrollViewStyleReset } from "expo-router/html";

const APP_THEME_BG = "#080D1D";

export default function Root({ children }: PropsWithChildren) {
    return (
        <html lang="ru" style={{ backgroundColor: APP_THEME_BG }}>
            <head>
                <meta charSet="utf-8" />
                <meta httpEquiv="X-UA-Compatible" content="IE=edge" />
                <meta
                    name="viewport"
                    content="width=device-width, initial-scale=1, shrink-to-fit=no, viewport-fit=cover"
                />
                <meta name="theme-color" content={APP_THEME_BG} />
                <meta name="color-scheme" content="dark" />
                <title>URFU Link</title>
                <ScrollViewStyleReset />
            </head>
            <body style={{ backgroundColor: APP_THEME_BG, minHeight: "100%" }}>{children}</body>
        </html>
    );
}
