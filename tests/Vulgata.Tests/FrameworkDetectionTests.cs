using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;

namespace Vulgata.Tests;

public sealed class FrameworkDetectionTests
{
    private const string _catalogTypeName =
        "Vulgata.Infrastructure.FrameworkDetection.FrameworkCatalog, Vulgata.Infrastructure";
    private const string _detectorTypeName =
        "Vulgata.Infrastructure.FrameworkDetection.FrameworkEvidenceDetector, Vulgata.Infrastructure";
    private const string _limitsTypeName =
        "Vulgata.Infrastructure.FrameworkDetection.FrameworkDetectionLimits, Vulgata.Infrastructure";
    private const string _reactEntry =
        """
        {
          "ecosystem": "npm",
          "packageCoordinate": "npm:react",
          "frameworkName": "react",
          "allowedLanguages": ["javascript"],
          "evidenceKinds": ["direct"]
        }
        """;

    [Fact]
    public void Load_UnknownRootField_ThrowsInvalidDataException()
    {
        string json = CatalogJson(_reactEntry).Replace(
            "\"entries\"",
            "\"unknown\": true, \"entries\"",
            StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() => Load(json));
    }

    [Fact]
    public void Load_UnknownEntryField_ThrowsInvalidDataException()
    {
        string entry = _reactEntry.Replace(
            "\"evidenceKinds\"",
            "\"unknown\": true, \"evidenceKinds\"",
            StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() => Load(CatalogJson(entry)));
    }

    [Fact]
    public void Load_DuplicateCoordinate_ThrowsInvalidDataException() =>
        Assert.Throws<InvalidDataException>(() => Load(CatalogJson(_reactEntry, _reactEntry)));

    [Fact]
    public void Load_AmbiguousCoordinate_ThrowsInvalidDataException()
    {
        string ambiguous = _reactEntry.Replace(
            "\"frameworkName\": \"react\"",
            "\"frameworkName\": \"express\"",
            StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() => Load(CatalogJson(_reactEntry, ambiguous)));
    }

    [Fact]
    public void Load_InvalidFrameworkName_ThrowsInvalidDataException()
    {
        string entry = _reactEntry.Replace(
            "\"frameworkName\": \"react\"",
            "\"frameworkName\": \"React!\"",
            StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() => Load(CatalogJson(entry)));
    }

    [Fact]
    public void Load_InvalidLanguageToken_ThrowsInvalidDataException()
    {
        string entry = _reactEntry.Replace(
            "\"javascript\"",
            "\"../javascript\"",
            StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() => Load(CatalogJson(entry)));
    }

    [Fact]
    public void Load_UnsupportedEvidenceKind_ThrowsInvalidDataException()
    {
        string entry = _reactEntry.Replace(
            "\"direct\"",
            "\"restore\"",
            StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() => Load(CatalogJson(entry)));
    }

    [Fact]
    public void Load_UnsupportedEcosystem_ThrowsInvalidDataException()
    {
        string entry = _reactEntry.Replace(
            "\"npm\"",
            "\"gradle\"",
            StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() => Load(CatalogJson(entry)));
    }

    [Fact]
    public void FindExact_NearCoordinate_ReturnsNoMatch()
    {
        object catalog = Load(CatalogJson(_reactEntry));

        Assert.NotNull(FindExact(catalog, "npm:react"));
        Assert.Null(FindExact(catalog, "npm:React"));
        Assert.Null(FindExact(catalog, "npm:react-dom"));
        Assert.Null(FindExact(catalog, "prefix-npm:react"));
    }

