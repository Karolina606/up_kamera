using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Management;
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
using Windows.Media.Capture;
using Windows.Storage;
using AForge.Video;
using AForge.Video.DirectShow;
using AForge.Imaging;
using AForge.Imaging.Filters;
using AForge;
using AForge.Controls;
using Accord.Video.FFMPEG;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading;
using Accord.Vision.Motion;
using System.Media;

namespace up_kamera
{
    /// <summary>
    /// Logika interakcji dla klasy MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private VideoCaptureDevice videoCaptureDevice;
        private FilterInfoCollection filterInfoCollection;
        private VideoSourcePlayer videoSourcePlayer;
        private VideoFileWriter videoFileWriter;
        private bool isRecording = false;
        private bool isLookingForChanges = false;
        private int Contrast = 0;
        Bitmap frameToDetect;

        // Recording params
        int width = 1280;
        int height = 720;
        int fps = 9;

        // Motion detector
        MotionDetector detector = new MotionDetector(
            new SimpleBackgroundModelingDetector(),
            new MotionAreaHighlighting());

        public static List<string> GetAllConnectedCameras()
        {
            var cameraNames = new List<string>();
            using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE (PNPClass = 'Image' OR PNPClass = 'Camera')"))
            {
                foreach (var device in searcher.Get())
                {
                    cameraNames.Add(device["Caption"].ToString());
                }
            }

            return cameraNames;
        }

        private void onLoaded(object sender, RoutedEventArgs e)
        {
            filterInfoCollection = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            foreach (FilterInfo filterInfo in filterInfoCollection)
            {
                cbCamera.Items.Add(filterInfo.Name);

            }
            cbCamera.SelectedIndex = 0;
            videoCaptureDevice = new VideoCaptureDevice();
        }

        private void btnChooseCamClick(object sender, RoutedEventArgs e)
        {
            videoCaptureDevice = new VideoCaptureDevice(filterInfoCollection[cbCamera.SelectedIndex].MonikerString);
            videoCaptureDevice.NewFrame += videoCaptureDevice_NewFrame;
            videoCaptureDevice.Start();

            Console.WriteLine("Wybrałeś kamerę");

            // Parametry
            int zoom, exposure, pan, tilt, roll, iris, focus;
            CameraControlFlags flag;
            videoCaptureDevice.GetCameraProperty(CameraControlProperty.Zoom, out zoom, out flag);
            videoCaptureDevice.GetCameraProperty(CameraControlProperty.Exposure, out exposure, out flag);
            videoCaptureDevice.GetCameraProperty(CameraControlProperty.Pan, out pan, out flag);
            videoCaptureDevice.GetCameraProperty(CameraControlProperty.Tilt, out tilt, out flag);
            videoCaptureDevice.GetCameraProperty(CameraControlProperty.Iris, out iris, out flag);
            videoCaptureDevice.GetCameraProperty(CameraControlProperty.Roll, out roll, out flag);
            videoCaptureDevice.GetCameraProperty(CameraControlProperty.Focus, out focus, out flag);
            tbZoom.Text = zoom.ToString();
            tbExposure.Text = exposure.ToString();
            tbPan.Text = pan.ToString();
            tbTilt.Text = tilt.ToString();
            tbRoll.Text = roll.ToString();
            //tbIris.Text = iris.ToString();
            tbFocus.Text = focus.ToString();
            tbContrast.Text = "0";


            // Sprawdzenie zakresów parametrów
            int zoom2, exposure2, pan2, tilt2, roll2, iris2, focus2, non;
            int zoom3, exposure3, pan3, tilt3, roll3, iris3, focus3;
            String param = "Parameters: \n";
            videoCaptureDevice.GetCameraPropertyRange(CameraControlProperty.Zoom, out zoom, out zoom2, out zoom3, out non ,out flag);
            videoCaptureDevice.GetCameraPropertyRange(CameraControlProperty.Exposure, out exposure, out exposure2, out exposure3, out non, out flag);
            videoCaptureDevice.GetCameraPropertyRange(CameraControlProperty.Pan, out pan, out pan2, out pan3, out non, out flag);
            videoCaptureDevice.GetCameraPropertyRange(CameraControlProperty.Tilt, out tilt, out tilt2, out tilt3, out non, out flag);
            videoCaptureDevice.GetCameraPropertyRange(CameraControlProperty.Iris, out iris, out iris2, out iris3, out non, out flag);
            videoCaptureDevice.GetCameraPropertyRange(CameraControlProperty.Roll, out roll, out roll2, out roll3, out non, out flag);
            videoCaptureDevice.GetCameraPropertyRange(CameraControlProperty.Focus, out focus, out focus2, out focus3, out non, out flag);

            param += $"Zoom min: {zoom} max: {zoom2} step: {zoom3} \n";
            param += $"Exposure min: {exposure} max: {exposure2} step: {exposure3} \n";
            param += $"Pan min: {pan} max: {pan2} step: {pan3} \n";
            param += $"Tilt min: {tilt} max: {tilt2} step: {tilt3} \n";
            param += $"Iris min: {iris} max: {iris2} step: {iris3} \n";
            param += $"Roll min: {roll} max: {roll2} step: {roll3} \n";
            param += $"Focus min: {focus} max: {focus2} step: {focus3} \n";


            // Recording params
            tbRecordingWidth.Text = width.ToString();
            tbRecordingHeight.Text = height.ToString();
            tbRecordingFps.Text = fps.ToString();

            Console.WriteLine(param);
        }

