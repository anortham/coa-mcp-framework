using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace COA.Mcp.Framework.TokenOptimization.Intelligence
{
    /// <summary>
    /// Default implementation of insight template provider
    /// </summary>
    public class InsightTemplateProvider : IInsightTemplateProvider
    {
        private readonly Dictionary<Type, List<IInsightTemplate>> _templatesByType;
        private readonly List<IInsightTemplate> _universalTemplates;
        private readonly ILogger<InsightTemplateProvider> _logger;
        private readonly object _lock = new object();

        public InsightTemplateProvider(ILogger<InsightTemplateProvider> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _templatesByType = new Dictionary<Type, List<IInsightTemplate>>();
            _universalTemplates = new List<IInsightTemplate>();

            RegisterDefaultTemplates();
        }

        public Task<IEnumerable<IInsightTemplate>> GetTemplatesAsync(Type dataType, InsightContext context)
        {
            if (dataType == null) throw new ArgumentNullException(nameof(dataType));
            if (context == null) throw new ArgumentNullException(nameof(context));

            var templates = new List<IInsightTemplate>();

            lock (_lock)
            {
                // Add type-specific templates
                if (_templatesByType.TryGetValue(dataType, out var typeTemplates))
                {
                    templates.AddRange(typeTemplates);
                }

                // Add templates for base types and interfaces
                foreach (var baseType in GetBaseTypesAndInterfaces(dataType))
                {
                    if (_templatesByType.TryGetValue(baseType, out var baseTemplates))
                    {
                        templates.AddRange(baseTemplates);
                    }
                }

                // Add universal templates
                templates.AddRange(_universalTemplates);
            }

            // Filter by applicability and remove duplicates
            var applicableTemplates = templates
                .Where(t => t.IsApplicable(context))
                .GroupBy(t => t.Name)
                .Select(g => g.First())
                .OrderByDescending(t => t.Priority)
                .ToList();

            _logger.LogDebug("Found {Count} applicable templates for type {Type}",
                applicableTemplates.Count, dataType.Name);

            return Task.FromResult<IEnumerable<IInsightTemplate>>(applicableTemplates);
        }

        public bool HasTemplatesFor(Type dataType)
        {
            if (dataType == null) return false;

            lock (_lock)
            {
                if (_templatesByType.ContainsKey(dataType) || _universalTemplates.Any())
                    return true;

                // Check base types and interfaces
                return GetBaseTypesAndInterfaces(dataType)
                    .Any(t => _templatesByType.ContainsKey(t));
            }
        }

        public void RegisterTemplate(IInsightTemplate template)
        {
            if (template == null) throw new ArgumentNullException(nameof(template));

            lock (_lock)
            {
                foreach (var type in template.SupportedTypes)
                {
                    if (!_templatesByType.TryGetValue(type, out var templates))
                    {
                        templates = new List<IInsightTemplate>();
                        _templatesByType[type] = templates;
                    }

                    if (!templates.Any(t => t.Name == template.Name))
                    {
                        templates.Add(template);
                        _logger.LogDebug("Registered template {Name} for type {Type}",
                            template.Name, type.Name);
                    }
                }

                // If template supports object type, add to universal templates
                if (template.SupportedTypes.Contains(typeof(object)))
                {
                    if (!_universalTemplates.Any(t => t.Name == template.Name))
                    {
                        _universalTemplates.Add(template);
                    }
                }
            }
        }

        public void RegisterTemplates(Type dataType, params IInsightTemplate[] templates)
        {
            if (dataType == null) throw new ArgumentNullException(nameof(dataType));
            if (templates == null) throw new ArgumentNullException(nameof(templates));

            lock (_lock)
            {
                if (!_templatesByType.TryGetValue(dataType, out var typeTemplates))
                {
                    typeTemplates = new List<IInsightTemplate>();
                    _templatesByType[dataType] = typeTemplates;
                }

                foreach (var template in templates)
                {
                    if (!typeTemplates.Any(t => t.Name == template.Name))
                    {
                        typeTemplates.Add(template);
                        _logger.LogDebug("Registered template {Name} for type {Type}",
                            template.Name, dataType.Name);
                    }
                }
            }
        }

        private void RegisterDefaultTemplates()
        {
            // Register built-in templates
            RegisterTemplate(new EmptyCollectionInsightTemplate());
            RegisterTemplate(new LargeCollectionInsightTemplate());
            RegisterTemplate(new TruncatedResultsInsightTemplate());
            RegisterTemplate(new PerformanceInsightTemplate());
            RegisterTemplate(new PatternDetectionInsightTemplate());
            RegisterTemplate(new ErrorRateInsightTemplate());

            _logger.LogInformation("Registered {Count} default insight templates",
                _templatesByType.Sum(kvp => kvp.Value.Count) + _universalTemplates.Count);
        }

        private IEnumerable<Type> GetBaseTypesAndInterfaces(Type type)
        {
            var types = new HashSet<Type>();

            // Add interfaces
            foreach (var interfaceType in type.GetInterfaces())
            {
                types.Add(interfaceType);
            }

            // Add base types
            var baseType = type.BaseType;
            while (baseType != null && baseType != typeof(object))
            {
                types.Add(baseType);
                baseType = baseType.BaseType;
            }

            return types;
        }
    }
}