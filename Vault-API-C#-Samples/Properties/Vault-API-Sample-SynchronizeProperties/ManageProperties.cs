using Autodesk.Connectivity.WebServices;
using Autodesk.Connectivity.WebServicesTools;
using Autodesk.DataManagement.Client.Framework.Vault.Currency.Connections;
using Autodesk.DataManagement.Client.Framework.Vault.Currency.Properties;
using System;
using System.Collections.Generic;
using System.Linq;

// Vault SDK
using ACW = Autodesk.Connectivity.WebServices;
using ACWT = Autodesk.Connectivity.WebServicesTools;
using VDF = Autodesk.DataManagement.Client.Framework;

namespace Vault_API_Sample_ManageProperties
{

    /// <summary>
    /// Helper class to manage file properties including updates and synchronization using filestore service.
    /// Major parts of the code originally have been posted on the blog Just Ones and Zeros, written by Dave Mink and Doug Redmond.
    /// This refactored version combines synchronizing properties and updating property values in one go. Blog samples focused on 
    /// demonstrating the principles. This version is closer to production quality code with better error handling, optimizations 
    /// like caching property definitions and server configuration, and handling edge cases. Anyway treat this code as a sample and not a production-ready utility, 
    /// as it does not guarantee to cover all use cases.
    /// </summary>
    public class ManageProperties
    {
        // property cache used to find date and bool types; Vault options allow to return date without time and bool as 0/1 instead of true/false
        private Dictionary<string, Dictionary<long, ACW.PropDef>> propDefsByEntityClassAndId = new Dictionary<string, Dictionary<long, ACW.PropDef>>();
        // cache file property display name to system name mapping to apply override providerPropInst based on display names;
        private Dictionary<string, string> filePropDispToSysNames = new Dictionary<string, string>();
        // cache property definition infos to avoid repeated API calls
        private Dictionary<string, IEnumerable<PropDefInfo>> propDefInfosByEntityClass = new Dictionary<string, IEnumerable<PropDefInfo>>();
        // cache PropDefInfo lookup by Id for FILE entity class
        private Dictionary<long, PropDefInfo> filePropDefInfoById;
        // cache server configuration
        private ServerCfg serverConfig = null;
        // options to convert providerPropInst as configured in the Vault behaviors
        private bool dateOnly = true;
        private bool boolAsInt = false;

        private Connection connection;
        private WebServiceManager webSrvMgr;

        #region public methods

        /// <summary>
        /// Constructor, initializing the class leveraging current connection and Vault behavior settings for property value conversion.
        /// </summary>
        /// <param name="dateOnly"></param>
        /// <param name="boolAsInt"></param>
        public ManageProperties(Connection connection, bool dateOnly = true, bool boolAsInt = false)
        {
            this.dateOnly = dateOnly;
            this.boolAsInt = boolAsInt;
            this.connection = connection;
            this.webSrvMgr = connection.WebServiceManager;

            // prime the property definition cache with all prop defs for files and items
            foreach (string entityClass in new string[] { "FILE", "ITEM" })
            {
                propDefsByEntityClassAndId.Add(
                    entityClass,
                    webSrvMgr.PropertyService.GetPropertyDefinitionsByEntityClassId(entityClass).ToDictionary(pd => pd.Id)
                    );
                // Cache property definition infos for reuse
                propDefInfosByEntityClass.Add(
                    entityClass,
                    webSrvMgr.PropertyService.GetPropertyDefinitionInfosByEntityClassId(entityClass, null)
                    );
            }

            // Build PropDefInfo lookup by Id for FILE entity class
            filePropDefInfoById = propDefInfosByEntityClass["FILE"].ToDictionary(pdi => pdi.PropDef.Id);

            // cache file property system names to map display names
            ACW.PropDef[] filePropDefs = webSrvMgr.PropertyService.GetPropertyDefinitionsByEntityClassId("FILE");
            foreach (ACW.PropDef propDef in filePropDefs)
            {
                filePropDispToSysNames[propDef.DispName] = propDef.SysName;
            }

            // Cache server configuration
            serverConfig = webSrvMgr.AdminService.GetServerConfiguration();
        }

