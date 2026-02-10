using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using DevExpress.UserSkins;
using DevExpress.Skins;
using DevExpress.LookAndFeel;
using DevExpress.Utils;
using static DevExpress.XtraPrinting.Native.ExportOptionsPropertiesNames;

using VDF = Autodesk.DataManagement.Client.Framework;

namespace SelectEntity {
    static class Program {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main() {

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Application.Run(new SelectEntityMainForm());
        }
    }
}
