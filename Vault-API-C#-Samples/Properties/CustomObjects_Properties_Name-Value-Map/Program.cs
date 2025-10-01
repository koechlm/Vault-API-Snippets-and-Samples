using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Autodesk.Connectivity.WebServices;
using Autodesk.Connectivity.WebServicesTools;
using Autodesk.DataManagement.Client.Framework.Vault.Currency.Properties;

namespace CustomObjects_Properties_Name_Value_Map
{
    class Program
    {
        static void Main(string[] args)
        {
            #region ConnectToVault
            ServerIdentities mServerId = new ServerIdentities();
            mServerId.DataServer = "10.49.87.121";           //replace value by command line args[i]
            mServerId.FileServer = "10.49.87.121";
            string mVaultName = "PDMC-Sample";
            string mUserName = "Administrator";
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
                    // create a list of custom object ids to retrieve
                    List<string> customObjects = new List<string>
                        {
                            "WG110000036",
                            "WG110000037",
                            "WG110000038"
                        };

                    CustEnt[] custEnts = mVault.CustomEntityService.FindCustomEntitiesByNumbers(customObjects.ToArray());
                    List<long> custEntIds = custEnts.Select(ce => ce.Id).ToList();

                    Dictionary<string, object> nameValueMap = new Dictionary<string, object>();
                    Dictionary<string, Dictionary<string, object>> customObjectNameValueMap = new Dictionary<string, Dictionary<string, object>>();

                    PropDef[] mPropDefs = mVault.PropertyService.GetPropertyDefinitionsByEntityClassId("CUSTENT");
                    PropInst[] mPropInsts = mVault.PropertyService.GetPropertiesByEntityIds("CUSTENT", custEntIds.ToArray());

                    foreach (long custEntId in custEntIds)
                    {
                        nameValueMap = new Dictionary<string, object>();
                        foreach (PropDef propDef in mPropDefs)
                        {
                            PropInst propInst = mPropInsts.FirstOrDefault(pi => pi.PropDefId == propDef.Id && pi.EntityId == custEntId);
                            if (propInst != null)
                            {
                                nameValueMap.Add(propDef.DispName, propInst.Val);
                            }
                        }
                        customObjectNameValueMap.Add(custEnts.FirstOrDefault(ce => ce.Id == custEntId).Num, nameValueMap);
                    }

                    // output the name-value map for each custom object
                    foreach (var kvp in customObjectNameValueMap)
                    {
                        Console.WriteLine($"Custom Object: {kvp.Key}");
                        foreach (var propKvp in kvp.Value)
                        {
                            Console.WriteLine($"\t{propKvp.Key}: {propKvp.Value}");
                        }
                    }

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
