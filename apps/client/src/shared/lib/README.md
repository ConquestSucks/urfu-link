# Конфиг и bootstrap

Всё, что касается runtime- и build-time конфига, живёт здесь.

**`env.ts`** — типы `AppEnv` (dev/prod), `DEFAULT_API_URLS`, zod-схемы. Используется в `config.ts`. Чтобы Expo мог подхватить конфиг без резолва из `src/`, те же значения продублированы в `app.config.ts`.

**`config.ts`** — то, что читает приложение в рантайме: `Constants.expoConfig.extra` (натив/build) и `window.__APP_CONFIG__` (web). Валидация через zod, при невалидных данных — throw. В коде приложения импортируй `appConfig` отсюда.

**Build-time:** в `app.config.ts` читаются `process.env.APP_ENV`, `process.env.EXPO_PUBLIC_API_URL`, проверяются и уходят в `extra`.

**Web:** в `app/+html.tsx` подгружается `/app-config.js`; в контейнере `docker-entrypoint.sh` пишет его из env. `config.ts` мержит и валидирует.

Не читай `process.env` или `window.__APP_CONFIG__` напрямую — только через этот слой, т.е. импорт из `../lib/config` или `@/lib/config`.
