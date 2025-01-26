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
        private const string PackageFileName = "unity-git-package.json";

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

            float leftPanelWidth = _selectedRepo != null ? CardWidth * MinColumsAmount + Padding : MinWindowSize; // Учитываем отступы
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

            // 🎯 Правая панель (Детали репозитория)
           

            if (_selectedRepo != null)
            {
                EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
                DrawRepositoryDetails();
                EditorGUILayout.EndVertical(); // Закрытие правой панели
            }
            else
            {
                //GUILayout.Label("Select a repository to view details", EditorStyles.helpBox);
            }

            
            EditorGUILayout.EndHorizontal(); // Закрытие главного `BeginHorizontal()`
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
            GUILayout.Label(_selectedRepo.Name, titleStyle);

            VersionInfo info = _selectedRepo.GetVersionInfo();
            if (info != null)
            {
                GUILayout.Label($"Version: {info.Version}", EditorStyles.miniLabel);
            }
            
            GUILayout.Label($"Package ID: {_selectedRepo.CloneUrl}", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // 🔹 Верхние кнопки (Documentation, Changelog, Licenses)
            EditorGUILayout.BeginHorizontal();
            DrawLinkButton("Documentation", _selectedRepo.PackageInfo?.DocumentationUrl);
            DrawLinkButton("Changelog", _selectedRepo.ChangelogUrl);
            DrawLinkButton("License", _selectedRepo.PackageInfo?.LicenseUrl);
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
                        string bundleId = _packageManagerService.GetPackageBundle(_selectedRepo.Name);

                        if (bundleId != null)
                            _packageManagerService.RemovePackage(bundleId, Close);
                    }
                }
                else
                {
                    if (GUILayout.Button("Import", GUILayout.Height(30)))
                    {
                        _packageManagerService.ImportGitPackage(_selectedRepo.PackageInfo?.GitPackageUrl, Close);
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
                    GUILayout.Label(_selectedRepo.Description ?? "No description available", EditorStyles.wordWrappedLabel);
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
                    if (_selectedRepo.PackageInfo?.GitDependencies != null && _selectedRepo.PackageInfo.GitDependencies.Length > 0)
                    {
                        foreach (var dependency in _selectedRepo.PackageInfo.GitDependencies)
                        {
                            GUILayout.Label(dependency);
                        }
                    }
                    else
                    {
                        GUILayout.Label("No dependencies found.", EditorStyles.wordWrappedLabel);
                    }
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

        private void DrawVersionCard(VersionInfo version)
        {
            EditorGUILayout.BeginVertical("box");

            // Кнопка переключения состояния (свернуть/развернуть)
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(version.IsExpanded ? "▼" : "▶", GUILayout.Width(20)))
            {
                version.IsExpanded = !version.IsExpanded;
            }

            // Версия
            GUILayout.Label(version.Version, EditorStyles.boldLabel);

            // Определяем тип версии
            if (version.IsLatest)
            {
                DrawVersionType("Latest", Color.green);
            }
            if (version.IsRecommended)
            {
                DrawVersionType("Recommended", Color.green);
            }
            if (version.IsPrerelease)
            {
                DrawVersionType("Pre-release", Color.yellow);
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label(version.ReleaseDate, EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            // Если карточка развернута, рисуем дополнительную информацию
            if (version.IsExpanded)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Changelog:", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(version.Changelog, EditorStyles.wordWrappedLabel);

                if (!string.IsNullOrEmpty(version.ChangelogUrl))
                {
                    if (GUILayout.Button("See full changelog", EditorStyles.linkLabel))
                    {
                        Application.OpenURL(version.ChangelogUrl);
                    }
                }

                EditorGUILayout.Space();
                EditorGUILayout.BeginHorizontal();

                if (version.IsInstalled)
                {
                    if (GUILayout.Button("Remove", GUILayout.Width(80)))
                    {
                        Debug.Log($"Removing {version.Version}");
                    }
                }
                else
                {
                    if (GUILayout.Button("Update", GUILayout.Width(80)))
                    {
                        Debug.Log($"Updating to {version.Version}");
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
            string accessToken, string recommendedVersion)
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

                    bool isLatest = latestReleaseId.HasValue && releaseId == latestReleaseId.Value;
                    bool isInstalled = false;
                    bool isRecommended = string.IsNullOrEmpty(recommendedVersion) == false && recommendedVersion == version;
                    Debug.Log($"{recommendedVersion} / {version} = {isRecommended}");

                    versions.Add(new VersionInfo(
                        version,
                        item["published_at"]?.ToString(),
                        item["body"]?.ToString(),
                        item["html_url"]?.ToString(),
                        isInstalled, // IsInstalled (нужно проверять отдельно)
                        isLatest,
                        isRecommended,
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
                string description = item["description"]?.ToString();
                string cloneUrl = item["clone_url"]?.ToString();
                int stars = item["stargazers_count"]?.ToObject<int>() ?? 0;
                string updatedAt = item["updated_at"]?.ToString();

                if (string.IsNullOrEmpty(name))
                    return null;

                // Асинхронная загрузка `unity-git-package.json`
                GitPackageInfo packageInfo = await FetchGitPackageInfo(owner, name);
                Texture2D icon = await IconCacheManager.GetIcon(name, packageInfo?.IconUrl, _gitHubProvider.AccessToken);
                List<VersionInfo> versions = await FetchReleasesAsync(owner, name, _gitHubProvider.AccessToken, packageInfo.RecommendedVersion);

                bool isExist = _packageManagerService.HasPackage(name);

                return new RepositoryInfo(owner, name, description, stars, updatedAt, cloneUrl, packageInfo, icon, versions, isExist);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error parsing repository: {ex.Message}");
                return null;
            }
        }

        private async Task<GitPackageInfo> FetchGitPackageInfo(string owner, string repoName)
        {
            string fileApiUrl = $"https://api.github.com/repos/{owner}/{repoName}/contents/{PackageFileName}";
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
                            return new GitPackageInfo(null);
                        }

                        string fileContent = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(base64Content));
                        JObject json = JObject.Parse(fileContent);

                        return new GitPackageInfo(json);
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

            return new GitPackageInfo(null);
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
            GUI.Label(new Rect(lastRect.x + 10, lastRect.y + 10, width - 20, 20), repo.Name, EditorStyles.boldLabel);
            GUI.DrawTexture(new Rect(lastRect.x + 10, lastRect.y + 40, width - 20, 100), repo.Icon, ScaleMode.ScaleToFit);
            GUI.Label(new Rect(lastRect.x + 10, lastRect.y + 150, width - 20, 20), $"Stars: {repo.Stars}");
            GUI.Label(new Rect(lastRect.x + 10, lastRect.y + 170, width - 20, 20), $"Updated: {repo.UpdatedAt}");

            string status = repo.HasUnityPackage ? "✅ Valid unity-git-package.json" : "❌ Invalid json file";
            GUI.Label(new Rect(lastRect.x + 10, lastRect.y + 190, width - 20, 20), status, EditorStyles.miniLabel);

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
            public string Name { get; set; }                // Название репозитория
            public string Description { get; set; }         // Описание репозитория
            public int Stars { get; set; }                  // Количество звезд
            public string UpdatedAt { get; set; }           // Дата последнего обновления
            public string CloneUrl { get; set; }            // URL клонирования репозитория

            public GitPackageInfo PackageInfo { get; }      // Информация о Unity Package
            public Texture2D Icon { get; private set; }
            public IReadOnlyList<VersionInfo> Versions { get; private set; }
            public bool IsExist { get; }

            public RepositoryInfo(string owner, string name, string description,
                int stars, string updatedAt, string cloneUrl, GitPackageInfo packageInfo,
                Texture2D icon, List<VersionInfo> versions, bool isExist)
            {
                Owner = owner;
                Name = name;
                Description = description;
                Stars = stars;
                UpdatedAt = updatedAt;
                CloneUrl = cloneUrl;
                PackageInfo = packageInfo;
                Icon = icon;
                Versions = versions;
                IsExist = isExist;
            }

            public bool HasUnityPackage => !string.IsNullOrEmpty(PackageInfo?.GitPackageUrl);
            public string ChangelogUrl => HasUnityPackage ? $"https://github.com/{Owner}/{Name}/releases" : null;

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
        }
    }

    public class VersionInfo
    {
        public string Version { get; private set; }
        public string ReleaseDate { get; private set; }
        public string Changelog { get; private set; }
        public string ChangelogUrl { get; private set; }
        public bool IsInstalled { get; private set; }
        public bool IsLatest { get; private set; }
        public bool IsRecommended { get; private set; }
        public bool IsPrerelease { get; private set; }
        public bool IsDraft { get; private set; }
        public bool IsExpanded { get; set; }

        public VersionInfo(string version, string releaseDate, string changelog, string changelogUrl,
            bool isInstalled, bool isLatest, bool isRecommended, bool isPrerelease, bool isDraft)
        {
            Version = version;
            ReleaseDate = releaseDate;
            Changelog = changelog;
            ChangelogUrl = changelogUrl;
            IsInstalled = isInstalled;
            IsLatest = isLatest;
            IsRecommended = isRecommended;
            IsPrerelease = isPrerelease;
            IsDraft = isDraft;
        }
    }
}