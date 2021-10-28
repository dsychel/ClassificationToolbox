using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.IO;
using System.ComponentModel;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Diagnostics;

namespace ClassificationToolboxWPF
{
    /// <summary>
    /// Interaction logic for ClassifierTrainer.xaml
    /// </summary>
    public partial class ClassifierTrainer : UserControl
    {
        #region Structs
        struct LearningStatus
        {
            public string name;
            public string type;
            public int error;
            public double accurancy;
            public long time;
        }

        private class ClassifierItem
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public ImageSource Icon { get; set;}
        }
        #endregion

        #region Fields
        BackgroundWorker worker;
        readonly List<string> clsNames = new List<string>();
        readonly List<string> clsType = new List<string>();
        readonly List<NativeMethods.ClassifierParameters> clsParameters = new List<NativeMethods.ClassifierParameters>();
        long operationTime;
        #endregion

        #region Constructors
        public ClassifierTrainer()
        {
            InitializeComponent();

            GlobalFunctions.InitializeDirectory(ref classifierSaveInTextBox, "learningClassifierPath");
            GlobalFunctions.InitializePath(ref learningFeaturesTextBox, "learningFeaturesPath");
            
            classifierSettings.FeaturesName = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(learningFeaturesTextBox.Text));
        }
        #endregion

        #region Methods
        public void StopTask()
        {
            if (worker != null && worker.IsBusy)
                worker.CancelAsync();
        }

        //private int ListFiles(string path, string extension)
        //{
        //    List<String> fileList = new List<string>();
        //    SearchOption so = SearchOption.TopDirectoryOnly;
        //    fileList.AddRange(Directory.EnumerateFiles(path, extension, so).ToArray());

        //    if (fileList.Count > 0)
        //    {
        //        using (StreamWriter sw = new StreamWriter(path + "file.lst"))
        //        {
        //            for (int f = 0; f < fileList.Count - 1; f++)
        //                sw.WriteLine(fileList[f]);
        //            sw.Write(fileList[fileList.Count - 1]);
        //        }
        //    }
        //    return fileList.Count;
        //}
        #endregion

        #region Triggers
        public event EventHandler<StartedEventsArg> TrainingStarted;
        protected virtual void OnTrainingStarted(StartedEventsArg e)
        {
            TrainingStarted?.Invoke(this, e);
        }

        public event EventHandler<ProgressingEventsArg> TrainingProgressing;
        protected virtual void OnTrainingProgressing(ProgressingEventsArg e)
        {
            TrainingProgressing?.Invoke(this, e);
        }

        public event EventHandler<CompletionEventsArg> TrainingCompletion;
        protected virtual void OnTrainingCompletion(CompletionEventsArg e)
        {
            TrainingCompletion?.Invoke(this, e);
        }
        #endregion

        #region Events
        private void ClassfierSaveInPathBrowseButton_Click(object sender, RoutedEventArgs e)
        {
            GlobalFunctions.SelectFolder(ref classifierSaveInTextBox, "learningClassifierPath");
        }

        private void ClassfieLearningFeaturesPathBrowseButton_Click(object sender, RoutedEventArgs e)
        {
            if (GlobalFunctions.SelectFeatures(ref learningFeaturesTextBox, "learningFeaturesPath") == System.Windows.Forms.DialogResult.OK)
                classifierSettings.FeaturesName = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(learningFeaturesTextBox.Text));
        }

        private void ClassfieValidationFeaturesPathBrowseButton_Click(object sender, RoutedEventArgs e)
        {
            GlobalFunctions.SelectOptionalFeatures(ref valdiationFeaturesTextBox, "learningValidationFeaturesPath");
        }

        private void AddClassifierButton_Click(object sender, RoutedEventArgs e)
        {
            string name = classifierSettings.ClassifierName;
            string description = classifierSettings.ClassifierDescription;
            if (name == ".model")
            {
                MessageBox.Show("Classifier name is empty.");
                return;
            }
            if (clsNames.Contains(name))
            {
                MessageBox.Show("Classifier with given name has already been added to the list.");
                return;
            }
            if (File.Exists(classifierSaveInTextBox.Text + name))
            {
                MessageBox.Show("Classifier with given name exist in selected folder.");
                return;
            }

            clsNames.Add(name);
            clsType.Add(classifierSettings.ClassifierType);
            clsParameters.Add(classifierSettings.ClassifierParameters);
            classifierListView.Items.Add(new ClassifierItem { Name = name, Description = description, Icon = new BitmapImage(new Uri(@"/Images/pending.png", UriKind.Relative)) });
        }

        private void RemoveClassifierButton_Click(object sender, RoutedEventArgs e)
        {
            if (classifierListView.SelectedIndex >= 0)
            {
                int index = classifierListView.SelectedIndex;

                classifierListView.Items.RemoveAt(index);
                clsNames.RemoveAt(index);
                clsType.RemoveAt(index);
                clsParameters.RemoveAt(index);
            }
        }

        private void LearnButton_Click(object sender, RoutedEventArgs e)
        {
//#if DEBUG
//            classifierSaveInTextBox.Text = @"D:\Datatsets\Faces\Classifier";
//            learningFeaturesTextBox.Text = @"D:\Datatsets\Faces\Features\haarFeatures6t7s7p_TRAIN_SIMPLIFY.fet.bin";
//            valdiationFeaturesTextBox.Text = @"D:\Datatsets\Faces\Features\haarFeatures6t7s7p_VALIDATE_SMALL.fet.bin";
//#endif

            if (learningFeaturesTextBox.Text == "" || !File.Exists(learningFeaturesTextBox.Text))
            {
                MessageBox.Show("Features path is empty or file doesn't exist.");
                return;
            }
            if (classifierSaveInTextBox.Text == "")
            {
                MessageBox.Show("Classifier path is empty.");
                return;
            }

            if (worker != null && worker.IsBusy)
            {
                MessageBox.Show("Application is busy.");
                return;
            }

            for (int i = 0; i < classifierListView.Items.Count; i++)
                ((ClassifierItem)classifierListView.Items[i]).Icon = new BitmapImage(new Uri(@"/Images/pending.png", UriKind.Relative));
            classifierListView.Items.Refresh();

            string logMessage = "Classifier learning started. Features used: " + Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(learningFeaturesTextBox.Text)) + ".";
            StartedEventsArg args = new StartedEventsArg("Status: Working", logMessage, DateTime.Now, classifierListView.Items.Count, true);
            OnTrainingStarted(args);

            worker = new BackgroundWorker()
            {
                WorkerSupportsCancellation = true,
                WorkerReportsProgress = true
            };
            worker.DoWork += LearningBackgroundWorker_DoWork;
            worker.RunWorkerCompleted += LearningBackgroundWorker_RunWorkerCompleted;
            worker.ProgressChanged += LearningBackgroundWorker_ProgressChanged;
            worker.RunWorkerAsync();
        }

        private void LearningBackgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            LearningStatus status = ((LearningStatus)e.UserState);

            string logMessage;
            if (status.error == -10)
            {
                logMessage = "Training of classifier \"" + Path.GetFileNameWithoutExtension(status.name) + "\" (" + status.type + ")" + " canceled.";
                ((ClassifierItem)classifierListView.Items[e.ProgressPercentage - 1]).Icon = new BitmapImage(new Uri(@"/Images/error.png", UriKind.Relative));
            }
            else if (status.error < 0)
            {
                logMessage = "Training of classifier \"" + Path.GetFileNameWithoutExtension(status.name) + "\" (" + status.type + ")" + " completed with error: " + GlobalFunctions.GetErrorDescription(status.error);
                ((ClassifierItem)classifierListView.Items[e.ProgressPercentage - 1]).Icon = new BitmapImage(new Uri(@"/Images/error.png", UriKind.Relative));
            }
            else
            {
                logMessage = "Training of classifier \"" + Path.GetFileNameWithoutExtension(status.name) + "\" (" + status.type + ")" + " completed succesful. Accurancy: " + status.accurancy + ". " + "Elapsed time: " + status.time + "ms.";
                ((ClassifierItem)classifierListView.Items[e.ProgressPercentage - 1]).Icon = new BitmapImage(new Uri(@"/Images/successful.png", UriKind.Relative));
            }
            classifierListView.Items.Refresh();

            ProgressingEventsArg args = new ProgressingEventsArg(e.ProgressPercentage, -1, logMessage);
            OnTrainingProgressing(args);
        }

        unsafe private void LearningBackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            e.Result = 0;
            string savePath = "";
            string featurePath = "";
            string validationPath = "";

            Dispatcher.Invoke(new Action(() =>
            {
                savePath = classifierSaveInTextBox.Text;
                featurePath = learningFeaturesTextBox.Text;
                validationPath = valdiationFeaturesTextBox.Text;
            }));

            int code = NativeMethods.LoadLearningData(featurePath);
            if (code < 0)
                GlobalFunctions.ThrowError(code);

            for (int i = 0; i < clsNames.Count; i++)
            {
                if (this.worker.CancellationPending)
                {
                    for (int j = i; j < clsNames.Count; j++)
                    {
                        LearningStatus cancelStatus = new LearningStatus()
                        {
                            error = (int)GlobalFunctions.ERRORS.OPERATION_CANCELED,
                            name = clsNames[j],
                            time = 0,
                            type = clsType[j]
                        };

                        worker.ReportProgress(j + 1, cancelStatus);
                    }

                    e.Cancel = true;
                    return;
                }

                string classifierPath = savePath + clsNames[i];

                //if(clsType[i].Contains("Cascade"))
                //{
                //    ListFiles(clsParameters[i].nonFaceImagesPath + "\\", "*.gray.8bin"); 
                //}

                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                double accurancy = NativeMethods.Learn(classifierPath, clsType[i], clsParameters[i], validationPath);

                stopwatch.Stop();
                operationTime = stopwatch.ElapsedMilliseconds;

                LearningStatus status;

                if (accurancy < 0)
                {
                    status.error = (int)accurancy;
                    status.accurancy = 0.0;
                    e.Result = (int)accurancy;
                }
                else
                {
                    status.error = 0;
                    status.accurancy = accurancy;
                }
                status.name = clsNames[i];
                status.time = operationTime;
                status.type = clsType[i];

                worker.ReportProgress(i + 1, status);
            }
            NativeMethods.FreeLearningData();
        }

        private void LearningBackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            string statusLabel;
            string logMessage;
            string error = null;
            int errorCode = 0;

            if (!e.Cancelled && e.Error == null)
                errorCode = (int)e.Result;

            if (e.Cancelled)
            {
                statusLabel = "Status: Training cancelled. Check event log for details";
                logMessage = "Training cancelled.";
            }
            else if (e.Error != null)
            {
                statusLabel = "Status: Training completed with errors. Check event log for details.";
                logMessage = "Training completed with errors:";
                error = e.Error.Message;
            }
            else if (errorCode < 0)
            {
                statusLabel = "Status: Training completed with errors. Check event log for details.";
                logMessage = "Training completed with errors.";
            }
            else
            {
                statusLabel = "Status: Training completed successful. Check event log for details.";
                logMessage = "Training completed successful.";
            }

            bool shutdown = (shutdownLearningCheckBox.IsChecked == null || shutdownLearningCheckBox.IsChecked == false) ? false : true;
            CompletionEventsArg args = new CompletionEventsArg(statusLabel, logMessage, error, DateTime.Now, shutdown);
            OnTrainingCompletion(args);

            worker.Dispose();
            worker = null;
        }
#endregion
    }
}
