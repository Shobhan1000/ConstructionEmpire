using ConstructionEmpire.Data;

namespace ConstructionEmpire.Projects
{
    /// <summary>
    /// Pure quality-scoring math (spec section 20), factored out from
    /// ConstructionProjectSystem so it can be unit-tested in isolation
    /// from the rest of the simulation. ConstructionProjectSystem supplies
    /// the live crew/equipment/material inputs; this just does the blend
    /// and the score-to-level mapping used for inspections and for
    /// comparing against a project template's RequiredQuality.
    /// </summary>
    public static class QualityScoring
    {
        /// <summary>
        /// Blends three 0-1(ish) inputs into a single 0-1 quality score for
        /// an in-house phase. Crew productivity is remapped (a "normal"
        /// 1.0-productivity crew lands around 0.7 - Average/Good territory
        /// - rather than maxing out the scale) so an ordinary well-staffed
        /// phase doesn't automatically read as flawless.
        /// </summary>
        public static float BlendInHouseQuality(float averageCrewProductivity, float equipmentFactor, float materialFactor)
        {
            float crewQuality = Clamp01(averageCrewProductivity * 0.7f);
            float equipmentQuality = Clamp01(equipmentFactor);
            float materialQuality = Clamp01(materialFactor);

            return Clamp01((crewQuality + equipmentQuality + materialQuality) / 3f);
        }

        public static QualityLevel ScoreToLevel(float score)
        {
            if (score >= 0.9f) return QualityLevel.Premium;
            if (score >= 0.75f) return QualityLevel.Excellent;
            if (score >= 0.6f) return QualityLevel.Good;
            if (score >= 0.4f) return QualityLevel.Average;
            if (score >= 0.2f) return QualityLevel.BelowAverage;
            return QualityLevel.Poor;
        }

        public static float Clamp01(float value)
        {
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }
    }
}
