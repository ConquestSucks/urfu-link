# ADR 0005: Chat GET endpoints use EndpointWithoutRequest pattern

- Status: Accepted
- Date: 2026-05-17

## Контекст

В Chat-сервисе у GET-эндпоинтов раньше был базовый класс
`Endpoint<TRequest, TResponse>`, где `TRequest` содержал поля с атрибутами
`[QueryParam]` и `[RouteParam]`. После обновления FastEndpoints поведение
для GET изменилось: фреймворк пытается JSON-десериализовать `Request.Body`
до запуска биндинга query-параметров и возвращает 415/400, даже если у
GET-запроса тела нет.

FluentValidation `Validator<TRequest>` опирается на тот же биндинг, поэтому
валидация по этим же причинам срабатывает не всегда корректно.

## Решение

Все Chat GET-эндпоинты переезжают на `EndpointWithoutRequest<TResponse>`.
Параметры читаются вручную внутри `HandleAsync`:

```csharp
var q = Query<string?>("q", isRequired: false);
var conversationId = Query<string?>("conversationId", isRequired: false);
var limit = Query<int?>("limit", isRequired: false);
```

Валидация переносится inline через `AddError(field, message)`
с финальным `ThrowIfAnyErrors(StatusCodes.Status400BadRequest)`. Это
эквивалент прежнего FluentValidation, но выполняется уже после успешного
биндинга и не зависит от попыток FE прочитать тело.

Применимо к:

- `GetConversationEndpoint`
- `GetConversationParticipantsEndpoint`
- `ListConversationsEndpoint`
- `GetConversationMessagesEndpoint`
- `GetReadReceiptsEndpoint`
- `GetThreadMessagesEndpoint`
- `SearchMessagesEndpoint` (включая удаление `SearchMessagesValidator`)
- `GetActiveThreadsEndpoint`

## Альтернативы (отвергнуты)

- **`RequestBinder` для отключения чтения body.** Внутренний хук FE,
  хрупкий и плохо документирован. Любое обновление мажора FastEndpoints
  его сломает.
- **Откатить FastEndpoints до версии, где `Endpoint<TReq, TResp>`
  работал для GET.** Откат закрепляет уязвимости и блокирует доступ к
  фичам новых версий.

## Последствия

- (+) GET-эндпоинты работают предсказуемо и не зависят от наличия тела запроса.
- (+) Меньше DTO-классов (параметры выражены позиционно).
- (−) FluentValidation Validator не подхватывается — валидация
  становится дисциплиной разработчика. В POST/PUT-эндпоинтах
  паттерн остаётся прежним.
- (−) OpenAPI/Swagger описание query-параметров теряет автоматическую
  разметку через `[QueryParam]`. Нужны явные `Summary(s => s.Params[...])`,
  если требуется красивый Swagger.
- (−) Для каждого нового GET-эндпоинта нужно повторить шаблон вручную
  (Query/Route + AddError + ThrowIfAnyErrors). Помогает code review.

## Дальнейшее

Завести follow-up issue "OpenAPI Swagger description for migrated GET endpoints"
и постепенно добавить `Summary(s => s.Params[...])` для критичных эндпоинтов
(в первую очередь `SearchMessagesEndpoint`).
