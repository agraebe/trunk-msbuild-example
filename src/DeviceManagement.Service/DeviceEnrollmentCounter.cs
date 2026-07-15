namespace Contoso.DeviceManagement.Service;

/// <summary>Tiny in-memory counter used by the compliance dashboard widgets.</summary>
public sealed class DeviceEnrollmentCounter
{
    private int _count;

    public int Count => _count;

    public void RecordEnrollment() => Interlocked.Increment(ref _count);

    public void RecordUnenrollment()
    {
        if (_count > 0)
        {
            Interlocked.Decrement(ref _count);
        }
    }
}
