using System.Reflection;
using System.Runtime.CompilerServices;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API;
using global::RockTheVote;

namespace _unitTests.RockTheVote
{
    [TestClass]
    public class MapMenuPaginationTests
    {
        private class TestMenuInstance(BasePlugin plugin, CCSPlayerController player, CenterHtmlMenu menu)
            : MapMenuInstance(plugin, player, menu)
        {
            public bool CanGoNext => HasNextButton;
            public bool CanGoBack => HasPrevButton;
            public int ItemsOnPage => Math.Min(MenuItemsPerPage, Menu.MenuOptions.Count - CurrentOffset);
            public override void Display() { }
        }

        private static TestMenuInstance CreateInstance(int mapCount)
        {
            var menu = new CenterHtmlMenu("Maps", null!);
            for (int i = 0; i < mapCount; i++)
                menu.AddMenuOption($"map{i}", (_, _) => { });

            // The real constructor registers a native server listener, unavailable in unit tests.
            var instance = (TestMenuInstance)RuntimeHelpers.GetUninitializedObject(typeof(TestMenuInstance));
            typeof(BaseMenuInstance).GetField("<Menu>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(instance, menu);
            typeof(BaseMenuInstance).GetField("<PrevPageOffsets>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(instance, new Stack<int>());
            return instance;
        }

        [TestMethod]
        public void Paging_ShowsSixMapsAndPreservesNavigation()
        {
            var instance = CreateInstance(13);

            Assert.AreEqual(6, instance.ItemsOnPage);
            Assert.IsFalse(instance.CanGoBack);
            Assert.IsTrue(instance.CanGoNext);

            instance.NextPage();
            Assert.AreEqual(6, instance.CurrentOffset);
            Assert.AreEqual(6, instance.ItemsOnPage);
            Assert.IsTrue(instance.CanGoBack);
            Assert.IsTrue(instance.CanGoNext);

            instance.NextPage();
            Assert.AreEqual(12, instance.CurrentOffset);
            Assert.AreEqual(1, instance.ItemsOnPage);
            Assert.IsFalse(instance.CanGoNext);

            instance.PrevPage();
            Assert.AreEqual(6, instance.CurrentOffset);
        }

        [TestMethod]
        public void Paging_ExactlySixMapsDoesNotOfferNextPage()
        {
            var instance = CreateInstance(6);
            Assert.AreEqual(6, instance.ItemsOnPage);
            Assert.IsFalse(instance.CanGoNext);
        }
    }
}
