using FluentAssertions;
using MediaService.Api.Grpc;
using DomainEnums = MediaService.Api.Domain.Enums;

namespace MediaService.UnitTests.Unit;

public class AssetStateGrpcMappingTests
{
    [Theory]
    [InlineData(DomainEnums.AssetState.Initiated, AssetState.Initiated)]
    [InlineData(DomainEnums.AssetState.Uploaded, AssetState.Uploaded)]
    [InlineData(DomainEnums.AssetState.Deleted, AssetState.Deleted)]
    [InlineData(DomainEnums.AssetState.HardDeleted, AssetState.HardDeleted)]
    [InlineData(DomainEnums.AssetState.Failed, AssetState.Failed)]
    public void EveryDomainStateMapsToMatchingProtoMember(
        DomainEnums.AssetState domain, AssetState expectedProto)
    {
        ((int)domain).Should().Be((int)expectedProto,
            "domain and proto enums must keep numeric values in lockstep");

        Enum.IsDefined(typeof(AssetState), (int)domain)
            .Should().BeTrue(
                $"every domain AssetState ({domain}) must have a matching media.internal.v1.AssetState member; if you add a new domain state, add the same member to internal.proto");
    }
}
