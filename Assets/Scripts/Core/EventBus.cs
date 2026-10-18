using System;
using System.Collections.Generic;

namespace ConstructionEmpire.Core
{
    /// <summary>
    /// Simple static publish/subscribe bus. Systems publish structs that
    /// implement IGameEvent; UI and other systems subscribe to react
    /// instead of polling. Keeps simulation and UI decoupled (see
    /// spec section 52, Event-Driven Architecture).
    /// </summary>
    public static class EventBus
    {
        private static readonly Dictionary<Type, Delegate> Subscribers = new Dictionary<Type, Delegate>();

        public static void Subscribe<T>(Action<T> handler) where T : IGameEvent
        {
            var type = typeof(T);
            if (Subscribers.TryGetValue(type, out var existing))
            {
                Subscribers[type] = Delegate.Combine(existing, handler);
            }
            else
            {
                Subscribers[type] = handler;
            }
        }

        public static void Unsubscribe<T>(Action<T> handler) where T : IGameEvent
        {
            var type = typeof(T);
            if (!Subscribers.TryGetValue(type, out var existing)) return;

            var result = Delegate.Remove(existing, handler);
            if (result == null)
            {
                Subscribers.Remove(type);
            }
            else
            {
                Subscribers[type] = result;
            }
        }

        public static void Publish<T>(T gameEvent) where T : IGameEvent
        {
            if (Subscribers.TryGetValue(typeof(T), out var existing) && existing is Action<T> handler)
            {
                handler.Invoke(gameEvent);
            }
        }

        /// <summary>Clears every subscriber. Call between play sessions / in test teardown so handlers don't leak across scenes or tests.</summary>
        public static void Clear()
        {
            Subscribers.Clear();
        }
    }
}
