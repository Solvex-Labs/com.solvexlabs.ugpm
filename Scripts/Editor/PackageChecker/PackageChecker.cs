using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;

namespace SplashGames.Internal.UGPM
{
    public class PackageChecker
    {
        private readonly Dictionary<string, PackageInfo> _cachedPackages;

        public PackageChecker()
        {
            _cachedPackages = new Dictionary<string, PackageInfo>();
        }

        public async Task Init()
        {
            _cachedPackages.Clear();

            ListRequest request = Client.List(true);
            while (!request.IsCompleted)
            {
                await Task.Delay(10);
            }

            foreach (PackageInfo package in request.Result)
                Cache(package.packageId, package);
        }

        public PackageInfo GetPackage(string packageBundle)
        {
            if (_cachedPackages.TryGetValue(packageBundle, out PackageInfo package))
                return package;

            return null;
        }

        public bool IsPackageExist(string packageBundle)
        {
            foreach (var pair in _cachedPackages)
            {
                bool isExist = pair.Value.packageId.Contains(packageBundle);

                if (isExist)
                {
                    Cache(packageBundle, pair.Value);
                    return true;
                }
            }

            return false;
        }

        private void Cache(string bundleId, PackageInfo package)
        {
            if (_cachedPackages.ContainsKey(bundleId))
                return;

            _cachedPackages.Add(bundleId, package);
        }
    }
}
