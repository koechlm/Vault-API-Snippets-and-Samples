using ACW = Autodesk.Connectivity.WebServices;
using ACET = Autodesk.Connectivity.Explorer.ExtensibilityTools;
using ACWTools = Autodesk.Connectivity.WebServicesTools;
using VDF = Autodesk.DataManagement.Client.Framework;
using Vault = Autodesk.DataManagement.Client.Framework.Vault;
using Forms = Autodesk.DataManagement.Client.Framework.Vault.Forms;
using Autodesk.Connectivity.WebServicesTools;

namespace API_FormsApp_UpdateProperties
{
    public partial class Form1 : Form
    {
        private static Vault.Currency.Connections.Connection? conn = null;
        private static WebServiceManager? mWsMgr;

        public Form1()
        {
            InitializeComponent();

            //Initialize the Vault Forms Library
            Forms.Library.Initialize();
        }

        private void button1_Click(object sender, EventArgs e)
        {
           conn = Vault.Forms.Library.Login(null);

            if (conn == null)
            {
                Console.WriteLine("Connection to Vault failed.");
                return;
            }
            Console.WriteLine("Connected to Vault: " + conn.Vault);

            mWsMgr = conn.WebServiceManager;
            Console.WriteLine("Connected to Vault: " + conn.Vault);


            // Get the file to update properties; addjust the file path as needed
            string mVaultFullFileName = "$/Designs/Test.idw";

            List<string> mFiles = new List<string>();
            mFiles.Add(mVaultFullFileName);
            ACW.File? mFile = mWsMgr.DocumentService.FindLatestFilesByPaths(mFiles.ToArray()).FirstOrDefault();
            VDF.Vault.Currency.Entities.FileIteration mFileIt = new VDF.Vault.Currency.Entities.FileIteration(conn, mFile);

            // Create a dictionary of properties to update
            Dictionary<ACW.PropDef, object> mPropDictionary = new Dictionary<ACW.PropDef, object>();
            ACW.PropDef? mPropDef = mWsMgr.PropertyService.GetPropertyDefinitionsByEntityClassId("FILE").Where(x => x.SysName == "PartNumber").FirstOrDefault();

            if (mFile != null && mPropDef != null)
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

            Vault.Library.ConnectionManager.CloseAllConnections();
        }

        private static bool mUpdateFileProperties(ACW.File File, Dictionary<ACW.PropDef, object> PropDictionary)
        {
            try
            {
                if (conn != null)
                {
                    ACET.IExplorerUtil mExplUtil = ACET.ExplorerLoader.LoadExplorerUtil(
                             conn.Server, conn.Vault, conn.UserID, conn.Ticket);
                    mExplUtil.UpdateFileProperties(File, PropDictionary);
                    return true;
                }
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
