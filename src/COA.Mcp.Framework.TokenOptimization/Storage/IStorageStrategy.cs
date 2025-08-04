using System.Threading.Tasks;

namespace COA.Mcp.Framework.TokenOptimization.Storage;

public interface IStorageStrategy
{
    string StorageType { get; }
    
    Task<string> StoreAsync(byte[] data, ResourceUri uri);
    
    Task<byte[]?> RetrieveAsync(ResourceUri uri);
    
    Task<bool> ExistsAsync(ResourceUri uri);
    
    Task<bool> DeleteAsync(ResourceUri uri);
    
    Task<long> GetSizeAsync(ResourceUri uri);
    
    Task CleanupAsync();
}