using Contoso.Common.Core;
using Xunit;

namespace Contoso.Common.Core.Tests;

public class SeedDataLoaderTests
{
    private readonly SeedDataLoader _loader = new();

    [Fact]
    public void LoadCustomers_ReturnsThreeSeededCustomers()
    {
        var customers = _loader.LoadCustomers();
        Assert.Equal(3, customers.Count);
    }

    [Fact]
    public void LoadCustomers_IncludesEnterpriseTier()
    {
        var customers = _loader.LoadCustomers();
        Assert.Contains(customers, c => c.Tier == "Enterprise");
    }

    [Fact]
    public void LoadDevices_EachDeviceReferencesKnownCustomer()
    {
        var customers = _loader.LoadCustomers().Select(c => c.Id).ToHashSet();
        var devices = _loader.LoadDevices();

        Assert.All(devices, d => Assert.Contains(d.CustomerId, customers));
    }

    [Fact]
    public void LoadRegions_ExactlyOneIsDefaultForNewCustomers()
    {
        var regions = _loader.LoadRegions();
        Assert.Single(regions.Where(r => r.DefaultForNewCustomers));
    }

    [Fact]
    public void LoadCustomers_MissingFile_ThrowsFileNotFoundException()
    {
        var loader = new SeedDataLoader(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
        Assert.Throws<FileNotFoundException>(() => loader.LoadCustomers());
    }
}

public class ResultTests
{
    [Fact]
    public void Success_CarriesValue()
    {
        var result = Result<int>.Success(42);
        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Failure_CarriesError()
    {
        var result = Result<int>.Failure("bad input");
        Assert.False(result.IsSuccess);
        Assert.Equal("bad input", result.Error);
    }
}

public class SystemClockTests
{
    [Fact]
    public void UtcNow_IsCloseToRealTime()
    {
        var clock = new SystemClock();
        var delta = DateTimeOffset.UtcNow - clock.UtcNow;
        Assert.True(Math.Abs(delta.TotalSeconds) < 5);
    }
}
