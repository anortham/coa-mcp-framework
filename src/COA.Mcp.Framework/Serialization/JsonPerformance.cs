using System.Buffers;
using System.Text.Json;

namespace COA.Mcp.Framework.Serialization;

/// <summary>
/// Performance-optimized JSON serialization utilities that minimize memory allocations
/// and provide async streaming capabilities.
/// </summary>
public static class JsonPerformance
{
    /// <summary>
    /// Serializes an object to UTF-8 bytes without creating intermediate strings.
    /// This is more memory-efficient than JsonSerializer.Serialize which creates strings.
    /// </summary>
    /// <typeparam name="T">Type to serialize</typeparam>
    /// <param name="value">Value to serialize</param>
    /// <param name="options">JSON options (defaults to JsonDefaults.Standard)</param>
    /// <returns>UTF-8 encoded JSON bytes</returns>
    public static byte[] SerializeToUtf8Bytes<T>(T value, JsonSerializerOptions? options = null)
    {
        return JsonSerializer.SerializeToUtf8Bytes(value, options ?? JsonDefaults.Standard);
    }
    
    /// <summary>
    /// Serializes an object directly to a stream asynchronously without creating intermediate strings.
    /// This is the most memory-efficient approach for large objects.
    /// </summary>
    /// <typeparam name="T">Type to serialize</typeparam>
    /// <param name="stream">Destination stream</param>
    /// <param name="value">Value to serialize</param>
    /// <param name="options">JSON options (defaults to JsonDefaults.Standard)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public static async Task SerializeAsync<T>(
        Stream stream, 
        T value, 
        JsonSerializerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        await JsonSerializer.SerializeAsync(stream, value, options ?? JsonDefaults.Standard, cancellationToken);
    }
    
    /// <summary>
    /// Deserializes from a stream asynchronously without loading the entire content into memory.
    /// </summary>
    /// <typeparam name="T">Type to deserialize</typeparam>
    /// <param name="stream">Source stream</param>
    /// <param name="options">JSON options (defaults to JsonDefaults.Standard)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Deserialized object</returns>
    public static async Task<T?> DeserializeAsync<T>(
        Stream stream,
        JsonSerializerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return await JsonSerializer.DeserializeAsync<T>(stream, options ?? JsonDefaults.Standard, cancellationToken);
    }
    
    /// <summary>
    /// Deserializes from UTF-8 bytes without creating intermediate strings.
    /// </summary>
    /// <typeparam name="T">Type to deserialize</typeparam>
    /// <param name="utf8Bytes">UTF-8 encoded JSON bytes</param>
    /// <param name="options">JSON options (defaults to JsonDefaults.Standard)</param>
    /// <returns>Deserialized object</returns>
    public static T? DeserializeFromUtf8Bytes<T>(
        ReadOnlySpan<byte> utf8Bytes,
        JsonSerializerOptions? options = null)
    {
        return JsonSerializer.Deserialize<T>(utf8Bytes, options ?? JsonDefaults.Standard);
    }
    
    /// <summary>
    /// Serializes to a reusable buffer writer for scenarios where you need to reuse the JSON multiple times.
    /// Call ToArray() or WriteTo() on the returned writer to get the final result.
    /// </summary>
    /// <typeparam name="T">Type to serialize</typeparam>
    /// <param name="value">Value to serialize</param>
    /// <param name="options">JSON options (defaults to JsonDefaults.Standard)</param>
    /// <returns>ArrayBufferWriter containing the UTF-8 JSON</returns>
    public static ArrayBufferWriter<byte> SerializeToBuffer<T>(
        T value,
        JsonSerializerOptions? options = null)
    {
        var writer = new ArrayBufferWriter<byte>();
        using var jsonWriter = new Utf8JsonWriter(writer);
        JsonSerializer.Serialize(jsonWriter, value, options ?? JsonDefaults.Standard);
        return writer;
    }
}