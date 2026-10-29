# Navigation Samples

C# samples that open a Vault entity from external code: **Vault Explorer** (thick client) vs **Thin Client** (browser).

| Sample | Mechanism |
|--------|-----------|
| `Vault-API-Sample-NavigateToVaultExplorer` | Builds `.acr` (ADM XML) files and opens them so Vault Explorer navigates to folders, files, items, change orders, or custom entities. Also prints `EntityDataCommandRequest.aspx` URLs for browser testing. |
| `Vault-API-Sample-NavigateToVaultThinClient` | Opens `AutodeskTC/...` URLs in the default browser. **Does not use `.acr` files.** |

## Vault Explorer and `.acr` files

Thick-client links (email, intranet, or `EntityDataCommandRequest.aspx`) download an `.acr` file. Windows must associate `.acr` with `Connectivity.VaultPro.exe` for links to open Vault and navigate correctly.

If every link starts **another** Vault Explorer window, or Windows reports that no app is associated with `.acr`, fix file association:

1. Read **[Developer Guide — Vault Explorer navigation](../../Developer%20Guide.md#2-vault-explorer-navigation-acr-files)** and **[Troubleshooting](../../Developer%20Guide.md#6-each-vault-link-opens-a-new-vault-explorer-instance-or-acr-does-not-open-vault)**.
2. Edit **`Vault-Explorer-AcrFileAssociation.template.reg`** (Vault client year/path), back up the registry, then merge the file (administrator) or use **Open with → Always use this app** on an `.acr` file (standard user).

The Explorer navigation sample opens generated `.acr` files through the **same Windows association** (`UseShellExecute`), not by hard-starting `Connectivity.VaultPro.exe` on every navigation. Older direct `Process.Start(exe, path)` calls remain in source as comments for comparison.

## Demo videos

Solution-level videos (repo root / Solution Items): `Vault-API-Sample-NavigateToVaultExplorer.mp4`, `Vault-API-Sample-NavigateToThinClient.mp4`.
