using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using ChatService.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Urfu.Link.Services.Chat.Application.Conversations;
using Urfu.Link.Services.Chat.Application.Messages;
using Urfu.Link.Services.Chat.Domain.Enums;
using Urfu.Link.Services.Chat.Infrastructure.Persistence;
using Urfu.Link.Services.Chat.Infrastructure.Persistence.Documents;

namespace ChatService.IntegrationTests.Endpoints;

/// <summary>
/// Performance acceptance for issue #213: full-text search must keep p95 latency under 500ms
/// on a 100k-message dataset. Tests in this class are tagged Performance and excluded from
/// the default test run via <c>--filter "Category!=Performance"</c>.
/// </summary>
[Collection(IntegrationCollection.Name)]
[Trait("Category", "Performance")]
[SuppressMessage("Security", "CA5394:Do not use insecure randomness", Justification = "Deterministic test data seeding, not a security boundary.")]
public class SearchPerformanceTests : IAsyncLifetime
{
    private const int TotalMessages = 100_000;
    private const int ConversationsCount = 50;
    private const int MessagesPerConversation = TotalMessages / ConversationsCount;
    private const int Iterations = 100;
    private const int P95BudgetMs = 500;

    private static readonly string[] Vocabulary =
    {
        "квантовая", "теория", "поле", "оплата", "зачёт", "лекция", "семинар",
        "лабораторная", "курсовая", "проект", "дедлайн", "экзамен", "сессия",
        "формула", "интеграл", "матрица", "вектор", "энтропия", "функция",
        "график", "точка", "прямая", "плоскость", "симметрия",
    };

    // The needle term: appears in every Nth message so search has plenty of hits to rank.
    private const string SearchTerm = "иголка";
    private const int NeedleEvery = 10;

    private readonly ChatServiceFactory _factory;

    public SearchPerformanceTests(ChatServiceFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.ResetDataAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Search_On100kMessages_KeepsP95UnderBudget()
    {
        var caller = Guid.NewGuid();

        var conversationIds = await SeedDataAsync(caller);
        conversationIds.Should().HaveCount(ConversationsCount);

        // Bypass the HTTP layer (and the per-user rate limiter) and call the Application query
        // directly. The hot path under measurement is Mongo aggregate + cursor encoding + DTO
        // hydration — exactly what we want to keep under the 500ms p95 budget. The 30/min rate
        // limiter is a fixed orthogonal overhead and is exercised by SearchEndpointsTests.
        await using var scope = _factory.Services.CreateAsyncScope();
        var query = scope.ServiceProvider.GetRequiredService<SearchMessagesQuery>();
        var parameters = new SearchMessagesParameters(SearchTerm, null, null, null, null, null, null, null, Limit: 20);

        // Warm-up: a couple of calls so JIT / connection pools / index cache are hot.
        for (var i = 0; i < 3; i++)
        {
            _ = await query.ExecuteAsync(parameters, caller, default);
        }

        var latencies = new List<long>(Iterations);
        for (var i = 0; i < Iterations; i++)
        {
            var sw = Stopwatch.StartNew();
            var page = await query.ExecuteAsync(parameters, caller, default);
            sw.Stop();
            latencies.Add(sw.ElapsedMilliseconds);
            page.Items.Should().NotBeEmpty();
        }

        latencies.Sort();
        var p95 = latencies[(int)Math.Floor(0.95 * latencies.Count) - 1];
        var p50 = latencies[latencies.Count / 2];

        // Surface the timings even when the assertion passes — they show up in the test runner
        // output and make regressions visible at a glance.
        Console.WriteLine(
            $"Search perf: p50 = {p50}ms, p95 = {p95}ms, max = {latencies[^1]}ms over {Iterations} iterations on {TotalMessages} docs.");

        p95.Should().BeLessThan(
            P95BudgetMs,
            $"acceptance criterion (#213) requires p95 < {P95BudgetMs}ms; observed p50={p50}ms, p95={p95}ms");
    }

    private async Task<List<string>> SeedDataAsync(Guid caller)
    {
        var conversationIds = new List<string>(ConversationsCount);

        // Open ConversationsCount direct chats — caller in each.
        await using (var seedScope = _factory.Services.CreateAsyncScope())
        {
            var open = seedScope.ServiceProvider.GetRequiredService<OpenDirectConversationService>();
            for (var i = 0; i < ConversationsCount; i++)
            {
                var conv = await open.OpenAsync(caller, Guid.NewGuid(), default);
                conversationIds.Add(conv.Id);
            }
        }

        // Bulk-insert messages directly into Mongo. Going through SendMessageService for 100k
        // documents would dominate the test runtime; the search path doesn't care how the rows
        // got there as long as the schema is right.
        var ctx = _factory.Services.GetRequiredService<ChatMongoContext>();

        var rnd = new Random(42);
        var anchor = DateTime.UtcNow.AddDays(-7);
        var globalIndex = 0;
        const int batchSize = 5_000;

        foreach (var convId in conversationIds)
        {
            var docs = new List<MessageDocument>(MessagesPerConversation);
            for (var i = 0; i < MessagesPerConversation; i++)
            {
                var words = new List<string>(8);
                var len = 5 + rnd.Next(8);
                for (var w = 0; w < len; w++)
                {
                    words.Add(Vocabulary[rnd.Next(Vocabulary.Length)]);
                }
                if (globalIndex % NeedleEvery == 0)
                {
                    words.Insert(rnd.Next(words.Count), SearchTerm);
                }

                docs.Add(new MessageDocument
                {
                    Id = Guid.NewGuid(),
                    ConversationId = convId,
                    SenderId = caller,
                    Body = string.Join(' ', words),
                    State = MessageState.Sent,
                    ClientMessageId = $"perf-{globalIndex}",
                    CreatedAtUtc = anchor.AddSeconds(globalIndex),
                });

                if (docs.Count >= batchSize)
                {
                    await ctx.Messages.InsertManyAsync(docs);
                    docs.Clear();
                }
                globalIndex++;
            }

            if (docs.Count > 0)
            {
                await ctx.Messages.InsertManyAsync(docs);
            }
        }

        return conversationIds;
    }
}
