using SharedLibrary;

namespace _unitTests
{
    [TestClass]
    public class StatisticRetentionTests
    {
        private string _root = null!;
        private string _statsDirectory = null!;
        private DateTime _currentMonth;

        [TestInitialize]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), $"StatisticRetentionTests-{Guid.NewGuid():N}");
            _statsDirectory = Path.Combine(_root, "statistics");
            Directory.CreateDirectory(_statsDirectory);
            var now = DateTime.Now;
            _currentMonth = new DateTime(now.Year, now.Month, 1);
        }

        [TestCleanup]
        public void CleanUp() => Directory.Delete(_root, recursive: true);

        private string WriteMonthlyFile(string directory, int offset)
        {
            string path = Path.Combine(directory, StatisticHelper.GetPlayerStatFileName(_currentMonth.AddMonths(offset)));
            File.WriteAllText(path, "{}");
            return path;
        }

        [TestMethod]
        public void DeleteOldFiles_DeletesOnlyExpiredMonthlyFilesInStatisticsDirectory()
        {
            string expired = WriteMonthlyFile(_statsDirectory, -3);
            string oldestRetained = WriteMonthlyFile(_statsDirectory, -2);
            string current = WriteMonthlyFile(_statsDirectory, 0);
            string future = WriteMonthlyFile(_statsDirectory, 1);
            string otherDirectoryFile = WriteMonthlyFile(_root, -3);
            string invalidName = Path.Combine(_statsDirectory, "playerStatistic202613.json");
            string backup = Path.Combine(_statsDirectory, "playerStatistic202001.backup.json");
            File.WriteAllText(invalidName, "{}");
            File.WriteAllText(backup, "{}");

            StatisticHelper.DeleteOldFiles(_statsDirectory, 3);

            Assert.IsFalse(File.Exists(expired));
            Assert.IsTrue(File.Exists(oldestRetained));
            Assert.IsTrue(File.Exists(current));
            Assert.IsTrue(File.Exists(future));
            Assert.IsTrue(File.Exists(otherDirectoryFile));
            Assert.IsTrue(File.Exists(invalidName));
            Assert.IsTrue(File.Exists(backup));
        }

        [TestMethod]
        public void LoadMonthsStats_DoesNotDeleteOlderFiles()
        {
            string expired = WriteMonthlyFile(_statsDirectory, -3);

            StatisticHelper.LoadMonthsStats(_statsDirectory, 1);

            Assert.IsTrue(File.Exists(expired));
        }

        [TestMethod]
        public void LoadMonthsStats_DoesNotRecreateMissingHistoricalFiles()
        {
            string missingMonth = Path.Combine(_statsDirectory, StatisticHelper.GetPlayerStatFileName(_currentMonth.AddMonths(-3)));

            StatisticHelper.LoadMonthsStats(_statsDirectory, 4);

            Assert.IsFalse(File.Exists(missingMonth));
        }

        [TestMethod]
        public void LoadMonthsStats_AggregatesExistingMonthlyFiles()
        {
            string current = WriteMonthlyFile(_statsDirectory, 0);
            string previous = WriteMonthlyFile(_statsDirectory, -1);
            File.WriteAllText(current, """{"player":{"identity":"player","name":"Alice","kill":4}}""");
            File.WriteAllText(previous, """{"player":{"identity":"player","name":"Alice","kill":3}}""");

            var stats = StatisticHelper.LoadMonthsStats(_statsDirectory, 3);

            Assert.AreEqual(7, stats["player"].Kill);
        }

        [TestMethod]
        public void LoadDataFromFile_StillCreatesMissingFilesByDefault()
        {
            string path = Path.Combine(_root, "config.json");

            var result = Utils.LoadDataFromFile<string>(path);

            Assert.IsNull(result);
            Assert.AreEqual("{}", File.ReadAllText(path));
        }

        [TestMethod]
        [DataRow(0)]
        [DataRow(-1)]
        public void DeleteOldFiles_RejectsInvalidRetentionWithoutDeleting(int months)
        {
            string expired = WriteMonthlyFile(_statsDirectory, -3);

            Assert.Throws<ArgumentOutOfRangeException>(() => StatisticHelper.DeleteOldFiles(_statsDirectory, months));

            Assert.IsTrue(File.Exists(expired));
        }

        [TestMethod]
        public void DeleteOldFiles_ReportsMissingDirectory()
        {
            Assert.Throws<DirectoryNotFoundException>(() =>
                StatisticHelper.DeleteOldFiles(Path.Combine(_root, "missing"), 3));
        }
    }
}
