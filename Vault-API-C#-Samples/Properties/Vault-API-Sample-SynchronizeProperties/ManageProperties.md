# ManageProperties Class

## Namespace
`Vault_API_Sample_ManageProperties`

## Overview
Helper class to manage file properties including updates and synchronization using the Vault filestore service. This class provides optimized methods for updating file properties, synchronizing properties between Vault and CAD files, and converting property values between different formats.

**Note:** Major parts of the code originally have been posted on the blog "Just Ones and Zeros" by Dave Mink and Doug Redmond. This refactored version combines synchronizing properties and updating property values in one operation with enhanced error handling, performance optimizations (caching property definitions and server configuration), and better edge case handling.

**Important:** Treat this code as a sample and not a production-ready utility, as it does not guarantee to cover all use cases.

## Property Mapping Concepts

Understanding how Vault maps properties between the database and physical files is critical to this class. Each property can have one of four mapping relationships with a file's content source provider:

| Mapping | Read | Write | Description |
|---------|------|-------|-------------|
| **Write-only** | ❌ | ✅ | Value is written into the file during sync, but check-in does **not** read it back to DB. |
| **ReadAndWrite** | ✅ | ✅ | Value is written into the file during sync, and check-in reads it back to DB automatically. |
| **Read-only** | ✅ | ❌ | Value is read from the file during check-in but cannot be written via sync. Treated as unmapped for update purposes. |
| **Unmapped** | ❌ | ❌ | No file involvement; value exists only in the Vault database. |

The `MappingDirection` enum has discrete `Read` and `Write` values. A ReadAndWrite property has **two separate entries** in the `MapDirectionArray` — one `Read` and one `Write` — not a single combined value.

### Execution Flow

The `UpdateFileProperties` method uses a two-phase approach to handle all mapping types correctly:

```
Phase 1: SetDbPropValues (no check-in)
  ├── Write-only properties  → DB must be set explicitly (check-in won't read back)
  └── Unmapped properties    → DB is the only storage

Phase 2: SyncProperties (file write + check-in)
  ├── Write-only properties  → Written into the physical file as overrides
  ├── ReadAndWrite properties → Written into the physical file as overrides;
  │                             check-in reads them back to DB automatically
  └── No overrides            → Resolves any existing compliance failures
```

