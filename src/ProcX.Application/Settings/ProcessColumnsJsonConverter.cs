using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProcX.Application.Settings;

/// <summary>
/// Stores a <see cref="ProcessColumns"/> set as a JSON array of column names, e.g. <c>["Pid", "Session"]</c>.
/// </summary>
public sealed class ProcessColumnsJsonConverter : JsonConverter<ProcessColumns>
{
    /// <inheritdoc/>
    public override ProcessColumns Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException("Expected an array of column names.");
        }

        var columns = ProcessColumns.None;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException("Column names must be strings.");
            }

            // Unknown names (e.g. from a newer version) are ignored instead of failing the whole file.
            if (Enum.TryParse<ProcessColumns>(reader.GetString(), ignoreCase: true, out var column) && Enum.IsDefined(column))
            {
                columns |= column;
            }
        }

        return columns;
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, ProcessColumns value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var column in Enum.GetValues<ProcessColumns>())
        {
            if (column is not (ProcessColumns.None or ProcessColumns.All) && value.HasFlag(column))
            {
                writer.WriteStringValue(column.ToString());
            }
        }

        writer.WriteEndArray();
    }
}
