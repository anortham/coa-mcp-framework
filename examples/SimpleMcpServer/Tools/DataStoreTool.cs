using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using COA.Mcp.Framework.Base;
using COA.Mcp.Framework;
using COA.Mcp.Framework.Interfaces;
using COA.Mcp.Framework.Models;
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
                        OperationPerformed = "set",
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
                            OperationPerformed = "get",
                            Key = getKey,
                            Error = new ErrorInfo
                            {
                                Code = "KEY_NOT_FOUND",
                                Message = $"Key '{getKey}' not found"
                            }
                        };
                    }
                    
                    return new DataStoreResult
                    {
                        Success = true,
                        OperationPerformed = "get",
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
                        OperationPerformed = "delete",
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
                        OperationPerformed = "list",
                        Keys = keys.ToList(),
                        Message = $"Found {keys.Count()} keys"
                    };

                case "clear":
                    await _dataService.ClearAsync();
                    return new DataStoreResult
                    {
                        Success = true,
                        OperationPerformed = "clear",
                        Message = "All data cleared"
                    };

                case "exists":
                    var existsKey = ValidateRequired(parameters.Key, nameof(parameters.Key));
                    var exists = await _dataService.ExistsAsync(existsKey);
                    
                    return new DataStoreResult
                    {
                        Success = true,
                        OperationPerformed = "exists",
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
                        Error = new ErrorInfo
                        {
                            Code = "UNKNOWN_OPERATION",
                            Message = $"Unknown operation: {operation}. Supported: set, get, delete, list, clear, exists"
                        }
                    };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing data store operation {Operation}", operation);
            return new DataStoreResult
            {
                Success = false,
                OperationPerformed = operation,
                Error = new ErrorInfo
                {
                    Code = "PROCESSING_ERROR",
                    Message = $"Error: {ex.Message}"
                }
            };
        }
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

public class DataStoreResult : ToolResultBase
{
    public override string Operation => "data_store";
    public string? OperationPerformed { get; set; }
    public string? Key { get; set; }
    public string? Value { get; set; }
    public List<string>? Keys { get; set; }
    public bool? Exists { get; set; }
    public new string? Message { get; set; }  // Use 'new' to hide inherited member
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