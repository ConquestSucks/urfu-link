namespace MediaService.Api.Domain.Enums;

/// <summary>
/// Determines whether an asset can be downloaded by anyone authenticated (Public)
/// or only by owner / explicitly granted users (Private).
/// </summary>
public enum Visibility
{
    Private = 0,
    Public = 1,
}
