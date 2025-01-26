using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json.Linq;

public class GitHubRawFileDownloader
{
    private const string GitHubApiUrl = "https://api.github.com/repos/{0}/{1}/contents/{2}";

    public static async Task<string> DownloadFile(string owner, string repo, string filePath, string accessToken)
    {
        string cachePath = Path.Combine(Application.persistentDataPath, Path.GetFileName(filePath));

        // ✅ Проверяем кэш перед загрузкой
        if (File.Exists(cachePath))
        {
            Debug.Log($"Using cached file: {cachePath}");
            return cachePath;
        }

        // 📡 Запрашиваем `download_url` через GitHub API
        string downloadUrl = await GetFileDownloadUrl(owner, repo, filePath, accessToken);
        if (string.IsNullOrEmpty(downloadUrl)) return null;

        // 🌍 Скачиваем файл напрямую
        bool success = await DownloadRawFile(downloadUrl, cachePath, accessToken);
        return success ? cachePath : null;
    }

    private static async Task<string> GetFileDownloadUrl(string owner, string repo, string filePath, string accessToken)
    {
        string url = string.Format(GitHubApiUrl, owner, repo, filePath);
        Debug.Log(url);

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.SetRequestHeader("Authorization", $"Bearer {accessToken}");
            request.SetRequestHeader("User-Agent", "UnityEditor-UGPM");

            var asyncOp = request.SendWebRequest();
            while (!asyncOp.isDone) await Task.Yield();

            if (request.result == UnityWebRequest.Result.Success)
            {
                JObject jsonResponse = JObject.Parse(request.downloadHandler.text);
                return jsonResponse["download_url"]?.ToString();
            }
            else
            {
                Debug.LogError($"Failed to fetch file info: {request.responseCode} {request.error}");
            }
        }
        return null;
    }

    public static async void DownloadRawFile(string owner, string repo, string filePath, string accessToken)
    {
        string url = string.Format(GitHubApiUrl, owner, repo, filePath);
        Debug.Log($"Requesting file from: {url}");

        using (var request = new UnityEngine.Networking.UnityWebRequest(url, UnityEngine.Networking.UnityWebRequest.kHttpVerbGET))
        {
            request.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
            request.SetRequestHeader("Authorization", "Bearer " + accessToken);
            request.SetRequestHeader("User-Agent", "UnityEditor-UGPM");

            await SendWebRequestAsync(request);

            string savePath = Application.persistentDataPath;

            if (request.result == UnityWebRequest.Result.Success)
            {
                File.WriteAllBytes(savePath, request.downloadHandler.data);
                Debug.Log($"File downloaded and saved: {savePath}");
            }
            else
            {
                Debug.LogError($"Failed to download file: {url} - {request.error}");
            }
        }
    }

    private static async Task SendWebRequestAsync(UnityWebRequest request)
    {
        var completionSource = new TaskCompletionSource<bool>();
        var operation = request.SendWebRequest();

        operation.completed += _ => completionSource.SetResult(true);
        await completionSource.Task;
    }

    private static async Task<bool> DownloadRawFile(string url, string savePath, string accessToken)
    {
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.SetRequestHeader("Authorization", $"Bearer {accessToken}");

            var asyncOp = request.SendWebRequest();
            while (!asyncOp.isDone) await Task.Yield();

            if (request.result == UnityWebRequest.Result.Success)
            {
                File.WriteAllBytes(savePath, request.downloadHandler.data);
                Debug.Log($"File downloaded and saved: {savePath}");
                return true;
            }
            else
            {
                Debug.LogError($"Failed to download file: {url} - {request.error}");
                return false;
            }
        }
    }
}