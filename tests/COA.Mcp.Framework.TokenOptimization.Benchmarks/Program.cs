using BenchmarkDotNet.Running;
using COA.Mcp.Framework.TokenOptimization.Benchmarks;

// Run all benchmarks
BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);