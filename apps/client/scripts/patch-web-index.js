const fs = require("node:fs");
const path = require("node:path");

const DIST_INDEX = path.join(__dirname, "..", "dist", "index.html");
const META_DESCRIPTION =
    "URFU Link — рабочее пространство для чатов, дисциплин, уведомлений и звонков студентов и преподавателей УрФУ.";
const THEME_COLOR = "#080D1D";

const metaDescriptionTag = `<meta name="description" content="${META_DESCRIPTION}" />`;
const themeColorTag = `<meta name="theme-color" content="${THEME_COLOR}" />`;

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
    META_DESCRIPTION,
    patchIndexHtml,
    patchDistIndex,
};
