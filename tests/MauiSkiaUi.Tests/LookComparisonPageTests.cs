using Xunit;

namespace MauiSkiaUi.Tests;

[Collection(GlobalStateCollection.Name)]
public class LookComparisonPageTests
{
    [Fact]
    public void ThePageListsTheStockLooksAndTheAppsOwn()
    {
        var custom = new IosSkUiLook();
        var page = new SkUiLookComparisonPage().AddLook("Brand", custom);
        Assert.True(page.SelectLook("android (material 3)"));
        Assert.True(page.SelectLook("Brand"));
        Assert.False(page.SelectLook("Nonexistent"));
        Assert.Contains(SkUiLookComparisonPage.StockLooks, entry => entry.Look == AndroidMaterial3SkUiLook.Instance);

        // The look the page opens with takes the name AddLook gives it, once.
        var named = new SkUiLookComparisonPage(custom).AddLook("Brand", custom);
        Assert.True(named.SelectLook("Brand"));
        Assert.False(named.SelectLook(nameof(IosSkUiLook)));
    }

    [Fact]
    public void OptionsRebuildTheRows()
    {
        var page = new SkUiLookComparisonPage(AndroidSkUiLook.Instance)
            .AddRow("Brand button", () => new Button { Text = "Go" }, () => new SkUiButton { Text = "Go" });
        page.ShowAppStyles = false;
        page.Overlay = true;
        Assert.False(page.ShowAppStyles);
        Assert.True(page.Overlay);
    }
}
