using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Autodesk.Connectivity.WebServices;
using Autodesk.Connectivity.WebServicesTools;

namespace Vault_API_Sample_AutodeskAccountLogin
{
    class Program
    {
        static void Main(string[] args)
        {
            #region ConnectToVault using Autodesk Account
            
            // NOTE - click licensing v9 requires AdskLicensingSDK_9.dll, AdskIdentitySDK.config, 
            // and AdskIdentitySDK.dll to be available in the application directory
            
            ServerIdentities mServerId = new ServerIdentities();
            mServerId.DataServer = "localhost";
            mServerId.FileServer = "localhost";
            string mVaultName = "PDMC-Sample";
            
            WebServiceManager webServiceManager = null;
            AutodeskAuthCredentials mAdskAccntCred = null;

            try
            {
                // Promt user to press enter to continue; this allows time to attach a debugger if needed before the Autodesk Account login dialog appears
                Console.WriteLine("Time to connect your debugger. Press Enter to start login...");
                Console.ReadLine();

                // Prompt user to log in with Autodesk Account
                Console.WriteLine("Logging in with Autodesk Account...");
                
                // AutodeskAccount is a static class, call LogIn to get IAutodeskAccount
                IAutodeskAccount adskAccount = AutodeskAccount.Login(IntPtr.Zero);
                
                if (!adskAccount.IsLoggedIn || !adskAccount.IsLoginValid)
                {
                    Console.WriteLine("Autodesk Account login was cancelled or failed.");
                    return;
                }
                
                Console.WriteLine($"Autodesk Account logged in successfully.");
                
                // Get the access token
                string tokenId = adskAccount.GetAccessToken();
                
                // Create credentials using Autodesk Account
                // Constructor: AutodeskAuthCredentials(ServerIdentities, string vaultName, bool useAcceleratorService, IAutodeskAccount)
                mAdskAccntCred = new AutodeskAuthCredentials(
                    mServerId, 
                    mVaultName, 
                    false,  // useAcceleratorService
                    adskAccount
                );
                
                // Create WebServiceManager with Autodesk Account credentials
                webServiceManager = new WebServiceManager(mAdskAccntCred);
                
                Console.WriteLine("Connected to Vault successfully.");

                try
                {
                    // Example: Search for a file by name using the API, results should be reliable regardless of indexing status when using Autodesk Account login
                    string mSearchString = "Car Seat.iam";
                    
                    // Build the search condition
                    SrchCond srchCond = new SrchCond();
                    PropDef[] propDefs = webServiceManager.PropertyService.GetPropertyDefinitionsByEntityClassId("FILE");
                    PropDef propDef = propDefs.Where(pd => pd.SysName == "Name").FirstOrDefault();
                    
                    if (propDef != null)
                    {
                        srchCond.PropDefId = propDef.Id;
                        srchCond.SrchOper = 3; // Contains
                        srchCond.SrchTxt = mSearchString;
                        srchCond.PropTyp = PropertySearchType.SingleProperty;
                        srchCond.SrchRule = SearchRuleType.Must;
                        
                        SrchStatus searchStatus = null;
                        SrchSort srchSort = new SrchSort();
                        string bookmark = string.Empty;
                        List<File> resultAll = new List<File>();
                        
                        // Get root folder
                        Folder rootFolder = webServiceManager.DocumentService.GetFolderRoot();
                        
                        // Perform search
                        while (searchStatus == null || resultAll.Count < searchStatus.TotalHits)
                        {
                            File[] resultPage = webServiceManager.DocumentService.FindFilesBySearchConditions(
                                new SrchCond[] { srchCond },
                                new SrchSort[] { srchSort },
                                new long[] { rootFolder.Id },
                                true,
                                true,
                                ref bookmark,
                                out searchStatus
                            );
                            
                            // Check the indexing status
                            if (searchStatus.IndxStatus == IndexingStatus.IndexingComplete || 
                                searchStatus.IndxStatus == IndexingStatus.IndexingContent)
                            {
                                // Index is complete or being indexed, results are reliable
                            }
                            
                            if (resultPage != null && resultPage.Length > 0)
                            {
                                resultAll.AddRange(resultPage);
                                Console.WriteLine($"Found {resultPage.Length} files in this page. Total so far: {resultAll.Count}");
                            }
                            else
                            {
                                break;
                            }
                            
                            // Limit to first page for this example
                            break;
                        }
                        
                        Console.WriteLine($"Search completed. Total files found: {resultAll.Count}");
                        
                        // Display results
                        foreach (File file in resultAll)
                        {
                            Console.WriteLine($"  - {file.Name} (Version {file.VerNum})");
                        }
                    }
                    else
                    {
                        Console.WriteLine("Could not find 'Name' property definition.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during Vault operations: {ex.Message}");
                    throw;
                }
                
                // Don't forget to release the connection and return the license
                webServiceManager.Dispose();
                Console.WriteLine("Vault connection disposed.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fatal error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
            finally
            {
                Console.WriteLine("\nPress any key to exit...");
                Console.ReadKey();
            }
            
            #endregion connect to Vault
        }
    }
}
