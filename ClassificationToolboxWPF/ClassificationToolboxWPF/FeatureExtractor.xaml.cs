using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System;

namespace ClassificationToolboxWPF
{
    /// <summary>
    /// Interaction logic for FeatureExtractor.xaml
    /// </summary>
    public partial class FeatureExtractor : UserControl
    {
        #region Fields
        NativeMethods.ProgressCallback callback;
        BackgroundWorker worker;
        long operationTime = 0;
        int filesCount = 0;
        bool warnings = false;
        #endregion

        #region Constructors
        public FeatureExtractor()
        {
            InitializeComponent();

            GlobalFunctions.InitializeDirectory(ref negativeSamplesPathTextBox, "negativeSamplesPath");
            GlobalFunctions.InitializeDirectory(ref positiveSamplesPathTextBox, "positiveSamplesPath");
        }
        #endregion

        #region Methods
        private int CheckFolder(string path, string extension, ref List<String> fileList)
        {
            try
            {
                SearchOption so = SearchOption.TopDirectoryOnly;
                fileList.AddRange(Directory.EnumerateFiles(path, extension, so).ToArray());

                if (extractionIncludeSubfolderCheckBox.IsChecked == true)
                {
                    IEnumerable<string> directories = Directory.EnumerateDirectories(path);
                    foreach (string directory in directories)
                        CheckFolder(directory, extension, ref fileList);
                }
                return fileList.Count;
            }
            catch
            {
                ProgressingEventsArg progressArgs = new ProgressingEventsArg(0, -1, "Ignored folder: " + path);
                OnExtractionProgressing(progressArgs);

                return 0;
            }
        }

        private int ListFiles(string path, string extension)
        {
            List<String> fileList = new List<string>();
            CheckFolder(path, extension, ref fileList);

            if (fileList.Count > 0)
            {
                using (StreamWriter sw = new StreamWriter(path + "file.lst"))
                {
                    for (int f = 0; f < fileList.Count - 1; f++)
                        sw.WriteLine(fileList[f]);
                    sw.Write(fileList[fileList.Count - 1]);
                }
            }
            return fileList.Count;
        }
        #endregion

        #region Triggers
        public event EventHandler<StartedEventsArg> ExtractionStarted;
        protected virtual void OnExtractionStarted(StartedEventsArg e)
        {
            ExtractionStarted?.Invoke(this, e);
        }

        public event EventHandler<ProgressingEventsArg> ExtractionProgressing;
        protected virtual void OnExtractionProgressing(ProgressingEventsArg e)
        {
            ExtractionProgressing?.Invoke(this, e);
        }

        public event EventHandler<CompletionEventsArg> ExtractionCompleted;
        protected virtual void OnExtractionCompletion(CompletionEventsArg e)
        {
            ExtractionCompleted?.Invoke(this, e);
        }
        #endregion

        #region Events
        private void BrowseNegativeSamplesButton_Click(object sender, RoutedEventArgs e)
        {
            GlobalFunctions.SelectFolder(ref negativeSamplesPathTextBox, "negativeSamplesPath");
        }

        private void BrowsePositiveSamples_Click(object sender, RoutedEventArgs e)
        {
            GlobalFunctions.SelectFolder(ref positiveSamplesPathTextBox, "positiveSamplesPath");
        }

        private void BrowseFeatureSaveFolderButton_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.SaveFileDialog saveFileDialog = new System.Windows.Forms.SaveFileDialog()
            {
                Filter = "Features Files (*.fet.bin) | *.fet.bin",
                FileName = "",
                InitialDirectory = Properties.Settings.Default.featureSaveAs
            };
            saveFileDialog.FileName = extractorSettings.ExtractorFileName;

            if (saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                featureSaveAsTextBox.Text = saveFileDialog.FileName;

                Properties.Settings.Default.featureSaveAs = Path.GetDirectoryName(saveFileDialog.FileName);
                if (Properties.Settings.Default.featureSaveAs[Properties.Settings.Default.featureSaveAs.Length - 1] != '\\')
                    Properties.Settings.Default.featureSaveAs += '\\';
                Properties.Settings.Default.Save();


                saveFileDialog.Dispose();
            }
        }

        private void ClearSamplesFoldersButton_Click(object sender, RoutedEventArgs e)
        {
            negativeSamplesPathTextBox.Text = "";
            positiveSamplesPathTextBox.Text = "";
        }

