﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Diagnostics;
using System.Xml.Serialization;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using HDGraph.Resources;

namespace HDGraph
{
    public class VersionCheck
    {
        private const string CheckForNewVersionUrl = "http://www.hdgraph.com/VersionChecks/GetCurrentVersionNumber.php";
        private Form parent;
        private bool alwaysPopup;

        public void CheckForNewVersion(Form parent, bool alwaysPopup)
        {
            try
            {
                this.alwaysPopup = alwaysPopup;
                this.parent = parent;
                WebClient client = new WebClient();
                client.DownloadStringCompleted += new DownloadStringCompletedEventHandler(client_DownloadStringCompleted);
                client.DownloadStringAsync(new Uri(CheckForNewVersionUrl));
            }
            catch (Exception ex)
            {
                Trace.TraceError("Error during check for new version : " + HDGTools.PrintError(ex));
            }
        }

        void client_DownloadStringCompleted(object sender, DownloadStringCompletedEventArgs e)
        {
            if (e.Cancelled)
                return;
            Version currentVersion = Assembly.GetExecutingAssembly().GetName().Version;
            if (e.Error == null)
            {
                try
                {
                    string result = e.Result;
                    VersionInfo versionInfo = VersionInfo.DeserializeFromString(result);
                    if (currentVersion.CompareTo(new Version(versionInfo.VersionNumber)) < 0)
                        //newer version is available:
                        new NewVersionAvailableForm() { VersionInfo = versionInfo }.ShowDialog(parent);
                    else
                    {
                        // no newer version available
                        if (alwaysPopup)
                            MessageBox.Show(ApplicationMessages.YourVersionIsUpToDate, "HDGraph", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }

                }
                catch (Exception ex)
                {
                    Trace.TraceError("Error during check for new version (response analysis failed): " + HDGTools.PrintError(ex));
                    if (alwaysPopup)
                        MessageBox.Show(String.Format(ApplicationMessages.ErrorOccuredWhileCheckingVersion, currentVersion),
                            "HDGraph", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            else
            {
                Trace.TraceError("Error downloading latest version info : " + HDGTools.PrintError(e.Error));
                if (alwaysPopup)
                    MessageBox.Show(String.Format(ApplicationMessages.ErrorOccuredWhileCheckingVersion, currentVersion), "HDGraph", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }

    public class VersionInfo
    {
        public DateTime ReleaseDate { get; set; }

        public string VersionNumber { get; set; }

        public string DownloadPageUrl { get; set; }

        public string ChangeLogUrl { get; set; }

        public string SerializeToString()
        {
            XmlSerializer serializer = new XmlSerializer(typeof(VersionInfo));
            using (StringWriter writer = new StringWriter())
            {
                serializer.Serialize(writer, this);
                return writer.ToString();
            }
        }

        public static VersionInfo DeserializeFromString(string serializedValue)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(VersionInfo));
            using (TextReader reader = new StringReader(serializedValue))
            {
                VersionInfo versionInfo = (VersionInfo)serializer.Deserialize(reader);
                return versionInfo;
            }
        }
    }
}
