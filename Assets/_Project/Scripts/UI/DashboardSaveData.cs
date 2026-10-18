using System.Collections.Generic;
using System.Runtime.Serialization;
namespace ConstructionEmpire.UI
{
    [DataContract]public sealed class DashboardSaveData
    {
        [DataMember]public string Screen="Dashboard",ProjectId,RegionId,TutorialProject,TutorialPhase;
        [DataMember]public int Step;
        [DataMember]public bool GuideHidden;
        [DataMember]public List<NotificationRecord> Notifications=new List<NotificationRecord>();
    }
}
