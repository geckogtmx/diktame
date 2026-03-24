namespace DiktaMe.Plugin;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class PluginEntryAttribute : Attribute
{
    public string Id { get; }
    public string DisplayName { get; }
    public string Version { get; }

    public PluginEntryAttribute(string id, string displayName, string version)
    {
        Id = id;
        DisplayName = displayName;
        Version = version;
    }
}
