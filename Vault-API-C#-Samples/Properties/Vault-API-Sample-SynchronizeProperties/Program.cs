using System;
using System.Linq;
using ACW = Autodesk.Connectivity.WebServices;
using Autodesk.Connectivity.WebServicesTools;
using Vault = Autodesk.DataManagement.Client.Framework.Vault;
using System.Collections.Generic;

// Synchronize properties sample helper class

namespace Vault_API_Sample_ManageProperties
{
    class Program
    {
        static void Main(string[] args)
        {
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
            #endregion connect to Vault

            try
            {
                webServiceManager = connection.WebServiceManager;

                // Get server and vault name from the connection
                string server = connection.Server;
                string vaultName = connection.Vault;

                Console.WriteLine($"Connected to Vault: {vaultName} on Server: {server}");
                Console.WriteLine();

                // Prompt user for entity identifiers

                try
                {
                    // prompt the user to input a Vault file to be updated later in the code; share a sample file path like the one below to make it easier for the user to find a file that works with the sample; if the file is not found or the user does not have permissions to access it, the sample will exit gracefully with a message
                    // the sample Vault has a file named "Hairdryer.ipt" in the folder "$/Designs/Inventor Sample Data/Models/Parts/Hairdryer", the full path to the file is "$/Designs/Inventor Sample Data/Models/Parts/Hairdryer/Hairdryer.ipt"
                    string filePath = "";
                    // get the file path from the user input; if the user just presses enter, use the default sample file path
                    Console.WriteLine("Enter the full path of the file to synchronize properties on (press Enter to use default sample file '$/Designs/Inventor Sample Data/Models/Parts/Hairdryer/Hairdryer.ipt'):");
                    filePath = Console.ReadLine();

                    ACW.File file = webServiceManager.DocumentService.FindLatestFilesByPaths(new string[] { filePath }).FirstOrDefault();

                    if (file.Id == -1)
                    {
                        Console.WriteLine("File not found");
                        return;
                    }

                    Console.WriteLine($"Found file: {file.Name} (ID: {file.Id})");
                    Console.WriteLine();

                    // read the options for date and bool conversion from the Vault settings; both settings store 0/1 values, so we check if the value is 1 to determine if the option is enabled or not; if the options are enabled, we will convert dates to date-only values and bools to integers during the property sync process; if the options are disabled, we will sync properties with their full date/time values and bool values as true/false
                    bool dateOnly = webServiceManager.KnowledgeVaultService.GetVaultOption("Autodesk.EDM.UpdateProperties.DateMappingOption") == "1";
                    bool boolAsInt = webServiceManager.KnowledgeVaultService.GetVaultOption("Autodesk.EDM.UpdateProperties.WriteBoolPropertyAsN") == "1";

                    // Initialize PropertySync helper class, leverage date and bool conversion options if needed by setting the dateOnly and boolAsInt parameters in the constructor;
                    ManageProperties manageProps = new ManageProperties(connection, dateOnly, boolAsInt);
                    Dictionary<string, string> newPropValues = null;

                    newPropValues = new Dictionary<string, string>()
                    {
                        { "Document Code (DCC)", DateTime.Now.ToLongTimeString() },
                        { "Volume", DateTime.Now.ToShortTimeString() }
                    };

                    manageProps.UpdateProperties(file, newPropValues);

                    return;

                    // Prompt to update property values before synchronization; this allows the user to test the property sync with custom values if desired; if the user just presses enter, the sample will proceed with default property values defined in the ManageProperties class
                    Console.WriteLine("Do you want to update property values before synchronization? (y/n, press Enter for default 'n')");
                    string updatePropertiesInput = Console.ReadLine();
                    if (updatePropertiesInput.ToLower() == "y")
                    {
                        newPropValues = propertiesToUpdate();
                    }

                    // Synchronize properties from CAD file to Vault
                    ACW.PropWriteResults writeResults;
                    string[] cloakedEntityClasses;

                    Console.WriteLine("Synchronizing properties...");
                    
                    ACW.File updatedFile = manageProps.SyncProperties(
                        file,
                        "Property sync via API sample",
                        allowSync: true,
                        out writeResults,
                        out cloakedEntityClasses,
                        force: false,  // set to true to force sync even if no compliance failures exist
                        overridePropValues: newPropValues  // optionally pass in custom property values to override values read from the CAD file during sync; this allows you to test the sync with different values without having to change the values in the CAD file and re-upload each time
                    );

                    if (updatedFile.Id == file.Id && updatedFile.VerNum == file.VerNum)
                    {
                        Console.WriteLine("No property sync was needed - properties are already up to date.");
                    }
                    else
                    {
                        Console.WriteLine($"Property sync completed successfully!");
                        Console.WriteLine($"New version: {updatedFile.VerNum}");
                        
                        if (writeResults != null)
                        {
                            Console.WriteLine($"Properties written: {writeResults.Results?.Length ?? 0}");
                        }
                    }

                    if (cloakedEntityClasses != null && cloakedEntityClasses.Length > 0)
                    {
                        Console.WriteLine($"Warning: Insufficient permissions for entity classes: {string.Join(", ", cloakedEntityClasses)}");
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

        private static Dictionary<string, string> propertiesToUpdate()
        {
            Dictionary<string, string> overridePropValues = new Dictionary<string, string>();
            Console.WriteLine("Enter property values to override during sync (press Enter to skip a property):");
            Console.Write("Title: ");
            string titleValue = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(titleValue))
            {
                overridePropValues["Title"] = titleValue;
            }
            Console.Write("Part Number: ");
            string partNumberValue = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(partNumberValue))
            {
                overridePropValues["Part Number"] = partNumberValue;
            }
            return overridePropValues;
        }
    }
}