        /// <summary>
        /// Updates file properties for multiple files by intelligently routing them to either writeToFile (filestore service) or updateDb (direct database) update paths.
        /// This overload is for single file update.
        /// </summary>
        /// <param name="file"></param>
        /// <param name="comment"></param>
        /// <param name="allowSync"></param>
        /// <param name="newPropValues"></param>
        /// <param name="writeResults"></param>
        /// <param name="cloakedEntityClasses"></param>
        /// <param name="force"></param>
        /// <returns></returns>
        public ACW.File UpdateFileProperties(ACW.File file, string comment, bool allowSync, Dictionary<ACW.PropDef, object> newPropValues,
            out ACW.PropWriteResults writeResults, out string[] cloakedEntityClasses, bool force = false)
        {
            // Resolve the provider for this file
            CtntSrc provider = ResolveFileProvider(file.Id);

            // Classify into three categories based on mapping direction
            ClassifyProperties(newPropValues, provider,
                out Dictionary<ACW.PropDef, object> writeOnlyPropValues,
                out Dictionary<ACW.PropDef, object> readWritePropValues,
                out Dictionary<ACW.PropDef, object> unmappedPropValues);

            // Build the DB update set: Write-only + Unmapped need explicit DB update
            Dictionary<ACW.PropDef, object> dbUpdateProps = new Dictionary<ACW.PropDef, object>();
            foreach (var kvp in writeOnlyPropValues) dbUpdateProps[kvp.Key] = kvp.Value;
            foreach (var kvp in unmappedPropValues) dbUpdateProps[kvp.Key] = kvp.Value;

            // Build the sync override set: Write-only + ReadAndWrite need file write via sync
            Dictionary<ACW.PropDef, object> syncOverrideProps = new Dictionary<ACW.PropDef, object>();
            foreach (var kvp in writeOnlyPropValues) syncOverrideProps[kvp.Key] = kvp.Value;
            foreach (var kvp in readWritePropValues) syncOverrideProps[kvp.Key] = kvp.Value;

            // Execute: set DB values without check-in first, then sync handles file write + check-in.
            // DB update must not check in, because check-in triggers Read mappings that would
            // overwrite ReadAndWrite DB values with old file content before sync writes the new values.
            if (dbUpdateProps.Count > 0)
            {
                SetDbPropValues(ref file, comment, dbUpdateProps);
            }
            // Sync always runs: with overrides for mapped properties, without overrides to resolve compliance
            file = SyncProperties(file, comment, allowSync, out writeResults, out cloakedEntityClasses, force,
                overridePropValues: syncOverrideProps.Count > 0 ? syncOverrideProps : null);

            return file;
        }


        /// <summary>
        /// Updates file properties for multiple files by intelligently routing them to either writeToFile (filestore service) or updateDb (direct database) update paths.
        /// This overload provides batch processing capabilities for improved performance when updating multiple files.
        /// </summary>
        /// <param name="files">Array of files to update properties on</param>
        /// <param name="comment">Comment to use for the new versions</param>
        /// <param name="allowSync">If true, allows the filestore to retrieve files from another filestore if not available locally</param>
        /// <param name="propValuesByFile">Array of property value dictionaries, one for each file (must match the length of files array)</param>
        /// <param name="writeResultsByFile">Output array of write results for writeToFile properties, one per file</param>
        /// <param name="cloakedEntityClassesByFile">Output array of cloaked entity classes, one per file</param>
        /// <param name="force">If true, forces the sync operation even if no compliance failures exist</param>
        /// <returns>Array of updated file objects</returns>
        public ACW.File[] UpdateFileProperties(ACW.File[] files, string comment, bool allowSync,
            Dictionary<ACW.PropDef, object>[] propValuesByFile,
            out ACW.PropWriteResults[] writeResultsByFile,
            out string[][] cloakedEntityClassesByFile,
            bool force = false)
        {
            if (files == null || files.Length == 0)
                throw new ArgumentException("Files array cannot be null or empty", nameof(files));

            if (propValuesByFile == null || propValuesByFile.Length != files.Length)
                throw new ArgumentException("Property values array must match the length of files array", nameof(propValuesByFile));

            // Initialize output arrays
            writeResultsByFile = new ACW.PropWriteResults[files.Length];
            cloakedEntityClassesByFile = new string[files.Length][];

            for (int i = 0; i < files.Length; i++)
            {
                writeResultsByFile[i] = new ACW.PropWriteResults();
            }

            // Batch-resolve providers for all files
            Dictionary<long, CtntSrc> providersByFileId = ResolveFileProviders(files.Select(f => f.Id).ToArray());

            // Classify each file's properties into the three categories,
            // then derive the two execution sets per file: dbUpdateProps and syncOverrideProps
            Dictionary<ACW.PropDef, object>[] dbUpdatePropsByFile = new Dictionary<ACW.PropDef, object>[files.Length];
            Dictionary<ACW.PropDef, object>[] syncOverridePropsByFile = new Dictionary<ACW.PropDef, object>[files.Length];

            for (int fileIndex = 0; fileIndex < files.Length; fileIndex++)
            {
                providersByFileId.TryGetValue(files[fileIndex].Id, out CtntSrc provider);

                ClassifyProperties(propValuesByFile[fileIndex], provider,
                    out Dictionary<ACW.PropDef, object> writeOnly,
                    out Dictionary<ACW.PropDef, object> readWrite,
                    out Dictionary<ACW.PropDef, object> unmapped);

                // DB update set: Write-only + Unmapped
                Dictionary<ACW.PropDef, object> dbUpdate = new Dictionary<ACW.PropDef, object>();
                foreach (var kvp in writeOnly) dbUpdate[kvp.Key] = kvp.Value;
                foreach (var kvp in unmapped) dbUpdate[kvp.Key] = kvp.Value;

                // Sync override set: Write-only + ReadAndWrite
                Dictionary<ACW.PropDef, object> syncOverride = new Dictionary<ACW.PropDef, object>();
                foreach (var kvp in writeOnly) syncOverride[kvp.Key] = kvp.Value;
                foreach (var kvp in readWrite) syncOverride[kvp.Key] = kvp.Value;

                dbUpdatePropsByFile[fileIndex] = dbUpdate;
                syncOverridePropsByFile[fileIndex] = syncOverride;
            }

            ACW.File[] resultFiles = new ACW.File[files.Length];

            // Process each file: set DB values without check-in first, then sync handles file write + check-in.
            // DB update must not check in, because check-in triggers Read mappings that would
            // overwrite ReadAndWrite DB values with old file content before sync writes the new values.
            for (int i = 0; i < files.Length; i++)
            {
                ACW.File file = files[i];

                if (dbUpdatePropsByFile[i].Count > 0)
                {
                    SetDbPropValues(ref file, comment, dbUpdatePropsByFile[i]);
                }

                // Sync always runs: with overrides for mapped properties, without overrides to resolve compliance
                file = SyncProperties(file, comment, allowSync,
                    out writeResultsByFile[i],
                    out cloakedEntityClassesByFile[i],
                    force,
                    overridePropValues: syncOverridePropsByFile[i].Count > 0 ? syncOverridePropsByFile[i] : null);

                resultFiles[i] = file;
            }

            return resultFiles;
        }


