using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Globalization;
using DirectShowLib;
using Emgu.CV;

namespace ClassificationToolboxWPF
{
    /// <summary>
    /// Interaction logic for DetectionControl.xaml
    /// </summary>
    public partial class DetectionControl : UserControl
    {
        #region Fields
        readonly DispatcherTimer timer = new DispatcherTimer(DispatcherPriority.ApplicationIdle);

        readonly BackgroundWorker worker = new BackgroundWorker();
        bool isEnabled = true;
        long detectionTime = 0;
        long operationTime = 0;

        // Camera Data
        VideoCapture singleImageVideoCapture; //create a camera captue
        int selectedCamera = -1;
        List<KeyValuePair<int, string>> cameras;
        List<List<System.Drawing.Point>> camerasSupportedResolutions;
        readonly List<System.Drawing.Point> supportedResolutions = new List<System.Drawing.Point>() { new System.Drawing.Point(4096, 2160), new System.Drawing.Point(1920, 1080),
                                                               new System.Drawing.Point(1600, 896), new System.Drawing.Point(1280, 720),
                                                               new System.Drawing.Point(1024, 576), new System.Drawing.Point(800, 600),
                                                               new System.Drawing.Point(640, 480), new System.Drawing.Point(320, 240) };

        // Image Data
        int width;
        int height;
        int stride;
        int bitsPerPixel;
        byte[] data = null;

        // Detection Outputs
        double[] detectionOutputs = null;
        List<System.Drawing.Rectangle> detectionWindows = new List<System.Drawing.Rectangle> ();
        List<int> selectedWindows = new List<int>();
        List<System.Drawing.Rectangle> correctWindows = new List<System.Drawing.Rectangle>();

        // Frames Settings
        System.Drawing.Font windowFont = new System.Drawing.Font("Microsoft Sans Serif", 12.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(238)));
        int penSize = 2;
        int fontSize = 12;

        public bool IsExtractorLoaded { get; set; } = false;
        public bool IsClassifierLoaded { get; set; } = false;
        #endregion

        #region Constructors
        public DetectionControl()
        {
            InitializeComponent();

            worker.WorkerSupportsCancellation = true;
            worker.DoWork += DetectionBackgroundWorker_DoWork;
            worker.RunWorkerCompleted += DetectionBackgroundWorker_RunWorkerCompleted;

            GlobalFunctions.InitializePath(ref detectionClassifierPathTextBox, "detectionClassifierPath");
            detectionExtractorSettings.TrySetFromString(detectionClassifierPathTextBox.Text);

            DetectionExtractorSettings_CheckedChanged(detectionExtractorSettings, new EventArgs());

            IsExtractorLoaded = false;
            IsClassifierLoaded = false;

            UpdateCameraList();
        }
        #endregion

