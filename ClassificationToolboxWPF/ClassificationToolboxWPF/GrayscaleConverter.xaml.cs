using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading;
using System.Threading.Tasks;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;

namespace ClassificationToolboxWPF
{
    /// <summary>
    /// Interaction logic for GrayscaleConverter.xaml
    /// </summary>
    public partial class GrayscaleConverter : UserControl
    {
        #region Fields
        BackgroundWorker worker;
        long operationTime;

        int FileNumber = 0;
        #endregion

        #region Constructors
        public GrayscaleConverter()
        {
            InitializeComponent();

            GlobalFunctions.InitializeDirectory(ref imageFolderTextBox, "grayScaleConversionImagePath");
            GlobalFunctions.InitializeDirectory(ref outputFolderTextBox, "grayScaleConversionOutputPath");

            BuildImageFilesStatistic(Properties.Settings.Default.grayScaleConversionImagePath);
        }
        #endregion

        #region Methods
        public void StopTask()
        {
            if (worker != null && worker.IsBusy)
                worker.CancelAsync();
        }

        private void CheckFolder(string path, ref int jpgs, ref int bmps, ref int pngs)
        {
            try
            {
                SearchOption so = SearchOption.TopDirectoryOnly;
                StringComparison sc = StringComparison.OrdinalIgnoreCase;
                IEnumerable<string> files = Directory.EnumerateFiles(path, "*.*", so);

                jpgs += files.Count(s => s.EndsWith(".jpg", sc) || s.EndsWith(".jpeg", sc));
                bmps += files.Count(s => s.EndsWith(".bmp", sc));
                pngs += files.Count(s => s.EndsWith(".png", sc));

                if (includeSubfolderCheckBox.IsChecked == true)
                {
                    IEnumerable<string> directories = Directory.EnumerateDirectories(path);
                    foreach (string directory in directories)
                        CheckFolder(directory, ref jpgs, ref bmps, ref pngs);
                }
            }
            catch
            {
                statisticTextBox.Text += "Ignored folder: " + path + "\r\n";
            }
        }

        private void BuildImageFilesStatistic(string path)
        {
            statisticTextBox.Text = "Conversion to gray scale binary file.\r\n" + "\r\n";
            int bmps = 0, jpgs = 0, pngs = 0, total;

            if (Directory.Exists(path))
            {
                    CheckFolder(path, ref jpgs, ref bmps, ref pngs);
                    statisticTextBox.Text += "\r\n";

                    statisticTextBox.Text += "PNG files count: " + pngs + "\r\n";
                    statisticTextBox.Text += "BMP files count: " + bmps + "\r\n";
                    statisticTextBox.Text += "JPG files count: " + jpgs + "\r\n" + "\r\n";
                    total = bmps + jpgs + pngs;

                    statisticTextBox.Text += "All files: " + total + "\r\n" + "\r\n";
            }
            else
            {
                Properties.Settings.Default.grayScaleConversionImagePath = "";
                Properties.Settings.Default.Save();
                imageFolderTextBox.Text = "";
            }
        }
        #endregion

        #region Triggers
        public event EventHandler<StartedEventsArg> ConversionStarted;
        protected virtual void OnConversionStarted(StartedEventsArg e)
        {
            ConversionStarted?.Invoke(this, e);
        }

        public event EventHandler<ProgressingEventsArg> ConversionProgressing;
        protected virtual void OnConversionProgressing(ProgressingEventsArg e)
        {
            ConversionProgressing?.Invoke(this, e);
        }

        public event EventHandler<CompletionEventsArg> ConversionCompleted;
        protected virtual void OnConversionCompletion(CompletionEventsArg e)
        {
            ConversionCompleted?.Invoke(this, e);
        }
        #endregion

        #region Events
        private void SelectImageFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (GlobalFunctions.SelectFolder(ref imageFolderTextBox, "grayScaleConversionImagePath") == System.Windows.Forms.DialogResult.OK)
            {
                statisticTextBox.Text = "Conversion to grayscale binary file.\r\n" + "\r\n";
                BuildImageFilesStatistic(imageFolderTextBox.Text);
            }
        }

        private void SelectOutputFolderButton_Click(object sender, RoutedEventArgs e)
        {
            GlobalFunctions.SelectFolder(ref outputFolderTextBox, "grayScaleConversionOutputPath");
        }

        private void IncludeSubfolderCheckBox_CheckedChange(object sender, RoutedEventArgs e)
        {
            if (statisticTextBox != null)
            {
                statisticTextBox.Text = "Conversion to gray scale binary file.\r\n" + "\r\n";

                BuildImageFilesStatistic(imageFolderTextBox.Text);
            }
        }

