using System;
using System.Collections.Generic;
using System.Linq;
using ConstructionEmpire.Data.Events;
namespace ConstructionEmpire.Systems.Simulation
{
    /// <summary>Simulation-owned, stable FIFO for equal hours. No wall-clock timers.</summary>
    public sealed class ScheduledEventQueue : IRegisterableSystem
    {
        private sealed class Entry { public long Id, Hour; public Action Dispatch; }
        private readonly List<Entry> entries=new List<Entry>();
        private long nextId, currentHour;
        public ScheduledEventQueue(long initialHour=0) { currentHour=initialHour; }
        public long Schedule<T>(long dueHour,T payload)
        {
            if(dueHour<=currentHour) throw new ArgumentOutOfRangeException(nameof(dueHour),"Schedule a future simulation hour.");
            var entry=new Entry { Id=checked(++nextId),Hour=dueHour,Dispatch=()=>GameEvents.Publish(payload) };
            entries.Add(entry); return entry.Id;
        }
        public bool Cancel(long id) => entries.RemoveAll(x=>x.Id==id)>0;
        public void OnTick(TickContext context)
        {
            currentHour=context.TotalHours;
            var due=entries.Where(x=>x.Hour<=currentHour).OrderBy(x=>x.Hour).ThenBy(x=>x.Id).ToArray();
            var errors=new List<Exception>();
            foreach(var entry in due)
                if(entries.Remove(entry)) { try { entry.Dispatch(); } catch(Exception e) { errors.Add(e); } }
            if(errors.Count>0) throw new AggregateException(errors);
        }
    }
}
