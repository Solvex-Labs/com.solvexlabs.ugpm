# Unity Git Package Manager (UGPM)

Unity Git Package Manager (UGPM) is a tool for conveniently managing packages in Unity via Git. It allows you to easily add, update, and remove packages using Git repositories without manually editing `manifest.json`.

## Features

- Support for installing packages directly from Git repositories
- Automatic package updates
- Removing packages from the project
- Support for private repositories using Git Credential Manager (GCM)
- Simplified interface for working with `Packages/manifest.json`
- Integration with Unity Package Manager (UPM)

## Installation

### Cloning via Unity Package Manager (UPM)

1. Open Unity.
2. Go to `Window > Package Manager`.
3. Click the `+` button in the top-left corner and select `Add package from git URL`.
4. Enter the repository URL:
   ```
   https://github.com/Solvex-Labs/com.solvexlabs.ugpm.git#{version}
   ```
   ### Example
   ```
   https://github.com/Solvex-Labs/com.solvexlabs.ugpm.git#v1.0.3
   ```
5. Click `Add` and wait for Unity to install the package.

### Cloning via manifest.json

1. Open Unity and navigate to your project's `Packages` folder.
2. Open `manifest.json` with a text editor.
3. Add the UGPM repository URL under `dependencies`. Use the appropriate release version:
   ```json
   {
     "dependencies": {
       "com.solvexlabs.ugpm": "git+https://github.com/Solvex-Labs/com.solvexlabs.ugpm.git#v1.0.3"
     }
   }
   ```
4. Save the file and restart Unity for the changes to take effect.
5. You can check available versions in the [Releases](https://github.com/Solvex-Labs/com.solvexlabs.ugpm/releases) section of the repository to select the correct version.

## Git Credential Manager (GCM)

To access private repositories and interact with GitHub's API, UGPM uses [Git Credential Manager (GCM)](https://github.com/git-ecosystem/git-credential-manager). GCM provides secure authentication without requiring manual entry of credentials every time you interact with a private repository.

### Why Use GCM?

- **Security**: Credentials are stored securely using the system’s credential store.
- **Convenience**: No need to repeatedly enter credentials when accessing private repositories.
- **Multi-platform Support**: Works on Windows, macOS, and Linux.

### More Information

For installation and configuration details, please visit the official Git Credential Manager repository on GitHub: [https://github.com/git-ecosystem/git-credential-manager](https://github.com/git-ecosystem/git-credential-manager)

## How to Use

### Opening UGPM

1. Open Unity.
2. Navigate to `Tools > Unity Git Package Manager` in the top menu.  

### Browsing Packages

1. The UGPM window will display a list of available packages from the selected source.  
   ![UGPM Package List](docs/images/ugpm_package_list.png)
2. Use the `Sources` dropdown to filter by organization (e.g., Solvex-Labs).
3. Browse through the packages and select the one you need.

### Installing a Package

1. Click on the package you want to install.  
   ![UGPM Package Details](docs/images/ugpm_package_details_soe.png)
2. The package details panel on the right will show its description, version, and repository link.
3. Click `Install` or select the desired version from the `Version History` tab.  
   ![UGPM Version History](docs/images/ugpm_version_history.png)

## Creating Custom Repositories

UGPM allows you to host and manage your own Git repositories for custom Unity packages. Below are the steps to set up and use a custom repository.

### Setting Up a Custom Package Repository

1. **Create a New Git Repository Using a Template**
   - Go to the [Solvex Labs Package Template](https://github.com/Solvex-Labs/package-template).
   - Click the `Use this template` button to create a new repository based on the template.
   - Name your repository following the Unity package naming convention, e.g., `com.yourcompany.mypackage`.

2. **Configure the Unity Package**
   - The template already contains the necessary folder structure, including `Runtime/`, `Editor/`, and `Tests/` directories.
   - Open the `package.json` file and update the following fields:
     ```json
     {
       "name": "com.yourcompany.mypackage",
       "version": "1.0.0",
       "displayName": "My Custom Package",
       "description": "A custom Unity package.",
       "unity": "2022.3",
       "documentationUrl": "https://example.com/",
       "licensesUrl": "https://example.com/licensing.html",
       "dependencies": {},
       "author": {
         "email": "your.email@example.com",
         "name": "Your Name or Company",
         "url": "https://github.com/yourcompany"
       },
       "iconPath": "Resources/packageIcon.png"
     }
     ```
   - Ensure that:
     - The `name` field matches the repository name.
     - The `author` field contains your name, email, and URL.
     - The `documentationUrl` and `licensesUrl` point to relevant pages.
     - Dependencies are correctly specified if needed.
   - If necessary, you can replace the package icon at **[Icon](Resources/packageIcon.png)**. The required resolution for the icon is **128x128 pixels**.

### Automated Versioning and Releases

To simplify package versioning and release creation, UGPM uses **GitHub Actions** to automatically generate releases when a new tag is pushed. The GitHub Actions workflow for release automation is available in:

📄 **[.github/workflows/release.yml](.github/workflows/release.yml)**

#### Tagging Suggestions
It’s common practice to prefix your version names with the letter `v`. Some good tag names might be:
- `v1.0.0`
- `v2.3.4`

If the tag isn’t meant for production use, add a pre-release version after the version name. Some good pre-release versions might be:
- `v0.2.0-alpha`
- `v5.9-beta.3`

#### Semantic Versioning
If you’re new to releasing software, we highly recommend learning more about **[semantic versioning](https://semver.org/)**.

A newly published release will automatically be labeled as the latest release for this repository.

If 'Set as the latest release' is unchecked, the latest release will be determined by higher semantic version and creation date. **[Learn more about release settings](https://docs.github.com/en/repositories/releasing-projects-on-github/managing-releases-in-a-repository)**.

### Viewing the Repository in UGPM

Once the repository is created and pushed, it should appear in UGPM under the **source that owns the repository**. This allows you to easily browse, install, and manage the package directly within Unity.

## Requirements

- Unity 2021.3 or newer
- Git installed on the system and accessible from the command line
- Internet access to download packages

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.

## Contact

If you have any questions or suggestions, contact us:
- GitHub Issues: [https://github.com/Solvex-Labs/com.solvexlabs.ugpm/issues](https://github.com/Solvex-Labs/com.solvexlabs.ugpm/issues)

