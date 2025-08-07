# Token Optimization Suggestions

This document proposes targeted improvements to `TokenEstimator` (in `src/COA.Mcp.Framework.TokenOptimization/TokenEstimator.cs`) to increase accuracy, reduce allocations, improve performance on large collections, and make budgeting safer. Changes are backward-compatible unless noted.

## TL;DR (Highlights)

- Treat JSON punctuation overhead as characters first, then convert to tokens (avoid mixing units)
- Handle `IEnumerable`/`IDictionary` directly in `EstimateObject` (avoid serializing huge graphs)
- Add a relative, percentage-based safety buffer overload for token budgeting
- Use deterministic, even-coverage sampling that includes tail elements
- Reduce allocations in `EstimateString` (no `Split`), add CJK/low-space heuristics
- Optional: introduce DI-friendly instance (`ITokenEstimator`) and keep static facade
- Add quick tests to calibrate constants against a reference tokenizer (optional)

---

## 1) JSON overhead: convert chars → tokens

Problem: The code adds `JSON_STRUCTURE_OVERHEAD` and `itemsList.Count * 5` as if they were tokens. The `5` looks like a character estimate per comma/space, which mixes units.

Approach: Estimate structure characters (e.g., `[]` + commas) and convert to tokens using `CHARS_PER_TOKEN`.

```csharp
private static int ApproxStructureTokensForJson(int approxStructureChars)
{
    // Convert punctuation/structure char overhead into tokens
    return (int)Math.Ceiling(approxStructureChars / CHARS_PER_TOKEN);
}
```

Usage in collections:

```csharp
// "[" + "]" + commas between items
var structureChars = 2 + Math.Max(0, itemsList.Count - 1);
var structureTokens = ApproxStructureTokensForJson(structureChars);
return estimatedItemsTokens + structureTokens;
```

And when serializing objects:

```csharp
var json = JsonSerializer.Serialize(obj, options);
return EstimateString(json) + ApproxStructureTokensForJson( /* small baseline */ 16 );
```

Why: Keeps a single unit (tokens) derived from character counts, so array size impacts overhead correctly without overcounting.

---

## 2) Smarter `EstimateObject` for collections/dictionaries

Problem: Complex objects always serialize, which can be slow and inaccurate for large `IEnumerable`/`IDictionary` graphs.

Approach: Detect `IDictionary` and `IEnumerable` and reuse `EstimateCollection` with an item estimator. Fall back to JSON only for true complex leaf objects.

```csharp
public static int EstimateObject(object? obj, JsonSerializerOptions? options = null)
{
    if (obj == null) return 0;

    try
    {
        if (obj is string s) return EstimateString(s);

        var type = obj.GetType();
        if (type.IsPrimitive || obj is decimal or DateTime or DateTimeOffset or Guid)
            return EstimateString(obj.ToString());

        if (obj is IDictionary dict)
        {
            var list = new List<KeyValuePair<object?, object?>>(dict.Count);
            foreach (DictionaryEntry e in dict)
                list.Add(new KeyValuePair<object?, object?>(e.Key, e.Value));

            return EstimateCollection(list, kv => EstimateObject(kv.Key) + EstimateObject(kv.Value));
        }

        if (obj is IEnumerable enumerable)
        {
            var list = enumerable.Cast<object?>().ToList();
            return EstimateCollection(list, item => EstimateObject(item));
        }

        options ??= DefaultJsonOptions;
        var json = JsonSerializer.Serialize(obj, options);
        return EstimateString(json) + ApproxStructureTokensForJson(16);
    }
    catch
    {
        return EstimateString(obj.ToString()) + ApproxStructureTokensForJson(16);
    }
}

private static readonly JsonSerializerOptions DefaultJsonOptions = new()
{
    WriteIndented = false,
    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
};
```

Why: Avoids serializing entire collections just to estimate, improving speed and scaling better with large inputs.

---

## 3) Deterministic, even-coverage sampling

Problem: Current stratified sampling can miss tail elements and produce duplicate indices, skewing estimates.

Approach: Even spacing with middle-of-bucket indices, ensure coverage of first/last, guarantee uniqueness.

```csharp
private static List<int> GetSampleIndicesDeterministic(int n, int k)
{
    if (n <= k) return Enumerable.Range(0, n).ToList();

    var result = new HashSet<int>();
    var step = (double)n / k;

    for (int i = 0; i < k; i++)
    {
        var idx = (int)Math.Floor(i * step + step / 2.0);
        if (idx >= n) idx = n - 1;
        result.Add(idx);
    }

    result.Add(0);
    result.Add(n - 1);

    return result.OrderBy(x => x).Take(k).ToList();
}
```

Use in place of the existing `GetSampleIndices`.

