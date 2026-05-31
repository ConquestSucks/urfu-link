const fs = require("node:fs");
const path = require("node:path");

const DIST_INDEX = path.join(__dirname, "..", "dist", "index.html");
const META_DESCRIPTION =
    "URFU Link — рабочее пространство для чатов, дисциплин, уведомлений и звонков студентов и преподавателей УрФУ.";
const THEME_COLOR = "#080D1D";
const BOOT_SHELL_STYLE_ID = "app-boot-shell-style";

const metaDescriptionTag = `<meta name="description" content="${META_DESCRIPTION}" />`;
const themeColorTag = `<meta name="theme-color" content="${THEME_COLOR}" />`;
const bootShellStyleTag = `<style id="${BOOT_SHELL_STYLE_ID}">
html,
body {
  background: ${THEME_COLOR};
}

#root:not(:empty) ~ .app-boot-shell {
  display: none;
}

.app-boot-shell {
  position: fixed;
  inset: 0;
  z-index: 1;
  display: flex;
  min-height: 100vh;
  overflow: hidden;
  background: ${THEME_COLOR};
  color: #fff;
  font-family: Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
  pointer-events: none;
}

.app-boot-sidebar {
  display: flex;
  width: 288px;
  flex-shrink: 0;
  flex-direction: column;
  gap: 24px;
  padding: 18px 16px;
  background: #080d1d;
}

.app-boot-logo {
  display: flex;
  align-items: center;
  gap: 10px;
  font-size: 13px;
  font-weight: 700;
  letter-spacing: 0;
}

.app-boot-logo-mark {
  width: 28px;
  height: 28px;
  border-radius: 8px;
  background: #2563eb;
}

.app-boot-nav {
  display: grid;
  gap: 10px;
}

.app-boot-nav-row,
.app-boot-profile,
.app-boot-item,
.app-boot-message,
.app-boot-input {
  border: 1px solid rgba(255, 255, 255, 0.06);
  background: rgba(255, 255, 255, 0.035);
}

.app-boot-nav-row {
  height: 42px;
  border-radius: 8px;
}

.app-boot-profile {
  margin-top: auto;
  height: 52px;
  border-radius: 8px;
}

.app-boot-master {
  width: 342px;
  flex-shrink: 0;
  border-left: 1px solid rgba(255, 255, 255, 0.04);
  border-right: 1px solid rgba(255, 255, 255, 0.06);
  background: #0a1023;
  padding: 16px 14px;
}

.app-boot-search {
  height: 42px;
  margin-bottom: 16px;
  border-radius: 8px;
  background: rgba(255, 255, 255, 0.06);
}

.app-boot-items {
  display: grid;
  gap: 10px;
}

.app-boot-item {
  height: 68px;
  border-radius: 8px;
}

.app-boot-detail {
  display: flex;
  min-width: 0;
  flex: 1;
  flex-direction: column;
  background: #090f21;
}

.app-boot-header {
  height: 56px;
  flex-shrink: 0;
  border-bottom: 1px solid rgba(255, 255, 255, 0.06);
  background: rgba(255, 255, 255, 0.02);
}

.app-boot-messages {
  display: flex;
  flex: 1;
  flex-direction: column;
  justify-content: flex-end;
  gap: 16px;
  padding: 24px;
}

.app-boot-message {
  height: 48px;
  max-width: 460px;
  border-radius: 12px;
}

.app-boot-message:nth-child(2),
.app-boot-message:nth-child(5) {
  width: 38%;
  margin-left: auto;
  background: rgba(37, 99, 235, 0.20);
}

.app-boot-message:nth-child(1),
.app-boot-message:nth-child(4) {
  width: 52%;
}

.app-boot-message:nth-child(3) {
  width: 44%;
}

.app-boot-input {
  height: 56px;
  flex-shrink: 0;
  margin: 0 18px 18px;
  border-radius: 8px;
  background: rgba(255, 255, 255, 0.045);
}

@media (max-width: 767px) {
  .app-boot-sidebar,
  .app-boot-master {
    display: none;
  }

  .app-boot-header {
    height: 60px;
  }

  .app-boot-messages {
    padding: 18px;
  }
}
</style>`;
const bootShellHtml = `<div class="app-boot-shell" aria-hidden="true">
  <aside class="app-boot-sidebar">
    <div class="app-boot-logo">
      <span class="app-boot-logo-mark"></span>
      <span>URFU Link</span>
    </div>
    <nav class="app-boot-nav">
      <div class="app-boot-nav-row"></div>
      <div class="app-boot-nav-row"></div>
      <div class="app-boot-nav-row"></div>
    </nav>
    <div class="app-boot-profile"></div>
  </aside>
  <section class="app-boot-master">
    <div class="app-boot-search"></div>
    <div class="app-boot-items">
      <div class="app-boot-item"></div>
      <div class="app-boot-item"></div>
      <div class="app-boot-item"></div>
      <div class="app-boot-item"></div>
      <div class="app-boot-item"></div>
    </div>
  </section>
  <main class="app-boot-detail">
    <div class="app-boot-header"></div>
    <div class="app-boot-messages">
      <div class="app-boot-message"></div>
      <div class="app-boot-message"></div>
      <div class="app-boot-message"></div>
      <div class="app-boot-message"></div>
      <div class="app-boot-message"></div>
    </div>
    <div class="app-boot-input"></div>
  </main>
</div>`;

const patchIndexHtml = (html) => {
    let next = html;

    if (/<html\b[^>]*\blang=/i.test(next)) {
        next = next.replace(/<html\b([^>]*)\blang="[^"]*"([^>]*)>/i, '<html$1lang="ru"$2>');
    } else {
        next = next.replace(/<html\b([^>]*)>/i, '<html$1 lang="ru">');
    }

    if (!/<meta\s+name=["']description["']/i.test(next)) {
        next = next.replace(
            /(<meta\s+name=["']viewport["'][^>]*>\s*)/i,
            `$1\n    ${metaDescriptionTag}`,
        );
    }

    if (!/<meta\s+name=["']theme-color["']/i.test(next)) {
        next = next.replace(
            /(<meta\s+name=["']description["'][^>]*>\s*)/i,
            `$1\n    ${themeColorTag}`,
        );
    }

    if (!next.includes(`id="${BOOT_SHELL_STYLE_ID}"`)) {
        next = next.replace(/<\/head>/i, `  ${bootShellStyleTag}</head>`);
    }

    if (!/class=["']app-boot-shell["']/i.test(next)) {
        next = next.replace(/<\/body>/i, `${bootShellHtml}\n</body>`);
    }

    return next;
};

const patchDistIndex = (indexPath = DIST_INDEX) => {
    const html = fs.readFileSync(indexPath, "utf8");
    const patched = patchIndexHtml(html);

    if (patched !== html) {
        fs.writeFileSync(indexPath, patched, "utf8");
    }
};

if (require.main === module) {
    patchDistIndex();
}

module.exports = {
    BOOT_SHELL_STYLE_ID,
    META_DESCRIPTION,
    patchIndexHtml,
    patchDistIndex,
};