        private void videoCaptureDevice_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            // Kontrast
            ContrastCorrection filter = new ContrastCorrection(Contrast);
            Bitmap original = (Bitmap)eventArgs.Frame.Clone();
            Bitmap toFrame = new Bitmap(original, new System.Drawing.Size(original.Width/2, original.Height/2));
            pictBox.Image = filter.Apply(toFrame);

            if (isRecording)
            {
                Bitmap second = filter.Apply((Bitmap)eventArgs.Frame.Clone());
                videoFileWriter.WriteVideoFrame(second);
            }

            try
            {
                if (isLookingForChanges)
                {
                    frameToDetect = (Bitmap) toFrame.Clone();
                    detectMotion();
                }
            }catch (System.Exception e)
            {
                
                lblDetectInfo.Content = "Wykryto ruch";
                //Thread.Sleep(2000);
            }
        }

        private void detectMotion()
        {
            try
            {
                float motionFractal = detector.ProcessFrame(frameToDetect);
                if (motionFractal > 0.2)
                {

                    Console.WriteLine("Wykryto ruch");
                    SystemSounds.Beep.Play();
                    // lblInfo.Content = "Wykryto ruch";
                    // Thread.Sleep(2000);
                }
            }
            catch (System.Exception e)
            {
                
                lblDetectInfo.Content = "Wykryto ruch";
                //Thread.Sleep(2000);
            }
        }

        private void btnCapturePhotoClick(object sender, RoutedEventArgs e)
        {
            if (pictBox.Image != null)
            {
                //Save First
                Bitmap varBmp = new Bitmap(pictBox.Image);
                Bitmap newBitmap = new Bitmap(varBmp);
                varBmp.Save(@"zdjecie.jpg", ImageFormat.Jpeg);
                //Now Dispose to free the memory
                varBmp.Dispose();
                varBmp = null;

                // Wyświetlenie informacji 
                lblInfo.Content = "Zdjęcie zostało zrobione!";
            }
            else
            {
                MessageBox.Show("null exception");
                // Wyświetlenie informacji 
                lblInfo.Content = "Zdjęcie nie zostało zrobione!";
            }
        }

        private void windowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (videoCaptureDevice.IsRunning)
            {
                videoCaptureDevice.Stop();
            }
        }

        private void btnSetupClick(object sender, RoutedEventArgs e)
        {
            videoCaptureDevice.SetCameraProperty(CameraControlProperty.Zoom, Int32.Parse(tbZoom.Text), CameraControlFlags.Manual);
            videoCaptureDevice.SetCameraProperty(CameraControlProperty.Exposure, Int32.Parse(tbExposure.Text), CameraControlFlags.Manual);
            videoCaptureDevice.SetCameraProperty(CameraControlProperty.Pan, Int32.Parse(tbPan.Text), CameraControlFlags.Manual);
            videoCaptureDevice.SetCameraProperty(CameraControlProperty.Tilt, Int32.Parse(tbTilt.Text), CameraControlFlags.Manual);
            //videoCaptureDevice.SetCameraProperty(CameraControlProperty.Iris, Int32.Parse(tbIris.Text), CameraControlFlags.Manual);
            videoCaptureDevice.SetCameraProperty(CameraControlProperty.Roll, Int32.Parse(tbRoll.Text), CameraControlFlags.Manual);
            videoCaptureDevice.SetCameraProperty(CameraControlProperty.Focus, Int32.Parse(tbFocus.Text), CameraControlFlags.Manual);

            Contrast = Int32.Parse(tbContrast.Text);

            // Recording params
            width = Int32.Parse(tbRecordingWidth.Text);
            height = Int32.Parse(tbRecordingHeight.Text);
            fps = Int32.Parse(tbRecordingFps.Text);

            // Wyświetlenie informacji 
            lblInfo.Content = "Ustawienia zmienione!";
        }

        private void btnStartRecordingClick(object sender, RoutedEventArgs e)
        {
            btnStartRecording.IsEnabled = false;
            btnStopRecording.IsEnabled = true;
            videoSourcePlayer = new VideoSourcePlayer();
            videoSourcePlayer.VideoSource = videoCaptureDevice;
            videoSourcePlayer.Start();
            videoFileWriter = new VideoFileWriter();


            if(videoSourcePlayer.IsRunning && videoCaptureDevice.IsRunning)
            {
                videoFileWriter.Open("nagranie.wmv", width, height, fps, VideoCodec.WMV1);
                //videoFileWriter.WriteVideoFrame((Bitmap)pictBox.Image);
            }

            // Wyświetlenie informacji 
            lblInfo.Content = "Nagrywanie rozpoczęte!";

            isRecording = true;

        }

        private void btnStopRecordingClick(object sender, RoutedEventArgs e)
        {
            btnStopRecording.IsEnabled = false;
            btnStartRecording.IsEnabled = true;
            isRecording = false;
            videoSourcePlayer.Stop();
            videoFileWriter.Close();

            // Wyświetlenie informacji
            lblInfo.Content = "Nagrywanie zakończone!";
        }

        private void btnDetectionOffClick(object sender, RoutedEventArgs e)
        {
            isLookingForChanges = false;
            btnDetectionOff.IsEnabled = false;
            btnDetectionOn.IsEnabled = true;
        }

        private void btnDetectionOnClick(object sender, RoutedEventArgs e)
        {
            isLookingForChanges = true;
            btnDetectionOff.IsEnabled = true;
            btnDetectionOn.IsEnabled = false;
        }
    }
}
