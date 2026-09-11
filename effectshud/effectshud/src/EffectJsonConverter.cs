using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace effectshud.src
{
    /// <summary>
    /// Safe JSON converter for Effect serialization/deserialization.
    /// Uses TypeId from EffectRegistrationAttribute instead of storing full type names,
    /// preventing RCE vulnerabilities from TypeNameHandling.Auto.
    /// </summary>
    public class EffectJsonConverter : JsonConverter
    {
        // Inner serializer without this converter to avoid infinite recursion
        private static readonly JsonSerializer _innerSerializer = new JsonSerializer();

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(Effect) || objectType.IsSubclassOf(typeof(Effect));
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var jsonObject = JObject.Load(reader);

            var typeId = jsonObject["effectTypeId"]?.Value<string>();
            if (string.IsNullOrEmpty(typeId))
                return null;

            if (effectshud.Instance == null || !effectshud.Instance.effects.TryGetValue(typeId, out Type effectType))
                return null;

            return jsonObject.ToObject(effectType, _innerSerializer) as Effect;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value is Effect effect)
            {
                _innerSerializer.Serialize(writer, effect);
            }
        }
    }
}
