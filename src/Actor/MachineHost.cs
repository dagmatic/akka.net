using System;
using Akka.Actor;

namespace Dagmatic.Akka.Actor
{
    public interface IMachineHost
    {
        IDisposable ScheduleMessage(TimeSpan delay, object message);
    }

    public abstract class MachineHost : UntypedActor, IMachineHost
    {
        public class WFTimeout
        {
            public WFTimeout() { }

            public WFTimeout(Guid id)
            {
                Id = id;
            }

            public Guid Id { get; set; }
        }

        public IDisposable ScheduleMessage(TimeSpan delay, object message)
        {
            return new CancellableOnDispose(
                Context.System.Scheduler.ScheduleTellOnceCancelable(delay, Self, message, Sender));
        }

        private class CancellableOnDispose : IDisposable
        {
            private bool _disposedValue = false; // To detect redundant calls

            public CancellableOnDispose(ICancelable cancel)
            {
                Cancel = cancel;
            }

            public ICancelable Cancel { get; }

            public void Dispose()
            {
                if (!_disposedValue)
                {
                    if (!Cancel.IsCancellationRequested)
                    {
                        try
                        {
                            Cancel.Cancel(false);
                        }
                        catch { }
                    }

                    try
                    {
                        var toDispose = Cancel as IDisposable;
                        toDispose?.Dispose();
                    }
                    catch { }

                    _disposedValue = true;
                }
            }
        }
    }
}