        /// <summary>
        /// Sync properties of a file, optionally overriding property values.
        /// </summary>
        /// <param name="file">the file you would like to sync</param>
        /// <param name="comment">the comment for the new version (if a property sync was performed)</param>
        /// <param name="allowSync">if the local filestore doesn't have the file, get it from another filestore</param>
        /// <param name="writeResults">see FilestoreService.CopyFile method</param>
        /// <param name="cloakedEntityClasses">if you can't read an entity where properties would come from, its entity class is returned here</param>
        /// <param name="force">skip check for equivalence and always do a sync, creating a new version</param>
        /// <param name="overridePropValues">[Optional] dictionary of property definitions and their override values</param>
        /// <returns>the file returned from the checkin, same as the input if no property sync is done</returns>
        public ACW.File SyncProperties(ACW.File file, string comment, bool allowSync, out ACW.PropWriteResults writeResults,
            out string[] cloakedEntityClasses, bool force = false, Dictionary<ACW.PropDef, object> overridePropValues = null, bool keepCheckedOut = false)
        {
            ACW.ByteArray downloadTicket = null;

            // initialize output parameters to empty results so callers can safely iterate without null checks.
            writeResults = new ACW.PropWriteResults();
            cloakedEntityClasses = null;

            // synchronization is needed if there are compliance failures or if newPropValues are provided;
            if (!force && (overridePropValues == null || overridePropValues.Count == 0))
            {
                ACW.PropCompFail[] complianceFailures = webSrvMgr.PropertyService.GetPropertyComplianceFailuresByEntityIds(
                    "FILE", new long[] { file.Id }, /*filterPending*/true
                    );
                if (complianceFailures == null
                    || complianceFailures.Sum(cf => (cf.PropEquivFailArray != null ? cf.PropEquivFailArray.Length : 0)) == 0
                    )
                {
                    // get the latest file info to return any changes that might have happened since the file was checked out, without doing sync.
                    EnsureFileCheckedOut(webSrvMgr, ref file, comment, out downloadTicket);
                    // keep file associations as they are
                    ACW.FileAssocParam[] associations = GetFileAssociations(webSrvMgr, file);

                    // checkin and exit without doing sync, as there are no compliance failures and no override values to apply
                    return webSrvMgr.DocumentService.CheckinUploadedFile(
                        file.MasterId,
                        comment, /*keepCheckedOut*/keepCheckedOut, /*lastWrite*/DateTime.Now,
                        associations,
                        /*bom*/null, /*copyBom*/true,
                        file.Name, file.FileClass, file.Hidden,
                        null /*no file content change, so no upload ticket*/
                        );
                }
            }

            if (!EnsureFileCheckedOut(webSrvMgr, ref file, comment, out downloadTicket))
            {
                return file;
            }

            try
            {
                // get component properties.                
                // NOTE: a null component UID means to get write-back properties for root component in the file.
                // WARNING: we can't sync component level properties without CAD!
                ACW.CompProp[] compProps = webSrvMgr.DocumentService.GetComponentProperties(file.Id, /*compUID*/null);
                if (compProps == null || compProps.Length == 0)
                {
                    return webSrvMgr.DocumentService.UndoCheckoutFile(file.MasterId, out downloadTicket);
                }

                // return cloaked entity classes
                // NOTE: a propDefId of -1 indicates get couldn't get properties from an inaccessible entity.
                cloakedEntityClasses = compProps.Where(p => p.PropDefId < 0).Select(p => p.EntClassId).ToArray();
                if (cloakedEntityClasses != null && cloakedEntityClasses.Length > 0)
                {
                    return webSrvMgr.DocumentService.UndoCheckoutFile(file.MasterId, out downloadTicket);
                }

                compProps = compProps.Where(p => p.PropDefId > 0).ToArray();

                if (compProps == null || compProps.Length == 0)
                {
                    return webSrvMgr.DocumentService.UndoCheckoutFile(file.MasterId, out downloadTicket);
                }

                // convert CompProp array to PropWriteReq array.
                ACW.PropWriteReq[] writeProps = compProps.Select(
                    p => new ACW.PropWriteReq()
                    {
                        Moniker = p.Moniker,
                        CanCreate = p.CreateNew,
                        Val = ConvertPropertyValue(p, propDefsByEntityClassAndId[p.EntClassId][p.PropDefId].Typ)
                    }
                    ).ToArray();

                // retrieve the file property definitions to check for write mappings.
                CtntSrcPropDef[] fileProps = webSrvMgr.FilestoreService.GetContentSourcePropertyDefinitions(
                downloadTicket.Bytes, true).Where(p => p.MapDirection == AllowedMappingDirection.Write || p.MapDirection == AllowedMappingDirection.ReadAndWrite).ToArray();

                // Build moniker to property definition mapping for override values
                Dictionary<string, ACW.PropDef> propMonikers = BuildOverrideMonikerMap(overridePropValues, fileProps);

                // get BOM data to include in the write request so that writeToFile properties that are also part of the BOM get updated correctly;
                BOM currentBOM = webSrvMgr.DocumentService.GetBOMByFileId(file.Id);

                // apply override values using moniker mappings
                ApplyOverridesToWriteProps(writeProps, propMonikers, overridePropValues, currentBOM);

                ACW.PropWriteRequests writePropsReq = new ACW.PropWriteRequests();
                writePropsReq.Requests = writeProps;
                writePropsReq.Bom = currentBOM;
                byte[] uploadTicket = webSrvMgr.FilestoreService.CopyFile(
                    downloadTicket.Bytes, null, allowSync, writePropsReq,
                    out writeResults
                    );

                ACW.FileAssocParam[] associations = GetFileAssociations(webSrvMgr, file);

                file = webSrvMgr.DocumentService.CheckinUploadedFile(
                    file.MasterId,
                    comment, /*keepCheckedOut*/keepCheckedOut, /*lastWrite*/DateTime.Now,
                    associations,
                    /*bom*/currentBOM, /*copyBom*/false,
                    file.Name, file.FileClass, file.Hidden,
                    new ACW.ByteArray() { Bytes = uploadTicket }
                    );
            }
            finally
            {
                if (webSrvMgr.DocumentService.GetLatestFileByMasterId(file.MasterId).CheckedOut)
                    file = webSrvMgr.DocumentService.UndoCheckoutFile(file.MasterId, out downloadTicket);
            }

            return file;
        }


