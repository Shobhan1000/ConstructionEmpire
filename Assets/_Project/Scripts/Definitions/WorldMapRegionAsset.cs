using ConstructionEmpire.Data.Expansion;
using UnityEngine;
namespace ConstructionEmpire.Definitions
{
    [CreateAssetMenu(menuName="Construction Empire/World Map Region")]
    public sealed class WorldMapRegionAsset : ScriptableObject
    {public WorldMapData Data=new WorldMapData();public WorldMapData CreateRuntimeData(){Data.Validate();return Data.Copy();}}
}
