using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.ComponentModel;
using System.IO;
using Microsoft.SqlServer.Server;

namespace ClassificationToolboxWPF
{
    /// <summary>
    /// Interaction logic for ZernikeComparsionControl.xaml
    /// </summary>
    public partial class ZernikeComparsionControl : UserControl
    {
        #region Fields
        readonly BackgroundWorker worker = new BackgroundWorker() { WorkerReportsProgress = true, WorkerSupportsCancellation = false };
        NativeMethods.ProgressCallback callback;

        int[] extractorParams = new int[5] { 8, 8, 6, 1, 100 };
        int size;
        #endregion

        public ZernikeComparsionControl()
        {
            InitializeComponent();

            worker.DoWork += ComparisonBackgroundWorker_DoWork;
            worker.ProgressChanged += ComparisonBackgroundWorker_ProgressChanged;
            worker.RunWorkerCompleted += ComparisonBackgroundWorker_RunWorkerCompleted;
        }

        #region Triggers
        public event EventHandler<StartedEventsArg> ComparisonStarted;
        protected virtual void OnComparisonStarted(StartedEventsArg e)
        {
            ComparisonStarted?.Invoke(this, e);
        }

        public event EventHandler<ProgressingEventsArg> ComparisonProgressing;
        protected virtual void OnComparisonProgressing(ProgressingEventsArg e)
        {
            ComparisonProgressing?.Invoke(this, e);
        }

        public event EventHandler<StatusChangedArg> ComparisonStatusChanged;
        protected virtual void OnComparisonStatusChanged(StatusChangedArg e)
        {
            ComparisonStatusChanged?.Invoke(this, e);
        }

        public event EventHandler<CompletionEventsArg> ComparisonCompletion;
        protected virtual void OnComparisonCompletion(CompletionEventsArg e)
        {
            ComparisonCompletion?.Invoke(this, e);
        }
        #endregion

        #region Events
        private void SelectImageButton_Click(object sender, RoutedEventArgs e)
        {

            if (GlobalFunctions.SelectImage(out string fileName, "zernikeComparisonImage") == System.Windows.Forms.DialogResult.OK)
            {
                PixelFormat pf = PixelFormats.Bgr24;
                BitmapSource bmp = new BitmapImage(new Uri(fileName, UriKind.Relative));
                if (bmp.Format != PixelFormats.Bgr24)
                    bmp = new FormatConvertedBitmap(bmp, pf, null, 0);

                imagePathTextBox.Text = fileName;

                int widthbyte = (bmp.PixelWidth * pf.BitsPerPixel + 7) / 8;
                int stride = ((widthbyte + 3) / 4) * 4;
                byte[] data = new byte[stride * bmp.PixelHeight];
                bmp.CopyPixels(data, stride, 0);

                originalImagePictureBox.Source = new WriteableBitmap(bmp.PixelWidth, bmp.PixelHeight, 96d, 96d, pf, null);

                var rect = new Int32Rect(0, 0, bmp.PixelWidth, bmp.PixelHeight);
                ((WriteableBitmap)originalImagePictureBox.Source).WritePixels(rect, data, stride, 0);

                sizeNumericUpDown.Maximum = Math.Min(bmp.PixelWidth, bmp.PixelHeight);
                dNumericUpDown.Maximum = Math.Min(bmp.PixelWidth, bmp.PixelHeight);
                if(sizeNumericUpDown.Value > sizeNumericUpDown.Maximum)
                    sizeNumericUpDown.Value = sizeNumericUpDown.Maximum;
                if (dNumericUpDown.Value > dNumericUpDown.Maximum)
                    dNumericUpDown.Value = dNumericUpDown.Maximum;
            }
        }
        private void sizeNumericUpDown_ValueChanged(object sender, ValueChangedEventArg e)
        {
            dNumericUpDown.Minimum = (decimal)e.CurrentValue;
            if (dNumericUpDown.Value < dNumericUpDown.Minimum)
                dNumericUpDown.Value = dNumericUpDown.Minimum;
        }

