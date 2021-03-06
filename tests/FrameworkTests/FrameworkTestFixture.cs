﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Buildalyzer;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Shouldly;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Buildalyzer.Environment;

namespace FrameworkTests
{
#if Is_Windows
    [TestFixture]
    [NonParallelizable]
    public class FrameworkTestFixture
    {
        private const LoggerVerbosity Verbosity = LoggerVerbosity.Normal;
        private const bool BinaryLog = false;

        private static string[] _projectFiles =
        {
            @"LegacyFrameworkProject\LegacyFrameworkProject.csproj",
            @"LegacyFrameworkProjectWithReference\LegacyFrameworkProjectWithReference.csproj",
            @"LegacyFrameworkProjectWithPackageReference\LegacyFrameworkProjectWithPackageReference.csproj",
            @"SdkNetCoreProject\SdkNetCoreProject.csproj",
            @"SdkNetStandardProject\SdkNetStandardProject.csproj",
            @"SdkNetCoreProjectImport\SdkNetCoreProjectImport.csproj",
            @"SdkNetStandardProjectImport\SdkNetStandardProjectImport.csproj",
            @"SdkNetStandardProjectWithPackageReference\SdkNetStandardProjectWithPackageReference.csproj",
            @"SdkFrameworkProject\SdkFrameworkProject.csproj",
            @"SdkProjectWithImportedProps\SdkProjectWithImportedProps.csproj",
            @"SdkMultiTargetingProject\SdkMultiTargetingProject.csproj"
        };

        [TestCaseSource(nameof(_projectFiles))]
        public void LoadsProject(string projectFile)
        {
            // Given
            StringWriter log = new StringWriter();
            ProjectAnalyzer analyzer = GetProjectAnalyzer(projectFile, log);

            // When
            DeleteProjectDirectory(projectFile, "obj");
            DeleteProjectDirectory(projectFile, "bin");
            Project project = analyzer.Load();

            // Then
            project.ShouldNotBeNull(log.ToString());
        }

        [TestCaseSource(nameof(_projectFiles))]
        public void DesignTimeBuildsProject(string projectFile)
        {
            // Given
            StringWriter log = new StringWriter();
            ProjectAnalyzer analyzer = GetProjectAnalyzer(projectFile, log);

            // When
            DeleteProjectDirectory(projectFile, "obj");
            DeleteProjectDirectory(projectFile, "bin");
            AnalyzerResults results = analyzer.BuildAllTargetFrameworks();

            // Then
            results.Count.ShouldBeGreaterThan(0, log.ToString());
            results.First().ProjectInstance.ShouldNotBeNull(log.ToString());
        }

        [TestCaseSource(nameof(_projectFiles))]
        public void BuildsProject(string projectFile)
        {
            // Given
            StringWriter log = new StringWriter();
            ProjectAnalyzer analyzer = GetProjectAnalyzer(projectFile, log);
            EnvironmentOptions options = new EnvironmentOptions
            {
                DesignTime = false
            };

            // When
            DeleteProjectDirectory(projectFile, "obj");
            DeleteProjectDirectory(projectFile, "bin");
            AnalyzerResults results = analyzer.BuildAllTargetFrameworks(options);

            // Then
            results.Count.ShouldBeGreaterThan(0, log.ToString());
            results.First().ProjectInstance.ShouldNotBeNull(log.ToString());
        }

        [TestCaseSource(nameof(_projectFiles))]
        public void GetsSourceFiles(string projectFile)
        {
            // Given
            StringWriter log = new StringWriter();
            ProjectAnalyzer analyzer = GetProjectAnalyzer(projectFile, log);

            // When
            IReadOnlyList<string> sourceFiles = analyzer.BuildAllTargetFrameworks().First().GetSourceFiles();

            // Then
            sourceFiles.ShouldNotBeNull(log.ToString());
            sourceFiles.Select(x => Path.GetFileName(x).Split('.').Reverse().Take(2).Reverse().First()).ShouldBe(new[]
            {
                "Class1",
                "AssemblyAttributes",
                "AssemblyInfo"
            }, true, log.ToString());
        }

        [Test]
        public void BuildAllTargetFrameworksGetsSourceFiles()
        {
            // Given
            StringWriter log = new StringWriter();
            ProjectAnalyzer analyzer = GetProjectAnalyzer(@"SdkMultiTargetingProject\SdkMultiTargetingProject.csproj", log);

            // When
            AnalyzerResults results = analyzer.BuildAllTargetFrameworks();

            // Then
            results.Count.ShouldBe(2);
            results.TargetFrameworks.ShouldBe(new[] { "net462", "netstandard2.0" }, true, log.ToString());
            results["net462"].GetSourceFiles().Select(x => Path.GetFileName(x).Split('.').Reverse().Take(2).Reverse().First()).ShouldBe(new[]
            {
                "Class1",
                "AssemblyAttributes",
                "AssemblyInfo"
            }, true, log.ToString());
            results["netstandard2.0"].GetSourceFiles().Select(x => Path.GetFileName(x).Split('.').Reverse().Take(2).Reverse().First()).ShouldBe(new[]
            {
                "Class2",
                "AssemblyAttributes",
                "AssemblyInfo"
            }, true, log.ToString());
        }

