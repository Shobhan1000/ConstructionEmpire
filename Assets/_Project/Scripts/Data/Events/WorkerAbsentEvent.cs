namespace ConstructionEmpire.Data.Events
{
    public readonly struct WorkerAbsentEvent
    {
        public string EmployeeId { get; }
        public string ProjectId { get; }
        public int AbsentHours { get; }
        public string Reason { get; }

        public WorkerAbsentEvent(string employeeId, string projectId, int absentHours, string reason)
        {
            EmployeeId = employeeId;
            ProjectId = projectId;
            AbsentHours = absentHours;
            Reason = reason;
        }
    }
}
