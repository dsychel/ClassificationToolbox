using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Diagnostics;
using System.Management;

namespace ClassificationToolboxWPF
{
    /// <summary>
    /// Interaction logic for Loger.xaml
    /// </summary>
    public partial class Loger : UserControl
    {
        readonly Paragraph paragraph;

        #region Constructors
        public Loger()
        {
            InitializeComponent();

            paragraph = new Paragraph();
            eventLog.Document = new FlowDocument(paragraph);

            paragraph.Inlines.Add(new Underline(new Bold(new Run("Event Log:"))));
            paragraph.Inlines.Add(new LineBreak());
            paragraph.Inlines.Add(new Bold(new Run(DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToLongTimeString())));
            paragraph.Inlines.Add(" - Aplication started.");
            paragraph.Inlines.Add(new LineBreak());
            paragraph.Inlines.Add(new Bold(new Run(DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToLongTimeString())));
            paragraph.Inlines.Add(" - Loading computer information:");
            paragraph.Inlines.Add(new LineBreak());
            paragraph.Inlines.Add(new LineBreak());

            paragraph.Inlines.Add(new Underline(new Bold(new Run("Operating System"))));
            paragraph.Inlines.Add(new LineBreak());
            foreach (ManagementBaseObject item in new ManagementObjectSearcher(new SelectQuery("Win32_OperatingSystem", null, new string[] { "Caption", "OSArchitecture", "Version" })).Get())
            { 
                paragraph.Inlines.Add(new Bold(new Run("Name: \t\t\t\t")));
                paragraph.Inlines.Add(item["Caption"] + ".");
                paragraph.Inlines.Add(new LineBreak());

                paragraph.Inlines.Add(new Bold(new Run("OS Architecture: \t\t\t")));
                paragraph.Inlines.Add(item["OSArchitecture"] + ".");
                paragraph.Inlines.Add(new LineBreak());

                paragraph.Inlines.Add(new Bold(new Run("Version: \t\t\t")));
                paragraph.Inlines.Add(item["Version"] + ".");
                paragraph.Inlines.Add(new LineBreak());
            }
            paragraph.Inlines.Add(new LineBreak());

            paragraph.Inlines.Add(new Underline(new Bold(new Run("Processor"))));
            foreach (ManagementBaseObject item in new ManagementObjectSearcher(new SelectQuery("Win32_Processor", null, new string[] { "AddressWidth", "Architecture",
                "CurrentClockSpeed", "DataWidth", "L2CacheSize", "L3CacheSize", "Manufacturer", "MaxClockSpeed", "Name", "NumberOfCores", "NumberOfLogicalProcessors", "ThreadCount" })).Get())
            {
                paragraph.Inlines.Add(new LineBreak());
                //foreach (var subitem in item.Properties)
                //{
                //    paragraph.Inlines.Add(subitem.Name + " " + subitem.Value);
                //    paragraph.Inlines.Add(new LineBreak());
                //}
                paragraph.Inlines.Add(new Bold(new Run("Name: \t\t\t\t")));
                paragraph.Inlines.Add(item["Name"] + ".");
                paragraph.Inlines.Add(new LineBreak());

                paragraph.Inlines.Add(new Bold(new Run("Manufacturer: \t\t\t")));
                paragraph.Inlines.Add(item["Manufacturer"] + ".");
                paragraph.Inlines.Add(new LineBreak());

                paragraph.Inlines.Add(new Bold(new Run("Max Clock Speed: \t\t")));
                paragraph.Inlines.Add(item["MaxClockSpeed"] + ".");
                paragraph.Inlines.Add(new LineBreak());

                paragraph.Inlines.Add(new Bold(new Run("Current Clock Speed: \t\t")));
                paragraph.Inlines.Add(item["CurrentClockSpeed"] + ".");
                paragraph.Inlines.Add(new LineBreak());

                paragraph.Inlines.Add(new Bold(new Run("Address Width: \t\t\t")));
                paragraph.Inlines.Add(item["AddressWidth"] + ".");
                paragraph.Inlines.Add(new LineBreak());

                paragraph.Inlines.Add(new Bold(new Run("Data Width: \t\t\t")));
                paragraph.Inlines.Add(item["DataWidth"] + ".");
                paragraph.Inlines.Add(new LineBreak());

                paragraph.Inlines.Add(new Bold(new Run("Architecture: \t\t\t")));
                paragraph.Inlines.Add(item["Architecture"] + "nm.");
                paragraph.Inlines.Add(new LineBreak());

                paragraph.Inlines.Add(new Bold(new Run("L2 Cache Size: \t\t\t")));
                paragraph.Inlines.Add(item["L2CacheSize"] + ".");
                paragraph.Inlines.Add(new LineBreak());

                paragraph.Inlines.Add(new Bold(new Run("L3 Cache Size: \t\t\t")));
                paragraph.Inlines.Add(item["L3CacheSize"] + ".");
                paragraph.Inlines.Add(new LineBreak());

                paragraph.Inlines.Add(new Bold(new Run("Number Of Cores: \t\t")));
                paragraph.Inlines.Add(item["NumberOfCores"] + ".");
                paragraph.Inlines.Add(new LineBreak());

                paragraph.Inlines.Add(new Bold(new Run("Number Of Logical Processors: \t")));
                paragraph.Inlines.Add(item["NumberOfLogicalProcessors"] + ".");
                paragraph.Inlines.Add(new LineBreak());

                paragraph.Inlines.Add(new Bold(new Run("Thread Count: \t\t\t")));
                paragraph.Inlines.Add(item["ThreadCount"] + ".");
                paragraph.Inlines.Add(new LineBreak());
            }
            paragraph.Inlines.Add(new LineBreak());

            paragraph.Inlines.Add(new Underline(new Bold(new Run("Video Controller"))));
            foreach(ManagementBaseObject item in new ManagementObjectSearcher(new SelectQuery("Win32_VideoController", null, new string[] { "AdapterCompatibility", "AdapterRAM", "Name", "DriverVersion", "VideoProcessor", "DeviceID"} )).Get())
            {
                paragraph.Inlines.Add(new LineBreak());

                paragraph.Inlines.Add(new Bold(new Run("Device ID: \t\t\t")));
                paragraph.Inlines.Add(item["DeviceID"] + ".");
                paragraph.Inlines.Add(new LineBreak());

                paragraph.Inlines.Add(new Bold(new Run("Name: \t\t\t\t")));
                paragraph.Inlines.Add(item["Name"] + ".");
                paragraph.Inlines.Add(new LineBreak());

                paragraph.Inlines.Add(new Bold(new Run("Manufacturer: \t\t\t")));
                paragraph.Inlines.Add(item["AdapterCompatibility"] + ".");
                paragraph.Inlines.Add(new LineBreak());

                paragraph.Inlines.Add(new Bold(new Run("Video Processor: \t\t")));
                paragraph.Inlines.Add(item["VideoProcessor"] + ".");
                paragraph.Inlines.Add(new LineBreak());

                paragraph.Inlines.Add(new Bold(new Run("Adapter RAM: \t\t\t")));
                paragraph.Inlines.Add(item["AdapterRAM"] + ".");
                paragraph.Inlines.Add(new LineBreak());

                paragraph.Inlines.Add(new Bold(new Run("Driver Version: \t\t\t")));
                paragraph.Inlines.Add(item["DriverVersion"] + ".");
                paragraph.Inlines.Add(new LineBreak());

            }
            paragraph.Inlines.Add(new LineBreak());

            paragraph.Inlines.Add(new Underline(new Bold(new Run("Memory"))));
            foreach (ManagementBaseObject item in new ManagementObjectSearcher(new SelectQuery("Win32_PhysicalMemory", null, new string[] { "Tag", "Name", "Manufacturer", "Capacity", "ConfiguredClockSpeed",
                "ConfiguredVoltage", "DataWidth",  "TotalWidth" })).Get())
            {
                paragraph.Inlines.Add(new LineBreak());

                paragraph.Inlines.Add(new Bold(new Run("Device ID: \t\t\t")));
                paragraph.Inlines.Add(item["Tag"] + ".");
                paragraph.Inlines.Add(new LineBreak());

                paragraph.Inlines.Add(new Bold(new Run("Name: \t\t\t\t")));
                paragraph.Inlines.Add(item["Name"] + ".");
                paragraph.Inlines.Add(new LineBreak());

                paragraph.Inlines.Add(new Bold(new Run("Manufacturer: \t\t\t")));
                paragraph.Inlines.Add(item["Manufacturer"] + ".");
                paragraph.Inlines.Add(new LineBreak());

                paragraph.Inlines.Add(new Bold(new Run("Capacity: \t\t\t")));
                paragraph.Inlines.Add(item["Capacity"] + ".");
                paragraph.Inlines.Add(new LineBreak());

                paragraph.Inlines.Add(new Bold(new Run("Configured Clock Speed: \t\t")));
                paragraph.Inlines.Add(item["ConfiguredClockSpeed"] + ".");
                paragraph.Inlines.Add(new LineBreak());

                paragraph.Inlines.Add(new Bold(new Run("Configured Voltage: \t\t")));
                paragraph.Inlines.Add(item["ConfiguredVoltage"] + ".");
                paragraph.Inlines.Add(new LineBreak());

                paragraph.Inlines.Add(new Bold(new Run("Data Width: \t\t\t")));
                paragraph.Inlines.Add(item["DataWidth"] + ".");
                paragraph.Inlines.Add(new LineBreak());

                paragraph.Inlines.Add(new Bold(new Run("Total Width: \t\t\t")));
                paragraph.Inlines.Add(item["TotalWidth"] + ".");
                paragraph.Inlines.Add(new LineBreak());
            }
            paragraph.Inlines.Add(new LineBreak());
        }
        #endregion

