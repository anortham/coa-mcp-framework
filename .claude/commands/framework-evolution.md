---
name: framework-evolution
version: 1.0.0
description: Orchestrates COA MCP Framework development team for architecture evolution, feature implementation, and quality assurance
author: COA MCP Framework Team  
command: evolve-framework
---

You are orchestrating the evolution of the COA MCP Framework through a specialized team of experts. This command coordinates complex framework development tasks that require deep architectural knowledge, rigorous testing, performance optimization, and careful consumer integration.

## Workflow Overview

This command manages the complete lifecycle of framework evolution: from architectural design through implementation, testing, optimization, and deployment. It ensures the critical build-test-pack cycle is maintained while preserving the framework's 100% test pass rate and consumer compatibility.

## Agent Team Composition

### Agent 1: framework-architecture-expert

**Role**: Architectural foundation and design decisions
**Triggers**: Base class changes, API design, architectural questions, interface modifications
**Dependencies**: None (primary decision maker for architecture)

### Agent 2: testing-quality-specialist

**Role**: Quality assurance and comprehensive testing
**Triggers**: After architecture changes, for quality validation, CI/CD issues
**Dependencies**: Architecture decisions from Agent 1

### Agent 3: middleware-pipeline-specialist  

**Role**: Middleware system and execution pipeline optimization
**Triggers**: Pipeline issues, TypeVerificationMiddleware updates, performance bottlenecks
**Dependencies**: Architecture foundation from Agent 1, test infrastructure from Agent 2

### Agent 4: performance-optimization-expert

**Role**: Performance analysis and optimization implementation
**Triggers**: Performance issues, concurrent execution needs, memory optimization
**Dependencies**: Architecture from Agent 1, performance testing from Agent 2, pipeline analysis from Agent 3

### Agent 5: integration-packaging-specialist

**Role**: Consumer integration, packaging, and deployment
**Triggers**: Release preparation, consumer issues, packaging problems
**Dependencies**: All other agents (final integration point)

## Orchestration Strategy

### Phase 1: Assessment and Planning

Determine the scope of framework evolution needed:

1. **Analyze Request**: Understand what aspect of the framework needs evolution
2. **Impact Assessment**: Determine which framework components are affected
3. **Team Composition**: Decide which agents to engage based on the scope

```pseudo
if (request.involves("base_classes") || request.involves("architecture")):
    engage framework-architecture-expert first
    set architectural_foundation = true

if (request.involves("middleware") || request.involves("pipeline")):
    engage middleware-pipeline-specialist  
    ensure architectural_foundation is established

if (request.involves("performance") || request.involves("optimization")):
    engage performance-optimization-expert
    coordinate with middleware specialist if needed

always_engage testing-quality-specialist for validation
always_engage integration-packaging-specialist for consumer impact
```

### Phase 2: Sequential Development Execution

Based on assessment, execute development in dependency order:

#### Pattern: Architecture-First Development
When architectural changes are needed:

1. **Framework Architecture Expert** designs and implements base changes
2. **Testing & Quality Specialist** creates/updates comprehensive test coverage
3. **Middleware & Pipeline Specialist** adapts pipeline components as needed  
4. **Performance & Optimization Expert** optimizes new patterns
5. **Integration & Packaging Specialist** validates consumer impact and prepares release

#### Pattern: Feature Enhancement  
When adding new features without architectural changes:

1. **Relevant Specialist** (middleware, performance, etc.) implements feature
2. **Testing & Quality Specialist** ensures comprehensive test coverage
3. **Framework Architecture Expert** validates architectural consistency
4. **Integration & Packaging Specialist** handles consumer integration

#### Pattern: Performance Optimization
When optimizing existing functionality:

1. **Performance & Optimization Expert** analyzes and implements optimizations
2. **Testing & Quality Specialist** validates performance improvements and regression testing
3. **Middleware & Pipeline Specialist** validates pipeline performance impact
4. **Integration & Packaging Specialist** assesses consumer performance impact

### Phase 3: Quality Validation and Integration

Ensure all changes meet framework quality standards:

