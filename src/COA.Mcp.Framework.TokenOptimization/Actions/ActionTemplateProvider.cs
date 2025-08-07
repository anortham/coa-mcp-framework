using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace COA.Mcp.Framework.TokenOptimization.Actions
{
    /// <summary>
    /// Default implementation of action template provider
    /// </summary>
    public class ActionTemplateProvider : IActionTemplateProvider, IDisposable
    {
        private readonly Dictionary<Type, List<IActionTemplate>> _templatesByType;
        private readonly List<IActionTemplate> _universalTemplates;
        private readonly ILogger<ActionTemplateProvider> _logger;
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

        public ActionTemplateProvider(ILogger<ActionTemplateProvider> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _templatesByType = new Dictionary<Type, List<IActionTemplate>>();
            _universalTemplates = new List<IActionTemplate>();

            RegisterDefaultTemplates();
        }

        public Task<IEnumerable<IActionTemplate>> GetTemplatesAsync(Type dataType, ActionContext context)
        {
            if (dataType == null) throw new ArgumentNullException(nameof(dataType));
            if (context == null) throw new ArgumentNullException(nameof(context));

            var templates = new List<IActionTemplate>();

            _lock.EnterReadLock();
            try
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
            finally
            {
                _lock.ExitReadLock();
            }

            // Filter by applicability and remove duplicates
            var applicableTemplates = templates
                .Where(t => t.IsApplicable(context))
                .GroupBy(t => t.Name)
                .Select(g => g.First())
                .OrderByDescending(t => t.Priority)
                .ToList();

            _logger.LogDebug("Found {Count} applicable action templates for type {Type}",
                applicableTemplates.Count, dataType.Name);

            return Task.FromResult<IEnumerable<IActionTemplate>>(applicableTemplates);
        }

        public bool HasTemplatesFor(Type dataType)
        {
            if (dataType == null) return false;

            _lock.EnterReadLock();
            try
            {
                if (_templatesByType.ContainsKey(dataType) || _universalTemplates.Any())
                    return true;

                // Check base types and interfaces
                return GetBaseTypesAndInterfaces(dataType)
                    .Any(t => _templatesByType.ContainsKey(t));
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public void RegisterTemplate(IActionTemplate template)
        {
            if (template == null) throw new ArgumentNullException(nameof(template));

            _lock.EnterWriteLock();
            try
            {
                foreach (var type in template.SupportedTypes)
                {
                    if (!_templatesByType.TryGetValue(type, out var templates))
                    {
                        templates = new List<IActionTemplate>();
                        _templatesByType[type] = templates;
                    }

                    if (!templates.Any(t => t.Name == template.Name))
                    {
                        templates.Add(template);
                        _logger.LogDebug("Registered action template {Name} for type {Type}",
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
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public void RegisterTemplates(Type dataType, params IActionTemplate[] templates)
        {
            if (dataType == null) throw new ArgumentNullException(nameof(dataType));
            if (templates == null) throw new ArgumentNullException(nameof(templates));

            _lock.EnterWriteLock();
            try
            {
                if (!_templatesByType.TryGetValue(dataType, out var typeTemplates))
                {
                    typeTemplates = new List<IActionTemplate>();
                    _templatesByType[dataType] = typeTemplates;
                }

                foreach (var template in templates)
                {
                    if (!typeTemplates.Any(t => t.Name == template.Name))
                    {
                        typeTemplates.Add(template);
                        _logger.LogDebug("Registered action template {Name} for type {Type}",
                            template.Name, dataType.Name);
                    }
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        private void RegisterDefaultTemplates()
        {
            // Register built-in templates
            RegisterTemplate(new EmptyCollectionActionTemplate());
            RegisterTemplate(new LargeCollectionActionTemplate());
            RegisterTemplate(new PaginationActionTemplate());
            RegisterTemplate(new ExportActionTemplate());
            RegisterTemplate(new AnalysisActionTemplate());
            RegisterTemplate(new DrillDownActionTemplate());
            RegisterTemplate(new ComparisonActionTemplate());

            _logger.LogInformation("Registered {Count} default action templates",
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

        public void Dispose()
        {
            _lock?.Dispose();
        }
    }
}