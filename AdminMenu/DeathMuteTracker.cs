namespace AdminMenu;

internal sealed record DeathMuteToken(Guid Id, bool WasMutedBeforeDeath);

internal sealed class DeathMuteTracker
{
    private readonly Dictionary<ulong, DeathMuteToken> _activeMutes = [];

    public DeathMuteToken Begin(ulong steamId, bool wasMutedBeforeDeath)
    {
        if (_activeMutes.TryGetValue(steamId, out var existingMute))
            wasMutedBeforeDeath = existingMute.WasMutedBeforeDeath;

        var token = new DeathMuteToken(Guid.NewGuid(), wasMutedBeforeDeath);
        _activeMutes[steamId] = token;
        return token;
    }

    public bool TryComplete(ulong steamId, DeathMuteToken token)
    {
        if (!_activeMutes.TryGetValue(steamId, out var currentMute) || currentMute.Id != token.Id)
            return false;

        _activeMutes.Remove(steamId);
        return true;
    }
}