        [Test]
        public void BuildTargetFrameworkGetsSourceFiles()
        {            
            // Given
            StringWriter log = new StringWriter();
            ProjectAnalyzer analyzer = GetProjectAnalyzer(@"SdkMultiTargetingProject\SdkMultiTargetingProject.csproj", log);

            // When
            IReadOnlyList<string> sourceFiles = analyzer.Build("net462").GetSourceFiles();

            // Then
            sourceFiles.ShouldNotBeNull(log.ToString());
            sourceFiles.Select(x => Path.GetFileName(x).Split('.').Reverse().Take(2).Reverse().First()).ShouldBe(new[]
            {
                "Class1",
                "AssemblyAttributes",
                "AssemblyInfo"
            }, true, log.ToString());

            // When
            log.GetStringBuilder().Clear();
            sourceFiles = analyzer.Build("netstandard2.0").GetSourceFiles();

            // Then
            sourceFiles.ShouldNotBeNull(log.ToString());
            sourceFiles.Select(x => Path.GetFileName(x).Split('.').Reverse().Take(2).Reverse().First()).ShouldBe(new[]
            {
                "Class2",
                "AssemblyAttributes",
                "AssemblyInfo"
            }, true, log.ToString());
        }

        [TestCaseSource(nameof(_projectFiles))]
        public void GetsVirtualProjectSourceFiles(string projectFile)
        {
            // Given
            StringWriter log = new StringWriter();
            projectFile = GetProjectPath(projectFile);
            XDocument projectDocument = XDocument.Load(projectFile);
            projectFile = projectFile.Replace(".csproj", "Virtual.csproj");
            ProjectAnalyzer analyzer = new AnalyzerManager(
                new AnalyzerManagerOptions
                {
                    LogWriter = log,
                    LoggerVerbosity = Verbosity
                })
                .GetProject(projectFile, projectDocument);

            // When
            IReadOnlyList<string> sourceFiles = analyzer.BuildAllTargetFrameworks().First().GetSourceFiles();

            // Then
            sourceFiles.ShouldNotBeNull(log.ToString());
            sourceFiles.ShouldContain(x => x.EndsWith("Class1.cs"), log.ToString());
        }

        [TestCaseSource(nameof(_projectFiles))]
        public void GetsReferences(string projectFile)
        {
            // Given
            StringWriter log = new StringWriter();
            ProjectAnalyzer analyzer = GetProjectAnalyzer(projectFile, log);

            // When
            IReadOnlyList<string> references = analyzer.BuildAllTargetFrameworks().First().GetReferences();

            // Then
            references.ShouldNotBeNull(log.ToString());
            references.ShouldContain(x => x.EndsWith("mscorlib.dll"), log.ToString());
        }

        [Test]
        public void SdkProjectWithPackageReferenceGetsReferences()
        {
            // Given
            StringWriter log = new StringWriter();
            ProjectAnalyzer analyzer = GetProjectAnalyzer(@"SdkNetStandardProjectWithPackageReference\SdkNetStandardProjectWithPackageReference.csproj", log);

            // When
            IReadOnlyList<string> references = analyzer.BuildAllTargetFrameworks().First().GetReferences();

            // Then
            references.ShouldNotBeNull(log.ToString());
            references.ShouldContain(x => x.EndsWith("NodaTime.dll"), log.ToString());
        }

        [Test]
        public void LegacyFrameworkProjectWithPackageReferenceGetsReferences()
        {
            // Given
            StringWriter log = new StringWriter();
            ProjectAnalyzer analyzer = GetProjectAnalyzer(@"LegacyFrameworkProjectWithPackageReference\LegacyFrameworkProjectWithPackageReference.csproj", log);

            // When
            IReadOnlyList<string> references = analyzer.BuildAllTargetFrameworks().First().GetReferences();

            // Then
            references.ShouldNotBeNull(log.ToString());
            references.ShouldContain(x => x.EndsWith("NodaTime.dll"), log.ToString());
        }

        [Test]
        public void GetsProjectsInSolution()
        {
            // Given
            StringWriter log = new StringWriter();

            // When
            AnalyzerManager manager = new AnalyzerManager(
                GetProjectPath("TestProjects.sln"),
                new AnalyzerManagerOptions
                {
                    LogWriter = log,
                    LoggerVerbosity = Verbosity
                });

            // Then
            _projectFiles.Select(x => GetProjectPath(x)).ShouldBeSubsetOf(manager.Projects.Keys, log.ToString());
        }

        private static ProjectAnalyzer GetProjectAnalyzer(string projectFile, StringWriter log)
        {
            ProjectAnalyzer analyzer =  new AnalyzerManager(
                new AnalyzerManagerOptions
                {
                    LogWriter = log,
                    LoggerVerbosity = Verbosity
                })
                .GetProject(GetProjectPath(projectFile));
            if(BinaryLog)
            {
                analyzer.AddBinaryLogger(Path.Combine(@"E:\Temp\", Path.ChangeExtension(Path.GetFileName(projectFile), ".framework.binlog")));
            }
            return analyzer;
        }

        private static string GetProjectPath(string file) =>
            Path.GetFullPath(
                Path.Combine(
                    Path.GetDirectoryName(typeof(FrameworkTestFixture).Assembly.Location),
                    @"..\..\..\projects\" + file));

        private static void DeleteProjectDirectory(string projectFile, string directory)
        {
            string path = Path.Combine(Path.GetDirectoryName(GetProjectPath(projectFile)), directory);
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
    }
#endif
}
