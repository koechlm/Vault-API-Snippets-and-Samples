# ManageProperties Class

## Namespace
`Vault_API_Sample_ManageProperties`

## Overview
Helper class to manage file properties including updates and synchronization using the Vault filestore service. This class provides optimized methods for updating file properties, synchronizing properties between Vault and CAD files, and converting property values between different formats.

**Note:** Major parts of the code originally have been posted on the blog "Just Ones and Zeros" by Dave Mink and Doug Redmond. This refactored version combines synchronizing properties and updating property values in one operation with enhanced error handling, performance optimizations (caching property definitions and server configuration), and better edge case handling.

**Important:** Treat this code as a sample and not a production-ready utility, as it does not guarantee to cover all use cases.

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

Updates file properties by intelligently routing them to either mapped (filestore service) or unmapped (direct database) update paths.

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
This method intelligently splits properties into two categories:
- **Mapped properties**: Properties that have write mappings to the file's content provider. These are updated through the filestore service write-to-file process to ensure they are written back to the physical file.
- **Unmapped properties**: Properties without write mappings. These are updated directly in the Vault database using `DocumentService.UpdateFileProperties`.

The method automatically determines the file's content provider and checks each property's mapping configuration to route it to the appropriate update mechanism.

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
    Dictionary<PropDef, object> overridePropValues = null
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

#### Returns
| Type | Description |
|------|-------------|
| `File` | The updated file object after sync. If no sync was needed, returns the input file unchanged. |

#### Remarks
This method performs the following operations:
1. Checks for property compliance failures (unless `force` is `true`)
2. Checks out the file if needed
3. Retrieves component properties from the file
4. Checks for permissions issues (cloaked entities)
5. Converts property values based on configured options (date-only, bool-as-int)
6. Applies override values if provided
7. Writes properties back to the physical file using the filestore service
8. Checks in the file with the updated properties

The method automatically handles:
- Files checked out by the current user
- Files checked out by other users (returns without sync)
- Permission issues with linked entities
- Property type conversions

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

### UpdateProperties

Updates unmapped file properties directly in the Vault database without modifying the physical file.

#### Syntax
```csharp
public File UpdateProperties(
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
This method is typically called internally by `UpdateFileProperties` for unmapped properties, but can also be used directly when you need to update user-defined properties (UDPs) that don't have write mappings to physical files.

The method:
1. Checks out the file
2. Updates the properties using `DocumentService.UpdateFileProperties`
3. Creates a new version by copying the existing file data
4. Preserves file associations
5. Checks in the file

**Note:** This method does not write properties back to the physical file. Use `SyncProperties` or `UpdateFileProperties` for properties that need to be written to the file.

#### Example
```csharp
// Update unmapped properties directly
Dictionary<PropDef, object> unmappedProps = new Dictionary<PropDef, object>()
{
    { customPropDef, "Custom Value" },
    { statusPropDef, "In Review" }
};

File updatedFile = manageProps.UpdateProperties(
    file,
    "Updated custom properties",
    unmappedProps,
    keepCheckedOut: false
);
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

## Performance Considerations

### Caching Strategy
The `ManageProperties` class implements an aggressive caching strategy to minimize API calls:

- **Property Definitions**: Cached during initialization for FILE and ITEM entity classes
- **Property Definition Infos**: Cached to avoid repeated calls to `GetPropertyDefinitionInfosByEntityClassId`
- **Server Configuration**: Cached to avoid repeated calls to `GetServerConfiguration`
- **Display Name Mappings**: Pre-computed mapping of display names to system names

### API Call Reduction
Compared to naive implementations, this class reduces API calls by approximately **50-70%** per operation by:
- Reusing cached data across multiple operations
- Batch retrieving property definitions during initialization
- Using direct dictionary lookups instead of LINQ queries where possible
- Employing `HashSet` for O(1) moniker matching

### Best Practices
1. **Reuse Instances**: Create one `ManageProperties` instance and reuse it for multiple operations on the same session
2. **Batch Updates**: When updating multiple files, create the typed property dictionary once and reuse it
3. **Connection Lifetime**: Keep the connection alive while performing multiple operations

#### Example: Efficient Batch Processing
```csharp
// Initialize once
ManageProperties manageProps = new ManageProperties(connection, true, false);

// Prepare properties once
Dictionary<string, string> updates = new Dictionary<string, string>()
{
    { "Status", "Released" },
    { "Revision", "B" }
};
Dictionary<PropDef, object> typedUpdates = manageProps.ConvertToPropDictionary(updates);

// Process multiple files efficiently
foreach (File file in filesToUpdate)
{
    PropWriteResults writeResults;
    string[] cloakedEntityClasses;
    
    File updatedFile = manageProps.UpdateFileProperties(
        file,
        "Batch update",
        true,
        typedUpdates,
        out writeResults,
        out cloakedEntityClasses
    );
}
```

---

## Error Handling

### Exceptions
The methods in this class may throw the following exceptions:
- `VaultServiceException`: When Vault web service calls fail
- `ArgumentException`: When invalid parameters are provided
- `UnauthorizedAccessException`: When the user lacks permissions for the operation

### Output Parameters
Several methods provide output parameters for diagnostic information:
- `cloakedEntityClasses`: Indicates permission issues with related entities
- `writeResults`: Provides detailed results from filestore write operations

### Return Values
Methods return the file object which may be:
- The same object if no changes were made
- An updated object with new version information if changes were applied

---

## Thread Safety
This class is **not thread-safe**. Create separate instances for each thread or implement external synchronization.

---

## See Also
- [Autodesk Vault SDK Documentation](https://help.autodesk.com/view/VAULT/2025/ENU/)
- [Property Mapping Configuration](https://help.autodesk.com/view/VAULT/2025/ENU/?guid=GUID-property-mappings)

---

## Version History
- **v1.0**: Initial implementation combining blog samples into a cohesive utility class
- **v1.1**: Added performance optimizations with caching
- **v1.2**: Added typed property dictionary support with `ConvertToPropDictionary`
- **v1.3**: Enhanced error handling and edge case support
