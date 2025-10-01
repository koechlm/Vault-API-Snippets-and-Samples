/*=====================================================================
  
  This file is part of the Autodesk Vault API Code Samples.

  Copyright (C) Autodesk Inc.  All rights reserved.

THIS CODE AND INFORMATION ARE PROVIDED "AS IS" WITHOUT WARRANTY OF ANY
KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE
IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
PARTICULAR PURPOSE.
=====================================================================*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Xml.Serialization;

using Autodesk.Connectivity.WebServices;
using Autodesk.Connectivity.WebServicesTools;
using Autodesk.DataManagement.Client.Framework.Vault.Currency.Connections;
using System.Xml;

namespace SelectEntity
{

    [XmlRoot("settings")]
    public class Settings
    {
        private static string mOption;

        private static string mOptionName;

        [XmlElement("ActiveTheme")]
        public string Theme;


        public Settings()
        {

        }

        private static string GetSettingsPath()
        {
            string codeFolder = Util.GetAssemblyPath();
            string xmlPath = Path.Combine(codeFolder, "Settings.xml");
            return xmlPath;
        }

        public bool Save()
        {
            string mFilePathandName = GetSettingsPath();
            try
            {
                using (System.IO.StreamWriter writer = new System.IO.StreamWriter(mFilePathandName))
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(Settings));
                    serializer.Serialize(writer, this);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool SaveToVault(Connection connection)
        {
            string settingsString = null;

            try
            {
                using (System.IO.StringWriter writer = new System.IO.StringWriter())
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(Settings));
                    serializer.Serialize(writer, this);
                    settingsString = writer.ToString();
                }
                connection.WebServiceManager.KnowledgeVaultService.SetVaultOption(mOptionName, settingsString);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static Settings Load()
        {
            Settings retVal = new Settings();

            using (System.IO.StreamReader reader = new System.IO.StreamReader(GetSettingsPath()))
            {
                XmlSerializer serializer = new XmlSerializer(typeof(Settings));
                retVal = (Settings)serializer.Deserialize(reader);
            }

            return retVal;
        }

        public static Settings LoadFromVault(Connection connection)
        {
            Settings retval = null;

            string settingsString = connection.WebServiceManager.KnowledgeVaultService.GetVaultOption(mOptionName);
            if (settingsString != null && settingsString.Length > 0)
            {
                try
                {
                    using (System.IO.StringReader reader = new System.IO.StringReader(settingsString))
                    {
                        XmlSerializer serializer = new XmlSerializer(typeof(Settings));

                        retval = (Settings)serializer.Deserialize(reader);
                    }
                }
                catch
                { }
            }
            return retval;
        }


        public static string mGetVaultOption(Connection connection, string mOptionName)
        {
            try
            {
                string mOptionValue = connection.WebServiceManager.KnowledgeVaultService.GetVaultOption(mOptionName);
                return mOptionValue;
            }
            catch (Exception)
            {
                return null;
            }
        }


        public static bool mSetVaultOption(Connection connection, string mOptionName, string mOptionValue)
        {
            try
            {
                connection.WebServiceManager.KnowledgeVaultService.SetVaultOption(mOptionName, mOptionValue);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }


        public static string mGetSystemOption(Connection connection, string mOptionName)
        {
            try
            {
                string mOptionValue = connection.WebServiceManager.KnowledgeVaultService.GetSystemOption(mOptionName);
                return mOptionValue;
            }
            catch (Exception)
            {
                return null;
            }
        }


        public static string mSetSystemOption(Connection connection, string mOptionName, string mOptionValue)
        {
            try
            {
                connection.WebServiceManager.KnowledgeVaultService.SetSystemOption(mOptionName, mOptionValue);
                return mOptionName;
            }
            catch (Exception)
            {
                return null;
            }
        }

    }

}
