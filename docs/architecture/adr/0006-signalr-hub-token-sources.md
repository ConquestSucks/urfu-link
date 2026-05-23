# ADR 0006: SignalR hub negotiate accepts both query and Authorization header

- Status: Accepted
- Date: 2026-05-17

## Контекст

API Gateway (`HubAccessTokenPresenceMiddleware`) до этой правки принимал
JWT-токен для `/hubs/*` только через query-параметр `?access_token=`.
SignalR JavaScript-клиент передаёт токен по-разному в зависимости от фазы:

- HTTP-фаза `/hubs/<name>/negotiate` — `Authorization: Bearer <token>` (header).
- WebSocket-апгрейд — `?access_token=<token>` в query (браузеры не дают
  выставить header при WebSocket-апгрейде).

В итоге negotiate проходил только если клиент сам копировал токен в query
перед запросом. Это нестандартное поведение, и оно ломалось при использовании
дефолтной конфигурации `withUrl(url, { accessTokenFactory })`.

## Решение

Middleware принимает токен из обоих источников **до** JWT pipeline:

```csharp
var hasQuery = !string.IsNullOrWhiteSpace(context.Request.Query["access_token"]);
var hasBearer = HasBearerAuthorizationHeader(context.Request);
if (!hasQuery && !hasBearer)
{
    context.Response.Headers[HeaderNames.WWWAuthenticate] = "Bearer";
    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
    return;
}
```

`HasBearerAuthorizationHeader` парсит `Authorization` через
`AuthenticationHeaderValue.TryParse` и сравнивает scheme case-insensitive.

Контракт зафиксирован интеграционным тестом
`HubRoute_WithBearerHeader_ProxiesToDownstream` в `ApiGateway.Tests`.

## Альтернативы (отвергнуты)

- **Поправить только клиент, чтобы всегда копировал токен в query.**
  Это идёт против дефолтного поведения SignalR JS-клиента и любой новый
  клиент (RN, iOS, .NET) встретит ту же ловушку.

## Последствия

- (+) `/hubs/*` работает с дефолтной конфигурацией SignalR клиента
  без обходов.
- (+) `withAutomaticReconnect()` ведёт себя предсказуемо: на HTTP-фазе
  используется header, на WS-фазе — query.
- (−) Middleware немного сложнее: добавлен метод `HasBearerAuthorizationHeader`
  с парсингом scheme. Покрыто тестом.

## Дальнейшее

Если в будущем потребуется ограничить, какие именно hub-маршруты принимают
header (например, требовать только query для определённых пут), это можно
сделать через дополнительный filter на route-level — middleware остаётся
permissive по умолчанию.