**Why Phase 1 must not check in:** A check-in triggers Read mappings on **all** Read-mapped properties, not just the ones being updated. If the file still contains old values (sync hasn't written the new values yet), the Read-back would overwrite ReadAndWrite DB values with stale file content. `SetDbPropValues` avoids this by only calling `DocumentService.UpdateFileProperties` without `CopyFile`/`CheckinUploadedFile`, leaving the file checked out for the subsequent sync.

## Constructor

### ManageProperties(Connection, Boolean, Boolean)

Initializes a new instance of the `ManageProperties` class with the specified connection and property conversion options.

#### Syntax
```csharp
public ManageProperties(
    Connection connection,
    bool dateOnly = true,
    bool boolAsInt = false
)
```

#### Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `connection` | `Connection` | The Vault connection to use for all operations. |
| `dateOnly` | `Boolean` | *(Optional)* If `true`, date properties will be converted to date-only format (without time). Default is `true`. |
| `boolAsInt` | `Boolean` | *(Optional)* If `true`, boolean properties will be converted to integers (1/0). Default is `false`. |

#### Remarks
The constructor performs the following initialization operations:
- Caches all property definitions for FILE and ITEM entity classes
- Caches property definition infos to avoid repeated API calls
- Builds a `PropDefInfo` lookup dictionary by Id for the FILE entity class, enabling O(1) lookups during property classification and moniker mapping
- Creates a mapping of display names to system names for file properties
- Caches the server configuration for efficient provider lookups

These caching operations optimize performance by reducing the number of API calls during subsequent operations.

#### Example
```csharp
using Autodesk.DataManagement.Client.Framework.Vault.Currency.Connections;
using Vault_API_Sample_ManageProperties;

// Connect to Vault
Connection connection = Vault.Forms.Library.Login(null);

// Read date and bool conversion options from Vault settings
bool dateOnly = connection.WebServiceManager.KnowledgeVaultService
    .GetVaultOption("Autodesk.EDM.UpdateProperties.DateMappingOption") == "1";
bool boolAsInt = connection.WebServiceManager.KnowledgeVaultService
    .GetVaultOption("Autodesk.EDM.UpdateProperties.WriteBoolPropertyAsN") == "1";

// Initialize ManageProperties helper
ManageProperties manageProps = new ManageProperties(connection, dateOnly, boolAsInt);
```

---

## Public Methods

### UpdateFileProperties

Updates file properties by classifying them into three categories based on their mapping direction, then executing a two-phase update: database-only values first (without check-in), followed by a property sync that writes mapped values into the physical file and checks in.

#### Syntax
```csharp
public File UpdateFileProperties(
    File file,
    string comment,
    bool allowSync,
    Dictionary<PropDef, object> newPropValues,
    out PropWriteResults writeResults,
    out string[] cloakedEntityClasses,
    bool force = false
)
```

#### Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `file` | `File` | The file to update properties on. |
| `comment` | `String` | The comment to use for the new version (if properties are updated). |
| `allowSync` | `Boolean` | If `true`, allows the filestore to retrieve the file from another filestore if not available locally. |
| `newPropValues` | `Dictionary<PropDef, Object>` | Dictionary of property definitions and their new values to update. |
| `writeResults` | `PropWriteResults` | *(Output)* Results from the filestore write operation for mapped properties. |
| `cloakedEntityClasses` | `String[]` | *(Output)* Array of entity class IDs that couldn't be accessed due to insufficient permissions. |
| `force` | `Boolean` | *(Optional)* If `true`, forces the sync operation even if no compliance failures exist. Default is `false`. |

#### Returns
| Type | Description |
|------|-------------|
| `File` | The updated file object. If a new version was created, this will reflect the new version. |

#### Remarks
The method uses `ClassifyProperties` to split input properties into three categories, then builds two execution sets:

| Classification | DB Update Set | Sync Override Set | Rationale |
|---------------|---------------|-------------------|-----------|
| **Write-only** | ✅ | ✅ | No Read mapping → DB must be set explicitly; sync writes value into file |
| **ReadAndWrite** | ❌ | ✅ | Sync writes value into file; check-in reads it back to DB automatically |
| **Unmapped** | ✅ | ❌ | No file involvement; value exists only in DB |

**Execution order:**
1. `SetDbPropValues` — Sets Write-only + Unmapped values in the DB without checking in. The file remains checked out.
2. `SyncProperties` — Always runs. With overrides for Write-only + ReadAndWrite properties (writes values into the file), or without overrides to resolve any existing property compliance failures. The sync performs the check-in.

#### Example
```csharp
using Autodesk.Connectivity.WebServices;
using System.Collections.Generic;

// Get a file from Vault
File file = webServiceManager.DocumentService
    .FindLatestFilesByPaths(new string[] { "$/Designs/Part.ipt" })
    .FirstOrDefault();

// Create property values dictionary
Dictionary<string, string> stringProps = new Dictionary<string, string>()
{
    { "Title", "Updated Title" },
    { "Description", "Updated Description" }
};

// Convert to typed dictionary
Dictionary<PropDef, object> typedProps = manageProps.ConvertToPropDictionary(stringProps);

// Update properties
PropWriteResults writeResults;
string[] cloakedEntityClasses;
File updatedFile = manageProps.UpdateFileProperties(
    file,
    "Updated properties via API",
    allowSync: true,
    typedProps,
    out writeResults,
    out cloakedEntityClasses,
    force: false
);

Console.WriteLine($"Updated to version {updatedFile.VerNum}");
```

---

### UpdateFileProperties (Batch Overload)

Updates file properties for multiple files using the same two-phase approach as the single-file overload, with batch provider resolution for improved performance.

#### Syntax
```csharp
public File[] UpdateFileProperties(
    File[] files,
    string comment,
    bool allowSync,
    Dictionary<PropDef, object>[] propValuesByFile,
    out PropWriteResults[] writeResultsByFile,
    out string[][] cloakedEntityClassesByFile,
    bool force = false
)
```

#### Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `files` | `File[]` | Array of files to update properties on. |
| `comment` | `String` | The comment to use for the new versions. |
| `allowSync` | `Boolean` | If `true`, allows the filestore to retrieve files from another filestore if not available locally. |
| `propValuesByFile` | `Dictionary<PropDef, Object>[]` | Array of property value dictionaries, one for each file. Must match the length of files array. |
| `writeResultsByFile` | `PropWriteResults[]` | *(Output)* Array of results from the filestore write operations for mapped properties. |
| `cloakedEntityClassesByFile` | `String[][]` | *(Output)* Array of arrays containing entity class IDs that couldn't be accessed due to insufficient permissions. |
| `force` | `Boolean` | *(Optional)* If `true`, forces the sync operation even if no compliance failures exist. Default is `false`. |

#### Returns
| Type | Description |
|------|-------------|
| `File[]` | Array of updated file objects. Each element corresponds to the input file at the same index. |

#### Exceptions

| Exception | Condition |
|-----------|-----------|
| `ArgumentException` | Thrown when `files` is null or empty, or when `propValuesByFile` length doesn't match `files` length. |

#### Remarks
This batch overload uses the same three-way classification and two-phase execution as the single-file overload, with optimizations for multiple files:

- **Batching provider lookups**: Uses `ResolveFileProviders` to get providers for all files in a single API call, with an internal cache that avoids repeated lookups for files sharing the same provider name
- **Shared classification logic**: Uses `ClassifyProperties` (the same helper used by the single-file overload) to split each file's properties into three categories

Each file is processed individually through the two-phase sequence:
1. `SetDbPropValues` — Sets Write-only + Unmapped values in the DB without checking in (if any)
2. `SyncProperties` — Writes mapped property overrides into the file and checks in; also resolves compliance failures

**Note:** Since every file requires a sync operation (either with overrides or to resolve compliance), files are processed individually rather than batched. The performance gain comes from the batch provider resolution.

#### Example: Batch Update with Same Properties
```csharp
// Get multiple files
File[] files = webServiceManager.DocumentService
    .FindLatestFilesByPaths(new string[] 
    { 
        "$/Designs/Part001.ipt",
        "$/Designs/Part002.ipt",
        "$/Designs/Part003.ipt"
    });

// Prepare same properties for all files
Dictionary<string, string> commonUpdates = new Dictionary<string, string>()
{
    { "Status", "Released" },
    { "Revision", "B" }
};
Dictionary<PropDef, object> typedCommonUpdates = manageProps.ConvertToPropDictionary(commonUpdates);

// Create array with same properties for all files
Dictionary<PropDef, object>[] propValuesByFile = new Dictionary<PropDef, object>[files.Length];
for (int i = 0; i < files.Length; i++)
{
    propValuesByFile[i] = typedCommonUpdates;
}

// Batch update
PropWriteResults[] writeResults;
string[][] cloakedEntityClasses;
File[] updatedFiles = manageProps.UpdateFileProperties(
    files,
    "Batch release to Revision B",
    allowSync: true,
    propValuesByFile,
    out writeResults,
    out cloakedEntityClasses,
    force: false
);

Console.WriteLine($"Updated {updatedFiles.Length} files");
```

#### Example: Different Properties per File
```csharp
// Prepare different properties for each file
Dictionary<PropDef, object>[] propValuesByFile = new Dictionary<PropDef, object>[files.Length];

for (int i = 0; i < files.Length; i++)
{
    Dictionary<string, string> fileProps = new Dictionary<string, string>()
    {
        { "Title", $"Part {i + 1}" },
        { "Part Number", $"PN-{1000 + i}" },
        { "Description", $"Auto-generated part {i + 1}" }
    };
    propValuesByFile[i] = manageProps.ConvertToPropDictionary(fileProps);
}

// Batch update with file-specific properties
PropWriteResults[] writeResults;
string[][] cloakedEntityClasses;
File[] updatedFiles = manageProps.UpdateFileProperties(
    files,
    "Bulk part numbering",
    allowSync: true,
    propValuesByFile,
    out writeResults,
    out cloakedEntityClasses,
    force: false
);
```

---

### SyncProperties

Synchronizes properties from the physical CAD file to Vault, optionally overriding specific property values during the sync operation.

#### Syntax
```csharp
public File SyncProperties(
    File file,
    string comment,
    bool allowSync,
    out PropWriteResults writeResults,
    out string[] cloakedEntityClasses,
    bool force = false,
    Dictionary<PropDef, object> overridePropValues = null,
    bool keepCheckedOut = false
)
```

#### Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `file` | `File` | The file to synchronize properties on. |
| `comment` | `String` | The comment to use for the new version (if a property sync is performed). |
| `allowSync` | `Boolean` | If `true`, allows the filestore to retrieve the file from another filestore if not available locally. |
| `writeResults` | `PropWriteResults` | *(Output)* Results from the filestore write operation. |
| `cloakedEntityClasses` | `String[]` | *(Output)* Array of entity class IDs that couldn't be accessed due to insufficient permissions. |
| `force` | `Boolean` | *(Optional)* If `true`, forces sync even if no property compliance failures exist. Default is `false`. |
| `overridePropValues` | `Dictionary<PropDef, Object>` | *(Optional)* Dictionary of property definitions and values to override during sync. If `null`, properties are synced as-is from the file. |
| `keepCheckedOut` | `Boolean` | *(Optional)* If `true`, keeps the file checked out after the sync check-in. Default is `false`. |

#### Returns
| Type | Description |
|------|-------------|
| `File` | The updated file object after sync. If no sync was needed, returns the input file unchanged. |

#### Remarks
This method performs the following operations:
1. Checks for property compliance failures (unless `force` is `true` or `overridePropValues` are provided)
2. Checks out the file if needed (handles files already checked out by `SetDbPropValues`)
3. Retrieves component properties from the file
4. Checks for permissions issues (cloaked entities)
5. Converts component property values using `ApplyTypeConversion` based on configured options (`dateOnly`, `boolAsInt`)
6. Builds override moniker mappings using `BuildOverrideMonikerMap` if override values are provided
7. Applies override values using `ApplyOverridesToWriteProps`, which also respects the `dateOnly` and `boolAsInt` conversion options for override values
8. Updates BOM data if overridden properties are part of the BOM
9. Writes properties back to the physical file using the filestore service (`CopyFile`)
10. Checks in the file with the updated properties (`CheckinUploadedFile`)

The method automatically handles:
- Files already checked out by the current user (e.g., from a preceding `SetDbPropValues` call)
- Files checked out by other users (returns without sync)
- Permission issues with linked entities
- Property type conversions (both for synced component values and override values)

**Warning:** Component-level properties cannot be synced without CAD integration.

#### Example
```csharp
// Sync properties without overrides
PropWriteResults writeResults;
string[] cloakedEntityClasses;

File syncedFile = manageProps.SyncProperties(
    file,
    "Property sync via API",
    allowSync: true,
    out writeResults,
    out cloakedEntityClasses,
    force: false
);

if (syncedFile.VerNum > file.VerNum)
{
    Console.WriteLine($"Properties synchronized, new version: {syncedFile.VerNum}");
}
else
{
    Console.WriteLine("No sync needed - properties are up to date");
}
```

#### Example with Override Values
```csharp
// Sync properties with overrides
Dictionary<string, string> overrides = new Dictionary<string, string>()
{
    { "Title", "Overridden Title" },
    { "Revision", "B" }
};

Dictionary<PropDef, object> typedOverrides = manageProps.ConvertToPropDictionary(overrides);

File syncedFile = manageProps.SyncProperties(
    file,
    "Property sync with overrides",
    allowSync: true,
    out writeResults,
    out cloakedEntityClasses,
    force: true, // Force sync even if no compliance failures
    overridePropValues: typedOverrides
);
```

---

### UpdateDbPropValues

Updates file properties directly in the Vault database with a full checkout/check-in cycle. Creates a new file version.

#### Syntax
```csharp
public File UpdateDbPropValues(
    File file,
    string comment,
    Dictionary<PropDef, object> newPropValues,
    bool keepCheckedOut = false
)
```

#### Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `file` | `File` | The file to update properties on. |
| `comment` | `String` | The comment to use for the new version. |
| `newPropValues` | `Dictionary<PropDef, Object>` | Dictionary of property definitions and their new values. |
| `keepCheckedOut` | `Boolean` | *(Optional)* If `true`, keeps the file checked out after updating. Default is `false`. |

#### Returns
| Type | Description |
|------|-------------|
| `File` | The updated file object with the new version information. |

#### Remarks
This method performs a full checkout → DB update → check-in cycle. It is available as a standalone public method for scenarios where only unmapped properties need updating and no sync is required.

**Important:** This method should **not** be used before `SyncProperties` when ReadAndWrite-mapped properties are involved, because the check-in triggers Read mappings that overwrite DB values with old file content. Use `SetDbPropValues` (called internally by `UpdateFileProperties`) for that scenario.

The method:
1. Checks out the file
2. Builds the property instance array using `BuildPropInstParamArray`
3. Updates the properties using `DocumentService.UpdateFileProperties`
4. Creates a new version by copying the existing file data (`CopyFile`)
5. Preserves file associations
6. Checks in the file (`CheckinUploadedFile`)

#### Example
```csharp
// Update unmapped properties directly (standalone, no sync needed)
Dictionary<PropDef, object> unmappedProps = new Dictionary<PropDef, object>()
{
    { customPropDef, "Custom Value" },
    { statusPropDef, "In Review" }
};

File updatedFile = manageProps.UpdateDbPropValues(
    file,
    "Updated custom properties",
    unmappedProps,
    keepCheckedOut: false
);
```

---

### UpdatePropertiesBatch

Updates file properties for multiple files in a single batch operation with full checkout/check-in cycles, providing significant performance improvements over individual updates.

#### Syntax
```csharp
public File[] UpdatePropertiesBatch(
    File[] files,
    string comment,
    Dictionary<PropDef, object>[] propValuesByFile,
    bool keepCheckedOut = false
)
```

#### Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `files` | `File[]` | Array of files to update properties on. |
| `comment` | `String` | The comment to use for the new versions. |
| `propValuesByFile` | `Dictionary<PropDef, Object>[]` | Array of property dictionaries, one for each file. Must match the length of the files array. |
| `keepCheckedOut` | `Boolean` | *(Optional)* If `true`, keeps the files checked out after updating. Default is `false`. |

#### Returns
| Type | Description |
|------|-------------|
| `File[]` | Array of updated file objects. Files that couldn't be checked out are excluded from the results. |

#### Exceptions

| Exception | Condition |
|-----------|-----------|
| `ArgumentException` | Thrown when `files` is null or empty, or when `propValuesByFile` length doesn't match `files` length. |

#### Remarks
This method provides significant performance improvements over calling `UpdateDbPropValues` multiple times by:
- **Batching the property update API call**: Uses `DocumentService.UpdateFileProperties` with arrays to update all files in one server round-trip
- **Shared helper usage**: Uses `BuildPropInstParamArray` (the same helper used by `UpdateDbPropValues`) to construct property arrays for each file
- **Reducing API overhead**: Fewer network calls mean faster execution, especially over high-latency connections

The method:
1. Checks out all files that can be checked out (skips files checked out by others)
2. Builds property arrays for all files using `BuildPropInstParamArray`
3. Updates all properties in a single batched API call
4. Checks in each file individually (preserving file-specific associations)
5. Handles errors gracefully by undoing checkouts on failure

**Important:** Like `UpdateDbPropValues`, this method performs a full check-in and should not be used before `SyncProperties` when ReadAndWrite-mapped properties are involved.

**Performance Note:** For 100 files, this method can be **10-50x faster** than individual updates, depending on network latency.

#### Example: Batch Update Multiple Files
```csharp
// Get multiple files to update
File[] files = webServiceManager.DocumentService
    .FindLatestFilesByPaths(new string[] 
    { 
        "$/Designs/Part001.ipt",
        "$/Designs/Part002.ipt",
        "$/Designs/Part003.ipt"
    });

// Prepare property updates for each file
Dictionary<PropDef, object>[] propValuesByFile = new Dictionary<PropDef, object>[files.Length];

// Same properties for all files
Dictionary<string, string> commonUpdates = new Dictionary<string, string>()
{
    { "Status", "Released" },
    { "Revision", "B" }
};
Dictionary<PropDef, object> typedCommonUpdates = manageProps.ConvertToPropDictionary(commonUpdates);

for (int i = 0; i < files.Length; i++)
{
    propValuesByFile[i] = typedCommonUpdates;
}

// Batch update all files
File[] updatedFiles = manageProps.UpdatePropertiesBatch(
    files,
    "Batch property update",
    propValuesByFile,
    keepCheckedOut: false
);

Console.WriteLine($"Successfully updated {updatedFiles.Length} out of {files.Length} files");
```

---

### ConvertToPropDictionary

Converts a dictionary of property display names and string values to properly typed property definitions and values.

#### Syntax
```csharp
public Dictionary<PropDef, object> ConvertToPropDictionary(
    Dictionary<string, string> keyValuePairs
)
```

#### Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `keyValuePairs` | `Dictionary<String, String>` | Dictionary where keys are property display names and values are string representations of the property values. |

#### Returns
| Type | Description |
|------|-------------|
| `Dictionary<PropDef, Object>` | Dictionary of property definitions with properly typed values. |

#### Remarks
This method is essential for converting user input or external data sources to the typed format required by other methods in this class. It performs the following conversions:
- **String properties**: Returned as-is
- **Numeric properties**: Parsed to `double`
- **DateTime properties**: Parsed to `DateTime`
- **Boolean properties**: Handles multiple formats (`true`/`false`, `1`/`0`, `yes`/`no`)
- **Image properties**: Returned as string representation

The method uses cached property definitions for efficient lookups and handles:
- Unknown property names (skipped)
- Invalid values (skipped)
- Null or empty values (skipped)

This method is particularly useful when:
- Accepting user input from console or UI
- Reading property updates from configuration files
- Importing property values from external systems

#### Example
```csharp
// Convert string-based properties to typed dictionary
Dictionary<string, string> userInput = new Dictionary<string, string>()
{
    { "Title", "My Part" },
    { "Part Number", "12345" },
    { "Revision", "A" },
    { "Date Created", "2024-01-15" },
    { "Is Released", "true" },
    { "Cost", "125.50" }
};

Dictionary<PropDef, object> typedProps = manageProps.ConvertToPropDictionary(userInput);

// Now use typedProps with UpdateFileProperties or SyncProperties
File updatedFile = manageProps.UpdateFileProperties(
    file,
    "Bulk property update",
    true,
    typedProps,
    out writeResults,
    out cloakedEntityClasses
);
```

---

## Internal Architecture

The public methods delegate to a set of focused private helper methods. This section documents these helpers to aid debugging and maintenance.

### Provider Resolution

| Method | Purpose |
|--------|---------|
| `ResolveFileProvider(long fileId)` | Resolves the content source provider for a single file by reading its `Provider` property and matching it to the server configuration. Returns `null` if no provider is found. |
| `ResolveFileProviders(long[] fileIds)` | Batch-resolves providers for multiple files in a single API call. Internally caches resolved providers by name so files sharing the same provider (e.g., multiple `.ipt` files) only trigger one lookup. |
| `ResolveProviderByName(string providerName)` | Matches a provider display name to a `CtntSrc` in the server configuration. Falls back to the `IFilter` provider if no match is found. |

### Property Classification

| Method | Purpose |
|--------|---------|
| `ClassifyProperties(...)` | Splits a property value dictionary into three categories — **writeOnly**, **readWrite**, and **unmapped** — based on the file's content source provider. Used by both `UpdateFileProperties` overloads to determine the DB update set and sync override set. |
| `GetProviderMappingDirections(PropDefInfo, CtntSrc, out bool, out bool)` | Scans all mapping entries for a property/provider combination and sets independent `hasRead`/`hasWrite` flags. A ReadAndWrite property has two separate entries in `MapDirectionArray` (one `Read`, one `Write`), so both flags are scanned independently. |

### Database Operations

| Method | Purpose |
|--------|---------|
| `SetDbPropValues(ref File, string, Dictionary)` | Sets property values in the Vault database **without checking in** the file. Checks out the file if not already checked out, calls `DocumentService.UpdateFileProperties`, and returns with the file still checked out. This avoids triggering Read mappings that would overwrite DB values with old file content. Used by `UpdateFileProperties` before `SyncProperties`. |

### Property Building & Conversion

| Method | Purpose |
|--------|---------|
| `BuildPropInstParamArray(...)` | Constructs a `PropInstParamArray` from a `Dictionary<PropDef, object>` for use with `DocumentService.UpdateFileProperties`. Used by `SetDbPropValues`, `UpdateDbPropValues`, and `UpdatePropertiesBatch`. |
| `BuildOverrideMonikerMap(...)` | Builds a dictionary mapping content source monikers to their `PropDef` for override values. Only monikers that match the file's writable property definitions are included. |
| `ApplyOverridesToWriteProps(...)` | Applies override property values to the `PropWriteReq` array and updates BOM component attributes if the overridden property is part of the BOM. Applies `dateOnly`/`boolAsInt` conversion via `ApplyTypeConversion`. |
| `ApplyTypeConversion(object, DataType)` | Central conversion method that applies `dateOnly` and `boolAsInt` options to a property value. Used for both component property values (via `ConvertPropertyValue`) and override values (via `ApplyOverridesToWriteProps`). |
| `ConvertPropertyValue(CompProp, DataType)` | Extracts the value from a `CompProp` and delegates to `ApplyTypeConversion`. |
| `ConvertStringToPropertyType(string, DataType)` | Parses a string value into the appropriate CLR type (`DateTime`, `double`, `bool`, etc.) based on the property's `DataType`. |

### File Operations

| Method | Purpose |
|--------|---------|
| `EnsureFileCheckedOut(...)` | Ensures a file is checked out by the current user. Returns `false` if checked out by another user. Handles the three cases: not checked out, checked out by current user, checked out by someone else. |
| `GetFileAssociations(...)` | Retrieves file associations (parent-child references) to preserve them during check-in operations. |

### Call Graph

The following diagram shows how public methods delegate to private helpers:

```
UpdateFileProperties (single file)
├── ResolveFileProvider
│   └── ResolveProviderByName
├── ClassifyProperties
│   └── GetProviderMappingDirections
├── SetDbPropValues (Write-only + Unmapped, no check-in)
│   ├── EnsureFileCheckedOut
│   └── BuildPropInstParamArray
└── SyncProperties (Write-only + ReadAndWrite overrides, or compliance-only)
    ├── EnsureFileCheckedOut (file already checked out from SetDbPropValues)
    ├── ConvertPropertyValue
    │   └── ApplyTypeConversion
    ├── BuildOverrideMonikerMap
    ├── ApplyOverridesToWriteProps
    │   └── ApplyTypeConversion
    ├── GetFileAssociations
    └── CheckinUploadedFile (single check-in for all changes)

UpdateFileProperties (batch)
├── ResolveFileProviders
│   └── ResolveProviderByName
├── ClassifyProperties (per file)
│   └── GetProviderMappingDirections
└── Per file:
    ├── SetDbPropValues (if DB updates needed)
    │   ├── EnsureFileCheckedOut
    │   └── BuildPropInstParamArray
    └── SyncProperties (overrides or compliance)
        └── (same as above)

UpdateDbPropValues (standalone, with check-in)
├── EnsureFileCheckedOut
├── BuildPropInstParamArray
├── CopyFile
├── GetFileAssociations
└── CheckinUploadedFile

UpdatePropertiesBatch (standalone, with check-in)
├── EnsureFileCheckedOut (per file)
├── BuildPropInstParamArray (per file)
├── UpdateFileProperties (batched API call)
├── CopyFile (per file)
├── GetFileAssociations (per file)
└── CheckinUploadedFile (per file)
```

---

## Performance Considerations

### Caching Strategy
The `ManageProperties` class implements an aggressive caching strategy to minimize API calls:

- **Property Definitions**: Cached during initialization for FILE and ITEM entity classes
- **Property Definition Infos**: Cached to avoid repeated calls to `GetPropertyDefinitionInfosByEntityClassId`
- **PropDefInfo Lookup by Id**: Pre-built dictionary (`filePropDefInfoById`) for O(1) lookups during property classification and moniker mapping, avoiding repeated `.ToDictionary()` calls
- **Server Configuration**: Cached to avoid repeated calls to `GetServerConfiguration`
- **Display Name Mappings**: Pre-computed mapping of display names to system names
- **Provider Name Cache**: During batch provider resolution (`ResolveFileProviders`), resolved providers are cached by name so files sharing the same provider type avoid redundant lookups

### API Call Reduction
Compared to naive implementations, this class reduces API calls by approximately **50-70%** per operation by:
- Reusing cached data across multiple operations
- Batch retrieving property definitions during initialization
- Using direct dictionary lookups instead of LINQ queries where possible
- Employing `HashSet` for O(1) moniker matching

### Method Selection Guide

**Choose the right method for your scenario:**

| Method | Use When | Check-in? |
|--------|----------|-----------|
| `UpdateFileProperties` | You have a mix of mapped and unmapped properties | Yes (via sync) |
| `SyncProperties` | You need to resolve compliance failures or write overrides into the file | Yes |
| `UpdateDbPropValues` | You only have unmapped properties (single file, standalone) | Yes |
| `UpdatePropertiesBatch` | You only have unmapped properties (multiple files, standalone) | Yes |

**Important:** `UpdateDbPropValues` and `UpdatePropertiesBatch` perform a full check-in. If ReadAndWrite-mapped properties exist on the file, the check-in will trigger Read mappings that read values from the (unchanged) physical file back into the DB, potentially overwriting values set by other means. Always use `UpdateFileProperties` when mapped properties are involved.
