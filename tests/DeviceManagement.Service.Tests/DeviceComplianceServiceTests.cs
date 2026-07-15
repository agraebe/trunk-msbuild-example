using Contoso.Common.Core;
using Contoso.Common.FeatureFlags;
using Contoso.DeviceManagement.Service;
using Xunit;

namespace Contoso.DeviceManagement.Service.Tests;

public class DeviceComplianceServiceTests
{
    private static DeviceComplianceService CreateService() =>
        new(new JsonFileFeatureFlagProvider(), new SeedDataLoader());

    [Fact]
    public void GetDevice_KnownDevice_ReturnsSuccess()
    {
        var service = CreateService();
        var result = service.GetDevice("dev-1001");
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void GetDevice_UnknownDevice_ReturnsFailure()
    {
        var service = CreateService();
        var result = service.GetDevice("dev-9999");
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void IsEligibleForOnboarding_NonCompliantDevice_ReturnsFalse()
    {
        var service = CreateService();
        var device = service.GetDevice("dev-1003").Value!;
        Assert.False(service.IsEligibleForOnboarding(device));
    }

    [Fact]
    public void IsEligibleForOnboarding_CompliantWindowsDevice_ReturnsTrue()
    {
        var service = CreateService();
        var device = service.GetDevice("dev-1001").Value!;
        Assert.True(service.IsEligibleForOnboarding(device));
    }

    [Fact]
    public void IsEligibleForOnboarding_CompliantLinuxDeviceUnderNewFlow_ReturnsFalse()
    {
        // dev-1004 is compliant but runs Linux; the new onboarding flow (enabled
        // globally in flags.json) only allows Windows/macOS.
        var service = CreateService();
        var device = service.GetDevice("dev-1004").Value!;
        Assert.False(service.IsEligibleForOnboarding(device));
    }

    [Fact]
    public void ListForCustomer_ReturnsOnlyMatchingDevices()
    {
        var service = CreateService();
        var devices = service.ListForCustomer("cust-001");
        Assert.All(devices, d => Assert.Equal("cust-001", d.CustomerId));
        Assert.Equal(2, devices.Count);
    }

    [Fact]
    public void EnrollmentCounter_RecordsAndUnenrolls()
    {
        var counter = new DeviceEnrollmentCounter();
        counter.RecordEnrollment();
        counter.RecordEnrollment();
        counter.RecordUnenrollment();
        Assert.Equal(1, counter.Count);
    }

    [Fact]
    public void EnrollmentCounter_NeverGoesNegative()
    {
        var counter = new DeviceEnrollmentCounter();
        counter.RecordUnenrollment();
        Assert.Equal(0, counter.Count);
    }
}