```pseudo
quality_gates = [
    "All 647 tests pass (100% rate)",
    "Build completes with 0 warnings", 
    "Performance benchmarks show no regression",
    "Consumer compatibility validated",
    "Documentation updated"
]

for gate in quality_gates:
    if not gate.passes():
        return_to_appropriate_specialist(gate)
        halt_until_resolved()
```

### Phase 4: Build-Test-Pack Cycle Execution

Execute the critical framework deployment cycle:

1. **Build Validation**: `dotnet build` must complete successfully
2. **Test Execution**: All 647 tests must pass (100% rate)
3. **Package Creation**: `dotnet pack -c Release` with proper versioning  
4. **Consumer Integration**: Update guidance and compatibility validation

## Coordination Patterns

### Pattern: Progressive Enhancement

For incremental improvements that build on existing functionality:

```pseudo
1. framework-architecture-expert: Validate enhancement fits architectural patterns
2. implementing-specialist: Build the enhancement  
3. testing-quality-specialist: Create comprehensive test coverage
4. performance-optimization-expert: Validate no performance regression
5. integration-packaging-specialist: Ensure consumer compatibility
```

### Pattern: Breaking Change Management

For changes that affect public API surface:

```pseudo
1. framework-architecture-expert: Design change with backward compatibility analysis
2. integration-packaging-specialist: Assess consumer impact and migration requirements
3. implementing-specialist: Implement with migration support
4. testing-quality-specialist: Test both old and new patterns
5. integration-packaging-specialist: Create migration documentation and tooling
```

### Pattern: Quality Issue Resolution

When quality issues are discovered:

```pseudo
1. testing-quality-specialist: Identify root cause and impact
2. responsible-specialist: Implement fix based on issue domain
3. All agents: Validate fix doesn't introduce regressions
4. testing-quality-specialist: Ensure comprehensive test coverage for the issue
```

## Error Handling and Recovery

### Failure Modes

- **If Architecture Agent fails**: Framework consistency may be compromised
  - **Recovery**: Engage backup architectural review, validate against existing patterns
  
- **If Testing Agent fails**: Quality gates not met, release blocked
  - **Recovery**: Focus on test infrastructure, ensure 100% pass rate before proceeding
  
- **If Performance Agent fails**: Framework performance degraded
  - **Recovery**: Revert performance changes, implement alternative optimizations
  
- **If Integration Agent fails**: Consumer compatibility broken  
  - **Recovery**: Extend transition period, provide additional migration support

### Quality Recovery Protocols

When the framework fails quality gates:

1. **Halt all development**: No further changes until quality is restored
2. **Root cause analysis**: Identify which component caused the failure
3. **Targeted fixing**: Engage appropriate specialist for resolution
4. **Comprehensive validation**: Full test suite and integration validation
5. **Progressive rollout**: Careful validation before full release

## Success Criteria

The framework evolution succeeds when:

- [ ] All architectural changes maintain framework design consistency
- [ ] 647 NUnit tests continue to pass at 100% rate
- [ ] Build completes with zero warnings
- [ ] Performance benchmarks show improvement or no regression  
- [ ] Consumer integration remains seamless with clear migration paths
- [ ] Build-test-pack cycle executes successfully
- [ ] Framework version is properly incremented following semantic versioning
- [ ] Documentation and examples are updated to reflect changes

## Special Orchestration Rules

### Critical Framework Constraints

1. **Build-Test-Pack Enforcement**: Never bypass the mandatory cycle
2. **100% Test Pass Rate**: No exceptions - all tests must pass before release
3. **CodeNav Integration**: Maintain TypeVerificationMiddleware effectiveness
4. **Consumer Compatibility**: Breaking changes require major version increment and migration support
5. **Performance Standards**: No regression in core framework performance metrics

### Agent Priority Rules

- **Framework Architecture Expert** has final say on architectural decisions
- **Testing & Quality Specialist** can block any release that doesn't meet quality gates
- **Integration & Packaging Specialist** controls release timing and consumer impact
- All agents must coordinate through this orchestration command for framework-level changes

### Collaboration Quality Standards

Each agent interaction must include:
- Clear handoff criteria and deliverables
- Validation that previous agent's work is complete and correct
- Integration testing between agent deliverables  
- Documentation of decisions and changes for future reference

This orchestration ensures the COA MCP Framework continues to evolve while maintaining its reputation for quality, performance, and reliability.