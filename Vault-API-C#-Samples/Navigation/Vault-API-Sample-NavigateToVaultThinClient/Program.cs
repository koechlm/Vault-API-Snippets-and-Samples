using Autodesk.Connectivity.WebServices;
using Autodesk.Connectivity.WebServicesTools;
using Autodesk.DataManagement.Client.Framework.Vault.Currency.Entities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ACW = Autodesk.Connectivity.WebServices;
using Vault = Autodesk.DataManagement.Client.Framework.Vault;
using VDF = Autodesk.DataManagement.Client.Framework;


namespace Vault_API_Sample_NavigateToVaultThinClient
{
    class Program
    {
        static void Main(string[] args)
        {
            #region entity variables
            ACW.File file = null;
            ACW.Item item = null;
            ACW.ChangeOrder changeOrder = null;
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
                        long folderId = folder.Id;
                        // build the URL to navigate using a browser
                        string folderUrl = $"http://{server}/AutodeskTC/{vaultName}/explore/folder/{folderId}\r\n";
                        Console.WriteLine($"Folder URL: {folderUrl}");

                        // Open the folder URL in the default browser
                        System.Diagnostics.Process.Start(folderUrl);

                        Console.WriteLine($"Navigated to folder '{folderFullName}' in Vault Thin Client. Press Enter to continue...");
                        Console.ReadLine();
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
                        long fileMasterId = file.MasterId;
                        string fileUrl = $"http://{server}/AutodeskTC/{vaultName}/explore/file/{fileMasterId}\r\n";
                        Console.WriteLine($"File URL: {fileUrl}");

                        // Open the file URL in the default browser
                        System.Diagnostics.Process.Start(fileUrl);

                        Console.WriteLine($"Navigated to file '{fileName}' in Vault Thin Client. Press Enter to continue...");
                        Console.ReadLine();

                        long fileId = file.Id;
                        string fileVersionUrl = $"http://{server}/AutodeskTC/{vaultName}/explore/fileversion/{fileId}\r\n";
                        Console.WriteLine($"File Version URL: {fileVersionUrl}");

                        // Open the file version URL in the default browser
                        System.Diagnostics.Process.Start(fileVersionUrl);

                        Console.WriteLine($"Navigated to file version of '{fileName}' in Vault Thin Client. Press Enter to continue...");
                        Console.ReadLine();

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
                            long itemMasterId = item.MasterId;
                            string itemUrl = $"http://{server}/AutodeskTC/{vaultName}/items/item/{itemMasterId}\r\n";
                            Console.WriteLine($"Item URL: {itemUrl}");

                            // Open the item URL in the default browser
                            System.Diagnostics.Process.Start(itemUrl);
                            Console.WriteLine($"Navigated to item '{itemNumber}' in Vault Thin Client. Press Enter to continue...");
                            Console.ReadLine();

                            long itemId = item.Id;
                            string itemVersionUrl = $"http://{server}/AutodeskTC/{vaultName}/items/itemversion/{itemId}\r\n";
                            Console.WriteLine($"Item Version URL: {itemVersionUrl}");

                            // Open the item version URL in the default browser
                            System.Diagnostics.Process.Start(itemVersionUrl);
                            Console.WriteLine($"Navigated to item version of '{itemNumber}' in Vault Thin Client. Press Enter to continue...");
                            Console.ReadLine();
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
                            long changeOrderId = changeOrder.Id;
                            string changeOrderUrl = $"http://{server}/AutodeskTC/{vaultName}/changeorders/changeorder/{changeOrderId}\r\n";
                            Console.WriteLine($"Change Order URL: {changeOrderUrl}");

                            // Open the change order URL in the default browser
                            System.Diagnostics.Process.Start(changeOrderUrl);
                            Console.WriteLine($"Navigated to change order '{changeOrderNumber}' in Vault Thin Client. Press Enter to continue...");
                            Console.ReadLine();
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error retrieving change order '{changeOrderNumber}': {ex.Message}");
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

    }
}