        #region Methods
        public void SaveLog(string path)
        {
            var content = new TextRange(eventLog.Document.ContentStart, eventLog.Document.ContentEnd);

            if (content.CanSave(DataFormats.Rtf))
            {
                using (var stream = new FileStream(path, FileMode.OpenOrCreate))
                {
                    content.Save(stream, DataFormats.Rtf);
                }
            }
        }

        public void SaveLog()
        {
            System.Windows.Forms.SaveFileDialog saveFileDialog = new System.Windows.Forms.SaveFileDialog()
            {
                Filter = "Log Files(*.rtf) | *.rtf",
                FileName = ""
            };
            if (Properties.Settings.Default.lastLogFolder != "" && Directory.Exists(Properties.Settings.Default.lastLogFolder))
                saveFileDialog.InitialDirectory = Properties.Settings.Default.lastLogFolder;
            else
                saveFileDialog.InitialDirectory = "";

            if (saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                Properties.Settings.Default.lastLogFolder = Path.GetDirectoryName(saveFileDialog.FileName);
                Properties.Settings.Default.Save();

                SaveLog(saveFileDialog.FileName);
            }
            saveFileDialog.Dispose();
        }

        public void AddEntry(StartedEventsArg args)
        {
            paragraph.Inlines.Add(new Bold(new Run(args.EventDate.ToShortDateString() + " " + args.EventDate.ToLongTimeString())));
            paragraph.Inlines.Add(" - " + args.LogMessage);
            paragraph.Inlines.Add(new LineBreak());
        }

        public void AddEntry(CompletionEventsArg args)
        {
            paragraph.Inlines.Add(new Bold(new Run(args.EventDate.ToShortDateString() + " " + args.EventDate.ToLongTimeString())));
            paragraph.Inlines.Add(" - " + args.LogMessage);
            if (args.Error != null)
            {
                paragraph.Inlines.Add(new LineBreak());
                paragraph.Inlines.Add("--- " + args.Error);
            }

            paragraph.Inlines.Add(new LineBreak());
        }

        public void AddEntry(DateTime date, string message)
        {
            paragraph.Inlines.Add(new Bold(new Run(date.ToShortDateString() + " " + date.ToLongTimeString())));
            paragraph.Inlines.Add(" - " + message);
            paragraph.Inlines.Add(new LineBreak());
        }

        public void AddEntry(string message)
        {
            paragraph.Inlines.Add("--- " + message);
            paragraph.Inlines.Add(new LineBreak());
        }
        #endregion

        #region Events
        private void SaveLogButton_Click(object sender, RoutedEventArgs e)
        {
           SaveLog();
        }
        #endregion
    }
}
