using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Solvex.Internal.UGPM
{
    public class IconCacheManager
    {
        private static string CacheDirectory => Path.Combine(GetUnityPackageCachePath(), "package.splashgames.ugpm");
        private static readonly string PlaceholderIconPath = "Packages/com.splashgames.ugpm/Resources/placeholderIcon.png";
        private static readonly string AlternativePlaceholderPath = "Assets/com.splashgames.ugpm/Resources/placeholderIcon.png";

        public static async Task<Texture2D> GetIcon(string repoName, string iconUrl, string accessToken)
        {
            if (string.IsNullOrEmpty(iconUrl))
                return GetPlaceholderIcon();

            string filePath = Path.Combine(CacheDirectory, repoName + ".png");

            if (File.Exists(filePath))
                return LoadTextureFromFile(filePath);

            Texture2D downloadedIcon = await DownloadAndSaveIcon(iconUrl, filePath, accessToken);
            return downloadedIcon ?? GetPlaceholderIcon();
        }

        private static async Task<Texture2D> DownloadAndSaveIcon(string url, string filePath, string accessToken)
        {
            if (!Directory.Exists(CacheDirectory))
                Directory.CreateDirectory(CacheDirectory);

            using (var request = UnityWebRequestTexture.GetTexture(url))
            {
                request.SetRequestHeader("Authorization", "Bearer " + accessToken);
                request.SetRequestHeader("User-Agent", "UnityEditor-UGPM");
                var asyncOp = request.SendWebRequest();
                while (!asyncOp.isDone) await Task.Yield();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    Texture2D texture = DownloadHandlerTexture.GetContent(request);
                    SaveTextureToFile(texture, filePath);
                    return texture;
                }
                else
                {
                    return null;
                }
            }
        }

        private static void SaveTextureToFile(Texture2D texture, string filePath)
        {
            byte[] pngData = texture.EncodeToPNG();
            File.WriteAllBytes(filePath, pngData);
        }

        private static Texture2D LoadTextureFromFile(string filePath)
        {
            byte[] fileData = File.ReadAllBytes(filePath);
            Texture2D texture = new Texture2D(2, 2);
            texture.LoadImage(fileData);
            return texture;
        }

        private static Texture2D GetPlaceholderIcon()
        {
            if (File.Exists(PlaceholderIconPath))
            {
                byte[] fileData = File.ReadAllBytes(PlaceholderIconPath);
                Texture2D texture = new Texture2D(128, 128);
                texture.LoadImage(fileData);
                return texture;
            }
            else if (File.Exists(AlternativePlaceholderPath))
            {
                byte[] fileData = File.ReadAllBytes(AlternativePlaceholderPath);
                Texture2D texture = new Texture2D(128, 128);
                texture.LoadImage(fileData);
                return texture;
            }

            Texture2D placeholder = new Texture2D(128, 128);
            Color fillColor = Color.gray;
            Color[] pixels = placeholder.GetPixels();
            for (int i = 0; i < pixels.Length; i++) pixels[i] = fillColor;
            placeholder.SetPixels(pixels);
            placeholder.Apply();
            return placeholder;
        }


        private static string GetUnityPackageCachePath()
        {
            // Определяем путь к Unity Package Manager Cache
#if UNITY_EDITOR_WIN
        return Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "Unity", "cache", "packages");
#elif UNITY_EDITOR_OSX
            return Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Library", "Unity", "cache", "packages");
#elif UNITY_EDITOR_LINUX
        return Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal), ".cache", "unity", "packages");
#else
        return Application.persistentDataPath;
#endif
        }
    }
}