        /// <summary>
        /// Update UDPs of updateDb file properties
        /// </summary>
        /// <param name="file">File Iteration</param>
        /// <param name="comment">comment</param>
        /// <param name="newPropValues">Dictionary of property definitions and their new values</param>
        /// <returns>Updated file</returns>
        public ACW.File UpdateDbPropValues(ACW.File file, string comment, Dictionary<ACW.PropDef, object> newPropValues, bool keepCheckedOut = false)
        {
            ACW.ByteArray downloadTicket = null;

            ACW.File currentFile = file;
            ACW.File updatedFile = null;

            if (!EnsureFileCheckedOut(webSrvMgr, ref currentFile, comment, out downloadTicket))
            {
                return file;
            }

            PropInstParamArray propInstParamArray = BuildPropInstParamArray(newPropValues);

            webSrvMgr.DocumentService.UpdateFileProperties(new long[] { currentFile.MasterId }, new PropInstParamArray[] { propInstParamArray });

            ACW.PropWriteRequests writePropsReq = new ACW.PropWriteRequests();
            writePropsReq.Requests = null;
            writePropsReq.Bom = null;
            byte[] uploadTicket = webSrvMgr.FilestoreService.CopyFile(
                downloadTicket.Bytes, null, allowSync: true, writePropsReq,
                out _
                );

            ACW.FileAssocParam[] associations = GetFileAssociations(webSrvMgr, file);

            updatedFile = webSrvMgr.DocumentService.CheckinUploadedFile(
                file.MasterId,
                comment, keepCheckedOut, /*lastWrite*/DateTime.Now,
                associations,
                /*bom*/null, /*copyBom*/true,
                file.Name, file.FileClass, file.Hidden,
                new ACW.ByteArray() { Bytes = uploadTicket }
                );

            return updatedFile;
        }


