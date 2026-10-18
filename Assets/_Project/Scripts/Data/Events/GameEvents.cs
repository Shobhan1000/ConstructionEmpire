using System;
using System.Collections.Generic;
namespace ConstructionEmpire.Data.Events
{
    /// <summary>Main-thread typed bus with isolated reconstruction domains and owned cleanup.</summary>
    public static class GameEvents
    {
        private static List<Dictionary<Type,Delegate>> active=new List<Dictionary<Type,Delegate>> {new Dictionary<Type,Delegate>()};
        public static void Subscribe<T>(Action<T> handler)=>SubscribeOwned(handler);
        public static Action SubscribeOwned<T>(Action<T> handler)
        {
            if(handler==null)throw new ArgumentNullException(nameof(handler));var store=active[0];
            if(store.TryGetValue(typeof(T),out var existing))
            {foreach(var registered in existing.GetInvocationList())if(registered.Equals(handler))return ()=>Remove(store,handler);store[typeof(T)]=Delegate.Combine(existing,handler);}
            else store.Add(typeof(T),handler);
            return ()=>Remove(store,handler);
        }
        private static void Remove<T>(Dictionary<Type,Delegate> store,Action<T> handler)
        {if(!store.TryGetValue(typeof(T),out var existing))return;var remaining=Delegate.Remove(existing,handler);if(remaining==null)store.Remove(typeof(T));else store[typeof(T)]=remaining;}
        public static void Unsubscribe<T>(Action<T> handler)
        {if(handler==null)throw new ArgumentNullException(nameof(handler));foreach(var store in active)Remove(store,handler);}
        public static void Publish<T>(T payload)
        {
            // Capture the complete invocation list before any callback can edit subscriptions.
            Delegate snapshot=null;foreach(var store in active)if(store.TryGetValue(typeof(T),out var item))snapshot=Delegate.Combine(snapshot,item);
            ((Action<T>)snapshot)?.Invoke(payload);
        }
        public static void Clear(){foreach(var store in active)store.Clear();}
        public sealed class Domain
        {
            private readonly Dictionary<Type,Delegate> store=new Dictionary<Type,Delegate>();
            public IDisposable Activate()=>new Activation(store);
            public void Attach()
            {active.RemoveAll(x=>x.Count==0 && !ReferenceEquals(x,active[0]));if(!active.Contains(store))active.Add(store);}
            private sealed class Activation : IDisposable
            {
                private readonly List<Dictionary<Type,Delegate>> previous;private bool disposed;
                public Activation(Dictionary<Type,Delegate> store){previous=active;active=new List<Dictionary<Type,Delegate>>{store};}
                public void Dispose(){if(disposed)return;active=previous;disposed=true;}
            }
        }
    }
}
