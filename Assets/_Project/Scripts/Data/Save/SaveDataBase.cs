using System;
using System.Runtime.Serialization;

namespace ConstructionEmpire.Data.Save
{
    [Serializable, DataContract]
    public abstract class SaveDataBase
    {
        [DataMember(Order = 0, IsRequired = true)]
        public int SchemaVersion = 1;

        /// <summary>Apply exactly one version step; update the version after success.</summary>
        public virtual void Migrate()
        {
            throw new NotSupportedException(
                $"{GetType().Name} has no migration from schema {SchemaVersion}.");
        }
    }
}