        #region Methods
        public void UpdateCameraList()
        {
            cameras = new List<KeyValuePair<int, string>>();
            camerasSupportedResolutions = new List<List<System.Drawing.Point>>();
            camerasComboBox.Items.Clear();
            cameraResolutionComboBox.Items.Clear();

            DsDevice[] systemCamereas = DsDevice.GetDevicesOfCat(FilterCategory.VideoInputDevice);
            int deviceIndex = 0;

            foreach (DirectShowLib.DsDevice camera in systemCamereas)
            {
                if (camera.Name != "Intel(R) RealSense(TM) Camera SR300 RGB" && camera.Name != "Intel(R) RealSense(TM) Camera SR300 Depth" && camera.Name != "Logi Capture")
                {
                    cameras.Add(new KeyValuePair<int, string>(deviceIndex, camera.Name));
                    camerasComboBox.Items.Add(deviceIndex + ". " + camera.Name);

                    camerasSupportedResolutions.Add(new List<System.Drawing.Point>());

                    IFilterGraph2 ifilter = new FilterGraph() as IFilterGraph2;
                    IBaseFilter sourceFilter = null;
                    IPin pRaw2 = null;
                    try
                    {
                        int bitCount = 0;

                        ifilter.AddSourceFilterForMoniker(camera.Mon, null, camera.Name, out sourceFilter);
                        pRaw2 = DsFindPin.ByCategory(sourceFilter, PinCategory.Capture, 0);

                        VideoInfoHeader v = new VideoInfoHeader();
                        pRaw2.EnumMediaTypes(out IEnumMediaTypes mediaTypeEnum);

                        AMMediaType[] mediaTypes = new AMMediaType[1];
                        IntPtr fetched = IntPtr.Zero;
                        mediaTypeEnum.Next(1, mediaTypes, fetched);

                        while (fetched != null && mediaTypes[0] != null)
                        {
                            Marshal.PtrToStructure(mediaTypes[0].formatPtr, v);
                            if (v.BmiHeader.Size != 0 && v.BmiHeader.BitCount != 0)
                            {
                                if (v.BmiHeader.BitCount > bitCount)
                                    bitCount = v.BmiHeader.BitCount;
                                System.Drawing.Point resolution = new System.Drawing.Point(v.BmiHeader.Width, v.BmiHeader.Height);

                                if (supportedResolutions.Contains(resolution) && !camerasSupportedResolutions[deviceIndex].Contains(resolution))
                                    camerasSupportedResolutions[deviceIndex].Add(resolution);
                            }
                            DsUtils.FreeAMMediaType(mediaTypes[0]);
                            mediaTypeEnum.Next(1, mediaTypes, fetched);
                        }
                    }
                    finally
                    {
                        if (ifilter != null)
                            Marshal.ReleaseComObject(ifilter);
                        if (sourceFilter != null)
                            Marshal.ReleaseComObject(sourceFilter);
                        if (pRaw2 != null)
                            Marshal.ReleaseComObject(pRaw2);
                    }
                    camera.Dispose();
                    camerasSupportedResolutions[deviceIndex] = camerasSupportedResolutions[deviceIndex].OrderByDescending(p => p.X).ThenBy(p => p.Y).ToList();
                    deviceIndex++;
                }
            }

            if (cameras.Count > 0)
            {
                camerasComboBox.IsEnabled = true;
                cameraResolutionComboBox.IsEnabled = true;
                singleImageCameraCheckBox.IsChecked = false;
                singleImageCameraCheckBox.IsEnabled = true;

                camerasComboBox.HorizontalContentAlignment = HorizontalAlignment.Left;
                camerasComboBox.SelectedIndex = 0;
                selectedCamera = 0;
            }
            else
            {
                camerasComboBox.IsEnabled = false;
                cameraResolutionComboBox.IsEnabled = false;
                singleImageCameraCheckBox.IsChecked = false;
                singleImageCameraCheckBox.IsEnabled = false;

                camerasComboBox.Items.Add("{empty}");
                camerasComboBox.HorizontalContentAlignment = HorizontalAlignment.Center;
                camerasComboBox.SelectedIndex = 0;
                cameraResolutionComboBox.Items.Add("{empty}");
                cameraResolutionComboBox.SelectedIndex = 0;
            }

        }

        public void DisableCamera()
        {
            if (singleImageCameraCheckBox.IsChecked != false)
                singleImageCameraCheckBox.IsChecked = false;
        }

        public void DisableControl()
        {
            isEnabled = false;
            foreach (UIElement ctrl in detectionGrid.Children)
            {
                if (ctrl != imageGrid)
                    ctrl.IsEnabled = false;
            }
            foreach (UIElement ctrl in imageGrid.Children)
            {
                if (ctrl != imageGroupBox)
                    ctrl.IsEnabled = false;
            }
            foreach (UIElement ctrl in originalImageGrid.Children)
            {
                if (ctrl != singleImageCameraCheckBox)
                    ctrl.IsEnabled = false;
            }
        }

        public void EnableControl()
        {
            isEnabled = true;
            foreach (UIElement ctrl in detectionGrid.Children)
                    ctrl.IsEnabled = true;
            foreach (UIElement ctrl in imageGrid.Children)
                    ctrl.IsEnabled = true;
            foreach (UIElement ctrl in originalImageGrid.Children)
                    ctrl.IsEnabled = true;
        }

        public void SaveResult()
        {
            if (detectionOutputs == null)
            {
                MessageBox.Show("No data to save. Generate data using Detect Object button.");
                return;
            }

            System.Windows.Forms.SaveFileDialog saveFileDialog = new System.Windows.Forms.SaveFileDialog()
            {
                Filter = "Bitmap Image |*.bmp|JPEG Image |*.jpg|Png Image |*.png",
                FileName = ""
            };
            if (Properties.Settings.Default.detectionResultFolder != "" && Directory.Exists(Properties.Settings.Default.detectionResultFolder))
                saveFileDialog.InitialDirectory = Properties.Settings.Default.detectionResultFolder;
            else
                saveFileDialog.InitialDirectory = "";

            if (saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                Properties.Settings.Default.detectionResultFolder = System.IO.Path.GetDirectoryName(saveFileDialog.FileName);
                Properties.Settings.Default.Save();

                if (detectedObjectPictureBox.Source != null)
                {
                    if (saveFileDialog.FilterIndex == 1)
                    {
                        using (var fileStream = new FileStream(saveFileDialog.FileName, FileMode.Create))
                        {
                            BitmapEncoder encoder = new BmpBitmapEncoder();
                            encoder.Frames.Add(BitmapFrame.Create((BitmapSource)detectedObjectPictureBox.Source));
                            encoder.Save(fileStream);
                        }
                    }
                    else if (saveFileDialog.FilterIndex == 2)
                    {
                        using (var fileStream = new FileStream(saveFileDialog.FileName, FileMode.Create))
                        {
                            BitmapEncoder encoder = new JpegBitmapEncoder();
                            encoder.Frames.Add(BitmapFrame.Create((BitmapSource)detectedObjectPictureBox.Source));
                            encoder.Save(fileStream);
                        }
                    }
                    else
                    {
                        using (var fileStream = new FileStream(saveFileDialog.FileName, FileMode.Create))
                        {
                            BitmapEncoder encoder = new PngBitmapEncoder();
                            encoder.Frames.Add(BitmapFrame.Create((BitmapSource)detectedObjectPictureBox.Source));
                            encoder.Save(fileStream);
                        }
                    }
                }
            }
            saveFileDialog.Dispose();
        }