        private void CompareButton_Click(object sender, RoutedEventArgs e)
        {
            if (imagePathTextBox.Text == "" || !File.Exists(imagePathTextBox.Text))
            {
                MessageBox.Show("Image path is empty or file does not exist.");
                return;
            }

            matlabCodeTextBox.Text = "% Matlab code:\r\n\r\n";
            extractorParams[0] = (int)pNumericUpDown.Value;
            extractorParams[1] = (int)qNumericUpDown.Value;
            extractorParams[2] = (int)rNumericUpDown.Value;
            extractorParams[3] = (int)rtNumericUpDown.Value;
            extractorParams[4] = (int)dNumericUpDown.Value;

            int windowsSize = (int)sizeNumericUpDown.Value;
            int width = ((WriteableBitmap)originalImagePictureBox.Source).PixelWidth;
            int height = ((WriteableBitmap)originalImagePictureBox.Source).PixelHeight;
            size = (width - windowsSize + 1) * (height - windowsSize + 1);

            StartedEventsArg args = new StartedEventsArg("Status: Working", "Zernike extractors comparison.", DateTime.Now, size + 1, true);
            OnComparisonStarted(args);

            worker.RunWorkerAsync();
        }

        private void ComparisonBackgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            ProgressingEventsArg args = new ProgressingEventsArg(e.ProgressPercentage);
            OnComparisonProgressing(args);
        }

        unsafe private void ComparisonBackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            #region Configuration
            callback = (value) =>
            {
                worker.ReportProgress(value);
            };

            int windowsSize = Dispatcher.Invoke(new Func<int>(() => { return (int)sizeNumericUpDown.Value; }));
            double threshold = Dispatcher.Invoke(new Func<double>(() => { return (double)thrNumericUpDown.Value; }));

            int width = Dispatcher.Invoke(new Func<int>(() => { return ((WriteableBitmap)originalImagePictureBox.Source).PixelWidth; }));
            int height = Dispatcher.Invoke(new Func<int>(() => { return ((WriteableBitmap)originalImagePictureBox.Source).PixelHeight; }));
            int stride = Dispatcher.Invoke(new Func<int>(() => { return ((WriteableBitmap)originalImagePictureBox.Source).BackBufferStride; }));
            int bytesPerPixel = Dispatcher.Invoke(new Func<int>(() => { return (int)(((WriteableBitmap)originalImagePictureBox.Source).Format.BitsPerPixel / 8); }));
            byte[] data = new byte[stride * height];
            // możliwe że nie zadziała bez tego dla nieparzystych wymiarów loadImage() zaokragla wymiary
            width = width - width % 2; 
            height = height - height % 2;

            size = (width - windowsSize + 1) * (height - windowsSize + 1);
            double[] resultsII = new double[size + 1];
            double[] resultsFPII = new double[size + 1];

            Dispatcher.Invoke(new Action(() => { ((WriteableBitmap)originalImagePictureBox.Source).CopyPixels(data, stride, 0); }));
            #endregion

            fixed (double* resultsIIPointer = resultsII)
            {
                fixed (double* resultsFPIIPointer = resultsFPII)
                {
                    fixed (int* parPointer = extractorParams)
                    {
                        fixed (byte* dataPointer = data)
                        {
                            NativeMethods.ZernikeComparison(resultsIIPointer, resultsFPIIPointer, parPointer, dataPointer, bytesPerPixel, width, height, stride, windowsSize, threshold, callback);
                        }
                    }
                }
            }

            //string[] split;
            //using (System.IO.StreamReader file = new System.IO.StreamReader(@"dataForTest.txt") )
            //{
            //    split = file.ReadLine().Split();
            //    for (int i = 0; i < size; i++)
            //        resultsII[i] = Double.Parse(split[i]);
            //    split = file.ReadLine().Split();
            //    for (int i = 0; i < size; i++)
            //        resultsFPII[i] = Double.Parse(split[i]);
            //}

            int width2 = width - windowsSize + 1;
            int height2 = height - windowsSize + 1;
            StringBuilder mcode = new StringBuilder();
            mcode.Append("close all\r\n");
            mcode.Append("clear\r\n\r\n");

            mcode.Append("IIerrors = [");
            for (int i = 0; i < size; i++)
                mcode.Append(resultsII[i].ToString() + " ");
            mcode.Append("];\r\n");

            mcode.Append("FPIIerrors = [");
            for (int i = 0; i < size; i++)
                mcode.Append(resultsFPII[i].ToString() + " ");
            mcode.Append("];\r\n\r\n");

            mcode.Append("IIerrors = reshape(IIerrors, " + height2 + ", " + width2 + ");\r\n");
            mcode.Append("FPIIerrors = reshape(FPIIerrors, " + height2 + ", " + width2 + ");\r\n");
            mcode.Append("maxVal1 = max(max(IIerrors));\r\n");
            mcode.Append("maxVal2 = max(max(FPIIerrors));\r\n");
            mcode.Append("maxVal = max([maxVal1 maxVal2])\r\n\r\n");

