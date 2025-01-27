using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace SplashGames.Internal.UGPM
{
    public class UGPMMainWindow : EditorWindow
    {
        private GitHubProvider _gitHubProvider;
        private PackageManagerService _packageManagerService;

        private string _selectedSource;
        private List<RepositoryInfo> _repositories = new List<RepositoryInfo>();
        private Vector2 _repoScroll, _detailsScroll;
        private RepositoryInfo _selectedRepo;

        private int _totalRepositories = 0;  // Общее количество репозиториев
        private int _loadedRepositories = 0; // Количество загруженных репозиториев
        private bool _isLoading = false;     // Флаг загрузки

        private const int MinWindowSize = 1200;
        private const int CardWidth = 220;  // Фиксированная ширина карточки
        private const int MinColumsAmount = 3;      // Количество колонок
        private const int Padding = 35;     // Внутренний отступ

        private bool _isHideInvalid;

        [MenuItem("Tools/Unity Git Package Manager")]
        public static void ShowWindow()
        {
            var window = GetWindow<UGPMMainWindow>("Unity Git Package Manager");

            window.minSize = new Vector2(MinWindowSize, 600);
            window.Show();
            window.Initialize();
        }

        private async void Initialize()
        {
            _gitHubProvider = new GitHubProvider();
            _packageManagerService = new PackageManagerService();

            await _packageManagerService.Init();

            _gitHubProvider.OnAuthComplete += OnAuthComplete;
            _gitHubProvider.Authenticate();
        }

        private void OnAuthComplete()
        {
            if (_gitHubProvider.Sources.Count > 0)
            {
                _selectedSource = _gitHubProvider.Sources[0];
                _selectedRepo = null;
                FetchRepositories();
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();

            bool canPreviewRepo = _selectedRepo != null && _selectedRepo.GetVersionInfo() != null;
            float leftPanelWidth = canPreviewRepo ? CardWidth * MinColumsAmount + Padding : MinWindowSize; // Учитываем отступы
            int columnAmount = (int)(leftPanelWidth / CardWidth);
            // 🎯 Левая панель (Источники + Список репозиториев)
            EditorGUILayout.BeginVertical(GUILayout.Width(leftPanelWidth));

            if (_gitHubProvider.Sources.Count > 0 && _isLoading == false)
            {
                GUILayout.Label("Sources:", EditorStyles.boldLabel);

                // Выбор источника
                int selectedIndex = new List<string>(_gitHubProvider.Sources).IndexOf(_selectedSource);
                selectedIndex = EditorGUILayout.Popup(selectedIndex, _gitHubProvider.Sources.ToList().ToArray(), GUILayout.Width(280));

                if (selectedIndex >= 0 && _gitHubProvider.Sources[selectedIndex] != _selectedSource)
                {
                    _selectedSource = _gitHubProvider.Sources[selectedIndex];
                    _selectedRepo = null;
                    FetchRepositories();
                }

                if (GUILayout.Button(_isHideInvalid ? "S" : "H", GUILayout.Width(20)))
                {
                    _isHideInvalid = !_isHideInvalid;
                }
            }
            else
            {
                GUILayout.Label("Fetch sources...", EditorStyles.boldLabel);
            }

            EditorGUILayout.Space();

            // 🖼️ Индикатор загрузки
            if (_isLoading && _totalRepositories > 0)
            {
                float progress = _loadedRepositories / (float)_totalRepositories;
                string title = $"Fetched {_loadedRepositories}/{_totalRepositories}";
                EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(false, 20), progress, title);
            }

            // 🖼️ Область со списком репозиториев
            _repoScroll = EditorGUILayout.BeginScrollView(_repoScroll, GUILayout.Width(leftPanelWidth), GUILayout.Height(position.height - 50));

            if (!_isLoading)
            {
                DrawRepositoriesGrid(columnAmount, CardWidth);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical(); // Закрытие левой панели

            if (canPreviewRepo)
            {
                EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
                DrawRepositoryDetails();
                EditorGUILayout.EndVertical();
            }
            
            EditorGUILayout.EndHorizontal();
        }

        private enum RepositoryDetailTab
        {
            None,
            Description,
            VersionHistory,
            Dependencies
        }

        private RepositoryDetailTab _selectedTab = RepositoryDetailTab.None;

        private void DrawRepositoryDetails()
        {
            if (_selectedRepo == null)
            {
                GUILayout.Label("No repository selected.", EditorStyles.helpBox);
                return;
            }

            VersionInfo info = _selectedRepo.GetVersionInfo();

            if (info == null)
            {
                GUILayout.Label("No released versions.", EditorStyles.helpBox);
                return;
            }

            EditorGUILayout.BeginVertical("box", GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            // 🔹 Верхняя секция с иконкой и названием
            EditorGUILayout.BeginHorizontal();

            if (_selectedRepo.Icon != null)
                GUILayout.Label(_selectedRepo.Icon, GUILayout.Width(64), GUILayout.Height(64));

            EditorGUILayout.BeginVertical();
            GUIStyle titleStyle = new GUIStyle
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white },
                alignment = TextAnchor.MiddleLeft
            };
            GUILayout.Label(_selectedRepo.GetDisplayName(), titleStyle);

            if (info != null)
            {
                GUILayout.Label($"Version: {info.package.version}", EditorStyles.miniLabel);
            }
            
            GUILayout.Label($"Package ID: {_selectedRepo.CloneUrl}", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // 🔹 Верхние кнопки (Documentation, Changelog, Licenses)
            EditorGUILayout.BeginHorizontal();
            DrawLinkButton("Documentation", info.package.documentationUrl);
            DrawLinkButton("Changelog", _selectedRepo.ChangelogUrl);
            DrawLinkButton("License", info.package.licensesUrl);
            EditorGUILayout.EndHorizontal();

            // 🔹 Горизонтальная линия
            GUILayout.Box("", GUILayout.ExpandWidth(true), GUILayout.Height(1));

            EditorGUILayout.Space();

            // 🔹 Кнопки переключения контента
            EditorGUILayout.BeginHorizontal();
            DrawTabButton("Description", RepositoryDetailTab.Description, true);
            DrawTabButton("Version History", RepositoryDetailTab.VersionHistory, _selectedRepo.HasUnityPackage);
            DrawTabButton("Dependencies", RepositoryDetailTab.Dependencies, _selectedRepo.HasUnityPackage);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // 🔹 Область контента
            DrawSelectedTabContent();

            EditorGUILayout.Space();

            // 🔹 Нижние кнопки (Import/Remove)
            if (_selectedRepo.HasUnityPackage)
            {
                EditorGUILayout.BeginHorizontal();

                if (_selectedRepo.IsExist)
                {
                    if (GUILayout.Button("Remove", GUILayout.Height(30)))
                    {
                        //_packageManagerService.RemovePackage(_selectedRepo.GetVersionInfo().package.name, Close);
                    }
                }
                else
                {
                    if (GUILayout.Button("Import", GUILayout.Height(30)))
                    {
                        //_packageManagerService.ImportGitPackage(_selectedRepo.PackageInfo?.GitPackageUrl, Close);
                    }
                }
                
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        // Рендеринг ссылок (если нет ссылки, кнопка неактивна)
        private void DrawLinkButton(string label, string url)
        {
            GUI.enabled = !string.IsNullOrEmpty(url);
            if (GUILayout.Button(label, GUILayout.Height(25)))
            {
                Application.OpenURL(url);
            }
            GUI.enabled = true;
        }

        // Рендеринг кнопок переключения контента
        private void DrawTabButton(string label, RepositoryDetailTab tab, bool isEnable)
        {
            GUI.enabled = isEnable;
            if (GUILayout.Button(label, GUILayout.Height(25)))
            {
                _selectedTab = tab;
            }
            GUI.enabled = true;
        }

        // Рендеринг контента в зависимости от активной вкладки
        private void DrawSelectedTabContent()
        {
            switch (_selectedTab)
            {
                case RepositoryDetailTab.Description:
                    GUILayout.Label("Description:", EditorStyles.boldLabel);
                    GUILayout.Label(_selectedRepo.GetVersionInfo().package.description ?? "No description available", EditorStyles.wordWrappedLabel);
                    break;

                case RepositoryDetailTab.VersionHistory:
                    GUILayout.Label("Version History:", EditorStyles.boldLabel);

                    if (_selectedRepo.Versions == null || (_selectedRepo.Versions != null && _selectedRepo.Versions.Count == 0))
                    {
                        GUILayout.Label("No version history available.", EditorStyles.wordWrappedLabel); // Можно заменить на данные из GitPackageInfo
                        break;
                    }

                    DrawVersionHistory(_selectedRepo.Versions);

                    break;

                case RepositoryDetailTab.Dependencies:
                    GUILayout.Label("Dependencies:", EditorStyles.boldLabel);
                    GUILayout.Label("No dependencies found.", EditorStyles.wordWrappedLabel);
                    /*if (_selectedRepo.PackageInfo?.GitDependencies != null && _selectedRepo.PackageInfo.GitDependencies.Length > 0)
                    {
                        foreach (var dependency in _selectedRepo.PackageInfo.GitDependencies)
                        {
                            GUILayout.Label(dependency);
                        }
                    }
                    else
                    {
                        GUILayout.Label("No dependencies found.", EditorStyles.wordWrappedLabel);
                    }*/
                    break;

                default:
                    GUILayout.Label("Select a tab to view details.", EditorStyles.helpBox);
                    break;
            }
        }

        private Vector2 _versionHistoryScroll; // Переменная для хранения позиции скролла

        private void DrawVersionHistory(IReadOnlyList<VersionInfo> versions)
        {
            if (versions == null || versions.Count == 0)
            {
                GUILayout.Label("No version history available.", EditorStyles.helpBox);
                return;
            }

            // Обертываем в ScrollView
            _versionHistoryScroll = EditorGUILayout.BeginScrollView(_versionHistoryScroll);

            foreach (var version in versions)
            {
                if (version.IsDraft)
                    continue;

                DrawVersionCard(version);
            }

            EditorGUILayout.EndScrollView(); // Закрываем ScrollView
        }

        private void DrawVersionCard(VersionInfo info)
        {
            EditorGUILayout.BeginVertical("box");

            // Кнопка переключения состояния (свернуть/развернуть)
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(info.IsExpanded ? "▼" : "▶", GUILayout.Width(20)))
            {
                info.IsExpanded = !info.IsExpanded;
            }

            string version = info.package.version;
            // Версия
            GUILayout.Label(version, EditorStyles.boldLabel);

            // Определяем тип версии
            if (info.IsLatest)
            {
                DrawVersionType("Recommended", Color.green);
            }
            if (info.IsPrerelease)
            {
                DrawVersionType("Pre-release", Color.yellow);
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label(info.ReleaseDate, EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            // Если карточка развернута, рисуем дополнительную информацию
            if (info.IsExpanded)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Changelog:", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(info.Changelog, EditorStyles.wordWrappedLabel);

                if (!string.IsNullOrEmpty(info.ChangelogUrl))
                {
                    if (GUILayout.Button("See full changelog", EditorStyles.linkLabel))
                    {
                        Application.OpenURL(info.ChangelogUrl);
                    }
                }

                EditorGUILayout.Space();
                EditorGUILayout.BeginHorizontal();

                if (info.IsInstalled)
                {
                    if (GUILayout.Button("Remove", GUILayout.Width(80)))
                    {
                        Debug.Log($"Removing {version}");
                    }
                }
                else
                {
                    if (GUILayout.Button("Update", GUILayout.Width(80)))
                    {
                        Debug.Log($"Updating to {version}");
                    }
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawVersionType(string label, Color color)
        {
            GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel);
            labelStyle.normal.textColor = color;
            GUILayout.Label(label, labelStyle);
        }

        private async void FetchRepositories()
        {
            _repositories.Clear();
            _isLoading = true;
            _loadedRepositories = 0;
            _totalRepositories = 0;

            string apiUrl = _selectedSource == _gitHubProvider.Username
                ? "https://api.github.com/user/repos"
                : $"https://api.github.com/orgs/{_selectedSource}/repos";

            using (var request = new UnityEngine.Networking.UnityWebRequest(apiUrl, UnityEngine.Networking.UnityWebRequest.kHttpVerbGET))
            {
                request.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
                request.SetRequestHeader("Authorization", "Bearer " + _gitHubProvider.AccessToken);
                request.SetRequestHeader("User-Agent", "UnityEditor-UGPM");

                await SendWebRequestAsync(request);

                if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    var jsonArray = JArray.Parse(request.downloadHandler.text);
                    _totalRepositories = jsonArray.Count; // Устанавливаем общее количество репозиториев

                    List<Task<RepositoryInfo>> tasks = new List<Task<RepositoryInfo>>();

                    foreach (var item in jsonArray)
                    {
                        tasks.Add(ParseRepository(item)); // Запускаем обработку каждого репозитория
                    }

                    var results = await Task.WhenAll(tasks); // Дожидаемся всех задач

                    foreach (var repo in results)
                    {
                        if (repo != null)
                        {
                            _repositories.Add(repo);

                            await Task.Delay(50);

                            _loadedRepositories++;
                        }
                        Repaint();
                    }
                }
                else
                {
                    Debug.LogError($"Error fetching repositories: {request.responseCode} {request.error}");
                }
            }

            _isLoading = false;
            Repaint();
        }

        public async Task<List<VersionInfo>> FetchReleasesAsync(string owner, string repo,
            string accessToken)
        {
            string url = $"https://api.github.com/repos/{owner}/{repo}/releases";
            long? latestReleaseId = await GetLatestReleaseIdAsync(owner, repo, accessToken);

            var versions = new List<VersionInfo>();

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("UnityEditor-UGPM");
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                HttpResponseMessage response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    Debug.LogError($"Error fetching releases: {response.StatusCode}");
                    return versions;
                }

                string jsonResponse = await response.Content.ReadAsStringAsync();
                var releases = JArray.Parse(jsonResponse);

                foreach (var item in releases)
                {
                    long releaseId = item["id"]?.ToObject<long>() ?? 0;
                    string version = NormalizeVersion(item["tag_name"]?.ToString());

                    PackageInfo package = await FetchPackageInfo(owner, repo, version);

                    bool isLatest = latestReleaseId.HasValue && releaseId == latestReleaseId.Value;
                    bool isInstalled = false;

                    versions.Add(new VersionInfo(
                        package,
                        item["published_at"]?.ToString(),
                        item["body"]?.ToString(),
                        item["html_url"]?.ToString(),
                        isInstalled, // IsInstalled (нужно проверять отдельно)
                        isLatest,
                        item["prerelease"]?.ToObject<bool>() ?? false,
                        item["draft"]?.ToObject<bool>() ?? false
                    ));
                }
            }

            return versions;
        }

        public static string NormalizeVersion(string version)
        {
            if (string.IsNullOrEmpty(version))
                return "0.0.0"; // Возвращаем дефолтное значение, если версия пустая

            // Убираем все символы, кроме цифр и точек
            string cleanedVersion = Regex.Replace(version, @"[^\d.]", "");

            // Разбиваем строку по точкам, убирая пустые части
            var parts = cleanedVersion.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);

            // Если элементов меньше 3, дополняем до трех "0"
            while (parts.Length < 3)
                cleanedVersion += ".0";

            // Если элементов больше 3, отбрасываем лишние
            parts = parts.Take(3).ToArray();

            return string.Join(".", parts);
        }

        private async Task<long?> GetLatestReleaseIdAsync(string owner, string repo, string accessToken)
        {
            string url = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("UnityEditor-UGPM");
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                HttpResponseMessage response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                    return null;

                string jsonResponse = await response.Content.ReadAsStringAsync();
                var releaseData = JObject.Parse(jsonResponse);

                return releaseData["id"]?.ToObject<long>();
            }
        }

        private async Task<RepositoryInfo> ParseRepository(JToken item)
        {
            try
            {
                string owner = item["owner"]["login"]?.ToString();
                string name = item["name"]?.ToString();
                string cloneUrl = item["clone_url"]?.ToString();
                int stars = item["stargazers_count"]?.ToObject<int>() ?? 0;
                string updatedAt = item["updated_at"]?.ToString();

                if (string.IsNullOrEmpty(name))
                    return null;

                // Асинхронная загрузка `unity-git-package.json`
                PackageInfo packageInfo = await FetchPackageInfo(owner, name);

                string iconPath = $"https://raw.githubusercontent.com/{owner}/{name}/main/{packageInfo?.iconPath}";
                Texture2D icon = await IconCacheManager.GetIcon(name, iconPath, _gitHubProvider.AccessToken);
                List<VersionInfo> versions = await FetchReleasesAsync(owner, name, _gitHubProvider.AccessToken);

                bool isExist = _packageManagerService.HasPackage(name);

                return new RepositoryInfo(owner, name, stars, updatedAt, cloneUrl, icon, versions, isExist);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error parsing repository: {ex.Message}");
                return null;
            }
        }

        private async Task<PackageInfo> FetchPackageInfo(string owner, string repoName, string version = null)
        {
            string fileApiUrl = $"https://api.github.com/repos/{owner}/{repoName}/contents/package.json";
            fileApiUrl += version != null ? $"#{version}" : "";
            Debug.Log($"Requesting file from: {fileApiUrl}");

            using (var request = new UnityEngine.Networking.UnityWebRequest(fileApiUrl, UnityEngine.Networking.UnityWebRequest.kHttpVerbGET))
            {
                request.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
                request.SetRequestHeader("Authorization", "Bearer " + _gitHubProvider.AccessToken);
                request.SetRequestHeader("User-Agent", "UnityEditor-UGPM");

                await SendWebRequestAsync(request);

                if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    try
                    {
                        var jsonResponse = JObject.Parse(request.downloadHandler.text);
                        string base64Content = jsonResponse["content"]?.ToString();
                        if (string.IsNullOrEmpty(base64Content))
                        {
                            Debug.LogWarning($"File content is empty or not found for {repoName}.");
                            return new PackageInfo(null);
                        }

                        string fileContent = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(base64Content));
                        JObject json = JObject.Parse(fileContent);

                        return new PackageInfo(json);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Error parsing unity-git-package.json for {repoName}: {ex.Message}");
                    }
                }
                else
                {
                    Debug.LogWarning($"Failed to fetch unity-git-package.json for {repoName}: {request.error}");
                }
            }

            return new PackageInfo(null);
        }

        private async Task SendWebRequestAsync(UnityEngine.Networking.UnityWebRequest request)
        {
            var completionSource = new TaskCompletionSource<bool>();
            var operation = request.SendWebRequest();

            operation.completed += _ => completionSource.SetResult(true);
            await completionSource.Task;
        }

        private void DrawRepositoriesGrid(int columns, float width)
        {
            int count = 0;

            EditorGUILayout.BeginVertical();
            EditorGUILayout.BeginHorizontal();

            foreach (var repo in _repositories)
            {
                if (_isHideInvalid && repo.HasUnityPackage == false)
                    continue;

                DrawRepositoryCard(repo, width);
                count++;

                // Начинаем новую строку каждые `columns` элементов
                if (count % columns == 0)
                {
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                }
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }


        private void DrawRepositoryCard(RepositoryInfo repo, float width)
        {
            bool isSelected = _selectedRepo == repo;

            // Определяем стиль карточки
            GUIStyle cardStyle = new GUIStyle(GUI.skin.box);

            // Добавляем выделение при выборе
            if (isSelected)
            {
                cardStyle.normal.background = Texture2D.whiteTexture;
                GUI.backgroundColor = Color.cyan; // Цвет рамки
            }
            else
            {
                GUI.backgroundColor = Color.gray;
            }

            if (GUILayout.Button("", cardStyle, GUILayout.Width(width), GUILayout.Height(250)))
            {
                _selectedRepo = repo;
                _selectedTab = RepositoryDetailTab.Description;
                Repaint(); // Обновляем UI
            }

            // Получаем последнюю область, куда была отрисована карточка
            Rect lastRect = GUILayoutUtility.GetLastRect();

            // 🟢 Рисуем границу только для выделенной карточки
            if (isSelected)
            {
                DrawBorder(lastRect, 2f, Color.cyan);
            }

            // Рисуем текст и элементы внутри карточки
            GUI.Label(new Rect(lastRect.x + 10, lastRect.y + 10, width - 20, 20), repo.GetDisplayName(), EditorStyles.boldLabel);
            GUI.DrawTexture(new Rect(lastRect.x + 10, lastRect.y + 40, width - 20, 100), repo.Icon, ScaleMode.ScaleToFit);
            GUI.Label(new Rect(lastRect.x + 10, lastRect.y + 150, width - 20, 20), $"Stars: {repo.Stars}");
            GUI.Label(new Rect(lastRect.x + 10, lastRect.y + 170, width - 20, 20), $"Updated: {repo.UpdatedAt}");

            // ✅ Добавляем галочку в правый нижний угол, если пакет уже импортирован
            if (repo.IsExist)
            {
                Rect checkmarkRect = new Rect(lastRect.x + width - 25, lastRect.y + 225, 20, 20);
                GUI.DrawTexture(checkmarkRect, EditorGUIUtility.IconContent("d_Toggle Icon").image as Texture2D);
            }

            // Сбрасываем цвет
            GUI.backgroundColor = Color.white;
        }

        private void DrawBorder(Rect rect, float thickness, Color color)
        {
            // Запоминаем текущий цвет
            Color prevColor = GUI.color;
            GUI.color = color;

            // Верхняя линия
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), Texture2D.whiteTexture);
            // Нижняя линия
            GUI.DrawTexture(new Rect(rect.x, rect.y + rect.height - thickness, rect.width, thickness), Texture2D.whiteTexture);
            // Левая линия
            GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), Texture2D.whiteTexture);
            // Правая линия
            GUI.DrawTexture(new Rect(rect.x + rect.width - thickness, rect.y, thickness, rect.height), Texture2D.whiteTexture);

            // Восстанавливаем исходный цвет
            GUI.color = prevColor;
        }

        public class RepositoryInfo
        {
            public string Owner { get; set; }               // Владелец репозитория (юзер или организация)
            public int Stars { get; set; }                  // Количество звезд
            public string UpdatedAt { get; set; }           // Дата последнего обновления
            public string CloneUrl { get; set; }            // URL клонирования репозитория

            private readonly string _name;

            public readonly Texture2D Icon;
            public IReadOnlyList<VersionInfo> Versions { get; private set; }
            public bool IsExist { get; }

            public RepositoryInfo(string owner, string name,
                int stars, string updatedAt, string cloneUrl, Texture2D icon,
                List<VersionInfo> versions, bool isExist)
            {
                Owner = owner;
                _name = name;
                Stars = stars;
                UpdatedAt = updatedAt;
                CloneUrl = cloneUrl;
                Versions = versions;
                IsExist = isExist;
                Icon = icon;
            }

            public bool HasUnityPackage => Versions.Count > 0;
            public string ChangelogUrl => HasUnityPackage ? $"https://github.com/{Owner}/{_name}/releases" : null;

            public string GetDisplayName()
            {
                if (HasUnityPackage)
                    return GetVersionInfo().package.displayName;

                return _name;
            }

            public VersionInfo GetVersionInfo()
            {
                foreach (VersionInfo version in Versions)
                {
                    if (version.IsDraft)
                        continue;

                    if (version.IsLatest)
                        return version;
                }

                if (Versions.Count > 0)
                    return Versions[0];

                return null;
            }
        }

        [Serializable]
        public class PackageInfo
        {
            public readonly string name;
            public readonly string version;
            public readonly string displayName;
            public readonly string description;
            public readonly string documentationUrl;
            public readonly string licensesUrl;
            public readonly string iconPath;

            public PackageInfo(JObject json)
            {
                if (json == null)
                {
                    Debug.LogWarning("GitPackageInfo: JSON is null, using default values.");
                    return;
                }

                name = json["name"]?.ToString() ?? string.Empty;
                version = json["version"]?.ToString() ?? string.Empty;
                displayName = json["displayName"]?.ToString() ?? string.Empty;
                description = json["description"]?.ToString() ?? string.Empty;
                documentationUrl = json["documentationUrl"]?.ToString() ?? string.Empty;
                licensesUrl = json["licensesUrl"]?.ToString() ?? string.Empty;
                iconPath = json["iconPath"]?.ToString() ?? string.Empty;
            }
        }

        /*[Serializable]
        public class GitPackageInfo
        {
            public string GitPackageUrl { get; private set; }
            public string DocumentationUrl { get; private set; }
            public string LicenseUrl { get; private set; }
            public string IconUrl { get; private set; }
            public string RecommendedVersion { get; private set; }
            public string[] GitDependencies { get; private set; }

            // ✅ Конструктор, который принимает JSON-объект и безопасно извлекает данные
            public GitPackageInfo(JObject json)
            {
                if (json == null)
                {
                    Debug.LogWarning("GitPackageInfo: JSON is null, using default values.");
                    return;
                }

                GitPackageUrl = json["gitPackageUrl"]?.ToString() ?? string.Empty;
                DocumentationUrl = json["readmeUrl"]?.ToString() ?? string.Empty;
                LicenseUrl = json["licenseUrl"]?.ToString() ?? string.Empty;
                IconUrl = json["iconUrl"]?.ToString() ?? string.Empty;
                RecommendedVersion = json["recommended"]?.ToString() ?? string.Empty;

                var dependenciesArray = json["gitDependencies"] as JArray;
                GitDependencies = dependenciesArray?.ToObject<string[]>() ?? new string[0];

                Debug.Log($"Parsed GitPackageInfo: {GitPackageUrl}, Readme: {DocumentationUrl}, License: {LicenseUrl}, Icon: {IconUrl}");
            }
        }*/
        public class VersionInfo
        {
            public readonly PackageInfo package;
            public string ReleaseDate { get; private set; }
            public string Changelog { get; private set; }
            public string ChangelogUrl { get; private set; }
            public bool IsInstalled { get; private set; }
            public bool IsLatest { get; private set; }
            public bool IsPrerelease { get; private set; }
            public bool IsDraft { get; private set; }
            public bool IsExpanded { get; set; }

            public VersionInfo(PackageInfo package, string releaseDate, string changelog, string changelogUrl,
                bool isInstalled, bool isLatest, bool isPrerelease, bool isDraft)
            {
                this.package = package;
                ReleaseDate = releaseDate;
                Changelog = changelog;
                ChangelogUrl = changelogUrl;
                IsInstalled = isInstalled;
                IsLatest = isLatest;
                IsPrerelease = isPrerelease;
                IsDraft = isDraft;
            }
        }
    }
}