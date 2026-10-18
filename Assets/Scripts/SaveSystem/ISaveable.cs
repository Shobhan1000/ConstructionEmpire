namespace ConstructionEmpire.Persistence
{
    /// <summary>
    /// Implemented by any system whose data needs to survive a save/load
    /// cycle. TState should be a plain serializable DTO (see CompanyData).
    /// SaveSystem currently wires up CompanySystem directly; once more
    /// systems implement this, generalize SaveSystem to loop over a
    /// registered list instead of listing each one by name.
    /// </summary>
    public interface ISaveable<TState>
    {
        TState CaptureState();
        void RestoreState(TState state);
    }
}
