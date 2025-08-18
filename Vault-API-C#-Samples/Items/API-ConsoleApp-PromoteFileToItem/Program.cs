using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading.Tasks;

using Autodesk.Connectivity.WebServices;
using Autodesk.Connectivity.WebServicesTools;
using Autodesk.DataManagement.Client.Framework.Vault.Currency.Connections;
using VDF = Autodesk.DataManagement.Client.Framework;

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
            Connection mConn = null;

            try
            {
                mCred = new UserPasswordCredentials(mServerId, mVaultName, mUserName, mPassword, mLicAgent);
                mVault = new WebServiceManager(mCred);
                mConn = new Connection(mVault, mCred.VaultName, mVault.SecurityService.Session.User.Id, mServerId.DataServer, AuthenticationFlags.Standard);

                // Get the primary file iteration to assign/update item; addjust the file path as needed
                string mPrimaryFileName = "$/Designs/Test.ipt";
                File mPrimFile = mVault.DocumentService.FindLatestFilesByPaths((new List<string> { mPrimaryFileName }).ToArray()).FirstOrDefault();
                VDF.Vault.Currency.Entities.FileIteration mPrimFileIteration = new VDF.Vault.Currency.Entities.FileIteration(mConn, mPrimFile);

                // Get the secondary file iteration to assign/update item; addjust the file path as needed
                string mSecondaryFileName = "$/Designs/Test.doc";
                File mSecFile = mVault.DocumentService.FindLatestFilesByPaths((new List<string> { mSecondaryFileName }).ToArray()).FirstOrDefault();
                VDF.Vault.Currency.Entities.FileIteration mSecFileIteration = new VDF.Vault.Currency.Entities.FileIteration(mConn, mSecFile);

                ItemsAndFiles mPromoteResult = null;
                ItemAssignAll itemAssignAll = ItemAssignAll.No;

                bool mPromoteFailed = false;

                List<long> mFileIdsToPromote = new List<long>();
                mFileIdsToPromote.Add(mPrimFileIteration.EntityIterationId);


                // Promote the files to item(s)
                try
                {
                    mVault.ItemService.AddFilesToPromote(mFileIdsToPromote.ToArray(), itemAssignAll, true);
                    DateTime mTimeStamp = DateTime.Now;
                    GetPromoteOrderResults mPromoteOrderResults = mVault.ItemService.GetPromoteComponentOrder(out mTimeStamp);
                    if (mPromoteOrderResults.PrimaryArray != null && mPromoteOrderResults.PrimaryArray.Length != 0)
                    {
                        try
                        {
                            mVault.ItemService.PromoteComponents(mTimeStamp, mPromoteOrderResults.PrimaryArray);
                        }
                        catch (Exception)
                        {
                            mPromoteFailed = true;
                        }
                    }
                    if (mPromoteOrderResults.NonPrimaryArray != null && mPromoteOrderResults.NonPrimaryArray.Length != 0)
                    {
                        try
                        {
                            mVault.ItemService.PromoteComponents(mTimeStamp, mPromoteOrderResults.NonPrimaryArray);
                        }
                        catch (Exception)
                        {
                            mPromoteFailed = true;
                        }
                    }

                    // process the results of the given timestamp/promote operation
                    try
                    {
                        if (mPromoteFailed != true)
                        {
                            mPromoteResult = mVault.ItemService.GetPromoteComponentsResults(mTimeStamp);
                            if (mPromoteResult.ItemRevArray.FirstOrDefault().Locked != true)
                            {
                                Item[] mUpdatedItems = mPromoteResult.ItemRevArray;
                                Item mCurrentItem = mUpdatedItems.FirstOrDefault();
                                List<Item> mItemsToUpdate = new List<Item>();
                                mItemsToUpdate.Add(mCurrentItem);
                                mVault.ItemService.UpdateAndCommitItems(mItemsToUpdate.ToArray());
                            }
                            else
                            {
                                // feedback that the current item assignable already exists and is locked by another process
                            }
                        }
                    }
                    catch (Exception)
                    {

                    }
                }
                catch (Exception)
                {

                }


                mFileIdsToPromote.Add(mSecFileIteration.EntityIterationId);

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
