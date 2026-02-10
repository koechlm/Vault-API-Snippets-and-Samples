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
        // Constant for Vault Explorer executable path
        private const string VAULT_EXPLORER_PATH = @"C:\Program Files\Autodesk\Vault Client 2026\Explorer\Connectivity.VaultPro.exe";

        static void Main(string[] args)
        {
            #region entity variables
            ACW.File file = null;
            ACW.Item item = null;
            ACW.ChangeOrder changeOrder = null;
            ACW.CustEnt CustomObject = null;
            #endregion entity variables

            #region ConnectToVault

            // Prompt user to press enter to continue; this allows time to attach a debugger if needed before the Autodesk Account login dialog appears
            Console.WriteLine("Time to connect your debugger. Press Enter to start login...");
            Console.ReadLine();

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

                // Get server and vault name from the connection
                string server = connection.Server;
                string vaultName = connection.Vault;

                Console.WriteLine($"Connected to Vault: {vaultName} on Server: {server}");
                Console.WriteLine();

                // Prompt user for entity identifiers
                Console.WriteLine("We continue to ask for entity navigation information...");

                // Prompt for folder path
                Console.Write("Enter folder full path or press Enter to use default (e.g., $/Designs) [default: $/Designs]: ");
                string folderFullName = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(folderFullName))
                {
                    folderFullName = "$/Designs";
                }

                // Navigate to Folder
                if (!string.IsNullOrWhiteSpace(folderFullName))
                {
                    ACW.Folder folder = webServiceManager.DocumentService.FindFoldersByPaths(new string[] { folderFullName }).FirstOrDefault();
                    if (folder == null)
                    {
                        Console.WriteLine($"Folder '{folderFullName}' not found");
                    }
                    else
                    {
                        // build the URL to navigate using a browser
                        string folderUrl = $"http://{server}/AutodeskDM/Services/EntityDataCommandRequest.aspx?Vault={Uri.EscapeDataString(vaultName)}&ObjectId={Uri.EscapeDataString(folderFullName)}&ObjectType=Folder&Command=Select";
                        Console.WriteLine($"Folder URL: {folderUrl}");

                        // Create ACR file from template for navigating to folder
                        string templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DataCommandRequest.acr");
                        string outputPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GoToFolder.acr");

                        if (CreateAcrFileFromTemplate(templatePath, outputPath, server, vaultName, folderFullName, "Folder"))
                        {
                            Console.WriteLine($"Created ACR file: {outputPath}");

                            // Start Vault Explorer with the ACR file to navigate to the folder
                            System.Diagnostics.Process.Start(VAULT_EXPLORER_PATH, outputPath);

                            Console.WriteLine("Check your Vault Explorer application and review the navigation result. Press Enter once you are done...");
                            Console.ReadLine();
                        }
                        else
                        {
                            Console.WriteLine($"Failed to create ACR file. Template not found: {templatePath}");
                        }
                    }
                }

                // Navigate to File
                // Prompt for file information
                Console.Write("Enter parent folder path for a file or press Enter to use the default(e.g., $/Designs/Inventor Sample Data/Car Seat):  [default: $/Designs/Inventor Sample Data/Car Seat]");
                string parentFolderFullName = Console.ReadLine();
                string fileName = null;
                if (string.IsNullOrWhiteSpace(parentFolderFullName))
                {
                    parentFolderFullName = "$/Designs/Inventor Sample Data/Car Seat";
                }

                Console.Write("Enter file name or press Enter to use the default (e.g., Car Seat.iam): [default: Car Seat.iam]");
                fileName = Console.ReadLine();
                // set default file name if not provided
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    fileName = "Car Seat.iam";
                }

                if (!string.IsNullOrWhiteSpace(parentFolderFullName) && !string.IsNullOrWhiteSpace(fileName))
                {
                    file = webServiceManager.DocumentService.FindLatestFilesByPaths(new string[] { parentFolderFullName + "/" + fileName }).FirstOrDefault();
                    if (file == null)
                    {
                        Console.WriteLine($"File '{fileName}' not found in '{parentFolderFullName}'");
                    }
                    else
                    {
                        string fileTemplatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DataCommandRequest.acr");
                        string fileOutputPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GoToFile.acr");
                        string fileFullPath = parentFolderFullName + "/" + fileName;

                        if (CreateAcrFileFromTemplate(fileTemplatePath, fileOutputPath, server, vaultName, fileFullPath, "File"))
                        {
                            Console.WriteLine($"Created ACR file for file: {fileOutputPath}");

                            System.Diagnostics.Process.Start(VAULT_EXPLORER_PATH, fileOutputPath);

                            Console.WriteLine("Check your Vault Explorer application and review the navigation result. Press Enter once you are done...");
                            Console.ReadLine();
                        }
                    }
                }

                // Navigate to Item
                // Prompt for item number
                Console.Write("Enter Item Number or press Enter to use the default (e.g., 002654): [default: 002654]");
                string itemNumber = Console.ReadLine();
                // provide a default item number if none provided
                if (string.IsNullOrWhiteSpace(itemNumber))
                {
                    itemNumber = "002654";
                }
                if (!string.IsNullOrWhiteSpace(itemNumber))
                {
                    try
                    {
                        item = webServiceManager.ItemService.GetLatestItemByItemNumber(itemNumber);
                        if (item == null)
                        {
                            Console.WriteLine($"Item '{itemNumber}' not found");
                        }
                        else
                        {
                            string itemTemplatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DataCommandRequest.acr");
                            string itemOutputPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GoToItem.acr");

                            if (CreateAcrFileFromTemplate(itemTemplatePath, itemOutputPath, server, vaultName, item.ItemNum, "ItemRevision"))
                            {
                                Console.WriteLine($"Created ACR file for item: {itemOutputPath}");

                                System.Diagnostics.Process.Start(VAULT_EXPLORER_PATH, itemOutputPath);

                                Console.WriteLine("Check your Vault Explorer application and review the navigation result. Press Enter once you are done...");
                                Console.ReadLine();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error retrieving item '{itemNumber}': {ex.Message}");
                    }
                }

                // Navigate to Change Order
                // Prompt for change order number
                Console.Write("Enter Change Order Number or press Enter to use the default (e.g., ECO-000012): [default: ECO-000012]");
                string changeOrderNumber = Console.ReadLine();
                // provide a default change order number if none provided
                if (string.IsNullOrWhiteSpace(changeOrderNumber))
                {
                    changeOrderNumber = "ECO-000012";
                }
                if (!string.IsNullOrWhiteSpace(changeOrderNumber))
                {
                    try
                    {
                        changeOrder = webServiceManager.ChangeOrderService.GetChangeOrderByNumber(changeOrderNumber);
                        if (changeOrder == null)
                        {
                            Console.WriteLine($"Change Order '{changeOrderNumber}' not found");
                        }
                        else
                        {
                            string ecoTemplatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DataCommandRequest.acr");
                            string ecoOutputPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GoToChangeOrder.acr");

                            if (CreateAcrFileFromTemplate(ecoTemplatePath, ecoOutputPath, server, vaultName, changeOrder.Num, "ECO"))
                            {
                                Console.WriteLine($"Created ACR file for change order: {ecoOutputPath}");

                                System.Diagnostics.Process.Start(VAULT_EXPLORER_PATH, ecoOutputPath);

                                Console.WriteLine("Check your Vault Explorer application and review the navigation result. Press Enter once you are done...");
                                Console.ReadLine();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error retrieving change order '{changeOrderNumber}': {ex.Message}");
                    }
                }

                // Navigate to Custom Entity
                // Prompt for custom entity information
                Console.Write("Enter Custom Entity Definition Name (singular, the UI displays plural!) or press Enter to use the default (e.g., Task): [default: Task]: ");
                string customEntityDefName = Console.ReadLine();
                // provide a default custom entity definition name if none provided
                if (string.IsNullOrWhiteSpace(customEntityDefName))
                {
                    customEntityDefName = "Task";
                }
                // if custom entity definition name is provided, prompt for custom object name

                string customObjectName = null;
                if (!string.IsNullOrWhiteSpace(customEntityDefName))
                {
                    Console.Write("Enter Custom Object Name or press Enter to use the default (e.g., T-00001): [default: T-00001]: ");
                    customObjectName = Console.ReadLine();
                    // provide a default custom object name if none provided
                    if (string.IsNullOrWhiteSpace(customObjectName))
                    {
                        customObjectName = "T-00001";
                    }
                }
                if (!string.IsNullOrWhiteSpace(customEntityDefName) && !string.IsNullOrWhiteSpace(customObjectName))
                {
                    CustomObject = searchCustentByName(webServiceManager, customEntityDefName, customObjectName);
                    if (CustomObject == null)
                    {
                        Console.WriteLine($"Custom Object '{customObjectName}' of type '{customEntityDefName}' not found");
                    }
                    else
                    {
                        // Get the custom entity definition to retrieve the correct sub-type id for the custom entity
                        ACW.CustEntDef custEntDef = webServiceManager.CustomEntityService.GetAllCustomEntityDefinitions().Where(d => d.Id == CustomObject.CustEntDefId).FirstOrDefault();
                        string custEntObjectID = $"{custEntDef.Name}/{CustomObject.Num}";

                        string custentTemplatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DataCommandRequest.acr");
                        string custentOutputPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GoToCustomEntity.acr");

                        if (CreateAcrFileFromTemplate(custentTemplatePath, custentOutputPath, server, vaultName, custEntObjectID, "CustomEntity"))
                        {
                            Console.WriteLine($"Created ACR file for custom entity: {custentOutputPath}");

                            System.Diagnostics.Process.Start(VAULT_EXPLORER_PATH, custentOutputPath);

                            Console.WriteLine("Check your Vault Explorer application and review the navigation result. Press Enter once you are done...");
                            Console.ReadLine();
                        }
                    }
                }

                Console.WriteLine();
                Console.WriteLine("All navigation operations completed.");

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
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
        /// <param name="objectType">Object type (Folder, File, ItemRevision, ECO, CustomEntity)</param>
        /// <returns>True if successful, false otherwise</returns>
        private static bool CreateAcrFileFromTemplate(string templatePath, string outputPath, string server, string vaultName, string objectId, string objectType)
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
                    Console.WriteLine($"Property 'CustomEntityName' not found");
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
