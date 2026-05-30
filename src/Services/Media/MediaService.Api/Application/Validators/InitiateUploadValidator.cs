using FastEndpoints;
using FluentValidation;
using MediaService.Api.Application.Contracts.Requests;
using MediaService.Api.Application.Limits;
using Microsoft.Extensions.Options;

namespace MediaService.Api.Application.Validators;

public sealed class InitiateUploadValidator : Validator<InitiateUploadRequest>
{
    public InitiateUploadValidator(IOptions<MediaLimitsOptions> limitsOptions)
    {
        ArgumentNullException.ThrowIfNull(limitsOptions);
        var limits = limitsOptions.Value;

        RuleFor(x => x.FileName)
            .NotEmpty()
            .MaximumLength(MediaConstraints.MaxFileNameLength);

        RuleFor(x => x.Size)
            .GreaterThan(0).WithMessage("Size must be positive.");

        RuleFor(x => x.RequestedKind)
            .Must(k => k is null || MimeTypeCatalog.IsSupportedRequestedKind(k.Value))
            .WithMessage("Requested kind is not supported for explicit upload classification.");

        RuleFor(x => x.MimeType)
            .NotEmpty()
            .MaximumLength(MediaConstraints.MaxMimeTypeLength)
            .Must((req, mt) => MimeTypeCatalog.TryResolve(mt, req.RequestedKind, out _))
            .WithMessage("Mime type is not in the white-list.");

        RuleFor(x => x)
            .Custom((req, context) =>
            {
                if (!MimeTypeCatalog.TryResolve(req.MimeType, req.RequestedKind, out var kind)) return;
                var kindLimit = limits.For(kind);

                if (req.Size > kindLimit.MaxSizeBytes)
                {
                    context.AddFailure(
                        nameof(req.Size),
                        $"File size {req.Size} exceeds the limit {kindLimit.MaxSizeBytes} for kind {kind}.");
                }

                // Voice (and any future kind with MaxDurationSeconds set) requires the
                // recorder to declare the recording length. The size cap alone allows a
                // 30-minute high-bitrate recording to slip through (EPIC #206 caps voice
                // at 5 minutes specifically because long voice messages are a chat anti-pattern).
                if (kindLimit.MaxDurationSeconds is int maxDuration)
                {
                    if (req.DurationSeconds is null)
                    {
                        context.AddFailure(
                            nameof(req.DurationSeconds),
                            $"DurationSeconds is required for {kind}.");
                    }
                    else if (req.DurationSeconds < 1)
                    {
                        context.AddFailure(
                            nameof(req.DurationSeconds),
                            "DurationSeconds must be positive.");
                    }
                    else if (req.DurationSeconds > maxDuration)
                    {
                        context.AddFailure(
                            nameof(req.DurationSeconds),
                            $"Duration {req.DurationSeconds}s exceeds the {kind} limit of {maxDuration}s.");
                    }
                }
            });
    }
}
