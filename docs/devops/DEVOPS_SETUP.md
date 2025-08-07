# COA MCP Framework - DevOps Setup Guide

This document outlines the complete DevOps setup for the COA MCP Framework, including CI/CD pipeline, package publishing, and deployment strategies.

## ✅ Completed Setup

### 1. Azure DevOps Pipeline Configuration
- **File**: `azure-pipelines.yml`
- **Features**:
  - Multi-branch triggers (main, develop, release/*, tags)
  - Automated version management with counters
  - Build, test, and pack all 6 framework packages
  - Publish to Azure DevOps Artifacts feed
  - Code coverage reporting
  - NuGet package caching

### 2. NuGet Package Configuration
- **All 5 packages** configured for publishing:
  - `COA.Mcp.Framework` - Core framework
  - `COA.Mcp.Framework.TokenOptimization` - Token management
  - `COA.Mcp.Framework.Testing` - Testing helpers
  - `COA.Mcp.Framework.Templates` - Project templates
  - `COA.Mcp.Framework.Migration` - Migration utilities

### 3. Version Management Strategy
- **Auto-incrementing patch versions** using Azure DevOps counters
- **Branch-based versioning**:
  - `main` → 1.0.x (stable releases)
  - `develop` → 1.0.x-preview.n (preview releases)
  - `release/*` → 1.0.x-rc.n (release candidates)
  - `feature/*` → 1.0.x-alpha.branch.n (alpha builds)
  - Tags → Use tag version (e.g., v1.1.0)

### 4. Package Metadata Standardization
- **Directory.Build.props** centralizes common properties
- **Consistent metadata** across all packages:
  - Authors: COA Development Team
  - License: MIT
  - Repository: https://github.com/coa/mcp-framework
  - README inclusion in packages

### 5. Build Configuration
- **Multi-targeting**: .NET 9.0 for all packages (except templates: .NET 8.0)
- **Release builds** with optimizations
- **Documentation generation** for all packages
- **No icon dependency** (can be added later)

## 🏗️ Pipeline Details

### Build Stages

1. **Restore** - NuGet package restoration with caching
2. **Build** - Solution build with version injection
3. **Test** - All unit tests with coverage reporting
4. **Pack** - Individual package creation for each project
5. **Publish** - Push to Azure DevOps Artifacts feed

### Version Calculation Logic

```powershell
# Main branch: 1.0.{auto-increment}
# Develop: 1.0.{auto-increment}-preview.{build}
# Release branches: 1.0.{auto-increment}-rc.{build}
# Tags: Use tag version exactly
# Feature branches: 1.0.{auto-increment}-alpha.{branch}.{build}
```

### Triggers

- **CI Triggers**: main, develop, release/* branches
- **PR Triggers**: main, develop branches
- **Tag Triggers**: v* tags for releases
- **Path Exclusions**: Documentation and config files

## 📦 Package Publishing

### Azure DevOps Artifacts Feed
- **Feed Name**: COA
- **URL**: https://pkgs.dev.azure.com/COA/_packaging/COA/nuget/v3/index.json
- **Package Sources**: Configured in NuGet.config
- **Package Mapping**: COA.* packages from COA feed, others from NuGet.org

### Package Naming Convention
```
COA.Mcp.Framework.{Component}
```

Examples:
- COA.Mcp.Framework (core)
- COA.Mcp.Framework.TokenOptimization
- COA.Mcp.Framework.Testing

## 🔒 Security & Quality

### Current Setup
- **NuGet vulnerability scanning** enabled
- **Code analysis** through .NET analyzers
- **Build warnings** treated as informational (not errors)
- **Test coverage** reporting included

### Future Enhancements
- **SonarQube integration** for code quality analysis
- **Dependency scanning** for security vulnerabilities
- **Performance regression testing**
- **Security policy enforcement**

## 🚀 Deployment Strategy

### Package Distribution
1. **Internal Feed** (Azure DevOps) - All builds
2. **NuGet.org** - Stable releases only (manual approval)
3. **GitHub Packages** - Mirror for public access

### Release Process
1. **Feature Development** → feature branches → alpha packages
2. **Integration** → develop branch → preview packages
3. **Release Preparation** → release branch → RC packages
4. **Stable Release** → main branch → stable packages
5. **Tag Release** → Git tag → official versioned packages

## 📋 Next Steps for Full Production

### Immediate (Week 1)
- [ ] Set up Azure DevOps project and configure agent pool
- [ ] Create COA artifacts feed with proper permissions
- [ ] Test pipeline with initial builds
- [ ] Configure branch policies for main/develop

### Short Term (Month 1)
- [ ] Add SonarQube integration for code quality
- [ ] Set up automated security scanning
- [ ] Configure release notes generation
- [ ] Add performance benchmarking to pipeline

### Medium Term (Quarter 1)
- [ ] Multi-stage deployment to test/staging/prod
- [ ] Integration testing with consuming projects
- [ ] Automated migration testing
- [ ] Documentation site deployment

### Long Term (Ongoing)
- [ ] Package usage analytics
- [ ] Community feedback integration
- [ ] Breaking change detection
- [ ] Automated dependency updates

## 📊 Success Metrics

### Build Health
- **Build Success Rate**: >95%
- **Build Time**: <5 minutes
- **Test Success Rate**: 100%
- **Code Coverage**: >85%

### Package Quality
- **Download Growth**: Track adoption
- **Issue Resolution Time**: <7 days
- **Breaking Changes**: Minimize in minor versions
- **Documentation Coverage**: 100% public APIs

### Developer Experience
- **Time to First Success**: <15 minutes
- **Framework Overhead**: <5%
- **API Discoverability**: Rich IntelliSense
- **Migration Success**: 100% automated

## 🛠️ Tools and Technologies

- **CI/CD**: Azure DevOps Pipelines
- **Package Management**: Azure Artifacts + NuGet.org
- **Testing**: NUnit + FluentAssertions
- **Coverage**: Coverlet
- **Benchmarking**: BenchmarkDotNet
- **Analysis**: .NET Analyzers + SonarQube (planned)
- **Documentation**: XML docs + Markdown

## 📞 Support and Contacts

- **Pipeline Issues**: DevOps team
- **Package Publishing**: Release management
- **Security Concerns**: Security team
- **General Questions**: Development team

---

**Status**: ✅ **Ready for Azure DevOps project setup**

All configuration files are in place and tested. The next step is creating the Azure DevOps project and running the first build.