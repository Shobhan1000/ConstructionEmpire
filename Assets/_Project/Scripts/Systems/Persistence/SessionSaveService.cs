using System;
using System.IO;
using System.Text;
using System.Runtime.Serialization;
using ConstructionEmpire.Data.Save;
namespace ConstructionEmpire.Systems.Persistence
{
    public static class SessionSaveService
    {
        private const long MaximumBytes=64L*1024*1024;
        [DataContract]private sealed class Envelope
        {[DataMember(IsRequired=true)]public string Payload;[DataMember(IsRequired=true)]public string Checksum;}
        public static void Validate(SessionSaveData save)
        {
            if(save==null || save.Version!=SessionSaveData.CurrentVersion || save.Rules!=SessionSaveData.CurrentRules)throw new NotSupportedException("This save uses unsupported simulation rules or format.");
            if(save.Initial==null || save.Initial.Clock==null || save.Clock==null || save.Commands==null || string.IsNullOrEmpty(save.StateProof))throw new InvalidDataException("Save is incomplete.");
            save.Initial.Clock.Validate();save.Clock.Validate();
            if(save.Clock.TotalHours<save.Initial.Clock.TotalHours || save.Clock.TotalHours-save.Initial.Clock.TotalHours>2000000 || save.Commands.Count>1000000)throw new InvalidDataException("Save exceeds the supported timeline size.");
            long hour=save.Initial.Clock.TotalHours;
            foreach(var c in save.Commands)
            {if(c==null || !Enum.IsDefined(typeof(SessionCommandKind),c.Kind) || c.Hour<hour || c.Hour>save.Clock.TotalHours || c.EquipmentIds==null || c.EquipmentIds.Count>10000 || double.IsNaN(c.Amount) || double.IsInfinity(c.Amount) || double.IsNaN(c.Value) || double.IsInfinity(c.Value))throw new InvalidDataException("Invalid saved command.");hour=c.Hour;}
        }
        public static void Save(string path,SessionSaveData save)
        {
            Validate(save);string payload=SessionJson.Serialize(save);string json=SessionJson.Serialize(new Envelope {Payload=payload,Checksum=SessionStateProof.Hash(payload)});
            if(Encoding.UTF8.GetByteCount(json)>MaximumBytes)throw new InvalidDataException("Save exceeds the supported file size.");
            string full=Path.GetFullPath(path);Directory.CreateDirectory(Path.GetDirectoryName(full));string temporary=full+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                {byte[] bytes=new UTF8Encoding(false).GetBytes(json);stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
                if(File.Exists(full))File.Replace(temporary,full,full+".bak");else File.Move(temporary,full);
            }
            finally{if(File.Exists(temporary))File.Delete(temporary);}
        }
        public static SessionSaveData Load(string path)
        {
            var file=new FileInfo(path);if(!file.Exists)throw new FileNotFoundException("No save exists in this slot.",path);if(file.Length>MaximumBytes)throw new InvalidDataException("Save file is too large.");
            var envelope=SessionJson.Deserialize<Envelope>(File.ReadAllText(path,Encoding.UTF8));
            if(envelope==null || envelope.Payload==null || envelope.Checksum!=SessionStateProof.Hash(envelope.Payload))throw new InvalidDataException("Save checksum failed; try the backup slot.");
            var save=SessionJson.Deserialize<SessionSaveData>(envelope.Payload);Validate(save);return save;
        }
    }
}
