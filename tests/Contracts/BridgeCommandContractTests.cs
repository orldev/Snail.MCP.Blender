using System.Text.RegularExpressions;

namespace Snail.MCP.Blender.Tests.Contracts;

/// <summary>The C# command catalog and the add-on's registry are two copies of one contract, module by module: each partial of the catalog equals the registrations of the add-on file it names.</summary>
public class BridgeCommandContractTests
{
    private static readonly Regex Registration = new(@"^@(?:immediate_)?command\(""([a-z_]+)""\)", RegexOptions.Multiline | RegexOptions.Compiled);

    [Fact]
    public void CommandCatalog_EqualsTheAddOnRegistry()
    {
        var registered = AddOnContractTests.RegisteredCommands().ToHashSet(StringComparer.Ordinal);
        var catalogued = BridgeCommands.All.Select(command => command.Name).ToHashSet(StringComparer.Ordinal);

        Assert.Equal(registered, catalogued);
    }

    [Fact]
    public void CommandCatalog_NamesEveryCommandOnce()
    {
        var names = BridgeCommands.All.Select(command => command.Name).ToList();

        Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
    }

    [Theory]
    [MemberData(nameof(Modules))]
    public void CommandCatalog_ModulePartial_EqualsTheRegistrationsOfThatAddOnFile(string module)
    {
        var source = File.ReadAllText(Path.Combine(AddOnContractTests.AddOnSource(), $"{module}.py"));
        var registered = Registration.Matches(source).Select(match => match.Groups[1].Value).Order().ToList();

        Assert.Equal(registered, BridgeCommands.Of(module).Select(command => command.Name).Order());
    }

    [Fact]
    public void AddOnModules_NameEveryFileThatRegistersCommands()
    {
        var registering = Directory.EnumerateFiles(AddOnContractTests.AddOnSource(), "*.py")
            .Where(file => Registration.IsMatch(File.ReadAllText(file)))
            .Select(Path.GetFileNameWithoutExtension)
            .Order();

        Assert.Equal(registering, AddOnModules.All.Order());
    }

    public static TheoryData<string> Modules()
    {
        var modules = new TheoryData<string>();

        foreach (var module in AddOnModules.All)
        {
            modules.Add(module);
        }

        return modules;
    }
}
