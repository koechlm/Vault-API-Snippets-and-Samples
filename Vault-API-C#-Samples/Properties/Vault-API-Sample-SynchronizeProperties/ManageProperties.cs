using Autodesk.Connectivity.WebServices;
using Autodesk. Connectivity.WebServicesTools;
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

            // cache file property system names to map display names
            ACW.PropDef[] filePropDefs = webSrvMgr.PropertyService.GetPropertyDefinitionsByEntityClassId("FILE");
            foreach (ACW.PropDef propDef in filePropDefs)
            {
                filePropDispToSysNames[propDef.DispName] = propDef.SysName;
            }

            // Cache server configuration
            serverConfig = webSrvMgr.AdminService.GetServerConfiguration();
        }

        public ACW.File UpdateFileProperties(ACW.File file, string comment, bool allowSync, Dictionary<ACW.PropDef, object> newPropValues, 
            out ACW.PropWriteResults writeResults, out string[] cloakedEntityClasses, bool force = false)
        {
            // we need to split the properties in mapped and unmapped ones, because mapped properties need to be updated through the filestore service
            // write to file process, while unmapped properties can be updated directly through the DocumentService.UpdateFileProperties
            Dictionary<ACW.PropDef, object> mappedPropValues = new Dictionary<ACW.PropDef, object>();
            Dictionary<ACW.PropDef, object> unmappedPropValues = new Dictionary<ACW.PropDef, object>();

            // Use cached property definition infos
            IEnumerable<PropDefInfo> propDefInfos = propDefInfosByEntityClass["FILE"];
            
            // Get provider for the current file - batch this with a single API call
            PropDefInfo providerPropDefInfo = propDefInfos.FirstOrDefault(p => p.PropDef.SysName == "Provider");
            if (providerPropDefInfo == null)
            {
                // No provider property, treat all as unmapped
                foreach (var kvp in newPropValues)
                {
                    unmappedPropValues[kvp.Key] = kvp.Value;
                }
            }
            else
            {
                PropInst providerPropInst = webSrvMgr.PropertyService.GetProperties("FILE", new long[] { file.Id }, new long[] { providerPropDefInfo.PropDef.Id }).FirstOrDefault();
                string providerName = (string)providerPropInst.Val;

                // Use cached server configuration
                IEnumerable<CtntSrc> providers = serverConfig.CtntSrcArray.Where(source => source.DispName == providerName);

                if (providers.Count() == 0)
                    providers = serverConfig.CtntSrcArray.Where(source => source.SysName == "IFilter");

                CtntSrc provider = providers.FirstOrDefault();

                // Build a dictionary of PropDef.Id to PropDefInfo for faster lookup
                Dictionary<long, PropDefInfo> propDefInfoById = propDefInfos.ToDictionary(pdi => pdi.PropDef.Id);

                foreach (var kvp in newPropValues)
                {
                    ACW.PropDef propDef = kvp.Key;
                    object value = kvp.Value;

                    // Direct lookup instead of LINQ query for better performance
                    if (propDefInfoById.TryGetValue(propDef.Id, out PropDefInfo propDefInfo))
                    {
                        bool isMapped = false;

                        if (propDefInfo.EntClassCtntSrcPropCfgArray != null)
                        {
                            foreach (EntClassCtntSrcPropCfg contentSource in propDefInfo.EntClassCtntSrcPropCfgArray)
                            {
                                if (contentSource.EntClassId != "FILE" || contentSource.CtntSrcPropDefArray == null)
                                    continue;

                                for (int i = 0; i < contentSource.CtntSrcPropDefArray.Length; i++)
                                {
                                    if (contentSource.CtntSrcPropDefArray[i].CtntSrcId == provider.Id)
                                    {
                                        // this property is mapped to the provider of the file, check if it allows write
                                        if (contentSource.MapDirectionArray[i] == ACW.MappingDirection.Write)
                                        {
                                            mappedPropValues[propDef] = value;
                                            isMapped = true;
                                            break;
                                        }
                                    }
                                }

                                if (isMapped) break;
                            }
                        }

                        if (!isMapped)
                        {
                            // no mapping for this property, update directly
                            unmappedPropValues[propDef] = value;
                        }
                    }
                }
            }

            bool keepCheckedOut = false;
            if (mappedPropValues.Count > 0) keepCheckedOut = true;

            file = UpdateProperties(file, comment, unmappedPropValues, keepCheckedOut);
            file = SyncProperties(file, comment, allowSync, out writeResults, out cloakedEntityClasses, force, overridePropValues: mappedPropValues);
            return file;
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
            out string[] cloakedEntityClasses, bool force = false, Dictionary<ACW.PropDef, object> overridePropValues = null)
        {
            //Get the binary data for the file to be synced from the filestore service.
            ACW.ByteArray downloadTicket = null;

            // clear output parameters so we don't have to worry about that for each possible return condition.
            writeResults = null;
            cloakedEntityClasses = null;

            // synchronization is needed if there are compliance failures or if newPropValues are provided;
            if (!force && (overridePropValues == null || overridePropValues.Count == 0))
            {
                // NOTE: if we synced props to multiple files at a time, we could get compliance failures for all of them in one call.
                ACW.PropCompFail[] complianceFailures = webSrvMgr.PropertyService.GetPropertyComplianceFailuresByEntityIds(
                    "FILE", new long[] { file.Id }, /*filterPending*/true
                    );
                if (complianceFailures == null
                    || complianceFailures.Sum(cf => (cf.PropEquivFailArray != null ? cf.PropEquivFailArray.Length : 0)) == 0
                    )
                {
                    // nothing to do!
                    return file;
                }
            }

            // Use the shared checkout logic
            if (!EnsureFileCheckedOut(webSrvMgr, ref file, comment, out downloadTicket))
            {
                // File is checked out by someone else, cannot proceed
                return file;
            }

            try // if anything goes wrong from here on out, undo the checkout
            {
                // get component properties.                
                // NOTE: a null component UID means to get write-back properties for root component in the file.
                // WARNING: we can't sync component level properties without CAD!
                ACW.CompProp[] compProps = webSrvMgr.DocumentService.GetComponentProperties(file.Id, /*compUID*/null);
                if (compProps == null || compProps.Length == 0)
                {
                    // no component properties found, undo checkout and return
                    return webSrvMgr.DocumentService.UndoCheckoutFile(file.MasterId, out downloadTicket);
                }

                // return cloaked entity classes
                // NOTE: a propDefId of -1 indicates get couldn't get properties from an inaccessible entity.
                cloakedEntityClasses = compProps.Where(p => p.PropDefId < 0).Select(p => p.EntClassId).ToArray();
                if (cloakedEntityClasses != null && cloakedEntityClasses.Length > 0)
                {
                    // don't proceed since we don't have the permissions to write back 
                    // everything that is necessary to clear the failures.
                    return webSrvMgr.DocumentService.UndoCheckoutFile(file.MasterId, out downloadTicket);
                    
                }

                // filter so we only keep providerPropInst from non-cloaked entities
                // NOTE: this is unnecessary as long as we bail out if there are cloaked entities involved.
                compProps = compProps.Where(p => p.PropDefId > 0).ToArray();

                // if there is nothing to write back, bail out.
                // We shouldn't have made it this far if this was the case;
                // but you never can be too sure!
                if (compProps == null || compProps.Length == 0)
                {
                    // nothing to do, undo checkout and return
                    return webSrvMgr.DocumentService.UndoCheckoutFile(file.MasterId, out downloadTicket);                    
                }

                // convert CompProp array to PropWriteReq array.
                ACW.PropWriteReq[] writeProps = compProps.Select(
                    p => new ACW.PropWriteReq()
                    {
                        Moniker = p.Moniker,
                        CanCreate = p.CreateNew,
                        // convert property value based on property type and conversion options for date and bool types.
                        Val = ConvertPropertyValue(p, propDefsByEntityClassAndId[p.EntClassId][p.PropDefId].Typ)
                    }
                    ).ToArray();

                // retrieve the file property definitions to check for write mappings.
                CtntSrcPropDef[] fileProps = webSrvMgr.FilestoreService.GetContentSourcePropertyDefinitions(
                downloadTicket.Bytes, true).Where(p => p.MapDirection == AllowedMappingDirection.Write || p.MapDirection == AllowedMappingDirection.ReadAndWrite).ToArray();

                // Build moniker to property definition mapping for override values
                Dictionary<string, ACW.PropDef> propMonikers = new Dictionary<string, ACW.PropDef>();

                if (overridePropValues != null && overridePropValues.Count > 0)
                {
                    // Create a HashSet of file property monikers for faster lookup
                    HashSet<string> filePropMonikers = new HashSet<string>(fileProps.Select(fp => fp.Moniker));

                    foreach (var kvp in overridePropValues)
                    {
                        ACW.PropDef propDef = kvp.Key;
                        
                        // Use cached property definition info instead of creating new VDF PropertyDefinition
                        PropDefInfo propDefInfo = propDefInfosByEntityClass["FILE"].FirstOrDefault(pdi => pdi.PropDef.Id == propDef.Id);
                        
                        if (propDefInfo?.EntClassCtntSrcPropCfgArray != null)
                        {
                            foreach (EntClassCtntSrcPropCfg contentSource in propDefInfo.EntClassCtntSrcPropCfgArray)
                            {
                                if (contentSource.CtntSrcPropDefArray != null)
                                {
                                    foreach (CtntSrcPropDef ctntSrcPropDef in contentSource.CtntSrcPropDefArray)
                                    {
                                        string mappingMoniker = ctntSrcPropDef.Moniker;
                                        // Use HashSet for O(1) lookup instead of LINQ Any
                                        if (filePropMonikers.Contains(mappingMoniker))
                                        {
                                            propMonikers[mappingMoniker] = propDef;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                // update the writeProps with the override values using moniker mappings
                foreach (ACW.PropWriteReq writeReq in writeProps)
                {
                    if (propMonikers.TryGetValue(writeReq.Moniker, out ACW.PropDef propDef) &&
                        overridePropValues.TryGetValue(propDef, out object overrideValue))
                    {
                        writeReq.Val = overrideValue;
                    }
                }

                ACW.PropWriteRequests writePropsReq = new ACW.PropWriteRequests();
                writePropsReq.Requests = writeProps;
                writePropsReq.Bom = null;
                // use CopyFile to copy existing resource and write the properties.
                byte[] uploadTicket = webSrvMgr.FilestoreService.CopyFile(
                    downloadTicket.Bytes, null, allowSync, writePropsReq,
                    out writeResults
                    );

                // Get file associations to preserve them
                ACW.FileAssocParam[] associations = GetFileAssociations(webSrvMgr, file);

                // checkin file
                file = webSrvMgr.DocumentService.CheckinUploadedFile(
                    file.MasterId,
                    comment, /*keepCheckedOut*/false, /*lastWrite*/DateTime.Now,
                    associations,
                    /*bom*/null, /*copyBom*/true, // preserve any BOM
                    file.Name, file.FileClass, file.Hidden, // preserve these attributes
                    new ACW.ByteArray() { Bytes = uploadTicket }
                    );
            }
            finally
            {
                // if we got here and file is still checked-out, 
                // something went wrong so undo the checkout.
                if (file.CheckedOut)
                    file = webSrvMgr.DocumentService.UndoCheckoutFile(file.MasterId, out downloadTicket);
            }

            return file;
        }


        /// <summary>
        /// Update UDPs of unmapped file properties
        /// </summary>
        /// <param name="file">File Iteration</param>
        /// <param name="comment">comment</param>
        /// <param name="newPropValues">Dictionary of property definitions and their new values</param>
        /// <returns>Updated file</returns>
        public ACW.File UpdateProperties(ACW.File file, string comment, Dictionary<ACW.PropDef, object> newPropValues, bool keepCheckedOut = false)
        {
            ACW.ByteArray downloadTicket = null;

            ACW.File currentFile = file;
            ACW.File updatedFile = null;

            // Use the shared checkout logic
            if (!EnsureFileCheckedOut(webSrvMgr, ref currentFile, comment, out downloadTicket))
            {
                // File is checked out by someone else, cannot proceed
                return file;
            }

            // build the propinstance array for the properties to update based on the input dictionary and the property definitions for the file.
            List<PropInstParam> propInstParams = new List<PropInstParam>();
            PropInstParamArray propInstParamArray = new PropInstParamArray();
            
            foreach (var kvp in newPropValues)
            {
                ACW.PropDef propDef = kvp.Key;
                object value = kvp.Value;
                
                PropInstParam propInstParam = new PropInstParam()
                {
                    PropDefId = propDef.Id,
                    Val = value
                };
                propInstParams.Add(propInstParam);
            }
            
            propInstParamArray.Items = propInstParams.ToArray();

            // update unmapped properties using DocumentService.UpdateFileProperties
            webSrvMgr.DocumentService.UpdateFileProperties(new long[] { currentFile.MasterId }, new PropInstParamArray[] { propInstParamArray });

            // get the upload ticket for the current file by copying it
            ACW.PropWriteRequests writePropsReq = new ACW.PropWriteRequests();
            writePropsReq.Requests = null;
            writePropsReq.Bom = null;
            byte[] uploadTicket = null;
            // use CopyFile to copy existing resource and write the properties.
            uploadTicket = webSrvMgr.FilestoreService.CopyFile(
                downloadTicket.Bytes, null, allowSync: true, writePropsReq,
                out _
                );

            // Get file associations to preserve them
            ACW.FileAssocParam[] associations = GetFileAssociations(webSrvMgr, file);

            // checkin file
            updatedFile = webSrvMgr.DocumentService.CheckinUploadedFile(
                file.MasterId,
                comment, keepCheckedOut, /*lastWrite*/DateTime.Now,
                associations,
                /*bom*/null, /*copyBom*/true, // preserve any BOM
                file.Name, file.FileClass, file.Hidden, // preserve these attributes
                new ACW.ByteArray() { Bytes = uploadTicket }
                );

            return updatedFile;
        }

        /// <summary>
        /// Update UDPs of unmapped file properties for multiple files in a single batch operation.
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

                // Use the shared checkout logic
                if (!EnsureFileCheckedOut(webSrvMgr, ref file, comment, out downloadTicket))
                {
                    // File is checked out by someone else, skip it
                    Console.WriteLine($"Skipping file {file.Name} - checked out by another user");
                    continue;
                }

                // Build property instance array for this file
                List<PropInstParam> propInstParams = new List<PropInstParam>();
                
                foreach (var kvp in propValuesByFile[i])
                {
                    ACW.PropDef propDef = kvp.Key;
                    object value = kvp.Value;
                    
                    PropInstParam propInstParam = new PropInstParam()
                    {
                        PropDefId = propDef.Id,
                        Val = value
                    };
                    propInstParams.Add(propInstParam);
                }

                PropInstParamArray propInstParamArray = new PropInstParamArray
                {
                    Items = propInstParams.ToArray()
                };

                masterIdsToUpdate.Add(file.MasterId);
                propInstParamArrays.Add(propInstParamArray);
                filesByMasterId[file.MasterId] = file;
                downloadTicketsByMasterId[file.MasterId] = downloadTicket;
            }

            if (masterIdsToUpdate.Count == 0)
            {
                return files; // No files could be checked out
            }

            try
            {
                // Batch update all file properties in a single API call
                webSrvMgr.DocumentService.UpdateFileProperties(masterIdsToUpdate.ToArray(), propInstParamArrays.ToArray());

                // Check in each file
                foreach (long masterId in masterIdsToUpdate)
                {
                    ACW.File file = filesByMasterId[masterId];
                    ACW.ByteArray downloadTicket = downloadTicketsByMasterId[masterId];

                    // Get upload ticket by copying the file
                    ACW.PropWriteRequests writePropsReq = new ACW.PropWriteRequests
                    {
                        Requests = null,
                        Bom = null
                    };

                    byte[] uploadTicket = webSrvMgr.FilestoreService.CopyFile(
                        downloadTicket.Bytes, null, allowSync: true, writePropsReq,
                        out _
                    );

                    // Get file associations to preserve them
                    ACW.FileAssocParam[] associations = GetFileAssociations(webSrvMgr, file);

                    // Check in file
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
                // If batch update fails, undo checkouts for all files
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
        /// Updates file properties for multiple files by intelligently routing them to either mapped (filestore service) or unmapped (direct database) update paths.
        /// This overload provides batch processing capabilities for improved performance when updating multiple files.
        /// </summary>
        /// <param name="files">Array of files to update properties on</param>
        /// <param name="comment">Comment to use for the new versions</param>
        /// <param name="allowSync">If true, allows the filestore to retrieve files from another filestore if not available locally</param>
        /// <param name="propValuesByFile">Array of property value dictionaries, one for each file (must match the length of files array)</param>
        /// <param name="writeResultsByFile">Output array of write results for mapped properties, one per file</param>
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

            // Split properties for each file into mapped and unmapped
            Dictionary<ACW.PropDef, object>[] mappedPropValuesByFile = new Dictionary<ACW.PropDef, object>[files.Length];
            Dictionary<ACW.PropDef, object>[] unmappedPropValuesByFile = new Dictionary<ACW.PropDef, object>[files.Length];
            
            // Use cached property definition infos
            IEnumerable<PropDefInfo> propDefInfos = propDefInfosByEntityClass["FILE"];
            PropDefInfo providerPropDefInfo = propDefInfos.FirstOrDefault(p => p.PropDef.SysName == "Provider");

            // Get providers for all files in a single batch call
            long[] fileIds = files.Select(f => f.Id).ToArray();
            PropInst[] providerPropInsts = null;
            
            if (providerPropDefInfo != null)
            {
                providerPropInsts = webSrvMgr.PropertyService.GetProperties("FILE", fileIds, new long[] { providerPropDefInfo.PropDef.Id });
            }

            // Build a dictionary of PropDef.Id to PropDefInfo for faster lookup
            Dictionary<long, PropDefInfo> propDefInfoById = propDefInfos.ToDictionary(pdi => pdi.PropDef.Id);

            // Process each file
            for (int fileIndex = 0; fileIndex < files.Length; fileIndex++)
            {
                mappedPropValuesByFile[fileIndex] = new Dictionary<ACW.PropDef, object>();
                unmappedPropValuesByFile[fileIndex] = new Dictionary<ACW.PropDef, object>();

                ACW.File file = files[fileIndex];
                Dictionary<ACW.PropDef, object> newPropValues = propValuesByFile[fileIndex];

                if (providerPropDefInfo == null)
                {
                    // No provider property, treat all as unmapped
                    unmappedPropValuesByFile[fileIndex] = newPropValues;
                    continue;
                }

                // Get provider for this file
                PropInst providerPropInst = providerPropInsts?.FirstOrDefault(pi => pi.EntityId == file.Id);
                if (providerPropInst == null)
                {
                    unmappedPropValuesByFile[fileIndex] = newPropValues;
                    continue;
                }

                string providerName = (string)providerPropInst.Val;
                IEnumerable<CtntSrc> providers = serverConfig.CtntSrcArray.Where(source => source.DispName == providerName);

                if (providers.Count() == 0)
                    providers = serverConfig.CtntSrcArray.Where(source => source.SysName == "IFilter");

                CtntSrc provider = providers.FirstOrDefault();

                // Classify properties as mapped or unmapped
                foreach (var kvp in newPropValues)
                {
                    ACW.PropDef propDef = kvp.Key;
                    object value = kvp.Value;

                    if (propDefInfoById.TryGetValue(propDef.Id, out PropDefInfo propDefInfo))
                    {
                        bool isMapped = false;

                        if (propDefInfo.EntClassCtntSrcPropCfgArray != null)
                        {
                            foreach (EntClassCtntSrcPropCfg contentSource in propDefInfo.EntClassCtntSrcPropCfgArray)
                            {
                                if (contentSource.EntClassId != "FILE" || contentSource.CtntSrcPropDefArray == null)
                                    continue;

                                for (int i = 0; i < contentSource.CtntSrcPropDefArray.Length; i++)
                                {
                                    if (contentSource.CtntSrcPropDefArray[i].CtntSrcId == provider.Id)
                                    {
                                        if (contentSource.MapDirectionArray[i] == ACW.MappingDirection.Write)
                                        {
                                            mappedPropValuesByFile[fileIndex][propDef] = value;
                                            isMapped = true;
                                            break;
                                        }
                                    }
                                }

                                if (isMapped) break;
                            }
                        }

                        if (!isMapped)
                        {
                            unmappedPropValuesByFile[fileIndex][propDef] = value;
                        }
                    }
                }
            }

            // Separate files into those with mapped vs unmapped only properties
            List<ACW.File> filesWithUnmappedOnly = new List<ACW.File>();
            List<Dictionary<ACW.PropDef, object>> unmappedOnlyProps = new List<Dictionary<ACW.PropDef, object>>();
            List<int> unmappedOnlyIndices = new List<int>();

            List<ACW.File> filesWithMapped = new List<ACW.File>();
            List<int> filesWithMappedIndices = new List<int>();

            for (int i = 0; i < files.Length; i++)
            {
                if (unmappedPropValuesByFile[i].Count > 0 && mappedPropValuesByFile[i].Count == 0)
                {
                    filesWithUnmappedOnly.Add(files[i]);
                    unmappedOnlyProps.Add(unmappedPropValuesByFile[i]);
                    unmappedOnlyIndices.Add(i);
                }
                else if (mappedPropValuesByFile[i].Count > 0)
                {
                    filesWithMapped.Add(files[i]);
                    filesWithMappedIndices.Add(i);
                }
            }

            ACW.File[] resultFiles = new ACW.File[files.Length];

            // Batch update unmapped-only files
            if (filesWithUnmappedOnly.Count > 0)
            {
                ACW.File[] updatedUnmappedFiles = UpdatePropertiesBatch(
                    filesWithUnmappedOnly.ToArray(),
                    comment,
                    unmappedOnlyProps.ToArray(),
                    keepCheckedOut: false
                );

                for (int i = 0; i < updatedUnmappedFiles.Length; i++)
                {
                    int originalIndex = unmappedOnlyIndices[i];
                    resultFiles[originalIndex] = updatedUnmappedFiles[i];
                }
            }

            // Process files with mapped properties individually (requires sync operation)
            for (int i = 0; i < filesWithMapped.Count; i++)
            {
                int originalIndex = filesWithMappedIndices[i];
                ACW.File file = filesWithMapped[i];

                bool keepCheckedOut = mappedPropValuesByFile[originalIndex].Count > 0;

                if (unmappedPropValuesByFile[originalIndex].Count > 0)
                {
                    file = UpdateProperties(file, comment, unmappedPropValuesByFile[originalIndex], keepCheckedOut);
                }

                file = SyncProperties(file, comment, allowSync, 
                    out writeResultsByFile[originalIndex], 
                    out cloakedEntityClassesByFile[originalIndex], 
                    force, 
                    overridePropValues: mappedPropValuesByFile[originalIndex]);

                resultFiles[originalIndex] = file;
            }

            return resultFiles;
        }
        /// <summary>
        /// Converts a dictionary of property display names and string values to properly typed property definitions and values.
        /// </summary>
        /// <param name="keyValuePairs">Key=Property DisplayName, Value=Property Value as string</param>
        /// <returns>Dictionary of FILE property definition and typed value object</returns>
        public Dictionary<PropDef, object> ConvertToPropDictionary(Dictionary<string, string> keyValuePairs)
        {
            Dictionary<ACW.PropDef, object> propDictionary = new Dictionary<ACW.PropDef, object>();

            // Get FILE property definitions from cache
            Dictionary<long, ACW.PropDef> filePropDefs = propDefsByEntityClassAndId["FILE"];

            foreach (var kvp in keyValuePairs)
            {
                string displayName = kvp.Key;
                string stringValue = kvp.Value;

                // Convert display name to system name, then find the property definition
                if (filePropDispToSysNames.TryGetValue(displayName, out string sysName))
                {
                    // Find the property definition by system name
                    ACW.PropDef propDef = filePropDefs.Values.FirstOrDefault(pd => pd.SysName == sysName);

                    if (propDef != null && !string.IsNullOrWhiteSpace(stringValue))
                    {
                        // Convert string value to appropriate type based on property definition data type
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

        /// <summary>
        /// Ensures the file is checked out by the current user. If already checked out by another user, returns false.
        /// If already checked out by current user or successfully checks out, returns true.
        /// </summary>
        /// <param name="webSrvMgr">WebServiceManager instance</param>
        /// <param name="file">File to check out (will be updated if checkout is performed)</param>
        /// <param name="comment">Comment for the checkout operation</param>
        /// <param name="downloadTicket">Output parameter for the download ticket</param>
        /// <returns>True if file is checked out by current user, false if checked out by someone else</returns>
        private bool EnsureFileCheckedOut(ACWT.WebServiceManager webSrvMgr, ref ACW.File file, string comment, out ACW.ByteArray downloadTicket)
        {
            downloadTicket = null;

            // Check if file is checked out by someone else
            if (file.CheckedOut == true && file.CkOutUserId != webSrvMgr.AuthService.Session.User.Id)
            {
                return false; // can't check out since file is already checked out by someone else
            }

            // If file is already checked out by current user, get the download ticket
            if (file.CheckedOut == true && file.CkOutUserId == webSrvMgr.AuthService.Session.User.Id)
            {
                downloadTicket = webSrvMgr.DocumentService.GetDownloadTicketsByFileIds(new long[] { file.Id }).FirstOrDefault();
                return true;
            }

            // File is not checked out, perform checkout
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

        #region private helper methods

        /// <summary>
        /// Converts a string value to the appropriate data type based on the property definition type.
        /// </summary>
        /// <param name="stringValue">The string value to convert</param>
        /// <param name="dataType">The target data type</param>
        /// <returns>The converted value as an object, or null if conversion fails</returns>
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
                        // Handle multiple bool representations
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
                        // Image data should be handled separately, return as-is for now
                        return stringValue;

                    default:
                        // For unknown types, return the string value
                        return stringValue;
                }
            }
            catch (Exception)
            {
                // If conversion fails, return null
                return null;
            }

            return null;
        }

        /// <summary>
        /// Gets file associations to preserve them during check-in operations.
        /// </summary>
        /// <param name="webSrvMgr">WebServiceManager instance</param>
        /// <param name="file">File to get associations for</param>
        /// <returns>Array of FileAssocParam for use in check-in operations</returns>
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
        /// Converts a component property value to the appropriate format based on property type and conversion options.
        /// Handles date-only and bool-as-int conversions if configured.
        /// </summary>
        /// <param name="compProp">The component property to convert</param>
        /// <param name="dataType">The property data type from the property definition</param>
        /// <returns>The converted property value</returns>
        private object ConvertPropertyValue(ACW.CompProp compProp, ACW.DataType dataType)
        {
            if (compProp.Val == null)
                return null;

            switch (dataType)
            {
                case ACW.DataType.DateTime:
                    if (compProp.Val is DateTime dateValue)
                    {
                        if (dateOnly)
                        {
                            return dateValue.Date.ToShortDateString();
                        }
                        return dateValue;
                    }
                    break;

                case ACW.DataType.Bool:
                    if (compProp.Val is bool boolValue)
                    {
                        if (boolAsInt)
                        {
                            return boolValue ? 1 : 0;
                        }
                        return boolValue;
                    }
                    break;

                case ACW.DataType.Numeric:
                case ACW.DataType.String:
                case ACW.DataType.Image:
                default:
                    return compProp.Val;
            }

            return compProp.Val;
        }

        #endregion private helper methods
    }
}
