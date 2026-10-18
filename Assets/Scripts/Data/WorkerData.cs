using System;

namespace ConstructionEmpire.Data
{
    /// <summary>An individual hired employee (spec section 9).</summary>
    [Serializable]
    public class WorkerData
    {
        public string Id;
        public string Name;
        public WorkerType Type;
        public float Productivity = 1f;
        public float Fatigue;      // 0-100, higher is more tired
        public float Morale = 70f; // 0-100
        public double DailyWage;
        public string CurrentCrewId;

        /// <summary>Productivity after fatigue/morale modifiers - what the sim should actually use, not the raw base value.</summary>
        public float EffectiveProductivity()
        {
            float fatiguePenalty = Fatigue / 200f;        // up to -50% at max fatigue
            float moraleBonus = (Morale - 50f) / 200f;     // +/-25% around a neutral midpoint
            float multiplier = 1f - fatiguePenalty + moraleBonus;
            if (multiplier < 0.1f) multiplier = 0.1f;
            return Productivity * multiplier;
        }
    }
}
