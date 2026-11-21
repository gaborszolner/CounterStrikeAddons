using System.Resources;
using SharedLibrary;

namespace RockTheVote
{
    internal static class Msg
    {
        private static readonly ResourceManager _rm =
            new ResourceManager("RockTheVote.Resources.Messages", typeof(Msg).Assembly);

        public static string Get(string key, params object[] args)
            => Localizer.Get(_rm, key, args);
    }
}
