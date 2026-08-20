# Vault API Snippets & Samples

Sample code and reusable helpers for the **Autodesk Vault .NET API** (Vault Professional 2026/2027). The repository is organized as a Visual Studio solution with focused examples you can open, build, and adapt for your own integrations.

## Getting Started

If you are new to Vault API development, start with **[Developer Guide.md](Developer%20Guide.md)**. It covers prerequisites, core concepts, authentication, common operations, and troubleshooting for both C# and PowerShell. The guide is written as an onboarding path and pairs well with the samples in this repository.

### Prerequisites

- Autodesk Vault Professional 2026/2027 (client and/or server)
- Autodesk Vault SDK 2026/2027 (installer is available within the Vault client's program folders)
- Visual Studio 2026 or later
- .NET Framework 4.8, .NET Core 10

Open `Vault-API-Snippets-&-Samples.sln` in Visual Studio, set a sample project as the startup project, update connection settings in `App.config` or the sample code, and run.

## C# Samples

Samples live under `Vault-API-C#-Samples/` and are grouped by topic:


| Folder           | Sample                                                   | Description                                                                                                         |
| ---------------- | -------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------- |
| **Login**        | `Vault-API-Sample-LogOnUsingDialog`                      | Log in using the built-in Vault login dialog (VDF Forms).                                                           |
|                  | `Vault-API-Sample-AutodeskAccountLogin`                  | Authenticate with an Autodesk Account (Click Licensing v9).                                                         |
| **Navigation**   | `Vault-API-Sample-NavigateToVaultExplorer`               | Launch Vault Explorer and navigate to a file, item, change order, or custom object.                                 |
|                  | `Vault-API-Sample-NavigateToVaultThinClient`             | Open the Vault Thin Client at a specific entity.                                                                    |
| **Files**        | `API-FormsApp-UpdateProperties`                          | Windows Forms app for updating file properties via the API.                                                         |
| **Folders**      | `API-ConsoleApp-CreateFolders`                           | Create folders in the Vault hierarchy.                                                                              |
| **Items**        | `API-ConsoleApp-PromoteFileToItem`                       | Promote a file to an item.                                                                                          |
|                  | `API-ConsoleApp-UpdateItemFileAssociations`              | Update file associations on an item.                                                                                |
| **Links**        | `API-ConsoleApp-GetLinksByTargetOrParent`                | Retrieve links by target or parent entity.                                                                          |
| **ECO**          | `API-Onboarding-Create-ECO`                              | Create a change order (ECO) programmatically.                                                                       |
| **Jobs**         | `API-SampleJob-UpdatePartNumberUDP`                      | Custom Job Processor extension that updates a part-number UDP on a file.                                            |
| **VaultDialogs** | `SelectEntity`                                           | Use Vault selection dialogs to pick files, items, and other entities in a standalone app.                           |
| **Properties**   | `Vault-API-Sample-SynchronizeProperties`                 | Synchronize and update file properties (uses the ManageProperties helper).                                          |
|                  | `CustomObjects_Properties_Name-Value-Map`                | Work with custom object properties using name/value maps.                                                           |
|                  | `Vault.ManageProperties` / `Vault.ManageProperties.Core` | Reusable NuGet packages for property update and sync workflows. See each project's README for install instructions. |


Most console samples follow the same pattern: connect to Vault, perform one focused operation, and print results. Forms and dialog samples show how to integrate Vault UI components into standalone applications.

## PowerShell Script Samples

`Vault-API-PowerShell-Script-Samples/` contains complementary scripts for login, search, items, jobs, admin tasks, and ECO creation. A script template is included under `_Templates/` to help you build new samples quickly.

PowerShell scripts are deprecated, as .NET Core/PowerShell 7.2+  no longer allows to simply connect to Vault. The existing scripts require PowerShell 5.

## Additional Resources

- **[Developer Guide.md](Developer%20Guide.md)** — recommended starting point for new developers
- `Autodesk Vault SDK/` — API reference XML documentation for Vault SDK assemblies
- **Solution Items** — demo videos for navigation samples (`Vault-API-Sample-NavigateToVaultExplorer.mp4`, `Vault-API-Sample-NavigateToThinClient.mp4`)



## License

This project is licensed under the GNU General Public License v3.0. See [LICENSE.txt](LICENSE.txt) for details.