using System;
using System.Diagnostics;
using System.Windows;
using System.Reflection;
using System.IO;

namespace ClassificationToolboxWPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            InitializeAbout();
            InitializeParallelity();
        }

        #region Assembly Attribute Accessors
        public string AssemblyTitle
        {
            get
            {
                object[] attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyTitleAttribute), false);
                if (attributes.Length > 0)
                {
                    AssemblyTitleAttribute titleAttribute = (AssemblyTitleAttribute)attributes[0];
                    if (titleAttribute.Title != "")
                    {
                        return titleAttribute.Title;
                    }
                }
                return Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().CodeBase);
            }
        }

        public string AssemblyVersion
        {
            get
            {
                return Assembly.GetExecutingAssembly().GetName().Version.ToString();
            }
        }

        public string AssemblyDescription
        {
            get
            {
                object[] attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyDescriptionAttribute), false);
                if (attributes.Length == 0)
                {
                    return "";
                }
                return ((AssemblyDescriptionAttribute)attributes[0]).Description;
            }
        }

        public string AssemblyProduct
        {
            get
            {
                object[] attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyProductAttribute), false);
                if (attributes.Length == 0)
                {
                    return "";
                }
                return ((AssemblyProductAttribute)attributes[0]).Product;
            }
        }

        public string AssemblyCopyright
        {
            get
            {
                object[] attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyCopyrightAttribute), false);
                if (attributes.Length == 0)
                {
                    return "";
                }
                return ((AssemblyCopyrightAttribute)attributes[0]).Copyright;
            }
        }

        public string AssemblyCompany
        {
            get
            {
                object[] attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyCompanyAttribute), false);
                if (attributes.Length == 0)
                {
                    return "";
                }
                return ((AssemblyCompanyAttribute)attributes[0]).Company;
            }
        }
        #endregion

        #region Methods
        private void InitializeParallelity()
        {
            NativeMethods.SetParallelity((int)Properties.Settings.Default.openMPthreads,
                (int)Properties.Settings.Default.progressBuffer);
        }

        private void SetStatus(string status)
        {
            statusLabel.Text = status;
        }

        private void Shutdown()
        {
            ShutdownDialog shutdown = new ShutdownDialog();
            if (shutdown.ShowDialog() == true)
            {
                eventLog.SaveLog("shutdownLog.rtf");

                var psi = new ProcessStartInfo("shutdown", "/f /s /t 10")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                Process.Start(psi);
            }
        }

        void DisableButtons(bool showProgress = true)
        {
            parallelitySettings.IsEnabled = false;
            restoreDefault.IsEnabled = false;

            grayscaleConverter.IsEnabled = false;
            trainClassifierControl.IsEnabled = false;
            cropSamplesControl.IsEnabled = false;
            batchCropSamplesControl.IsEnabled = false;
            extractionControl.IsEnabled = false;
            batchExtractionControl.IsEnabled = false;
            featurePostprocessingControl.IsEnabled = false;
            testClassifierControl.IsEnabled = false;
            testClassifierTimeControl.IsEnabled = false;
            testBatchClassifierTimeControl.IsEnabled = false;
            batchTesterControl.IsEnabled = false;
            viewAndCompareFeaturesControl.IsEnabled = false;
            batchDetectionControl.IsEnabled = false;
            batchDetectionResultControl.IsEnabled = false;
            extractionImageControl.IsEnabled = false;
            zernikeComparsionControl.IsEnabled = false;
            detectionControl.DisableControl();

            progressBar.Value = 0;
            if (showProgress)
                progressBar.Visibility = Visibility.Visible;
        }

        void EnableButtons()
        {
            parallelitySettings.IsEnabled = true;
            restoreDefault.IsEnabled = true;

            grayscaleConverter.IsEnabled = true;
            trainClassifierControl.IsEnabled = true;
            cropSamplesControl.IsEnabled = true;
            batchCropSamplesControl.IsEnabled = true;
            extractionControl.IsEnabled = true;
            batchExtractionControl.IsEnabled = true;
            featurePostprocessingControl.IsEnabled = true;
            testClassifierControl.IsEnabled = true;
            testClassifierTimeControl.IsEnabled = true;
            testBatchClassifierTimeControl.IsEnabled = true;
            batchTesterControl.IsEnabled = true;
            viewAndCompareFeaturesControl.IsEnabled = true;
            batchDetectionControl.IsEnabled = true;
            batchDetectionResultControl.IsEnabled = true;
            extractionImageControl.IsEnabled = true;
            zernikeComparsionControl.IsEnabled = true;
            detectionControl.EnableControl();

            progressBar.Value = 0;
            progressBar.Visibility = Visibility.Hidden;
        }

        private void InitializeAbout()
        {
            labelProductName.Content = AssemblyProduct;
            labelCompanyName.Content = AssemblyCompany;
            textBoxDescription.Text = "Toolbox for object detection.\r\n\r\n" +
                                      "Icons created by: \r\n" +
                                      "https://www.iconfinder.com/Vecteezy";
        }
        #endregion

        #region Working status
        private void StatusChanged(object sender, StatusChangedArg e)
        {
            Dispatcher.Invoke(new Action(() =>
            {
                SetStatus(e.Status);
            }));
        }

        private void WorkStarted(object sender, StartedEventsArg e)
        {
            progressBar.Maximum = e.MaxProgress;
            DisableButtons(e.ReportProgress);
            SetStatus(e.Status);
            eventLog.AddEntry(e);
        }

        private void WorkProgressing(object sender, ProgressingEventsArg e)
        {
            Dispatcher.Invoke(new Action(() =>
            {
                if (e.MaxProgress != -1)
                    progressBar.Maximum = e.MaxProgress;
                if (e.Progress > progressBar.Maximum)
                    progressBar.Maximum = e.Progress;
                progressBar.Value = e.Progress;

                if (e.LogMessage != null)
                    eventLog.AddEntry(e.LogMessage);
            }));
        }

        private void WorkCompleted(object sender, CompletionEventsArg e)
        {
            EnableButtons();
            SetStatus(e.Status);
            eventLog.AddEntry(e);

            if (e.Shutdown)
                Shutdown();
        }
        #endregion

        #region Menu
        private void SaveLog_Click(object sender, RoutedEventArgs e)
        {
            eventLog.SaveLog();
        }

        private void ParallelitySettings_Click(object sender, RoutedEventArgs e)
        {
            ParallelitySettings parSet = new ParallelitySettings();
            parSet.ShowDialog();
        }

        private void RestoreDefault_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.Reset();
            InitializeParallelity();
            RestartApplication_Click(this, null);
        }

        private void StopCurrentWork_Click(object sender, RoutedEventArgs e)
        {
            batchDetectionControl.StopTask();
            cropSamplesControl.StopTask();
            batchCropSamplesControl.StopTask();
            grayscaleConverter.StopTask();
            testClassifierControl.StopTask();
            trainClassifierControl.StopTask();
            batchExtractionControl.StopTask();
            batchTesterControl.StopTask();
            testBatchClassifierTimeControl.StopTask();
            //testClassifierTimeControl.StopTask();

            testBatchClassifierTimeControl.StopTask();
        }

        private void RestartApplication_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(System.Windows.Forms.Application.ExecutablePath);
            this.Close();
        }

        private void LoadGenerateLUTpfmm_Click(object sender, RoutedEventArgs e)
        {
            NativeMethods.InitializePFMM();
        }

        private void UnloadLUTpfmm_Click(object sender, RoutedEventArgs e)
        {
            NativeMethods.ClearMemoryPFMM();
        }

        private void LoadGenerateLUTzernike_Click(object sender, RoutedEventArgs e)
        {
            NativeMethods.InitializeZernike();
        }

        private void UnloadLUTzernike_Click(object sender, RoutedEventArgs e)
        {
            NativeMethods.ClearMemoryZernike();
        }

        private void LoadGenerateLUTzernikeFP_Click(object sender, RoutedEventArgs e)
        {
            NativeMethods.InitializeZernikeFP();
        }

        private void UnloadLUTzernikeFP_Click(object sender, RoutedEventArgs e)
        {
            NativeMethods.ClearMemoryZernikeFP();
        }

        private void DetectionControl_ClassifierLoaded(object sender, EventArgs e)
        {
            batchDetectionControl.IsClassifierLoaded = false;
            testClassifierTimeControl.IsClassifierLoaded = false;
            testBatchClassifierTimeControl.IsClassifierLoaded = false;
        }

        private void DetectionControl_ExtractorLoaded(object sender, EventArgs e)
        {
            batchDetectionControl.IsExtractorLoaded = false;
            testClassifierTimeControl.IsExtractorLoaded = false;
            testBatchClassifierTimeControl.IsExtractorLoaded = false;
        }

        private void BatchDetectionControl_ClassifierLoaded(object sender, EventArgs e)
        {
            detectionControl.IsClassifierLoaded = false;
            testClassifierTimeControl.IsClassifierLoaded = false;
            testBatchClassifierTimeControl.IsClassifierLoaded = false;
        }

        private void BatchDetectionControl_ExtractorLoaded(object sender, EventArgs e)
        {
            detectionControl.IsExtractorLoaded = false;
            testClassifierTimeControl.IsExtractorLoaded = false;
            testBatchClassifierTimeControl.IsExtractorLoaded = false;
        }

        private void TestClassifierTimeControl_ClassifierLoaded(object sender, EventArgs e)
        {
            detectionControl.IsClassifierLoaded = false;
            batchDetectionControl.IsClassifierLoaded = false;
            testBatchClassifierTimeControl.IsClassifierLoaded = false;
        }

        private void TestClassifierTimeControl_ExtractorLoaded(object sender, EventArgs e)
        {
            detectionControl.IsExtractorLoaded = false;
            batchDetectionControl.IsExtractorLoaded = false;
            testBatchClassifierTimeControl.IsExtractorLoaded = false;
        }

        private void BatchTestClassifierTimeControl_ClassifierLoaded(object sender, EventArgs e)
        {
            detectionControl.IsClassifierLoaded = false;
            batchDetectionControl.IsClassifierLoaded = false;
            testClassifierTimeControl.IsClassifierLoaded = false;
        }

        private void BatchTestClassifierTimeControl_ExtractorLoaded(object sender, EventArgs e)
        {
            detectionControl.IsExtractorLoaded = false;
            batchDetectionControl.IsExtractorLoaded = false;
            testClassifierTimeControl.IsExtractorLoaded = false;
        }
        #endregion
    }
}
