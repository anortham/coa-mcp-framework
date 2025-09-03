using System.Threading.Tasks;

namespace COA.Mcp.Framework.Interfaces
{
    /// <summary>
    /// Minimal typed response builder interface to decouple framework from specific builder implementations.
    /// </summary>
    public interface IResponseBuilder<TInput, TResult>
    {
        Task<TResult> BuildResponseAsync(TInput data, ResponseBuildContext context);
    }

    /// <summary>
    /// Minimal response build context used by framework helpers.
    /// Implementation-specific builders may map these to richer contexts.
    /// </summary>
    public class ResponseBuildContext
    {
        public string ResponseMode { get; set; } = "full";
        public int? TokenLimit { get; set; }
        public string? ToolName { get; set; }
    }
}
