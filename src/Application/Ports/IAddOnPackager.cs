namespace Snail.MCP.Blender.Application.Ports;

/// <summary>Builds the zip Blender installs through Preferences → Get Extensions → Install from Disk.</summary>
public interface IAddOnPackager
{
    AddOnPackage Pack();

    /// <summary>The version and source digest of the add-on this server ships, to compare with the one Blender has installed.</summary>
    AddOnFingerprint Describe();
}
