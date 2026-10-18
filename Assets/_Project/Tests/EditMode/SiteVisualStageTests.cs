using System;
using System.Linq;
using NUnit.Framework;
using ConstructionEmpire.Data.Bidding;
using ConstructionEmpire.Data.Presentation;
using ConstructionEmpire.Systems.Presentation;
namespace ConstructionEmpire.Tests.EditMode
{
    public sealed class SiteVisualStageTests
    {
        [TestCase(0,0)][TestCase(19.99,0)][TestCase(20,1)][TestCase(39.99,1)][TestCase(40,2)][TestCase(60,3)][TestCase(85,4)][TestCase(100,4)]
        public void StageBoundariesMapWithoutChangingProgress(double progress,int expected)
        {Assert.That(SiteVisualStageSelector.Select(SiteVisualStageData.CreateDefaults(),progress,ProjectStatus.Active,Array.Empty<string>()),Is.EqualTo(expected));}
        [Test]public void FinishedRequiresAuthoritativeCompletion()
        {Assert.That(SiteVisualStageSelector.Select(SiteVisualStageData.CreateDefaults(),100,ProjectStatus.Complete,Array.Empty<string>()),Is.EqualTo(5));}
        [Test]public void MissingFoundationGatePreventsSkippingToLaterStages()
        {var stages=SiteVisualStageData.CreateDefaults();stages[1].RequiredCompletedPhaseIds.Add("foundation");Assert.That(SiteVisualStageSelector.Select(stages,75,ProjectStatus.Active,Array.Empty<string>()),Is.Zero);Assert.That(SiteVisualStageSelector.Select(stages,75,ProjectStatus.Active,new[]{"foundation"}),Is.EqualTo(3));}
        [Test]public void InvalidThresholdsAndNonfiniteProgressAreRejected()
        {var stages=SiteVisualStageData.CreateDefaults();stages[2].MinimumProgress=10;Assert.Throws<ArgumentException>(()=>SiteVisualStageSelector.Validate(stages));Assert.Throws<ArgumentOutOfRangeException>(()=>SiteVisualStageSelector.Select(SiteVisualStageData.CreateDefaults(),double.NaN,ProjectStatus.Active,Array.Empty<string>()));}
    }
}