Why: Reduces bias and increases stability for heterogeneous collections.

---

## 4) `EstimateString`: lower allocations + language-aware heuristics

Problem: `.Split()` allocates; English-centric averages undercount CJK and low-space text (code, long URLs).

Approach: Avoid `Split`; approximate word count with a single pass; detect CJK or “no-space-heavy” content and adjust `chars-per-token`.

```csharp
private const double CJK_CHARS_PER_TOKEN = 2.0;

public static int EstimateString(string? text)
{
    if (string.IsNullOrEmpty(text)) return 0;

    // Normalize whitespace (compiled regex already present)
    var normalized = WhitespaceRegex.Replace(text, " ");

    var charCount = normalized.Length;
    var wordCount = ApproxWordCount(normalized);

    var useCjkRate = ContainsCjk(normalized) || NoSpaceHeavy(normalized);
    var charsPerToken = useCjkRate ? CJK_CHARS_PER_TOKEN : CHARS_PER_TOKEN;

    var charBased = (int)Math.Ceiling(charCount / charsPerToken);
    var wordBased = (int)Math.Ceiling(wordCount * 1.3); // keep existing multiplier

    // Slightly favor the char-based estimate; it generalizes better
    return (int)Math.Round(charBased * 0.6 + wordBased * 0.4);
}

private static int ApproxWordCount(string s)
{
    int words = 0; bool inWord = false;
    foreach (var ch in s)
    {
        var isSep = ch == ' ';
        if (!isSep && !inWord) { inWord = true; words++; }
        else if (isSep && inWord) { inWord = false; }
    }
    return words;
}

private static bool NoSpaceHeavy(string s)
{
    if (s.Length < 24) return false;
    int spaces = 0; foreach (var ch in s) if (ch == ' ') spaces++;
    return ((double)spaces / s.Length) < 0.05; // <5% spaces
}

private static bool ContainsCjk(string s)
{
    foreach (var ch in s)
    {
        var u = (int)ch;
        if ((u >= 0x4E00 && u <= 0x9FFF) || // CJK Unified
            (u >= 0x3400 && u <= 0x4DBF) || // CJK Ext A
            (u >= 0x3040 && u <= 0x30FF) || // Hiragana/Katakana
            (u >= 0xAC00 && u <= 0xD7AF))   // Hangul
            return true;
    }
    return false;
}
```

Why: Improves estimation accuracy for non-English text and code without adding dependencies.

---

## 5) Safer token budgeting: relative buffer overload

Problem: Absolute buffers (10k/5k/1k) are crude across different context windows.

Approach: Keep existing API for compatibility and add a new overload that uses a percentage-based buffer with min/max clamps.

```csharp
// Existing method unchanged
public static int CalculateTokenBudget(
    int totalLimit,
    int currentUsage,
    TokenSafetyMode safetyMode = TokenSafetyMode.Default)
{
    var safetyLimit = safetyMode switch
    {
        TokenSafetyMode.Conservative => CONSERVATIVE_SAFETY_LIMIT,
        TokenSafetyMode.Minimal => MINIMUM_SAFETY_LIMIT,
        _ => DEFAULT_SAFETY_LIMIT
    };

    return Math.Max(0, totalLimit - currentUsage - safetyLimit);
}

// New overload (preferred)
public static int CalculateTokenBudget(
    int totalLimit,
    int currentUsage,
    double? safetyPercent = 0.05, // 5%
    int? minAbsoluteBuffer = 1000,
    int? maxAbsoluteBuffer = 10000)
{
    var pct = Math.Clamp(safetyPercent ?? 0.05, 0.0, 0.5);
    var bufferFromPct = (int)Math.Ceiling(totalLimit * pct);

    var buffer = bufferFromPct;
    if (minAbsoluteBuffer.HasValue) buffer = Math.Max(buffer, minAbsoluteBuffer.Value);
    if (maxAbsoluteBuffer.HasValue) buffer = Math.Min(buffer, maxAbsoluteBuffer.Value);

    return Math.Max(0, totalLimit - currentUsage - buffer);
}
```

Why: Adapts to small/large models while preventing pathologically small or large buffers.

---

## 6) `EstimateCollection`: unit-correct overhead + sampling

Combine the overhead fix and deterministic sampling:

