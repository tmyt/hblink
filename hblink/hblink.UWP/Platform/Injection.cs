using Windows.Storage;

namespace hblink.Platform
{
    public static class Injection
    {
        public static string GetPicturesPath()
        {
            var library = KnownFolders.PicturesLibrary;
            return library.Path;
        }
    }
}