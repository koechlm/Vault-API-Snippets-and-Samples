using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Autodesk.Connectivity.Extensibility.Framework;
using Autodesk.DataManagement.Client.Framework.Vault.Currency.Entities;
using Autodesk.Connectivity.JobProcessor.Extensibility;
using ACW = Autodesk.Connectivity.WebServices;
using VDF = Autodesk.DataManagement.Client.Framework;
using ACET = Autodesk.Connectivity.Explorer.ExtensibilityTools;

// *ComponentUpgradeEveryRelease-Client*
[assembly: ApiVersion("19.0")]
[assembly: ExtensionId("6b58ddf4-af34-47a0-8b64-9b498ccd0978")]


namespace API_SampleJob_UpdatePartNumberUDP
{
    public class JobExtension : IJobHandler
    {
        private static string JOB_TYPE = "MyCompany.JobName";

        #region IJobHandler Implementation
        public bool CanProcess(string jobType)
        {
            return jobType == JOB_TYPE;
        }

        public JobOutcome Execute(IJobProcessorServices context, IJob job)
        {
            try
            {
                // Correcting the usage of Params property to access the dictionary
                long fileId = Convert.ToInt64(job.Params["FileId"]);
                ACW.File mFile;
                mFile = context.Connection.WebServiceManager.DocumentService.GetFileById(fileId);
                VDF.Vault.Currency.Entities.FileIteration mFileIt = new VDF.Vault.Currency.Entities.FileIteration(context.Connection, mFile);

                // Create a dictionary of properties to update
                Dictionary<ACW.PropDef, object> mPropDictionary = new Dictionary<ACW.PropDef, object>();
                ACW.PropDef mPropDef = context.Connection.WebServiceManager.PropertyService
                    .GetPropertyDefinitionsByEntityClassId("FILE")
                    .FirstOrDefault(x => x.SysName == "PartNumber");

                if (mPropDef != null)
                {
                    mPropDictionary.Add(mPropDef, "New: " + DateTime.Now.ToString());

                    try
                    {
                        ACET.IExplorerUtil mExplUtil = ACET.ExplorerLoader.LoadExplorerUtil(context.Connection.Server, 
                            context.Connection.Vault, context.Connection.UserID, context.Connection.Ticket);

                        // Note: method UpdateFileProperties requires 2026 Update 1 for Vault Client installed
                        mExplUtil.UpdateFileProperties(mFile, mPropDictionary);
                    }
                    catch (Exception ex)
                    {
                        context.Log(ex, "Job-Template Job failed: " + ex.ToString() + " ");
                        return JobOutcome.Failure;
                    }

                }
                else
                {
                    return JobOutcome.Failure;
                }

                return JobOutcome.Success;
            }
            catch (Exception ex)
            {
                context.Log(ex, "Job-Template Job failed: " + ex.ToString() + " ");
                return JobOutcome.Failure;
            }
        }

        public void OnJobProcessorShutdown(IJobProcessorServices context)
        {
            //throw new NotImplementedException();
        }

        public void OnJobProcessorSleep(IJobProcessorServices context)
        {
            //throw new NotImplementedException();
        }

        public void OnJobProcessorStartup(IJobProcessorServices context)
        {
            //throw new NotImplementedException();
        }

        public void OnJobProcessorWake(IJobProcessorServices context)
        {
            //throw new NotImplementedException();
        }
        #endregion IJobHandler Implementation
    }
}
