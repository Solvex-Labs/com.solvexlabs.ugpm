# Unity Git Package Manager (UGPM)

Unity Git Package Manager (UGPM) is a tool for managing custom Unity packages hosted in Git repositories. UGPM provides an intuitive interface for browsing available repositories, checking compatibility, and automatically importing them into your Unity project.

---

## Features

- **Authentication via Git Credential Manager (GCM):** Secure integration with GitHub for accessing private repositories.
- **UPM (Unity Package Manager) Support:** Automatically adds packages to your Unity project using standard UPM tools.
- **Compatibility Check:** Automatically verifies the presence of a `unity-git-package.json` file and validates dependencies.
- **Simple Interface:** A user-friendly Unity Editor window for managing packages.

---

## Installation

1. Ensure **Git Credential Manager (GCM)** is installed. For more information, refer to the [official documentation](https://github.com/GitCredentialManager/git-credential-manager).
2. Clone this repository or add it to your Unity project's manifest file:
   ```json
   {
       "dependencies": {
           "com.splashgames.ugpm": "https://github.com/your-repo-url.git"
       }
   }
   ```
3. Unity will automatically download and install the package.

---

## How to Use

1. Open UGPM in Unity:
   - **Menu:** `Tools > UGPM`
2. Authenticate via Git Credential Manager.
3. Select an available repository source (personal account or organization).
4. Click `Fetch Repositories` to load the list of accessible repositories.
5. For repositories with a valid `unity-git-package.json` file, click **Import** to add the package to your project.

---

## Format of `unity-git-package.json`

To work correctly with UGPM, the repository must include a `unity-git-package.json` file in the root. Example:

```json
{
    "gitPackageUrl": "https://github.com/username/repository.git#v1.0.0",
    "gitDependencies": [
        "https://github.com/anotheruser/dependency-repo.git#v2.1.0",
        "https://github.com/yetanotheruser/another-dependency.git#v3.0.1"
    ]
}
```

- **`gitPackageUrl`**: The Git repository URL of the package, including an optional tag or branch.
- **`gitDependencies`**: A list of dependencies that should also be added to the project.

---

## Requirements

- Unity 2021.3 or newer.
- Git Credential Manager (GCM) for authentication.
- GitHub or compatible hosting for repositories.

---

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE.md) file for details.

---

## Contact

For suggestions or questions, reach out via [GitHub Issues](https://github.com/your-repo-url/issues).
