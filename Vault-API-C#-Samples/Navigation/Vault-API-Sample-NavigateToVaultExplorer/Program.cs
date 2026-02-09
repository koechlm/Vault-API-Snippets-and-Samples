using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Xml.Linq;

using ACW = Autodesk.Connectivity.WebServices;
using Autodesk.Connectivity.WebServicesTools;
using VDF = Autodesk.DataManagement.Client.Framework;
using Vault = Autodesk.DataManagement.Client.Framework.Vault;
using Forms = Autodesk.DataManagement.Client.Framework.Vault.Forms;


namespace Vault_API_Sample_NavigateToVaultExplorer
{
    class Program
    {
        static void Main(string[] args)
        {
            #region constants
            // You can change these constants to match your Vault environment
            const string server = "http://your_vault_server";
            const string vaultName = "YourVaultName";
            const string folderFullName = "/path/to/your/folder";
            const string parentFolderFullName = "/path/to/your";
            const string fileName = "file.txt";

            const string ItemNumber = "ITEM-0001";
            const string ChangeOrderNumber = "ECO-0001";
            const string customEntityDefName = "CustomEntityDefinitionName"; // the name of a custom object definition in your Vault
            const string CustomObjectName = "CustomObjectName"; // The (default) Name property value of the custom object you want to retrieve
            #endregion constants

            #region entity variables
            ACW.File file = null;
            ACW.Item item = null;
            ACW.ChangeOrder changeOrder = null;
            ACW.CustEnt CustomObject = null;
            #endregion entity variables

            #region ConnectToVault
            Autodesk.DataManagement.Client.Framework.Vault.Currency.Connections.Connection connection = null;
            WebServiceManager webServiceManager = null;

            connection = Vault.Forms.Library.Login(null);
            if (connection == null)
            {
                Console.WriteLine("Failed to log in to Vault");
                return;
            }

            try
            {
                webServiceManager = connection.WebServiceManager;

                // Get the folder by its path in Vault
                ACW.Folder folder = webServiceManager.DocumentService.FindFoldersByPaths(new string[] { folderFullName }).FirstOrDefault();
                if (folder == null) {
                    Console.WriteLine($"Folder '{folderFullName}' not found");
                    return;
                }
                // build the URL to navigate using a browser. The URL format is: http://{server}/AutodeskDM/Services/EntityDataCommandRequest.aspx?Vault={vaultName}&ObjectId={folderFullName}&ObjectType=Folder&Command=Select
                string folderUrl = $"http://{server}/AutodeskDM/Services/EntityDataCommandRequest.aspx?Vault={Uri.EscapeDataString(vaultName)}&ObjectId={Uri.EscapeDataString(folderFullName)}&ObjectType=Folder&Command=Select";
                Console.WriteLine($"Folder URL: {folderUrl}");

                // Create ACR file from template for navigating to folder
                string templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FolderDataCommandRequest.acr");
                string outputPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GoToFolder.acr");
                
                if (CreateAcrFileFromTemplate(templatePath, outputPath, server, vaultName, folderFullName))
                {
                    Console.WriteLine($"Created ACR file: {outputPath}");

                    // Start Vault Explorer with the ACR file to navigate to the folder; the path to the Vault Explorer is C:\Program Files\Autodesk\Vault Client 2026\Explorer\Connectivity.VaultPro.exe, provide the acr file as an argument when starting the process
                    System.Diagnostics.Process.Start(@"C:\Program Files\Autodesk\Vault Client 2026\Explorer\Connectivity.VaultPro.exe", outputPath);
                }
                else
                {
                    Console.WriteLine($"Failed to create ACR file. Template not found: {templatePath}");
                }

                // Get the file by its path in Vault
                file = webServiceManager.DocumentService.FindLatestFilesByPaths(new string[] { parentFolderFullName + "/" + fileName }).FirstOrDefault();
                if (file != null)
                {
                    // Create ACR file for navigating to file
                    string fileTemplatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FileDataCommandRequest.acr");
                    string fileOutputPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GoToFile.acr");
                    string fileFullPath = parentFolderFullName + "/" + fileName;
                    
                    if (CreateAcrFileFromTemplate(fileTemplatePath, fileOutputPath, server, vaultName, fileFullPath, "File"))
                    {
                        Console.WriteLine($"Created ACR file for file: {fileOutputPath}");
                    }
                }

                // Get the item by its item number
                item = webServiceManager.ItemService.GetLatestItemByItemNumber(ItemNumber);
                if (item != null)
                {
                    // Create ACR file for navigating to item
                    string itemTemplatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ItemDataCommandRequest.acr");
                    string itemOutputPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GoToItem.acr");
                    
                    if (CreateAcrFileFromTemplate(itemTemplatePath, itemOutputPath, server, vaultName, item.ItemNum, "Item"))
                    {
                        Console.WriteLine($"Created ACR file for item: {itemOutputPath}");
                    }
                }

                // Get the change order by its change order number
                changeOrder = webServiceManager.ChangeOrderService.GetChangeOrderByNumber(ChangeOrderNumber);
                if (changeOrder != null)
                {
                    // Create ACR file for navigating to change order
                    string ecoTemplatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "EcoDataCommandRequest.acr");
                    string ecoOutputPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GoToChangeOrder.acr");
                    
                    if (CreateAcrFileFromTemplate(ecoTemplatePath, ecoOutputPath, server, vaultName, changeOrder.Num, "ChangeOrder"))
                    {
                        Console.WriteLine($"Created ACR file for change order: {ecoOutputPath}");
                    }
                }

                // Custom Objects (CUSTENT) are user defined entities. Not knowing the sub-type id of it, we best search for a custom object by a given name and then get its id to retrieve it.
                CustomObject = searchCustentByName(webServiceManager, customEntityDefName, CustomObjectName);
                if (CustomObject != null)
                {
                    // Create ACR file for navigating to custom entity
                    string custentTemplatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CustentDataCommandRequest.acr");
                    string custentOutputPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GoToCustomEntity.acr");
                    
                    if (CreateAcrFileFromTemplate(custentTemplatePath, custentOutputPath, server, vaultName, CustomObject.Num, "CustomEntity"))
                    {
                        Console.WriteLine($"Created ACR file for custom entity: {custentOutputPath}");
                    }
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                throw;
            }
            finally
            {
                if (connection != null)
                {
                    Vault.Forms.Library.Logout(connection);
                }
                
                Console.WriteLine("\nPress any key to exit...");
                Console.ReadKey();
            }
            #endregion connect to Vault
        }

