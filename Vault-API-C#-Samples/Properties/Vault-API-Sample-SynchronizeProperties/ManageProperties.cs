using Autodesk.Connectivity.WebServices;
using Autodesk.DataManagement.Client.Framework.Vault.Currency.Connections;
using Autodesk.DataManagement.Client.Framework.Vault.Currency.Entities;
using Autodesk.DataManagement.Client.Framework.Vault.Currency.Properties;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// Vault SDK
using ACW = Autodesk.Connectivity.WebServices;
using ACWT = Autodesk.Connectivity.WebServicesTools;
using VDF = Autodesk.DataManagement.Client.Framework;

namespace Vault_API_Sample_ManageProperties
{

    /// <summary>
    /// Helper class to manage file properties including updates and synchronization using filestore service.
    /// This code originally has been posted on the blog Just Ones and Zeros, written by Dave Mink.
    /// This refactored version is adapted to be used as a sample for Vault API, but does not guarantee to cover all use cases.
    /// </summary>
    public class ManageProperties
    {
        // property cache used to find date and bool types; Vault options allow to return date without time and bool as 0/1 instead of true/false
        private Dictionary<string, Dictionary<long, ACW.PropDef>> propDefsByEntityClassAndId = new Dictionary<string, Dictionary<long, ACW.PropDef>>();
        private Dictionary<string, string> filePropDispToSysNames = new Dictionary<string, string>();
        private bool dateOnly = true;
        private bool boolAsInt = false;
        private Connection connection;
        private ACWT.WebServiceManager webSrvMgr;

        /// <summary>
        /// Constructor, takes a WebServiceManager as parameter in case we need to make multiple calls to the web services and want to reuse the same manager.
        /// </summary>
        /// <param name="webSrvMgr"></param>
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

        /// <summary>
        /// Ensures the file is checked out by the current user. If already checked out by another user, returns false.
        /// If already checked out by current user or successfully checks out, returns true.
        /// </summary>
        /// <param name="webSrvMgr">WebServiceManager instance</param>
        /// <param name="file">File to check out (will be updated if checkout is performed)</param>
        /// <param name="comment">Comment for the checkout operation</param>
        /// <param name="downloadTicket">Output parameter for the download ticket</param>
        /// <returns>True if file is checked out by current user, false if checked out by someone else</returns>
        public bool EnsureFileCheckedOut(ACWT.WebServiceManager webSrvMgr, ref ACW.File file, string comment, out ACW.ByteArray downloadTicket)
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

