using System;
using System.Collections.Generic;

namespace ConstructionEmpire.Data.Events
{
    /// <summary>Owns distinct subscriptions; disposing also suppresses in-flight snapshot callbacks.</summary>
    public sealed class EventSubscriptions : IDisposable
    {
        private readonly List<Action> cleanup = new List<Action>();
        private bool disposed;

        public void Subscribe<T>(Action<T> handler)
        {
            if (disposed) throw new ObjectDisposedException(nameof(EventSubscriptions));
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            Action<T> guarded = payload => { if (!disposed) handler(payload); };
            cleanup.Add(GameEvents.SubscribeOwned(guarded));
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (var unsubscribe in cleanup) unsubscribe();
            cleanup.Clear();
        }
    }
}
