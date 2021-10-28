using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;
using System;
using System.Windows.Media.Imaging;
using System.Windows.Media;

namespace ClassificationToolboxWPF
{
    /// <summary>
    /// Interaction logic for BatchExtraction.xaml
    /// </summary>
    public partial class BatchFeatureExtractor : UserControl
    {
        #region Structs
        struct ExtractionStatus
        {
            public string name;
            public int error;
            public long time;
            public int extractorNumber;
        }

        private class ExtractorItem
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public ImageSource Icon { get; set; }
        }
        #endregion

        #region Fields
        NativeMethods.ProgressCallback callback;
        BackgroundWorker worker;
        long operationTime = 0;
        int filesCount = 0;
        bool warnings = false;

        readonly List<string> extNames = new List<string>();
        readonly List<string> extTypes = new List<string>();
        readonly List<int[]> extParameters = new List<int[]>();
        #endregion

        #region Constructors
        public BatchFeatureExtractor()
        {
            InitializeComponent();

            GlobalFunctions.InitializeDirectory(ref negativeSamplesPathTextBox, "batchNegativeSamplesPath");
            GlobalFunctions.InitializeDirectory(ref positiveSamplesPathTextBox, "batchPositiveSamplesPath");
            GlobalFunctions.InitializeDirectory(ref saveInPathTextBox, "batchExtractionSaveInPath");
        }
        #endregion

