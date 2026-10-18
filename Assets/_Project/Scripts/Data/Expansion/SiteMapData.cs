using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using ConstructionEmpire.Data.Save;
using ConstructionEmpire.Data.Construction;
namespace ConstructionEmpire.Data.Expansion
{
    [Serializable,DataContract]public sealed class SiteZoneData
    {[DataMember]public string PhaseId,Name;[DataMember]public double Progress;public SiteZoneData Copy()=>(SiteZoneData)MemberwiseClone();}
    [Serializable,DataContract]
    public sealed class SiteMapData : SaveDataBase
    {
        [DataMember]public string ProjectId,RegionId,OfficeId;
        [DataMember]public double WorldX,WorldY,BoundaryWidth=100,BoundaryHeight=80,FootprintX=25,FootprintY=20,FootprintWidth=50,FootprintHeight=40;
        [DataMember]public string Stage="Site preparation";
        [DataMember]public List<SiteZoneData> Zones=new List<SiteZoneData>();
        [DataMember]public List<string> WorkerIds=new List<string>(),EquipmentIds=new List<string>();
        [DataMember]public double Progress,StoredMaterialUnits;
        public SiteMapData Copy(){var c=(SiteMapData)MemberwiseClone();c.Zones=Zones.Select(x=>x.Copy()).ToList();c.WorkerIds=new List<string>(WorkerIds);c.EquipmentIds=new List<string>(EquipmentIds);return c;}
        public void Validate(){ConstructionValidation.Text(ProjectId,"Project");ConstructionValidation.Text(RegionId,"Region");ConstructionValidation.Positive(BoundaryWidth,"Boundary width");ConstructionValidation.Positive(BoundaryHeight,"Boundary height");ConstructionValidation.Positive(FootprintWidth,"Footprint width");ConstructionValidation.Positive(FootprintHeight,"Footprint height");if(FootprintX<0 || FootprintY<0 || FootprintX+FootprintWidth>BoundaryWidth || FootprintY+FootprintHeight>BoundaryHeight)throw new ArgumentException("Building footprint must be inside the site boundary.");ConstructionValidation.Percent(Progress,"Site progress");}
        public override void Migrate(){if(SchemaVersion!=1){base.Migrate();return;}Validate();SchemaVersion=2;}
    }
}