        public void DisposeCamera()
        {

            if (singleImageVideoCapture != null)
            {
                singleImageVideoCapture.Stop();
                singleImageVideoCapture.Dispose();
                singleImageVideoCapture = null;
            }
        }

        public bool GetImageFromCamera()
        {
            try
            {
                if (singleImageVideoCapture != null && singleImageVideoCapture.IsOpened)
                {
                    Mat ImageFrame = singleImageVideoCapture.QueryFrame(); //draw the image obtained from camera

                    System.Drawing.Bitmap bmp = null;
                    if (ImageFrame.Bitmap.PixelFormat != System.Drawing.Imaging.PixelFormat.Format24bppRgb)
                        bmp = ImageFrame.Bitmap.Clone(new System.Drawing.Rectangle(0, 0, ImageFrame.Bitmap.Width, ImageFrame.Bitmap.Height), System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                    else
                        bmp = ImageFrame.Bitmap;

                    System.Drawing.Imaging.BitmapData bitmapData = bmp.LockBits(new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height), System.Drawing.Imaging.ImageLockMode.ReadOnly, bmp.PixelFormat);

                    BitmapSource bitmapSource = BitmapSource.Create(bmp.Width, bmp.Height, bmp.HorizontalResolution, bmp.VerticalResolution, PixelFormats.Bgr24, null, bitmapData.Scan0, bitmapData.Stride * bitmapData.Height, bitmapData.Stride);
                    originalImagePictureBox.Source = bitmapSource;

                    bmp.UnlockBits(bitmapData);

                    penSize = (int)Math.Max(2, 0.002 * bmp.Width);
                    fontSize = (int)Math.Max(12, 0.015 * bmp.Width);
                    windowFont = new System.Drawing.Font("Microsoft Sans Serif", fontSize, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(238)));
                }
                else
                {
                    MessageBox.Show("Problem with camera detected.");
                    timer.Stop();
                    timer.Tick -= SingleImageGetFromCameraAndDetect;
                    timer.Tick -= SingleImageGetFromCamera;
                    singleImageCameraCheckBox.IsChecked = false;

                    return false;
                }
            }
            catch
            {
                MessageBox.Show("Problem with camera detected.");
                timer.Stop();
                timer.Tick -= SingleImageGetFromCameraAndDetect;
                timer.Tick -= SingleImageGetFromCamera;
                singleImageCameraCheckBox.IsChecked = false;

                return false;
            }

