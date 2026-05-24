using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using MediaService.Api.Infrastructure.Storage;

namespace MediaService.UnitTests.Unit;

public class StorageOptionsValidationTests
{
    [Theory]
    [InlineData(nameof(StorageOptions.Endpoint))]
    [InlineData(nameof(StorageOptions.AccessKey))]
    [InlineData(nameof(StorageOptions.SecretKey))]
    [InlineData(nameof(StorageOptions.PrivateBucket))]
    [InlineData(nameof(StorageOptions.PublicBucket))]
    public void EmptyRequiredField_FailsDataAnnotationsValidation(string propertyName)
    {
        var options = new StorageOptions
        {
            Endpoint = "http://localhost:9000",
            AccessKey = "ak",
            SecretKey = "sk",
            PrivateBucket = "p",
            PublicBucket = "pub",
        };
        typeof(StorageOptions).GetProperty(propertyName)!.SetValue(options, string.Empty);

        var context = new ValidationContext(options);
        var results = new List<ValidationResult>();
        var ok = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

        ok.Should().BeFalse(
            $"empty {propertyName} must fail data-annotations validation so .ValidateOnStart() in DependencyInjection rejects the host build");
        results.Should().Contain(r => r.MemberNames.Contains(propertyName));
    }
}
