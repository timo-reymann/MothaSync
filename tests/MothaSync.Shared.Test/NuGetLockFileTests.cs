using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;

namespace MothaSync.Shared.Test;

// Build configuration needs the real MSBuild/NuGet evaluator. Restore tests use
// isolated local feeds and caches so they never depend on nuget.org or edit the checkout.
public sealed class NuGetLockFileTests
{
    private static string RepositoryRoot
    {
        get
        {
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
                 directory is not null;
                 directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "MothaSync.slnx")))
                {
                    return directory.FullName;
                }
            }

            throw new DirectoryNotFoundException("Run these build configuration tests from the repository checkout.");
        }
    }

    public static IEnumerable<object[]> SolutionProjects() =>
        XDocument.Load(Path.Combine(RepositoryRoot, "MothaSync.slnx"))
            .Descendants("Project")
            .Select(project => new object[] { project.Attribute("Path")!.Value });

    [Theory]
    [MemberData(nameof(SolutionProjects))]
    public async Task EverySolutionProject_EnablesLockFiles_AndLocksOnlyInCi(string projectPath)
    {
        foreach (var ci in new string?[] { null, "true" })
        {
            var result = await RunDotnet(RepositoryRoot, ci, "msbuild", projectPath,
                "-nologo", "-getProperty:RestorePackagesWithLockFile,RestoreLockedMode");

            Assert.True(result.ExitCode == 0, result.Output);
            using var json = JsonDocument.Parse(result.Output);
            var properties = json.RootElement.GetProperty("Properties");
            Assert.Equal("true", properties.GetProperty("RestorePackagesWithLockFile").GetString());
            Assert.Equal(ci == "true" ? "true" : "", properties.GetProperty("RestoreLockedMode").GetString());
        }
    }

    [Theory]
    [InlineData("TRUE", "true")]
    [InlineData("True", "true")]
    [InlineData("", "")]
    [InlineData("false", "")]
    [InlineData("1", "")]
    public async Task LockedMode_CiCondition_UsesMsBuildBooleanComparison(string ci, string expected)
    {
        var result = await RunDotnet(RepositoryRoot, ci, "msbuild", "Directory.Build.props",
            "-nologo", "-getProperty:RestoreLockedMode");

        Assert.True(result.ExitCode == 0, result.Output);
        Assert.Equal(expected, result.Output.Trim());
    }

    [Theory]
    [MemberData(nameof(SolutionProjects))]
    public void EverySolutionProject_HasCompleteLockFile_WithPinnedPackages(string projectPath)
    {
        var project = XDocument.Load(Path.Combine(RepositoryRoot, projectPath));
        var targetFramework = project.Descendants("TargetFramework").Single().Value;
        var lockPath = Path.Combine(RepositoryRoot, Path.GetDirectoryName(projectPath)!, "packages.lock.json");
        Assert.True(File.Exists(lockPath), $"Missing lock file for {projectPath}");

        using var json = JsonDocument.Parse(File.ReadAllText(lockPath));
        Assert.Equal(1, json.RootElement.GetProperty("version").GetInt32());
        var packages = json.RootElement.GetProperty("dependencies").GetProperty(targetFramework)
            .EnumerateObject().ToDictionary(package => package.Name, package => package.Value,
                StringComparer.OrdinalIgnoreCase);
        var references = project.Descendants("PackageReference")
            .ToDictionary(reference => reference.Attribute("Include")!.Value,
                reference => reference.Attribute("Version")!.Value, StringComparer.OrdinalIgnoreCase);

        var directPackages = packages.Where(package => package.Value.GetProperty("type").GetString() == "Direct")
            .Select(package => package.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.True(directPackages.SetEquals(references.Keys), $"Direct packages differ for {projectPath}");
        foreach (var (name, version) in references)
        {
            Assert.Equal($"[{version}, )", packages[name].GetProperty("requested").GetString());
        }

        foreach (var (name, package) in packages)
        {
            if (package.GetProperty("type").GetString() != "Project")
            {
                Assert.False(string.IsNullOrWhiteSpace(package.GetProperty("resolved").GetString()), name);
                Assert.Equal(64, Convert.FromBase64String(package.GetProperty("contentHash").GetString()!).Length);
            }

            if (package.TryGetProperty("dependencies", out var dependencies))
            {
                foreach (var dependency in dependencies.EnumerateObject())
                {
                    Assert.True(packages.ContainsKey(dependency.Name),
                        $"{projectPath}: {name} references unpinned dependency {dependency.Name}");
                }
            }
        }
    }

    [Fact]
    public async Task LocalRestore_CreatesLockFile_WithDirectAndTransitivePackages()
    {
        using var project = new RestoreProject();

        var result = await project.Restore(ci: null);

        Assert.True(result.ExitCode == 0, result.Output);
        using var json = JsonDocument.Parse(File.ReadAllText(project.LockPath));
        var packages = json.RootElement.GetProperty("dependencies").GetProperty("net10.0");
        var direct = packages.GetProperty("Fixture.Package");
        Assert.Equal("Direct", direct.GetProperty("type").GetString());
        Assert.Equal("1.0.0", direct.GetProperty("resolved").GetString());
        Assert.Equal(64, Convert.FromBase64String(direct.GetProperty("contentHash").GetString()!).Length);
        var transitive = packages.GetProperty("Fixture.Dependency");
        Assert.Equal("Transitive", transitive.GetProperty("type").GetString());
        Assert.Equal("1.0.0", transitive.GetProperty("resolved").GetString());
        Assert.Equal(64, Convert.FromBase64String(transitive.GetProperty("contentHash").GetString()!).Length);
    }

    [Fact]
    public async Task CiRestore_WithMatchingLockFile_SucceedsWithoutRewritingIt()
    {
        using var project = new RestoreProject();
        var initial = await project.Restore(ci: null);
        Assert.True(initial.ExitCode == 0, initial.Output);
        var original = File.ReadAllBytes(project.LockPath);

        var result = await project.Restore(ci: "true");

        Assert.True(result.ExitCode == 0, result.Output);
        Assert.Equal(original, File.ReadAllBytes(project.LockPath));
    }

    [Theory]
    [InlineData("update")]
    [InlineData("add")]
    [InlineData("remove")]
    public async Task CiRestore_WhenPackageReferencesDrift_FailsWithNu1004_AndPreservesLockFile(string change)
    {
        using var project = new RestoreProject();
        var initial = await project.Restore(ci: null);
        Assert.True(initial.ExitCode == 0, initial.Output);
        var original = File.ReadAllBytes(project.LockPath);
        var document = XDocument.Load(project.ProjectPath);
        var reference = document.Descendants("PackageReference").Single();
        switch (change)
        {
            case "update":
                reference.SetAttributeValue("Version", "1.0.1");
                break;
            case "add":
                reference.AddAfterSelf(new XElement("PackageReference",
                    new XAttribute("Include", "Fixture.Dependency"), new XAttribute("Version", "1.0.0")));
                break;
            case "remove":
                reference.Remove();
                break;
        }
        document.Save(project.ProjectPath);

        var result = await project.Restore(ci: "true");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("NU1004", result.Output);
        Assert.Equal(original, File.ReadAllBytes(project.LockPath));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("false")]
    public async Task LocalRestore_WhenPackageVersionChanges_UpdatesLockFile(string? ci)
    {
        using var project = new RestoreProject();
        var initial = await project.Restore(ci);
        Assert.True(initial.ExitCode == 0, initial.Output);
        var document = XDocument.Load(project.ProjectPath);
        document.Descendants("PackageReference").Single().SetAttributeValue("Version", "1.0.1");
        document.Save(project.ProjectPath);

        var result = await project.Restore(ci);

        Assert.True(result.ExitCode == 0, result.Output);
        using var json = JsonDocument.Parse(File.ReadAllText(project.LockPath));
        var package = json.RootElement.GetProperty("dependencies").GetProperty("net10.0")
            .GetProperty("Fixture.Package");
        Assert.Equal("[1.0.1, )", package.GetProperty("requested").GetString());
        Assert.Equal("1.0.1", package.GetProperty("resolved").GetString());
    }

    private static async Task<(int ExitCode, string Output)> RunDotnet(
        string workingDirectory, string? ci, params string[] arguments)
    {
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }
        // Change only the child environment so xunit can run alongside other tests.
        start.Environment.Remove("CI");
        start.Environment.Remove("RestoreLockedMode");
        start.Environment.Remove("RestorePackagesWithLockFile");
        if (ci is not null)
        {
            start.Environment["CI"] = ci;
        }
        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        start.Environment["DOTNET_NOLOGO"] = "1";

        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            throw new TimeoutException($"dotnet {string.Join(' ', arguments)} exceeded 15 seconds.\n"
                + await output + await error);
        }

        return (process.ExitCode, await output + await error);
    }

    private sealed class RestoreProject : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), $"mothasync nuget test {Guid.NewGuid():N}");
        public string ProjectPath => Path.Combine(_root, "Fixture.csproj");
        public string LockPath => Path.Combine(_root, "packages.lock.json");

        public RestoreProject()
        {
            Directory.CreateDirectory(_root);
            File.Copy(Path.Combine(RepositoryRoot, "Directory.Build.props"), Path.Combine(_root, "Directory.Build.props"));
            File.Copy(Path.Combine(RepositoryRoot, "global.json"), Path.Combine(_root, "global.json"));
            File.WriteAllText(ProjectPath, """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
                  <ItemGroup><PackageReference Include="Fixture.Package" Version="1.0.0" /></ItemGroup>
                </Project>
                """);
            File.WriteAllText(Path.Combine(_root, "NuGet.Config"), """
                <configuration>
                  <packageSources><clear /><add key="local" value="feed" /></packageSources>
                  <config><add key="globalPackagesFolder" value="packages" /></config>
                </configuration>
                """);
            CreatePackage("Fixture.Dependency", "1.0.0");
            CreatePackage("Fixture.Package", "1.0.0", "Fixture.Dependency");
            CreatePackage("Fixture.Package", "1.0.1", "Fixture.Dependency");
        }

        public Task<(int ExitCode, string Output)> Restore(string? ci) =>
            RunDotnet(_root, ci, "restore", ProjectPath, "--configfile", Path.Combine(_root, "NuGet.Config"),
                "--disable-parallel", "-p:NuGetAudit=false", "--nologo");

        private void CreatePackage(string id, string version, string? dependency = null)
        {
            var feed = Directory.CreateDirectory(Path.Combine(_root, "feed")).FullName;
            using var archive = ZipFile.Open(Path.Combine(feed, $"{id}.{version}.nupkg"), ZipArchiveMode.Create);
            using var writer = new StreamWriter(archive.CreateEntry($"{id}.nuspec").Open());
            var metadata = new XElement("metadata",
                new XElement("id", id), new XElement("version", version),
                new XElement("authors", "MothaSync tests"), new XElement("description", "Offline restore fixture"));
            if (dependency is not null)
            {
                metadata.Add(new XElement("dependencies", new XElement("dependency",
                    new XAttribute("id", dependency), new XAttribute("version", "[1.0.0]"))));
            }
            writer.Write(new XElement("package", metadata).ToString());
        }

        public void Dispose() => Directory.Delete(_root, recursive: true);
    }
}