            return true;
        }
        #endregion

        #region Triggers
        public event EventHandler<StartedEventsArg> DetectionStarted;
        protected virtual void OnDetectionStarted(StartedEventsArg e)
        {
            DetectionStarted?.Invoke(this, e);
        }

        public event EventHandler<CompletionEventsArg> DetectionCompletion;
        protected virtual void OnDetectionCompletion(CompletionEventsArg e)
        {
            DetectionCompletion?.Invoke(this, e);
        }

        public event EventHandler<EventArgs> ClassifierLoaded;
        protected virtual void OnClassifierLoaded(EventArgs e)
        {
            ClassifierLoaded?.Invoke(this, e);
        }

        public event EventHandler<EventArgs> ExtractorLoaded;
        protected virtual void OnExtractorLoaded(EventArgs e)
        {
            ExtractorLoaded?.Invoke(this, e);
        }
        #endregion

        #region Event
        private void SelectClassifierButton_Click(object sender, RoutedEventArgs e)
        {
            if (GlobalFunctions.SelectClassifier(ref detectionClassifierPathTextBox, "detectionClassifierPath") == System.Windows.Forms.DialogResult.OK)
            {
                detectionExtractorSettings.TrySetFromString(detectionClassifierPathTextBox.Text);
                IsClassifierLoaded = false;
            }
        }

        private void SelectImageButton_Click(object sender, RoutedEventArgs e)
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            if (GlobalFunctions.SelectImage(out string fileName, "detectionImagePath") == System.Windows.Forms.DialogResult.OK)
            {
                PixelFormat pf = PixelFormats.Bgr24;
                BitmapSource bmp = new BitmapImage(new Uri(fileName, UriKind.Relative));
                if (bmp.Format != PixelFormats.Bgr24)
                    bmp = new FormatConvertedBitmap(bmp, pf, null, 0);

                height = bmp.PixelHeight;
                width = bmp.PixelWidth;
                int widthbyte = (width * pf.BitsPerPixel + 7) / 8;
                stride = ((widthbyte + 3) / 4) * 4;
                bitsPerPixel = pf.BitsPerPixel;
                data = new byte[stride * height];
                bmp.CopyPixels(data, stride, 0);

                originalImagePictureBox.Source = BitmapSource.Create(width, height, 96, 96, pf, null, data, stride);

                imageSizeLabel.Content = bmp.PixelWidth + " x " + bmp.PixelHeight;

                penSize = (int)Math.Max(2, 0.002 * width);
                fontSize = (int)Math.Max(12, 0.015 * width);
                windowFont = new System.Drawing.Font("Microsoft Sans Serif", fontSize, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(238)));

                detectionOutputs = null;
                detectionWindows.Clear();
                selectedWindows.Clear();

                singleImageCameraCheckBox.IsChecked = false;

                minHeightNumericUpDown.Maximum = bmp.PixelHeight;
                minWidthNumericUpDown.Maximum = bmp.PixelWidth;

                if (detectionExtractorSettings.ExtractorName == "HaarExtractor")
                {
                    minHeightNumericUpDown.Value = Math.Round(height * (10.0M / 100.0M));
                    minWidthNumericUpDown.Value = Math.Round(width * (10.0M / 100.0M));
                }
                else if (detectionExtractorSettings.ExtractorName == "HOGExtractor")
                {
                    minHeightNumericUpDown.Value = Math.Round(height * (10.0M / 100.0M));
                    minWidthNumericUpDown.Value = Math.Round(width * (10.0M / 100.0M));
                }
                else if (detectionExtractorSettings.ExtractorName == "PFMMExtractor")
                {
                    decimal minimum = Math.Min(width, height);
                    minWidthNumericUpDown.Value = Math.Round(minimum * (10.0M / 100.0M));
                    minHeightNumericUpDown.Value = Math.Round(minimum * (10.0M / 100.0M));
                }
                else if (detectionExtractorSettings.ExtractorName.StartsWith("Zernike"))
                {
                    decimal minimum = Math.Min(width, height);
                    minWidthNumericUpDown.Value = Math.Round(minimum * (10.0M / 100.0M));
                    minHeightNumericUpDown.Value = Math.Round(minimum * (10.0M / 100.0M));

                    minWidthNumericUpDown.Value += minWidthNumericUpDown.Value % 2;
                    minHeightNumericUpDown.Value += minHeightNumericUpDown.Value % 2;
                }
                correctWindows.Clear();
                if (File.Exists(fileName + ".txt"))
                {
                    using (StreamReader sr = new StreamReader(fileName + ".txt"))
                    {
                        string line;
                        while ((line = sr.ReadLine()) != null)
                        {
                            line = line.Trim().Replace("   ", " ");
                            string[] values = line.Split(' ');
                            System.Drawing.Rectangle window = new System.Drawing.Rectangle((int)double.Parse(values[0]), (int)double.Parse(values[1]), (int)double.Parse(values[2]), (int)double.Parse(values[2]));
                            correctWindows.Add(window);
                        }
                    }
                }
            }
        }

        private void DetectionExtractorSettings_CheckedChanged(object sender, EventArgs e)
        {
            if (detectionExtractorSettings.ExtractorName == "HaarExtractor")
            {
                minHeightNumericUpDown.IsEnabled = true;
            }
            else if (detectionExtractorSettings.ExtractorName == "HOGExtractor")
            {
                minHeightNumericUpDown.IsEnabled = true;
            }
            else if (detectionExtractorSettings.ExtractorName == "PFMMExtractor" || detectionExtractorSettings.ExtractorName.StartsWith("Zernike"))
            {
                minHeightNumericUpDown.Value = minWidthNumericUpDown.Value;
                minHeightNumericUpDown.IsEnabled = false;

                decimal maxWindow = Math.Round((decimal)Math.Pow((double)scalesRatioNumericUpDown.Value, (double)scalesNumericUpDown.Value - 1) * minWidthNumericUpDown.Value);
                detectionExtractorSettings.SetMinimum(maxWindow, "d");
                detectionExtractorSettings.SetMinimum(maxWindow, "w");
            }

            IsExtractorLoaded = false;
        }

        private void DetectionExtractorSettings_ValueChanged(object sender, EventArgs e)
        {
            IsExtractorLoaded = false;
        }

        private void MinWidthNumericUpDown_ValueChanged(object sender, ValueChangedEventArg e)
        {
            if (detectionExtractorSettings.ExtractorName == "PFMMExtractor" || detectionExtractorSettings.ExtractorName.StartsWith("Zernike"))
            {
                minHeightNumericUpDown.Value = minWidthNumericUpDown.Value;

                decimal maxWindow = Math.Round((decimal)Math.Pow((double)scalesRatioNumericUpDown.Value, (double)scalesNumericUpDown.Value - 1) * minWidthNumericUpDown.Value);
                detectionExtractorSettings.SetMinimum(maxWindow, "d");
                detectionExtractorSettings.SetMinimum(maxWindow, "w");

                IsExtractorLoaded = false;
            }
        }

        private void ScalesRatioNumericUpDown_ValueChanged(object sender, ValueChangedEventArg e)
        {
            if (detectionExtractorSettings.ExtractorName == "PFMMExtractor" || detectionExtractorSettings.ExtractorName.StartsWith("Zernike"))
            {
                decimal maxWindow = Math.Ceiling((decimal)Math.Pow((double)scalesRatioNumericUpDown.Value, (double)scalesNumericUpDown.Value - 1) * minWidthNumericUpDown.Value);
                detectionExtractorSettings.SetMinimum(maxWindow, "d");
                detectionExtractorSettings.SetMinimum(maxWindow, "w");

                IsExtractorLoaded = false;
            }
        }

        private void DetectedObjectPictureBox_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                if (detectedObjectPictureBox.Source != null)
                {
                    ImageSource bmp = detectedObjectPictureBox.Source.Clone();
                    ImagePreviewer imagePreviewer = new ImagePreviewer(bmp);
                    imagePreviewer.ShowDialog();
                }
                e.Handled = true;
            }
        }

        private void SingleImageCameraCheckBox_CheckStateChanged(object sender, RoutedEventArgs e)
        {
            singleImageCameraCheckBox.IsEnabled = false;

            if (singleImageCameraCheckBox.IsChecked == true)
            {
                if (cameras.Count > 0)
                {
                    timer.Tick += SingleImageGetFromCameraAndDetect;

                    camerasComboBox.IsEnabled = false;
                    cameraResolutionComboBox.IsEnabled = false;                  

                    if (singleImageVideoCapture != null)
                    {
                        DisposeCamera();
                    }

                    System.Drawing.Point resolution = camerasSupportedResolutions[selectedCamera][cameraResolutionComboBox.SelectedIndex];
                    singleImageVideoCapture = new VideoCapture(selectedCamera);
                    singleImageVideoCapture.SetCaptureProperty(Emgu.CV.CvEnum.CapProp.FrameWidth, resolution.X);
                    singleImageVideoCapture.SetCaptureProperty(Emgu.CV.CvEnum.CapProp.FrameHeight, resolution.Y);

                    imageSizeLabel.Content = resolution.X + " x " + resolution.Y;
                    minHeightNumericUpDown.Maximum = resolution.Y;
                    minWidthNumericUpDown.Maximum = resolution.X;

                    timer.Start();
                }
                else
                {
                    singleImageCameraCheckBox.IsChecked = false;
                    singleImageCameraCheckBox.IsEnabled = false;
                }
            }
            else if (singleImageCameraCheckBox.IsChecked == null)
            {
                if (!worker.IsBusy && !isEnabled)
                    EnableControl();

                timer.Stop();
                timer.Tick -= SingleImageGetFromCameraAndDetect;

                if (cameras.Count > 0)
                {
                    timer.Tick += SingleImageGetFromCamera;

                    if(singleImageVideoCapture == null)
                    {
                        System.Drawing.Point resolution = camerasSupportedResolutions[selectedCamera][cameraResolutionComboBox.SelectedIndex];
                        singleImageVideoCapture = new VideoCapture(selectedCamera);
                        singleImageVideoCapture.SetCaptureProperty(Emgu.CV.CvEnum.CapProp.FrameWidth, resolution.X);
                        singleImageVideoCapture.SetCaptureProperty(Emgu.CV.CvEnum.CapProp.FrameHeight, resolution.Y);

                        imageSizeLabel.Content = resolution.X + " x " + resolution.Y;
                        minHeightNumericUpDown.Maximum = resolution.Y;
                        minWidthNumericUpDown.Maximum = resolution.X;
                    }

                    nextThrButton.IsEnabled = true;
                    prevThrButton.IsEnabled = true;
                    thresholdDetNumericUpDown.IsEnabled = true;

                    timer.Start();
                }
                else
                {
                    singleImageCameraCheckBox.IsChecked = false;
                    singleImageCameraCheckBox.IsEnabled = false;
                }
            }
            else if (singleImageCameraCheckBox.IsChecked == false)
            {
                if (!worker.IsBusy && !isEnabled)
                    EnableControl();

                timer.Stop();
                timer.Tick -= SingleImageGetFromCamera;

                if (cameras.Count > 0)
                {
                    camerasComboBox.IsEnabled = true;
                    cameraResolutionComboBox.IsEnabled = true;
                }

                DisposeCamera();
                nextThrButton.IsEnabled = true;
                prevThrButton.IsEnabled = true;
                thresholdDetNumericUpDown.IsEnabled = true;
            }

            singleImageCameraCheckBox.IsEnabled = true;
        }

        private void SingleImageGetFromCamera(object sender, EventArgs e)
        {
            timer.Stop();

            if (GetImageFromCamera())
                timer.Start();
        }

        private void SingleImageGetFromCameraAndDetect(object sender, EventArgs e)
        {
            timer.Stop();

            if (GetImageFromCamera())
                timer.Start();

            if (!worker.IsBusy)
                SingleImageDetectionButton_Click(sender, new RoutedEventArgs());
        }

        private void CamerasComboBox_SelectionChanged(object sender, RoutedEventArgs e)
        {
            if (camerasComboBox.SelectedItem != null && camerasComboBox.SelectedItem.ToString() != "{empty}")
            {
                selectedCamera = camerasComboBox.SelectedIndex;
                cameraResolutionComboBox.Items.Clear();

                foreach (System.Drawing.Point resolution in camerasSupportedResolutions[selectedCamera])
                    cameraResolutionComboBox.Items.Add(resolution.X + " x " + resolution.Y);
                if (cameraResolutionComboBox.Items.Count != 0)
                {
                    cameraResolutionComboBox.SelectedIndex = 0;
                    singleImageCameraCheckBox.IsEnabled = true;
                    cameraResolutionComboBox.IsEnabled = true;
                }
                else
                {
                    singleImageCameraCheckBox.IsChecked = false;
                    singleImageCameraCheckBox.IsEnabled = false;
                    cameraResolutionComboBox.IsEnabled = false;
                    cameraResolutionComboBox.Items.Add("{empty}");
                    cameraResolutionComboBox.SelectedIndex = 0;
                }
            }
        }

        private void RefreshCameraList_Click(object sender, RoutedEventArgs e)
        {
            UpdateCameraList();
        }

        private void GroupWindowsButton_Click(object sender, RoutedEventArgs e)
        {
            if (data != null && detectionOutputs.Length > 0)
            {
                PixelFormat pf = PixelFormats.Bgr24;
                WriteableBitmap bmp = new WriteableBitmap(BitmapSource.Create(width, height, 96, 96, pf, null, data, stride));

                double jaccardIntersection =(double)minJaccardNumericUpDown.Value;
                double jaccardGrouping = (double)minJaccardGroupingNumericUpDown.Value;

                GlobalFunctions.GroupingMode mode;
                if (sumGroupingRadioButton.IsChecked == true)
                    mode = GlobalFunctions.GroupingMode.SUM;
                else if (averageGroupingRadioButton.IsChecked == true)
                    mode = GlobalFunctions.GroupingMode.AVERAGE_FAST;
                else
                    mode = GlobalFunctions.GroupingMode.AVERAGE_SLOW;
                GlobalFunctions.GroupWindow(ref bmp, ref detectionWindows, ref selectedWindows, ref correctWindows, jaccardGrouping, jaccardIntersection, mode,
                        penSize, windowFont, out _, out _, out _, out _);

                detectedObjectPictureBox.Source = bmp;
            }
        }

        private void PrevThrButton_Click(object sender, RoutedEventArgs e)
        {
            thresholdDetNumericUpDown.Value -= 0.5M;
        }

        private void NextThrButton_Click(object sender, RoutedEventArgs e)
        {
            thresholdDetNumericUpDown.Value += 0.5M;
        }

        private void ThresholdDetNumericUpDown_ValueChanged(object sender, ValueChangedEventArg e)
        {
            if (data != null && detectionOutputs?.Length > 0)
            {
                PixelFormat pf = PixelFormats.Bgr24;
                WriteableBitmap bmp = new WriteableBitmap(BitmapSource.Create(width, height, 96, 96, pf, null, data, stride));

                GlobalFunctions.SelectAndDrawWindow(ref bmp, ref detectionOutputs, ref detectionWindows, ref selectedWindows, ref correctWindows,
                    (double)thresholdDetNumericUpDown.Value, (double)minJaccardNumericUpDown.Value, penSize);

                detectedObjectPictureBox.Source = bmp;
            }
        }

        private void SaveDetectionResultButton_Click(object sender, RoutedEventArgs e)
        {
            SaveResult();
        }

        private void SaveFPButton_Click(object sender, RoutedEventArgs e)
        {
            if (detectionOutputs == null)
            {
                MessageBox.Show("No data to save. Generate data using Detect Object button.");
                return;
            }
            if (correctWindows == null || correctWindows.Count == 0)
            {
                if (MessageBox.Show("No information about object location avalible. All positive windows will be treated as FP. \r\n\r\nYou can mark object on photo in Cropp Sample module.\r\n\r\nAre you sure you want to continue?", "Warrning!", MessageBoxButton.OKCancel) == MessageBoxResult.Cancel)
                return;
            }

            if (GlobalFunctions.SelectFolder("detectionFPfolder") == System.Windows.Forms.DialogResult.OK)
            {
                for (int i = 0; i < detectionOutputs.Length; i++)
                {
                    if (detectionOutputs[i] >= 0.0)
                    {
                        bool correct = false;
                        foreach (System.Drawing.Rectangle correctWindow in correctWindows)
                        {
                            System.Drawing.Rectangle intersection = System.Drawing.Rectangle.Intersect(correctWindow, detectionWindows[i]);
                            int intersectionField = (intersection.Width * intersection.Height);
                            int fieldsSum = (correctWindow.Width * correctWindow.Height + detectionWindows[i].Width * detectionWindows[i].Height - intersectionField);
                            double J = intersectionField / (double)fieldsSum;

                            if (J >= (double)minJaccardNumericUpDown.Value)
                            {
                                correct = true;
                                break;
                            }
                        }

                        if (!correct)
                        {
                            BitmapSource cropedWindow = new CroppedBitmap((BitmapSource)originalImagePictureBox.Source, new Int32Rect(detectionWindows[i].X, detectionWindows[i].Y, detectionWindows[i].Width, detectionWindows[i].Height));

                            string date = DateTime.Now.Ticks.ToString();
                            string fileName = Properties.Settings.Default.detectionFPfolder + "\\window_" + Path.GetFileNameWithoutExtension(Path.GetRandomFileName()) + date.Substring(date.Length - 7);
                            using (var fileStream = new FileStream(fileName + ".bmp", FileMode.Create))
                            {
                                BitmapEncoder encoder = new BmpBitmapEncoder();
                                encoder.Frames.Add(BitmapFrame.Create(cropedWindow));
                                encoder.Save(fileStream);
                            }
                        }
                    }
                }
                MessageBox.Show("Work completed.");
            }
        }

        unsafe private void SingleImageDetectionButton_Click(object sender, RoutedEventArgs e)
        {
            if (detectionClassifierPathTextBox.Text == "" || !File.Exists(detectionClassifierPathTextBox.Text))
            {
                MessageBox.Show("Classifier path is empty or file doesn't exist.");
                singleImageCameraCheckBox.IsChecked = false;
                return;
            }
            if (originalImagePictureBox.Source == null)
            {
                MessageBox.Show("Image is empty.");
                singleImageCameraCheckBox.IsChecked = false;
                return;
            }
            if(!IsClassifierLoaded)
            {
                int status = NativeMethods.LoadClassifier(detectionClassifierPathTextBox.Text);
                if (status != 0)
                {
                    MessageBox.Show(GlobalFunctions.GetErrorDescription(status));
                    IsClassifierLoaded = false;

                    singleImageCameraCheckBox.IsChecked = false;
                    return;
                }
                else
                {
                    IsClassifierLoaded = true;
                }
                OnClassifierLoaded(new EventArgs());
            }
            if (!IsExtractorLoaded)
            {
                int[] parameters = detectionExtractorSettings.ParametersArray;

                int status;
                fixed (int* parPointer = parameters)
                {
                    status = NativeMethods.LoadExtractor(detectionExtractorSettings.ExtractorName, parPointer);
                }
                if (status != 0)
                {
                    MessageBox.Show(GlobalFunctions.GetErrorDescription(status));
                    IsExtractorLoaded = false;

                    singleImageCameraCheckBox.IsChecked = false;
                    return;
                }
                else
                {
                    IsExtractorLoaded = true;
                }
                OnExtractorLoaded(new EventArgs());
            }
            if (worker.IsBusy)
            {
                MessageBox.Show("Application is busy.");
                return;
            }

            if (singleImageCameraCheckBox.IsChecked != false && originalImagePictureBox.Source != null)
            {
                correctWindows.Clear();

                PixelFormat pf = PixelFormats.Bgr24;
                BitmapSource bmp = (BitmapSource)(originalImagePictureBox.Source);
                if (bmp.Format != PixelFormats.Bgr24)
                    bmp = new FormatConvertedBitmap(bmp, pf, null, 0);

                height = bmp.PixelHeight;
                width = bmp.PixelWidth;
                int widthbyte = (width * pf.BitsPerPixel + 7) / 8;
                stride = ((widthbyte + 3) / 4) * 4;
                bitsPerPixel = pf.BitsPerPixel;
                data = new byte[stride * height];
                bmp.CopyPixels(data, stride, 0);
            }

            if (singleImageCameraCheckBox.IsChecked != true)
            {
                StartedEventsArg args = new StartedEventsArg("Status: Working", "Object detection started.", DateTime.Now, 0, true);
                OnDetectionStarted(args);
            }

            if (isEnabled)
                DisableControl();
                
            worker.RunWorkerAsync();
        }

        unsafe private void DetectionBackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            Stopwatch stopwatchTotal = new Stopwatch();
            stopwatchTotal.Start();

            detectionOutputs = null;
            detectionWindows.Clear();
            selectedWindows.Clear();

            string classifierPath = Dispatcher.Invoke(new Func<string>(() => { return detectionClassifierPathTextBox.Text; }));
            string extractorType = Dispatcher.Invoke(new Func<string>(() => { return detectionExtractorSettings.ExtractorName; }));
        
            NativeMethods.DetectionParameters detectionParameters = new NativeMethods.DetectionParameters()
            {
                windowMinimalHeight = Dispatcher.Invoke(new Func<int>(() => { return (int)minHeightNumericUpDown.Value; })),
                windowMinimalWidth = Dispatcher.Invoke(new Func<int>(() => { return (int)minWidthNumericUpDown.Value; })),
                scales = Dispatcher.Invoke(new Func<int>(() => { return (int)scalesNumericUpDown.Value; })),
                windowJumpingRatio = Dispatcher.Invoke(new Func<double>(() => { return (double)jumpingRatioNumericUpDown.Value; })),
                windowScalingRatio = Dispatcher.Invoke(new Func<double>(() => { return (double)scalesRatioNumericUpDown.Value; }))
            };
            System.Drawing.Point[] sizes = new System.Drawing.Point[detectionParameters.scales];
            GlobalFunctions.InitializeDetectionWindows(extractorType, width, height, ref detectionParameters, ref detectionWindows, ref detectionOutputs, out int wMax, ref sizes);
            Dispatcher.Invoke(new Action(() => { windowsLabel.Content = detectionWindows.Count.ToString(); }));

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            GlobalFunctions.DetectObject(ref data, width, height, stride, bitsPerPixel, ref detectionParameters, ref detectionOutputs, ref detectionWindows, ref sizes);

            stopwatch.Stop();
            detectionTime = stopwatch.ElapsedMilliseconds;

            PixelFormat pf = PixelFormats.Bgr24;
            WriteableBitmap bmp = new WriteableBitmap(BitmapSource.Create(width, height, 96, 96, pf, null, data, stride));

            double threshold = Dispatcher.Invoke(new Func<double>(() => { return (double)thresholdDetNumericUpDown.Value; }));
            double jaccardIntersection = Dispatcher.Invoke(new Func<double>(() => { return (double)minJaccardNumericUpDown.Value; }));
            double jaccardGrouping = Dispatcher.Invoke(new Func<double>(() => { return (double)minJaccardGroupingNumericUpDown.Value; }));

            GlobalFunctions.GroupingMode mode;
            if (Dispatcher.Invoke(new Func<bool>(() => { return sumGroupingRadioButton.IsChecked == true; })))
                mode = GlobalFunctions.GroupingMode.SUM;
            else if (Dispatcher.Invoke(new Func<bool>(() => { return averageGroupingRadioButton.IsChecked == true; })))
                mode = GlobalFunctions.GroupingMode.AVERAGE_FAST;
            else
                mode = GlobalFunctions.GroupingMode.AVERAGE_SLOW;

            if (Dispatcher.Invoke(new Func<bool>(() => { return singleImageCameraCheckBox.IsChecked == true; })))
            {
                GlobalFunctions.SelectWindow(ref detectionOutputs, ref selectedWindows, threshold);
                GlobalFunctions.GroupWindow(ref bmp, ref detectionWindows, ref selectedWindows, ref correctWindows, jaccardGrouping, jaccardIntersection, mode,
                    penSize, windowFont, out double TP, out double FP, out double FN, out double TN);
            }
            else
            {
                GlobalFunctions.SelectAndDrawWindow(ref bmp, ref detectionOutputs, ref detectionWindows, ref selectedWindows, ref correctWindows,
                    threshold, jaccardIntersection, penSize);
            }
            bmp.Freeze();
            Dispatcher.Invoke(new Action(() => { detectedObjectPictureBox.Source = bmp; }));

            stopwatchTotal.Stop();
            operationTime = stopwatchTotal.ElapsedMilliseconds;

            Dispatcher.Invoke(new Action(() => { timeLabel.Content = operationTime.ToString() + "ms"; }));
            Dispatcher.Invoke(new Action(() => { fpsLabel.Content = Math.Round(1 / (operationTime / 1000.0)); }));
        }

        private void DetectionBackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (singleImageCameraCheckBox.IsChecked != true)
            {
                string statusLabel;
                string logMessage;
                string error = null;

                if (e.Error != null)
                {
                    statusLabel = "Status: Detection completed with errors. Check event log for details";
                    logMessage = "Detection completed with errors:";
                    error = e.Error.Message;
                }
                else
                {
                    statusLabel = "Status: Detection completed successful. Check event log for details";
                    logMessage = "Detection completed successful. Elapsed time: " + operationTime + "ms. Detection time: " + detectionTime + "ms.";
                }

                CompletionEventsArg args = new CompletionEventsArg(statusLabel, logMessage, error, DateTime.Now, false);
                OnDetectionCompletion(args);
            }
        }
        #endregion
    }
}
