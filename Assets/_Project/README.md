# Construction Empire - Milestone 0.5

Before setup, Assets/_Project did not exist. This is the foundation's folder map:

```text
Assets/_Project/
  Art/                         reserved art assets
  Audio/                       reserved audio assets
  Data/Definitions/            reserved ScriptableObject asset instances
  Prefabs/                     reserved prefabs
  Scenes/                      reserved project scenes
  Settings/                    reserved project configuration assets
  Scripts/
    Data/                      plain serializable runtime DTO assembly
      ConstructionEmpire.Data.asmdef
      Save/SaveDataBase.cs
    Definitions/               reserved ScriptableObject types (Unity layer)
    Systems/                   plain C# systems assembly
      ConstructionEmpire.Systems.asmdef
      Persistence/SaveMigrationRunner.cs
      Randomness/SeededRandom.cs
    UI/                        reserved event-driven presentation
    Editor/                    reserved editor automation
  Tests/EditMode/
    ConstructionEmpire.Tests.EditMode.asmdef
    SaveMigrationRunnerTests.cs
    SeededRandomTests.cs
  README.md
```

Data and Systems prohibit engine and automatic plugin references. Systems depends
only on Data. Tests reference those two assemblies, NUnit, and Unity's test runners;
they target Editor only and prohibit automatic engine references. Future Unity
definitions, UI, and editor scripts belong outside the two pure C# assembly roots.

CompanyData, EmployeeData, EquipmentRuntimeData, ProjectData, ContractData,
MaterialInventoryData, ClientData, and SupplierData must later derive from
SaveDataBase and carry [Serializable] on each concrete class. No gameplay DTOs
or systems are implemented in this milestone.

The base defaults to version 1. The current migration target is 2 to exercise
the required v1 -> v2 foundation test. Future DTOs must implement every supported
step (including an explicit version-only step if no fields change). Increment the
shared constant only alongside migration implementations for affected DTOs.
The runner migrates one object in memory, rejects unsupported versions, and requires
exactly one version per call. It does not silently downgrade or skip versions.
Logs go to a supplied Action<string>, or System.Console.WriteLine by default.
A future Unity host can supply message => UnityEngine.Debug.Log(message).
Save/load must migrate all persistent nested DTOs, and discard the working graph
on failure without overwriting the original save. Disk persistence is Milestone 1.

Create one SeededRandom from the saved simulation seed and inject that same instance
into every future random-consuming system. Save the seed in the future save root.
Replay from the beginning requires identical ordered calls, inputs, configuration,
and System.Random implementation. A seed alone does not resume a mid-sequence
checkpoint, and System.Random does not guarantee sequences across runtime versions.
Snapshot/random-state persistence is deliberately not implemented here.

Run the ConstructionEmpire.Tests.EditMode assembly in Unity Test Runner's EditMode
tab. It contains 14 cases covering migration success/logging, current-version no-op,
invalid/null/future versions, broken migrations, missing migrations, and seeded replay.

Scope source: the attached Kickoff Brief and the explicit Milestone 0.5 request.
Sections 50-53 were referenced but not included in that brief. In the brief,
principle 8 is seeded randomness and principle 9 is config-driven balance values.
Reserved folders establish boundaries only; they do not implement later milestones.
