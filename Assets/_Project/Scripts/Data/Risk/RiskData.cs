using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using ConstructionEmpire.Data.Construction;
namespace ConstructionEmpire.Data.Risk
{
    public enum RiskLevel { Low, Medium, High, Critical }
    public enum WeatherKind { Clear, Rain, HeavyRain, Wind, Storm, Snow, ExtremeHeat }
    [Serializable,DataContract]
    public sealed class ProjectSiteProfile
    {
        [DataMember] public string Description="Existing structure / urban site";
        [DataMember] public double Difficulty=.15,Soil=.1,Remoteness=.1,AccessConstraint=.1,DesignComplexity=.2;
        [DataMember] public double Compliance=.9,SafetyTraining=.7,Quality=.8;
        [DataMember] public bool GroundConditionDiscovered;
        public ProjectSiteProfile Copy()=>(ProjectSiteProfile)MemberwiseClone();
        public void Validate(){foreach(double x in new[]{Difficulty,Soil,Remoteness,AccessConstraint,DesignComplexity,Compliance,SafetyTraining,Quality})ConstructionValidation.Unit(x,"Site rating");}
    }
    [Serializable]
    public sealed class RiskFactors
    {
        public double SiteDifficulty,DeadlineTightness,SoilConditions,Remoteness,SubcontractorReliability,MaterialShortageExposure,DesignComplexity,WeatherVolatility,CrewExperience;
        public double[] Values()=>new[]{SiteDifficulty,DeadlineTightness,SoilConditions,Remoteness,SubcontractorReliability,MaterialShortageExposure,DesignComplexity,WeatherVolatility,CrewExperience};
        public static readonly string[] Names={"Site difficulty","Deadline tightness","Soil conditions","Remoteness","Subcontractor reliability exposure","Material shortage exposure","Design complexity","Weather volatility","Crew inexperience"};
    }
    [Serializable]
    public sealed class RiskSettings
    {
        public RiskFactors Weights=new RiskFactors {SiteDifficulty=1,DeadlineTightness=1.2,SoilConditions=1,Remoteness=.6,SubcontractorReliability=1,MaterialShortageExposure=1.2,DesignComplexity=1,WeatherVolatility=.8,CrewExperience=1};
        public double MediumThreshold=.25,HighThreshold=.5,CriticalThreshold=.75;
        public double WeatherShiftRate=.025,WeatherDriftStep=.2,IndoorWeatherExposure=.15;
        public double[] WeatherMultipliers={1,.8,.55,.75,.15,.4,.65};
        public string[] IndoorPhaseIds={"electrical","plumbing","hvac","insulation","drywall","flooring","finishes","inspection","completion"};
        public string[] InspectionPhaseIds={"foundation","framing","electrical","plumbing","hvac","inspection"};
        public string[] GroundPhaseIds={"excavation","foundation"};
        public double BreakdownRate=.002,AbsenceRate=.001,SafetyRate=.0005,GroundDiscoveryRate=.006;
        public double MinimumRiskFrequency=.1,RiskFrequencyMultiplier=2,InspectionBaseFailure=.01,InspectionVulnerabilityWeight=.35;
        public double FatiguePerWorkHour=.006,RestPerHour=.025,EquipmentWearPerHour=.0008,ExperienceReferenceYears=10;
        public int AbsenceHours=8,SafetyStopHours=8,GroundStopHours=12,InspectionStopHours=4;
        public double InspectionReworkHours=6,InspectionCost=350,SafetyInvestigationCost=750,SafetyInsuranceExcess=250,GroundRemediationCost=1200;
        public double SafetyReputationLoss=2,InspectionQualityLoss=.03,RepairCondition=.9,TrainingCostPerWorker=50,TrainingImprovement=.1;
        public double SiteDifficultyPenalty=.25,SoilPenalty=.2,AccessPenalty=.15,RemotenessPenalty=.1;
        public double ForecastRiskCostAllowance=.15,ForecastRiskScheduleAllowance=.2,MinimumForecastProductivity=.1;
        public double ScheduleWarningHours=72,EquipmentConditionWarning=.2,FatigueWarning=.8,BudgetWarningRatio=.95,CriticalFloatWarningHours=4;
        public void Validate()
        {
            if(Weights==null || WeatherMultipliers==null || WeatherMultipliers.Length!=7 || IndoorPhaseIds==null || InspectionPhaseIds==null || GroundPhaseIds==null)throw new ArgumentException("Risk configuration is incomplete.");
            double sum=0;foreach(var x in Weights.Values()){ConstructionValidation.NonNegative(x,"Risk weight");sum+=x;}ConstructionValidation.Positive(sum,"Total risk weight");
            foreach(var x in new[]{MediumThreshold,HighThreshold,CriticalThreshold,IndoorWeatherExposure,WeatherDriftStep,InspectionBaseFailure,InspectionVulnerabilityWeight,RepairCondition,TrainingImprovement,EquipmentConditionWarning,FatigueWarning,BudgetWarningRatio,InspectionQualityLoss})ConstructionValidation.Unit(x,"Risk setting");
            if(!(MediumThreshold<HighThreshold && HighThreshold<CriticalThreshold))throw new ArgumentException("Risk thresholds must increase.");
            foreach(var x in WeatherMultipliers)ConstructionValidation.Unit(x,"Weather multiplier");
            foreach(var x in new[]{WeatherShiftRate,BreakdownRate,AbsenceRate,SafetyRate,GroundDiscoveryRate,MinimumRiskFrequency,RiskFrequencyMultiplier,FatiguePerWorkHour,RestPerHour,EquipmentWearPerHour,InspectionReworkHours,InspectionCost,SafetyInvestigationCost,SafetyInsuranceExcess,GroundRemediationCost,SafetyReputationLoss,TrainingCostPerWorker,SiteDifficultyPenalty,SoilPenalty,AccessPenalty,RemotenessPenalty,ForecastRiskCostAllowance,ForecastRiskScheduleAllowance,ScheduleWarningHours,CriticalFloatWarningHours})ConstructionValidation.NonNegative(x,"Risk setting");
            ConstructionValidation.Positive(ExperienceReferenceYears,"Experience reference");ConstructionValidation.Positive(MinimumForecastProductivity,"Forecast productivity floor");
            if(AbsenceHours<=0 || SafetyStopHours<=0 || GroundStopHours<=0 || InspectionStopHours<=0)throw new ArgumentException("Event durations must be positive.");
        }
    }
    public sealed class ProjectRiskAssessment
    {
        public double Score {get;} public RiskLevel Level {get;} public IReadOnlyDictionary<string,double> Contributions {get;}
        public ProjectRiskAssessment(double score,RiskLevel level,Dictionary<string,double> factors){Score=score;Level=level;Contributions=factors;}
    }
    public sealed class ProjectForecast
    {
        public string ProjectId;public long UpdatedHour;public double ExpectedCompletionHour,DeadlineHour,ExpectedFinalCost,Budget,ExpectedProfit,Progress;public ProjectRiskAssessment Risk;
        public double DelayHours=>Math.Max(0,ExpectedCompletionHour-DeadlineHour);
    }
    public sealed class ProactiveAlert
    {
        public string Id,ProjectId,EntityId,Title,Description,Screen;public long Hour;public bool Critical;
    }
    public readonly struct WeatherChangedEvent {public string Region {get;}public WeatherKind Weather {get;}public WeatherChangedEvent(string region,WeatherKind weather){Region=region;Weather=weather;}}
    public readonly struct RiskStateChangedEvent {public string ProjectId {get;}public string Description {get;}public RiskStateChangedEvent(string id,string description){ProjectId=id;Description=description;}}
    public readonly struct ForecastChangedEvent {public ProjectForecast Forecast {get;}public ForecastChangedEvent(ProjectForecast forecast){Forecast=forecast;}}
    public readonly struct ProactiveAlertsChangedEvent {public string ProjectId {get;}public ProactiveAlertsChangedEvent(string id){ProjectId=id;}}
    public readonly struct SiteConditionDiscoveredEvent {public string ProjectId {get;}public string Description {get;}public SiteConditionDiscoveredEvent(string id,string description){ProjectId=id;Description=description;}}
    public readonly struct InspectionPassedEvent {public string ProjectId {get;}public string PhaseId {get;}public InspectionPassedEvent(string id,string phase){ProjectId=id;PhaseId=phase;}}
}
