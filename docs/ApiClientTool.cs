using COA.Mcp.Framework.Base;
using COA.Mcp.Framework.Models;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text;

// HTTP API client template
// Copy this and customize for your API needs

public class ApiClientTool : McpToolBase<ApiClientParams, ApiClientResult>
{
    private static readonly HttpClient _httpClient = new();
    
    public override string Name => "api_client";
    public override string Description => "Makes HTTP requests to external APIs";
    
    protected override async Task<ApiClientResult> ExecuteInternalAsync(
        ApiClientParams parameters, CancellationToken cancellationToken)
    {
        // Validate required parameters
        ValidateRequired(parameters.Url, nameof(parameters.Url));
        ValidateRequired(parameters.Method, nameof(parameters.Method));
        
        var url = parameters.Url!;
        var method = parameters.Method!.ToUpper();
        
        try
        {
            // Validate URL
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                throw new ArgumentException($"Invalid URL: {url}");
                
            // Create request
            var request = new HttpRequestMessage(new HttpMethod(method), uri);
            
            // Add headers
            if (parameters.Headers != null)
            {
                foreach (var header in parameters.Headers)
                {
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }
            
            // Add body for POST/PUT/PATCH
            if (!string.IsNullOrEmpty(parameters.Body) && 
                (method == "POST" || method == "PUT" || method == "PATCH"))
            {
                var contentType = parameters.ContentType ?? "application/json";
                request.Content = new StringContent(parameters.Body, Encoding.UTF8, contentType);
            }
            
            // Set timeout
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(parameters.TimeoutSeconds ?? 30));
            
            // Make request
            var response = await _httpClient.SendAsync(request, cts.Token);
            
            // Read response
            var responseBody = await response.Content.ReadAsStringAsync(cts.Token);
            var responseHeaders = response.Headers
                .Concat(response.Content.Headers)
                .ToDictionary(h => h.Key, h => string.Join(", ", h.Value));
            
            return new ApiClientResult
            {
                Success = true,
                Url = url,
                Method = method,
                StatusCode = (int)response.StatusCode,
                StatusText = response.ReasonPhrase ?? "",
                Headers = responseHeaders,
                Body = responseBody,
                ContentType = response.Content.Headers.ContentType?.MediaType,
                ContentLength = response.Content.Headers.ContentLength
            };
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            return new ApiClientResult
            {
                Success = false,
                Error = new ErrorInfo
                {
                    Code = "TIMEOUT",
                    Message = $"Request timed out after {parameters.TimeoutSeconds ?? 30} seconds"
                }
            };
        }
        catch (HttpRequestException ex)
        {
            return new ApiClientResult
            {
                Success = false,
                Error = new ErrorInfo
                {
                    Code = "HTTP_ERROR",
                    Message = $"HTTP error: {ex.Message}"
                }
            };
        }
        catch (JsonException ex)
        {
            return new ApiClientResult
            {
                Success = false,
                Error = new ErrorInfo
                {
                    Code = "JSON_ERROR", 
                    Message = $"JSON parsing error: {ex.Message}"
                }
            };
        }
    }
}

public class ApiClientParams
{
    [Required]
    public string? Url { get; set; }
    
    [Required]
    public string? Method { get; set; } // GET, POST, PUT, DELETE, etc.
    
    public Dictionary<string, string>? Headers { get; set; }
    
    public string? Body { get; set; } // For POST/PUT requests
    
    public string? ContentType { get; set; } // Default: application/json
    
    public int? TimeoutSeconds { get; set; } // Default: 30
}

public class ApiClientResult : ToolResultBase
{
    public override string Operation => "api_client";
    
    public string? Url { get; set; }
    public string? Method { get; set; }
    public int StatusCode { get; set; }
    public string? StatusText { get; set; }
    public Dictionary<string, string>? Headers { get; set; }
    public string? Body { get; set; }
    public string? ContentType { get; set; }
    public long? ContentLength { get; set; }
}

// Example usage:
/*
// GET request
{
  "url": "https://api.github.com/users/octocat",
  "method": "GET",
  "headers": {
    "User-Agent": "MCP-Client/1.0"
  }
}

// POST request
{
  "url": "https://jsonplaceholder.typicode.com/posts",
  "method": "POST",
  "body": "{\"title\": \"foo\", \"body\": \"bar\", \"userId\": 1}",
  "contentType": "application/json"
}
*/