# Конфиг и bootstrap

Всё, что касается runtime- и build-time конфига, живёт здесь.

**`env.ts`** — типы `AppEnv` (dev/prod), `DEFAULT_API_URLS`/`DEFAULT_KEYCLOAK_URLS` (только dev — localhost), zod-схемы. Используется в `config.ts`. Чтобы Expo мог подхватить конфиг без резолва из `src/`, те же значения продублированы в `app.config.ts`.

**`config.ts`** — то, что читает приложение в рантайме: `Constants.expoConfig.extra` (build-time) и `window.__APP_CONFIG__` (runtime override). Валидация через zod, при невалидных данных — throw. В коде приложения импортируй `appConfig` отсюда.

**Build-time:** в `app.config.ts` читаются `process.env.APP_ENV`, `process.env.EXPO_PUBLIC_API_URL`, `process.env.EXPO_PUBLIC_KEYCLOAK_URL`, проверяются и уходят в `extra`. Для `appEnv=dev` есть localhost-дефолты, для `prod` env-переменные обязательны (иначе throw на старте expo).

**Web prod:** в Docker-контейнере `docker-entrypoint.sh` инжектит `<script src="/app-config.js">` в `index.html` и пишет файл из container env — `window.__APP_CONFIG__` перебивает build-time `extra`. В dev файл `public/app-config.js` пустой и не мешает build-time конфигу.

Не читай `process.env` или `window.__APP_CONFIG__` напрямую — только через этот слой, т.е. импорт из `../lib/config` или `@/lib/config`.
