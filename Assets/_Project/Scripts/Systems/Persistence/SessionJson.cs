using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
namespace ConstructionEmpire.Systems.Persistence
{
    public static class SessionJson
    {
        public static string Serialize<T>(T value)
        {using(var stream=new MemoryStream()){Serializer<T>().WriteObject(stream,value);return Encoding.UTF8.GetString(stream.ToArray());}}
        public static T Deserialize<T>(string json)
        {if(string.IsNullOrWhiteSpace(json))throw new ArgumentException("Save content is empty.");using(var stream=new MemoryStream(Encoding.UTF8.GetBytes(json)))return (T)Serializer<T>().ReadObject(stream);}
        public static T Copy<T>(T value)=>Deserialize<T>(Serialize(value));
        private static DataContractJsonSerializer Serializer<T>()=>new DataContractJsonSerializer(typeof(T),new DataContractJsonSerializerSettings {UseSimpleDictionaryFormat=true,MaxItemsInObjectGraph=10000000});
    }
}
