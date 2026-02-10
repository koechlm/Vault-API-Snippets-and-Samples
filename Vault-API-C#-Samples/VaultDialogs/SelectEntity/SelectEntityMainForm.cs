using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using ACW = Autodesk.Connectivity.WebServices;
using ACWTools = Autodesk.Connectivity.WebServicesTools;
using VDF = Autodesk.DataManagement.Client.Framework;
using VdfVault = Autodesk.DataManagement.Client.Framework.Vault;
using VdfVaultForms = Autodesk.DataManagement.Client.Framework.Vault.Forms;

using DevExpress.Skins;
using DevExpress.LookAndFeel;
using DevExpress.Utils;
using Autodesk.DataManagement.Client.Framework.Vault.Forms.Settings;
using DevExpress.Mvvm.Native;
using Autodesk.DataManagement.Client.Framework.Vault.Forms.Results;
using Autodesk.DataManagement.Client.Framework.Forms.Interfaces;
using DevExpress.XtraPrinting.Native;
using DevExpress.CodeParser;

namespace SelectEntity
{
    public partial class SelectEntityMainForm : DevExpress.XtraEditors.XtraForm
    {
        public static Settings mSettings = null;

        private VDF.Vault.Currency.Connections.Connection conn = null;

        private object mDefaultHelpProvider = Autodesk.DataManagement.Client.Framework.Forms.Library.ApplicationConfiguration.CustomHelpProvider;
        internal static mHelpProvider mCustomHelpProvider = null;

        public SelectEntityMainForm()
        {
            InitializeComponent();

            //apply the theme of the settings file  
            mSettings = Settings.Load();

            //Initialize the Vault Forms library
            VDF.Forms.Library.Initialize();

            // register the VDF custom skins (Light and Dark)
            VDF.Forms.SkinUtils.CustomThemeSkins.Register();

            if (mSettings.Theme == "Light")
            {
                this.LookAndFeel.SetSkinStyle(VDF.Forms.SkinUtils.CustomThemeSkins.LightThemeName);
            }
            else
            {
                this.LookAndFeel.SetSkinStyle(VDF.Forms.SkinUtils.CustomThemeSkins.DarkThemeName);
                // apply this theme to all VDF dialogs
                VDF.Forms.SkinUtils.WinFormsTheme.Instance.ApplyThemeToForm(this);
            }

            // register a custom help provider
            mCustomHelpProvider = new mHelpProvider();           

        }

        private void btnLogin_Click(object sender, EventArgs e)
        {
            LoginSettings loginSettings = new LoginSettings();
            this.conn = VDF.Vault.Forms.Library.Login(loginSettings);
            if (conn != null)
            {
                btnSelectEntity.Enabled = true;
            }
            else
            {
                btnSelectEntity.Enabled = false;
            }
        }

        /// <summary>
        /// Property representing the current user's Vault connection state; returns true, if current user is logged in.
        /// </summary>
        public bool LoggedIn
        {
            get
            {
                if (conn != null)
                {
                    // enables the Select button

                    return true;
                }
                return false;
            }
        }

        private void SelectFiles_FormClosing(object sender, FormClosingEventArgs e)
        {
            Autodesk.DataManagement.Client.Framework.Forms.Library.ApplicationConfiguration.CustomHelpProvider = (ICustomHelpProvider)mDefaultHelpProvider;
            VDF.Vault.Library.ConnectionManager.CloseAllConnections();
        }

