using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

using ACW = Autodesk.Connectivity.WebServices;
using Autodesk.Connectivity.WebServicesTools;

namespace FileProperties_UpdateUdpValues
{
    class Program
    {
        static void Main(string[] args)
        {
            #region ConnectToVault
            ACW.ServerIdentities mServerId = new ACW.ServerIdentities();
            mServerId.DataServer = "192.168.85.129";           //replace value by command line args[i]
            mServerId.FileServer = "192.168.85.129";
            string mVaultName = "PDMC-Sample";
            string mUserName = "CAD-Admin";
            string mPassword = "";
            ACW.LicensingAgent mLicAgent = ACW.LicensingAgent.Client;
            WebServiceManager mVault = null;
            UserPasswordCredentials mCred = null;

            try
            {
                mCred = new UserPasswordCredentials(mServerId, mVaultName, mUserName, mPassword, mLicAgent);
                mVault = new WebServiceManager(mCred);

                try
                {
                    // specify the file path to update UDP values; adjust as needed
                    string mFilePath = "$/Designs/Test.ipt";
                    ACW.File mFile = mVault.DocumentService.FindLatestFilesByPaths((new List<string> { mFilePath }).ToArray()).FirstOrDefault();
                    
                    if (mFile != null)
                    {
                        // retrieve the available Property Definitions for files
                        ACW.PropDef[] mPropDefs = mVault.PropertyService.GetPropertyDefinitionsByEntityClassId("FILE");
                        
                        long mFileId = mFile.Id;
                        
                        // retrieve existing properties for the file
                        ACW.PropInst[] mPropInsts = mVault.PropertyService.GetPropertiesByEntityIds("FILE", new long[] { mFileId });
                        
                        // prepare the list of property instances to update
                        List<ACW.PropInst> mPropInstsToUpdate = new List<ACW.PropInst>();
                        
                        // example: update a UDP named "Description" and "Stock Number"
                        foreach (ACW.PropInst propInst in mPropInsts)
                        {
                            // get the property definition by name
                            ACW.PropDef descriptionPropDef = mPropDefs.FirstOrDefault(def => def.DispName == "Description");
                            ACW.PropDef stockNumberPropDef = mPropDefs.FirstOrDefault(def => def.DispName == "Stock Number");
                            
                            if (descriptionPropDef != null && propInst.PropDefId == descriptionPropDef.Id)
                            {
                                propInst.Val = "Updated description via API";
                                mPropInstsToUpdate.Add(propInst);
                            }
                            else if (stockNumberPropDef != null && propInst.PropDefId == stockNumberPropDef.Id)
                            {
                                propInst.Val = "B";
                                mPropInstsToUpdate.Add(propInst);
                            }
                        }
                        
                        // update the properties in Vault
                        if (mPropInstsToUpdate.Count > 0)
                        {
                            // Get file property configuration to determine if property mappings exist
                            ACW.FilePropCfg[] filePropCfgs = mVault.PropertyService.GetPropertyConfigurationsByEntityClassId("FILE");
                            FilePropCfg filePropCfg = filePropCfgs.FirstOrDefault(cfg => cfg.FileExtension == Path.GetExtension(mFile.Name));
                            
                            bool hasPropertyMapping = false;
                            Dictionary<string, object> mappedProperties = new Dictionary<string, object>();
                            
                            if (filePropCfg != null && filePropCfg.PropDefArray != null)
                            {
                                // Check if any of the properties being updated have mappings
                                foreach (PropInst propToUpdate in mPropInstsToUpdate)
                                {
                                    PropDef propDef = mPropDefs.FirstOrDefault(pd => pd.Id == propToUpdate.PropDefId);
                                    if (propDef != null && filePropCfg.PropDefArray.Any(pd => pd.Id == propToUpdate.PropDefId))
                                    {
                                        hasPropertyMapping = true;
                                        // Store the property name and value for writing to the file
                                        mappedProperties.Add(propDef.DispName, propToUpdate.Val);
                                    }
                                }
                            }
                            
                            if (hasPropertyMapping)
                            {
                                Console.WriteLine("Property mappings detected. File will be checked out, properties written to file, and checked in.");
                                
                                // Check out the file
                                try
                                {
                                    // Check if file is already checked out
                                    if (mFile.CheckoutState != CheckoutStateType.CheckedOut)
                                    {
                                        // Get download ticket
                                        ByteArray downloadTicket = mVault.DocumentService.GetDownloadTicketsByFileIds(
                                            new long[] { mFileId },
                                            false // allowSync
                                        ).FirstOrDefault();
                                        
                                        // Create temp folder for checkout
                                        string tempFolder = Path.Combine(Path.GetTempPath(), "VaultCheckout");
                                        if (!Directory.Exists(tempFolder))
                                        {
                                            Directory.CreateDirectory(tempFolder);
                                        }
                                        
                                        // Perform checkout
                                        mVault.DocumentService.CheckoutFile(
                                            mFile.Id,
                                            CheckoutFileOptions.Master,
                                            Environment.MachineName,
                                            tempFolder,
                                            "Checking out for property update",
                                            out mFile
                                        );
                                        
                                        Console.WriteLine($"File checked out successfully to: {tempFolder}");
                                        
                                        // Download the file
                                        string localFilePath = Path.Combine(tempFolder, mFile.Name);
                                        
                                        if (downloadTicket != null && downloadTicket.Bytes != null)
                                        {
                                            // Download file using ticket
                                            mVault.FilestoreService.DownloadFile(
                                                mFile.Id,
                                                downloadTicket,
                                                localFilePath
                                            );
                                            
                                            Console.WriteLine($"File downloaded to: {localFilePath}");
                                        }
                                        
                                        // Write properties to the physical file using appropriate provider
                                        string fileExtension = Path.GetExtension(mFile.Name).ToLower();
                                        bool propertiesWrittenToFile = false;
                                        
                                        if (fileExtension == ".ipt" || fileExtension == ".iam" || fileExtension == ".idw" || fileExtension == ".dwg")
                                        {
                                            // Use Inventor ApprenticeServer to write properties
                                            propertiesWrittenToFile = WritePropertiesToInventorFile(localFilePath, mappedProperties);
                                        }
                                        // Add other file type handlers as needed
                                        // else if (fileExtension == ".dwg")
                                        // {
                                        //     propertiesWrittenToFile = WritePropertiesToAutoCADFile(localFilePath, mappedProperties);
                                        // }
                                        
                                        if (propertiesWrittenToFile)
                                        {
                                            Console.WriteLine("Properties written to physical file successfully.");
                                            
                                            // Update properties in Vault database
                                            mVault.PropertyService.UpdateProperties(mPropInstsToUpdate.ToArray());
                                            Console.WriteLine("Properties updated in Vault database.");
                                            
                                            // Refresh file object
                                            mFile = mVault.DocumentService.GetFileById(mFileId);
                                            
                                            // Perform checkin
                                            File checkedInFile = null;
                                            mVault.DocumentService.CheckinFile(
                                                mFile.Id,
                                                "Updated properties via API",
                                                false, // keepCheckedOut (set to false to release the file)
                                                null,  // date modified
                                                null,  // associations
                                                null,  // fileFolderId
                                                false, // createFolder
                                                localFilePath,
                                                null,  // fileClass
                                                false, // hidden
                                                null,  // masterFileId
                                                out checkedInFile
                                            );
                                            
                                            if (checkedInFile != null)
                                            {
                                                Console.WriteLine("File checked in successfully with updated properties.");
                                            }
                                        }
                                        else
                                        {
                                            Console.WriteLine("Failed to write properties to physical file. Undoing checkout.");
                                            mVault.DocumentService.UndoCheckoutFile(mFileId, null);
                                        }
                                        
                                        // Clean up temp file
                                        try
                                        {
                                            if (File.Exists(localFilePath))
                                            {
                                                File.Delete(localFilePath);
                                            }
                                        }
                                        catch { }
                                    }
                                    else
                                    {
                                        Console.WriteLine($"File is already checked out by user ID {mFile.CheckoutUserId}. Cannot proceed.");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Error during checkout/checkin: {ex.Message}");
                                    
                                    // Try to undo checkout if it was successful
                                    try
                                    {
                                        mFile = mVault.DocumentService.GetFileById(mFileId);
                                        if (mFile.CheckoutState == CheckoutStateType.CheckedOut)
                                        {
                                            mVault.DocumentService.UndoCheckoutFile(mFileId, null);
                                            Console.WriteLine("Checkout undone due to error.");
                                        }
                                    }
                                    catch { }
                                    
                                    throw;
                                }
                            }
                            else
                            {
                                // No property mapping - update directly in database
                                Console.WriteLine("No property mappings detected. Updating properties directly in database.");
                                mVault.PropertyService.UpdateProperties(mPropInstsToUpdate.ToArray());
                                Console.WriteLine("Properties updated successfully.");
                            }
                        }
                        else
                        {
                            Console.WriteLine("No matching UDPs found to update for file: " + mFilePath);
                        }
                    }
                    else
                    {
                        Console.WriteLine("File not found: " + mFilePath);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                    throw;
                }
                //never forget to release the license, especially if pulled from Server
                mVault.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fatal error: {ex.Message}");
                throw;
            }
            #endregion connect to Vault
        }

        /// <summary>
        /// Writes properties to an Inventor file using ApprenticeServer API
        /// </summary>
        /// <param name="filePath">Local path to the Inventor file</param>
        /// <param name="properties">Dictionary of property names and values to write</param>
        /// <returns>True if successful, false otherwise</returns>
        private static bool WritePropertiesToInventorFile(string filePath, Dictionary<string, object> properties)
        {
            try
            {
                // Create ApprenticeServer instance
                // Note: Requires Inventor or Inventor View to be installed
                Type apprenticeType = Type.GetTypeFromProgID("Inventor.ApprenticeServer");
                if (apprenticeType == null)
                {
                    Console.WriteLine("Inventor ApprenticeServer is not available. Inventor or Inventor View must be installed.");
                    return false;
                }

                dynamic apprentice = Activator.CreateInstance(apprenticeType);
                
                try
                {
                    // Open the document
                    dynamic doc = apprentice.Open(filePath);
                    
                    // Get the property sets
                    dynamic propertySets = doc.PropertySets;
                    
                    // Iterate through properties to update
                    foreach (var prop in properties)
                    {
                        string propertyName = prop.Key;
                        object propertyValue = prop.Value;
                        
                        // Try to find the property in different property sets
                        bool propertyFound = false;
                        
                        // Common property sets to check
                        string[] propertySetNames = { 
                            "Design Tracking Properties", 
                            "Inventor Summary Information",
                            "Inventor Document Summary Information",
                            "Inventor User Defined Properties"
                        };
                        
                        foreach (string psName in propertySetNames)
                        {
                            try
                            {
                                dynamic propertySet = propertySets[psName];
                                
                                // Try to get existing property
                                try
                                {
                                    dynamic existingProp = propertySet[propertyName];
                                    existingProp.Value = propertyValue;
                                    propertyFound = true;
                                    Console.WriteLine($"Updated property '{propertyName}' in '{psName}' to '{propertyValue}'");
                                    break;
                                }
                                catch
                                {
                                    // Property doesn't exist in this set, try next set
                                    continue;
                                }
                            }
                            catch
                            {
                                // Property set doesn't exist or can't be accessed
                                continue;
                            }
                        }
                        
                        if (!propertyFound)
                        {
                            // If property not found, try to add to User Defined Properties
                            try
                            {
                                dynamic userDefinedProps = propertySets["Inventor User Defined Properties"];
                                userDefinedProps.Add(propertyValue, propertyName);
                                Console.WriteLine($"Added new property '{propertyName}' to 'Inventor User Defined Properties' with value '{propertyValue}'");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Could not add property '{propertyName}': {ex.Message}");
                            }
                        }
                    }
                    
                    // Save the document
                    doc.Save();
                    doc.Close();
                    
                    Console.WriteLine("Inventor file properties saved successfully.");
                    return true;
                }
                finally
                {
                    // Release COM object
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(apprentice);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing properties to Inventor file: {ex.Message}");
                return false;
            }
        }
    }
}