        private void ConvertImagesButton_Click(object sender, RoutedEventArgs e)
        {
            if (imageFolderTextBox.Text == "" || !Directory.Exists(imageFolderTextBox.Text))
            {
                MessageBox.Show("Images path is empty or doesn't exist.");
                return;
            }
            if (outputFolderTextBox.Text == "" || !Directory.Exists(outputFolderTextBox.Text))
            {
                MessageBox.Show("Output path is empty or doesn't exist.");
                return;
            }

            if (worker != null && worker.IsBusy)
            {
                MessageBox.Show("Application is busy.");
                return;
            }

            statisticTextBox.Text = "Conversion to grayscale.\r\n" + "\r\n";

            StartedEventsArg args = new StartedEventsArg("Status: Converting images", "Conversion to grayscale started:", DateTime.Now, 0, true);
            OnConversionStarted(args);

            worker = new BackgroundWorker()
            {
                WorkerSupportsCancellation = true,
                WorkerReportsProgress = true
            };
            worker.DoWork += ImageConversionBackgroundWorker_DoWork;
            worker.RunWorkerCompleted += ImageConversionBackgroundWorker_RunWorkerCompleted;
            worker.ProgressChanged += ImageConversionBackgroundWorker_ProgressChanged;
            worker.RunWorkerAsync();
        }

