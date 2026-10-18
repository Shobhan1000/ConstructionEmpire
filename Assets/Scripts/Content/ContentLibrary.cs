using System.Collections.Generic;
using System.Linq;
using ConstructionEmpire.Data;

namespace ConstructionEmpire.Content
{
    /// <summary>
    /// Runtime lookup for all static game content. Built once at startup
    /// (see DefaultContentFactory) and referenced everywhere else by
    /// stable id/enum, never by direct object reference - content is
    /// rebuilt fresh each session, so a saved object reference wouldn't
    /// survive a reload anyway.
    /// </summary>
    public class ContentLibrary
    {
        private readonly Dictionary<string, ConstructionPhaseDefinition> _phasesById = new Dictionary<string, ConstructionPhaseDefinition>();
        private readonly Dictionary<ProjectType, ProjectTemplate> _projectTemplates = new Dictionary<ProjectType, ProjectTemplate>();
        private readonly Dictionary<MaterialType, MaterialDefinition> _materials = new Dictionary<MaterialType, MaterialDefinition>();
        private readonly Dictionary<EquipmentType, EquipmentDefinition> _equipment = new Dictionary<EquipmentType, EquipmentDefinition>();
        private readonly Dictionary<WorkerType, WorkerTypeDefinition> _workerTypes = new Dictionary<WorkerType, WorkerTypeDefinition>();
        private readonly Dictionary<string, SubcontractorDefinition> _subcontractorsById = new Dictionary<string, SubcontractorDefinition>();

        public void RegisterPhase(ConstructionPhaseDefinition phase) => _phasesById[phase.PhaseId] = phase;
        public void RegisterProjectTemplate(ProjectTemplate template) => _projectTemplates[template.Type] = template;
        public void RegisterMaterial(MaterialDefinition material) => _materials[material.Type] = material;
        public void RegisterEquipment(EquipmentDefinition equipment) => _equipment[equipment.Type] = equipment;
        public void RegisterWorkerType(WorkerTypeDefinition workerType) => _workerTypes[workerType.Type] = workerType;
        public void RegisterSubcontractor(SubcontractorDefinition subcontractor) => _subcontractorsById[subcontractor.Id] = subcontractor;

        public ConstructionPhaseDefinition GetPhase(string phaseId) => phaseId != null && _phasesById.TryGetValue(phaseId, out var p) ? p : null;
        public ProjectTemplate GetProjectTemplate(ProjectType type) => _projectTemplates.TryGetValue(type, out var t) ? t : null;
        public MaterialDefinition GetMaterial(MaterialType type) => _materials.TryGetValue(type, out var m) ? m : null;
        public EquipmentDefinition GetEquipment(EquipmentType type) => _equipment.TryGetValue(type, out var e) ? e : null;
        public WorkerTypeDefinition GetWorkerType(WorkerType type) => _workerTypes.TryGetValue(type, out var w) ? w : null;
        public SubcontractorDefinition GetSubcontractor(string id) => id != null && _subcontractorsById.TryGetValue(id, out var s) ? s : null;

        public IReadOnlyList<ProjectTemplate> AllProjectTemplates => _projectTemplates.Values.ToList();
        public IReadOnlyList<SubcontractorDefinition> GetSubcontractors(SubcontractorSpecialty specialty) =>
            _subcontractorsById.Values.Where(s => s.Specialty == specialty).ToList();
    }
}
