namespace ConstructionEmpire.Data
{
    public enum WorkerType
    {
        GeneralLaborer,
        Carpenter,
        Mason,
        Electrician,
        Plumber,
        EquipmentOperator,
        Welder,
        Painter,
        SiteSupervisor,
        ProjectManager
    }

    public enum EquipmentType
    {
        Excavator,
        Bulldozer,
        WheelLoader,
        Crane,
        ConcreteMixer,
        DumpTruck,
        Forklift,
        ScissorLift,
        Telehandler,
        Compactor,
        Generator
    }

    public enum MaterialType
    {
        Concrete,
        Cement,
        Sand,
        Gravel,
        Steel,
        Lumber,
        Bricks,
        Glass,
        Drywall,
        Insulation,
        Roofing,
        Pipes,
        ElectricalCable,
        Fixtures,
        Paint,
        Flooring
    }

    public enum ProjectType
    {
        HouseRenovation,
        SmallWarehouse,
        RestaurantFitOut,
        OfficeRenovation,
        ResidentialExtension
    }

    public enum ProjectStatus
    {
        Available,
        InProgress,
        BidLost,
        Completed,
        Cancelled
    }

    public enum PhaseStatus
    {
        Locked,
        Ready,
        InProgress,
        Completed
    }

    public enum EquipmentStatus
    {
        Available,
        Assigned,
        Working,
        Maintenance,
        Broken,
        Retired
    }

    public enum QualityLevel
    {
        Poor,
        BelowAverage,
        Average,
        Good,
        Excellent,
        Premium
    }

    public enum CompetitorStrategy
    {
        LowCost,
        Premium,
        Fast,
        Specialist,
        Local
    }

    /// <summary>Trades commonly handed to subcontractors rather than done in-house (spec section 18). "None" means the phase has no subcontractor option.</summary>
    public enum SubcontractorSpecialty
    {
        None,
        Electrical,
        Plumbing,
        HVAC,
        Roofing,
        Elevators,
        Landscaping,
        StructuralSteel,
        SpecializedConcrete,
        SecuritySystems,
        FireProtection
    }

    public enum WeatherType
    {
        Clear,
        Wind,
        Rain,
        ExtremeHeat,
        HeavyRain,
        Snow,
        Storm
    }

    /// <summary>Who's paying for the project (spec section 30). Affects nothing structurally yet beyond flavor and future bidding/quality tuning hooks - see ClientData.</summary>
    public enum ClientType
    {
        PrivateHomeowner,
        SmallBusiness,
        PropertyDeveloper,
        Corporation,
        Government,
        Municipality,
        IndustrialCompany
    }

    /// <summary>Maps a client's RelationshipScore (0-100) into a display tier (spec section 31).</summary>
    public enum RelationshipLevel
    {
        New,
        Established,
        Trusted,
        Preferred,
        Strategic
    }
}
