using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ACW = Autodesk.Connectivity.WebServices;
using Autodesk.Connectivity.WebServicesTools;
using VDF = Autodesk.DataManagement.Client.Framework;
using Vault = Autodesk.DataManagement.Client.Framework.Vault;
using Forms = Autodesk.DataManagement.Client.Framework.Vault.Forms;

namespace Vault_API_Sample_LogOnUsingDialog
{
    class Program
    {
        static void Main(string[] args)
        {
            string server = null;
            string vaultName = null;

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
                server = connection.Server;
                vaultName = connection.Vault;

                Console.WriteLine($"Connected to Vault: {vaultName} on Server: {server}");
                Console.WriteLine();


                try
                {
                    //query data, create files, folders, items... etc. here
                }
                catch (Exception ex)
                {
                    throw ex;
                }
                //never forget to release the license, especially if pulled from Server
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                // prompt the user to log out
                Console.WriteLine("\nPress Enter to log out...");
                Console.ReadLine();

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
