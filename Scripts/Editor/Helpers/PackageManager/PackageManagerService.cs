using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace SplashGames.Internal.UGPM
{
    public class PackageManagerService
    {
        private readonly PackageChecker _checker;
        private AddAndRemoveRequest _addRequest;
        private bool _isProcessing = false;

        public PackageManagerService()
        {
            _checker = new PackageChecker();
        }

        public async Task Init()
        {
            await _checker.Init();
        }

        public void UpdatePackage(string packageName, string packageUrl)
        {
            if (HasPackage(packageName) == false)
            {
                ImportGitPackage(packageUrl);
                return;
            }

            List<string> removeList = new List<string> { packageName };
            List<string> addList = new List<string> { packageUrl };

            AddAndRemoveRequest request = Client.AddAndRemove(addList.ToArray(), removeList.ToArray());

            EditorApplication.update += () =>
            {
                if (request.IsCompleted)
                {
                    if (request.Status == StatusCode.Success)
                    {
                        Debug.Log($"Package updated successfully");
                    }
                    else
                    {
                        Debug.LogError($"Failed to update package: {request.Error.message}");
                        EditorApplication.update -= () => { };
                    }
                    EditorApplication.update -= () => { };
                }
            };
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
                        Debug.Log("Package Remove successfully: " + removeRequest.PackageIdOrName);
                    }
                    else
                    {
                        Debug.Log("Package Remove failed: " + removeRequest.PackageIdOrName);
                        EditorApplication.update -= () => { };
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

        public async void InstallDependencies(string[] dependencies)
        {
            if (_isProcessing)
            {
                Debug.LogWarning("⚠Installation is already in progress.");
                return;
            }

            _isProcessing = true;
            Debug.Log("Installing multiple dependencies at once...");

            _addRequest = Client.AddAndRemove(dependencies, new string[0]);

            while (!_addRequest.IsCompleted)
            {
                await Task.Yield();
            }

            if (_addRequest.Status == StatusCode.Success)
            {
                Debug.Log($"Successfully installed {dependencies.Length} packages.");
            }
            else
            {
                Debug.LogError($"Failed to install packages: {_addRequest.Error.message}");
                _isProcessing = false;
                return;
            }

            Client.Resolve();

            Debug.Log("Triggering final compilation...");
            AssetDatabase.Refresh();
            CompilationPipeline.RequestScriptCompilation();
            _isProcessing = false;
        }

        internal bool HasPackage(string packageBundle, string version = null)
        {
            return _checker.IsPackageExist(packageBundle, version);
        }
    }
}
