using ConstructionEmpire.Data.Construction;
using ConstructionEmpire.Definitions;
using NUnit.Framework;
using UnityEngine;
namespace ConstructionEmpire.Tests.EditorIntegration
{
    public sealed class ConstructionDefinitionTests
    {
        [Test] public void PhaseDefinition_CreatesIndependentRuntimeGraphs()
        {
            var definition=ScriptableObject.CreateInstance<ConstructionPhaseDefinition>();
            try
            {
                definition.Configure(SampleRenovationPhaseFactory.CreateHouseRenovationPhases()[2]);
                var first=definition.CreateRuntimeData(); var second=definition.CreateRuntimeData();
                first.Progress=100; first.PrerequisiteIds.Clear(); first.MaterialRequirements[0].Quantity=0;
                Assert.That(second.Progress,Is.Zero); Assert.That(second.PrerequisiteIds.Count,Is.EqualTo(1));
                Assert.That(second.MaterialRequirements[0].Quantity,Is.EqualTo(12)); Assert.That(definition.MaterialRequirements[0].Quantity,Is.EqualTo(12));
            }
            finally { Object.DestroyImmediate(definition); }
        }
    }
}
