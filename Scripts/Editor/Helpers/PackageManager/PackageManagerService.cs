using System;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace SplashGames.Internal.UGPM
{
    public class PackageManagerService
    {
        private readonly PackageChecker _checker;

        public PackageManagerService()
        {
            _checker = new PackageChecker();
        }

        public async Task Init()
        {
            await _checker.Init();
        }

        public void RemovePackage(string packageName, Action onSeccessCallback = null)
        {
            if (string.IsNullOrEmpty(packageName))
                return;

            Debug.Log($"Removing package: {packageName}...");

            RemoveRequest removeRequest = Client.Remove(packageName);
            EditorApplication.update += () =>
            {
                if (removeRequest.IsCompleted)
                {
                    if (removeRequest.Status == StatusCode.Success)
                    {
                        onSeccessCallback?.Invoke();
                        ReopenUGPMWindow();
                        Debug.Log("Package Remove successfully: " + removeRequest.PackageIdOrName);
                    }
                    else
                    {
                        Debug.Log("Package Remove failed: " + removeRequest.PackageIdOrName);
                    }
                    EditorApplication.update -= () => { };
                }
            };
        }

        public void ImportGitPackage(string gitPackageUrl, Action onSeccessCallback = null)
        {
            var addRequest = Client.Add(gitPackageUrl);
            EditorApplication.update += () =>
            {
                if (addRequest.IsCompleted)
                {
                    if (addRequest.Status == StatusCode.Success)
                    {
                        onSeccessCallback?.Invoke();
                        ReopenUGPMWindow();
                        Debug.Log("Package imported successfully: " + addRequest.Result.name);
                    }
                    else
                    {
                        Debug.LogError("Package import failed: " + addRequest.Error.message);
                        EditorApplication.update -= () => { };
                    }
                    EditorApplication.update -= () => { };
                }
            };
        }

        public async void UpdatePackage(string packageName, string gitPackageUrl)
        {
            RemoveRequest removeRequest = Client.Remove(packageName);
            while (!removeRequest.IsCompleted)
                await Task.Yield();

            if (removeRequest.Status == StatusCode.Success)
            {
                ImportGitPackage(gitPackageUrl);
            }
            else
            {
                Debug.LogError($"❌ Failed to remove package: {removeRequest.Error.message}");
            }
        }

        internal bool HasPackage(string packageBundle)
        {
            return _checker.IsPackageExist(packageBundle);
        }

        private static void ReopenUGPMWindow()
        {
            EditorApplication.delayCall += () =>
            {
                UGPMMainWindow.ShowWindow();
            };
        }
    }
}