        private void btnSelectEntity_Click(object sender, EventArgs e)
        {
            if (LoggedIn)
            {
                // prepare initial folder
                VDF.Vault.Currency.Entities.Folder mLastFldr = null;

                // prepare last used entity type filter
                string mLastEntityFilter = null;


                // create and set the select from Vault settings
                VDF.Vault.Forms.Results.SelectEntityResults mSelectionResult = new();
                VDF.Vault.Forms.Settings.SelectEntitySettings mSelectFileSettings = new();
                mSelectFileSettings.DialogCaption = "Select an Inventor File...";
                mSelectFileSettings.InitialBrowseLocation = null; //mLastFldr;
                mSelectFileSettings.ActionableEntityClassIds.Add("FILE");
                mSelectFileSettings.MultipleSelect = false;
                mSelectFileSettings.ActionButtonNavigatesContainers = true;
                mSelectFileSettings.ClearTextBoxOnNonActionableEntity = false;
                mSelectFileSettings.ActionButtonEnablementRule = VDF.Vault.Forms.Settings.SelectEntitySettings.ActionButtonEnablementRules.MustExist;
                mSelectFileSettings.ShowFolderView = true;
                mSelectFileSettings.ConfigureRevisions(true, true, true);
                mSelectFileSettings.SelectionTextLabel = "Inventor Component";
                mSelectFileSettings.PersistenceKey = "API-Sample-SelectEntity";
                mSelectFileSettings.ClearTextBoxOnNonActionableEntity = true;

                mSelectFileSettings.OptionsExtensibility.DoSearch = mSearch();
                mSelectFileSettings.OptionsExtensibility.CreateFolder = mCreateFolder =>
                {
                    MessageBox.Show("Create Folder clicked");
                    return null;
                };

                mSelectFileSettings.HelpContext = "Select Files";

                // set Filters for IPT/IAM
                List<VDF.Vault.Forms.Settings.SelectEntitySettings.EntityRegularExpressionFilter> mFilters = new List<SelectEntitySettings.EntityRegularExpressionFilter>();
                mFilters.Add(new SelectEntitySettings.EntityRegularExpressionFilter(
                    "Inventor Components (*.ipt; *.iam)", @"^.*\.(iam|ipt)$", VDF.Vault.Currency.Entities.EntityClassIds.Files, SelectEntitySettings.EntityFilter.MultiSelectFlag.Disabled));
                mFilters.Add(new SelectEntitySettings.EntityRegularExpressionFilter(
                    "Inventor Assenbly (*.iam)", @"^.*\.iam$", VDF.Vault.Currency.Entities.EntityClassIds.Files));
                mFilters.Add(new SelectEntitySettings.EntityRegularExpressionFilter(
                    "Inventor Part (*.ipt)", @"^.*\.ipt$", VDF.Vault.Currency.Entities.EntityClassIds.Files));
                mSelectFileSettings.ConfigureFilters("File Types", mFilters, mFilters.Where(n => n.DisplayName == mLastEntityFilter).FirstOrDefault());

                // configure the selection validation (e.g., = keep the dialog open, if selection is not IPT or IAM
                mSelectFileSettings.OptionsExtensibility.Validate = mVaultSlctVldtn;

                // Show the dialog
                mSelectionResult = Autodesk.DataManagement.Client.Framework.Vault.Forms.Library.SelectEntity(conn, mSelectFileSettings);

                // Handle the cancel event
                if (mSelectionResult == null || String.IsNullOrEmpty(mSelectionResult.SelectionText))
                {
                    return;
                }


            }
        }

        private Action<IEnumerable<string>, bool, Action<IEnumerable<VDF.Vault.Currency.Entities.IEntity>>> mSearch()
        {
            return (searchTerms, includeHidden, callback) =>
            {
                // Implement custom search logic here
                MessageBox.Show("Search clicked");
                callback?.Invoke(new List<VDF.Vault.Currency.Entities.IEntity>()); // Example callback invocation with an empty list
            };
        }

        /// <summary>
        /// Validate the selection from the Vault dialog. Only files are allowed. Filters for IPT and IAM files are set.
        /// </summary>
        /// <param name="mSelectionResult"></param>
        /// <returns>True for Files, flase for other entity types</returns>
        private bool mVaultSlctVldtn(SelectEntityResults mSelectionResult)
        {
            foreach (var item in mSelectionResult.SelectedEntities)
            {
                if (item.EntityClass.Id != "FILE")
                {
                    return false;
                }
            }
            return true;
        }

        internal class mHelpProvider : Autodesk.DataManagement.Client.Framework.Forms.Interfaces.ICustomHelpProvider
        {
            public bool CanShowHelp(string helpId, object helpContext)
            {
                //HelpIds can be found in Autodesk.DataManagement.Client.Framework.Vault.Forms.Currency.HelpIds
                return true;
            }

            public void ShowHelp(string helpId, object helpContext)
            {
                //MessageBox.Show("My Help Provider, Id: " + helpId + " Context: " + helpContext.ToString());
                VDF.Forms.Library.ShowMessage("Help Display", "Custom Help", VDF.Forms.Currency.ButtonConfiguration.Ok);
            }
        }

        private static Action mHelpAction()
        {
            //mCustomHelpProvider.ShowHelp("101", "mActionContext");
            return null;
        }

        private void btnHelp_Click(object sender, EventArgs e)
        {
            // call the custom help provider ShowHelp
            mCustomHelpProvider.ShowHelp("100", "Main Form"); 
        }
    }
}
