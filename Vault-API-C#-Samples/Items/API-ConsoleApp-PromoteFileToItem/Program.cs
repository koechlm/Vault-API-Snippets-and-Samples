using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Autodesk.Connectivity.WebServices;
using Autodesk.Connectivity.WebServicesTools;

namespace API_ConsoleApp_PromoteFileToItem
{
    class Program
    {
        static void Main(string[] args)
        {
            #region ConnectToVault
            ServerIdentities mServerId = new ServerIdentities();
            mServerId.DataServer = "localhost";           //replace value by command line args[i]
            mServerId.FileServer = "localhost";
            string mVaultName = "PDMC-Sample";
            string mUserName = "CAD Admin";
            string mPassword = "";
            LicensingAgent mLicAgent = LicensingAgent.Client;
            WebServiceManager mVault = null;
            UserPasswordCredentials mCred = null;

            try
            {
                mCred = new UserPasswordCredentials(mServerId, mVaultName, mUserName, mPassword, mLicAgent);
                mVault = new WebServiceManager(mCred);

                

                mVault.Dispose();
            }
            catch (Exception ex)
            {
                throw ex;
            }
            #endregion connect to Vault
        }
    }
}
