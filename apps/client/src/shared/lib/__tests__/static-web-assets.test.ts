import { existsSync, readFileSync } from "node:fs";
import path from "node:path";

const {
    META_DESCRIPTION,
    patchIndexHtml,
} = require("../../../../scripts/patch-web-index.js") as {
    META_DESCRIPTION: string;
    patchIndexHtml: (html: string) => string;
};

const readClientFile = (...segments: string[]) =>
    readFileSync(path.join(process.cwd(), ...segments), "utf8");

describe("static web assets", () => {
    it("ships a valid robots.txt instead of falling back to the SPA shell", () => {
        const robotsPath = path.join(process.cwd(), "public", "robots.txt");

        expect(existsSync(robotsPath)).toBe(true);
        expect(readClientFile("public", "robots.txt")).toBe("User-agent: *\nAllow: /\n");
    });

    it("declares a Lighthouse-visible meta description in the root HTML shell", () => {
        const html = readClientFile("app", "+html.tsx");

        expect(html).toMatch(/<meta\s+name="description"/);
        expect(html).toContain(META_DESCRIPTION);
    });

    it("patches the SPA export shell with Lighthouse-visible metadata", () => {
        const html = [
            '<!DOCTYPE html><html lang="en"><head>',
            '<meta name="viewport" content="width=device-width" />',
            "</head><body></body></html>",
        ].join("");
        const patched = patchIndexHtml(html);

        expect(patched).toContain('<html lang="ru">');
        expect(patched).toContain(`<meta name="description" content="${META_DESCRIPTION}" />`);
        expect(patched).toContain('<meta name="theme-color" content="#080D1D" />');
        expect(patchIndexHtml(patched)).toBe(patched);
    });

    it("runs the index shell patch after Expo export", () => {
        const packageJson = readClientFile("package.json");

        expect(packageJson).toContain("node scripts/patch-web-index.js");
    });

    it("keeps Expo export in SPA mode because the app is not SSR-safe yet", () => {
        const appConfig = readClientFile("app.config.ts");

        expect(appConfig).toContain('output: "single"');
    });

    it("injects runtime config as a deferred script before the Expo bundle", () => {
        const entrypoint = readClientFile("docker-entrypoint.sh");

        expect(entrypoint).toContain(
            '<script src="/app-config.js" defer></script><script src="/_expo/static/js/',
        );
    });
});
