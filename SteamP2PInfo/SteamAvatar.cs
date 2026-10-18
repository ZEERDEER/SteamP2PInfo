using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Steamworks;

namespace SteamP2PInfo
{
    /// <summary>Loads and caches Steam avatars. Returns null when unavailable (UI falls back to an initial circle).</summary>
    static class SteamAvatar
    {
        private static readonly Dictionary<ulong, ImageSource> cache = new Dictionary<ulong, ImageSource>();

        public static ImageSource Get(CSteamID steamId)
        {
            if (steamId.m_SteamID == 0)
                return null;

            if (cache.TryGetValue(steamId.m_SteamID, out ImageSource cached))
                return cached;

            ImageSource source = null;
            try
            {
                int handle = SteamFriends.GetMediumFriendAvatar(steamId);
                if (handle > 0 && SteamUtils.GetImageSize(handle, out uint width, out uint height) && width > 0 && height > 0)
                {
                    byte[] data = new byte[width * height * 4];
                    if (SteamUtils.GetImageRGBA(handle, data, data.Length))
                    {
                        var bitmap = BitmapSource.Create((int)width, (int)height, 96, 96, PixelFormats.Bgra32, null, data, (int)width * 4);
                        bitmap.Freeze();
                        source = bitmap;
                    }
                }
            }
            catch
            {
                source = null;
            }

            cache[steamId.m_SteamID] = source;
            return source;
        }
    }
}