            mcode.Append("figure\r\n");
            mcode.Append("imshow(IIerrors, [0 maxVal])\r\n");
            mcode.Append("colorbar\r\n\r\n");

            mcode.Append("figure\r\n");
            mcode.Append("imshow(FPIIerrors, [0 maxVal])\r\n");
            mcode.Append("colorbar\r\n\r\n");

            mcode.Append("features = " + ((int)resultsII[size]).ToString() + ";\r\n");
            mcode.Append("figure\r\n");
            mcode.Append("imshow(IIerrors / features * 100, [0 (maxVal / features * 100)])\r\n");
            mcode.Append("colorbar\r\n\r\n");

            mcode.Append("figure\r\n");
            mcode.Append("imshow(FPIIerrors / features * 100, [0 (maxVal / features * 100)])\r\n");
            mcode.Append("colorbar\r\n\r\n");

            mcode.Append("avgErrII = mean(IIerrors, 'all')  / features * 100\r\n");
            mcode.Append("avgErrFPII = mean(FPIIerrors, 'all')  / features * 100\r\n\r\n");

            Dispatcher.Invoke(new Action(() => { matlabCodeTextBox.Text += mcode.ToString(); }));

            Dispatcher.Invoke(new Action(() => 
            {
                zernikeIIImagePictureBox.Source = new WriteableBitmap(width2, height2, 96d, 96d, PixelFormats.Bgr24, null);
                zernikeFPIIImagePictureBox.Source = new WriteableBitmap(width2, height2, 96d, 96d, PixelFormats.Bgr24, null);

                WriteableBitmap bmpII = (WriteableBitmap)zernikeIIImagePictureBox.Source;
                WriteableBitmap bmpFPII = (WriteableBitmap)zernikeFPIIImagePictureBox.Source;
                byte[] dataII = new byte[bmpII.BackBufferStride * bmpII.PixelHeight];
                byte[] dataFPII = new byte[bmpFPII.BackBufferStride * bmpFPII.PixelHeight];
                Int32Rect rect = new Int32Rect(0, 0, bmpII.PixelWidth, bmpII.PixelHeight);

                int stride2 = bmpII.BackBufferStride;
                int stride3 = bmpFPII.BackBufferStride;

                double maxVal = 1;
                for (int i = 0; i < size; i++)
                {
                    maxVal = Math.Max(maxVal, resultsII[i]);
                    maxVal = Math.Max(maxVal, resultsFPII[i]);
                }
                //maxVal = Math.Max(resultsII.Max(), resultsFPII.Max());


                int id = 0;
                for (int w = 0; w < width2; w++)
                {
                    for (int h = 0; h < height2; h++)
                    {
                        int idII = h * stride2 + w * 3;
                        int idFPII = h * stride3 + w * 3;
                        dataII[idII] = dataII[idII + 1] = dataII[idII + 2] = (byte)(resultsII[id] / maxVal * 255.0);
                        dataFPII[idFPII] = dataFPII[idFPII + 1] = dataFPII[idFPII + 2] = (byte)(resultsFPII[id] / maxVal * 255.0);
                        id++;
                    }
                }
                maxValueLabel.Content = ((int)maxVal).ToString() + " Features";
                ((WriteableBitmap)zernikeIIImagePictureBox.Source).WritePixels(rect, dataII, bmpII.BackBufferStride, 0);
                ((WriteableBitmap)zernikeFPIIImagePictureBox.Source).WritePixels(rect, dataFPII, bmpFPII.BackBufferStride, 0);
            }));

            worker.ReportProgress(size + 1);
        }

        private void ComparisonBackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            string statusLabel;
            string logMessage;
            string error = null;

            if (e.Error != null)
            {
                statusLabel = "Status: Zernike extractors comparison completed with errors. Check event log for details";
                logMessage = "Zernike extractors comparison completed with errors:";
                error = e.Error.Message;
            }
            else
            {
                statusLabel = "Status: Zernike extractors comparison completed successful. Check event log for details";
                logMessage = "Zernike extractors comparison completed successful.";
            }

            CompletionEventsArg args = new CompletionEventsArg(statusLabel, logMessage, error, DateTime.Now, false);
            OnComparisonCompletion(args);
        }
        #endregion
    }
}
