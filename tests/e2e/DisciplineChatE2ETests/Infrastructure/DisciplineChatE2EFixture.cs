namespace DisciplineChatE2ETests.Infrastructure;

public sealed class DisciplineChatE2EFixture : IAsyncLifetime
{
    public DisciplineServiceE2EFactory Discipline { get; } = new();

    public ChatServiceE2EFactory Chat { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await Discipline.InitializeAsync();
        _ = Discipline.CreateClient();

        Chat = new ChatServiceE2EFactory(Discipline);
        await Chat.InitializeAsync();
        _ = Chat.CreateClient();
    }

    public async Task DisposeAsync()
    {
        if (Chat is not null)
        {
            await Chat.DisposeAsync();
        }

        await Discipline.DisposeAsync();
    }

    public async Task ResetAsync()
    {
        await Chat.ResetDataAsync();
        await Discipline.ResetDataAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class DisciplineChatE2ECollection : ICollectionFixture<DisciplineChatE2EFixture>
{
    public const string Name = "discipline-chat-e2e";
}
