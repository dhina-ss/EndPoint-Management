using EMS.API.Services;

namespace EMS.API.Tests;

public class DeviceStatusCalculatorTests
{
    private static readonly DateTime Now = new(2026, 7, 27, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void RecentHeartbeat_IsOnline()
    {
        var status = DeviceStatusCalculator.Compute(Now.AddMinutes(-1), null, Now);
        Assert.Equal(DeviceStatusCalculator.Online, status);
    }

    [Fact]
    public void NoRecentHeartbeat_NoSuspend_IsOffline()
    {
        var status = DeviceStatusCalculator.Compute(Now.AddMinutes(-30), null, Now);
        Assert.Equal(DeviceStatusCalculator.Offline, status);
    }

    [Fact]
    public void SuspendedAfterLastHeartbeat_NotOnline_IsSleep()
    {
        var status = DeviceStatusCalculator.Compute(Now.AddMinutes(-10), Now.AddMinutes(-8), Now);
        Assert.Equal(DeviceStatusCalculator.Sleep, status);
    }

    [Fact]
    public void OnlineWins_EvenIfSuspendFlagSet()
    {
        // A fresh heartbeat means it is awake regardless of a stale suspend mark.
        var status = DeviceStatusCalculator.Compute(Now.AddSeconds(-30), Now.AddMinutes(-1), Now);
        Assert.Equal(DeviceStatusCalculator.Online, status);
    }

    [Fact]
    public void VeryOldSuspend_IsOffline()
    {
        var status = DeviceStatusCalculator.Compute(Now.AddDays(-2), Now.AddDays(-2), Now);
        Assert.Equal(DeviceStatusCalculator.Offline, status);
    }
}
