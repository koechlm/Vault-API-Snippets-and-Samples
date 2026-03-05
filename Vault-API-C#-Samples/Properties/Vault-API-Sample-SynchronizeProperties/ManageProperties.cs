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
    /// This refactored version combines synchronizing properties and updating property values in one go.
    /// Use this class as a sample while being aware that it does not guarantee to cover all use cases.
    /// </summary>
    public class ManageProperties
    {
        // property cache used to find date and bool types; Vault options allow to return date without time and bool as 0/1 instead of true/false
        private Dictionary<string, Dictionary<long, ACW.PropDef>> propDefsByEntityClassAndId = new Dictionary<string, Dictionary<long, ACW.PropDef>>();
        // cache file property display name to system name mapping to apply override providerPropInst based on display names;
        private Dictionary<string, string> filePropDispToSysNames = new Dictionary<string, string>();
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
            }

            // cache file property system names to map display names
            ACW.PropDef[] filePropDefs = webSrvMgr.PropertyService.GetPropertyDefinitionsByEntityClassId("FILE");
            foreach (ACW.PropDef propDef in filePropDefs)
            {
                filePropDispToSysNames[propDef.DispName] = propDef.SysName;
            }
        }

        public ACW.File UpdateFileProperties(ACW.File file, string comment, bool allowSync, Dictionary<ACW.PropDef, object> newPropValues, 
            out ACW.PropWriteResults writeResults, out string[] cloakedEntityClasses, bool force = false)
        {
            // we need to split the properties in mapped and unmapped ones, because mapped properties need to be updated through the filestore service
            // write to file process, while unmapped properties can be updated directly through the DocumentService.UpdateFileProperties
            Dictionary<ACW.PropDef, object> mappedPropValues = new Dictionary<ACW.PropDef, object>();
            Dictionary<ACW.PropDef, object> unmappedPropValues = new Dictionary<ACW.PropDef, object>();

            // we need to get provider for the current file because a property might be mapped to multiple providers,
            IEnumerable<PropDefInfo> propDefInfos = webSrvMgr.PropertyService.GetPropertyDefinitionInfosByEntityClassId("FILE", null);
            PropDefInfo providerPropDefInfo = propDefInfos.Where(p => p.PropDef.SysName == "Provider").FirstOrDefault();
            PropInst providerPropInst = webSrvMgr.PropertyService.GetProperties("FILE", new long[] { file.Id }, new long[] { providerPropDefInfo.PropDef.Id }).FirstOrDefault();

            string providerName = (string)providerPropInst.Val;

            ServerCfg srvConfig = webSrvMgr.AdminService.GetServerConfiguration();
            IEnumerable<CtntSrc> providers = srvConfig.CtntSrcArray.Where(source => source.DispName == providerName);

            if (providers.Count() == 0)
                providers = srvConfig.CtntSrcArray.Where(source => source.SysName == "IFilter");

            CtntSrc provider = providers.FirstOrDefault();

            foreach (var kvp in newPropValues)
            {
                ACW.PropDef propDef = kvp.Key;
                object value = kvp.Value;
                
                PropertyDefinition vdfPropDef = connection.PropertyManager.GetPropertyDefinitionBySystemName(propDef.SysName);
                if (vdfPropDef != null)
                {
                    IEnumerable<PropDefInfo> results = propDefInfos.Where(prop => prop.PropDef.Id == vdfPropDef.Id);
                    PropDefInfo propDefInfo = results.FirstOrDefault();

                    if (propDefInfo?.EntClassCtntSrcPropCfgArray != null)
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
                                        // this property is mapped to the provider of the file and allows write, so we need to update it through the filestore service write to file process
                                        mappedPropValues[propDef] = value;
                                    }
                                }
                                else // this property is mapped to other providers, so we handle it like an unmapped property
                                {
                                    unmappedPropValues[propDef] = value;
                                }
                            }
                        }
                    }
                    else
                    {
                        // no mapping for this property, update directly
                        unmappedPropValues[propDef] = value;
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

                foreach (var kvp in overridePropValues)
                {
                    ACW.PropDef propDef = kvp.Key;
                    PropertyDefinition vdfPropDef = connection.PropertyManager.GetPropertyDefinitionBySystemName(propDef.SysName);
                    
                    if (vdfPropDef?.Mappings.HasMappings == true)
                    {
                        foreach (var mapping in vdfPropDef.Mappings.GetAllContentSourcePropertyMapping())
                        {
                            string mappingMoniker = mapping.ContentPropertyDefinition.Moniker;
                            if (fileProps.Any(fp => fp.Moniker == mappingMoniker))
                            {
                                propMonikers[mappingMoniker] = propDef;
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

            ACW.File currentFile = null;
            ACW.File updatedFile = null;
            // Checkout the file to be updated without downloading it
            currentFile = webSrvMgr.DocumentService.CheckoutFile(
                file.Id, ACW.CheckoutFileOptions.Master,
                /*machine*/Environment.MachineName, /*localPath*/string.Empty, /*comment*/string.Empty,
                out downloadTicket
                );

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
