using System;
using System.Collections.Generic;

namespace ConstructionEmpire.Data
{
    /// <summary>A group of workers assigned to work together (spec section 10).</summary>
    [Serializable]
    public class CrewData
    {
        public string Id;
        public string Name;
        public List<string> WorkerIds = new List<string>();
        public string CurrentProjectId;
        public string CurrentPhaseId;
    }
}
