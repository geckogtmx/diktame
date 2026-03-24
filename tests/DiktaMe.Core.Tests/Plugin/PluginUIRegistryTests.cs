using DiktaMe.Plugin;
using FluentAssertions;

namespace DiktaMe.Core.Tests.Plugin;

public sealed class PluginUIRegistryTests
{
    private readonly PluginUIRegistry _registry = new();

    private static PluginSettingsPageInfo MakePage(string pluginId = "test-plugin") =>
        new(pluginId, $"nav-{pluginId}", "Test Settings", "\uE713", () => new object());

    private static List<PluginTrayMenuItem> MakeItems(string label = "Test Item") =>
    [
        new PluginTrayMenuItem(label, () => { }),
    ];

    [Fact]
    public void Add_settings_page_appears_in_list()
    {
        var page = MakePage();

        _registry.AddSettingsPage(page);

        _registry.SettingsPages.Should().ContainSingle()
            .Which.Should().BeSameAs(page);
    }

    [Fact]
    public void Remove_settings_page_removes_from_list()
    {
        _registry.AddSettingsPage(MakePage("p1"));

        _registry.RemoveSettingsPage("p1");

        _registry.SettingsPages.Should().BeEmpty();
    }

    [Fact]
    public void Add_tray_items_appear_in_list()
    {
        var items = MakeItems();

        _registry.AddTrayMenuItems("test-plugin", items);

        _registry.TrayMenuItems.Should().HaveCount(1);
        _registry.TrayMenuItems[0].Label.Should().Be("Test Item");
    }

    [Fact]
    public void Remove_tray_items_removes_from_list()
    {
        _registry.AddTrayMenuItems("p1", MakeItems());

        _registry.RemoveTrayMenuItems("p1");

        _registry.TrayMenuItems.Should().BeEmpty();
    }

    [Fact]
    public void Add_fires_contributions_changed()
    {
        var fired = false;
        _registry.ContributionsChanged += (_, _) => fired = true;

        _registry.AddSettingsPage(MakePage());

        fired.Should().BeTrue();
    }

    [Fact]
    public void Remove_fires_contributions_changed()
    {
        _registry.AddSettingsPage(MakePage("p1"));

        var fired = false;
        _registry.ContributionsChanged += (_, _) => fired = true;
        _registry.RemoveSettingsPage("p1");

        fired.Should().BeTrue();
    }

    [Fact]
    public void Initially_empty()
    {
        _registry.SettingsPages.Should().BeEmpty();
        _registry.TrayMenuItems.Should().BeEmpty();
    }
}
