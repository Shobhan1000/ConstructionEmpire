using System;
using System.Collections.Generic;
using System.Linq;
using ConstructionEmpire.Bootstrap;
using ConstructionEmpire.Data.Bidding;
using ConstructionEmpire.Data.Events;
using ConstructionEmpire.Data.Presentation;
using ConstructionEmpire.Definitions;
using ConstructionEmpire.Systems.Management;
using ConstructionEmpire.Systems.Presentation;
using UnityEngine;
using UnityEngine.UIElements;
namespace ConstructionEmpire.UI
{
    /// <summary>Event-driven view of one authoritative project. No Update, physics, or simulation mutation.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class SiteVisualController : MonoBehaviour
    {
        public SimulationBootstrapper Bootstrap;public string ProjectId;public SiteVisualDefinition Definition;
        [Header("Optional world-space outputs")]
        public SpriteRenderer ShellRenderer;public Transform PrefabRoot,ProgressFill;
        public GameObject WorkerPresenceIcon,EquipmentPresenceIcon;
        private ManagementSession session;private EventSubscriptions subscriptions;
        private readonly Dictionary<int,GameObject> instances=new Dictionary<int,GameObject>();
        private List<SiteVisualStageData> rules;private VisualElement view,shell,fill,workers,equipment;
        private Label title,stageLabel,percentage,workerCount,equipmentCount;private Image shellImage,workerImage,equipmentImage;
        private int renderedStage=-1;private Vector3 fullBarScale;private bool scaleCaptured;
        public int CurrentStageIndex {get;private set;}=-1;
        public double DisplayedProgress {get;private set;}
        public int AssignedWorkerCount {get;private set;}public int AssignedEquipmentCount {get;private set;}
        private void Start(){if(Application.isPlaying && session==null && Bootstrap!=null && Bootstrap.Management!=null && !string.IsNullOrEmpty(ProjectId))Initialize(Bootstrap.Management,ProjectId);}
        public void Initialize(ManagementSession source,string projectId,VisualElement host=null)
        {
            subscriptions?.Dispose();subscriptions=null;view?.RemoveFromHierarchy();view=null;ClearInstances();renderedStage=-1;
            session=source??throw new ArgumentNullException(nameof(source));ProjectId=projectId;
            rules=Definition==null?SiteVisualStageData.CreateDefaults():Definition.Stages.Select(x=>x?.Stage?.Copy()??throw new ArgumentException("Null stage binding.")).ToList();SiteVisualStageSelector.Validate(rules);
            if(ProgressFill!=null && !scaleCaptured){fullBarScale=ProgressFill.localScale;scaleCaptured=true;}
            if(host!=null)BuildView(host);if(isActiveAndEnabled)Subscribe();Refresh();
        }
        private void Subscribe()
        {
            if(subscriptions!=null || session==null)return;subscriptions=new EventSubscriptions();
            subscriptions.Subscribe<PhaseProgressChangedEvent>(e=>RefreshProject(e.ProjectId));
            subscriptions.Subscribe<ProjectStartedEvent>(e=>RefreshProject(e.ProjectId));
            subscriptions.Subscribe<ProjectCompletedEvent>(e=>RefreshProject(e.ProjectId));
            subscriptions.Subscribe<ResourcesChangedEvent>(e=>{if(string.IsNullOrEmpty(e.ProjectId) || e.ProjectId==ProjectId)RefreshPresence();});
            subscriptions.Subscribe<ConstructionEmpire.Data.Scheduling.ScheduleChangedEvent>(e=>RefreshProject(e.ProjectId));
            subscriptions.Subscribe<ConstructionEmpire.Data.Risk.ForecastChangedEvent>(e=>RefreshProject(e.Forecast.ProjectId));
            subscriptions.Subscribe<SimulationReplacedEvent>(e=>{if(Bootstrap==e.Source){var host=view?.parent;Initialize(Bootstrap.Management,ProjectId,host);}});
        }
        private void RefreshProject(string id){if(id==ProjectId)Refresh();}
        public void Refresh()
        {
            var project=session?.Find(ProjectId);if(project==null){Hide();return;}
            if(view!=null)view.style.display=DisplayStyle.Flex;
            DisplayedProgress=session.Progress(project);CurrentStageIndex=SiteVisualStageSelector.Select(rules,DisplayedProgress,project.Status,session.Phases(ProjectId).Where(x=>x.IsComplete).Select(x=>x.Id));
            if(title!=null)title.text=project.Title;
            if(CurrentStageIndex!=renderedStage)RenderStage(CurrentStageIndex);
            if(percentage!=null)percentage.text=$"{DisplayedProgress:0.0}% · {project.Status}";
            if(fill!=null)
            {fill.style.width=Length.Percent((float)DisplayedProgress);var forecast=session.Risks.Forecasts.Get(ProjectId);fill.style.backgroundColor=project.Status==ProjectStatus.Complete?new Color(.15f,.55f,.34f):forecast!=null && forecast.DelayHours>0?new Color(.8f,.56f,.14f):new Color(.17f,.43f,.65f);}
            if(ProgressFill!=null)ProgressFill.localScale=new Vector3(fullBarScale.x*(float)DisplayedProgress/100,fullBarScale.y,fullBarScale.z);
            RefreshPresence();
        }
        private void RenderStage(int index)
        {
            renderedStage=index;foreach(var instance in instances.Values)if(instance!=null)instance.SetActive(false);
            var binding=Definition!=null?Definition.Stages[index]:null;var sprite=binding?.Sprite;
            if(ShellRenderer!=null){ShellRenderer.sprite=sprite;ShellRenderer.enabled=sprite!=null && (binding.Prefab==null || PrefabRoot==null);}
            if(binding?.Prefab!=null && PrefabRoot!=null)
            {if(!instances.TryGetValue(index,out var instance) || instance==null){instance=Instantiate(binding.Prefab,PrefabRoot,false);instances[index]=instance;}instance.SetActive(true);}
            if(shellImage!=null){shellImage.sprite=sprite;shellImage.style.display=sprite==null?DisplayStyle.None:DisplayStyle.Flex;}
            if(stageLabel!=null)stageLabel.text=rules[index].Name+(sprite==null?" · visual placeholder":"");
            if(shell!=null)shell.tooltip="Illustrative construction stage; progress comes from the live project. "+rules[index].Name;
        }
        private void RefreshPresence()
        {
            if(session?.Find(ProjectId)==null){AssignedWorkerCount=0;AssignedEquipmentCount=0;}else
            {AssignedWorkerCount=session.Workforce.Workers.Count(x=>x.CurrentProjectId==ProjectId);AssignedEquipmentCount=session.Workforce.Equipment.Count(x=>x.CurrentProjectId==ProjectId);}
            if(WorkerPresenceIcon!=null)WorkerPresenceIcon.SetActive(AssignedWorkerCount>0);if(EquipmentPresenceIcon!=null)EquipmentPresenceIcon.SetActive(AssignedEquipmentCount>0);
            if(workers!=null){workers.style.display=AssignedWorkerCount>0?DisplayStyle.Flex:DisplayStyle.None;workerCount.text="Workers · "+AssignedWorkerCount;}
            if(equipment!=null){equipment.style.display=AssignedEquipmentCount>0?DisplayStyle.Flex:DisplayStyle.None;equipmentCount.text="Equipment · "+AssignedEquipmentCount;}
        }
        private void BuildView(VisualElement host)
        {
            view=new VisualElement {name="site-visual"};view.AddToClassList("site-visual");host.Add(view);
            title=DashboardElements.Label(view,"","card-title","site-title");percentage=DashboardElements.Label(view,"","site-progress-label","site-progress-label");
            var track=DashboardElements.Box(view,"site-progress-track");fill=DashboardElements.Box(track,"site-progress-fill","site-progress-fill");
            shell=DashboardElements.Box(view,"site-shell","site-shell");shellImage=new Image {name="site-shell-image",scaleMode=ScaleMode.ScaleToFit};shellImage.AddToClassList("site-shell-image");shell.Add(shellImage);stageLabel=DashboardElements.Label(shell,"","site-stage-label","site-stage");
            var badges=DashboardElements.Box(view,"site-presence");workers=DashboardElements.Box(badges,"site-badge","site-workers");equipment=DashboardElements.Box(badges,"site-badge","site-equipment");
            workerImage=new Image {sprite=Definition?.WorkerIcon};equipmentImage=new Image {sprite=Definition?.EquipmentIcon};workerImage.AddToClassList("site-presence-icon");equipmentImage.AddToClassList("site-presence-icon");workers.Add(workerImage);equipment.Add(equipmentImage);
            workerImage.style.display=workerImage.sprite==null?DisplayStyle.None:DisplayStyle.Flex;equipmentImage.style.display=equipmentImage.sprite==null?DisplayStyle.None:DisplayStyle.Flex;
            workerCount=DashboardElements.Label(workers,"");equipmentCount=DashboardElements.Label(equipment,"");workers.tooltip="Employees currently assigned to this site.";equipment.tooltip="Equipment currently assigned to this site.";
        }
        private void Hide(){CurrentStageIndex=-1;renderedStage=-1;DisplayedProgress=0;if(view!=null)view.style.display=DisplayStyle.None;if(ShellRenderer!=null)ShellRenderer.enabled=false;foreach(var item in instances.Values)if(item!=null)item.SetActive(false);if(ProgressFill!=null)ProgressFill.localScale=new Vector3(0,fullBarScale.y,fullBarScale.z);RefreshPresence();}
        private void ClearInstances(){foreach(var item in instances.Values)if(item!=null){item.SetActive(false);if(Application.isPlaying)Destroy(item);else DestroyImmediate(item);}instances.Clear();}
        private void OnEnable(){if(session!=null){Subscribe();Refresh();}}
        private void OnDisable(){subscriptions?.Dispose();subscriptions=null;if(view!=null)view.style.display=DisplayStyle.None;foreach(var item in instances.Values)if(item!=null)item.SetActive(false);if(ShellRenderer!=null)ShellRenderer.enabled=false;if(WorkerPresenceIcon!=null)WorkerPresenceIcon.SetActive(false);if(EquipmentPresenceIcon!=null)EquipmentPresenceIcon.SetActive(false);renderedStage=-1;}
        private void OnDestroy(){subscriptions?.Dispose();subscriptions=null;ClearInstances();view?.RemoveFromHierarchy();session=null;}
    }
}
