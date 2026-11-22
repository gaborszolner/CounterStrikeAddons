global using Msg = SharedLibrary.Msg<SharedLibrary.Config>;

using System.Resources;

namespace SharedLibrary
{
    public static class Msg<T>
    {
        private static readonly ResourceManager _rm =
            new ResourceManager($"{typeof(T).Assembly.GetName().Name}.Resources.Messages", typeof(T).Assembly);

        public static string Get(string key, params object[] args)
            => Localizer.Get(_rm, key, args);
    }
}
