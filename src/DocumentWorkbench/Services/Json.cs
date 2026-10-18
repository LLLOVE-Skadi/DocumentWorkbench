using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocumentWorkbench.Services;

public static class Json
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };
}