        #region Methods
        public void StopTask()
        {
            if (worker != null && worker.IsBusy)
                worker.CancelAsync();
        }

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
            GlobalFunctions.SelectFolder(ref negativeSamplesPathTextBox, "batchNegativeSamplesPath");
        }

        private void BrowsePositiveSamples_Click(object sender, RoutedEventArgs e)
        {
            GlobalFunctions.SelectFolder(ref positiveSamplesPathTextBox, "batchPositiveSamplesPath");
        }

        private void BrowseSaveInFolder_Click(object sender, RoutedEventArgs e)
        {
            GlobalFunctions.SelectFolder(ref saveInPathTextBox, "batchExtractionSaveInPath");
        }

        private void GenerateNameButton_Click(object sender, RoutedEventArgs e)
        {
            extractorNameTextBox.Text = extractorSettings.ExtractorFileName;
        }

        private void AddExtractorButton_Click(object sender, RoutedEventArgs e)
        {
            string name = extractorNameTextBox.Text + extractorExtensionTextBox.Text;
            string description = extractorSettings.ExtractorDescription;

            if (name == ".fet.bin")
            {
                MessageBox.Show("Extractor name is empty.");
                return;
            }
            if (extNames.Contains(name))
            {
                MessageBox.Show("Extractor with given name has already been added to the list.");
                return;
            }

            extNames.Add(name);
            extParameters.Add(extractorSettings.ParametersArray);
            extTypes.Add(extractorSettings.ExtractorName);
            extractorListView.Items.Add(new ExtractorItem { Name = name, Description = description, Icon = new BitmapImage(new Uri(@"/Images/pending.png", UriKind.Relative)) });

        }

        private void RemoveExtractorButton_Click(object sender, RoutedEventArgs e)
        {
            if (extractorListView.SelectedIndex >= 0)
            {
                int index = extractorListView.SelectedIndex;

                extractorListView.Items.RemoveAt(index);
                extNames.RemoveAt(index);
                extTypes.RemoveAt(index);
                extParameters.RemoveAt(index);
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
            if (saveInPathTextBox.Text == "")
            {
                MessageBox.Show("Save path is empty or doesn't exist.");
                return;
            }

            if (worker != null && worker.IsBusy)
            {
                MessageBox.Show("Application is busy.");
                return;
            }

            for (int i = 0; i < extractorListView.Items.Count; i++)
                ((ExtractorItem)extractorListView.Items[i]).Icon = new BitmapImage(new Uri(@"/Images/pending.png", UriKind.Relative));
            extractorListView.Items.Refresh();

            StartedEventsArg args = new StartedEventsArg("Status: Working", "Extraction started.", DateTime.Now, extractorListView.Items.Count, true);
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
                string logMessage = "Conversion completed with errors.";
                bool shutdown = (shutdownExtractionCheckBox.IsChecked == null || shutdownExtractionCheckBox.IsChecked == false) ? false : true;
                CompletionEventsArg endArgs = new CompletionEventsArg(statusLabel, logMessage, "No samples with given format detected.", DateTime.Now, shutdown);
                OnExtractionCompletion(endArgs);
            }
            else
            {
                ProgressingEventsArg progressArgs = new ProgressingEventsArg(0, extractorListView.Items.Count * filesCount);
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
            if (e.UserState != null)
            {
                ExtractionStatus status = ((ExtractionStatus)e.UserState);

                string logMessage;
                if (status.error == -10)
                {
                    logMessage = "Features extraction canceled (" + status.name + ")";
                    ((ExtractorItem)extractorListView.Items[status.extractorNumber]).Icon = new BitmapImage(new Uri(@"/Images/error.png", UriKind.Relative));
                }
                else if (status.error < 0)
                {
                    logMessage = "Features extraction (" + status.name + ") completed with error: " + GlobalFunctions.GetErrorDescription(status.error);
                    ((ExtractorItem)extractorListView.Items[status.extractorNumber]).Icon = new BitmapImage(new Uri(@"/Images/error.png", UriKind.Relative));
                }
                else
                {
                    logMessage = "Features extraction (" + status.name + ") completed succesful. Elapsed time: " + status.time + "ms.";
                    ((ExtractorItem)extractorListView.Items[status.extractorNumber]).Icon = new BitmapImage(new Uri(@"/Images/successful.png", UriKind.Relative));
                }
                extractorListView.Items.Refresh();

                ProgressingEventsArg args = new ProgressingEventsArg(e.ProgressPercentage, -1, logMessage);
                OnExtractionProgressing(args);
            }
            else
            {
                ProgressingEventsArg args = new ProgressingEventsArg(e.ProgressPercentage);
                OnExtractionProgressing(args);
            }
        }

        unsafe private void ExtractionBackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            int progress = 0;
            int stage = 0;
            long currentTime = 0;
            operationTime = 0;
            e.Result = 0;
            callback = (value) =>
            {
                progress = value + filesCount * stage;
                worker.ReportProgress(progress);
            };
            
            string negativeSamplesPath = "";
            string positiveSamplesPath = "";
            string savePath = "";
            bool append = false;
            ImageFileTypeSelector.FileType fileType = ImageFileTypeSelector.FileType.text;

            Dispatcher.Invoke(new Action(() =>
            {
                fileType = extractionSaveSettings.SelectedFileType;
                negativeSamplesPath = negativeSamplesPathTextBox.Text;
                positiveSamplesPath = positiveSamplesPathTextBox.Text;
                savePath = saveInPathTextBox.Text;
                append = (extractionAppendRadioButton.IsChecked == null || extractionAppendRadioButton.IsChecked == false) ? false : true;
            }));

            for (int i = 0; i < extNames.Count; i++)
            {
                stage = i;
                if (this.worker.CancellationPending)
                {
                    for (int j = i; j < extNames.Count; j++)
                    {
                        ExtractionStatus cancelStatus = new ExtractionStatus()
                        {
                            error = (int)GlobalFunctions.ERRORS.OPERATION_CANCELED,
                            name = extNames[j],
                            time = 0,
                            extractorNumber = j
                        };

                        worker.ReportProgress(progress, cancelStatus);
                    }
                    e.Cancel = true;
                    return;
                }

                string extractorPath = savePath + extNames[i];
                int[] parameters = extParameters[i];
                string extractorType = extTypes[i];

                int status;
                fixed (int* parPointer = parameters)
                {
                    Stopwatch stopwatch = new Stopwatch();
                    stopwatch.Start();

                    status = NativeMethods.Extraction(negativeSamplesPath, positiveSamplesPath, extractorPath, extractorType, parPointer, fileType, append, callback);

                    stopwatch.Stop();
                    currentTime = stopwatch.ElapsedMilliseconds;
                    operationTime += currentTime;

                    if (status < 0)
                        GlobalFunctions.ThrowError(status);
                }


                ExtractionStatus extStatus;

                if (status < 0)
                {
                    extStatus.error = status;
                    e.Result = status;
                }
                else
                {
                    extStatus.error = 0;
                }
                extStatus.name = extNames[i];
                extStatus.time = currentTime;
                extStatus.extractorNumber = i;

                worker.ReportProgress(progress, extStatus);
            }
        }

        private void ExtractionBackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            string statusLabel;
            string logMessage;
            string error = null;
            int errorCode = 0;

            if (!e.Cancelled && e.Error == null)
                errorCode = (int)e.Result;


            if (e.Cancelled)
            {
                statusLabel = "Status: Extraction cancelled. Check event log for details";
                logMessage = "Extraction cancelled.";
            }
            else if (e.Error != null)
            {
                statusLabel = "Status: Extraction completed with errors. Check event log for details.";
                logMessage = "Extraction completed with errors: ";
                error = e.Error.Message;
            }
            else if (errorCode < 0)
            {
                statusLabel = "Status: Extraction completed with errors. Check event log for details.";
                logMessage = "Extraction completed with errors.";
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
