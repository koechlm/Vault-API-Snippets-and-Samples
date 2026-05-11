# Vault.ManageProperties.Core

Helper class to manage Autodesk Vault file properties including updates and synchronization using the filestore service. This is the **.NET 10** build targeting the Vault 2027 SDK Core assemblies.

> For the .NET Framework 4.8 build, see [Vault.ManageProperties](https://github.com/koechlm/Vault-API-Snippets-and-Samples/pkgs/nuget/Vault.ManageProperties).

## Prerequisites

- **Autodesk Vault 2026 SDK** must be installed (Core assemblies from `...\bin\x64\Core\`)
- **.NET 8.0** (Windows)
- **x64 platform target**

## Installation

```
Install-Package Vault.ManageProperties.Core
```

Or via .NET CLI:

```
dotnet add package Vault.ManageProperties.Core
```

> **Important:** After installing, ensure your project references the following Vault SDK **Core** assemblies from your local SDK installation (`%ProgramW6432%\Autodesk\Autodesk Vault 2026 SDK\bin\x64\Core\`):
> - `Autodesk.Connectivity.WebServices.dll`
> - `Autodesk.Connectivity.WebServices.WCF.dll`
> - `Autodesk.DataManagement.Client.Framework.dll`
> - `Autodesk.DataManagement.Client.Framework.Vault.dll`
>
> These are **not** included in the NuGet package.

## Quick Start

```csharp
using Autodesk.Connectivity.WebServices;
using Autodesk.DataManagement.Client.Framework.Vault.Currency.Connections;
using Vault_API_Sample_ManageProperties;

// Connect to Vault
Connection connection = Vault.Forms.Library.Login(null);

// Read conversion options from Vault settings
bool dateOnly = connection.WebServiceManager.KnowledgeVaultService
    .GetVaultOption("Autodesk.EDM.UpdateProperties.DateMappingOption") == "1";
bool boolAsInt = connection.WebServiceManager.KnowledgeVaultService
    .GetVaultOption("Autodesk.EDM.UpdateProperties.WriteBoolPropertyAsN") == "1";

// Initialize
ManageProperties manageProps = new ManageProperties(connection, dateOnly, boolAsInt);

// Prepare property updates (display name -> string value)
Dictionary<string, string> newValues = new Dictionary<string, string>()
{
    { "Title", "Updated Title" },
    { "Description", "Updated via API" }
};

// Convert to typed dictionary and update
Dictionary<PropDef, object> typedProps = manageProps.ConvertToPropDictionary(newValues);

PropWriteResults writeResults;
string[] cloakedEntityClasses;
File updatedFile = manageProps.UpdateFileProperties(
    file, "Property update via API", true,
    typedProps, out writeResults, out cloakedEntityClasses
);
```

## Key Methods

| Method | Description |
|--------|-------------|
| `UpdateFileProperties` | Intelligently classifies properties by mapping direction and executes a two-phase update (DB first, then sync). Handles Write-only, ReadAndWrite, and unmapped properties correctly. |
| `SyncProperties` | Synchronizes properties from Vault to the physical file, optionally overriding values. Resolves compliance failures. |
| `UpdateDbPropValues` | Updates unmapped properties directly in the Vault database with full check-in. |
| `UpdatePropertiesBatch` | Batch version of `UpdateDbPropValues` for improved performance with multiple files. |
| `ConvertToPropDictionary` | Converts display-name/string-value pairs to typed `PropDef`/object dictionaries. |

## Property Mapping Concepts

The class classifies properties into three categories based on their mapping direction:

| Mapping | Behavior |
|---------|----------|
| **Write-only** | Written to file via sync; DB updated explicitly (no Read mapping to read back) |
| **ReadAndWrite** | Written to file via sync; check-in reads back to DB automatically |
| **Unmapped** | DB-only; no file involvement |

`UpdateFileProperties` handles all three types automatically using a two-phase approach:
1. **Phase 1:** `SetDbPropValues` — sets Write-only + Unmapped values in DB without check-in
2. **Phase 2:** `SyncProperties` — writes mapped values into the file and checks in

## Documentation

Full API reference is available in the `docs/ManageProperties.md` file included in the package, or in the [GitHub repository](https://github.com/koechlm/Vault-API-Snippets-and-Samples).

## Credits

Major parts of the code originally posted on the blog "Just Ones and Zeros" by Dave Mink and Doug Redmond. Refactored with enhanced error handling, caching, and three-way property classification.