        /// <summary>
        /// Creates an ACR file from a template by replacing placeholders with actual values
        /// </summary>
        /// <param name="templatePath">Path to the ACR template file</param>
        /// <param name="outputPath">Path where the new ACR file will be created</param>
        /// <param name="server">Vault server address</param>
        /// <param name="vaultName">Vault name</param>
        /// <param name="objectId">Object ID (folder path, file path, item number, etc.)</param>
        /// <param name="objectType">Object type (Folder, File, Item, ChangeOrder, CustomEntity)</param>
        /// <returns>True if successful, false otherwise</returns>
        private static bool CreateAcrFileFromTemplate(string templatePath, string outputPath, string server, string vaultName, string objectId, string objectType = "Folder")
        {
            try
            {
                // Check if template file exists
                if (!System.IO.File.Exists(templatePath))
                {
                    Console.WriteLine($"Template file not found: {templatePath}");
                    return false;
                }

                // Read the template file as XML
                XDocument doc = XDocument.Load(templatePath);
                
                // Get the namespace
                XNamespace ns = "http://schemas.autodesk.com/msd/plm/ExplorerAutomation/2004-11-01";
                
                // Replace placeholders in the XML
                var serverElement = doc.Descendants(ns + "Server").FirstOrDefault();
                if (serverElement != null)
                {
                    serverElement.Value = server;
                }
                
                var vaultElement = doc.Descendants(ns + "Vault").FirstOrDefault();
                if (vaultElement != null)
                {
                    vaultElement.Value = vaultName;
                }
                
                var operationElement = doc.Descendants(ns + "Operation").FirstOrDefault();
                if (operationElement != null)
                {
                    // Update ObjectType attribute if needed
                    var objectTypeAttr = operationElement.Attribute("ObjectType");
                    if (objectTypeAttr != null)
                    {
                        objectTypeAttr.Value = objectType;
                    }
                }
                
                var objectIdElement = doc.Descendants(ns + "ObjectID").FirstOrDefault();
                if (objectIdElement != null)
                {
                    objectIdElement.Value = objectId;
                }
                
                // Save the modified XML to the output file
                doc.Save(outputPath);
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating ACR file: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Search for a custom entity by a given definition name and a given Name property value.
        /// Note - custom entities don't have unique names; therefore we return the first match based on the search criteria. In a production environment, you may want to add more search criteria to ensure you get the correct custom entity.
        /// </summary>
        /// <param name="webServiceManager"></param>
        /// <param name="custentDefName"></param>
        /// <param name="custentName"></param>
        /// <returns></returns>
        private static ACW.CustEnt searchCustentByName(WebServiceManager webServiceManager, string custentDefName, string custentName)
        {
            try
            {
                // Build the two search conditions
                List<ACW.SrchCond> srchConds = new List<ACW.SrchCond>();
                ACW.SrchCond srchCond = new ACW.SrchCond();
                ACW.PropDef[] propDefs = webServiceManager.PropertyService.GetPropertyDefinitionsByEntityClassId("CUSTENT");
                ACW.PropDef propDef = propDefs.FirstOrDefault(p => p.SysName == "CustomEntityName");
                
                if (propDef == null)
                {
                    Console.WriteLine($"Property '{custentDefName}' not found");
                    return null;
                }

                srchCond.PropDefId = propDef.Id;
                srchCond.SrchOper = 1; // equals
                srchCond.SrchTxt = custentDefName;
                srchCond.PropTyp = ACW.PropertySearchType.SingleProperty;
                srchCond.SrchRule = ACW.SearchRuleType.Must;

                srchConds.Add(srchCond);

                srchCond = new ACW.SrchCond();
                propDef = propDefs.FirstOrDefault(p => p.SysName == "Name");
                if (propDef == null)
                {
                    Console.WriteLine($"Property 'Name' not found");
                    return null;
                }
                srchCond.PropDefId = propDef.Id;
                srchCond.SrchOper = 1; // equals
                srchCond.SrchTxt = custentName;
                srchCond.PropTyp = ACW.PropertySearchType.SingleProperty;
                srchCond.SrchRule = ACW.SearchRuleType.Must;

                srchConds.Add(srchCond);

                ACW.SrchSort srchSort = new ACW.SrchSort();
                string bookmark = string.Empty;
                ACW.SrchStatus searchStatus = null;

                List<ACW.CustEnt> mResultAll = new List<ACW.CustEnt>();
                while (searchStatus == null || searchStatus.TotalHits == 0 || mResultAll.Count < searchStatus.TotalHits)
                {
                    ACW.CustEnt[] mResultPage = webServiceManager.CustomEntityService.FindCustomEntitiesBySearchConditions(
                        srchConds.ToArray(),
                        new ACW.SrchSort[] { srchSort },
                        ref bookmark,
                        out searchStatus);

                    if (searchStatus.IndxStatus != ACW.IndexingStatus.IndexingComplete)
                    {
                        Console.WriteLine("Warning: Search results may be incomplete due to indexing status");
                    }

                    if (mResultPage != null && mResultPage.Length > 0)
                    {
                        mResultAll.AddRange(mResultPage);
                    }
                    else
                    {
                        break;
                    }

                    break; // Limit to first result page
                }

                return mResultAll.FirstOrDefault();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error searching for custom entity: {ex.Message}");
                return null;
            }
        }

    }
}
