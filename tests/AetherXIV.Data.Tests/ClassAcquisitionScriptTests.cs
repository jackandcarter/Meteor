using System.Text.RegularExpressions;
using Xunit;

namespace AetherXIV.Data.Tests;

public sealed class ClassAcquisitionScriptTests
{
    [Fact]
    public void MultiWeaponShopContainsEveryRetailBaseClassStarter()
    {
        string root = FindRepositoryRoot();
        string catalog = File.ReadAllText(Path.Combine(root, "Data", "scripts", "shopgoods.lua"));
        uint[] starters =
        [
            4020001, 4030010, 4040001, 4070001, 4080201, 5020001, 5030101,
            6010001, 6020001, 6030001, 6040001, 6050003, 6060006, 6070001,
            6080001, 7010005, 7020002, 7030002
        ];

        foreach (uint itemId in starters)
            Assert.Matches(new Regex($@"item={itemId}\b"), catalog);
    }

    [Fact]
    public void ShopSelectionResolvesClientSlotAndPurchasesTheCatalogItem()
    {
        string root = FindRepositoryRoot();
        string script = File.ReadAllText(Path.Combine(
            root,
            "Data",
            "scripts",
            "base",
            "chara",
            "npc",
            "populace",
            "shop",
            "PopulaceShopSalesman.lua"));

        Assert.Contains("require (\"shopgoods\")", script);
        Assert.Contains("itemChosen =  (itemRangeStart - 1) + buyResult", script);
        Assert.Contains("requestItem = shopGoods[itemChosen]", script);
        Assert.Contains("requestItem.price * quantity", script);
        Assert.DoesNotContain("TO-DO:  Request item information", script);
    }

    [Fact]
    public void JobCommandUsesValidatedProgressionInsteadOfHardCodedJob()
    {
        string root = FindRepositoryRoot();
        string script = File.ReadAllText(Path.Combine(root, "Data", "scripts", "commands", "ChangeJobCommand.lua"));

        Assert.Contains("player:TryChangeToCurrentClassJob()", script);
        Assert.DoesNotContain("SetCurrentJob(17)", script);
    }

    [Fact]
    public void ActiveJobHasARequiredCompatibilityMigration()
    {
        string root = FindRepositoryRoot();
        string migration = File.ReadAllText(Path.Combine(
            root,
            "db",
            "direct-core",
            "migrations",
            "20260723_000026_class_job_progression.sql"));
        string database = File.ReadAllText(Path.Combine(
            root,
            "src",
            "AetherXIV.Core.Map",
            "Database.cs"));

        Assert.Contains("ADD COLUMN IF NOT EXISTS `currentJob`", migration);
        Assert.Contains("SET currentJob = @currentJob", database);
        Assert.DoesNotContain("INSERT INTO character_login_state", database);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "Data"))
                && Directory.Exists(Path.Combine(directory.FullName, "src")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
