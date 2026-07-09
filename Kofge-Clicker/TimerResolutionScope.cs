namespace KofgeClicker;

public sealed class TimerResolutionScope : IDisposable
{
    private readonly uint _period;
    private readonly bool _active;
    private bool _disposed;

    public TimerResolutionScope(uint period)
    {
        _period = period;
        _active = NativeMethods.TimeBeginPeriod(period) == 0;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_active)
        {
            NativeMethods.TimeEndPeriod(_period);
        }

        _disposed = true;
    }
}
