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

        RuleFor(x => x.MimeType)
            .NotEmpty()
            .MaximumLength(MediaConstraints.MaxMimeTypeLength)
            .Must(mt => MimeTypeCatalog.TryResolve(mt, out _))
            .WithMessage("Mime type is not in the white-list.");

        RuleFor(x => x)
            .Custom((req, context) =>
            {
                if (!MimeTypeCatalog.TryResolve(req.MimeType, out var kind)) return;
                var max = limits.For(kind).MaxSizeBytes;
                if (req.Size > max)
                {
                    context.AddFailure(
                        nameof(req.Size),
                        $"File size {req.Size} exceeds the limit {max} for kind {kind}.");
                }
            });
    }
}
