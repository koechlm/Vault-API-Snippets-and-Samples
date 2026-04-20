using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ACW = Autodesk.Connectivity.WebServices;
using Autodesk.Connectivity.WebServicesTools;
using Vault = Autodesk.DataManagement.Client.Framework.Vault;

namespace API_ConsoleApp_CreateFolders
{
    class Program
    {
        static void Main(string[] args)
        {
            #region entity variables
            ACW.Folder folder = null;
            ACW.Folder parentFolder = null;
            ACW.Folder catFolder = null;

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

                // we need to get the root folder of the vault to create a new folder
                parentFolder = webServiceManager.DocumentService.GetFolderByPath("$/");
                // create a new folder under the root folder
                folder = webServiceManager.DocumentService.AddFolder("My New Folder", parentFolder.Id, isLibrary: false);

                // create a new category folder under the root folder; we use the default project category for this example.
                long catId = webServiceManager.CategoryService.GetCategoriesByEntityClassId("FLDR", true).FirstOrDefault(c => c.SysName == "Project").Id;
                catFolder = webServiceManager.DocumentServiceExtensions.AddFolderWithCategory("My New Project", parentFolder.Id, isLibrary: false, catId);

                Console.WriteLine($"Created folder: {folder.Name} with id: {folder.Id}");
                Console.WriteLine($"Created category folder: {catFolder.Name} with id: {catFolder.Id}");

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
