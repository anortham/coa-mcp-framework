using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using COA.Mcp.Framework.Base;
using COA.Mcp.Framework;
using COA.Mcp.Framework.Interfaces;
using Microsoft.Extensions.Logging;

namespace SimpleMcpServer.Tools;

/// <summary>
/// A simple in-memory data store tool for storing and retrieving key-value pairs.
/// </summary>
public class DataStoreTool : McpToolBase<DataStoreParameters, DataStoreResult>
{
    private readonly IDataService _dataService;
    private readonly ILogger<DataStoreTool> _logger;

    public DataStoreTool(IDataService dataService, ILogger<DataStoreTool> logger)
    {
        _dataService = dataService;
        _logger = logger;
    }

    public override string Name => "data_store";
    public override string Description => "Store and retrieve data using key-value pairs";
    public override ToolCategory Category => ToolCategory.Resources;

    protected override async Task<DataStoreResult> ExecuteInternalAsync(
        DataStoreParameters parameters, 
        CancellationToken cancellationToken)
    {
        var operation = ValidateRequired(parameters.Operation, nameof(parameters.Operation));

        try
        {
            switch (operation.ToLower())
            {
                case "set":
                case "store":
                    var setKey = ValidateRequired(parameters.Key, nameof(parameters.Key));
                    var setValue = ValidateRequired(parameters.Value, nameof(parameters.Value));
                    
                    await _dataService.SetAsync(setKey, setValue);
                    _logger.LogInformation("Stored value for key: {Key}", setKey);
                    
                    return new DataStoreResult
                    {
                        Success = true,
                        Operation = "set",
                        Key = setKey,
                        Message = $"Value stored successfully for key '{setKey}'"
                    };

                case "get":
                case "retrieve":
                    var getKey = ValidateRequired(parameters.Key, nameof(parameters.Key));
                    var value = await _dataService.GetAsync(getKey);
                    
                    if (value == null)
                    {
                        return new DataStoreResult
                        {
                            Success = false,
                            Operation = "get",
                            Key = getKey,
                            Error = $"Key '{getKey}' not found"
                        };
                    }
                    
                    return new DataStoreResult
                    {
                        Success = true,
                        Operation = "get",
                        Key = getKey,
                        Value = value,
                        Message = $"Value retrieved for key '{getKey}'"
                    };

                case "delete":
                case "remove":
                    var deleteKey = ValidateRequired(parameters.Key, nameof(parameters.Key));
                    var deleted = await _dataService.DeleteAsync(deleteKey);
                    
                    return new DataStoreResult
                    {
                        Success = deleted,
                        Operation = "delete",
                        Key = deleteKey,
                        Message = deleted 
                            ? $"Key '{deleteKey}' deleted successfully" 
                            : $"Key '{deleteKey}' not found"
                    };

                case "list":
                case "keys":
                    var keys = await _dataService.GetKeysAsync();
                    return new DataStoreResult
                    {
                        Success = true,
                        Operation = "list",
                        Keys = keys.ToList(),
                        Message = $"Found {keys.Count()} keys"
                    };

                case "clear":
                    await _dataService.ClearAsync();
                    return new DataStoreResult
                    {
                        Success = true,
                        Operation = "clear",
                        Message = "All data cleared"
                    };

                case "exists":
                    var existsKey = ValidateRequired(parameters.Key, nameof(parameters.Key));
                    var exists = await _dataService.ExistsAsync(existsKey);
                    
                    return new DataStoreResult
                    {
                        Success = true,
                        Operation = "exists",
                        Key = existsKey,
                        Exists = exists,
                        Message = exists 
                            ? $"Key '{existsKey}' exists" 
                            : $"Key '{existsKey}' does not exist"
                    };

                default:
                    return new DataStoreResult
                    {
                        Success = false,
                        Error = $"Unknown operation: {operation}. Supported: set, get, delete, list, clear, exists"
                    };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing data store operation {Operation}", operation);
            return new DataStoreResult
            {
                Success = false,
                Operation = operation,
                Error = $"Error: {ex.Message}"
            };
        }
    }

    public override object GetInputSchema()
    {
        return new
        {
            type = "object",
            properties = new
            {
                operation = new 
                { 
                    type = "string", 
                    description = "The operation to perform",
                    @enum = new[] { "set", "get", "delete", "list", "clear", "exists" }
                },
                key = new 
                { 
                    type = "string", 
                    description = "The key for the data" 
                },
                value = new 
                { 
                    type = "string", 
                    description = "The value to store (for set operation)" 
                }
            },
            required = new[] { "operation" }
        };
    }
}

public class DataStoreParameters
{
    [Required]
    [Description("The operation to perform: set, get, delete, list, clear, exists")]
    public string Operation { get; set; } = string.Empty;

    [Description("The key for the data")]
    public string? Key { get; set; }

    [Description("The value to store (for set operation)")]
    public string? Value { get; set; }
}

public class DataStoreResult
{
    public bool Success { get; set; }
    public string? Operation { get; set; }
    public string? Key { get; set; }
    public string? Value { get; set; }
    public List<string>? Keys { get; set; }
    public bool? Exists { get; set; }
    public string? Message { get; set; }
    public string? Error { get; set; }
}

// Service interface and implementation
public interface IDataService
{
    Task SetAsync(string key, string value);
    Task<string?> GetAsync(string key);
    Task<bool> DeleteAsync(string key);
    Task<IEnumerable<string>> GetKeysAsync();
    Task ClearAsync();
    Task<bool> ExistsAsync(string key);
}

public class InMemoryDataService : IDataService
{
    private readonly Dictionary<string, string> _store = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task SetAsync(string key, string value)
    {
        await _semaphore.WaitAsync();
        try
        {
            _store[key] = value;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<string?> GetAsync(string key)
    {
        await _semaphore.WaitAsync();
        try
        {
            return _store.TryGetValue(key, out var value) ? value : null;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<bool> DeleteAsync(string key)
    {
        await _semaphore.WaitAsync();
        try
        {
            return _store.Remove(key);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<IEnumerable<string>> GetKeysAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            return _store.Keys.ToList();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task ClearAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            _store.Clear();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<bool> ExistsAsync(string key)
    {
        await _semaphore.WaitAsync();
        try
        {
            return _store.ContainsKey(key);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}