        /// <summary>
        /// Gets file associations to preserve them during check-in operations.
        /// </summary>
        /// <param name="webSrvMgr">WebServiceManager instance</param>
        /// <param name="file">File to get associations for</param>
        /// <returns>Array of FileAssocParam for use in check-in operations</returns>
        private ACW.FileAssocParam[] GetFileAssociations(ACWT.WebServiceManager webSrvMgr, ACW.File file)
        {
            // get child file associations so we can preserve them.
            ACW.FileAssocLite[] childAssocs = webSrvMgr.DocumentService.GetFileAssociationLitesByIds(
                new long[] { file.Id },
                ACW.FileAssocAlg.Actual, // preserve the associations provided by CAD add-in
                /*parentAssociationType*/ACW.FileAssociationTypeEnum.None, /*parentRecurse*/false,
                /*childAssociationType*/ACW.FileAssociationTypeEnum.All, /*childRecurse*/false,
                /*includeLibraryFiles*/true,
                /*includeRelatedDocuments*/false,
                /*includeHidden*/true
                );

            // convert FileAssocLite array to FileAssocParam array
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
        /// Sync properties of a file.
        /// </summary>
        /// <param name="webSrvMgr">a WebServiceManager</param>
        /// <param name="file">the file you would like to sync</param>
        /// <param name="comment">the comment for the new version (if a property sync was performed)</param>
        /// <param name="allowSync">if the local filestore doesn't have the file, get it from another filestore</param>
        /// <param name="writeResults">see FilestoreService.CopyFile method</param>
        /// <param name="cloakedEntityClasses">if you can't read an entity where properties would come from, its entity class is returned here</param>
        /// <param name="force">skip check for equivalence and always do a sync, creating a new version</param>
        /// <param name="overridePropValues">a dictionary of property display names and their override values</param>
        /// <returns>the file returned from the checkin, same as the input if no property sync is done</returns>
        public ACW.File SyncProperties(ACWT.WebServiceManager webSrvMgr, ACW.File file, string comment, bool allowSync, out ACW.PropWriteResults writeResults, 
            out string[] cloakedEntityClasses, bool force = false, Dictionary<string, string> overridePropValues = null)
        {
            //Get the binary data for the file to be synced from the filestore service.
            ACW.ByteArray downloadTicket = null;

            // clear output parameters so we don't have to worry about that for each possible return condition.
            writeResults = null;
            cloakedEntityClasses = null;

            // synchronization is needed if there are compliance failures or if overridePropValues are provided;
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

                // return cloaked entity classes thru out parameter.
                // NOTE: a propDefId of -1 indicates get couldn't get properties from an inaccessible entity.
                cloakedEntityClasses = compProps.Where(p => p.PropDefId < 0).Select(p => p.EntClassId).ToArray();
                if (cloakedEntityClasses != null && cloakedEntityClasses.Length > 0)
                {
                    // don't proceed since we don't have the permissions to write back 
                    // everything that is necessary to clear the failures.
                    return webSrvMgr.DocumentService.UndoCheckoutFile(file.MasterId, out downloadTicket);
                }

                // filter so we only keep values from non-cloaked entities
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

                List<PropertyDefinition> vdfPropDefs = new List<PropertyDefinition>();

                /// key is the mapping moniker, value is the property definition system name; we will use this to match the properties from the file with the property definitions in Vault to make sure we are only trying to sync properties that have mappings configured, and to get the correct system names for the properties to apply the override values if needed.
                Dictionary<string, string> propMonikers = new Dictionary<string, string>();

                foreach (string name in overridePropValues.Keys)
                {
                    string sysName = filePropDispToSysNames.ContainsKey(name) ? filePropDispToSysNames[name] : name;
                    vdfPropDefs.Add(connection.PropertyManager.GetPropertyDefinitionBySystemName(sysName));
                }
                foreach (PropertyDefinition definition in vdfPropDefs)
                {
                    if (definition.Mappings.HasMappings == true)
                    {
                        foreach (var mapping in definition.Mappings.GetAllContentSourcePropertyMapping())
                        {
                            // add all monikers to the list, where the fileProps' moniker matches the definition's mapping moniker
                            string mappingMoniker = mapping.ContentPropertyDefinition.Moniker;
                            if (fileProps.Any(fp => fp.Moniker == mappingMoniker))
                            {
                                propMonikers[mappingMoniker] = definition.DisplayName;
                            }
                        }
                    }
                }

                // update the writeProps with the monikers from the mappings; if a property doesn't have a mapping, it won't be included in the sync, so we can skip it.
                foreach (ACW.PropWriteReq writeReq in writeProps)
                {
                    if (propMonikers.ContainsKey(writeReq.Moniker))
                    {
                        string dispName = propMonikers[writeReq.Moniker];
                        writeReq.Val = overridePropValues[dispName];
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
                            // Remove time portion, keep only date
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
                            // Convert bool to integer: true = 1, false = 0
                            return boolValue ? 1 : 0;
                        }
                        return boolValue;
                    }
                    break;

                case ACW.DataType.Numeric:
                case ACW.DataType.String:
                case ACW.DataType.Image:
                default:
                    // Return value as-is for numeric, string, image, and other types
                    return compProp.Val;
            }

            // Fallback: return the original value
            return compProp.Val;
        }
    }
}
