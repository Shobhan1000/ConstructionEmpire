using System;
using ConstructionEmpire.Data.Bidding;
using ConstructionEmpire.Definitions;
using NUnit.Framework;
using UnityEngine;
namespace ConstructionEmpire.Tests.EditorIntegration
{
    public sealed class ProjectTemplateTests
    {
        [Test]
        public void TemplateScalesRequirementsAndCreatesIndependentData()
        {
            var template=ScriptableObject.CreateInstance<ProjectTemplateDefinition>();
            try
            {
                template.TemplateId="T";template.BuildingType="Office";template.MinimumSize=50;template.MaximumSize=200;
                template.ReferenceSize=100;template.BaseDurationHours=20;template.BaseRequirements=new RequirementProfile { LaborHours=50,MaterialUnits=10,EquipmentHours=4,SubcontractorHours=2 };
                var a=template.CreateProject("P","Test","Region","Road","C","Client",200,10000,100,0,"Pending");
                Assert.That(a.EstimatedDurationHours,Is.EqualTo(40));Assert.That(a.Requirements.LaborHours,Is.EqualTo(100));
                Assert.That(a.Requirements.MaterialUnits,Is.EqualTo(20));Assert.That(a.Requirements.EquipmentHours,Is.EqualTo(8));
                Assert.That(a.Requirements.SubcontractorHours,Is.EqualTo(4));
                a.Requirements.LaborHours=0;Assert.That(template.BaseRequirements.LaborHours,Is.EqualTo(50));
                Assert.Throws<ArgumentOutOfRangeException>(()=>template.CreateProject("P","Test","Region","Road","C","Client",201,10000,100,0,"Pending"));
            }
            finally { UnityEngine.Object.DestroyImmediate(template); }
        }
    }
}
