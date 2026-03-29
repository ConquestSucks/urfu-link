using FastEndpoints;
using UserService.Api.Application.Contracts.Responses;
using UserService.Api.Domain;
using UserService.Api.Domain.Interfaces;
using UserService.Api.Infrastructure.Auth;

namespace UserService.Api.Endpoints;

public sealed class UploadAvatarRequest
{
    public IFormFile File { get; set; } = null!;
}

public sealed class UploadAvatarEndpoint(
    IUserRepository userRepository,
    IAvatarStorage avatarStorage) : Endpoint<UploadAvatarRequest, AvatarUploadResponse>
{
    private static readonly HashSet<string> AllowedContentTypes =
    [
        "image/jpeg",
        "image/png",
        "image/webp",
    ];

    private const long MaxFileSize = 5 * 1024 * 1024;

    public override void Configure()
    {
        Put("/me/avatar");
        AllowFileUploads();
        Summary(s => s.Summary = "Upload or replace avatar");
    }

    public override async Task HandleAsync(UploadAvatarRequest req, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);
        if (req.File is null || req.File.Length == 0)
        {
            AddError("File is required.");
            await HttpContext.Response.SendErrorsAsync(ValidationFailures, cancellation: ct).ConfigureAwait(false);
            return;
        }

        if (req.File.Length > MaxFileSize)
        {
            AddError("File size must not exceed 5 MB.");
            await HttpContext.Response.SendErrorsAsync(ValidationFailures, cancellation: ct).ConfigureAwait(false);
            return;
        }

        if (!AllowedContentTypes.Contains(req.File.ContentType))
        {
            AddError("Only JPEG, PNG, and WebP images are allowed.");
            await HttpContext.Response.SendErrorsAsync(ValidationFailures, cancellation: ct).ConfigureAwait(false);
            return;
        }

        var userId = HttpContext.User.GetUserId();
        var user = await userRepository.GetByIdAsync(userId, ct).ConfigureAwait(false);

        if (user is null)
        {
            user = UserProfile.CreateDefault(userId);
            userRepository.Add(user);
        }

        if (user.Account.AvatarUrl is not null)
        {
            await avatarStorage.DeleteAsync(user.Account.AvatarUrl, ct).ConfigureAwait(false);
        }

        using var stream = req.File.OpenReadStream();
        var avatarUrl = await avatarStorage.UploadAsync(userId, stream, req.File.ContentType, ct).ConfigureAwait(false);

        user.UploadAvatar(avatarUrl);
        await userRepository.SaveChangesAsync(ct).ConfigureAwait(false);

        await HttpContext.Response.SendAsync(new AvatarUploadResponse(avatarUrl), cancellation: ct).ConfigureAwait(false);
    }
}
