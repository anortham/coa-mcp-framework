# Publishing Strategy: NuGet Packages and GitHub MCP Servers

## Overview
This document outlines the plan to:
1. Publish COA MCP Framework packages to both internal Azure DevOps and NuGet.org
2. Publish MCP servers (CodeSearch, CodeNav) to GitHub as releases
3. Enable community adoption while maintaining internal development

## Phase 1: NuGet Package Publishing

### 1.1 Update Azure DevOps Pipeline
- Add a new stage for NuGet.org publishing
- Configure conditional publishing (tags only for public releases)
- Set up proper versioning strategy

### 1.2 Package Preparation
- Add package metadata for NuGet.org (icon, readme, license)
- Ensure proper package descriptions and tags
- Add package icon and documentation URLs

### 1.3 Security Configuration
- Store NuGet.org API key in Azure DevOps secure variables
- Configure package signing (optional but recommended)
- Set up proper access controls

## Phase 2: GitHub MCP Server Publishing

### 2.1 Repository Structure
```
COA-MCP-Servers/
├── .github/
│   └── workflows/
│       ├── release-codesearch.yml
│       └── release-codenav.yml
├── CodeSearch/
│   ├── src/
│   ├── README.md
│   └── LICENSE
├── CodeNav/
│   ├── src/
│   ├── README.md
│   └── LICENSE
└── README.md
```

### 2.2 GitHub Actions Workflows
- Build and test on push to main
- Create releases on version tags
- Build Windows, Linux, and macOS binaries
- Attach binaries to GitHub releases
- Generate release notes automatically

### 2.3 Release Artifacts
Each release should include:
- Windows executable (self-contained)
- Linux binary (self-contained)
- macOS binary (self-contained)
- Installation instructions
- Configuration examples

## Implementation Plan

### Step 1: Update Azure DevOps Pipeline (azure-pipelines.yml)
```yaml
stages:
  # ... existing Build stage ...

  - stage: PublishInternal
    displayName: "Publish to Azure DevOps"
    dependsOn: Build
    condition: and(succeeded(), ne(variables['Build.Reason'], 'PullRequest'))
    jobs:
      - job: PublishToAzureDevOps
        # ... existing internal publish ...

  - stage: PublishNuGetOrg
    displayName: "Publish to NuGet.org"
    dependsOn: Build
    condition: and(succeeded(), startsWith(variables['Build.SourceBranch'], 'refs/tags/v'))
    jobs:
      - job: PublishToNuGetOrg
        steps:
          - task: DownloadBuildArtifacts@1
            inputs:
              artifactName: "packages"
              
          - task: NuGetCommand@2
            displayName: "Push to NuGet.org"
            inputs:
              command: 'push'
              packagesToPush: '$(System.ArtifactsDirectory)/packages/*.nupkg'
              nuGetFeedType: 'external'
              publishFeedCredentials: 'NuGetOrg-ServiceConnection'
```

### Step 2: Package Metadata Updates
Update each .csproj file:
```xml
<PropertyGroup>
    <PackageId>COA.Mcp.Framework</PackageId>
    <Authors>City of Austin</Authors>
    <Company>City of Austin</Company>
    <Description>Framework for building MCP (Model Context Protocol) servers</Description>
    <PackageTags>mcp;ai;llm;framework;anthropic</PackageTags>
    <PackageProjectUrl>https://github.com/CityOfAustin/COA-MCP-Framework</PackageProjectUrl>
    <RepositoryUrl>https://github.com/CityOfAustin/COA-MCP-Framework</RepositoryUrl>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageIcon>icon.png</PackageIcon>
    <PackageReadmeFile>README.md</PackageReadmeFile>
</PropertyGroup>

<ItemGroup>
    <None Include="..\..\icon.png" Pack="true" PackagePath=""/>
    <None Include="..\..\README.md" Pack="true" PackagePath=""/>
</ItemGroup>
```

### Step 3: GitHub Actions for MCP Servers
Create `.github/workflows/release.yml`:
```yaml
name: Release MCP Servers

on:
  push:
    tags:
      - 'v*'

jobs:
  build:
    strategy:
      matrix:
        include:
          - os: windows-latest
            runtime: win-x64
            extension: .exe
          - os: ubuntu-latest
            runtime: linux-x64
            extension: ''
          - os: macos-latest
            runtime: osx-x64
            extension: ''
    
    runs-on: ${{ matrix.os }}
    
    steps:
      - uses: actions/checkout@v4
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 9.x
          
      - name: Build CodeSearch
        run: |
          dotnet publish ./CodeSearch/src/COA.CodeSearch.McpServer.csproj \
            -c Release \
            -r ${{ matrix.runtime }} \
            --self-contained \
            -p:PublishSingleFile=true \
            -o ./publish/codesearch
            
      - name: Build CodeNav
        run: |
          dotnet publish ./CodeNav/src/COA.CodeNav.McpServer.csproj \
            -c Release \
            -r ${{ matrix.runtime }} \
            --self-contained \
            -p:PublishSingleFile=true \
            -o ./publish/codenav
            
      - name: Upload artifacts
        uses: actions/upload-artifact@v4
        with:
          name: mcp-servers-${{ matrix.runtime }}
          path: ./publish/

  release:
    needs: build
    runs-on: ubuntu-latest
    
    steps:
      - name: Download all artifacts
        uses: actions/download-artifact@v4
        
      - name: Create Release
        uses: softprops/action-gh-release@v1
        with:
          files: |
            mcp-servers-win-x64/codesearch/COA.CodeSearch.McpServer.exe
            mcp-servers-linux-x64/codesearch/COA.CodeSearch.McpServer
            mcp-servers-osx-x64/codesearch/COA.CodeSearch.McpServer
            mcp-servers-win-x64/codenav/COA.CodeNav.McpServer.exe
            mcp-servers-linux-x64/codenav/COA.CodeNav.McpServer
            mcp-servers-osx-x64/codenav/COA.CodeNav.McpServer
          generate_release_notes: true
```

### Step 4: Setup Requirements

1. **Azure DevOps**:
   - Create service connection to NuGet.org
   - Add NuGet API key as secure variable
   - Update pipeline permissions

2. **GitHub**:
   - Create new repository for MCP servers
   - Set up repository secrets
   - Configure release permissions

3. **NuGet.org**:
   - Create organization account
   - Reserve package name prefixes
   - Generate API keys with push permissions

### Step 5: Documentation Updates

1. **Framework README.md**:
   - Add NuGet.org badges
   - Include installation instructions
   - Add links to MCP server implementations

2. **MCP Server README.md**:
   - Installation instructions for each platform
   - Configuration examples
   - Troubleshooting guide

## Timeline

- Week 1: Update Azure DevOps pipeline and test internal publishing
- Week 2: Add NuGet.org publishing and test with preview packages
- Week 3: Set up GitHub repository and workflows
- Week 4: First public release of framework and MCP servers

## Success Criteria

1. Framework packages available on NuGet.org
2. MCP servers available as GitHub releases
3. Documentation complete and accessible
4. Community can easily install and use both framework and servers
5. Internal development workflow unchanged