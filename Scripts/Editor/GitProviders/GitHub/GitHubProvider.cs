using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace Solvex.Internal.UGPM
{
    public class GitHubProvider
    {
        private const string GitHubApiUrlUser = "https://api.github.com/user";
        private const string GitHubApiUrlUserOrgs = "https://api.github.com/user/orgs";

        public string Username { get; private set; }
        public string AccessToken { get; private set; }
        public IReadOnlyList<string> Sources => _sources;

        private List<string> _sources = new List<string>();

        public event Action OnAuthComplete;

        public async void Authenticate()
        {
            try
            {
                string protocol = "https";
                string host = "github.com";

                var credentials = CredentialHelper.GetCredentials(protocol, host);
                Username = credentials.username;
                AccessToken = credentials.password;

                await FetchUserAndOrganizations();
                OnAuthComplete?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error retrieving credentials: {ex.Message}");
            }
        }

        private async Task FetchUserAndOrganizations()
        {
            await FetchUserInfo();
            await FetchUserOrganizations();
        }

        private async Task FetchUserInfo()
        {
            try
            {
                using (var request = new UnityWebRequest(GitHubApiUrlUser, UnityWebRequest.kHttpVerbGET))
                {
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Authorization", $"Bearer {AccessToken}");
                    request.SetRequestHeader("User-Agent", "UnityEditor-UGPM");

                    await SendWebRequestAsync(request);

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        var userInfo = JObject.Parse(request.downloadHandler.text);
                        var username = userInfo["login"]?.ToString();
                        if (!string.IsNullOrEmpty(username))
                        {
                            _sources.Add(username);
                            Username = username;
                        }
                    }
                    else
                    {
                        Debug.LogError($"Error fetching user info: {request.error}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Exception in FetchUserInfo: {ex.Message}");
            }
        }

        private async Task FetchUserOrganizations()
        {
            try
            {
                using (var request = new UnityWebRequest(GitHubApiUrlUserOrgs, UnityWebRequest.kHttpVerbGET))
                {
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Authorization", $"Bearer {AccessToken}");
                    request.SetRequestHeader("User-Agent", "UnityEditor-UGPM");

                    await SendWebRequestAsync(request);

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        var orgArray = JArray.Parse(request.downloadHandler.text);
                        foreach (var org in orgArray)
                        {
                            var orgName = org["login"]?.ToString();
                            if (!string.IsNullOrEmpty(orgName))
                            {
                                _sources.Add(orgName);
                            }
                        }
                    }
                    else
                    {
                        Debug.LogError($"Error fetching organizations: {request.error}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Exception in FetchUserOrganizations: {ex.Message}");
            }
        }

        private async Task SendWebRequestAsync(UnityWebRequest request)
        {
            var completionSource = new TaskCompletionSource<bool>();
            var operation = request.SendWebRequest();

            operation.completed += _ => completionSource.SetResult(true);
            await completionSource.Task;
        }
    }
}