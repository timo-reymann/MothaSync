using System.Diagnostics;
using System.Text.Json;
using System.Xml.Linq;

namespace MothaSync.Shared.Test;

public sealed class NuGetLockFileTests : IDisposable
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"mothasync-nuget-test-{Guid.NewGuid():N}");

    public NuGetLockFileTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    public static IEnumerable<object[]> SolutionProjects() =>
        XDocument.Load(Path.Combine(RepositoryRoot, "MothaSync.slnx"))
            .Descendants("Project")
            .Select(project => new object[] { project.Attribute("Path")!.Value });

    [Theory]
    [MemberData(nameof(SolutionProjects))]
    public void LockFile_MatchesProjectFrameworkAndDirectDependencies(string relativeProjectPath)
    {
        var projectPath = Path.Combine(RepositoryRoot, relativeProjectPath);
        var project = XDocument.Load(projectPath);
        using var lockFile = ReadLockFile(projectPath);

        Assert.Equal(1, lockFile.RootElement.GetProperty("version").GetInt32());
        var frameworks = lockFile.RootElement.GetProperty("dependencies");
        var framework = Assert.Single(project.Descendants("TargetFramework")).Value;
        Assert.Equal(framework, Assert.Single(frameworks.EnumerateObject()).Name);

        var packages = frameworks.GetProperty(framework);
        var references = project.Descendants("PackageReference").ToDictionary(
            reference => reference.Attribute("Include")!.Value,
            reference => reference.Attribute("Version")!.Value,
            StringComparer.OrdinalIgnoreCase);
        var directPackages = packages.EnumerateObject()
            .Where(package => package.Value.GetProperty("type").GetString() == "Direct")
            .ToDictionary(package => package.Name, package => package.Value, StringComparer.OrdinalIgnoreCase);

        Assert.Equal(references.Keys.Order(StringComparer.OrdinalIgnoreCase),
            directPackages.Keys.Order(StringComparer.OrdinalIgnoreCase));
        foreach (var (name, version) in references)
        {
            Assert.Equal($"[{version}, )", directPackages[name].GetProperty("requested").GetString());
        }

        var projectReferences = ReferencedProjectNames(projectPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase);
        var lockedProjects = packages.EnumerateObject()
            .Where(package => package.Value.GetProperty("type").GetString() == "Project")
            .Select(package => package.Name)
            .Order(StringComparer.OrdinalIgnoreCase);
        Assert.Equal(projectReferences, lockedProjects, StringComparer.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(SolutionProjects))]
    public void LockFile_PinsCompleteDependencyGraphWithContentHashes(string relativeProjectPath)
    {
        using var lockFile = ReadLockFile(Path.Combine(RepositoryRoot, relativeProjectPath));
        foreach (var framework in lockFile.RootElement.GetProperty("dependencies").EnumerateObject())
        {
            var packages = framework.Value.EnumerateObject()
                .ToDictionary(package => package.Name, package => package.Value, StringComparer.OrdinalIgnoreCase);
            Assert.NotEmpty(packages);
            foreach (var (name, package) in packages)
            {
                var type = package.GetProperty("type").GetString();
                Assert.Contains(type, new[] { "Direct", "Transitive", "Project" });
                if (type != "Project")
                {
                    Assert.False(string.IsNullOrWhiteSpace(package.GetProperty("resolved").GetString()), name);
                    Assert.Equal(64, Convert.FromBase64String(package.GetProperty("contentHash").GetString()!).Length);
                }

                if (package.TryGetProperty("dependencies", out var dependencies))
                {
                    foreach (var dependency in dependencies.EnumerateObject())
                    {
                        Assert.True(packages.ContainsKey(dependency.Name),
                            $"{relativeProjectPath}: {name} depends on unpinned package {dependency.Name}.");
                    }
                }
            }
        }
    }

    [Fact]
    public void LockFiles_SamePackageVersionHasSameContentHashAcrossProjects()
    {
        var hashes = new Dictionary<(string Name, string Version), string>();
        foreach (var project in SolutionProjects())
        {
            using var lockFile = ReadLockFile(Path.Combine(RepositoryRoot, (string)project[0]));
            foreach (var framework in lockFile.RootElement.GetProperty("dependencies").EnumerateObject())
            {
                foreach (var package in framework.Value.EnumerateObject())
                {
                    if (package.Value.GetProperty("type").GetString() == "Project") continue;

                    var key = (package.Name.ToUpperInvariant(), package.Value.GetProperty("resolved").GetString()!);
                    var hash = package.Value.GetProperty("contentHash").GetString()!;
                    if (hashes.TryGetValue(key, out var existingHash)) Assert.Equal(existingHash, hash);
                    else hashes.Add(key, hash);
                }
            }
        }
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("false", "")]
    [InlineData("1", "")]
    [InlineData("true", "true")]
    [InlineData("TRUE", "true")]
    [InlineData("True", "true")]
    public async Task BuildProperties_AlwaysGenerateLocksAndEnableLockedModeOnlyForCiTrue(
        string? ci, string expectedLockedMode)
    {
        WritePropertiesProbe();

        var result = await RunDotnet(ci, "msbuild", "Probe.proj", "-nologo",
            "-getProperty:RestorePackagesWithLockFile,RestoreLockedMode");

        Assert.Equal(0, result.ExitCode);
        using var output = JsonDocument.Parse(result.Output);
        var properties = output.RootElement.GetProperty("Properties");
        Assert.Equal("true", properties.GetProperty("RestorePackagesWithLockFile").GetString());
        Assert.Equal(expectedLockedMode, properties.GetProperty("RestoreLockedMode").GetString());
    }

    [Theory]
    [InlineData("true", "false")]
    [InlineData(null, "true")]
    public async Task BuildProperties_RespectExplicitLockedModeOverride(string? ci, string lockedMode)
    {
        WritePropertiesProbe();

        var result = await RunDotnet(ci, "msbuild", "Probe.proj", "-nologo",
            $"-p:RestoreLockedMode={lockedMode}", "-getProperty:RestoreLockedMode");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(lockedMode, result.Output.Trim());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("true")]
    public async Task Restore_UnchangedProjectSucceedsWithoutRewritingLockFile(string? ci)
    {
        CopySharedProject();
        var originalLock = File.ReadAllBytes(LockFilePath);

        var result = await Restore(ci);

        Assert.True(result.ExitCode == 0, result.Output);
        Assert.Equal(originalLock, File.ReadAllBytes(LockFilePath));
    }

    [Theory]
    [InlineData("version")]
    [InlineData("added")]
    [InlineData("removed")]
    public async Task Restore_CiRejectsDependencyDriftWithoutRewritingLockFile(string change)
    {
        CopySharedProject();
        var originalLock = File.ReadAllBytes(LockFilePath);
        var project = XDocument.Load(ProjectPath);
        var reference = project.Descendants("PackageReference").First();
        switch (change)
        {
            case "version":
                var version = Version.Parse(reference.Attribute("Version")!.Value);
                reference.SetAttributeValue("Version", $"{version.Major + 1}.0.0");
                break;
            case "added":
                reference.AddAfterSelf(new XElement("PackageReference",
                    new XAttribute("Include", "MothaSync.LockFileRegression"), new XAttribute("Version", "1.0.0")));
                break;
            case "removed":
                reference.Remove();
                break;
        }
        project.Save(ProjectPath);

        var result = await Restore("true");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("NU1004", result.Output);
        Assert.Equal(originalLock, File.ReadAllBytes(LockFilePath));
    }

    [Fact]
    public async Task Restore_OutsideCiRegeneratesLockFileAfterDependencyRemoval()
    {
        CopySharedProject();
        var project = XDocument.Load(ProjectPath);
        var reference = project.Descendants("PackageReference").First();
        var removedPackage = reference.Attribute("Include")!.Value;
        reference.Remove();
        project.Save(ProjectPath);

        var result = await Restore(null);

        Assert.True(result.ExitCode == 0, result.Output);
        using var lockFile = ReadLockFile(ProjectPath);
        Assert.All(lockFile.RootElement.GetProperty("dependencies").EnumerateObject(), framework =>
            Assert.False(framework.Value.TryGetProperty(removedPackage, out _)));
    }

    [Fact]
    public async Task Restore_OutsideCiCreatesMissingLockFile()
    {
        CopySharedProject();
        File.Delete(LockFilePath);

        var result = await Restore(null);

        Assert.True(result.ExitCode == 0, result.Output);
        using var generated = ReadLockFile(ProjectPath);
        using var committed = ReadLockFile(Path.Combine(RepositoryRoot, "src", "MothaSync.Shared", "MothaSync.Shared.csproj"));
        Assert.True(JsonElement.DeepEquals(committed.RootElement, generated.RootElement));
    }

    private string ProjectPath => Path.Combine(_root, "MothaSync.Shared.csproj");
    private string LockFilePath => Path.Combine(_root, "packages.lock.json");

    private void CopySharedProject()
    {
        var source = Path.Combine(RepositoryRoot, "src", "MothaSync.Shared");
        File.Copy(Path.Combine(source, "MothaSync.Shared.csproj"), ProjectPath);
        File.Copy(Path.Combine(source, "packages.lock.json"), LockFilePath);
        File.Copy(Path.Combine(RepositoryRoot, "Directory.Build.props"), Path.Combine(_root, "Directory.Build.props"));
        Directory.CreateDirectory(Path.Combine(_root, "feed"));
    }

    private void WritePropertiesProbe() =>
        new XDocument(new XElement("Project", new XElement("Import",
            new XAttribute("Project", Path.Combine(RepositoryRoot, "Directory.Build.props")))))
            .Save(Path.Combine(_root, "Probe.proj"));

    // The shared project and its packages are already restored to run this test assembly.
    // An empty local feed and disabled audit keep these restore checks offline.
    private Task<(int ExitCode, string Output)> Restore(string? ci) =>
        RunDotnet(ci, "restore", ProjectPath, "--source", Path.Combine(_root, "feed"),
            "-p:NuGetAudit=false", "--verbosity", "minimal");

    private async Task<(int ExitCode, string Output)> RunDotnet(string? ci, params string[] arguments)
    {
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        {
            WorkingDirectory = _root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        start.Environment.Remove("CI");
        start.Environment.Remove("RestoreLockedMode");
        start.Environment.Remove("RestorePackagesWithLockFile");
        if (ci is not null) start.Environment["CI"] = ci;
        start.Environment["DOTNET_NOLOGO"] = "1";
        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            Assert.Fail($"dotnet {string.Join(' ', arguments)} exceeded 10 seconds.");
        }
        return (process.ExitCode, await stdout + await stderr);
    }

    private static JsonDocument ReadLockFile(string projectPath) =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(Path.GetDirectoryName(projectPath)!, "packages.lock.json")));

    private static IEnumerable<string> ReferencedProjectNames(string projectPath)
    {
        foreach (var reference in XDocument.Load(projectPath).Descendants("ProjectReference"))
        {
            var referencedPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(projectPath)!,
                reference.Attribute("Include")!.Value.Replace('\\', Path.DirectorySeparatorChar)));
            yield return Path.GetFileNameWithoutExtension(referencedPath);
            foreach (var transitive in ReferencedProjectNames(referencedPath)) yield return transitive;
        }
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MothaSync.slnx"))) return directory.FullName;
        }
        throw new DirectoryNotFoundException("Could not find MothaSync.slnx above the test output directory.");
    }
}