        private void ImageConversionBackgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            ProgressingEventsArg args = new ProgressingEventsArg(e.ProgressPercentage);
            OnConversionProgressing(args);
        }

        private void ImageConversionBackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            ParallelOptions parOpt = new ParallelOptions()
            {
                MaxDegreeOfParallelism = (int)Properties.Settings.Default.tplThreads
            };

            HashSet<string> errors = new HashSet<string>();
            List<string> fileList = new List<string>();

            bool includeSubFolder = false, keepFolderStructure = false;
            string imagesPath = "", outputPath = "";
            Dispatcher.Invoke(new Action(() =>
            {
                includeSubFolder = (includeSubfolderCheckBox.IsChecked == null || includeSubfolderCheckBox.IsChecked == false) ? false : true;
                keepFolderStructure = (keepFolderStructureCheckBox.IsChecked == null || keepFolderStructureCheckBox.IsChecked == false) ? false : true;
                imagesPath = imageFolderTextBox.Text;
                outputPath = outputFolderTextBox.Text;

                BuildImageFilesStatistic(imagesPath);
            }));

            GlobalFunctions.GetImageFiles(ref fileList, imagesPath, includeSubFolder);
            ProgressingEventsArg args = new ProgressingEventsArg(0, fileList.Count());
            OnConversionProgressing(args);

            Object lockMe = new Object();
            FileNumber = 0;
            Parallel.ForEach(fileList, parOpt, (string file) =>
            {
                try
                {
                    // Generate Path
                    FileInfo fileInfo = new FileInfo(file);
                    if (!fileInfo.Exists)
                        throw new Exception(file + " doesn't exist.");

                    string path;
                    if (keepFolderStructure == true && fileInfo.Directory.FullName.Length + 1 != imagesPath.Length)
                    {
                        string folder = outputPath + fileInfo.Directory.FullName.Substring(imagesPath.Length);
                        if (!Directory.Exists(folder))
                            Directory.CreateDirectory(folder);
                        path = folder + "\\" + Path.GetFileNameWithoutExtension(fileInfo.Name);
                    }
                    else
                        path = outputPath + Path.GetFileNameWithoutExtension(fileInfo.Name);

                    // Format Conversion to 24RGB
                    Bitmap tmp = new Bitmap(file);
                    Bitmap bmp = new Bitmap(tmp.Width, tmp.Height, PixelFormat.Format24bppRgb);

                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        if (tmp.PixelFormat == PixelFormat.Format32bppArgb)
                            g.Clear(Color.White);
                        g.DrawImage(tmp, new Rectangle(0, 0, bmp.Width, bmp.Height));
                    }
                    tmp.Dispose();

                    // Conversion to gray
                    BitmapData bData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadWrite, bmp.PixelFormat);
                    byte bytePerPixel = (byte)(System.Drawing.Image.GetPixelFormatSize(bData.PixelFormat) / 8);
                    int size = bData.Stride * bData.Height;
                    byte[] data = new byte[size];
                    Marshal.Copy(bData.Scan0, data, 0, size);

                    #region 64Bit
                    if (conversionSaveSettings.SelectedFileType == ImageFileTypeSelector.FileType.binary64bit)
                    {
                        using (BinaryWriter sw = new BinaryWriter(File.Open(path + ".gray.64bin", FileMode.Create)))
                        {
                            double outData;

                            sw.Write(bData.Width);
                            sw.Write(bData.Height);
                            for (int h = 0; h < bData.Height; h++)
                            {
                                for (int w = 0; w < bData.Width; w++)
                                {
                                    int id = h * bData.Stride + w * bytePerPixel;
                                    outData = Math.Round((0.1140 * data[id + 0] + 0.5870 * data[id + 1] + 0.2990 * data[id + 2])) / 255.0;
                                    sw.Write(outData);
                                }
                            }
                        }
                    }
                    #endregion
                    #region 8Bit
                    else if (conversionSaveSettings.SelectedFileType == ImageFileTypeSelector.FileType.binary8bit)
                    {
                        byte outData;
                        using (BinaryWriter sw = new BinaryWriter(File.Open(path + ".gray.8bin", FileMode.Create)))
                        {
                            sw.Write(bData.Width);
                            sw.Write(bData.Height);

                            for (int h = 0; h < bData.Height; h++)
                            {
                                for (int w = 0; w < bData.Width; w++)
                                {
                                    int id = h * bData.Stride + w * bytePerPixel;
                                    outData = (byte)Math.Round((0.1140 * data[id + 0] + 0.5870 * data[id + 1] + 0.2990 * data[id + 2]));
                                    sw.Write(outData);
                                }
                            }
                        }
                    }
                    #endregion
                    #region Text
                    else if (conversionSaveSettings.SelectedFileType == ImageFileTypeSelector.FileType.text)
                    {
                        double outData;
                        string format = "{0:0." + new string('#', Dispatcher.Invoke(new Func<int>(() => { return (int)conversionSaveSettings.DecimalPlaces; }))) + "} ";
                        using (StreamWriter sw = new StreamWriter(path + ".gray.txt"))
                        {
                            for (int h = 0; h < bData.Height; h++)
                            {
                                for (int w = 0; w < bData.Width; w++)
                                {
                                    int id = h * bData.Stride + w * bytePerPixel;
                                    outData = Math.Round((0.1140 * data[id + 0] + 0.5870 * data[id + 1] + 0.2990 * data[id + 2])) / 255.0;

                                    if (w < bData.Width - 1)
                                        sw.Write(String.Format(CultureInfo.InvariantCulture, format, outData) + " ");
                                    else
                                        sw.Write(String.Format(CultureInfo.InvariantCulture, format, outData));
                                }
                                if (h < bData.Height - 1)
                                    sw.WriteLine();
                            }
                        }
                    }
                    #endregion
                    #region Image
                    else
                    {
                        for (int h = 0; h < bData.Height; h++)
                        {
                            for (int w = 0; w < bData.Width; w++)
                            {
                                int id = h * bData.Stride + w * bytePerPixel;
                                data[id] = (byte)Math.Round((0.1140 * data[id + 0] + 0.5870 * data[id + 1] + 0.2990 * data[id + 2]));
                                data[id + 1] = data[id];
                                data[id + 2] = data[id];
                            }
                        }
                        Marshal.Copy(data, 0, bData.Scan0, size);

                        if (conversionSaveSettings.SelectedFileType == ImageFileTypeSelector.FileType.jpeg)
                            bmp.Save(path + ".jpg", ImageFormat.Jpeg);
                        if (conversionSaveSettings.SelectedFileType == ImageFileTypeSelector.FileType.bitmap)
                            bmp.Save(path + ".bmp", ImageFormat.Bmp);
                        if (conversionSaveSettings.SelectedFileType == ImageFileTypeSelector.FileType.png)
                            bmp.Save(path + ".png", ImageFormat.Png);
                    }
                    #endregion
                    bmp.UnlockBits(bData);
                    bmp.Dispose();

                    lock (lockMe)
                    {
                        worker.ReportProgress(++FileNumber);

                        if (this.worker.CancellationPending)
                        {
                            e.Cancel = true;
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    lock (lockMe)
                    {
                        ProgressingEventsArg errArgs = new ProgressingEventsArg(FileNumber, -1, ex.Message);
                        OnConversionProgressing(errArgs);

                        Dispatcher.Invoke(new Action(() =>
                        {
                            statisticTextBox.Text += ex.Message + "\r\n";
                        }));

                        if (worker.CancellationPending)
                        {
                            e.Cancel = true;
                            return;
                        }
                    }
                }
            });

            stopwatch.Stop();
            operationTime = stopwatch.ElapsedMilliseconds;

            if (FileNumber != fileList.Count)
            {
                throw new Exception("Not all files were converted.");
            }
        }

        private void ImageConversionBackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            string statusLabel;
            string logMessage;
            string error = null;

            if (e.Cancelled)
            {
                statusLabel = "Status: Conversion cancelled. Check event log for details";
                logMessage = "Conversion cancelled.";
            }
            else if (e.Error != null)
            {
                statusLabel = "Status: Conversion completed with errors. Check event log for details.";
                logMessage = "Conversion completed with errors: " + FileNumber + " files converted.";
                error = e.Error.Message;
                statisticTextBox.Text += "\r\n" + FileNumber + " files converted." + "\r\n" + e.Error.Message;
            }
            else
            {
                statusLabel = "Status: Conversion completed successful. Check event log for details.";
                logMessage = "Conversion completed successful. Elapsed time: " + operationTime + "ms. Processed files: " + FileNumber;
                statisticTextBox.Text += FileNumber + " files converted.";
            }

            bool shutdown = (shutdownCheckBox.IsChecked == null || shutdownCheckBox.IsChecked == false) ? false : true;
            CompletionEventsArg args = new CompletionEventsArg(statusLabel, logMessage, error, DateTime.Now, shutdown);
            OnConversionCompletion(args);

            worker.Dispose();
            worker = null;
        }
        #endregion
    }
}
