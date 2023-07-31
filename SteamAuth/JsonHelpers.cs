using System.Text.Json.Serialization;
using System.Text.Json;

namespace SteamAuth {
    /// <summary>
    /// Helper classes to cover some short-fallings of <see cref="System.Text.Json"/>. (Provides a "better" (doesn't explode) number converter)
    /// </summary>
    internal class JsonHelpers {
        public static JsonSerializerOptions Options = new JsonSerializerOptions {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = {
                new NumericConverterFactory(),
            }
        };

        public class NumericConverterFactory : JsonConverterFactory {
            public override bool CanConvert(Type typeToConvert) {
                return IsNumericType(typeToConvert);
            }

            public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) {
                var converterType = typeof(NumericConverter<>).MakeGenericType(typeToConvert);
                var converterInstance = Activator.CreateInstance(converterType);
                return converterInstance as JsonConverter ?? throw new JsonException($"Converter creation failed for type '{typeToConvert.FullName}'.");
            }

            private static bool IsNumericType(Type type) {
                return
                    type == typeof(int) ||
                    type == typeof(long) ||
                    type == typeof(short) ||
                    type == typeof(byte) ||
                    type == typeof(uint) ||
                    type == typeof(ulong) ||
                    type == typeof(ushort) ||
                    type == typeof(float) ||
                    type == typeof(double) ||
                    type == typeof(decimal);
            }
        }

        public class NumericConverter<T> : JsonConverter<T> {
            public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
                if (reader.TokenType == JsonTokenType.Number) {
                    return (T)Convert.ChangeType(reader.GetDouble(), typeof(T));
                }

                if (reader.TokenType == JsonTokenType.String && double.TryParse(reader.GetString(), out var doubleValue)) {
                    return (T)Convert.ChangeType(doubleValue, typeof(T));
                }

                throw new JsonException($"Unable to convert value to {typeof(T).Name}.");
            }

            public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) {
                writer.WriteNumberValue(Convert.ToDouble(value));
            }
        }
    }
}
