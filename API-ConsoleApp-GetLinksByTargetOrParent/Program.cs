using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Autodesk.Connectivity.WebServicesTools;
using ACW = Autodesk.Connectivity.WebServices;
using Vault = Autodesk.DataManagement.Client.Framework.Vault;
using VDF = Autodesk.DataManagement.Client.Framework;

namespace API_ConsoleApp_GetLinksByTargetOrParent
{
    class Program
    {
        static void Main(string[] args)
        {
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
                Console.WriteLine("We continue to ask for entity link information...");

                try
                {
                    // the PDMC-Sample Vault has a custom object Task "T-00001" with links to three files
                    // $/Designs/Inventor Sample Data/Inventor CAM/Overview of 3D Toolpaths.iam
                    // $/Designs/Standard/Hydraulic Systems/Power Units/W091902 - 00.iam
                    // $/Designs/SR-0003/CAx/Compensator_iLogicConfigurator.iam

                    // get the files by their paths, we need the file Ids to get the links; in case we only have the file names, we can also search for the files by name and get the Ids that way
                    ACW.File file1 = webServiceManager.DocumentService.FindLatestFilesByPaths(new string[] { "$/Designs/Inventor Sample Data/Inventor CAM/Overview of 3D Toolpaths.iam" }).FirstOrDefault();
                    // ACW.File file2 = webServiceManager.DocumentService.FindLatestFilesByPaths(new string[] { "$/Designs/Standard/Hydraulic Systems/Power Units/W091902-00.iam" }).FirstOrDefault();
                    // ACW.File file3 = webServiceManager.DocumentService.FindLatestFilesByPaths(new string[] { "$/Designs/SR-0003/CAx/Compensator_iLogicConfigurator.iam" }).FirstOrDefault();

                    // the result are links that point to the file1, file2, or file3 as target
                    var links1 = webServiceManager.DocumentService.GetLinksByTargetEntityIds(new long[] { file1.Id });

                    // share the resulting links and target entity information to the user
                    Console.WriteLine($"Links with target {file1.Name}:");
                    foreach (var link in links1)
                    {
                        Console.WriteLine($"Link Id: {link.Id}, Parent Id: {link.ParentId}, Target Id: {link.ToEntId}, Target Entity Type: {link.ToEntClsId}");
                    }

                    // var links2 = webServiceManager.DocumentService.GetLinksByTargetEntityIds(new long[] { file2.Id });
                    // var links3 = webServiceManager.DocumentService.GetLinksByTargetEntityIds(new long[] { file3.Id });

                    // the links retrieved target a custom object; in case we only have this entity as a starting point, we can get links by parent
                    // note - we need to add the target entityClass Id, in our sample "FILE", as the files are the targets of the links
                    ACW.CustEnt custEnt1 = webServiceManager.CustomEntityService.GetCustomEntitiesByIds(new long[] { links1[0].ParentId }).FirstOrDefault();
                    var linksByParent = webServiceManager.DocumentService.GetLinksByParentIds(new long[] { custEnt1.Id }, new string[] { "FILE" });

                    // share the resulting links and target entity information to the user
                    Console.WriteLine($"Links with parent {custEnt1.Name}:");
                    foreach (var link in linksByParent)
                    {
                        Console.WriteLine($"Link Id: {link.Id}, Parent Id: {link.ParentId}, Target Id: {link.ToEntId}, Target Entity Type: {link.ToEntClsId}");
                    }
                }
                catch (Exception ex)
                {
                    throw ex;
                }

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

        }
    }
}