    [Fact]
    public void LoadEmbedded_InitialCoordinates_MatchAcceptedClosedCatalog()
    {
        object catalog = LoadEmbedded();

        CatalogCoordinate[] actual = GetEntries(catalog)
            .Select(entry => new CatalogCoordinate(
                GetProperty<string>(entry, "PackageCoordinate"),
                GetProperty<string>(entry, "FrameworkName"),
                string.Join(",", GetProperty<IEnumerable<string>>(entry, "AllowedLanguages")),
                string.Join(",", GetProperty<IEnumerable<string>>(entry, "EvidenceKinds"))))
            .OrderBy(entry => entry.PackageCoordinate, StringComparer.Ordinal)
            .ToArray();
        CatalogCoordinate[] expected =
        [
            new("maven:org.springframework.boot:spring-boot", "spring-boot", "java,kotlin", "direct"),
            new("maven:org.springframework.boot:spring-boot-starter", "spring-boot", "java,kotlin", "direct"),
            new("maven:org.springframework.boot:spring-boot-starter-web", "spring-boot", "java,kotlin", "direct"),
            new("npm:express", "express", "javascript,typescript", "direct,paired-lock"),
            new("npm:react", "react", "javascript,typescript,tsx", "direct,paired-lock"),
            new("nuget-framework:Microsoft.AspNetCore.App", "aspnet-core", "c_sharp", "direct"),
            new("nuget-sdk:Microsoft.NET.Sdk.BlazorWebAssembly", "blazor", "c_sharp", "direct"),
            new("nuget-sdk:Microsoft.NET.Sdk.Web", "aspnet-core", "c_sharp", "direct"),
            new("nuget:Microsoft.AspNetCore.Components.WebAssembly", "blazor", "c_sharp", "direct,paired-lock"),
            new("nuget:Microsoft.AspNetCore.Mvc", "aspnet-core", "c_sharp", "direct,paired-lock"),
            new("python:django", "django", "python", "direct"),
        ];

        Assert.Equal(expected, actual);
        Assert.Equal(
            ["aspnet-core", "blazor", "django", "express", "react", "spring-boot"],
            actual.Select(entry => entry.FrameworkName)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal));
    }

    [Fact]
    public void LoadEmbedded_Metadata_ReportsVersionAndExactContentSha256()
    {
        object catalog = LoadEmbedded();
        string version = GetProperty<string>(catalog, "Version");
        string sha256 = GetProperty<string>(catalog, "Sha256");
        Assembly assembly = RequireCatalogType().Assembly;
        string resourceName = Assert.Single(
            assembly.GetManifestResourceNames(),
            name => name.EndsWith("framework-catalog.v1.json", StringComparison.Ordinal));
        using Stream stream = Assert.IsAssignableFrom<Stream>(
            assembly.GetManifestResourceStream(resourceName));

        Assert.Equal("1", version);
        Assert.Matches(new Regex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant), sha256);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(stream)), sha256);
    }

    [Fact]
    public void Program_Startup_RegistersValidatedEmbeddedCatalog()
    {
        Type catalogType = RequireCatalogType();
        using var factory = new LoginLogoutTests.CustomWebApplicationFactory();

        object? catalog = factory.Services.GetService(catalogType);

        Assert.NotNull(catalog);
        Assert.Equal("1", GetProperty<string>(catalog, "Version"));
        Assert.Matches(
            new Regex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant),
            GetProperty<string>(catalog, "Sha256"));
    }

    [Fact]
    public async Task DetectAsync_TrackedLiteralDirectEvidence_ReturnsExactCatalogMatchesAsync()
    {
        using var repository = new GitFixture();
        repository.Write(
            "src/App/App.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <ItemGroup>
                <PackageReference Include="Microsoft.AspNetCore.Mvc" Version="2.3.0" />
              </ItemGroup>
            </Project>
            """);
        repository.Write(
            "src/ui/package.json",
            """
            {
              "dependencies": {
                "react": "19.0.0"
              }
            }
            """);
        repository.Write(
            "src/service/pom.xml",
            """
            <project>
              <dependencies>
                <dependency>
                  <groupId>org.springframework.boot</groupId>
                  <artifactId>spring-boot-starter-web</artifactId>
                </dependency>
              </dependencies>
            </project>
            """);
        repository.Write("src/python/requirements.txt", "Django==5.2.4\n");
        repository.TrackAll();

        DetectedEvidence[] actual =
        [
            .. (await DetectAsync(repository.Path))
                .OrderBy(item => item.PackageCoordinate, StringComparer.Ordinal),
        ];
        DetectedEvidence[] expected =
        [
            new(
                "src/service/pom.xml",
                "src/service",
                "maven",
                "maven:org.springframework.boot:spring-boot-starter-web",
                "Direct",
                "src/service/pom.xml#xml:dependency[org.springframework.boot:spring-boot-starter-web]"),
            new(
                "src/ui/package.json",
                "src/ui",
                "npm",
                "npm:react",
                "Direct",
                "src/ui/package.json#json:/dependencies/react"),
            new(
                "src/App/App.csproj",
                "src/App",
                "nuget",
                "nuget-sdk:Microsoft.NET.Sdk.Web",
                "Direct",
                "src/App/App.csproj#xml:Sdk[Microsoft.NET.Sdk.Web]"),
            new(
                "src/App/App.csproj",
                "src/App",
                "nuget",
                "nuget:Microsoft.AspNetCore.Mvc",
                "Direct",
                "src/App/App.csproj#xml:PackageReference[Microsoft.AspNetCore.Mvc]"),
            new(
                "src/python/requirements.txt",
                "src/python",
                "python-requirements",
                "python:django",
                "Direct",
                "src/python/requirements.txt#line:1:python:django"),
        ];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task DetectAsync_PairedCommittedLocks_ReturnsExactTransitiveMatchesAsync()
    {
        using var repository = new GitFixture();
        repository.Write(
            "src/dotnet/App.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Microsoft.AspNetCore.Mvc" Version="2.3.0" />
              </ItemGroup>
            </Project>
            """);
        repository.Write(
            "src/dotnet/packages.lock.json",
            """
            {
              "version": 1,
              "dependencies": {
                "net10.0": {
                  "Microsoft.AspNetCore.Mvc": {
                    "type": "Transitive",
                    "resolved": "2.3.0"
                  }
                }
              }
            }
            """);
        repository.Write(
            "src/npm-v2/package.json",
            """
            {
              "devDependencies": {
                "express": "5.0.0"
              }
            }
            """);
        repository.Write(
            "src/npm-v2/package-lock.json",
            """
            {
              "name": "npm-v2",
              "lockfileVersion": 2,
              "packages": {
                "": {
                  "devDependencies": {
                    "express": "5.0.0"
                  }
                },
                "node_modules/react": {
                  "version": "19.0.0"
                }
              }
            }
            """);
        repository.Write(
            "src/npm-v3/package.json",
            """
            {
              "optionalDependencies": {
                "react": "19.0.0"
              }
            }
            """);
        repository.Write(
            "src/npm-v3/package-lock.json",
            """
            {
              "name": "npm-v3",
              "lockfileVersion": 3,
              "packages": {
                "": {
                  "optionalDependencies": {
                    "react": "19.0.0"
                  }
                },
                "node_modules/express": {
                  "version": "5.0.0"
                }
              }
            }
            """);
        repository.TrackAll();

        DetectedEvidence[] actual =
        [
            .. (await DetectAsync(repository.Path))
                .OrderBy(item => item.DetectionSource, StringComparer.Ordinal),
        ];

        Assert.Contains(
            new DetectedEvidence(
                "src/dotnet/packages.lock.json",
                "src/dotnet",
                "nuget",
                "nuget:Microsoft.AspNetCore.Mvc",
                "Transitive",
                "src/dotnet/packages.lock.json#json:/dependencies/net10.0/Microsoft.AspNetCore.Mvc"),
            actual);
        Assert.Contains(
            new DetectedEvidence(
                "src/npm-v2/package-lock.json",
                "src/npm-v2",
                "npm",
                "npm:react",
                "Transitive",
                "src/npm-v2/package-lock.json#json:/packages/node_modules~1react"),
            actual);
        Assert.Contains(
            new DetectedEvidence(
                "src/npm-v3/package-lock.json",
                "src/npm-v3",
                "npm",
                "npm:express",
                "Transitive",
                "src/npm-v3/package-lock.json#json:/packages/node_modules~1express"),
            actual);
        Assert.Contains(
            actual,
            item => item.PackageCoordinate == "npm:express" &&
                item.Relationship == "Direct" &&
                item.DetectionSource == "src/npm-v2/package.json#json:/devDependencies/express");
        Assert.Contains(
            actual,
            item => item.PackageCoordinate == "npm:react" &&
                item.Relationship == "Direct" &&
                item.DetectionSource == "src/npm-v3/package.json#json:/optionalDependencies/react");
    }

    [Fact]
    public async Task DetectAsync_UntrackedIgnoredAndUnpairedLocks_ReturnsNoMatchesAsync()
    {
        using var repository = new GitFixture();
        repository.Write(
            ".gitignore",
            """
            ignored/
            """);
        repository.Write(
            "untracked/package.json",
            """
            { "dependencies": { "react": "19.0.0" } }
            """);
        repository.Write(
            "ignored/package.json",
            """
            { "dependencies": { "express": "5.0.0" } }
            """);
        repository.Write(
            "locks/package-lock.json",
            """
            {
              "lockfileVersion": 3,
              "packages": {
                "node_modules/react": { "version": "19.0.0" }
              }
            }
            """);
        repository.Write(
            "locks/packages.lock.json",
            """
            {
              "version": 1,
              "dependencies": {
                "net10.0": {
                  "Microsoft.AspNetCore.Mvc": {
                    "type": "Transitive",
                    "resolved": "2.3.0"
                  }
                }
              }
            }
            """);
        repository.Track(".gitignore", "locks/package-lock.json", "locks/packages.lock.json");

        Assert.Empty(await DetectAsync(repository.Path));
    }

    [Fact]
    public async Task DetectAsync_DynamicConditionalAndUnsupportedDeclarations_ReturnsNoMatchesAsync()
    {
        using var repository = new GitFixture();
        repository.Write(
            "dotnet/Dynamic.csproj",
            """
            <Project Sdk="$(WebSdk)">
              <PropertyGroup>
                <FrameworkPackage>Microsoft.AspNetCore.App</FrameworkPackage>
              </PropertyGroup>
              <ItemGroup Condition="'$(UseWeb)' == 'true'">
                <PackageReference Include="Microsoft.AspNetCore.Mvc" Version="2.3.0" />
              </ItemGroup>
              <ItemGroup>
                <PackageReference Include="$(FrameworkPackage)" Version="2.3.0" />
              </ItemGroup>
              <Import Project="framework.targets" />
              <Target Name="RestoreFramework">
                <Exec Command="dotnet restore" />
              </Target>
            </Project>
            """);
        repository.Write(
            "maven/pom.xml",
            """
            <project>
              <parent>
                <groupId>org.springframework.boot</groupId>
                <artifactId>spring-boot-starter</artifactId>
              </parent>
              <properties>
                <spring.artifact>spring-boot-starter-web</spring.artifact>
              </properties>
              <dependencyManagement>
                <dependencies>
                  <dependency>
                    <groupId>org.springframework.boot</groupId>
                    <artifactId>spring-boot-starter-web</artifactId>
                  </dependency>
                </dependencies>
              </dependencyManagement>
              <profiles>
                <profile>
                  <dependencies>
                    <dependency>
                      <groupId>org.springframework.boot</groupId>
                      <artifactId>spring-boot</artifactId>
                    </dependency>
                  </dependencies>
                </profile>
              </profiles>
              <dependencies>
                <dependency>
                  <groupId>org.springframework.boot</groupId>
                  <artifactId>${spring.artifact}</artifactId>
                </dependency>
              </dependencies>
            </project>
            """);
        repository.Write(
            "python/requirements.txt",
            """
            -r included.txt
            django @ https://example.invalid/django.whl
            -e git+https://example.invalid/django.git#egg=django
            django==5.2.4; python_version >= "3.12"
            """);
        repository.Write("python/included.txt", "django==5.2.4\n");
        repository.TrackAll();

        Assert.Empty(await DetectAsync(repository.Path));
    }

    [Fact]
    public async Task DetectAsync_ForbiddenExecutionInferenceAndExternalSurfaces_ReturnsNoMatchesAsync()
    {
        using var repository = new GitFixture();
        const string markerPath = "forbidden-execution-marker.txt";
        repository.Write(
            "package.json",
            $$"""
            {
              "scripts": {
                "build": "echo build > {{markerPath}}",
                "postinstall": "echo install > {{markerPath}}"
              },
              "frameworkHint": "react"
            }
            """);
        repository.Write(
            "build.gradle",
            """
            dependencies {
                implementation "org.springframework.boot:spring-boot-starter-web"
            }
            """);
        repository.Write(
            "Directory.Build.targets",
            """
            <Project>
              <ItemGroup>
                <PackageReference Include="Microsoft.AspNetCore.Mvc" />
              </ItemGroup>
              <Target Name="BuildFramework">
                <Exec Command="dotnet build" />
              </Target>
            </Project>
            """);
        repository.Write(
            "obj/project.assets.json",
            """
            { "libraries": { "Microsoft.AspNetCore.Mvc/2.3.0": {} } }
            """);
        repository.Write(
            "src/Django.cs",
            """
            using Microsoft.AspNetCore.Mvc;
            namespace React;
            public sealed class Express { }
            """);
        repository.Write(
            "src/framework-symbols.ts",
            """
            import React from "react";
            const express = "express";
            """);
        repository.Write(
            "external-service.json",
            """
            { "package": "react", "registry": "https://registry.npmjs.org" }
            """);
        repository.Write(
            "llm-framework-result.json",
            """
            { "framework": "django", "confidence": "high" }
            """);
        repository.TrackAll();

        Assert.Empty(await DetectAsync(repository.Path));
        Assert.False(File.Exists(System.IO.Path.Combine(repository.Path, markerPath)));
    }

    [Fact]
    public async Task DetectApplicableAsync_NestedManifest_SelectsDeepestAncestorScopeAsync()
    {
        using var repository = new GitFixture();
        repository.Write(
            "package.json",
            """
            { "dependencies": { "react": "19.0.0" } }
            """);
        repository.Write(
            "src/app/package.json",
            """
            { "dependencies": { "express": "5.0.0" } }
            """);
        repository.TrackAll();

        DetectedEvidence[] nested = [.. await DetectApplicableAsync(
            repository.Path,
            "src/app/features/orders.ts",
            "typescript")];
        DetectedEvidence[] outsideNested = [.. await DetectApplicableAsync(
            repository.Path,
            "src/shared/view.tsx",
            "tsx")];

        DetectedEvidence nestedEvidence = Assert.Single(nested);
        Assert.Equal("npm:express", nestedEvidence.PackageCoordinate);
        Assert.Equal("src/app", nestedEvidence.ScopeRoot);
        DetectedEvidence rootEvidence = Assert.Single(outsideNested);
        Assert.Equal("npm:react", rootEvidence.PackageCoordinate);
        Assert.Equal(string.Empty, rootEvidence.ScopeRoot);
    }

    [Fact]
    public async Task DetectApplicableAsync_ParserLanguage_SelectsIndependentColocatedEcosystemAsync()
    {
        using var repository = new GitFixture();
        repository.Write(
            "App.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Microsoft.AspNetCore.Mvc" Version="2.3.0" />
              </ItemGroup>
            </Project>
            """);
        repository.Write(
            "package.json",
            """
            { "dependencies": { "react": "19.0.0" } }
            """);
        repository.TrackAll();

        DetectedEvidence[] dotnet = [.. await DetectApplicableAsync(
            repository.Path,
            "src/Controllers/HomeController.cs",
            "c_sharp")];
        DetectedEvidence[] node = [.. await DetectApplicableAsync(
            repository.Path,
            "src/client/main.ts",
            "typescript")];
        DetectedEvidence[] incompatible = [.. await DetectApplicableAsync(
            repository.Path,
            "src/main.go",
            "go")];

        Assert.All(dotnet, item => Assert.Equal("nuget", item.Ecosystem));
        Assert.Contains(dotnet, item => item.PackageCoordinate == "nuget:Microsoft.AspNetCore.Mvc");
        Assert.All(node, item => Assert.Equal("npm", item.Ecosystem));
        Assert.Contains(node, item => item.PackageCoordinate == "npm:react");
        Assert.Empty(incompatible);
    }

    [Fact]
    public async Task DetectApplicableAsync_SameEcosystemSameRootAmbiguity_ReturnsNoMatchesAsync()
    {
        using var repository = new GitFixture();
        repository.Write(
            "Api.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Microsoft.AspNetCore.Mvc" Version="2.3.0" />
              </ItemGroup>
            </Project>
            """);
        repository.Write(
            "Client.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly"
                                  Version="10.0.0" />
              </ItemGroup>
            </Project>
            """);
        repository.TrackAll();

        Assert.Empty(await DetectApplicableAsync(
            repository.Path,
            "src/SharedComponent.cs",
            "c_sharp"));
    }

    [Fact]
    public async Task DetectApplicableAsync_UnpairedOrOutOfScopeEvidence_ReturnsNoMatchesAsync()
    {
        using var repository = new GitFixture();
        repository.Write(
            "app/package.json",
            """
            { "dependencies": { "react": "19.0.0" } }
            """);
        repository.Write(
            "service/package-lock.json",
            """
            {
              "lockfileVersion": 3,
              "packages": {
                "node_modules/express": { "version": "5.0.0" }
              }
            }
            """);
        repository.TrackAll();

        Assert.Empty(await DetectApplicableAsync(
            repository.Path,
            "service/src/server.ts",
            "typescript"));
    }

    [Fact]
    public async Task DetectApplicableAsync_DirectAndPairedLock_PreservesSingleDirectWinnerAsync()
    {
        using var repository = new GitFixture();
        repository.Write(
            "App.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Microsoft.AspNetCore.Mvc" Version="2.3.0" />
              </ItemGroup>
            </Project>
            """);
        repository.Write(
            "packages.lock.json",
            """
            {
              "version": 1,
              "dependencies": {
                "net10.0": {
                  "Microsoft.AspNetCore.Mvc": {
                    "type": "Transitive",
                    "resolved": "2.3.0"
                  }
                }
              }
            }
            """);
        repository.TrackAll();

        DetectedEvidence winner = Assert.Single(await DetectApplicableAsync(
            repository.Path,
            "src/Controllers/HomeController.cs",
            "c_sharp"));

        Assert.Equal("Direct", winner.Relationship);
        Assert.Equal(
            "App.csproj#xml:PackageReference[Microsoft.AspNetCore.Mvc]",
            winner.DetectionSource);
    }

    [Fact]
    public async Task DetectApplicableAsync_NestedDuplicateFramework_SelectsDeepestThenOrdinalSourceAsync()
    {
        using var repository = new GitFixture();
        repository.Write(
            "package.json",
            """
            { "dependencies": { "react": "root-version" } }
            """);
        repository.Write(
            "src/app/package.json",
            """
            {
              "dependencies": { "react": "nested-dependency" },
              "devDependencies": { "react": "nested-development" }
            }
            """);
        repository.TrackAll();

        DetectedEvidence winner = Assert.Single(await DetectApplicableAsync(
            repository.Path,
            "src/app/features/orders.tsx",
            "tsx"));

        Assert.Equal("src/app", winner.ScopeRoot);
        Assert.Equal(
            "src/app/package.json#json:/dependencies/react",
            winner.DetectionSource);
    }

    [Fact]
    public async Task DetectApplicableAsync_ExactDuplicates_CollapseToThreeClosedSafeLocatorFormsAsync()
    {
        using var repository = new GitFixture();
        repository.Write(
            "App.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Microsoft.AspNetCore.Mvc"
                                  Version="ARBITRARY-NUGET-VALUE" />
                <PackageReference Include="Microsoft.AspNetCore.Mvc"
                                  Version="ARBITRARY-NUGET-VALUE" />
              </ItemGroup>
            </Project>
            """);
        repository.Write(
            "package.json",
            """
            {
              "dependencies": {
                "react": "ARBITRARY-NPM-VALUE",
                "react": "ARBITRARY-NPM-VALUE"
              },
              "sourceText": "ARBITRARY-SOURCE-TEXT"
            }
            """);
        repository.Write(
            "requirements.txt",
            """
            Django==ARBITRARY-PYTHON-VALUE
            Django==ARBITRARY-PYTHON-VALUE
            """);
        repository.TrackAll();

        DetectedEvidence dotnet = Assert.Single(await DetectApplicableAsync(
            repository.Path,
            "src/Program.cs",
            "c_sharp"));
        DetectedEvidence node = Assert.Single(await DetectApplicableAsync(
            repository.Path,
            "src/main.tsx",
            "tsx"));
        DetectedEvidence python = Assert.Single(await DetectApplicableAsync(
            repository.Path,
            "src/app.py",
            "python"));

        Assert.Equal(
            "App.csproj#xml:PackageReference[Microsoft.AspNetCore.Mvc]",
            dotnet.DetectionSource);
        Assert.Equal(
            "package.json#json:/dependencies/react",
            node.DetectionSource);
        Assert.Equal(
            "requirements.txt#line:1:python:django",
            python.DetectionSource);

        string persistedEvidence = string.Join(
            '\n',
            new[] { dotnet, node, python }.Select(item =>
                $"{item.ArtifactPath}|{item.PackageCoordinate}|{item.DetectionSource}"));
        Assert.DoesNotContain("ARBITRARY-NUGET-VALUE", persistedEvidence, StringComparison.Ordinal);
        Assert.DoesNotContain("ARBITRARY-NPM-VALUE", persistedEvidence, StringComparison.Ordinal);
        Assert.DoesNotContain("ARBITRARY-PYTHON-VALUE", persistedEvidence, StringComparison.Ordinal);
        Assert.DoesNotContain("ARBITRARY-SOURCE-TEXT", persistedEvidence, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DetectAsync_RepositoryRelativePathAndLocatorBounds_RejectOversizeEvidenceAsync()
    {
        using var repository = new GitFixture();
        repository.Write(
            "package.json",
            """
            { "dependencies": { "react": "19.0.0" } }
            """);
        string maximumArtifactPath = ArtifactPathOfLength(2_048);
        string overlongArtifactPath = ArtifactPathOfLength(2_049);
        repository.Write(
            maximumArtifactPath,
            """
            { "dependencies": { "express": "5.0.0" } }
            """);
        repository.Write(
            overlongArtifactPath,
            """
            { "dependencies": { "express": "5.0.0" } }
            """);
        repository.Write(
            "App.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Microsoft.AspNetCore.Mvc" Version="2.3.0" />
              </ItemGroup>
            </Project>
            """);
        const string lockLocatorPrefix = "packages.lock.json#json:/dependencies/";
        const string lockLocatorSuffix = "/Microsoft.AspNetCore.Mvc";
        string maximumTargetText = new(
            'm',
            4_096 - lockLocatorPrefix.Length - lockLocatorSuffix.Length);
        string arbitraryTargetText = $"{maximumTargetText}x";
        repository.Write(
            "packages.lock.json",
            $$"""
            {
              "version": 1,
              "dependencies": {
                "{{maximumTargetText}}": {
                  "Microsoft.AspNetCore.Mvc": {
                    "type": "Transitive",
                    "resolved": "2.3.0"
                  }
                },
                "{{arbitraryTargetText}}": {
                  "Microsoft.AspNetCore.Mvc": {
                    "type": "Transitive",
                    "resolved": "2.3.0"
                  }
                }
              }
            }
            """);
        repository.TrackAll();

        string maximumCodeUnitPath = $"{new string('a', 2_045)}.ts";
        string overlongCodeUnitPath = $"{new string('b', 2_046)}.ts";
        Assert.Equal(2_048, maximumCodeUnitPath.Length);
        Assert.Equal(2_049, overlongCodeUnitPath.Length);
        Assert.NotEmpty(await DetectApplicableAsync(
            repository.Path,
            maximumCodeUnitPath,
            "typescript"));
        Assert.Empty(await DetectApplicableAsync(
            repository.Path,
            overlongCodeUnitPath,
            "typescript"));

        DetectedEvidence[] evidence = [.. await DetectAsync(repository.Path)];
        Assert.Contains(evidence, item => item.ArtifactPath.Length == 2_048);
        Assert.DoesNotContain(evidence, item => item.ArtifactPath.Length > 2_048);
        Assert.Contains(evidence, item => item.DetectionSource.Length == 4_096);
        Assert.DoesNotContain(evidence, item => item.DetectionSource.Length > 4_096);
        Assert.DoesNotContain(
            arbitraryTargetText,
            string.Join('\n', evidence.Select(item => item.DetectionSource)),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task DetectDetailedAsync_PerArtifactEightMiBBound_IsolatesOversizeEvidenceAsync()
    {
        using var repository = new GitFixture();
        repository.Write(
            "valid/package.json",
            """
            { "dependencies": { "react": "VALID-VALUE" } }
            """);
        repository.Write(
            "maximum/package.json",
            PadAsciiToLength(
                """
                { "dependencies": { "express": "MAXIMUM-VALUE" } }
                """,
                8 * 1024 * 1024));
        repository.Write(
            "oversize/package.json",
            PadAsciiToLength(
                """
                { "dependencies": { "react": "OVERSIZE-SECRET-VALUE" } }
                """,
                (8 * 1024 * 1024) + 1));
        repository.TrackAll();

        DetectedFrameworkResult result = await DetectDetailedAsync(repository.Path);

        Assert.Contains(
            result.Evidence,
            item => item.ArtifactPath == "maximum/package.json" &&
                item.PackageCoordinate == "npm:express");
        Assert.Contains(
            result.Evidence,
            item => item.ArtifactPath == "valid/package.json" &&
                item.PackageCoordinate == "npm:react");
        Assert.DoesNotContain(
            result.Evidence,
            item => item.ArtifactPath == "oversize/package.json");
        DetectedAuditOutcome audit = Assert.Single(
            result.AuditOutcomes,
            item => item.ArtifactPath == "oversize/package.json");
        Assert.Equal("Errored", audit.Outcome);
        Assert.Equal("FrameworkEvidenceBoundExceeded", audit.Reason);
        Assert.Equal("框架证据超出单个文件限制。", audit.Message);
        Assert.DoesNotContain("oversize/package.json", audit.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("OVERSIZE-SECRET-VALUE", audit.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DetectDetailedAsync_ParseDepth64Bound_IsolatesDeeperEvidenceAsync()
    {
        string maximumDepth = PackageJsonWithDepth("react", 64);
        string excessiveDepth = PackageJsonWithDepth("express", 65);
        using (JsonDocument.Parse(
                   maximumDepth,
                   new JsonDocumentOptions { MaxDepth = 64 }))
        {
        }

        Assert.ThrowsAny<JsonException>(() =>
        {
            using JsonDocument ignored = JsonDocument.Parse(
                excessiveDepth,
                new JsonDocumentOptions { MaxDepth = 64 });
        });

        using var repository = new GitFixture();
        repository.Write("maximum/package.json", maximumDepth);
        repository.Write("excessive/package.json", excessiveDepth);
        repository.Write(
            "valid/requirements.txt",
            """
            Django==VALID-VALUE
            """);
        repository.TrackAll();

        DetectedFrameworkResult result = await DetectDetailedAsync(repository.Path);

        Assert.Contains(
            result.Evidence,
            item => item.ArtifactPath == "maximum/package.json" &&
                item.PackageCoordinate == "npm:react");
        Assert.Contains(
            result.Evidence,
            item => item.ArtifactPath == "valid/requirements.txt" &&
                item.PackageCoordinate == "python:django");
        Assert.DoesNotContain(
            result.Evidence,
            item => item.ArtifactPath == "excessive/package.json");
        DetectedAuditOutcome audit = Assert.Single(
            result.AuditOutcomes,
            item => item.ArtifactPath == "excessive/package.json");
        Assert.Equal("Errored", audit.Outcome);
        Assert.Equal("FrameworkEvidenceBoundExceeded", audit.Reason);
        Assert.Equal("框架证据超出解析深度限制。", audit.Message);
    }

    [Fact]
    public async Task DetectDetailedAsync_MalformedAndUnsupportedArtifacts_UseClosedSafeChineseAuditsAsync()
    {
        using var repository = new GitFixture();
        repository.Write(
            "valid/package.json",
            """
            { "dependencies": { "react": "VALID-VALUE" } }
            """);
        repository.Write(
            "malformed/package.json",
            """
            { "dependencies": { "react": "RAW-MALFORMED-SECRET"
            """);
        repository.Write(
            "unsupported/requirements.txt",
            """
            django @ https://packages.example.invalid/RAW-URL-SECRET.whl
            """);
        repository.TrackAll();

        DetectedFrameworkResult result = await DetectDetailedAsync(repository.Path);

        DetectedEvidence fact = Assert.Single(result.Evidence);
        Assert.Equal("valid/package.json", fact.ArtifactPath);
        DetectedAuditOutcome malformed = Assert.Single(
            result.AuditOutcomes,
            item => item.ArtifactPath == "malformed/package.json");
        Assert.Equal("Errored", malformed.Outcome);
        Assert.Equal("FrameworkEvidenceMalformed", malformed.Reason);
        Assert.Equal("框架证据格式无效。", malformed.Message);
        DetectedAuditOutcome unsupported = Assert.Single(
            result.AuditOutcomes,
            item => item.ArtifactPath == "unsupported/requirements.txt");
        Assert.Equal("Skipped", unsupported.Outcome);
        Assert.Equal("FrameworkEvidenceUnsupported", unsupported.Reason);
        Assert.Equal("框架证据格式不受支持。", unsupported.Message);

        Assert.All(
            result.AuditOutcomes,
            audit =>
            {
                Assert.InRange(audit.Message.Length, 1, 32);
                Assert.Matches("[\u4e00-\u9fff]", audit.Message);
                Assert.DoesNotContain(audit.ArtifactPath, audit.Message, StringComparison.Ordinal);
            });
        string retained = RetainedDetectionText(result);
        Assert.DoesNotContain(repository.Path, retained, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("RAW-MALFORMED-SECRET", retained, StringComparison.Ordinal);
        Assert.DoesNotContain("RAW-URL-SECRET", retained, StringComparison.Ordinal);
        Assert.DoesNotContain("JsonException", retained, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DetectDetailedAsync_EscapeAndReparsePoint_AreRejectedWithoutDisclosureAsync()
    {
        using var repository = new GitFixture();
        repository.Write(
            "valid/package.json",
            """
            { "dependencies": { "react": "VALID-VALUE" } }
            """);
        string insideTarget = repository.CreateFileSymbolicLink(
            "reparse/package.json",
            "inside-target/package.json",
            """
            { "dependencies": { "express": "INSIDE-REPARSE-SECRET" } }
            """,
            targetOutsideRepository: false);
        string outsideTarget = repository.CreateFileSymbolicLink(
            "escape/package.json",
            "outside-target/package.json",
            """
            { "dependencies": { "express": "OUTSIDE-ESCAPE-SECRET" } }
            """,
            targetOutsideRepository: true);
        Assert.True(File.GetAttributes(
            System.IO.Path.Combine(repository.Path, "reparse", "package.json"))
            .HasFlag(FileAttributes.ReparsePoint));
        Assert.True(File.GetAttributes(
            System.IO.Path.Combine(repository.Path, "escape", "package.json"))
            .HasFlag(FileAttributes.ReparsePoint));
        repository.Track(
            "valid/package.json",
            "reparse/package.json",
            "escape/package.json");

        DetectedFrameworkResult result = await DetectDetailedAsync(repository.Path);

        DetectedEvidence fact = Assert.Single(result.Evidence);
        Assert.Equal("valid/package.json", fact.ArtifactPath);
        DetectedAuditOutcome reparse = Assert.Single(
            result.AuditOutcomes,
            item => item.ArtifactPath == "reparse/package.json");
        Assert.Equal("Errored", reparse.Outcome);
        Assert.Equal("FrameworkEvidenceReparsePoint", reparse.Reason);
        Assert.Equal("框架证据包含重解析点。", reparse.Message);
        DetectedAuditOutcome escape = Assert.Single(
            result.AuditOutcomes,
            item => item.ArtifactPath == "escape/package.json");
        Assert.Equal("Errored", escape.Outcome);
        Assert.Equal("FrameworkEvidenceUnsafePath", escape.Reason);
        Assert.Equal("框架证据路径不安全。", escape.Message);

        string retained = RetainedDetectionText(result);
        Assert.DoesNotContain(insideTarget, retained, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(outsideTarget, retained, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSIDE-REPARSE-SECRET", retained, StringComparison.Ordinal);
        Assert.DoesNotContain("OUTSIDE-ESCAPE-SECRET", retained, StringComparison.Ordinal);
        Assert.All(
            result.AuditOutcomes,
            audit => Assert.DoesNotContain(
                audit.ArtifactPath,
                audit.Message,
                StringComparison.Ordinal));
    }

    [Fact]
    public void FrameworkDetectionLimits_ProductionDefaults_MatchAcceptedRepositoryBounds()
    {
        object limits = GetProductionLimits();

        Assert.Equal(64L * 1024 * 1024, GetProperty<long>(limits, "MaximumAggregateArtifactBytes"));
        Assert.Equal(1_000, GetProperty<int>(limits, "MaximumSupportedArtifacts"));
        Assert.Equal(100_000, GetProperty<int>(limits, "MaximumParsedPackageEntries"));
        Assert.Equal(250_000, GetProperty<int>(limits, "MaximumCandidateFacts"));
        Assert.Equal(64, GetProperty<int>(limits, "MaximumFinalFactsPerCodeUnit"));
        Assert.Equal(100, GetProperty<int>(limits, "MaximumRetainedAuditDetails"));
    }

    [Fact]
    public async Task DetectDetailedAsync_AggregateArtifactBytesOverflow_FailsClosedWithoutPartialFactsAsync()
    {
        using var repository = new GitFixture();
        repository.Write(
            "first/package.json",
            """{ "dependencies": { "react": "FIRST-SECRET" } }""");
        repository.Write(
            "second/package.json",
            """{ "dependencies": { "express": "SECOND-SECRET" } }""");
        repository.TrackAll();
        object limits = CreateLimits(maximumAggregateArtifactBytes: 64);

        DetectedGlobalFrameworkResult result = await DetectGlobalDetailedAsync(
            repository.Path,
            limits);

        AssertGlobalFailure(result, "RepositoryBoundExceeded", "框架证据超出仓库级限制。");
        Assert.DoesNotContain("FIRST-SECRET", RetainedDetectionText(result), StringComparison.Ordinal);
        Assert.DoesNotContain("SECOND-SECRET", RetainedDetectionText(result), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DetectDetailedAsync_SupportedArtifactOverflow_FailsClosedWithoutPartialFactsAsync()
    {
        using var repository = new GitFixture();
        repository.Write(
            "first/package.json",
            """{ "dependencies": { "react": "FIRST-SECRET" } }""");
        repository.Write(
            "second/requirements.txt",
            "Django==SECOND-SECRET");
        repository.TrackAll();
        object limits = CreateLimits(maximumSupportedArtifacts: 1);

        DetectedGlobalFrameworkResult result = await DetectGlobalDetailedAsync(
            repository.Path,
            limits);

        AssertGlobalFailure(result, "RepositoryBoundExceeded", "框架证据超出仓库级限制。");
    }

    [Fact]
    public async Task DetectDetailedAsync_ParsedEntryOverflow_FailsClosedWithoutPartialFactsAsync()
    {
        using var repository = new GitFixture();
        repository.Write(
            "package.json",
            """
            {
              "dependencies": {
                "react": "FIRST-SECRET",
                "unsupported-package": "SECOND-SECRET"
              }
            }
            """);
        repository.TrackAll();
        object limits = CreateLimits(maximumParsedPackageEntries: 1);

        DetectedGlobalFrameworkResult result = await DetectGlobalDetailedAsync(
            repository.Path,
            limits);

        AssertGlobalFailure(result, "RepositoryBoundExceeded", "框架证据超出仓库级限制。");
    }

    [Fact]
    public async Task DetectDetailedAsync_CandidateFactOverflow_FailsClosedWithoutPartialFactsAsync()
    {
        using var repository = new GitFixture();
        repository.Write(
            "package.json",
            """
            {
              "dependencies": {
                "react": "FIRST-SECRET",
                "express": "SECOND-SECRET"
              }
            }
            """);
        repository.TrackAll();
        object limits = CreateLimits(maximumCandidateFacts: 1);

        DetectedGlobalFrameworkResult result = await DetectGlobalDetailedAsync(
            repository.Path,
            limits);

        AssertGlobalFailure(result, "RepositoryBoundExceeded", "框架证据超出仓库级限制。");
    }

    [Fact]
    public async Task DetectApplicableDetailedAsync_FinalFactOverflow_FailsClosedWithoutPartialFactsAsync()
    {
        using var repository = new GitFixture();
        repository.Write(
            "package.json",
            """
            {
              "dependencies": {
                "react": "FIRST-SECRET",
                "express": "SECOND-SECRET"
              }
            }
            """);
        repository.TrackAll();
        object limits = CreateLimits(maximumFinalFactsPerCodeUnit: 1);

        DetectedGlobalFrameworkResult result = await DetectApplicableGlobalDetailedAsync(
            repository.Path,
            "src/app.ts",
            "typescript",
            limits);

        AssertGlobalFailure(result, "RepositoryBoundExceeded", "框架证据超出仓库级限制。");
    }

    [Fact]
    public async Task DetectDetailedAsync_MoreThanRetainedDetailLimit_SamplesWithoutLosingTotalCountAsync()
    {
        using var repository = new GitFixture();
        repository.Write(
            "valid/package.json",
            """{ "dependencies": { "react": "VALID" } }""");
        for (var index = 0; index < 3; index++)
        {
            repository.Write(
                $"unsupported-{index}/package.json",
                $$"""{ "dependencies": { "unsupported-{{index}}": "RAW-SECRET-{{index}}" } }""");
        }

        repository.TrackAll();
        object limits = CreateLimits(maximumRetainedAuditDetails: 2);

        DetectedGlobalFrameworkResult result = await DetectGlobalDetailedAsync(
            repository.Path,
            limits);

        Assert.True(result.Succeeded);
        Assert.Single(result.Evidence);
        Assert.Equal(2, result.AuditOutcomes.Count);
        Assert.Equal(3, result.TotalAuditOutcomeCount);
        Assert.Equal("None", result.Failure);
        Assert.Null(result.Error);
        string retained = RetainedDetectionText(result);
        Assert.DoesNotContain("RAW-SECRET", retained, StringComparison.Ordinal);
        Assert.DoesNotContain(repository.Path, retained, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DetectDetailedAsync_UnexpectedDetectorFailure_ReturnsClosedSafeFailureAsync()
    {
        string directory = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"vulgata-framework-not-repository-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            DetectedGlobalFrameworkResult? result = null;
            Exception? exception = await Record.ExceptionAsync(async () =>
                result = await DetectGlobalDetailedAsync(directory));

            Assert.Null(exception);
            Assert.NotNull(result);
            AssertGlobalFailure(result, "DetectorFailed", "框架证据检测失败。");
            string retained = RetainedDetectionText(result);
            Assert.DoesNotContain(directory, retained, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("InvalidOperationException", retained, StringComparison.Ordinal);
            Assert.DoesNotContain("fatal:", retained, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string PadAsciiToLength(string content, int length)
    {
        Assert.Equal(content.Length, Encoding.UTF8.GetByteCount(content));
        Assert.InRange(content.Length, 0, length);
        return content + new string(' ', length - content.Length);
    }

    private static string PackageJsonWithDepth(string packageName, int depth)
    {
        string nested = "true";
        for (var currentDepth = 1; currentDepth < depth; currentDepth++)
        {
            nested = $$"""{"nested":{{nested}}}""";
        }

        return $$"""{"dependencies":{"{{packageName}}":"VALUE"},"nested":{{nested}}}""";
    }

    private static string RetainedDetectionText(DetectedFrameworkResult result) =>
        string.Join(
            '\n',
            result.Evidence.Select(item =>
                    $"{item.ArtifactPath}|{item.PackageCoordinate}|{item.DetectionSource}")
                .Concat(result.AuditOutcomes.Select(item =>
                    $"{item.ArtifactPath}|{item.Outcome}|{item.Reason}|{item.Message}")));

    private static string RetainedDetectionText(DetectedGlobalFrameworkResult result) =>
        string.Join(
            '\n',
            result.Evidence.Select(item =>
                    $"{item.ArtifactPath}|{item.PackageCoordinate}|{item.DetectionSource}")
                .Concat(result.AuditOutcomes.Select(item =>
                    $"{item.ArtifactPath}|{item.Outcome}|{item.Reason}|{item.Message}"))
                .Append($"{result.Failure}|{result.Error}"));

    private static void AssertGlobalFailure(
        DetectedGlobalFrameworkResult result,
        string expectedFailure,
        string expectedMessage)
    {
        Assert.False(result.Succeeded);
        Assert.Empty(result.Evidence);
        Assert.Empty(result.AuditOutcomes);
        Assert.Equal(0, result.TotalAuditOutcomeCount);
        Assert.Equal(expectedFailure, result.Failure);
        Assert.Equal(expectedMessage, result.Error);
        Assert.InRange(result.Error!.Length, 1, 32);
        Assert.Matches("[\u4e00-\u9fff]", result.Error);
    }

    private static string ArtifactPathOfLength(int length)
    {
        const string suffix = "/package.json";
        var segments = new List<string>();
        int remaining = length - suffix.Length;
        while (remaining > 100)
        {
            segments.Add(new string('p', 100));
            remaining -= 101;
        }

        segments.Add(new string('p', remaining));
        string result = $"{string.Join('/', segments)}{suffix}";
        Assert.Equal(length, result.Length);
        return result;
    }

    private static string CatalogJson(params string[] entries) =>
        $$"""
        {
          "version": "1",
          "entries": [{{string.Join(",", entries)}}]
        }
        """;

    private static object Load(string json)
    {
        Type catalogType = RequireCatalogType();
        MethodInfo? load = catalogType.GetMethod(
            "Load",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: [typeof(Stream)],
            modifiers: null);
        Assert.NotNull(load);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        try
        {
            object? catalog = load.Invoke(null, [stream]);
            Assert.NotNull(catalog);
            Assert.IsType(catalogType, catalog);
            return catalog;
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }

    private static object LoadEmbedded()
    {
        Type catalogType = RequireCatalogType();
        MethodInfo? loadEmbedded = catalogType.GetMethod(
            "LoadEmbedded",
            BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(loadEmbedded);
        object? catalog = loadEmbedded.Invoke(null, null);
        Assert.NotNull(catalog);
        Assert.IsType(catalogType, catalog);
        return catalog;
    }

    private static object? FindExact(object catalog, string packageCoordinate)
    {
        MethodInfo? findExact = catalog.GetType().GetMethod(
            "FindExact",
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            types: [typeof(string)],
            modifiers: null);
        Assert.NotNull(findExact);
        return findExact.Invoke(catalog, [packageCoordinate]);
    }

    private static IEnumerable<object> GetEntries(object catalog)
    {
        IEnumerable entries = GetProperty<IEnumerable>(catalog, "Entries");
        return entries.Cast<object>();
    }

    private static async Task<IEnumerable<DetectedEvidence>> DetectAsync(string repositoryPath)
    {
        Type? detectorType = Type.GetType(_detectorTypeName, throwOnError: false);
        Assert.NotNull(detectorType);
        object? detector = Activator.CreateInstance(detectorType, LoadEmbedded());
        Assert.NotNull(detector);
        MethodInfo? detectAsync = detectorType.GetMethod(
            "DetectAsync",
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            types: [typeof(string), typeof(CancellationToken)],
            modifiers: null);
        Assert.NotNull(detectAsync);
        object? invocation = detectAsync.Invoke(detector, [repositoryPath, CancellationToken.None]);
        Task task = Assert.IsAssignableFrom<Task>(invocation);
        await task;
        object? result = task.GetType().GetProperty("Result")?.GetValue(task);
        IEnumerable evidence = Assert.IsAssignableFrom<IEnumerable>(result);
        return evidence.Cast<object>().Select(item => new DetectedEvidence(
            GetProperty<string>(item, "ArtifactPath"),
            GetProperty<string>(item, "ScopeRoot"),
            GetProperty<string>(item, "Ecosystem"),
            GetProperty<string>(item, "PackageCoordinate"),
            GetProperty<object>(item, "Relationship").ToString()!,
            GetProperty<string>(item, "DetectionSource")));
    }

    private static async Task<IEnumerable<DetectedEvidence>> DetectApplicableAsync(
        string repositoryPath,
        string codeUnitPath,
        string parserLanguage)
    {
        Type? detectorType = Type.GetType(_detectorTypeName, throwOnError: false);
        Assert.NotNull(detectorType);
        object? detector = Activator.CreateInstance(detectorType, LoadEmbedded());
        Assert.NotNull(detector);
        MethodInfo? detectAsync = detectorType.GetMethod(
            "DetectApplicableAsync",
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            types:
            [
                typeof(string),
                typeof(string),
                typeof(string),
                typeof(CancellationToken),
            ],
            modifiers: null);
        Assert.NotNull(detectAsync);
        object? invocation = detectAsync.Invoke(
            detector,
            [repositoryPath, codeUnitPath, parserLanguage, CancellationToken.None]);
        Task task = Assert.IsAssignableFrom<Task>(invocation);
        await task;
        object? result = task.GetType().GetProperty("Result")?.GetValue(task);
        IEnumerable evidence = Assert.IsAssignableFrom<IEnumerable>(result);
        return evidence.Cast<object>().Select(item => new DetectedEvidence(
            GetProperty<string>(item, "ArtifactPath"),
            GetProperty<string>(item, "ScopeRoot"),
            GetProperty<string>(item, "Ecosystem"),
            GetProperty<string>(item, "PackageCoordinate"),
            GetProperty<object>(item, "Relationship").ToString()!,
            GetProperty<string>(item, "DetectionSource")));
    }

    private static async Task<DetectedFrameworkResult> DetectDetailedAsync(string repositoryPath)
    {
        Type? detectorType = Type.GetType(_detectorTypeName, throwOnError: false);
        Assert.NotNull(detectorType);
        object? detector = Activator.CreateInstance(detectorType, LoadEmbedded());
        Assert.NotNull(detector);
        MethodInfo? detectAsync = detectorType.GetMethod(
            "DetectDetailedAsync",
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            types: [typeof(string), typeof(CancellationToken)],
            modifiers: null);
        Assert.NotNull(detectAsync);
        object? invocation = detectAsync.Invoke(detector, [repositoryPath, CancellationToken.None]);
        Task task = Assert.IsAssignableFrom<Task>(invocation);
        await task;
        object? result = task.GetType().GetProperty("Result")?.GetValue(task);
        Assert.NotNull(result);
        IEnumerable evidence = GetProperty<IEnumerable>(result, "Evidence");
        IEnumerable auditOutcomes = GetProperty<IEnumerable>(result, "AuditOutcomes");
        return new DetectedFrameworkResult(
            [.. evidence.Cast<object>().Select(item => new DetectedEvidence(
                GetProperty<string>(item, "ArtifactPath"),
                GetProperty<string>(item, "ScopeRoot"),
                GetProperty<string>(item, "Ecosystem"),
                GetProperty<string>(item, "PackageCoordinate"),
                GetProperty<object>(item, "Relationship").ToString()!,
                GetProperty<string>(item, "DetectionSource")))],
            [.. auditOutcomes.Cast<object>().Select(item => new DetectedAuditOutcome(
                GetProperty<string>(item, "ArtifactPath"),
                GetProperty<object>(item, "Outcome").ToString()!,
                GetProperty<string>(item, "Reason"),
                GetProperty<string>(item, "Message")))]);
    }

    private static object GetProductionLimits()
    {
        Type? limitsType = Type.GetType(_limitsTypeName, throwOnError: false);
        Assert.NotNull(limitsType);
        PropertyInfo? property = limitsType.GetProperty(
            "Production",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(property);
        object? limits = property.GetValue(null);
        Assert.NotNull(limits);
        return limits;
    }

    private static object CreateLimits(
        long maximumAggregateArtifactBytes = 64L * 1024 * 1024,
        int maximumSupportedArtifacts = 1_000,
        int maximumParsedPackageEntries = 100_000,
        int maximumCandidateFacts = 250_000,
        int maximumFinalFactsPerCodeUnit = 64,
        int maximumRetainedAuditDetails = 100)
    {
        Type? limitsType = Type.GetType(_limitsTypeName, throwOnError: false);
        Assert.NotNull(limitsType);
        object? limits = Activator.CreateInstance(
            limitsType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args:
            [
                maximumAggregateArtifactBytes,
                maximumSupportedArtifacts,
                maximumParsedPackageEntries,
                maximumCandidateFacts,
                maximumFinalFactsPerCodeUnit,
                maximumRetainedAuditDetails,
            ],
            culture: null);
        Assert.NotNull(limits);
        return limits;
    }

    private static async Task<DetectedGlobalFrameworkResult> DetectGlobalDetailedAsync(
        string repositoryPath,
        object? limits = null)
    {
        object detector = CreateDetector(limits);
        MethodInfo? method = detector.GetType().GetMethod(
            "DetectDetailedAsync",
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            types: [typeof(string), typeof(CancellationToken)],
            modifiers: null);
        Assert.NotNull(method);
        object? invocation = method.Invoke(detector, [repositoryPath, CancellationToken.None]);
        Task task = Assert.IsAssignableFrom<Task>(invocation);
        await task;
        object? result = task.GetType().GetProperty("Result")?.GetValue(task);
        Assert.NotNull(result);
        return ReadGlobalResult(result);
    }

    private static async Task<DetectedGlobalFrameworkResult> DetectApplicableGlobalDetailedAsync(
        string repositoryPath,
        string codeUnitPath,
        string parserLanguage,
        object limits)
    {
        object detector = CreateDetector(limits);
        MethodInfo? method = detector.GetType().GetMethod(
            "DetectApplicableDetailedAsync",
            BindingFlags.Public | BindingFlags.Instance,
            binder: null,
            types:
            [
                typeof(string),
                typeof(string),
                typeof(string),
                typeof(CancellationToken),
            ],
            modifiers: null);
        Assert.NotNull(method);
        object? invocation = method.Invoke(
            detector,
            [repositoryPath, codeUnitPath, parserLanguage, CancellationToken.None]);
        Task task = Assert.IsAssignableFrom<Task>(invocation);
        await task;
        object? result = task.GetType().GetProperty("Result")?.GetValue(task);
        Assert.NotNull(result);
        return ReadGlobalResult(result);
    }

    private static object CreateDetector(object? limits)
    {
        Type? detectorType = Type.GetType(_detectorTypeName, throwOnError: false);
        Assert.NotNull(detectorType);
        object catalog = LoadEmbedded();
        if (limits is null)
        {
            object? detector = Activator.CreateInstance(detectorType, catalog);
            Assert.NotNull(detector);
            return detector;
        }

        ConstructorInfo? constructor = detectorType
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .SingleOrDefault(candidate =>
            {
                ParameterInfo[] parameters = candidate.GetParameters();
                return parameters.Length == 2 &&
                    parameters[0].ParameterType.IsInstanceOfType(catalog) &&
                    parameters[1].ParameterType.IsInstanceOfType(limits);
            });
        Assert.NotNull(constructor);
        return constructor.Invoke([catalog, limits]);
    }

    private static DetectedGlobalFrameworkResult ReadGlobalResult(object result)
    {
        IEnumerable evidence = GetProperty<IEnumerable>(result, "Evidence");
        IEnumerable auditOutcomes = GetProperty<IEnumerable>(result, "AuditOutcomes");
        return new DetectedGlobalFrameworkResult(
            GetProperty<bool>(result, "Succeeded"),
            [.. evidence.Cast<object>().Select(item => new DetectedEvidence(
                GetProperty<string>(item, "ArtifactPath"),
                GetProperty<string>(item, "ScopeRoot"),
                GetProperty<string>(item, "Ecosystem"),
                GetProperty<string>(item, "PackageCoordinate"),
                GetProperty<object>(item, "Relationship").ToString()!,
                GetProperty<string>(item, "DetectionSource")))],
            [.. auditOutcomes.Cast<object>().Select(item => new DetectedAuditOutcome(
                GetProperty<string>(item, "ArtifactPath"),
                GetProperty<object>(item, "Outcome").ToString()!,
                GetProperty<string>(item, "Reason"),
                GetProperty<string>(item, "Message")))],
            GetProperty<int>(result, "TotalAuditOutcomeCount"),
            GetProperty<object>(result, "Failure").ToString()!,
            GetNullableStringProperty(result, "Error"));
    }

    private static string? GetNullableStringProperty(object instance, string propertyName)
    {
        PropertyInfo? property = instance.GetType().GetProperty(
            propertyName,
            BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(property);
        return property.GetValue(instance) as string;
    }

    private static T GetProperty<T>(object instance, string propertyName)
    {
        PropertyInfo? property = instance.GetType().GetProperty(
            propertyName,
            BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(property);
        return Assert.IsAssignableFrom<T>(property.GetValue(instance));
    }

    private static Type RequireCatalogType()
    {
        Type? catalogType = Type.GetType(_catalogTypeName, throwOnError: false);
        Assert.NotNull(catalogType);
        return catalogType;
    }

    private sealed record CatalogCoordinate(
        string PackageCoordinate,
        string FrameworkName,
        string AllowedLanguages,
        string EvidenceKinds);

    private sealed record DetectedEvidence(
        string ArtifactPath,
        string ScopeRoot,
        string Ecosystem,
        string PackageCoordinate,
        string Relationship,
        string DetectionSource);

    private sealed record DetectedAuditOutcome(
        string ArtifactPath,
        string Outcome,
        string Reason,
        string Message);

    private sealed record DetectedFrameworkResult(
        IReadOnlyList<DetectedEvidence> Evidence,
        IReadOnlyList<DetectedAuditOutcome> AuditOutcomes);

    private sealed record DetectedGlobalFrameworkResult(
        bool Succeeded,
        IReadOnlyList<DetectedEvidence> Evidence,
        IReadOnlyList<DetectedAuditOutcome> AuditOutcomes,
        int TotalAuditOutcomeCount,
        string Failure,
        string? Error);

    private sealed class GitFixture : IDisposable
    {
        private string? _externalDirectory;

        public GitFixture()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"vulgata-framework-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
            RunGit("init", "--quiet");
            RunGit("config", "core.longpaths", "true");
            RunGit("config", "core.symlinks", "true");
        }

        public string Path { get; }

        public void Write(string relativePath, string content)
        {
            string fullPath = System.IO.Path.Combine(
                Path,
                relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        public void TrackAll() => RunGit("add", "--all");

        public void Track(params string[] relativePaths)
        {
            var arguments = new List<string> { "add", "--" };
            arguments.AddRange(relativePaths);
            RunGit([.. arguments]);
        }

        public string CreateFileSymbolicLink(
            string linkRelativePath,
            string targetRelativePath,
            string targetContent,
            bool targetOutsideRepository)
        {
            string targetRoot;
            if (targetOutsideRepository)
            {
                _externalDirectory ??= System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    $"vulgata-framework-external-{Guid.NewGuid():N}");
                targetRoot = _externalDirectory;
            }
            else
            {
                targetRoot = System.IO.Path.Combine(Path, ".fixture-targets");
            }

            string targetPath = System.IO.Path.Combine(
                targetRoot,
                targetRelativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(targetPath)!);
            File.WriteAllText(
                targetPath,
                targetContent,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            string linkPath = System.IO.Path.Combine(
                Path,
                linkRelativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(linkPath)!);
            File.CreateSymbolicLink(linkPath, targetPath);
            return targetPath;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            if (_externalDirectory is not null)
            {
                try
                {
                    Directory.Delete(_externalDirectory, recursive: true);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }

        private void RunGit(params string[] arguments)
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    WorkingDirectory = Path,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                },
            };
            foreach (string argument in arguments)
            {
                process.StartInfo.ArgumentList.Add(argument);
            }

            Assert.True(process.Start());
            string standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();
            Assert.True(
                process.ExitCode == 0,
                $"git {string.Join(' ', arguments)} failed: {standardError}");
        }
    }
}
