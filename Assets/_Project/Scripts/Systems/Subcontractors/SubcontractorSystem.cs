using System;
using System.Collections.Generic;
using System.Linq;
using ConstructionEmpire.Data.Construction;
using ConstructionEmpire.Data.Events;
using ConstructionEmpire.Data.Finance;
using ConstructionEmpire.Data.Subcontractors;
using ConstructionEmpire.Systems.Construction;
using ConstructionEmpire.Systems.Finance;
using ConstructionEmpire.Systems.Randomness;
using ConstructionEmpire.Systems.Simulation;
namespace ConstructionEmpire.Systems.Subcontractors
{
    public static class SubcontractorPerformance
    {
        public static bool RollDelay(double reliability,SeededRandom random)
        {ConstructionValidation.Unit(reliability,nameof(reliability));if(random==null)throw new ArgumentNullException(nameof(random));return random.NextDouble()>=reliability;}
    }
    public sealed class SubcontractorSystem : IDisposable
    {
        private readonly List<SubcontractorCompanyData> companies;private readonly List<SubcontractorQuote> quotes=new List<SubcontractorQuote>();private readonly List<SubcontractAward> awards=new List<SubcontractAward>();
        private readonly ProjectProgressEngine engine;private readonly Func<string,ProjectProgressEngine> resolveEngine;private readonly FinanceSystem finance;private readonly GameTimeManager clock;private readonly SeededRandom random;private readonly SubcontractorSettings settings;private readonly EventSubscriptions subscriptions=new EventSubscriptions();private bool disposed;
        public IReadOnlyList<SubcontractorCompanyData> Companies=>companies.AsReadOnly();public IReadOnlyList<SubcontractAward> Awards=>awards.AsReadOnly();
        public SubcontractorSystem(IEnumerable<SubcontractorCompanyData> profiles,ProjectProgressEngine engine,FinanceSystem finance,GameTimeManager clock,SeededRandom random,SubcontractorSettings settings,Func<string,ProjectProgressEngine> resolveEngine=null)
        {
            companies=profiles.Select(x=>x.Copy()).OrderBy(x=>x.Id,StringComparer.Ordinal).ToList();foreach(var c in companies)c.Validate();if(companies.Select(x=>x.Id).Distinct().Count()!=companies.Count)throw new ArgumentException("Duplicate subcontractor.");
            this.engine=engine;this.resolveEngine=resolveEngine;this.finance=finance;this.clock=clock;this.random=random;this.settings=settings;settings.Validate();
            subscriptions.Subscribe<PhaseProgressChangedEvent>(e=> {if(awards.Any(x=>x.Quote.ProjectId==e.ProjectId && x.Quote.PhaseId==e.PhaseId))GameEvents.Publish(new SubcontractorChangedEvent(e.ProjectId,e.PhaseId));});
        }
        public IReadOnlyList<SubcontractorQuote> GetQuotes(string project,string phase)=>quotes.Where(x=>x.ProjectId==project && x.PhaseId==phase).ToList().AsReadOnly();
        public IReadOnlyList<SubcontractorQuote> RequestQuotes(string project,ConstructionPhaseRuntimeData phase)
        {
            EnsureLive();ConstructionValidation.Text(project,nameof(project));if(phase==null || !phase.RequiresSubcontractor)throw new InvalidOperationException("This phase does not require subcontracted work.");phase.Validate();
            var existing=GetQuotes(project,phase.Id);if(existing.Count>0)return existing;
            var candidates=companies.Where(x=>x.Specialty==phase.SubcontractorSpecialty && x.CanAcceptWork).Take(3).ToList();
            if(candidates.Count<2)throw new InvalidOperationException("At least two available subcontractors are needed for competing quotes.");
            foreach(var company in candidates)
            {
                double amount=phase.LaborHours*settings.BaseHourlyRate*company.QuotePricingTendency*(1+(random.NextDouble()*2-1)*settings.QuotePriceVariation);
                double duration=Math.Ceiling(phase.DurationHours*company.TimelineMultiplier*(1+(random.NextDouble()*2-1)*settings.QuoteTimelineVariation));
                if(double.IsInfinity(amount) || amount>(double)decimal.MaxValue || duration>int.MaxValue)throw new ArgumentException("Quote exceeds supported range.");
                quotes.Add(new SubcontractorQuote("quote-"+(quotes.Count+1),project,phase.Id,company.Id,decimal.Round((decimal)amount,2,MidpointRounding.AwayFromZero),Math.Max(1,(int)duration),company.Reliability,company.Quality));
            }
            GameEvents.Publish(new SubcontractorChangedEvent(project,phase.Id));return GetQuotes(project,phase.Id);
        }
        public SubcontractAward Award(string quoteId,IEnumerable<EquipmentRuntimeData> equipment)
        {
            EnsureLive();var quote=quotes.Single(x=>x.Id==quoteId);var company=companies.Single(x=>x.Id==quote.CompanyId);
            if(!company.CanAcceptWork)throw new InvalidOperationException("This subcontractor is no longer available.");
            if(awards.Any(x=>x.Quote.ProjectId==quote.ProjectId && x.Quote.PhaseId==quote.PhaseId))throw new InvalidOperationException("This phase already has an award.");
            var target=resolveEngine?.Invoke(quote.ProjectId)??engine;
            var phase=target.GetPhases(quote.ProjectId).Single(x=>x.Id==quote.PhaseId);
            if(!new PhaseDependencyGraph(target.GetPhases(quote.ProjectId)).CanStart(phase.Id))throw new InvalidOperationException("Complete the prerequisite phases before awarding work.");
            // Validate assignment before consuming the performance roll or recording a financial obligation.
            var award=new SubcontractAward(quote,false,clock.TotalHours);
            var source=new SubcontractorWorkSource(company,award,phase);
            target.AssignWorkSource(quote.ProjectId,phase.Id,source,equipment);
            bool delayed=SubcontractorPerformance.RollDelay(company.Reliability,random);
            award.SetPerformance(delayed,checked(clock.TotalHours+(delayed ? settings.DelayHours : 0)));
            phase.Risk=Math.Max(phase.Risk,1-company.Reliability);awards.Add(award);
            finance.AccrueExpense(quote.ProjectId,quote.Price,FinancialCategory.Subcontractors,"subcontract-"+quote.Id,"Award: "+company.Name,checked(award.AvailableFromHour+quote.DurationHours));
            if(delayed)GameEvents.Publish(new ProjectDelayedEvent(quote.ProjectId,settings.DelayHours,company.Name+" delayed for "+settings.DelayHours+" hours on "+phase.Name+"."));
            GameEvents.Publish(new SubcontractorChangedEvent(quote.ProjectId,phase.Id));return award;
        }
        private void EnsureLive(){if(disposed)throw new ObjectDisposedException(nameof(SubcontractorSystem));}
        public void Dispose(){if(disposed)return;disposed=true;subscriptions.Dispose();}
    }
    /// <summary>A quoted work source uses the same engine as employees. No subcontractor progress loop exists.</summary>
    internal sealed class SubcontractorWorkSource : IPhaseWorkSource
    {
        private readonly SubcontractorCompanyData company;private readonly SubcontractAward award;private readonly ConstructionPhaseRuntimeData phase;
        public SubcontractorWorkSource(SubcontractorCompanyData company,SubcontractAward award,ConstructionPhaseRuntimeData phase){this.company=company;this.award=award;this.phase=phase;}
        public string Id=>"subcontract-"+award.Quote.Id;public IEnumerable<string> WorkerIds=>Array.Empty<string>();public bool IsClaimed {get;private set;}public double WagePerHour=>0;
        public void Validate()=>company.Validate();public bool CanPerform(ConstructionPhaseRuntimeData p)=>p.RequiresSubcontractor && p.SubcontractorSpecialty==company.Specialty;
        public bool IsAvailable(long hour)=>hour>award.AvailableFromHour;
        public double BaseProgressPerHour(ConstructionPhaseRuntimeData p)=>100d/award.Quote.DurationHours;
        public double LaborEfficiency(ConstructionPhaseRuntimeData p,CrewEfficiencyConfiguration configuration)=>1;
        public void Claim(string project,string task){if(!company.CanAcceptWork || IsClaimed)throw new InvalidOperationException("Subcontractor capacity exhausted.");company.ActiveAssignments++;IsClaimed=true;}
        public void Release(){if(!IsClaimed)return;company.ActiveAssignments--;IsClaimed=false;if(phase.IsComplete)award.MarkCompleted();}
    }
}
