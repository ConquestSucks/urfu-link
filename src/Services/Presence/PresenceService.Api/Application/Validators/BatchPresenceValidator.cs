using FastEndpoints;
using FluentValidation;
using Urfu.Link.Services.Presence.Application.Contracts.Requests;

namespace Urfu.Link.Services.Presence.Application.Validators;

public sealed class BatchPresenceValidator : Validator<BatchPresenceRequest>
{
    public BatchPresenceValidator()
    {
        RuleFor(x => x.UserIds).NotNull();
        RuleFor(x => x.UserIds.Length)
            .InclusiveBetween(1, BatchPresenceRequest.MaxUserIds)
            .WithMessage($"userIds must contain between 1 and {BatchPresenceRequest.MaxUserIds} items");
    }
}
