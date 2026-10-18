using ConstructionEmpire.Data.Events;
using UnityEngine;

namespace ConstructionEmpire.Bootstrap
{
    /// <summary>
    /// Derive and implement RegisterEvents. Use subscriptions.Subscribe&lt;T&gt; there.
    /// Do not shadow OnEnable/OnDisable/OnDestroy; lifecycle ownership stays here.
    /// Disabled objects stop listening and re-subscribe when enabled again.
    /// </summary>
    public abstract class GameEventListener : MonoBehaviour
    {
        private EventSubscriptions subscriptions;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetBus() => GameEvents.Clear();

        private void OnEnable()
        {
            Release();
            subscriptions = new EventSubscriptions();
            try { RegisterEvents(subscriptions); }
            catch { Release(); throw; }
        }

        protected abstract void RegisterEvents(EventSubscriptions subscriptions);
        private void OnDisable() => Release();
        private void OnDestroy() => Release();
        private void Release()
        {
            subscriptions?.Dispose();
            subscriptions = null;
        }
    }
}