```csharp
public static int EstimateCollection<T>(
    IEnumerable<T>? items,
    Func<T, int>? itemEstimator = null,
    int sampleSize = MAX_SAMPLE_SIZE)
{
    if (items == null) return 0;

    var itemsList = items as IList<T> ?? items.ToList();
    if (itemsList.Count == 0)
        return ApproxStructureTokensForJson(2); // "[]"

    itemEstimator ??= item => EstimateObject(item);

    // JSON array punctuation overhead (in tokens)
    var structureChars = 2 + Math.Max(0, itemsList.Count - 1);
    var structureTokens = ApproxStructureTokensForJson(structureChars);

    if (itemsList.Count <= sampleSize)
    {
        var total = 0; foreach (var item in itemsList) total += itemEstimator(item);
        return total + structureTokens;
    }

    var indices = GetSampleIndicesDeterministic(itemsList.Count, sampleSize);
    var sampleSum = 0; foreach (var i in indices) sampleSum += itemEstimator(itemsList[i]);

    var avg = (double)sampleSum / indices.Count;
    var estimatedTotal = (int)Math.Round(avg * itemsList.Count);
    return estimatedTotal + structureTokens;
}
```

---

## 7) Optional: DI-friendly instance while keeping static facade

If you already have `ITokenEstimator`, consider adding a default instance implementation and let the static class delegate to it. This improves testability and configurability (e.g., different `CHARS_PER_TOKEN` in certain services) without breaking existing calls.

Minimal pattern:

```csharp
public interface ITokenEstimator
{
    int EstimateString(string? text);
    int EstimateObject(object? obj, JsonSerializerOptions? options = null);
    int EstimateCollection<T>(IEnumerable<T>? items, Func<T, int>? itemEstimator = null, int sampleSize = 10);
}

public sealed class DefaultTokenEstimator : ITokenEstimator
{
    // Implement by reusing the static methods or moving logic here
}

// Static facade holds a default instance (can be overridden in tests/DI)
public static class TokenEstimator
{
    public static ITokenEstimator Instance { get; set; } = new DefaultTokenEstimator();
    // Static methods delegate to Instance...
}
```

---

## 8) Progressive reduction: value-aware overload (optional)

Current reduction keeps the first N items. Consider an overload that accepts a score selector to keep the highest-value subset while honoring token limits.

```csharp
public static List<T> ApplyProgressiveReduction<T>(
    IEnumerable<T> items,
    Func<T, int> itemEstimator,
    int tokenLimit,
    Func<T, double>? scoreSelector = null,
    int[]? reductionSteps = null)
{
    var list = items as IList<T> ?? items.ToList();
    if (list.Count == 0) return new List<T>();

    reductionSteps ??= new[] { 100, 75, 50, 30, 20, 10, 5 };

    IEnumerable<T> ordered = scoreSelector == null
        ? list
        : list.OrderByDescending(scoreSelector);

    foreach (var pct in reductionSteps)
    {
        var count = Math.Max(1, (list.Count * pct) / 100);
        var subset = ordered.Take(count).ToList();
        var estimatedTokens = EstimateCollection(subset, itemEstimator);
        if (estimatedTokens <= tokenLimit) return subset;
    }

    return ordered.Take(1).ToList();
}
```

---

## 9) Calibration tests (lightweight)

Add sanity tests in `tests/COA.Mcp.Framework.TokenOptimization.Tests`:

- Monotonicity: longer ASCII string has more or equal tokens than shorter
- CJK paragraph vs ASCII paragraph of same length: CJK yields more tokens
- Code block and long URL are estimated higher than plain prose of same char count
- Collections: doubling items roughly doubles estimated tokens (within tolerance)
- Huge collections: sampled estimate within ±10–15% of full estimate for synthetic data

If you can, add an opt-in test path that compares to a real tokenizer (e.g., tiktoken or OpenAI GPT tokenizer via fixtures) to tune `CHARS_PER_TOKEN` and weights. Keep it skipped by default in CI.

---

## 10) Migration & rollout

1. Implement unit-correct overhead and deterministic sampling (safe, local changes)
2. Add `EstimateObject` handling for `IEnumerable`/`IDictionary`
3. Introduce `CalculateTokenBudget` overload (leave old API intact)
4. Integrate `EstimateString` heuristics (low risk)
5. Optionally add value-aware reduction overload
6. Add tests; tune constants if needed

---

## Notes

- The previous `JSON_STRUCTURE_OVERHEAD = 50` tokens is likely too high and static; converting chars → tokens scales naturally with array size and nested objects.
- Heuristics keep complexity low and avoid bringing in heavy tokenizer libraries while being more robust across languages.
- All changes preserve existing public behavior unless callers opt into the new overloads/APIs.

---

## Appendix: Full snippets together (for convenience)

The sections above can be applied independently. When merging, prefer:

- `ApproxStructureTokensForJson`
- Updated `EstimateObject`
- Updated `EstimateCollection`
- Deterministic sampling helper
- `EstimateString` with CJK/no-space heuristics
- New `CalculateTokenBudget` overload

This strikes a good balance of accuracy and performance without external dependencies.
