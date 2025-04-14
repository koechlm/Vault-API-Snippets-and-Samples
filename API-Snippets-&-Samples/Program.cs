using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Autodesk.Connectivity.WebServices;
using Autodesk.Connectivity.WebServicesTools;

namespace API_Snippets___Samples
{
    class Program
    {
        static void Main(string[] args)
        {
            #region ConnectToVault
            ServerIdentities mServerId = new ServerIdentities();
            mServerId.DataServer = "serveraddress or name";           //replace value by command line args[i]
            mServerId.FileServer = "serveraddress or name";
            string mVaultName = "my Vault Name";
            string mUserName = "Administrator";
            string mPassword = "";
            LicensingAgent mLicAgent = LicensingAgent.Server;
            WebServiceManager mVault = null;
            UserPasswordCredentials mCred = null;

            try
            {
                mCred = new UserPasswordCredentials(mServerId, mVaultName, mUserName, mPassword, mLicAgent);
                mVault = new WebServiceManager(mCred);

                try
                {
                    //query data, create files, folders, items... etc. here
                }
                catch (Exception ex)
                {
                    throw ex;
                }
                //never forget to release the license, especially if pulled from Server
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
