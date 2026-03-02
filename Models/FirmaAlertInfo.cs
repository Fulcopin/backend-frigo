using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FormBuilder.API.Models
{
    /// <summary>
    /// Clase auxiliar para deserializar el JSON de Firmas 
    /// y extraer nombres para enviar alertas de email.
    /// </summary>
    public class FirmaAlertInfo
    {
        public string? Puesto { get; set; }
        public string? NombreCompleto { get; set; }
        public bool CapturaFecha { get; set; }
        public bool CapturaHora { get; set; }

        /// <summary>
        /// Lista de nombres de reemplazos (solo para la firma principal, index 0)
        /// </summary>
        public List<string>? Reemplazos { get; set; }

        /// <summary>
        /// Lista de jefes/superiores para escalamiento de alertas.
        /// Puede venir como array ["Jefe1", "Jefe2"] o como string simple "Jefe1" (retrocompat).
        /// </summary>
        [JsonConverter(typeof(JefeAlertaConverter))]
        public List<string>? JefeAlerta { get; set; }
    }

    /// <summary>
    /// Converter que acepta tanto un string como un array de strings para JefeAlerta,
    /// para mantener retrocompatibilidad con datos viejos.
    /// </summary>
    public class JefeAlertaConverter : JsonConverter<List<string>?>
    {
        public override List<string>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return null;

            if (reader.TokenType == JsonTokenType.String)
            {
                var val = reader.GetString();
                return string.IsNullOrWhiteSpace(val) ? new List<string>() : new List<string> { val };
            }

            if (reader.TokenType == JsonTokenType.StartArray)
            {
                var list = new List<string>();
                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndArray)
                        break;
                    if (reader.TokenType == JsonTokenType.String)
                    {
                        var item = reader.GetString();
                        if (!string.IsNullOrWhiteSpace(item))
                            list.Add(item);
                    }
                }
                return list;
            }

            return null;
        }

        public override void Write(Utf8JsonWriter writer, List<string>? value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }
            writer.WriteStartArray();
            foreach (var item in value)
                writer.WriteStringValue(item);
            writer.WriteEndArray();
        }
    }
}
