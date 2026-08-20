using Autodesk.Connectivity.WebServices;
using Autodesk.Connectivity.WebServicesTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


ServerIdentities mServerId = new ServerIdentities
{
    DataServer = "localhost",       //replace value by your server address or name
    FileServer = "localhost"
};
string mVaultName = "PDMC-SW-Sample";
string mUserName = "Administrator";
string mPassword = "";
LicensingAgent mLicAgent = LicensingAgent.Client; //Adjust based on your available license type
WebServiceManager? mVault = null;
UserPasswordCredentials? mCred = null;

try
{
    mCred = new UserPasswordCredentials(mServerId, mVaultName, mUserName, mPassword, mLicAgent);
    mVault = new WebServiceManager(mCred);

    try
    {
        //query data, create files, folders, items... etc. here
        Console.WriteLine("Connected to Vault successfully.");

        // attach to the process for debugging; press Enter to continue once you have attached the debugger
        Console.WriteLine("Press Enter to continue after attaching the debugger.");
        Console.ReadLine();

        // Example: Get the list of all entity attributes in the Vault
        string mNameSpace = "FLC.ITEM";
        EntAttr[] allAttributs = mVault.PropertyService.FindAllEntityAttributes(mNameSpace);
        // Print the list of entity attributes
        Console.WriteLine($"List of entity attributes in the Vault for namespace '{mNameSpace}':");
        foreach (EntAttr attr in allAttributs)
        {
            Console.WriteLine($"- {attr.Attr}");
            Console.WriteLine($"-Value: {attr.Val}");
        }

        // Example: Get the list of entity attributes for a specific file in the Vault
        Autodesk.Connectivity.WebServices.File? file = mVault.DocumentService.FindLatestFilesByPaths(new string[] { "$/Designs/Solidworks Sample Data/01-0995.SLDDRW" }).FirstOrDefault();
        if (file != null)
        {
            EntAttr[] mEntAttribs = mVault.PropertyService.GetEntityAttributes(file.Id, mNameSpace);
            // Print the list of entity attributes for the specific file
            Console.WriteLine($"List of entity attributes for file '{file.Name}' in the Vault for namespace '{mNameSpace}':");
            foreach (EntAttr attr in mEntAttribs)
            {
                Console.WriteLine($"- {attr.Attr}");
                Console.WriteLine($"-Value: {attr.Val}");
            }
        }

        Console.WriteLine("We are done: Press Enter to exit.");
        // keep the console window open
        Console.ReadLine();
    }
    catch (Exception)
    {
        throw;
    }
    // release the license
    mVault.Dispose();
}
catch (Exception)
{
    throw;
}
