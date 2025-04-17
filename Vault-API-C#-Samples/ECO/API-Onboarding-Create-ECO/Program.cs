using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Autodesk.Connectivity.WebServices;
using Autodesk.Connectivity.WebServicesTools;

namespace API_Onboarding_Create_ECO
{
    class Program
    {
        static void Main(string[] args)
        {
            #region ConnectToVault
            ServerIdentities mServerId = new ServerIdentities();
            mServerId.DataServer = "localhost";           //replace value by command line args[i]
            mServerId.FileServer = "localhost";
            string mVaultName = "Vault";
            string mUserName = "Administrator";
            string mPassword = "";

            // select the matching licensing agent 
            LicensingAgent mLicAgent = LicensingAgent.Client;

            WebServiceManager mVault = null;
            UserPasswordCredentials mCred = null;

            // create user credentials
            mCred = new UserPasswordCredentials(mServerId, mVaultName, mUserName, mPassword, mLicAgent);
            // create a new WebServiceManager object (= login)
            mVault = new WebServiceManager(mCred);

            // Set Reference to ChangeOrderService
            ChangeOrderService mCoSrvc = mVault.ChangeOrderService;
            // Get Default ECO Workflow
            Workflow mCoWflow = mCoSrvc.GetDefaultWorkflow();

            // Get Default ECO Routing
            Routing mCoRouting = mCoSrvc.GetRoutingsByWorkflowId(mCoWflow.Id).Where(o => o.IsDflt).FirstOrDefault();

            // Get Default ECO Numbering Scheme
            NumSchm mCoNumSchm = mVault.NumberingService.GetNumberingSchemes("CO", NumSchmType.SystemDefault).Where(o => o.IsDflt).FirstOrDefault();

            // Create a new number
            string mNewNumber = mVault.NumberingService.GenerateNumberBySchemeId(mCoNumSchm.SchmID, null);

            // set the ECO title and description
            string mCoTitle = "C# Sample";
            string mCoDescr = "Change Order Automation Sample";

            // Set Due date to next month
            DateTime mDueDate = DateTime.Now.AddMonths(1);

            // add entities to the Records tab; item or file master ids
            List<long> mItemIds = new List<long>();
            List<Item> mItems = new List<Item>();

            if (mItemIds.Count > 0)
            {
                foreach (var mItem in mItems)
                {
                    mItemIds.Add(mItem.Id);
                }
            }

            // add entities to the Files tab
            List<long> mFileIds = new List<long>();

            // add entities to the Attachments tab
            List<long> mFileAttmtsIds = new List<long>();

            // add properties to the Change Order
            List<PropInst> mCoProps = new List<PropInst>();

            // add properties to the Change Order Item Links
            List<AssocPropItem> mCoItemAssocProps = new List<AssocPropItem>();

            // add comments to the Change Order
            List<MsgGroup> mCoComments = new List<MsgGroup>();

            // add emails to the Change Order
            List<Email> mCoEmails = new List<Email>();

            // create Change Order in Vault
            ChangeOrder mChangeOrder = mCoSrvc.AddChangeOrder(mCoRouting.Id, mNewNumber, mCoTitle, mCoDescr, mDueDate, mItemIds.ToArray(), mFileAttmtsIds.ToArray(), mFileIds.ToArray(), mCoProps.ToArray(), mCoItemAssocProps.ToArray(), mCoComments.ToArray(), mCoEmails.ToArray());

            // never forget to release the license, especially if pulled from Server
            mVault.Dispose();

            #endregion connect to Vault
        }
    }
}
