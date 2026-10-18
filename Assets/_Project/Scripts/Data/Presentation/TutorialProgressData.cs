using System;
namespace ConstructionEmpire.Data.Presentation
{
    /// <summary>Presentation state only; never changes awards, inventory, payments or simulation time.</summary>
    public sealed class TutorialProgressData
    {
        public int Step {get;private set;}
        public string ProjectId {get;private set;}
        public string PhaseId {get;private set;}
        public bool Complete => Step==7;
        public bool Navigate(string screen) {if(Step!=0 || screen!="Bidding")return false;Step=1;return true;}
        public bool ReviewEstimate(string id) {if(Step!=1 || string.IsNullOrEmpty(id))return false;ProjectId=id;Step=2;return true;}
        public bool Submitted(string id) {if(Step!=2 || id!=ProjectId)return false;Step=3;return true;}
        public bool Awarded(string id,bool won) {if(Step!=3 || id!=ProjectId)return false;if(won)Step=4;else Reset();return true;}
        public bool Started(string id) {if(Step!=4 || id!=ProjectId)return false;Step=5;return true;}
        public bool Progress(string projectId,string phaseId,double value)
        {
            if(projectId!=ProjectId || string.IsNullOrEmpty(phaseId) || double.IsNaN(value) || double.IsInfinity(value) || value<=0 || value>100)return false;
            if(Step==5){PhaseId=phaseId;Step=value>=100?7:6;return true;}
            if(Step!=6 || phaseId!=PhaseId || value<100)return false;
            Step=7;return true;
        }
        public void Restore(int step,string projectId,string phaseId)
        {if(step<0 || step>7 || step>=2 && string.IsNullOrEmpty(projectId) || step>=6 && string.IsNullOrEmpty(phaseId))throw new ArgumentException("Invalid tutorial progress.");Step=step;ProjectId=projectId;PhaseId=phaseId;}
        public void Reset(){Step=0;ProjectId=null;PhaseId=null;}
    }
}