        /// <summary>
        /// Update UDPs of updateDb file properties for multiple files in a single batch operation.
        /// This method leverages the bulk UpdateFileProperties API for better performance when updating multiple files.
        /// </summary>
        /// <param name="files">Array of files to update</param>
        /// <param name="comment">Comment for the check-in operation</param>
        /// <param name="propValuesByFile">Array of property value dictionaries, one for each file (must match the length of files array)</param>
        /// <param name="keepCheckedOut">If true, keeps files checked out after updating</param>
        /// <returns>Array of updated files</returns>
        public ACW.File[] UpdatePropertiesBatch(ACW.File[] files, string comment, Dictionary<ACW.PropDef, object>[] propValuesByFile, bool keepCheckedOut = false)
        {
            if (files == null || files.Length == 0)
                throw new ArgumentException("Files array cannot be null or empty", nameof(files));

            if (propValuesByFile == null || propValuesByFile.Length != files.Length)
                throw new ArgumentException("Property values array must match the length of files array", nameof(propValuesByFile));

            List<ACW.File> updatedFiles = new List<ACW.File>();
            List<long> masterIdsToUpdate = new List<long>();
            List<PropInstParamArray> propInstParamArrays = new List<PropInstParamArray>();
            Dictionary<long, ACW.File> filesByMasterId = new Dictionary<long, ACW.File>();
            Dictionary<long, ACW.ByteArray> downloadTicketsByMasterId = new Dictionary<long, ACW.ByteArray>();

            // Check out files and prepare property arrays
            for (int i = 0; i < files.Length; i++)
            {
                ACW.File file = files[i];
                ACW.ByteArray downloadTicket = null;

                if (!EnsureFileCheckedOut(webSrvMgr, ref file, comment, out downloadTicket))
                {
                    Console.WriteLine($"Skipping file {file.Name} - checked out by another user");
                    continue;
                }

                PropInstParamArray propInstParamArray = BuildPropInstParamArray(propValuesByFile[i]);

                masterIdsToUpdate.Add(file.MasterId);
                propInstParamArrays.Add(propInstParamArray);
                filesByMasterId[file.MasterId] = file;
                downloadTicketsByMasterId[file.MasterId] = downloadTicket;
            }

            if (masterIdsToUpdate.Count == 0)
            {
                return files;
            }

            try
            {
                webSrvMgr.DocumentService.UpdateFileProperties(masterIdsToUpdate.ToArray(), propInstParamArrays.ToArray());

                foreach (long masterId in masterIdsToUpdate)
                {
                    ACW.File file = filesByMasterId[masterId];
                    ACW.ByteArray downloadTicket = downloadTicketsByMasterId[masterId];

                    ACW.PropWriteRequests writePropsReq = new ACW.PropWriteRequests
                    {
                        Requests = null,
                        Bom = null
                    };

                    byte[] uploadTicket = webSrvMgr.FilestoreService.CopyFile(
                        downloadTicket.Bytes, null, allowSync: true, writePropsReq,
                        out _
                    );

                    ACW.FileAssocParam[] associations = GetFileAssociations(webSrvMgr, file);

                    ACW.File updatedFile = webSrvMgr.DocumentService.CheckinUploadedFile(
                        masterId,
                        comment, keepCheckedOut, DateTime.Now,
                        associations,
                        null, true,
                        file.Name, file.FileClass, file.Hidden,
                        new ACW.ByteArray() { Bytes = uploadTicket }
                    );

                    updatedFiles.Add(updatedFile);
                }
            }
            catch (Exception)
            {
                foreach (long masterId in masterIdsToUpdate)
                {
                    ACW.File file = filesByMasterId[masterId];
                    if (file.CheckedOut)
                    {
                        webSrvMgr.DocumentService.UndoCheckoutFile(masterId, out _);
                    }
                }
                throw;
            }

            return updatedFiles.ToArray();
        }
        
        
        /// <summary>
        /// Converts a dictionary of property display names and string values to properly typed property definitions and values.
        /// </summary>
        /// <param name="keyValuePairs">Key=Property DisplayName, Value=Property Value as string</param>
        /// <returns>Dictionary of FILE property definition and typed value object</returns>
        public Dictionary<PropDef, object> ConvertToPropDictionary(Dictionary<string, string> keyValuePairs)
        {
            Dictionary<ACW.PropDef, object> propDictionary = new Dictionary<ACW.PropDef, object>();

            Dictionary<long, ACW.PropDef> filePropDefs = propDefsByEntityClassAndId["FILE"];

            foreach (var kvp in keyValuePairs)
            {
                string displayName = kvp.Key;
                string stringValue = kvp.Value;

                if (filePropDispToSysNames.TryGetValue(displayName, out string sysName))
                {
                    ACW.PropDef propDef = filePropDefs.Values.FirstOrDefault(pd => pd.SysName == sysName);

                    if (propDef != null && !string.IsNullOrWhiteSpace(stringValue))
                    {
                        object typedValue = ConvertStringToPropertyType(stringValue, propDef.Typ);

                        if (typedValue != null)
                        {
                            propDictionary[propDef] = typedValue;
                        }
                    }
                }
            }

            return propDictionary;
        }

        #endregion public methods

        #region private helper methods

