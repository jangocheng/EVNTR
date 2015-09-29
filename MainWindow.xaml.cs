using System;
using System.IO;
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
using EDSDKLib;
using System.Windows.Threading;

namespace EVNTR
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        SDKHandler CameraHandler;
        List<Camera> CamList;
        Action<BitmapImage> SetImageAction;
        ImageBrush bgbrush = new ImageBrush();
        DispatcherTimer _CountDownTimer = new DispatcherTimer();
        int _CountDownTimeRemaining = 11; // +1 for compensation -- Actually 10s
        private Button btnStartCapture;
        private Grid gridEmailGroup;

        private const string emailInputDefaultText = "E-Mail / Courriel";
        private string imageDirectory = @"C:\Users\" + Environment.UserName + @"\Desktop\EVNTR_Photos";
        private string userEmailsCSV = @"C:\Users\" + Environment.UserName + @"\Desktop\EVNTR_Photos\CSV\emails_and_photos.csv";

        int ErrCount;
        object ErrLock = new object();

        public MainWindow()
        {
            InitializeComponent();

            btnStartCapture = StartCapture;
            ToggleCaptureBtn(true);

            gridEmailGroup = EmailGrouping;
            ToggleEmailGrouping(false);

            ToggleSetupTimer(false);

            _CountDownTimer.Tick += new EventHandler(CountDown_Tick);
            _CountDownTimer.Interval = new TimeSpan(0, 0, 1);

            Closing += new CancelEventHandler(this.KillLiveViewOnClose);

            if (!Directory.Exists(imageDirectory))
            {
                Directory.CreateDirectory(imageDirectory);
            }

            if (!File.Exists(userEmailsCSV))
            {
                string csvHeader = "E-Mail" + "," + "Photo 1" + "," + "Photo 2" + "," + "Photo 3";

                File.WriteAllText(userEmailsCSV, csvHeader);
            }

        }

        private void CountDown_Tick(object sender, EventArgs e)
        {

            if (_CountDownTimeRemaining > 0)
            {
                _CountDownTimeRemaining--;
                Console.WriteLine(_CountDownTimeRemaining);
                TimerPhotoSetup.Content = _CountDownTimeRemaining;
            }
            else
            {
                _CountDownTimer.Stop();
                ToggleSetupTimer(false);
                CameraHandler.TakePhoto();
                ToggleEmailGrouping(true);
            }
        }

        private void ToggleCaptureBtn(bool state)
        {
            if (state == true)
            btnStartCapture.Visibility = Visibility.Visible;
            else
            btnStartCapture.Visibility = Visibility.Collapsed;
        }

        private void ToggleEmailGrouping(bool state)
        {
            if (state == true)
            { 
                gridEmailGroup.Visibility = Visibility.Visible;
                this.PreviewKeyDown -= KeyTakePhotoAction;
            }
            else
            {
                SaveEmail.Focus();
                gridEmailGroup.Visibility = Visibility.Collapsed;
                EmailInput.Text = emailInputDefaultText;
                this.PreviewKeyDown += KeyTakePhotoAction;
            }
        }

        private void ToggleSetupTimer(bool state)
        {
            if (state == true)
            {
                PreviewKeyDown -= KeyTakePhotoAction;
                TimerPhotoSetup.Visibility = Visibility.Visible;
            }
            else
            {
                TimerPhotoSetup.Visibility = Visibility.Collapsed;
                PreviewKeyDown += KeyTakePhotoAction;
            }
        }

        private void WriteDataToCSV(string csvFile)
        {
                // Temp test to write to CSV
                FileInfo[] imageFileNames = Directory.GetFiles(imageDirectory)
                                             .Select(x => new FileInfo(x))
                                             .OrderByDescending(x => x.LastWriteTime)
                                             .Take(3)
                                             .ToArray();

                string dataForCSV = "\n" + EmailInput.Text + "," + imageFileNames[2].Name + "," + imageFileNames[1].Name + "," + imageFileNames[0].Name;
                File.AppendAllText(csvFile, dataForCSV);
        }

        private void KillLiveViewOnClose(object sender, CancelEventArgs e)
        {
            if (CameraHandler.IsLiveViewOn)
                CameraHandler.StopLiveView();
        }

        private void KeyTakePhotoAction(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                // See if you can not hang the liveview update process.
                // Move to seperate thread? Should be on one already though... 
                _CountDownTimeRemaining = 11;
                ToggleSetupTimer(true);
                _CountDownTimer.Start();
                //_timer.Stop();

            }
        }

        private void EmailInput_GotFocus(object sender, RoutedEventArgs e)
        {
            EmailInput.Text = EmailInput.Text == emailInputDefaultText ? string.Empty : EmailInput.Text;
        }

        private void EmailInput_LostFocus(object sender, RoutedEventArgs e)
        {
            EmailInput.Text = EmailInput.Text == string.Empty ? emailInputDefaultText : EmailInput.Text;
        }

        private void SaveEmailOnEnter(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                this.EmailSaveOnClick(EmailInput.Text, e);
            }
        }

        private void EmailSaveOnClick(object sender, RoutedEventArgs e)
        {

            if ((EmailInput.Text == emailInputDefaultText) || (EmailInput.Text == ""))
            {
                MessageBox.Show("Please enter your email!\n SVP entrer votre courriel!");
            }
            else
            {
                WriteDataToCSV(userEmailsCSV);
                ToggleEmailGrouping(false);

                /*

                    IMPLEMENT SAVE EMAIL TO CSV ALONG WITH IMG NAME

                */
            }
        }

        private void StopLiveViewCapture(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                CameraHandler.StopLiveView();
                ToggleCaptureBtn(true);
            }
        }

        private void StartLiveViewCaptureBtn(object sender, RoutedEventArgs e)
        {
            ToggleCaptureBtn(false);

            try
            {
                CameraHandler = new SDKHandler();
                CameraHandler.LiveViewUpdated += new SDKHandler.StreamUpdate(SDK_LiveViewUpdated);
                CamList = CameraHandler.GetCameraList();
                CameraHandler.OpenSession(CamList[0]);
                CameraHandler.SetSetting(EDSDK.PropID_SaveTo, (uint)EDSDK.EdsSaveTo.Both);
                CameraHandler.SetCapacity();
                Console.WriteLine(Environment.UserName);
                CameraHandler.ImageSaveDirectory = imageDirectory;
                SetImageAction = (BitmapImage img) => { bgbrush.ImageSource = img; };
            }
            catch (DllNotFoundException) { ReportError("Canon DLLs not found!", true); }
            catch (Exception ex) { ReportError(ex.Message, true); }

            LiveViewController();

        }

        private void LiveViewController()
        {
            if (!CameraHandler.IsLiveViewOn)
            {
                LVCanvas.Background = bgbrush;
                CameraHandler.StartLiveView();
                this.PreviewKeyDown += KeyTakePhotoAction;
            }
            else
            {
                CameraHandler.StopLiveView();
                LVCanvas.Background = Brushes.LightGray;
            }
        }

        private void SDK_LiveViewUpdated(Stream img)
        {
            try
            {
                if (CameraHandler.IsLiveViewOn)
                {
                    using (WrappingStream s = new WrappingStream(img))
                    {
                        img.Position = 0;
                        BitmapImage EvfImage = new BitmapImage();
                        EvfImage.BeginInit();
                        EvfImage.StreamSource = s;
                        EvfImage.CacheOption = BitmapCacheOption.OnLoad;
                        EvfImage.EndInit();
                        EvfImage.Freeze();
                        Application.Current.Dispatcher.Invoke(SetImageAction, EvfImage);
                    }
                }
            }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }

        private void ReportError(string message, bool lockdown)
        {
            int errc;
            lock (ErrLock) { errc = ++ErrCount; }

            if (errc < 4) MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            else if (errc == 4) MessageBox.Show("Many errors happened!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);

            lock (ErrLock) { ErrCount--; }
        }

    }
}
