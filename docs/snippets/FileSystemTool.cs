using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Models;
using System.ComponentModel.DataAnnotations;

// File system operations template
// Copy this and customize for your needs

public class FileSystemTool : McpToolBase<FileSystemParams, FileSystemResult>
{
    public override string Name => "filesystem";
    public override string Description => "File and directory operations";
    
    protected override async Task<FileSystemResult> ExecuteInternalAsync(
        FileSystemParams parameters, CancellationToken cancellationToken)
    {
        // Validate required parameters
        ValidateRequired(parameters.Operation, nameof(parameters.Operation));
        ValidateRequired(parameters.Path, nameof(parameters.Path));
        
        var operation = parameters.Operation!.ToLower();
        var path = parameters.Path!;
        
        try
        {
            return operation switch
            {
                "read" => await ReadFileAsync(path, cancellationToken),
                "write" => await WriteFileAsync(path, parameters.Content ?? "", cancellationToken),
                "list" => await ListDirectoryAsync(path, cancellationToken),
                "exists" => CheckExistsAsync(path),
                "delete" => await DeleteAsync(path, cancellationToken),
                _ => throw new ArgumentException($"Unknown operation: {parameters.Operation}")
            };
        }
        catch (UnauthorizedAccessException ex)
        {
            return new FileSystemResult
            {
                Success = false,
                Error = new ErrorInfo
                {
                    Code = "ACCESS_DENIED",
                    Message = $"Access denied: {ex.Message}"
                }
            };
        }
        catch (DirectoryNotFoundException ex)
        {
            return new FileSystemResult
            {
                Success = false,
                Error = new ErrorInfo
                {
                    Code = "PATH_NOT_FOUND",
                    Message = $"Path not found: {ex.Message}"
                }
            };
        }
        catch (IOException ex)
        {
            return new FileSystemResult
            {
                Success = false,
                Error = new ErrorInfo
                {
                    Code = "IO_ERROR",
                    Message = $"I/O error: {ex.Message}"
                }
            };
        }
    }
    
    private async Task<FileSystemResult> ReadFileAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"File not found: {path}");
            
        var content = await File.ReadAllTextAsync(path, cancellationToken);
        
        return new FileSystemResult
        {
            Success = true,
            Operation = "read",
            Path = path,
            Content = content,
            Size = new FileInfo(path).Length
        };
    }
    
    private async Task<FileSystemResult> WriteFileAsync(string path, string content, CancellationToken cancellationToken)
    {
        // Create directory if it doesn't exist
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        await File.WriteAllTextAsync(path, content, cancellationToken);
        
        return new FileSystemResult
        {
            Success = true,
            Operation = "write",
            Path = path,
            Size = new FileInfo(path).Length
        };
    }
    
    private async Task<FileSystemResult> ListDirectoryAsync(string path, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(path))
            throw new DirectoryNotFoundException($"Directory not found: {path}");
            
        await Task.CompletedTask; // Directory operations are synchronous
        
        var entries = Directory.GetFileSystemEntries(path)
            .Select(entry => new FileSystemEntry
            {
                Name = Path.GetFileName(entry),
                Path = entry,
                IsDirectory = Directory.Exists(entry),
                Size = Directory.Exists(entry) ? null : new FileInfo(entry).Length
            })
            .ToList();
        
        return new FileSystemResult
        {
            Success = true,
            Operation = "list",
            Path = path,
            Entries = entries
        };
    }
    
    private FileSystemResult CheckExistsAsync(string path)
    {
        var exists = File.Exists(path) || Directory.Exists(path);
        var isDirectory = Directory.Exists(path);
        
        return new FileSystemResult
        {
            Success = true,
            Operation = "exists",
            Path = path,
            Exists = exists,
            IsDirectory = isDirectory
        };
    }
    
    private async Task<FileSystemResult> DeleteAsync(string path, CancellationToken cancellationToken)
    {
        await Task.CompletedTask; // Delete operations are synchronous
        
        if (File.Exists(path))
        {
            File.Delete(path);
        }
        else if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
        else
        {
            throw new FileNotFoundException($"Path not found: {path}");
        }
        
        return new FileSystemResult
        {
            Success = true,
            Operation = "delete",
            Path = path
        };
    }
}

public class FileSystemParams
{
    [Required]
    public string? Operation { get; set; } // read, write, list, exists, delete
    
    [Required]
    public string? Path { get; set; }
    
    public string? Content { get; set; } // For write operations
}

public class FileSystemResult : ToolResultBase
{
    public override string Operation => "filesystem";
    
    public string? Path { get; set; }
    public string? Content { get; set; }
    public long? Size { get; set; }
    public bool? Exists { get; set; }
    public bool? IsDirectory { get; set; }
    public List<FileSystemEntry>? Entries { get; set; }
}

public class FileSystemEntry
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public bool IsDirectory { get; set; }
    public long? Size { get; set; }
}