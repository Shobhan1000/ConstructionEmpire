using ConstructionEmpire.Data.Presentation;
using NUnit.Framework;
namespace ConstructionEmpire.Tests.EditMode
{
    public sealed class TutorialProgressTests
    {
        private static TutorialProgressData Awarded(){var t=new TutorialProgressData();t.Navigate("Bidding");t.ReviewEstimate("p");t.Submitted("p");t.Awarded("p",true);return t;}
        [Test]public void GuideUsesActualOrderedActionsAndSameProject()
        {var t=new TutorialProgressData();Assert.That(t.Submitted("p"),Is.False);Assert.That(t.Navigate("Projects"),Is.False);t.Navigate("Bidding");t.ReviewEstimate("p");Assert.That(t.Submitted("other"),Is.False);t.Submitted("p");Assert.That(t.Awarded("other",true),Is.False);t.Awarded("p",true);t.Started("p");Assert.That(t.Progress("other","a",50),Is.False);t.Progress("p","a",50);Assert.That(t.Progress("p","b",100),Is.False);Assert.That(t.Complete,Is.False);t.Progress("p","a",100);Assert.That(t.Complete,Is.True);}
        [Test]public void LostBidAllowsNewOpportunityWithoutForcingWin()
        {var t=new TutorialProgressData();t.Navigate("Bidding");t.ReviewEstimate("p");t.Submitted("p");t.Awarded("p",false);Assert.That(t.Step,Is.Zero);Assert.That(t.ProjectId,Is.Null);t.Navigate("Bidding");Assert.That(t.ReviewEstimate("new"),Is.True);}
        [Test]public void SingleTickPhaseCompletionDoesNotStrandGuide()
        {var t=Awarded();t.Started("p");t.Progress("p","fast",100);Assert.That(t.Complete,Is.True);}
        [Test]public void ZeroAndInvalidProgressCannotAdvancePreparation()
        {var t=Awarded();t.Started("p");foreach(var n in new[]{0d,-1d,double.NaN,double.PositiveInfinity,101d})Assert.That(t.Progress("p","a",n),Is.False);Assert.That(t.Step,Is.EqualTo(5));}
    }
}