        /// <summary>
        /// Ensures the file is checked out by the current user. If already checked out by another user, returns false.
        /// If already checked out by current user or successfully checks out, returns true.
        /// </summary>
        private bool EnsureFileCheckedOut(ACWT.WebServiceManager webSrvMgr, ref ACW.File file, string comment, out ACW.ByteArray downloadTicket)
        {
            downloadTicket = null;

            if (file.CheckedOut == true && file.CkOutUserId != webSrvMgr.AuthService.Session.User.Id)
            {
                return false;
            }

            if (file.CheckedOut == true && file.CkOutUserId == webSrvMgr.AuthService.Session.User.Id)
            {
                downloadTicket = webSrvMgr.DocumentService.GetDownloadTicketsByFileIds(new long[] { file.Id }).FirstOrDefault();
                return true;
            }

            if (file.CheckedOut == false)
            {
                file = webSrvMgr.DocumentService.CheckoutFile(
                    file.Id, ACW.CheckoutFileOptions.Master,
                    /*machine*/Environment.MachineName, /*localPath*/string.Empty, comment,
                    out downloadTicket
                    );
                return true;
            }

            return false;
        }

        /// <summary>
        /// Resolves the content source provider for a single file.
        /// Returns null if no provider is found.
        /// </summary>
        private CtntSrc ResolveFileProvider(long fileId)
        {
            IEnumerable<PropDefInfo> propDefInfos = propDefInfosByEntityClass["FILE"];
            PropDefInfo providerPropDefInfo = propDefInfos.FirstOrDefault(p => p.PropDef.SysName == "Provider");
            if (providerPropDefInfo == null)
                return null;

            PropInst providerPropInst = webSrvMgr.PropertyService.GetProperties("FILE", new long[] { fileId }, new long[] { providerPropDefInfo.PropDef.Id }).FirstOrDefault();
            if (providerPropInst == null)
                return null;

            return ResolveProviderByName((string)providerPropInst.Val);
        }

        /// <summary>
        /// Batch-resolves the content source providers for multiple files in a single API call.
        /// Returns a dictionary mapping file Id to its CtntSrc provider (entries omitted for files without a provider).
        /// </summary>
        private Dictionary<long, CtntSrc> ResolveFileProviders(long[] fileIds)
        {
            Dictionary<long, CtntSrc> result = new Dictionary<long, CtntSrc>();

            IEnumerable<PropDefInfo> propDefInfos = propDefInfosByEntityClass["FILE"];
            PropDefInfo providerPropDefInfo = propDefInfos.FirstOrDefault(p => p.PropDef.SysName == "Provider");
            if (providerPropDefInfo == null)
                return result;

            PropInst[] providerPropInsts = webSrvMgr.PropertyService.GetProperties("FILE", fileIds, new long[] { providerPropDefInfo.PropDef.Id });
            if (providerPropInsts == null)
                return result;

            // Cache resolved providers by name to avoid repeated lookups
            Dictionary<string, CtntSrc> providerCache = new Dictionary<string, CtntSrc>();

            foreach (PropInst propInst in providerPropInsts)
            {
                string providerName = (string)propInst.Val;
                if (providerName == null)
                    continue;

                if (!providerCache.TryGetValue(providerName, out CtntSrc provider))
                {
                    provider = ResolveProviderByName(providerName);
                    providerCache[providerName] = provider;
                }

                if (provider != null)
                {
                    result[propInst.EntityId] = provider;
                }
            }

            return result;
        }

        /// <summary>
        /// Resolves a content source provider by display name, falling back to IFilter.
        /// </summary>
        private CtntSrc ResolveProviderByName(string providerName)
        {
            CtntSrc provider = serverConfig.CtntSrcArray.FirstOrDefault(source => source.DispName == providerName);
            if (provider == null)
                provider = serverConfig.CtntSrcArray.FirstOrDefault(source => source.SysName == "IFilter");
            return provider;
        }

        /// <summary>
        /// Classifies property values into three categories based on mapping direction to the file's content source provider.
        /// 
        /// Classification rules:
        /// - Write-only (Write mapping, no Read mapping) ? writeOnlyPropValues
        ///   Needs both DB update (check-in won't read back) and sync (to write into file).
        /// - ReadAndWrite (both Read and Write mappings) ? readWritePropValues
        ///   Needs sync override only; check-in reads the value back to DB automatically.
        /// - Unmapped (no mapping to provider) ? unmappedPropValues
        ///   Needs DB update + check-in only; no file involvement.
        /// </summary>
        private void ClassifyProperties(Dictionary<ACW.PropDef, object> newPropValues, CtntSrc provider,
            out Dictionary<ACW.PropDef, object> writeOnlyPropValues,
            out Dictionary<ACW.PropDef, object> readWritePropValues,
            out Dictionary<ACW.PropDef, object> unmappedPropValues)
        {
            writeOnlyPropValues = new Dictionary<ACW.PropDef, object>();
            readWritePropValues = new Dictionary<ACW.PropDef, object>();
            unmappedPropValues = new Dictionary<ACW.PropDef, object>();

            if (provider == null)
            {
                foreach (var kvp in newPropValues)
                {
                    unmappedPropValues[kvp.Key] = kvp.Value;
                }
                return;
            }

            foreach (var kvp in newPropValues)
            {
                ACW.PropDef propDef = kvp.Key;
                object value = kvp.Value;

                if (filePropDefInfoById.TryGetValue(propDef.Id, out PropDefInfo propDefInfo))
                {
                    bool hasRead;
                    bool hasWrite;
                    GetProviderMappingDirections(propDefInfo, provider, out hasRead, out hasWrite);

                    if (hasWrite && hasRead)
                    {
                        readWritePropValues[propDef] = value;
                    }
                    else if (hasWrite)
                    {
                        writeOnlyPropValues[propDef] = value;
                    }
                    else
                    {
                        // Read-only or unmapped: direct DB update
                        unmappedPropValues[propDef] = value;
                    }
                }
            }
        }

