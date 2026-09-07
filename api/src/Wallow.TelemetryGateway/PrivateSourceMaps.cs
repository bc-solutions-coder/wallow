using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Wallow.TelemetryGateway;

internal static class PrivateSourceMaps
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
    private const int MapBytes = 64 * 1024;
    private const int StoreBytes = 128 * 1024 * 1024;

    public static void Map(WebApplication app, string databasePath)
    {
        string root = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(databasePath))!, "source-maps");
        Directory.CreateDirectory(root);
        app.MapPut("/control/v1/source-maps/{registrationId:guid}/{release}/{file}", async (
            Guid registrationId, string release, string file, JsonElement map, RegistryDb db, CancellationToken cancellationToken) =>
        {
            if (!Label(release) || !Label(file) || !await db.Registrations.AnyAsync(item => item.Id == registrationId, cancellationToken).ConfigureAwait(false))
            {
                return Results.BadRequest();
            }
            try
            {
                if (map.GetProperty("version").GetInt32() != 3 || map.TryGetProperty("sections", out _)) { return Results.BadRequest(); }
                string[] sources = map.GetProperty("sources").EnumerateArray().Select(item => Basename(item.GetString() ?? throw new FormatException("Missing source-map string"))).ToArray();
                string[] names = map.GetProperty("names").EnumerateArray().Select(item => SafeName(item.GetString() ?? throw new FormatException("Missing source-map string"))).ToArray();
                string mappings = map.GetProperty("mappings").GetString() ?? throw new FormatException("Missing mappings");
                if (sources.Length > 1024 || names.Length > 4096 || mappings.Length > MapBytes) { return Results.BadRequest(); }
                StoredMap sanitized = new(sources, names, mappings);
                _ = Decode(sanitized, 0, 0);
                byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(sanitized);
                string path = Location(root, registrationId, release, file);
                long existing = File.Exists(path) ? new FileInfo(path).Length : 0;
                long total = new DirectoryInfo(root).EnumerateFiles("*.json").Sum(item => item.Length);
                if (bytes.Length > MapBytes || total - existing + bytes.Length > StoreBytes) { return Results.StatusCode(507); }
                string temporary = path + ".tmp";
                await File.WriteAllBytesAsync(temporary, bytes, cancellationToken).ConfigureAwait(false);
                File.Move(temporary, path, overwrite: true);
                return Results.NoContent();
            }
            catch (Exception error) when (error is JsonException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException or ArgumentException)
            {
                return Results.BadRequest();
            }
        });
        app.MapGet("/control/v1/source-maps/{registrationId:guid}/{release}/{file}", async (
            Guid registrationId, string release, string file, int line, int column, CancellationToken cancellationToken) =>
        {
            if (!Label(release) || !Label(file) || line < 1 || line > 1000000 || column < 1 || column > 1000000) { return Results.BadRequest(); }
            string path = Location(root, registrationId, release, file);
            if (!File.Exists(path)) { return Results.NotFound(); }
            StoredMap map = JsonSerializer.Deserialize<StoredMap>(await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false))!;
            SourcePosition? position = Decode(map, line - 1, column - 1);
            return position is null ? Results.NotFound() : Results.Ok(position);
        });
    }

    private static string Location(string root, Guid registration, string release, string file) => Path.Combine(root,
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{registration}/{release}/{file}"))) + ".json");

    private static bool Label(string value) => value.Length is > 0 and <= 128 && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '.');
    private static string SafeName(string value) => Label(value) ? value : "unknown";
    private static string Basename(string value) => SafeName(value.Replace('\\', '/').Split('?', '#')[0].Split('/')[^1]);

    private static SourcePosition? Decode(StoredMap map, int requestedLine, int requestedColumn)
    {
        int source = 0, originalLine = 0, originalColumn = 0, name = 0, generatedLine = 0, segments = 0;
        SourcePosition? result = null;
        foreach (string line in map.Mappings.Split(';'))
        {
            int generatedColumn = 0;
            foreach (string segment in line.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                if (++segments > 16384) { throw new FormatException("Too many mappings"); }
                List<int> values = [];
                int index = 0;
                while (index < segment.Length)
                {
                    uint value = 0;
                    int shift = 0;
                    int digit;
                    do
                    {
                        if (index >= segment.Length || shift > 30) { throw new FormatException("Invalid VLQ"); }
                        digit = Alphabet.IndexOf(segment[index++], StringComparison.Ordinal);
                        if (digit < 0 || (shift == 30 && (digit & 31) > 3)) { throw new FormatException("Invalid VLQ"); }
                        value |= (uint)(digit & 31) << shift;
                        shift += 5;
                    }
                    while ((digit & 32) != 0);
                    values.Add((value & 1) == 0 ? checked((int)(value >> 1)) : -checked((int)(value >> 1)));
                    if (values.Count > 5) { throw new FormatException("Invalid segment"); }
                }
                if (values.Count is not (1 or 4 or 5)) { throw new FormatException("Invalid segment"); }
                generatedColumn = checked(generatedColumn + values[0]);
                if (generatedColumn < 0) { throw new FormatException("Negative column"); }
                SourcePosition? position = null;
                if (values.Count > 1)
                {
                    source = checked(source + values[1]);
                    originalLine = checked(originalLine + values[2]);
                    originalColumn = checked(originalColumn + values[3]);
                    if (source < 0 || source >= map.Sources.Length || originalLine < 0 || originalColumn < 0) { throw new FormatException("Invalid position"); }
                    string? function = null;
                    if (values.Count == 5)
                    {
                        name = checked(name + values[4]);
                        if (name < 0 || name >= map.Names.Length) { throw new FormatException("Invalid name"); }
                        function = map.Names[name];
                    }
                    position = new(map.Sources[source], checked(originalLine + 1), checked(originalColumn + 1), function);
                }
                if (generatedLine == requestedLine && generatedColumn <= requestedColumn) { result = position; }
            }
            generatedLine++;
        }
        return result;
    }

    private sealed record StoredMap(string[] Sources, string[] Names, string Mappings);
    private sealed record SourcePosition(string File, int Line, int Column, string? Function);
}
