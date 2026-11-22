using System.Text.Json;
using global::RockTheVote;

namespace _unitTests.RockTheVote
{
    [TestClass]
    public class MapListSortingTests
    {
        [TestMethod]
        public void None_LeavesFileUnchangedWithoutStatistics()
        {
            WithFiles((mapFile, statsFile) =>
            {
                var original = "// comment\nz_map\n\na_map:123\n";
                File.WriteAllText(mapFile, original);

                MapListSorter.Sort(mapFile, statsFile, "None");

                Assert.AreEqual(original, File.ReadAllText(mapFile));
            });
        }

        [TestMethod]
        public void Name_SortsNamesIgnoringCaseAndKeepsCommentsAndIds()
        {
            WithFiles((mapFile, statsFile) =>
            {
                File.WriteAllLines(mapFile, ["z_map:123", "// comment", "Beta", "", "alpha:456"]);

                MapListSorter.Sort(mapFile, statsFile, "Name");

                CollectionAssert.AreEqual(new[] { "// comment", "alpha:456", "Beta", "z_map:123" }, File.ReadAllLines(mapFile));
            });
        }

        [TestMethod]
        public void Usage_SortsNeverPlayedThenOldestWithAlphabeticalTies()
        {
            WithFiles((mapFile, statsFile) =>
            {
                File.WriteAllLines(mapFile, ["new", "z_unplayed:12", "old", "// comment", "a_unplayed"]);
                File.WriteAllText(statsFile, """{"NEW":{"name":"new","lastPlayed":"2025-01-01T00:00:00"},"old":{"name":"old","lastPlayed":"2020-01-01T00:00:00"}}""");

                MapListSorter.Sort(mapFile, statsFile, "Usage");

                CollectionAssert.AreEqual(new[] { "// comment", "a_unplayed", "z_unplayed:12", "old", "new" }, File.ReadAllLines(mapFile));
            });
        }

        [TestMethod]
        public void Usage_MissingStatisticsDoesNotChangeMapList()
        {
            WithFiles((mapFile, statsFile) =>
            {
                File.WriteAllText(mapFile, "z\na\n");

                Assert.ThrowsExactly<FileNotFoundException>(() => MapListSorter.Sort(mapFile, statsFile, "Usage"));
                Assert.AreEqual("z\na\n", File.ReadAllText(mapFile));
            });
        }

        [TestMethod]
        [DataRow("not json")]
        [DataRow("""{"map":null}""")]
        public void Usage_InvalidStatisticsDoesNotChangeMapList(string statistics)
        {
            WithFiles((mapFile, statsFile) =>
            {
                File.WriteAllText(mapFile, "z\na\n");
                File.WriteAllText(statsFile, statistics);

                Assert.ThrowsExactly<JsonException>(() => MapListSorter.Sort(mapFile, statsFile, "Usage"));
                Assert.AreEqual("z\na\n", File.ReadAllText(mapFile));
            });
        }

        [TestMethod]
        public void InvalidSortOrderDoesNotChangeMapList()
        {
            WithFiles((mapFile, statsFile) =>
            {
                File.WriteAllText(mapFile, "z\na\n");

                Assert.ThrowsExactly<ArgumentException>(() => MapListSorter.Sort(mapFile, statsFile, "invalid"));
                Assert.AreEqual("z\na\n", File.ReadAllText(mapFile));
            });
        }

        [TestMethod]
        public void ExistingConfigDefaultsToNoSorting()
        {
            var config = JsonSerializer.Deserialize<RtvConfig>("""{"EnableRtv":true}""");

            Assert.AreEqual("None", config!.MapSortOrder);
        }

        private static void WithFiles(Action<string, string> action)
        {
            var directory = Path.Combine(Path.GetTempPath(), $"rtv-sort-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            try
            {
                action(Path.Combine(directory, "maplist.txt"), Path.Combine(directory, "mapStatistic.json"));
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
