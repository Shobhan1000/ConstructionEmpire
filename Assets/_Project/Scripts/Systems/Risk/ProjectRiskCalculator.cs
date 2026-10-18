using System;
using System.Collections.Generic;
using ConstructionEmpire.Data.Construction;
using ConstructionEmpire.Data.Risk;
namespace ConstructionEmpire.Systems.Risk
{
    public static class ProjectRiskCalculator
    {
        // All inputs are normalized exposures: 0 is safe, 1 is maximum risk.
        public static ProjectRiskAssessment Calculate(RiskFactors factors,RiskSettings settings)
        {
            if(factors==null || settings==null)throw new ArgumentNullException();settings.Validate();
            var values=factors.Values();var weights=settings.Weights.Values();double sum=0,total=0;
            foreach(var weight in weights)total+=weight;
            var contributions=new Dictionary<string,double>();
            for(int i=0;i<values.Length;i++){ConstructionValidation.Unit(values[i],RiskFactors.Names[i]);double value=values[i]*weights[i]/total;contributions.Add(RiskFactors.Names[i],value);sum+=value;}
            return new ProjectRiskAssessment(sum,sum>=settings.CriticalThreshold?RiskLevel.Critical:sum>=settings.HighThreshold?RiskLevel.High:sum>=settings.MediumThreshold?RiskLevel.Medium:RiskLevel.Low,contributions);
        }
    }
}
