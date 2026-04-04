using UserService.Api.Infrastructure.Devices;

namespace UserService.UnitTests.Infrastructure;

public sealed class DeviceNameParserTests
{
    // Chrome UA Reduction replaces device model with "K" on Android
    [Theory]
    [InlineData("Mozilla/5.0 (Linux; Android 10; K) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/146.0.0.0 Mobile Safari/537.36")]
    [InlineData("Mozilla/5.0 (Linux; Android 13; K) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Mobile Safari/537.36")]
    public void ParseDeviceNameWithChromeUaReductionShouldNotReturnPlaceholderModel(string userAgent)
    {
        var result = RedisDeviceRegistry.ParseDeviceName(userAgent);

        Assert.DoesNotContain("K,", result, StringComparison.Ordinal);
        Assert.Contains("Android", result, StringComparison.Ordinal);
    }

    [Fact]
    public void ParseDeviceNameWithKnownAndroidModelShouldReturnModelAndOs()
    {
        const string ua = "Mozilla/5.0 (Linux; Android 10; SM-G975F) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/100.0.4896.127 Mobile Safari/537.36";

        var result = RedisDeviceRegistry.ParseDeviceName(ua);

        Assert.Contains("Samsung", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Android", result, StringComparison.Ordinal);
    }

    [Fact]
    public void ParseDeviceNameWithDesktopChromeShouldReturnBrowserAndOs()
    {
        const string ua = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

        var result = RedisDeviceRegistry.ParseDeviceName(ua);

        Assert.StartsWith("Chrome", result, StringComparison.Ordinal);
        Assert.Contains("Windows", result, StringComparison.Ordinal);
    }

    [Fact]
    public void ParseDeviceNameWithEmptyStringShouldReturnUnknown()
    {
        var result = RedisDeviceRegistry.ParseDeviceName(string.Empty);

        Assert.Equal("Неизвестное устройство", result);
    }
}