        private void ExtractionButton_Click(object sender, RoutedEventArgs e)
        {
            if ((positiveSamplesPathTextBox.Text == "" || !Directory.Exists(positiveSamplesPathTextBox.Text)) && (negativeSamplesPathTextBox.Text == "" || !Directory.Exists(negativeSamplesPathTextBox.Text)))
            {
                MessageBox.Show("Positive and Negative samples paths are empty or doesn't exist.");
                return;
            }
            if (featureSaveAsTextBox.Text == "")
            {
                MessageBox.Show("Save path is empty or doesn't exist.");
                return;
            }
            if(extractorSettings.CheckFileName(featureSaveAsTextBox.Text) == MessageBoxResult.No)
            {
                return;
            }

            if (worker != null && worker.IsBusy)
            {
                MessageBox.Show("Application is busy.");
                return;
            }

            warnings = false;
            string message = "Extraction started: " + extractorSettings.ExtractorDescription;

            StartedEventsArg args = new StartedEventsArg("Status: Working", message, DateTime.Now, 1, true);
            OnExtractionStarted(args);

            string extension = "";
            if (extractionSaveSettings.SelectedFileType == ImageFileTypeSelector.FileType.text)
                extension = "*.gray.txt";
            else if (extractionSaveSettings.SelectedFileType == ImageFileTypeSelector.FileType.binary8bit)
                extension = "*.gray.8bin";
            else if (extractionSaveSettings.SelectedFileType == ImageFileTypeSelector.FileType.binary64bit)
                extension = "*.gray.64bin";

            filesCount = 0;
            int samples;
            if ((samples = ListFiles(positiveSamplesPathTextBox.Text, extension)) == 0)
            {
                ProgressingEventsArg progressArgs = new ProgressingEventsArg(0, -1, "Warning: No positives with given format detected.");
                OnExtractionProgressing(progressArgs);
                warnings = true;
            }
            filesCount += samples;

            if ((samples = ListFiles(negativeSamplesPathTextBox.Text, extension)) == 0)
            {
                ProgressingEventsArg progressArgs = new ProgressingEventsArg(0, -1, "Warning: No negatives with given format detected.");
                OnExtractionProgressing(progressArgs);
                warnings = true;
            }
            filesCount += samples;

            if (filesCount == 0)
            {
                string statusLabel = "Status: Extraction completed with errors. Check event log for details";
                string logMessage = "Extraction completed with errors.";
                bool shutdown = (shutdownExtractionCheckBox.IsChecked == null || shutdownExtractionCheckBox.IsChecked == false) ? false : true;
                CompletionEventsArg endArgs = new CompletionEventsArg(statusLabel, logMessage, "No samples with given format detected.", DateTime.Now, shutdown);
                OnExtractionCompletion(endArgs);
            }
            else
            {
                ProgressingEventsArg progressArgs = new ProgressingEventsArg(0, filesCount);
                OnExtractionProgressing(progressArgs);

                worker = new BackgroundWorker()
                {
                    WorkerSupportsCancellation = true,
                    WorkerReportsProgress = true
                };
                worker.DoWork += ExtractionBackgroundWorker_DoWork;
                worker.RunWorkerCompleted += ExtractionBackgroundWorker_RunWorkerCompleted;
                worker.ProgressChanged += ExtractionBackgroundWorker_ProgressChanged;
                worker.RunWorkerAsync();
            }
        }

        private void ExtractionBackgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            ProgressingEventsArg args = new ProgressingEventsArg(e.ProgressPercentage);
            OnExtractionProgressing(args);
        }

        unsafe private void ExtractionBackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            callback = (value) =>
            {
                worker.ReportProgress(value);
            };

            int[] parameters = null;
            string negativeSamplesPath = "";
            string positiveSamplesPath = "";
            string savePath = "";
            string extractorType = "";
            bool append = false;
            ImageFileTypeSelector.FileType fileType = ImageFileTypeSelector.FileType.text;

            Dispatcher.Invoke(new Action(() =>
            {
                parameters = extractorSettings.ParametersArray;

                fileType = extractionSaveSettings.SelectedFileType;
                negativeSamplesPath = negativeSamplesPathTextBox.Text;
                positiveSamplesPath = positiveSamplesPathTextBox.Text;
                savePath = featureSaveAsTextBox.Text;
                extractorType = extractorSettings.ExtractorName;
                append = (extractionAppendRadioButton.IsChecked == null || extractionAppendRadioButton.IsChecked == false) ? false : true;
            }));

            fixed (int* parPointer = parameters)
            {
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                int status = NativeMethods.Extraction(negativeSamplesPath, positiveSamplesPath, savePath, extractorType, parPointer, fileType, append, callback);

                stopwatch.Stop();
                operationTime = stopwatch.ElapsedMilliseconds;

                if (status < 0)
                    GlobalFunctions.ThrowError(status);
            }
        }

        private void ExtractionBackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            string statusLabel;
            string logMessage;
            string error = null;

            if (e.Error != null)
            {
                statusLabel = "Status: Extraction completed with errors. Check event log for details.";
                logMessage = "Extraction completed with errors: ";
                error = e.Error.Message;
            }
            else if (warnings)
            {
                statusLabel = "Status: Extraction completed with warnings. Check event log for details.";
                logMessage = "Extraction completed with warnings. Elapsed time: " + operationTime + "ms. Processed files: " + filesCount;
            }
            else
            {
                statusLabel = "Status: Extraction completed successful. Check event log for details.";
                logMessage = "Extraction completed successful. Elapsed time: " + operationTime + "ms. Processed files: " + filesCount;
            }

            bool shutdown = (shutdownExtractionCheckBox.IsChecked == null || shutdownExtractionCheckBox.IsChecked == false) ? false : true;
            CompletionEventsArg args = new CompletionEventsArg(statusLabel, logMessage, error, DateTime.Now, shutdown);
            OnExtractionCompletion(args);

            worker.Dispose();
            worker = null;
        }
        #endregion
    }
}
