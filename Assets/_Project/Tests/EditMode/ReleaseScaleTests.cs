using System;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;
using ConstructionEmpire.Data.Bidding;
using ConstructionEmpire.Data.Company;
using ConstructionEmpire.Data.Construction;
using ConstructionEmpire.Data.Events;
using ConstructionEmpire.Data.Time;
using ConstructionEmpire.Systems.Simulation;
namespace ConstructionEmpire.Tests.EditMode
{
    public sealed class ReleaseScaleTests
    {
        [Test]public void TwentyProjectsAndTwoHundredWorkersKeepIndependentProgressAndReservations()
        {
            GameEvents.Clear();var config=new CrewEfficiencyConfiguration {SkillFloor=1,UnmatchedTradeMultiplier=1,ExperienceBonusPerYear=0,MaximumExperienceBonus=0,NoSupervisorMultiplier=1,SupervisorSkillBonus=0,MaximumFatiguePenalty=0};
            using(var sim=new SimulationManager(new CompanyData("Scale test",10000000),new GameTimeData(),42,config))
            {
                var engines=Enumerable.Range(0,20).Select(i=>i==0?sim.Construction:sim.CreateProjectEngine()).ToArray();
                var phases=new List<ConstructionPhaseRuntimeData>();var timer=System.Diagnostics.Stopwatch.StartNew();
                for(int i=0;i<20;i++)
                {
                    var engine=engines[i];var p=SampleProjectDataFactory.CreateStarterProjects()[0];p.Id="scale-"+i;p.Status=ProjectStatus.Won;
                    var phase=new ConstructionPhaseRuntimeData {Id="work",Name="Work",DurationHours=1000,LaborHours=10000};phases.Add(phase);
                    var crew=new CrewData {Id="crew-"+i,Name="Crew "+i};
                    for(int w=0;w<10;w++){var worker=SampleFleetAndCrewDataFactory.CreateStarterWorkersAndEquipment().Workers[0];worker.Id="worker-"+i+"-"+w;worker.Productivity=1;crew.Members.Add(worker);}
                    engine.StartProject(p,new[]{phase},new MaterialInventoryData());engine.AssignCrew(p.Id,"work",crew,Array.Empty<EquipmentRuntimeData>());if(i==0)engine.SetEnvironment(p.Id,1,0);
                }
                for(int h=0;h<100;h++)sim.Advance(1);
                Assert.That(phases[0].Progress,Is.Zero);foreach(var p in phases.Skip(1))Assert.That(p.Progress,Is.EqualTo(10).Within(.00001));
                Assert.That(sim.ProjectEngines.Count,Is.EqualTo(20));
                TestContext.WriteLine("20 concurrent projects / 200 workers / 100 ticks: "+timer.ElapsedMilliseconds+" ms");
            }
            GameEvents.Clear();
        }
    }
}