        /// <summary>
        /// Determines the mapping directions of a property for the given content source provider.
        /// A property can have separate Read and Write entries in CtntSrcPropDefArray for the same provider.
        /// </summary>
        private void GetProviderMappingDirections(PropDefInfo propDefInfo, CtntSrc provider, out bool hasRead, out bool hasWrite)
        {
            hasRead = false;
            hasWrite = false;

            if (propDefInfo.EntClassCtntSrcPropCfgArray == null)
                return;

            foreach (EntClassCtntSrcPropCfg contentSource in propDefInfo.EntClassCtntSrcPropCfgArray)
            {
                if (contentSource.EntClassId != "FILE" || contentSource.CtntSrcPropDefArray == null)
                    continue;

                for (int i = 0; i < contentSource.CtntSrcPropDefArray.Length; i++)
                {
                    if (contentSource.CtntSrcPropDefArray[i].CtntSrcId == provider.Id)
                    {
                        if (contentSource.MapDirectionArray[i] == ACW.MappingDirection.Read)
                            hasRead = true;
                        else if (contentSource.MapDirectionArray[i] == ACW.MappingDirection.Write)
                            hasWrite = true;
                    }
                }
            }
        }

        /// <summary>
        /// Builds a PropInstParamArray from a property value dictionary for use with DocumentService.UpdateFileProperties.
        /// </summary>
        private PropInstParamArray BuildPropInstParamArray(Dictionary<ACW.PropDef, object> propValues)
        {
            List<PropInstParam> propInstParams = new List<PropInstParam>();

            foreach (var kvp in propValues)
            {
                propInstParams.Add(new PropInstParam()
                {
                    PropDefId = kvp.Key.Id,
                    Val = kvp.Value
                });
            }

            return new PropInstParamArray { Items = propInstParams.ToArray() };
        }

        /// <summary>
        /// Builds a dictionary mapping content source monikers to their PropDef for override values.
        /// Only monikers that match the file's writable property definitions are included.
        /// </summary>
        private Dictionary<string, ACW.PropDef> BuildOverrideMonikerMap(
            Dictionary<ACW.PropDef, object> overridePropValues, CtntSrcPropDef[] fileProps)
        {
            Dictionary<string, ACW.PropDef> propMonikers = new Dictionary<string, ACW.PropDef>();

            if (overridePropValues == null || overridePropValues.Count == 0)
                return propMonikers;

            HashSet<string> filePropMonikers = new HashSet<string>(fileProps.Select(fp => fp.Moniker));

            foreach (var kvp in overridePropValues)
            {
                ACW.PropDef propDef = kvp.Key;

                if (!filePropDefInfoById.TryGetValue(propDef.Id, out PropDefInfo propDefInfo))
                    continue;

                if (propDefInfo.EntClassCtntSrcPropCfgArray == null)
                    continue;

                foreach (EntClassCtntSrcPropCfg contentSource in propDefInfo.EntClassCtntSrcPropCfgArray)
                {
                    if (contentSource.CtntSrcPropDefArray == null)
                        continue;

                    foreach (CtntSrcPropDef ctntSrcPropDef in contentSource.CtntSrcPropDefArray)
                    {
                        if (filePropMonikers.Contains(ctntSrcPropDef.Moniker))
                        {
                            propMonikers[ctntSrcPropDef.Moniker] = propDef;
                        }
                    }
                }
            }

            return propMonikers;
        }

