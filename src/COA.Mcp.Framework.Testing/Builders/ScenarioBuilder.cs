using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace COA.Mcp.Framework.Testing.Builders
{
    /// <summary>
    /// Builds complex test scenarios with multiple steps and validations.
    /// </summary>
    public class ScenarioBuilder
    {
        private readonly string _name;
        private readonly List<ScenarioStep> _steps = new();
        private readonly Dictionary<string, object> _context = new();
        private Action<Dictionary<string, object>>? _setup;
        private Action<Dictionary<string, object>>? _teardown;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScenarioBuilder"/> class.
        /// </summary>
        /// <param name="name">The scenario name.</param>
        public ScenarioBuilder(string name)
        {
            _name = name;
        }

        /// <summary>
        /// Adds a setup action to run before the scenario.
        /// </summary>
        /// <param name="setup">The setup action.</param>
        /// <returns>The builder for chaining.</returns>
        public ScenarioBuilder WithSetup(Action<Dictionary<string, object>> setup)
        {
            _setup = setup;
            return this;
        }

        /// <summary>
        /// Adds a teardown action to run after the scenario.
        /// </summary>
        /// <param name="teardown">The teardown action.</param>
        /// <returns>The builder for chaining.</returns>
        public ScenarioBuilder WithTeardown(Action<Dictionary<string, object>> teardown)
        {
            _teardown = teardown;
            return this;
        }

        /// <summary>
        /// Adds initial context data.
        /// </summary>
        /// <param name="key">The context key.</param>
        /// <param name="value">The context value.</param>
        /// <returns>The builder for chaining.</returns>
        public ScenarioBuilder WithContext(string key, object value)
        {
            _context[key] = value;
            return this;
        }

        /// <summary>
        /// Adds a step to the scenario.
        /// </summary>
        /// <param name="description">Step description.</param>
        /// <param name="action">The action to perform.</param>
        /// <returns>The builder for chaining.</returns>
        public ScenarioBuilder AddStep(string description, Action<Dictionary<string, object>> action)
        {
            _steps.Add(new ScenarioStep
            {
                Description = description,
                Action = context =>
                {
                    action(context);
                    return Task.CompletedTask;
                }
            });
            return this;
        }

        /// <summary>
        /// Adds an async step to the scenario.
        /// </summary>
        /// <param name="description">Step description.</param>
        /// <param name="action">The async action to perform.</param>
        /// <returns>The builder for chaining.</returns>
        public ScenarioBuilder AddAsyncStep(string description, Func<Dictionary<string, object>, Task> action)
        {
            _steps.Add(new ScenarioStep
            {
                Description = description,
                Action = action
            });
            return this;
        }

        /// <summary>
        /// Adds a step with validation.
        /// </summary>
        /// <param name="description">Step description.</param>
        /// <param name="action">The action to perform.</param>
        /// <param name="validation">The validation to run after the action.</param>
        /// <returns>The builder for chaining.</returns>
        public ScenarioBuilder AddStepWithValidation(
            string description,
            Action<Dictionary<string, object>> action,
            Action<Dictionary<string, object>> validation)
        {
            _steps.Add(new ScenarioStep
            {
                Description = description,
                Action = context =>
                {
                    action(context);
                    return Task.CompletedTask;
                },
                Validation = validation
            });
            return this;
        }

        /// <summary>
        /// Adds a conditional step that only runs if a condition is met.
        /// </summary>
        /// <param name="description">Step description.</param>
        /// <param name="condition">The condition to check.</param>
        /// <param name="action">The action to perform if condition is true.</param>
        /// <returns>The builder for chaining.</returns>
        public ScenarioBuilder AddConditionalStep(
            string description,
            Func<Dictionary<string, object>, bool> condition,
            Action<Dictionary<string, object>> action)
        {
            _steps.Add(new ScenarioStep
            {
                Description = description,
                Condition = condition,
                Action = context =>
                {
                    action(context);
                    return Task.CompletedTask;
                }
            });
            return this;
        }

        /// <summary>
        /// Adds a delay step.
        /// </summary>
        /// <param name="milliseconds">Delay in milliseconds.</param>
        /// <param name="description">Optional description.</param>
        /// <returns>The builder for chaining.</returns>
        public ScenarioBuilder AddDelay(int milliseconds, string? description = null)
        {
            _steps.Add(new ScenarioStep
            {
                Description = description ?? $"Wait {milliseconds}ms",
                Action = async _ => await Task.Delay(milliseconds)
            });
            return this;
        }

        /// <summary>
        /// Builds and returns the scenario.
        /// </summary>
        /// <returns>The built scenario.</returns>
        public TestScenario Build()
        {
            return new TestScenario(_name, _steps, _context, _setup, _teardown);
        }

        /// <summary>
        /// Creates a scenario for testing successful operations.
        /// </summary>
        /// <param name="toolName">The tool being tested.</param>
        /// <returns>A scenario builder.</returns>
        public static ScenarioBuilder SuccessScenario(string toolName)
        {
            return new ScenarioBuilder($"{toolName}_Success")
                .AddStep("Prepare test data", ctx =>
                {
                    ctx["testData"] = new TestDataGenerator().GenerateCollection(10, i => new { Id = i, Name = $"Item{i}" });
                })
                .AddStep("Execute tool", ctx =>
                {
                    ctx["startTime"] = DateTime.UtcNow;
                })
                .AddStepWithValidation("Verify results", 
                    ctx => ctx["endTime"] = DateTime.UtcNow,
                    ctx =>
                    {
                        var duration = ((DateTime)ctx["endTime"] - (DateTime)ctx["startTime"]).TotalMilliseconds;
                        if (duration > 1000)
                            throw new Exception($"Tool took too long: {duration}ms");
                    });
        }

        /// <summary>
        /// Creates a scenario for testing error handling.
        /// </summary>
        /// <param name="toolName">The tool being tested.</param>
        /// <returns>A scenario builder.</returns>
        public static ScenarioBuilder ErrorScenario(string toolName)
        {
            return new ScenarioBuilder($"{toolName}_Error")
                .AddStep("Prepare invalid data", ctx =>
                {
                    ctx["invalidData"] = null;
                })
                .AddStep("Execute tool expecting error", ctx =>
                {
                    ctx["expectError"] = true;
                });
        }

        /// <summary>
        /// Creates a scenario for testing performance.
        /// </summary>
        /// <param name="toolName">The tool being tested.</param>
        /// <param name="dataSize">Size of test data.</param>
        /// <returns>A scenario builder.</returns>
        public static ScenarioBuilder PerformanceScenario(string toolName, int dataSize)
        {
            return new ScenarioBuilder($"{toolName}_Performance_{dataSize}")
                .AddStep($"Generate {dataSize} items", ctx =>
                {
                    var generator = new TestDataGenerator();
                    ctx["largeData"] = generator.GenerateCollection(dataSize, i => 
                        generator.GenerateLargeObject(100));
                })
                .AddStep("Warm up", ctx =>
                {
                    // Warm up run
                })
                .AddStep("Measure performance", ctx =>
                {
                    ctx["iterations"] = 5;
                    ctx["timings"] = new List<double>();
                });
        }
    }

    /// <summary>
    /// Represents a test scenario with multiple steps.
    /// </summary>
    public class TestScenario
    {
        private readonly List<ScenarioStep> _steps;
        private readonly Dictionary<string, object> _context;
        private readonly Action<Dictionary<string, object>>? _setup;
        private readonly Action<Dictionary<string, object>>? _teardown;

        /// <summary>
        /// Gets the scenario name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the execution context.
        /// </summary>
        public IReadOnlyDictionary<string, object> Context => _context;

        /// <summary>
        /// Gets the scenario steps.
        /// </summary>
        public IReadOnlyList<ScenarioStep> Steps => _steps;

        internal TestScenario(
            string name,
            List<ScenarioStep> steps,
            Dictionary<string, object> context,
            Action<Dictionary<string, object>>? setup,
            Action<Dictionary<string, object>>? teardown)
        {
            Name = name;
            _steps = steps;
            _context = context;
            _setup = setup;
            _teardown = teardown;
        }

        /// <summary>
        /// Executes the scenario.
        /// </summary>
        /// <returns>The scenario result.</returns>
        public async Task<ScenarioResult> ExecuteAsync()
        {
            var result = new ScenarioResult { ScenarioName = Name };
            var stepResults = new List<StepResult>();

            try
            {
                // Run setup
                _setup?.Invoke(_context);

                // Execute each step
                foreach (var step in _steps)
                {
                    var stepResult = await ExecuteStepAsync(step);
                    stepResults.Add(stepResult);

                    if (!stepResult.Success && step.Required)
                    {
                        result.Success = false;
                        result.FailedStep = step.Description;
                        result.Error = stepResult.Error;
                        break;
                    }
                }

                result.Success = result.Success ?? true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex;
            }
            finally
            {
                try
                {
                    _teardown?.Invoke(_context);
                }
                catch (Exception ex)
                {
                    result.TeardownError = ex;
                }

                result.StepResults = stepResults;
                result.ExecutionTime = stepResults.Count > 0
                    ? TimeSpan.FromMilliseconds(stepResults.Sum(s => s.Duration.TotalMilliseconds))
                    : TimeSpan.Zero;
            }

            return result;
        }

        private async Task<StepResult> ExecuteStepAsync(ScenarioStep step)
        {
            var startTime = DateTime.UtcNow;
            var stepResult = new StepResult
            {
                StepName = step.Description,
                StartTime = startTime
            };

            try
            {
                // Check condition
                if (step.Condition != null && !step.Condition(_context))
                {
                    stepResult.Skipped = true;
                    stepResult.Success = true;
                    return stepResult;
                }

                // Execute action
                await step.Action(_context);

                // Run validation
                step.Validation?.Invoke(_context);

                stepResult.Success = true;
            }
            catch (Exception ex)
            {
                stepResult.Success = false;
                stepResult.Error = ex;
            }
            finally
            {
                stepResult.EndTime = DateTime.UtcNow;
                stepResult.Duration = stepResult.EndTime - startTime;
            }

            return stepResult;
        }
    }

    /// <summary>
    /// Represents a step in a test scenario.
    /// </summary>
    public class ScenarioStep
    {
        /// <summary>
        /// Gets or sets the step description.
        /// </summary>
        public string Description { get; set; } = "";

        /// <summary>
        /// Gets or sets the action to perform.
        /// </summary>
        public Func<Dictionary<string, object>, Task> Action { get; set; } = _ => Task.CompletedTask;

        /// <summary>
        /// Gets or sets the optional validation.
        /// </summary>
        public Action<Dictionary<string, object>>? Validation { get; set; }

        /// <summary>
        /// Gets or sets the optional condition.
        /// </summary>
        public Func<Dictionary<string, object>, bool>? Condition { get; set; }

        /// <summary>
        /// Gets or sets whether this step is required for scenario success.
        /// </summary>
        public bool Required { get; set; } = true;
    }

    /// <summary>
    /// Represents the result of a scenario execution.
    /// </summary>
    public class ScenarioResult
    {
        /// <summary>
        /// Gets or sets the scenario name.
        /// </summary>
        public string ScenarioName { get; set; } = "";

        /// <summary>
        /// Gets or sets whether the scenario succeeded.
        /// </summary>
        public bool? Success { get; set; }

        /// <summary>
        /// Gets or sets the total execution time.
        /// </summary>
        public TimeSpan ExecutionTime { get; set; }

        /// <summary>
        /// Gets or sets the step results.
        /// </summary>
        public List<StepResult> StepResults { get; set; } = new();

        /// <summary>
        /// Gets or sets the failed step description.
        /// </summary>
        public string? FailedStep { get; set; }

        /// <summary>
        /// Gets or sets any error that occurred.
        /// </summary>
        public Exception? Error { get; set; }

        /// <summary>
        /// Gets or sets any teardown error.
        /// </summary>
        public Exception? TeardownError { get; set; }
    }

    /// <summary>
    /// Represents the result of a step execution.
    /// </summary>
    public class StepResult
    {
        /// <summary>
        /// Gets or sets the step name.
        /// </summary>
        public string StepName { get; set; } = "";

        /// <summary>
        /// Gets or sets whether the step succeeded.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets whether the step was skipped.
        /// </summary>
        public bool Skipped { get; set; }

        /// <summary>
        /// Gets or sets the step start time.
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// Gets or sets the step end time.
        /// </summary>
        public DateTime EndTime { get; set; }

        /// <summary>
        /// Gets or sets the step duration.
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Gets or sets any error that occurred.
        /// </summary>
        public Exception? Error { get; set; }
    }
}