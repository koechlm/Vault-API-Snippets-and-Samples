using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Autodesk.Connectivity.WebServices;
using Autodesk.Connectivity.WebServicesTools;

namespace API_ConsoleApp_UpdateItemFileAssociations
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

                try
                {
                    //query data, create files, folders, items... etc. here
                    Item item = mVault.ItemService.GetLatestItemByItemNumber("ISO 10788-2 - 60 x 40 x 4");
                    item = mVault.ItemService.EditItems(new long[] { item.RevId }).FirstOrDefault();
                    ItemFileAssoc itemPrimAssoc = mVault.ItemService.GetItemFileAssociationsByItemIds(new long[] { item.Id }, ItemFileLnkTypOpt.Primary).FirstOrDefault();
                    ItemFileAssoc[] itemSecAssocs = mVault.ItemService.GetItemFileAssociationsByItemIds(new long[] { item.Id }, ItemFileLnkTypOpt.Secondary);
                    ItemFileAssoc[] itemStdAssocs = mVault.ItemService.GetItemFileAssociationsByItemIds(new long[] { item.Id }, ItemFileLnkTypOpt.StandardComponent);
                    List<long> secFileIds = itemSecAssocs.Select(n => n.CldFileId).ToList<long>();
                    List<long> stdFileIds = itemStdAssocs.Select(n => n.CldFileId).ToList<long>();
                    stdFileIds.AddRange(secFileIds);
                    long[] empty = new long[0];
                    item = mVault.ItemService.UpdateItemFileAssociations(item.RevId, itemPrimAssoc.CldFileId, false, empty, stdFileIds.ToArray(), empty, empty);
                    mVault.ItemService.UpdateAndCommitItems(new Item[] { item });

                    File file = mVault.DocumentService.GetFileById(itemPrimAssoc.CldFileId);
                    LfCycDef lfCycDef = mVault.LifeCycleService.GetAllLifeCycleDefinitions().Where(n => n.Name == "Flexible Release Process").FirstOrDefault();

                    mVault.DocumentServiceExtensions.UpdateFileLifeCycleStates(new long[] { file.MasterId }, new long[] { lfCycDef.StateArray.Where(n => n.Name == "Work in Progress").FirstOrDefault().Id }, "test routine");
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