        /// <summary>
        /// Applies override property values to the write request array and updates BOM data if applicable.
        /// Respects dateOnly and boolAsInt conversion options.
        /// </summary>
        private void ApplyOverridesToWriteProps(ACW.PropWriteReq[] writeProps,
            Dictionary<string, ACW.PropDef> propMonikers,
            Dictionary<ACW.PropDef, object> overridePropValues, BOM currentBOM)
        {
            if (overridePropValues == null || overridePropValues.Count == 0)
                return;

            foreach (ACW.PropWriteReq writeReq in writeProps)
            {
                if (!propMonikers.TryGetValue(writeReq.Moniker, out ACW.PropDef propDef))
                    continue;
                if (!overridePropValues.TryGetValue(propDef, out object overrideValue))
                    continue;

                // apply dateOnly and boolAsInt conversion options to the override value
                object convertedValue = ApplyTypeConversion(overrideValue, propDef.Typ);

                writeReq.Val = convertedValue;

                // update the BOM data if this property is part of the BOM
                if (currentBOM != null)
                {
                    BOMProp bOMProp = currentBOM.PropArray?.FirstOrDefault(bp => bp.Moniker == writeReq.Moniker);
                    if (bOMProp != null)
                    {
                        BOMCompAttr bOMCompAttr = currentBOM.CompAttrArray?.FirstOrDefault(ca => ca.PropId == bOMProp.Id);
                        if (bOMCompAttr != null)
                        {
                            bOMCompAttr.Val = convertedValue.ToString();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Applies dateOnly and boolAsInt conversion options to a property value based on its data type.
        /// Used for both component property values and override values.
        /// </summary>
        private object ApplyTypeConversion(object value, ACW.DataType dataType)
        {
            if (value == null)
                return null;

            switch (dataType)
            {
                case ACW.DataType.DateTime:
                    if (value is DateTime dateValue && dateOnly)
                    {
                        return dateValue.Date.ToShortDateString();
                    }
                    break;

                case ACW.DataType.Bool:
                    if (value is bool boolValue && boolAsInt)
                    {
                        return boolValue ? 1 : 0;
                    }
                    break;
            }

            return value;
        }

        /// <summary>
        /// Converts a component property value to the appropriate format based on property type and conversion options.
        /// Handles date-only and bool-as-int conversions if configured.
        /// </summary>
        private object ConvertPropertyValue(ACW.CompProp compProp, ACW.DataType dataType)
        {
            if (compProp.Val == null)
                return null;

            return ApplyTypeConversion(compProp.Val, dataType);
        }

        /// <summary>
        /// Converts a string value to the appropriate data type based on the property definition type.
        /// </summary>
        private object ConvertStringToPropertyType(string stringValue, ACW.DataType dataType)
        {
            if (string.IsNullOrWhiteSpace(stringValue))
                return null;

            try
            {
                switch (dataType)
                {
                    case ACW.DataType.String:
                        return stringValue;

                    case ACW.DataType.Numeric:
                        if (double.TryParse(stringValue, out double numericValue))
                        {
                            return numericValue;
                        }
                        break;

                    case ACW.DataType.DateTime:
                        if (DateTime.TryParse(stringValue, out DateTime dateValue))
                        {
                            return dateValue;
                        }
                        break;

                    case ACW.DataType.Bool:
                        string lowerValue = stringValue.ToLower().Trim();
                        if (lowerValue == "true" || lowerValue == "1" || lowerValue == "yes")
                        {
                            return true;
                        }
                        else if (lowerValue == "false" || lowerValue == "0" || lowerValue == "no")
                        {
                            return false;
                        }
                        else if (bool.TryParse(stringValue, out bool boolValue))
                        {
                            return boolValue;
                        }
                        break;

                    case ACW.DataType.Image:
                        return stringValue;

                    default:
                        return stringValue;
                }
            }
            catch (Exception)
            {
                return null;
            }

            return null;
        }

        /// <summary>
        /// Gets file associations to preserve them during check-in operations.
        /// </summary>
        private ACW.FileAssocParam[] GetFileAssociations(ACWT.WebServiceManager webSrvMgr, ACW.File file)
        {
            ACW.FileAssocLite[] childAssocs = webSrvMgr.DocumentService.GetFileAssociationLitesByIds(
                new long[] { file.Id },
                ACW.FileAssocAlg.Actual,
                ACW.FileAssociationTypeEnum.None, false,
                ACW.FileAssociationTypeEnum.All, false,
                true, false, true
                );

            ACW.FileAssocParam[] associations = childAssocs.Select(
                a => new ACW.FileAssocParam()
                {
                    Typ = a.Typ,
                    CldFileId = a.CldFileId,
                    Source = a.Source,
                    RefId = a.RefId,
                    ExpectedVaultPath = a.ExpectedVaultPath
                }
                ).ToArray();

            return associations;
        }

        /// <summary>
        /// Sets property values in the Vault database without checking in the file.
        /// The file is checked out if not already, and remains checked out after the call.
        /// This avoids triggering Read mappings that would overwrite DB values with old file content.
        /// The caller is responsible for the subsequent check-in (typically via SyncProperties).
        /// </summary>
        private void SetDbPropValues(ref ACW.File file, string comment, Dictionary<ACW.PropDef, object> newPropValues)
        {
            ACW.ByteArray downloadTicket = null;

            if (!EnsureFileCheckedOut(webSrvMgr, ref file, comment, out downloadTicket))
            {
                return;
            }

            PropInstParamArray propInstParamArray = BuildPropInstParamArray(newPropValues);
            webSrvMgr.DocumentService.UpdateFileProperties(new long[] { file.MasterId }, new PropInstParamArray[] { propInstParamArray });
        }

        #endregion private helper methods
    }
}
