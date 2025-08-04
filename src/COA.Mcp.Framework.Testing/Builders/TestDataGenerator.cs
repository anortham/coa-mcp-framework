using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace COA.Mcp.Framework.Testing.Builders
{
    /// <summary>
    /// Generates test data for various testing scenarios.
    /// </summary>
    public class TestDataGenerator
    {
        /// <summary>
        /// Gets the random instance used for generation.
        /// </summary>
        public readonly Random _random;

        /// <summary>
        /// Initializes a new instance of the <see cref="TestDataGenerator"/> class.
        /// </summary>
        /// <param name="seed">Optional seed for reproducible data.</param>
        public TestDataGenerator(int? seed = null)
        {
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        #region String Generation

        /// <summary>
        /// Generates a random string of specified length.
        /// </summary>
        /// <param name="length">The length of the string.</param>
        /// <param name="charset">The character set to use.</param>
        /// <returns>A random string.</returns>
        public string GenerateString(int length, string charset = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789")
        {
            var result = new StringBuilder(length);
            for (int i = 0; i < length; i++)
            {
                result.Append(charset[_random.Next(charset.Length)]);
            }
            return result.ToString();
        }

        /// <summary>
        /// Generates a string that will have approximately the specified token count.
        /// </summary>
        /// <param name="targetTokens">Target number of tokens.</param>
        /// <returns>A string with approximately the target token count.</returns>
        public string GenerateStringWithTokens(int targetTokens)
        {
            // Approximate: 1 token ≈ 4 characters
            var targetChars = targetTokens * 4;
            return GenerateLorem(targetChars);
        }

        /// <summary>
        /// Generates Lorem Ipsum text of specified character length.
        /// </summary>
        /// <param name="characterCount">Target character count.</param>
        /// <returns>Lorem Ipsum text.</returns>
        public string GenerateLorem(int characterCount)
        {
            const string lorem = "Lorem ipsum dolor sit amet consectetur adipiscing elit sed do eiusmod tempor incididunt ut labore et dolore magna aliqua ";
            var result = new StringBuilder(characterCount);
            
            while (result.Length < characterCount)
            {
                result.Append(lorem);
            }
            
            return result.ToString(0, Math.Min(result.Length, characterCount));
        }

        #endregion

        #region File Path Generation

        /// <summary>
        /// Generates a random file path.
        /// </summary>
        /// <param name="depth">Directory depth.</param>
        /// <param name="extension">File extension.</param>
        /// <returns>A random file path.</returns>
        public string GenerateFilePath(int depth = 3, string extension = ".cs")
        {
            var parts = new List<string> { "C:", "src" };
            
            for (int i = 0; i < depth; i++)
            {
                parts.Add(GenerateIdentifier());
            }
            
            parts.Add(GenerateIdentifier() + extension);
            return string.Join("\\", parts);
        }

        /// <summary>
        /// Generates multiple file paths with a common structure.
        /// </summary>
        /// <param name="count">Number of paths to generate.</param>
        /// <param name="baseDir">Base directory.</param>
        /// <param name="extensions">Possible extensions.</param>
        /// <returns>Collection of file paths.</returns>
        public IEnumerable<string> GenerateFilePaths(
            int count, 
            string baseDir = "C:\\src\\TestProject",
            params string[] extensions)
        {
            if (extensions.Length == 0)
                extensions = new[] { ".cs", ".csproj", ".json", ".md" };

            for (int i = 0; i < count; i++)
            {
                var subDir = _random.Next(3) switch
                {
                    0 => "Controllers",
                    1 => "Services",
                    _ => "Models"
                };
                
                var fileName = GenerateIdentifier();
                var extension = extensions[_random.Next(extensions.Length)];
                
                yield return $"{baseDir}\\{subDir}\\{fileName}{extension}";
            }
        }

        #endregion

        #region Code Generation

        /// <summary>
        /// Generates a valid C# identifier.
        /// </summary>
        /// <param name="prefix">Optional prefix.</param>
        /// <returns>A valid identifier.</returns>
        public string GenerateIdentifier(string prefix = "")
        {
            var identifiers = new[]
            {
                "User", "Product", "Order", "Customer", "Service",
                "Manager", "Controller", "Repository", "Factory", "Builder",
                "Handler", "Processor", "Validator", "Calculator", "Helper"
            };
            
            var suffix = _random.Next(100, 999);
            var baseIdentifier = identifiers[_random.Next(identifiers.Length)];
            
            return string.IsNullOrEmpty(prefix) 
                ? $"{baseIdentifier}{suffix}"
                : $"{prefix}{baseIdentifier}{suffix}";
        }

        /// <summary>
        /// Generates a simple C# class.
        /// </summary>
        /// <param name="className">The class name.</param>
        /// <param name="propertyCount">Number of properties.</param>
        /// <returns>C# class code.</returns>
        public string GenerateClass(string? className = null, int propertyCount = 3)
        {
            className ??= GenerateIdentifier();
            var sb = new StringBuilder();
            
            sb.AppendLine($"public class {className}");
            sb.AppendLine("{");
            
            for (int i = 0; i < propertyCount; i++)
            {
                var propType = _random.Next(4) switch
                {
                    0 => "string",
                    1 => "int",
                    2 => "bool",
                    _ => "DateTime"
                };
                
                var propName = GenerateIdentifier("Property");
                sb.AppendLine($"    public {propType} {propName} {{ get; set; }}");
            }
            
            sb.AppendLine("}");
            return sb.ToString();
        }

        #endregion

        #region Collection Generation

        /// <summary>
        /// Generates a collection of items.
        /// </summary>
        /// <typeparam name="T">The item type.</typeparam>
        /// <param name="count">Number of items.</param>
        /// <param name="generator">Function to generate each item.</param>
        /// <returns>Collection of items.</returns>
        public List<T> GenerateCollection<T>(int count, Func<int, T> generator)
        {
            return Enumerable.Range(0, count).Select(generator).ToList();
        }

        /// <summary>
        /// Generates a collection with realistic distribution.
        /// </summary>
        /// <typeparam name="T">The item type.</typeparam>
        /// <param name="totalCount">Total number of items.</param>
        /// <param name="categories">Category generators.</param>
        /// <returns>Mixed collection.</returns>
        public List<T> GenerateMixedCollection<T>(
            int totalCount,
            params (Func<int, T> generator, double weight)[] categories)
        {
            var results = new List<T>();
            var totalWeight = categories.Sum(c => c.weight);
            
            foreach (var (generator, weight) in categories)
            {
                var count = (int)(totalCount * weight / totalWeight);
                results.AddRange(GenerateCollection(count, generator));
            }
            
            // Add remaining items to reach exact count
            while (results.Count < totalCount && categories.Length > 0)
            {
                var (generator, _) = categories[_random.Next(categories.Length)];
                results.Add(generator(results.Count));
            }
            
            // Shuffle the results
            return results.OrderBy(x => _random.Next()).ToList();
        }

        #endregion

        #region Data Patterns

        /// <summary>
        /// Generates data with a specific pattern for testing pattern detection.
        /// </summary>
        /// <param name="patternType">Type of pattern to generate.</param>
        /// <param name="itemCount">Number of items.</param>
        /// <returns>Patterned data.</returns>
        public List<object> GeneratePatternedData(string patternType, int itemCount)
        {
            return patternType.ToLower() switch
            {
                "duplicate" => GenerateDuplicatePattern(itemCount),
                "sequential" => GenerateSequentialPattern(itemCount),
                "alternating" => GenerateAlternatingPattern(itemCount),
                "clustered" => GenerateClusteredPattern(itemCount),
                _ => GenerateRandomPattern(itemCount)
            };
        }

        private List<object> GenerateDuplicatePattern(int count)
        {
            var unique = _random.Next(1, Math.Max(2, count / 5));
            var values = Enumerable.Range(0, unique).Select(i => (object)$"Value{i}").ToList();
            
            return Enumerable.Range(0, count)
                .Select(_ => values[_random.Next(values.Count)])
                .ToList();
        }

        private List<object> GenerateSequentialPattern(int count)
        {
            return Enumerable.Range(0, count)
                .Select(i => (object)$"Item{i:D4}")
                .ToList();
        }

        private List<object> GenerateAlternatingPattern(int count)
        {
            return Enumerable.Range(0, count)
                .Select(i => (object)(i % 2 == 0 ? $"Even{i}" : $"Odd{i}"))
                .ToList();
        }

        private List<object> GenerateClusteredPattern(int count)
        {
            var results = new List<object>();
            var clusterSize = 5;
            var currentCluster = 0;
            
            for (int i = 0; i < count; i++)
            {
                if (i % clusterSize == 0)
                    currentCluster++;
                    
                results.Add($"Cluster{currentCluster}_Item{i % clusterSize}");
            }
            
            return results;
        }

        private List<object> GenerateRandomPattern(int count)
        {
            return Enumerable.Range(0, count)
                .Select(_ => (object)GenerateString(10))
                .ToList();
        }

        #endregion

        #region Large Data Generation

        /// <summary>
        /// Generates data that will exceed token limits when serialized.
        /// </summary>
        /// <param name="targetTokens">Target token count.</param>
        /// <returns>Large object that exceeds token limit.</returns>
        public object GenerateLargeObject(int targetTokens)
        {
            // Create nested structure that will generate many tokens
            var itemsNeeded = targetTokens / 50; // Approximate tokens per item
            
            return new
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                Data = GenerateCollection(itemsNeeded, i => new
                {
                    Index = i,
                    Name = GenerateIdentifier($"Item{i}"),
                    Description = GenerateLorem(100),
                    Properties = new
                    {
                        Value1 = _random.Next(1000),
                        Value2 = _random.NextDouble(),
                        Value3 = GenerateString(20),
                        Tags = GenerateCollection(5, _ => GenerateIdentifier("Tag"))
                    }
                })
            };
        }

        #endregion

        #region Static Factory Methods

        /// <summary>
        /// Creates a generator with a specific seed for reproducible tests.
        /// </summary>
        /// <param name="seed">The random seed.</param>
        /// <returns>A seeded generator.</returns>
        public static TestDataGenerator WithSeed(int seed)
        {
            return new TestDataGenerator(seed);
        }

        /// <summary>
        /// Creates a generator for specific data domain.
        /// </summary>
        /// <param name="domain">The data domain (e.g., "ecommerce", "healthcare").</param>
        /// <returns>A domain-specific generator.</returns>
        public static TestDataGenerator ForDomain(string domain)
        {
            // Could be extended to use domain-specific vocabularies
            return new TestDataGenerator();
        }

        #endregion
    }

    /// <summary>
    /// Provides quick access to common test data scenarios.
    /// </summary>
    public static class TestData
    {
        private static readonly TestDataGenerator Generator = new();

        /// <summary>
        /// Gets a small collection (5-10 items).
        /// </summary>
        public static List<T> SmallCollection<T>(Func<int, T> generator)
            => Generator.GenerateCollection(Generator._random.Next(5, 11), generator);

        /// <summary>
        /// Gets a medium collection (50-100 items).
        /// </summary>
        public static List<T> MediumCollection<T>(Func<int, T> generator)
            => Generator.GenerateCollection(Generator._random.Next(50, 101), generator);

        /// <summary>
        /// Gets a large collection (500-1000 items).
        /// </summary>
        public static List<T> LargeCollection<T>(Func<int, T> generator)
            => Generator.GenerateCollection(Generator._random.Next(500, 1001), generator);

        /// <summary>
        /// Gets an empty collection.
        /// </summary>
        public static List<T> EmptyCollection<T>()
            => new List<T>();

        /// <summary>
        /// Gets a single item collection.
        /// </summary>
        public static List<T> SingleItem<T>(T item)
            => new List<T> { item };
    }
}