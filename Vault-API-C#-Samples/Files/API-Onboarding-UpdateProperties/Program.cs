using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Autodesk.Connectivity.WebServices;
using Autodesk.Connectivity.WebServicesTools;
using Autodesk.Connectivity.Extensibility.Framework;
using VDF = Autodesk.DataManagement.Client.Framework;
using ACET = Autodesk.Connectivity.Explorer.ExtensibilityTools;
using Autodesk.DataManagement.Client.Framework.Vault;
using Autodesk.DataManagement.Client.Framework.Vault.Forms;

namespace API_Onboarding_UpdateProperties
{
    class Program
    {
        private static VDF.Vault.Currency.Connections.Connection conn = null;
        private static WebServiceManager mWsMgr;

        static void Main(string[] args)
        {
            #region ConnectToVault
            // Connect to Vault using Vault Developer Framework
            conn = VDF.Vault.Forms.Library.Login(null);
            if (conn == null)
            {
                //Console.WriteLine("Connection to Vault failed.");
                return;
            }
            mWsMgr = conn.WebServiceManager;
            Console.WriteLine("Connected to Vault: " + conn.Vault);
            #endregion connect to Vault

            // Get the file to update properties; addjust the file path as needed
            string mVaultFullFileName = "$/Designs/Test.idw";

            List<string> mFiles = new List<string>();
            mFiles.Add(mVaultFullFileName);
            File mFile = mWsMgr.DocumentService.FindLatestFilesByPaths(mFiles.ToArray()).FirstOrDefault();
            VDF.Vault.Currency.Entities.FileIteration mFileIt = new VDF.Vault.Currency.Entities.FileIteration(conn, mFile);

            // Create a dictionary of properties to update
            Dictionary<PropDef, object> mPropDictionary = new Dictionary<PropDef, object>();
            PropDef mPropDef = mWsMgr.PropertyService.GetPropertyDefinitionsByEntityClassId("FILE").Where(x=>x.SysName == "PartNumber").FirstOrDefault();

            if (mPropDef != null)
            {
                mPropDictionary.Add(mPropDef, "New: " + DateTime.Now.ToString());
                bool mUpdateResult = mUpdateFileProperties(mFile, mPropDictionary);
                if (mUpdateResult)
                {
                    Console.WriteLine("File properties updated successfully.");
                }
                else
                {
                    Console.WriteLine("Failed to update file properties.");
                }
            }
            else
            {
                Console.WriteLine("Property definition not found.");
                return;
            }
        }

        private static bool mUpdateFileProperties(File File, Dictionary<PropDef, object> PropDictionary)
        {
            try
            {
                ACET.IExplorerUtil mExplUtil = ACET.ExplorerLoader.LoadExplorerUtil(
                                             conn.Server, conn.Vault, conn.UserID, conn.Ticket);
                mExplUtil.UpdateFileProperties(File, PropDictionary);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
