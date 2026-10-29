using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace DungeonGame.Core.Serialization
{
    /// <summary>
    /// JSON変換の共通設定。クライアントとサーバーで同じ設定を使うことで、同じJSONを同じように読む。
    /// 列挙型は数値ではなく名前(文字列)で書き出す(データを人が読み書きしやすくするため)。
    /// </summary>
    public static class GameJson
    {
        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            Culture = CultureInfo.InvariantCulture,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore,
            FloatParseHandling = FloatParseHandling.Decimal,
            Converters = { new StringEnumConverter() },
        };

        public static string Serialize<T>(T value) => JsonConvert.SerializeObject(value, Settings);

        public static T Deserialize<T>(string json) => JsonConvert.DeserializeObject<T>(json, Settings);
    }
}
