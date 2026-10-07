using Newtonsoft.Json;

namespace FightingAllstar.Core.Content
{
    /// <summary>Serializes plain combat snapshots, including recursive effect-condition trees.</summary>
    public static class SnapshotJson
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            MaxDepth = 128
        };
        public static string Serialize<T>(T value)
        {
            return JsonConvert.SerializeObject(value, Settings);
        }

        public static T Deserialize<T>(string json)
        {
            return JsonConvert.DeserializeObject<T>(json, Settings);
        }
    }
}
