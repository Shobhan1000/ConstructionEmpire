using UnityEngine;

namespace ConstructionEmpire.Config
{
    /// <summary>
    /// Static, designer-editable starting values (spec section 56: "Make
    /// all starting values configurable"). Create an instance via
    /// Assets > Create > ConstructionEmpire > Game Config and assign it to
    /// GameBootstrap in the Bootstrap scene.
    /// </summary>
    [CreateAssetMenu(fileName = "GameConfig", menuName = "ConstructionEmpire/Game Config")]
    public class GameConfig : ScriptableObject
    {
        [Header("Company Starting Values")]
        public string DefaultCompanyName = "New Construction Co.";
        public double StartingCash = 250000;
        [Range(0, 100)] public float StartingReputation = 50f;

        [Header("Starting Date")]
        public int StartingYear = 2025;
        [Range(1, 12)] public int StartingMonth = 1;
        [Range(1, 31)] public int StartingDay = 1;

        [Header("Simulation")]
        [Tooltip("Real-world seconds per in-game hour at 1x speed.")]
        public float RealSecondsPerGameHour = 3f;

        [Header("Determinism")]
        [Tooltip("Seed for the shared SeededRandom instance, so a run can be reproduced.")]
        public int RandomSeed = 12345;
    }
}
