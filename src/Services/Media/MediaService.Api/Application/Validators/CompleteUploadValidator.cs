using FastEndpoints;
using FluentValidation;
using MediaService.Api.Application.Contracts.Requests;

namespace MediaService.Api.Application.Validators;

public sealed class CompleteUploadValidator : Validator<CompleteUploadRequest>
{
    public CompleteUploadValidator()
    {
        RuleFor(x => x.AssetId).NotEmpty();
        RuleFor(x => x.Checksum)
            .MaximumLength(128)
            .When(x => x.Checksum is not null);
    }
}
