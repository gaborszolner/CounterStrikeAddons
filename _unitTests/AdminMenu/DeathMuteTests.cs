using CounterStrikeSharp.API.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;

namespace _unitTests.AdminMenu;

[TestClass]
public class DeathMuteTests
{
    [TestMethod]
    public void RestoreMuteFlag_PreservesOtherVoiceFlags()
    {
        var restoreMethod = typeof(global::AdminMenu.AdminMenu).GetMethod("RestoreMuteFlag", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(restoreMethod);

        var voiceFlagsType = typeof(CCSPlayerController).GetProperty("VoiceFlags")!.PropertyType;
        var mutedFlag = Enum.Parse(voiceFlagsType, "Muted");
        var otherFlag = Enum.ToObject(voiceFlagsType, 1 << 30);
        var anotherFlag = Enum.ToObject(voiceFlagsType, 1 << 29);
        var currentFlags = Enum.ToObject(voiceFlagsType, (1 << 30) | (1 << 29));
        var result = restoreMethod.Invoke(null, [currentFlags, true])!;

        var expectedFlags = Enum.ToObject(voiceFlagsType, Convert.ToInt32(mutedFlag) | Convert.ToInt32(otherFlag) | Convert.ToInt32(anotherFlag));
        Assert.AreEqual(Convert.ToInt64(expectedFlags), Convert.ToInt64(result));

        var currentlyMuted = Enum.ToObject(voiceFlagsType, Convert.ToInt32(currentFlags) | Convert.ToInt32(mutedFlag));
        var unmutedResult = restoreMethod.Invoke(null, [currentlyMuted, false])!;
        Assert.AreEqual(Convert.ToInt64(currentFlags), Convert.ToInt64(unmutedResult));
    }

    [TestMethod]
    public void TryComplete_IgnoresOlderDeathMuteTimer()
    {
        var tracker = new global::AdminMenu.DeathMuteTracker();
        const ulong steamId = 123UL;

        var firstToken = tracker.Begin(steamId, false);
        var secondToken = tracker.Begin(steamId, true);

        Assert.IsFalse(secondToken.WasMutedBeforeDeath);
        Assert.IsFalse(tracker.TryComplete(steamId, firstToken));
        Assert.IsTrue(tracker.TryComplete(steamId, secondToken));
        Assert.IsFalse(tracker.TryComplete(steamId, secondToken));
    }
}
