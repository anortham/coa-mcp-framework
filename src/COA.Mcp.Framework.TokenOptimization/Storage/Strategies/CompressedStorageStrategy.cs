using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace COA.Mcp.Framework.TokenOptimization.Storage.Strategies;

public class CompressedStorageStrategy : IStorageStrategy
{
    private readonly IStorageStrategy _innerStrategy;
    private readonly CompressionLevel _compressionLevel;
    
    public CompressedStorageStrategy(IStorageStrategy innerStrategy, CompressionLevel compressionLevel = CompressionLevel.Optimal)
    {
        _innerStrategy = innerStrategy ?? throw new ArgumentNullException(nameof(innerStrategy));
        _compressionLevel = compressionLevel;
    }
    
    public string StorageType => $"{_innerStrategy.StorageType}-compressed";
    
    public async Task<string> StoreAsync(byte[] data, ResourceUri uri)
    {
        var compressedData = Compress(data);
        return await _innerStrategy.StoreAsync(compressedData, uri);
    }
    
    public async Task<byte[]?> RetrieveAsync(ResourceUri uri)
    {
        var compressedData = await _innerStrategy.RetrieveAsync(uri);
        if (compressedData == null)
            return null;
            
        return Decompress(compressedData);
    }
    
    public Task<bool> ExistsAsync(ResourceUri uri)
    {
        return _innerStrategy.ExistsAsync(uri);
    }
    
    public Task<bool> DeleteAsync(ResourceUri uri)
    {
        return _innerStrategy.DeleteAsync(uri);
    }
    
    public Task<long> GetSizeAsync(ResourceUri uri)
    {
        return _innerStrategy.GetSizeAsync(uri);
    }
    
    public Task CleanupAsync()
    {
        return _innerStrategy.CleanupAsync();
    }
    
    private byte[] Compress(byte[] data)
    {
        using var output = new MemoryStream();
        using (var compressor = new GZipStream(output, _compressionLevel))
        {
            compressor.Write(data, 0, data.Length);
        }
        return output.ToArray();
    }
    
    private byte[] Decompress(byte[] compressedData)
    {
        using var input = new MemoryStream(compressedData);
        using var decompressor = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        
        decompressor.CopyTo(output);
        return output.ToArray();
    }
}