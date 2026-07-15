using Contoso.Common.Core;
using Contoso.Common.Core.Models;
using Contoso.Common.FeatureFlags;

namespace Contoso.DeviceManagement.Service;

/// <summary>Evaluates whether a device is allowed to enroll / stay enrolled.</summary>
public sealed class DeviceComplianceService
{
    private readonly IFeatureFlagProvider _flags;
    private readonly IReadOnlyList<Device> _devices;

    public DeviceComplianceService(IFeatureFlagProvider flags, SeedDataLoader seedDataLoader)
    {
        _flags = flags;
        _devices = seedDataLoader.LoadDevices();
    }

    public Result<Device> GetDevice(string deviceId)
    {
        var device = _devices.FirstOrDefault(d => d.Id == deviceId);
        return device is not null
            ? Result<Device>.Success(device)
            : Result<Device>.Failure($"Unknown device '{deviceId}'.");
    }

    public bool IsEligibleForOnboarding(Device device)
    {
        if (!device.Compliant)
        {
            return false;
        }

        // New onboarding flow only applies once the flag is on; otherwise every
        // compliant device is eligible under the legacy behavior.
        var newFlowEnabled = _flags.IsEnabled("new-device-onboarding-flow", device.CustomerId);
        return newFlowEnabled ? device.Platform is "Windows" or "macOS" : true;
    }

    public IReadOnlyList<Device> ListForCustomer(string customerId) =>
        _devices.Where(d => d.CustomerId == customerId).ToList();
}
