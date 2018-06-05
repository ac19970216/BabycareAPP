﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Security.Cryptography;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.Devices.SerialCommunication;
using Windows.UI.Popups;
using System.Threading;
using Windows.System.Threading;
using System.Text.RegularExpressions;
using MySql.Data.MySqlClient;
using Windows.UI;
using Windows.UI.Xaml.Automation.Peers;

namespace 多功能嬰兒助理
{
    /// <summary>
    /// 可以在本身使用或巡覽至框架內的空白頁面。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public string SelectedBleDeviceId, SelectedBleDeviceId1, SelectedBleDeviceId2;
        private ObservableCollection<BluetoothLEAttributeDisplay> ServiceCollection = new ObservableCollection<BluetoothLEAttributeDisplay>();

        private ObservableCollection<BluetoothLEAttributeDisplay> CharacteristicCollection = new ObservableCollection<BluetoothLEAttributeDisplay>();

        private BluetoothLEDevice bluetoothLeDevice = null,
        bluetoothLeDevice1 = null,
        bluetoothLeDevice2 = null;
        private GattCharacteristic selectedCharacteristic;

        // Only one registered characteristic at a time.
        private GattCharacteristic registeredCharacteristic, registeredCharacteristic1, registeredCharacteristic2;
        private GattPresentationFormat presentationFormat;

        private CancellationTokenSource ReadCancellationTokenSource;
        private SerialDevice Arduino_serialDevice = null;
        private SerialDevice Watch_serialDevice = null;
        string[] instruction = { "connect F8-CE-C5-80-79-49;", "disconnect;", "cmd.pair", "menu.g", "icon.g", "menu.hr", "icon.hr" };
        string T1, T2, T3;
        int[] X_Axis = new int[2], Y_Axis = new int[2], Z_Axis = new int[2];
        int Not_moving_count = 0, Moving_count = 0, Normal_HR_count = 0, High_HR_count = 0, Low_HR_count = 0, Normal_T_count = 0, High_T_count = 0, Low_T_count = 0;
        Boolean G_count = false, BLE_connect = false, one = false;
        DataWriter dataWriteObject = null;
        DataReader dataReaderObject = null;
        DBConnectHeartRate db_HR = new DBConnectHeartRate();
        DBConnectTemp db_T = new DBConnectTemp();
        DBConnectWarning db_Warning = new DBConnectWarning();
        ThreadPoolTimer PeriodicTimer, WatchTimer, AppeaseTimer;
        string Nowtime;
        #region Error Codes
        readonly int E_DEVICE_NOT_AVAILABLE = unchecked((int)0x800710df); // HRESULT_FROM_WIN32(ERROR_DEVICE_NOT_AVAILABLE)
        #endregion
        #region UI Code
        public MainPage()
        {
            InitializeComponent();
            SelectedBleDeviceId = "BluetoothLE#BluetoothLE40:e2:30:e0:d0:68-c2:1a:1b:8e:4b:53";
            SelectedBleDeviceId1 = "BluetoothLE#BluetoothLE40:e2:30:e0:d0:68-ec:9f:e8:f0:41:57";
            SelectedBleDeviceId2 = "BluetoothLE#BluetoothLE40:e2:30:e0:d0:68-d1:82:80:8d:13:18";
        }


        public void NotifyUser(string strMessage, NotifyType type)
        {
            // If called from the UI thread, then update immediately.
            // Otherwise, schedule a task on the UI thread to perform the update.
            if (Dispatcher.HasThreadAccess)
            {
                UpdateStatus(strMessage, type);
            }
            else
            {
                var task = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => UpdateStatus(strMessage, type));
            }
        }

        private void UpdateStatus(string strMessage, NotifyType type)
        {
            switch (type)
            {
                case NotifyType.StatusMessage:
                    StatusBorder.Background = new SolidColorBrush(Windows.UI.Colors.Green);
                    break;
                case NotifyType.ErrorMessage:
                    StatusBorder.Background = new SolidColorBrush(Windows.UI.Colors.Red);
                    break;
            }

            StatusBlock.Text = strMessage;

            // Collapse the StatusBlock if it has no text to conserve real estate.
            StatusBorder.Visibility = (StatusBlock.Text != String.Empty) ? Visibility.Visible : Visibility.Collapsed;
            if (StatusBlock.Text != String.Empty)
            {
                StatusBorder.Visibility = Visibility.Visible;
                StatusPanel.Visibility = Visibility.Visible;
            }
            else
            {
                StatusBorder.Visibility = Visibility.Collapsed;
                StatusPanel.Visibility = Visibility.Collapsed;
            }

            // Raise an event if necessary to enable a screen reader to announce the status update.
            var peer = FrameworkElementAutomationPeer.FromElement(StatusBlock);
            if (peer != null)
            {
                peer.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
            }
        }

        async void Footer_Click(object sender, RoutedEventArgs e)
        {
            await Windows.System.Launcher.LaunchUriAsync(new Uri(((HyperlinkButton)sender).Tag.ToString()));
        }

        public enum NotifyType
        {
            StatusMessage,
            ErrorMessage
        };
        protected override async void OnNavigatedFrom(NavigationEventArgs e)
        {
            var success = await ClearBluetoothLEDeviceAsync(0, subscribedForNotifications, registeredCharacteristic);
            if (!success)
            {

            }
            success = await ClearBluetoothLEDeviceAsync(1, subscribedForNotifications, registeredCharacteristic);
            if (!success)
            {

            }
            success = await ClearBluetoothLEDeviceAsync(2, subscribedForNotifications, registeredCharacteristic);
            if (!success)
            {

            }
        }
        #endregion
        #region watch_connect
        private void SendButton_Click()
        {


        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            
            ConnectDongle("COM4");
        }
        private async void ConnectDongle(string com)
        {
            string qFilter = SerialDevice.GetDeviceSelector(com);

            DeviceInformationCollection devices = await DeviceInformation.FindAllAsync(qFilter);
            try
            {
                if (devices.Any())
                {
                    string deviceId = devices.First().Id;
                    await OpenPort(deviceId);
                }
            }
            catch (Exception ex) { };


            ReadCancellationTokenSource = new CancellationTokenSource();
            while (Watch_serialDevice != null)
            {
                await Listen();
            }



        }
        private async void ConnectPort(string com)
        {
            string qFilter = SerialDevice.GetDeviceSelector(com);

            DeviceInformationCollection devices = await DeviceInformation.FindAllAsync(qFilter);
            try
            {
                if (devices.Any())
                {
                    string deviceId = devices.First().Id;
                    await OpenPort(deviceId);
                }
            }
            catch (Exception ex) { };



        }
        private async Task OpenPort(string deviceId)
        {
            if (deviceId.Contains("VID_1366"))
            {
                Watch_serialDevice = await SerialDevice.FromIdAsync(deviceId);

                if (Watch_serialDevice != null)
                {
                    Watch_serialDevice.WriteTimeout = TimeSpan.FromMilliseconds(1000);
                    Watch_serialDevice.ReadTimeout = TimeSpan.FromMilliseconds(1000);
                    Watch_serialDevice.BaudRate = 115200;
                    Watch_serialDevice.Parity = SerialParity.None;
                    Watch_serialDevice.StopBits = SerialStopBitCount.One;
                    Watch_serialDevice.DataBits = 8;
                    txtStatus.Text = "Serial port configured successfully";
                    await SendToPort("pkt " + instruction[2] + ",;");
                }
            }
            else
            {
                Arduino_serialDevice = await SerialDevice.FromIdAsync(deviceId);

                if (Arduino_serialDevice != null)
                {
                    Arduino_serialDevice.WriteTimeout = TimeSpan.FromMilliseconds(1000);
                    Arduino_serialDevice.ReadTimeout = TimeSpan.FromMilliseconds(1000);
                    Arduino_serialDevice.BaudRate = 9600;
                    Arduino_serialDevice.Parity = SerialParity.None;
                    Arduino_serialDevice.StopBits = SerialStopBitCount.One;
                    Arduino_serialDevice.DataBits = 8;
                    ArduinoState.Text = "Arduino藍芽：已連線";
                    ArduinoState_LED.Fill = new SolidColorBrush(Colors.LightGreen);
                }
            }

        }

        private async Task Listen()
        {
            try
            {
                if (Watch_serialDevice != null)
                {
                    dataReaderObject = new DataReader(Watch_serialDevice.InputStream);
                    await ReadAsync(ReadCancellationTokenSource.Token);
                }
            }
            catch (Exception ex)
            {

            }
            finally
            {
                if (dataReaderObject != null)// Cleanup once complete
                {
                    dataReaderObject.DetachStream();
                    dataReaderObject = null;
                }
            }
        }


        private async Task ReadAsync(CancellationToken cancellationToken)
        {
            Task<UInt32> loadAsyncTask;

            Boolean Not_moving;

            uint ReadBufferLength = 5;// only when this buffer would be full next code would be executed

            dataReaderObject.InputStreamOptions = InputStreamOptions.Partial;

            loadAsyncTask = dataReaderObject.LoadAsync(ReadBufferLength).AsTask(cancellationToken); // Create a task object

            UInt32 bytesRead = await loadAsyncTask;// Launch the task and wait until buffer would be full

            if (bytesRead > 0)
            {
                string strFromPort = dataReaderObject.ReadString(bytesRead);
                int fstLetter = strFromPort.IndexOf("Info");
                int lstLetter = strFromPort.IndexOf("Info", fstLetter + 1);
                if ((fstLetter >= 0) && (lstLetter > 0)) strFromPort = strFromPort.Substring(fstLetter, lstLetter - fstLetter);
                Read_Watch.Text = Read_Watch.Text + strFromPort;
                string strLineData;
                char[] delimit = new char[] { '\n' };
                String[] lines = Read_Watch.Text.Split(delimit);
                using (StringReader sr = new StringReader(Read_Watch.Text.Trim()))
                {
                    strLineData = sr.ReadLine();
                    while (!String.IsNullOrEmpty(strLineData))
                    {
                        if (strLineData.Contains("HR=") && strLineData.Contains(",") && (Regex.Replace(strLineData, @"[^\d]", String.Empty) != "0"))
                        {
                            HR_RadialGauge.Value = int.Parse(Regex.Replace(strLineData, @"[^\d]", String.Empty));
                            DateTime myDate = DateTime.Now;
                            string myDateString = myDate.ToString("yyyy-MM-dd HH:mm:ss");
                            string varString = Regex.Replace(strLineData, @"[^\d]", String.Empty);
                            db_HR.Insert(myDateString, varString);

                            HR_Text.Text = HR_RadialGauge.Value + "\nbpm";
                            Read_Watch.Text = lines[lines.Length - 1];
                            if (HR_RadialGauge.Value <= 110)
                            {
                                HR_RadialGauge.ScaleTickBrush = new SolidColorBrush(Colors.Red);
                                HR_RadialGauge.TrailBrush = new SolidColorBrush(Colors.Red);

                                Normal_HR_count = 0;

                                if (Low_HR_count == 10)
                                {
                                    String text = "1";
                                    db_Warning.Insert(myDateString, text);
                                    //心率過低
                                    Baby_HR_State.Text = "嬰兒心率：過低";
                                    Baby_HR_LED.Fill = new SolidColorBrush(Colors.Red);
                                    Low_HR_count = 30;
                                }
                                else if (Low_HR_count < 10)
                                {

                                    Low_HR_count++;
                                    if (Baby_HR_State.Text != "嬰兒心率：過高" && Baby_State.Text != "嬰兒狀態：哭鬧中")
                                    {
                                        Baby_HR_State.Text = "嬰兒心率：分析中";
                                        Baby_HR_LED.Fill = new SolidColorBrush(Colors.Yellow);
                                    }
                                }
                            }
                            else if (HR_RadialGauge.Value < 150)
                            {
                                HR_RadialGauge.ScaleTickBrush = new SolidColorBrush(Colors.LightGreen);
                                HR_RadialGauge.TrailBrush = new SolidColorBrush(Colors.LightGreen);
                                //心率正常
                                Low_HR_count = 0;
                                High_HR_count = 0;

                                if (Normal_HR_count == 30)
                                {

                                    Baby_HR_State.Text = "嬰兒心率：正常";
                                    Baby_HR_LED.Fill = new SolidColorBrush(Colors.LightGreen);
                                    String text = "0";
                                    if (Baby_State.Text != "嬰兒狀態：哭鬧中" && Baby_T_State.Text == "嬰兒體溫：正常" && Baby_HR_State.Text == "嬰兒心率：正常") db_Warning.Insert(myDateString, text);

                                    if (Appease_State.Text == "安撫功能：啟動")
                                    {
                                        HR_G_Enable(3);
                                        
                                    }
                                    else
                                    {
                                        Baby_State.Text = "嬰兒狀態：未哭鬧";
                                        Baby_LED.Fill = new SolidColorBrush(Colors.LightGreen);
                                    }
                                    Normal_HR_count = 40;
                                }
                                else if (Normal_HR_count < 30)
                                {
                                    Normal_HR_count++;
                                    if (Appease_State.Text == "安撫功能：停止" && Baby_State.Text != "嬰兒狀態：哭鬧中")
                                    {
                                        Baby_State.Text = "嬰兒狀態：分析中";
                                        Baby_LED.Fill = new SolidColorBrush(Colors.Yellow);
                                    }

                                    if (Baby_HR_State.Text != "嬰兒心率：過高" && Baby_State.Text != "嬰兒狀態：哭鬧中")
                                    {
                                        Baby_HR_State.Text = "嬰兒心率：分析中";
                                        Baby_HR_LED.Fill = new SolidColorBrush(Colors.Yellow);
                                    }
                                }
                            }
                            else if (HR_RadialGauge.Value >= 150)
                            {
                                HR_RadialGauge.ScaleTickBrush = new SolidColorBrush(Colors.Red);
                                HR_RadialGauge.TrailBrush = new SolidColorBrush(Colors.Red);
                                //心率過高
                                Normal_HR_count = 0;

                                if (Baby_HR_State.Text == "嬰兒心率：過高" || Baby_State.Text == "嬰兒狀態：哭鬧中") ;

                                else if (High_HR_count == 10)
                                {
                                    HR_G_Enable(3);
                                    High_HR_count = 22;

                                }

                                else if (High_HR_count < 10)
                                {
                                    High_HR_count++;
                                    Baby_State.Text = "嬰兒狀態：分析中";
                                    Baby_LED.Fill = new SolidColorBrush(Colors.Yellow);
                                    if (Baby_HR_State.Text != "嬰兒心率：過高" && Baby_State.Text != "嬰兒狀態：哭鬧中")
                                    {
                                        Baby_HR_State.Text = "嬰兒心率：分析中";
                                        Baby_HR_LED.Fill = new SolidColorBrush(Colors.Yellow);
                                    }

                                }
                            }

                        }
                        else if (lines[lines.Length - 2].Contains("G=") && lines[lines.Length - 2].Contains(","))
                        {

                            strLineData = Regex.Replace(strLineData, @"[^\d]", String.Empty);
                            Read_Watch.Text = lines[lines.Length - 1];
                            X.Text = "";
                            Y.Text = "";
                            Z.Text = "";
                            string[] substrings = Regex.Split(strLineData, "");
                            for (int ctr = 0; ctr < substrings.Length; ctr++)
                            {
                                if (ctr < 5) X.Text = X.Text + substrings[ctr];
                                else if (ctr < 9) Y.Text = Y.Text + substrings[ctr];
                                else Z.Text = Z.Text + substrings[ctr];
                            }
                            if (G_count)
                            {
                                X_Axis[1] = int.Parse(X.Text);
                                Y_Axis[1] = int.Parse(Y.Text);
                                Z_Axis[1] = int.Parse(Z.Text);
                                G_count = false;
                            }
                            else
                            {
                                X_Axis[0] = int.Parse(X.Text);
                                Y_Axis[0] = int.Parse(Y.Text);
                                Z_Axis[0] = int.Parse(Z.Text);
                                G_count = true;
                            }

                            if (Math.Abs(X_Axis[1] - X_Axis[0]) > 20 || Math.Abs(Y_Axis[1] - Y_Axis[0]) > 20 || Math.Abs(Z_Axis[1] - Z_Axis[0]) > 20) Not_moving = false;
                            else Not_moving = true;


                            if (Moving_count == 10)
                            {
                                Baby_State.Text = "嬰兒狀態：哭鬧中";
                                Baby_LED.Fill = new SolidColorBrush(Colors.Red);
                                Baby_HR_State.Text = "嬰兒心率：正常";
                                Baby_HR_LED.Fill = new SolidColorBrush(Colors.LightGreen);

                                HR_G_Enable(0);
                                if (Appease_State.Text == "安撫功能：停止")
                                {
                                    Appease_Timer();
                                    Appease_State.Text = "安撫功能：啟動";
                                }
                                Appease_LED.Fill = new SolidColorBrush(Colors.LightGreen);
                                if (PromoteSleep_State.Text == "促眠功能：停止") await SendToArduino("1");
                                Moving_count = 20;

                            }
                            else if (Not_moving_count == 10)
                            {
                                if (Appease_State.Text == "安撫功能：停止")
                                {
                                    Baby_HR_State.Text = "嬰兒心率：過高";
                                    Baby_HR_LED.Fill = new SolidColorBrush(Colors.Red);
                                    Baby_State.Text = "嬰兒狀態：未哭鬧";
                                    Baby_LED.Fill = new SolidColorBrush(Colors.LightGreen);
                                    DateTime myDate = DateTime.Now;
                                    string myDateString = myDate.ToString("yyyy-MM-dd HH:mm:ss");
                                    String text = "2";
                                    db_Warning.Insert(myDateString, text);
                                }
                                else
                                {
                                    Baby_State.Text = "嬰兒狀態：已安撫";
                                    Baby_LED.Fill = new SolidColorBrush(Colors.LightGreen);
                                }
                                Not_moving_count = 20;
                                HR_G_Enable(0);
                                Appease_State.Text = "安撫功能：停止";
                                Appease_LED.Fill = new SolidColorBrush(Colors.DarkRed);
                                if (PromoteSleep_State.Text == "促眠功能：停止") await SendToArduino("2");
                            }
                            else if (Not_moving_count != 11 && Moving_count != 11)
                            {
                                if (Not_moving)
                                {
                                    Not_moving_count++;
                                    Moving_count = 0;

                                }
                                else
                                {
                                    Moving_count++;
                                    Not_moving_count = 0;

                                }
                            }


                        }

                        else if (Read_Watch.Text.Contains("disconnect"))
                        {
                            WatchState.Text = "手錶：未連線";
                            WatchState_LED.Fill = new SolidColorBrush(Colors.DarkRed);
                            Read_Watch.Text = lines[lines.Length - 1];
                        }
                        else if ((Read_Watch.Text.Contains("TX characteristic") || (Read_Watch.Text.Contains("BAT=") && Read_Watch.Text.Contains(","))) && WatchState.Text == "手錶：未連線")
                        {
                            WatchState.Text = "手錶：已連線";
                            Nowtime = Regex.Replace(DateTime.Now.ToString("yyyy-MM-dd HH:mm"), @"[^\d]", String.Empty);
                            WatchState_LED.Fill = new SolidColorBrush(Colors.LightGreen);
                            await SendToPort("pkt T=" + Nowtime + ",;");
                            Read_Watch.Text = lines[lines.Length - 1];
                            HR_G_Enable(0);


                        }
                        strLineData = sr.ReadLine();
                    }

                }

                txtStatus.Text = "Read at " + DateTime.Now.ToString(System.Globalization.CultureInfo.CurrentUICulture.DateTimeFormat.LongTimePattern);
            }
        }

        private async Task WriteAsync(string text2write)
        {
            Task<UInt32> storeAsyncTask;

            if (text2write.Length != 0)
            {
                dataWriteObject.WriteString(text2write);

                storeAsyncTask = dataWriteObject.StoreAsync().AsTask();// Create a task object

                UInt32 bytesWritten = await storeAsyncTask; // Launch the task and wait
                if (bytesWritten > 0)
                {
                    txtStatus.Text = bytesWritten + " bytes written at " + DateTime.Now.ToString(System.Globalization.CultureInfo.CurrentUICulture.DateTimeFormat.LongTimePattern);
                }
            }
            else { }
        }


        private async Task SendToPort(string sometext)
        {
            try
            {
                if (Watch_serialDevice != null)
                {
                    dataWriteObject = new DataWriter(Watch_serialDevice.OutputStream);

                    await WriteAsync(sometext);
                }
                else { }
            }
            catch (Exception ex)
            {
                txtStatus.Text = ex.Message;
            }
            finally
            {
                if (dataWriteObject != null) // Cleanup once complete
                {
                    dataWriteObject.DetachStream();
                    dataWriteObject = null;
                }
            }
        }
        private async Task SendToArduino(string sometext)
        {
            try
            {
                if (Arduino_serialDevice != null)
                {
                    dataWriteObject = new DataWriter(Arduino_serialDevice.OutputStream);

                    await WriteAsync(sometext);
                }
                else { }
            }
            catch (Exception ex)
            {
                txtStatus.Text = ex.Message;
            }
            finally
            {
                if (dataWriteObject != null) // Cleanup once complete
                {
                    dataWriteObject.DetachStream();
                    dataWriteObject = null;
                }
            }
        }
        private void CancelReadTask()
        {
            if (ReadCancellationTokenSource != null)
            {
                if (!ReadCancellationTokenSource.IsCancellationRequested)
                {
                    ReadCancellationTokenSource.Cancel();
                }
            }
        }

        private async void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            CancelReadTask();
            if (Watch_serialDevice != null)
            {
                Watch_serialDevice.Dispose();
            }
            Watch_serialDevice = null;
            await ClearBluetoothLEDeviceAsync(0, subscribedForNotifications, registeredCharacteristic);
            await ClearBluetoothLEDeviceAsync(1, subscribedForNotifications, registeredCharacteristic1);
            await ClearBluetoothLEDeviceAsync(2, subscribedForNotifications, registeredCharacteristic2);
        }
        #endregion
        #region get_Temperature
        #region Enumerating Services
        private async Task<bool> ClearBluetoothLEDeviceAsync(int i, bool subscribedForNotifications_, GattCharacteristic registeredCharacteristic_)
        {
            if (subscribedForNotifications_)
            {
                // Need to clear the CCCD from the remote device so we stop receiving notifications
                var result = await registeredCharacteristic_.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.None);
                if (result != GattCommunicationStatus.Success)
                {
                    return false;
                }
                else
                {
                    selectedCharacteristic.ValueChanged -= Characteristic_ValueChanged;
                    subscribedForNotifications_ = false;
                    if (i == 0) subscribedForNotifications = subscribedForNotifications_;
                }
            }
            if (i == 0)
            {
                bluetoothLeDevice?.Dispose();
                bluetoothLeDevice = null;
            }
            else if (i == 1)
            {
                bluetoothLeDevice1?.Dispose();
                bluetoothLeDevice1 = null;
            }
            else if (i == 2)
            {
                bluetoothLeDevice2?.Dispose();
                bluetoothLeDevice2 = null;
            }

            return true;
        }

        private  void ConnectButton_Click1()
        {
            TimeSpan period = TimeSpan.FromSeconds(1);
            ConnectButton.IsEnabled = false;
            int i = 0;
            
            PeriodicTimer = ThreadPoolTimer.CreatePeriodicTimer(async (source) =>
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.High,
                async () =>
                {
                    if (!BLE_connect && WatchState.Text == "手錶：未連線" && i < 1)
                    {

                        await SendToPort(instruction[0]);
                        i = 6;

                    }
                    else if (ArduinoState.Text == "Arduino藍芽：未連線" && i == 6 || i==0)
                    {
                        ConnectPort("COM8");
                        i = 1;
                    } 
                    else if (!BLE_connect && Temperature1_State.Text.Contains("未連線") && i < 2)
                    {
                        ConnectButton_Click(bluetoothLeDevice, SelectedBleDeviceId, 0);
                        i = 3;
                    }
                    else if (!BLE_connect && Temperature2_State.Text.Contains("未連線") && i < 3)
                    {
                        ConnectButton_Click(bluetoothLeDevice1, SelectedBleDeviceId1, 1);
                        i = 2;
                    }
                    else if (!BLE_connect && Temperature3_State.Text.Contains("未連線") && i < 4)
                    {
                        ConnectButton_Click(bluetoothLeDevice2, SelectedBleDeviceId2, 2);
                        i = 5;
                    }
    
                    
                    else if (!BLE_connect)
                    {
                        close_timer();
                        i = 0;
                        ConnectButton.IsEnabled = true;
                    }

                });

            }, period);
        }
        private void HR_G_Enable(int i)
        {
            TimeSpan period = TimeSpan.FromSeconds(1);

            WatchTimer = ThreadPoolTimer.CreatePeriodicTimer(async (source) =>
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.High,
                async () =>
                {

                    if (i == 0)
                    {
                        await SendToPort("pkt " + instruction[5] + ",;");
                        G_State.Text = "G-Sensor：未執行";
                        G_LED.Fill = new SolidColorBrush(Colors.DarkRed);
                        i = 1;
                    }
                    else if (i == 1)
                    {
                        HR_State.Text = "心率偵測：執行中";
                        HR_LED.Fill = new SolidColorBrush(Colors.LightGreen);
                        await SendToPort("pkt " + instruction[6] + ",;");
                        i = 2;
                        Moving_count = 0;
                        Not_moving_count = 0;
                    }
                    else if (i == 2)
                    {
                        close_WatchTimer();
                    }
                    else if (i == 3)
                    {
                        HR_State.Text = "心率偵測：未執行";
                        HR_LED.Fill = new SolidColorBrush(Colors.DarkRed);
                        await SendToPort("pkt " + instruction[3] + ",;");
                        Moving_count = 0;
                        Not_moving_count = 0;
                        i = 4;
                    }
                    else if (i == 4)
                    {
                        await SendToPort("pkt " + instruction[4] + ",;");
                        G_State.Text = "G-Sensor：執行中";
                        G_LED.Fill = new SolidColorBrush(Colors.LightGreen);
                        i = 5;
                    }
                    else
                    {
                        close_WatchTimer();
                    }
                });

            }, period);
        }
        private void Appease_Timer()
        {
            int i = 0;
            TimeSpan period = TimeSpan.FromSeconds(1);

            AppeaseTimer = ThreadPoolTimer.CreatePeriodicTimer(async (source) =>
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.High,
                async () =>
                {
                    i++;
                    if (i == 10)
                    {
                        if (Baby_State.Text == "嬰兒狀態：哭鬧中")
                        {
                            DateTime myDate = DateTime.Now;
                            string myDateString = myDate.ToString("yyyy-MM-dd HH:mm:ss");
                            String text = "5";
                            db_Warning.Insert(myDateString, text);
                        }
                        Appease_State.Text = "安撫功能：停止";
                        Appease_LED.Fill = new SolidColorBrush(Colors.DarkRed);
                        if (PromoteSleep_State.Text == "促眠功能：停止") await SendToArduino("2");
                        
                        close_AppeaseTimer();

                    }
                    else if (Baby_State.Text == "嬰兒狀態：已安撫")
                    {
                        Appease_State.Text = "安撫功能：停止";
                        Appease_LED.Fill = new SolidColorBrush(Colors.DarkRed);
                        if (PromoteSleep_State.Text == "促眠功能：停止") await SendToArduino("2");
                        close_AppeaseTimer();
                    }

                });

            }, period);
        }
        private void close_WatchTimer()
        {
            WatchTimer.Cancel();
        }
        private void close_AppeaseTimer()
        {
            AppeaseTimer.Cancel();
        }
        private void close_timer()
        {
            PeriodicTimer.Cancel();
        }

        private async void ConnectButton_Click(BluetoothLEDevice bluetoothLeDevice_, string SelectedBleDeviceId_, int i)
        {
            BLE_connect = true;
            ServiceCollection.Clear();
            try
            {
                // BT_Code: BluetoothLEDevice.FromIdAsync must be called from a UI thread because it may prompt for consent.
                bluetoothLeDevice_ = await BluetoothLEDevice.FromIdAsync(SelectedBleDeviceId_);

                if (bluetoothLeDevice_ == null)
                {
                    NotifyUser("Failed to connect to device.", NotifyType.ErrorMessage);
                    BLE_connect = false;
                }
            }
            catch (Exception ex) when (ex.HResult == E_DEVICE_NOT_AVAILABLE)
            {
                NotifyUser("Bluetooth radio is not on.", NotifyType.ErrorMessage);
                BLE_connect = false;
            }

            if (bluetoothLeDevice_ != null)
            {
                // Note: BluetoothLEDevice.GattServices property will return an empty list for unpaired devices. For all uses we recommend using the GetGattServicesAsync method.
                // BT_Code: GetGattServicesAsync returns a list of all the supported services of the device (even if it's not paired to the system).
                // If the services supported by the device are expected to change during BT usage, subscribe to the GattServicesChanged event.
                GattDeviceServicesResult result = await bluetoothLeDevice_.GetGattServicesAsync(BluetoothCacheMode.Uncached);

                if (result.Status == GattCommunicationStatus.Success)
                {
                    var services = result.Services;
                    foreach (var service in services)
                    {
                        ServiceCollection.Add(new BluetoothLEAttributeDisplay(service));
                    }


                    ServiceList_SelectionChanged(i);
                }
                else
                {
                    if (i == 0)
                    {
                        Temperature1_State.Text = "體溫計(腋溫)：未連線";
                        Temperature1_State_LED.Fill = new SolidColorBrush(Colors.DarkRed);
                    }
                    else if (i == 1)
                    {
                        Temperature2_State.Text = "體溫計(臉)：未連線";
                        Temperature2_State_LED.Fill = new SolidColorBrush(Colors.DarkRed);
                    }
                    else if (i == 2)
                    {
                        Temperature3_State.Text = "體溫計(室溫)：未連線";
                        Temperature3_State_LED.Fill = new SolidColorBrush(Colors.DarkRed);
                    }
                    BLE_connect = false;
                }
            }
        }
        #endregion

        #region Enumerating Characteristics
        private async void ServiceList_SelectionChanged(int i)
        {
            ServiceList.SelectedIndex = 3;
            var attributeInfoDisp = (BluetoothLEAttributeDisplay)ServiceList.SelectedItem;

            CharacteristicCollection.Clear();

            IReadOnlyList<GattCharacteristic> characteristics = null;
            try
            {
                // Ensure we have access to the device.
                var accessStatus = await attributeInfoDisp.service.RequestAccessAsync();
                if (accessStatus == DeviceAccessStatus.Allowed)
                {
                    // BT_Code: Get all the child characteristics of a service. Use the cache mode to specify uncached characterstics only 
                    // and the new Async functions to get the characteristics of unpaired devices as well. 
                    var result = await attributeInfoDisp.service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
                    if (result.Status == GattCommunicationStatus.Success)
                    {
                        characteristics = result.Characteristics;

                    }
                    else
                    {
                        NotifyUser("Error accessing service.", NotifyType.ErrorMessage);

                        // On error, act as if there are no characteristics.
                        characteristics = new List<GattCharacteristic>();
                        BLE_connect = false;
                    }
                }
                else
                {
                    // Not granted access
                    NotifyUser("Error accessing service.", NotifyType.ErrorMessage);
                    BLE_connect = false;
                    // On error, act as if there are no characteristics.
                    characteristics = new List<GattCharacteristic>();

                }
            }
            catch (Exception ex)
            {
                NotifyUser("Restricted service. Can't read characteristics: " + ex.Message,
                NotifyType.ErrorMessage);
                BLE_connect = false;
                // On error, act as if there are no characteristics.
                characteristics = new List<GattCharacteristic>();
            }

            foreach (GattCharacteristic c in characteristics)
            {
                CharacteristicCollection.Add(new BluetoothLEAttributeDisplay(c));
                CharacteristicList_SelectionChanged(i);
            }

        }
        #endregion

        private void AddValueChangedHandler()
        {

            registeredCharacteristic = selectedCharacteristic;
            registeredCharacteristic.ValueChanged += Characteristic_ValueChanged;
            subscribedForNotifications = true;

        }
        private void AddValueChangedHandler1()
        {

            registeredCharacteristic1 = selectedCharacteristic;
            registeredCharacteristic1.ValueChanged += Characteristic_ValueChanged1;
            subscribedForNotifications = true;

        }
        private void AddValueChangedHandler2()
        {

            registeredCharacteristic2 = selectedCharacteristic;
            registeredCharacteristic2.ValueChanged += Characteristic_ValueChanged2;
            subscribedForNotifications = true;

        }

        private async void CharacteristicList_SelectionChanged(int i)
        {
            selectedCharacteristic = null;
            CharacteristicList.SelectedIndex = 0;
            var attributeInfoDisp = (BluetoothLEAttributeDisplay)CharacteristicList.SelectedItem;
            if (attributeInfoDisp == null)
            {
                EnableCharacteristicPanels(GattCharacteristicProperties.None);
                return;
            }

            selectedCharacteristic = attributeInfoDisp.characteristic;
            if (selectedCharacteristic == null)
            {
                NotifyUser("No characteristic selected", NotifyType.ErrorMessage);
                BLE_connect = false;
                return;
            }

            // Get all the child descriptors of a characteristics. Use the cache mode to specify uncached descriptors only 
            // and the new Async functions to get the descriptors of unpaired devices as well. 
            var result = await selectedCharacteristic.GetDescriptorsAsync(BluetoothCacheMode.Uncached);
            if (result.Status != GattCommunicationStatus.Success)
            {
                NotifyUser("Descriptor read failure: " + result.Status.ToString(), NotifyType.ErrorMessage);
                BLE_connect = false;
            }

            // BT_Code: There's no need to access presentation format unless there's at least one. 
            presentationFormat = null;
            if (selectedCharacteristic.PresentationFormats.Count > 0)
            {

                if (selectedCharacteristic.PresentationFormats.Count.Equals(1))
                {
                    // Get the presentation format since there's only one way of presenting it
                    presentationFormat = selectedCharacteristic.PresentationFormats[0];
                }
                else
                {
                    // It's difficult to figure out how to split up a characteristic and encode its different parts properly.
                    // In this case, we'll just encode the whole thing to a string to make it easy to print out.
                }
            }

            // Enable/disable operations based on the GattCharacteristicProperties.
            EnableCharacteristicPanels(selectedCharacteristic.CharacteristicProperties);
            ValueChangedSubscribeToggle_Click(i);
        }

        private void SetVisibility(UIElement element, bool visible)
        {
            element.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void PromoteSleep_Click(object sender, RoutedEventArgs e)
        {

            if (PromoteSleep_State.Text == "促眠功能：停止")
            {
                PromoteSleep_State.Text = "促眠功能：啟動";
                PromoteSleep.Content = "停止促眠";
                PromoteSleep_LED.Fill = new SolidColorBrush(Colors.LightGreen);
                await SendToArduino("1");
                HR_G_Enable(0);
            }
            else
            {
                PromoteSleep_State.Text = "促眠功能：停止";
                PromoteSleep_LED.Fill = new SolidColorBrush(Colors.DarkRed);
                PromoteSleep.Content = "啟動促眠";
                await SendToArduino("2");
            }
        }

        private void EnableCharacteristicPanels(GattCharacteristicProperties properties)
        {
            // BT_Code: Hide the controls which do not apply to this characteristic.
            SetVisibility(CharacteristicReadButton, properties.HasFlag(GattCharacteristicProperties.Read));


        }

        private async void CharacteristicReadButton_Click()
        {
            // BT_Code: Read the actual value from the device by using Uncached.
            GattReadResult result = await selectedCharacteristic.ReadValueAsync(BluetoothCacheMode.Uncached);
            if (result.Status == GattCommunicationStatus.Success)
            {
                string formattedResult = FormatValueByPresentation(result.Value, presentationFormat);
                NotifyUser($"Read result: {formattedResult}", NotifyType.StatusMessage);
            }
            else
            {
                NotifyUser($"Read failed: {result.Status}", NotifyType.ErrorMessage);
            }
        }

        private bool subscribedForNotifications = false;
        private async void ValueChangedSubscribeToggle_Click(int i)
        {

            // initialize status
            GattCommunicationStatus status = GattCommunicationStatus.Unreachable;
            var cccdValue = GattClientCharacteristicConfigurationDescriptorValue.None;
            if (selectedCharacteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Indicate))
            {
                cccdValue = GattClientCharacteristicConfigurationDescriptorValue.Indicate;
            }

            else if (selectedCharacteristic.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Notify))
            {
                cccdValue = GattClientCharacteristicConfigurationDescriptorValue.Notify;
            }

            try
            {
                // BT_Code: Must write the CCCD in order for server to send indications.
                // We receive them in the ValueChanged event handler.
                status = await selectedCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(cccdValue);

                if (status == GattCommunicationStatus.Success)
                {

                    if (i == 0)
                    {
                        AddValueChangedHandler();
                        Temperature1_State.Text = "體溫計(腋溫)：已連線";
                        Temperature1_State_LED.Fill = new SolidColorBrush(Colors.LightGreen);
                    }
                    else if (i == 1)
                    {
                        AddValueChangedHandler1();
                        Temperature2_State.Text = "體溫計(臉)：已連線";
                        Temperature2_State_LED.Fill = new SolidColorBrush(Colors.LightGreen);
                    }
                    else
                    {
                        AddValueChangedHandler2();
                        Temperature3_State.Text = "體溫計(室溫)：已連線";
                        Temperature3_State_LED.Fill = new SolidColorBrush(Colors.LightGreen);
                    }
                    BLE_connect = false;
                }
                else
                {
                    NotifyUser($"Error registering for value changes: {status}", NotifyType.ErrorMessage);
                    BLE_connect = false;
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                // This usually happens when a device reports that it support indicate, but it actually doesn't.
                NotifyUser(ex.Message, NotifyType.ErrorMessage);
                BLE_connect = false;
            }


        }

        private async void Characteristic_ValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            // BT_Code: An Indicate or Notify reported that the value has changed.
            // Display the new value with a timestamp.
            var newValue = FormatValueByPresentation(args.CharacteristicValue, presentationFormat);
            var message = $"Value at {DateTime.Now:hh:mm:ss.FFF}: Temperature: {newValue} °C";
            T1 = Convert.ToString( double.Parse(newValue)+7.7);
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
            () =>
            {
                if (double.Parse(T1) % 1 == 0) T1_Text.Text = T1 + ".0\n°C";
                else T1_Text.Text = T1 + "\n°C";
                T1_RadialGauge.Value = double.Parse(T1) * 10;
                if (double.Parse(T1) < 35)
                {
                    T1_RadialGauge.ScaleTickBrush = new SolidColorBrush(Colors.Red);
                    T1_RadialGauge.TrailBrush = new SolidColorBrush(Colors.Red);

                    Normal_T_count = 0;
                    if (Low_T_count == 10)
                    {
                        Baby_T_State.Text = "嬰兒體溫：過低";
                        Baby_T_LED.Fill = new SolidColorBrush(Colors.Red);
                        DateTime myDate = DateTime.Now;
                        string myDateString = myDate.ToString("yyyy-MM-dd HH:mm:ss");
                        String text = "3";
                        db_Warning.Insert(myDateString, text);
                        Low_T_count = 20;
                    }
                    else if (Low_T_count < 10)
                    {
                        Low_T_count++;
                        Baby_T_State.Text = "嬰兒體溫：分析中";
                        Baby_T_LED.Fill = new SolidColorBrush(Colors.Yellow);
                    }
                }
                else if (double.Parse(T1) < 37.2)
                {
                    High_T_count = 0;
                    Low_T_count = 0;
                    if (Normal_T_count == 5)
                    {
                        Baby_T_State.Text = "嬰兒體溫：正常";
                        Baby_T_LED.Fill = new SolidColorBrush(Colors.LightGreen);
                        DateTime myDate = DateTime.Now;
                        string myDateString = myDate.ToString("yyyy-MM-dd HH:mm:ss");
                        String text = "0";
                        if(Baby_State.Text!="嬰兒狀態：哭鬧中" && Baby_T_State.Text == "嬰兒體溫：正常" && Baby_HR_State.Text == "嬰兒心率：正常") db_Warning.Insert(myDateString, text);
                        Normal_T_count = 20;
                    }
                    else if (Normal_T_count < 10)
                    {
                        Normal_T_count++;
                        Baby_T_State.Text = "嬰兒體溫：分析中";
                        Baby_T_LED.Fill = new SolidColorBrush(Colors.Yellow);
                    }
                    T1_RadialGauge.ScaleTickBrush = new SolidColorBrush(Colors.LightGreen);
                    T1_RadialGauge.TrailBrush = new SolidColorBrush(Colors.LightGreen);
                }
                else
                {
                    T1_RadialGauge.ScaleTickBrush = new SolidColorBrush(Colors.Red);
                    T1_RadialGauge.TrailBrush = new SolidColorBrush(Colors.Red);

                    Normal_T_count = 0;
                    if (High_T_count == 10)
                    {
                        Baby_T_State.Text = "嬰兒體溫：發燒";
                        Baby_T_LED.Fill = new SolidColorBrush(Colors.Red);
                        DateTime myDate = DateTime.Now;
                        string myDateString = myDate.ToString("yyyy-MM-dd HH:mm:ss");
                        String text = "4";
                        db_Warning.Insert(myDateString, text);
                        High_T_count = 20;
                    }
                    else if (High_T_count < 10)
                    {
                        High_T_count++;
                        Baby_T_State.Text = "嬰兒體溫：分析中";
                        Baby_T_LED.Fill = new SolidColorBrush(Colors.Yellow);
                    }
                }
            });

        }
        private async void Characteristic_ValueChanged1(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            // BT_Code: An Indicate or Notify reported that the value has changed.
            // Display the new value with a timestamp.
            var newValue = FormatValueByPresentation(args.CharacteristicValue, presentationFormat);
            T2 = newValue;
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
            () =>
            {
                if (double.Parse(T2) % 1 == 0) T2_Text.Text = T2 + ".0\n°C";
                else T2_Text.Text = T2 + "\n°C";
                T2_RadialGauge.Value = double.Parse(T2) * 10;
                if (double.Parse(T2) < 30)
                {
                    T2_RadialGauge.ScaleTickBrush = new SolidColorBrush(Colors.LightGreen);
                    T2_RadialGauge.TrailBrush = new SolidColorBrush(Colors.LightGreen);
                }
                else
                {
                    T2_RadialGauge.ScaleTickBrush = new SolidColorBrush(Colors.Red);
                    T2_RadialGauge.TrailBrush = new SolidColorBrush(Colors.Red);
                }
            });

        }
        private async void Characteristic_ValueChanged2(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            // BT_Code: An Indicate or Notify reported that the value has changed.
            // Display the new value with a timestamp.
            var newValue = FormatValueByPresentation(args.CharacteristicValue, presentationFormat);
            T3 = newValue;
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
            () =>
            {
                if (double.Parse(T3) % 1 == 0) T3_Text.Text = T3 + ".0\n°C";
                else T3_Text.Text = T3 + "\n°C";
                T3_RadialGauge.Value = double.Parse(T3) * 10;
                if (double.Parse(T3) < 30)
                {
                    T3_RadialGauge.ScaleTickBrush = new SolidColorBrush(Colors.LightGreen);
                    T3_RadialGauge.TrailBrush = new SolidColorBrush(Colors.LightGreen);
                }
                else
                {
                    T3_RadialGauge.ScaleTickBrush = new SolidColorBrush(Colors.Red);
                    T3_RadialGauge.TrailBrush = new SolidColorBrush(Colors.Red);
                }
                DateTime myDate = DateTime.Now;
                string myDateString = myDate.ToString("yyyy-MM-dd HH:mm:ss");
                db_T.Insert(myDateString, T1, T3, T2);
            });

        }

        private string FormatValueByPresentation(IBuffer buffer, GattPresentationFormat format)
        {
            // BT_Code: For the purpose of this sample, this function converts only UInt32 and
            // UTF-8 buffers to readable text. It can be extended to support other formats if your app needs them.
            CryptographicBuffer.CopyToByteArray(buffer, out byte[] data);
            if (format != null)
            {
                if (format.FormatType == GattPresentationFormatTypes.UInt32 && data.Length >= 4)
                {
                    return BitConverter.ToInt32(data, 0).ToString();
                }
                else if (format.FormatType == GattPresentationFormatTypes.Utf8)
                {
                    try
                    {
                        return Encoding.UTF8.GetString(data);
                    }
                    catch (ArgumentException)
                    {
                        return "(error: Invalid UTF-8 string)";
                    }
                }
                else
                {
                    // Add support for other format types as needed.
                    return "Unsupported format: " + CryptographicBuffer.EncodeToHexString(buffer);
                }
            }
            else if (data != null)
            {
                // We don't know what format to use. Let's try some well-known profiles, or default back to UTF-8.
                if (selectedCharacteristic.Uuid.Equals(GattCharacteristicUuids.TemperatureMeasurement))
                {
                    try
                    {
                        return (ConvertTemperatureData(data) / 1000).ToString();
                    }
                    catch (ArgumentException)
                    {
                        return "Temperature: (unable to parse)";
                    }
                }
                else if (selectedCharacteristic.Uuid.Equals(GattCharacteristicUuids.BatteryLevel))
                {
                    try
                    {
                        // battery level is encoded as a percentage value in the first byte according to
                        // https://www.bluetooth.com/specifications/gatt/viewer?attributeXmlFile=org.bluetooth.characteristic.battery_level.xml
                        return "Battery Level: " + data[0].ToString() + "%";
                    }
                    catch (ArgumentException)
                    {
                        return "Battery Level: (unable to parse)";
                    }
                }
                // This is our custom calc service Result UUID. Format it like an Int
                else if (selectedCharacteristic.Uuid.Equals(Constants.ResultCharacteristicUuid))
                {
                    return BitConverter.ToInt32(data, 0).ToString();
                }
                // No guarantees on if a characteristic is registered for notifications.
                else if (registeredCharacteristic != null)
                {
                    // This is our custom calc service Result UUID. Format it like an Int
                    if (registeredCharacteristic.Uuid.Equals(Constants.ResultCharacteristicUuid))
                    {
                        return BitConverter.ToInt32(data, 0).ToString();
                    }
                }
                else
                {
                    try
                    {
                        return "Unknown format: " + Encoding.UTF8.GetString(data);
                    }
                    catch (ArgumentException)
                    {
                        return "Unknown format";
                    }
                }
            }
            else
            {
                return "Empty data received";
            }
            return "Unknown format";
        }

        private void PieChart_Loaded(object sender, RoutedEventArgs e)
        {

        }

        /// <summary>
        /// Process the raw data received from the device into application usable data,
        /// according the the Bluetooth Heart Rate Profile.
        /// https://www.bluetooth.com/specifications/gatt/viewer?attributeXmlFile=org.bluetooth.characteristic.heart_rate_measurement.xml&u=org.bluetooth.characteristic.heart_rate_measurement.xml
        /// This function throws an exception if the data cannot be parsed.
        /// </summary>
        /// <param name="data">Raw data received from the heart rate monitor.</param>
        /// <returns>The heart rate measurement value.</returns>
        private static double ConvertTemperatureData(byte[] temperatureData)
        {
            // Read temperature data in IEEE 11703 floating point format 
            // temperatureData[0] contains flags about optional data - not used 
            uint mantissa = ((uint)temperatureData[3] << 16) | ((uint)temperatureData[2] << 8) | ((uint)temperatureData[1]);


            return mantissa * Math.Pow(10.0, 1);
        }
        #endregion
        class DBConnectHeartRate
        {

            private MySqlConnection connection;
            private string server;
            private string database;
            private string uid;
            private string password;

            //Constructor
            public DBConnectHeartRate()
            {
                Initialize();
            }

            //Initialize values
            public void Initialize()
            {
                server = "192.168.100.7";
                database = "hr";
                uid = "104360082";
                password = "104360082";
                string connectionString;
                connectionString = "SERVER=" + server + ";" + "DATABASE=" +
                database + ";" + "UID=" + uid + ";" + "PASSWORD=" + password + ";" + "SslMode=None;" + "charset=utf8";

                connection = new MySqlConnection(connectionString);
            }

            //open connection to database
            public bool OpenConnection()
            {
                try
                {
                    connection.Open();
                    return true;
                }
                catch (MySqlException ex)
                {
                    //When handling errors, you can your application's response based 
                    //on the error number.
                    //The two most common error numbers when connecting are as follows:
                    //0: Cannot connect to server.
                    //1045: Invalid user name and/or password.
                    switch (ex.Number)
                    {
                        case 0:
                            //MessageBox.Show("Cannot connect to server.  Contact administrator");
                            break;

                        case 1045:
                            //MessageBox.Show("Invalid username/password, please try again");
                            break;
                    }
                    return false;
                }
            }

            //Close connection
            public bool CloseConnection()
            {
                try
                {
                    connection.Close();
                    return true;
                }
                catch (MySqlException ex)
                {
                    //MessageBox.Show(ex.Message);
                    return false;
                }
            }

            //Insert statement
            public void Insert(string Time, string mysqlHR)
            {

                string query = "INSERT INTO hr (Time,HR) VALUES('" + Time + "', '" + mysqlHR + "')";

                //open connection
                if (this.OpenConnection() == true)
                {
                    //create command and assign the query and connection from the constructor
                    MySqlCommand cmd = new MySqlCommand(query, connection);

                    //Execute command
                    cmd.ExecuteNonQuery();

                    //close connection
                    this.CloseConnection();
                }
            }

            //Update statement
            public void Update()
            {
                string query = "UPDATE tableinfo SET name='Joe', age='22' WHERE name='John Smith'";

                //Open connection
                if (this.OpenConnection() == true)
                {
                    //create mysql command
                    MySqlCommand cmd = new MySqlCommand();
                    //Assign the query using CommandText
                    cmd.CommandText = query;
                    //Assign the connection using Connection
                    cmd.Connection = connection;

                    //Execute query
                    cmd.ExecuteNonQuery();

                    //close connection
                    this.CloseConnection();
                }
            }

            //Delete statement
            /*public void Delete()
            {
                string query = "DELETE FROM tableinfo WHERE name='" + + "'";

                if (this.OpenConnection() == true)
                {
                    MySqlCommand cmd = new MySqlCommand(query, connection);
                    cmd.ExecuteNonQuery();
                    this.CloseConnection();
                }
            }*/

            //Select statement
            public List<string>[] Select()
            {
                string query = "SELECT * FROM tableinfo";

                //Create a list to store the result
                List<string>[] list = new List<string>[3];
                list[0] = new List<string>();
                list[1] = new List<string>();
                list[2] = new List<string>();

                //Open connection
                if (this.OpenConnection() == true)
                {
                    //Create Command
                    MySqlCommand cmd = new MySqlCommand(query, connection);
                    //Create a data reader and Execute the command
                    MySqlDataReader dataReader = cmd.ExecuteReader();

                    //Read the data and store them in the list
                    while (dataReader.Read())
                    {
                        list[0].Add(dataReader["id"] + "");
                        list[1].Add(dataReader["name"] + "");
                        list[2].Add(dataReader["age"] + "");
                    }

                    //close Data Reader
                    dataReader.Close();

                    //close Connection
                    this.CloseConnection();

                    //return list to be displayed
                    return list;
                }
                else
                {
                    return list;
                }
            }

            //Count statement
            public int Count()
            {
                string query = "SELECT Count(*) FROM tableinfo";
                int Count = -1;

                //Open Connection
                if (this.OpenConnection() == true)
                {
                    //Create Mysql Command
                    MySqlCommand cmd = new MySqlCommand(query, connection);

                    //ExecuteScalar will return one value
                    Count = int.Parse(cmd.ExecuteScalar() + "");

                    //close Connection
                    this.CloseConnection();

                    return Count;
                }
                else
                {
                    return Count;
                }
            }
        }


        class DBConnectTemp
        {

            private MySqlConnection connection;
            private string server;
            private string database;
            private string uid;
            private string password;

            //Constructor
            public DBConnectTemp()
            {
                Initialize();
            }

            //Initialize values
            public void Initialize()
            {
                server = "192.168.100.7";
                database = "temperature";
                uid = "104360082";
                password = "104360082";
                string connectionString;
                connectionString = "SERVER=" + server + ";" + "DATABASE=" +
                database + ";" + "UID=" + uid + ";" + "PASSWORD=" + password + ";" + "SslMode=None;" + "charset=utf8";

                connection = new MySqlConnection(connectionString);
            }

            //open connection to database
            public bool OpenConnection()
            {
                try
                {
                    connection.Open();
                    return true;
                }
                catch (MySqlException ex)
                {
                    //When handling errors, you can your application's response based 
                    //on the error number.
                    //The two most common error numbers when connecting are as follows:
                    //0: Cannot connect to server.
                    //1045: Invalid user name and/or password.
                    switch (ex.Number)
                    {
                        case 0:
                            //MessageBox.Show("Cannot connect to server.  Contact administrator");
                            break;

                        case 1045:
                            //MessageBox.Show("Invalid username/password, please try again");
                            break;
                    }
                    return false;
                }
            }

            //Close connection
            public bool CloseConnection()
            {
                try
                {
                    connection.Close();
                    return true;
                }
                catch (MySqlException ex)
                {
                    //MessageBox.Show(ex.Message);
                    return false;
                }
            }

            //Insert statement
            public void Insert(string Time, string mysqlTemp, string mysqlTempEnvir, string mysqlTempFace)
            {

                string query = "INSERT INTO temperature (Time,Temp,TempEnvir,TempFace) VALUES('" + Time + "', '" + mysqlTemp + "', '" + mysqlTempEnvir + "', '" + mysqlTempFace + "')";

                //open connection
                if (this.OpenConnection() == true)
                {
                    //create command and assign the query and connection from the constructor
                    MySqlCommand cmd = new MySqlCommand(query, connection);

                    //Execute command
                    cmd.ExecuteNonQuery();

                    //close connection
                    this.CloseConnection();
                }
            }

            //Update statement
            public void Update()
            {
                string query = "UPDATE tableinfo SET name='Joe', age='22' WHERE name='John Smith'";

                //Open connection
                if (this.OpenConnection() == true)
                {
                    //create mysql command
                    MySqlCommand cmd = new MySqlCommand();
                    //Assign the query using CommandText
                    cmd.CommandText = query;
                    //Assign the connection using Connection
                    cmd.Connection = connection;

                    //Execute query
                    cmd.ExecuteNonQuery();

                    //close connection
                    this.CloseConnection();
                }
            }

            //Delete statement
            /*public void Delete()
            {
                string query = "DELETE FROM tableinfo WHERE name='" + + "'";

                if (this.OpenConnection() == true)
                {
                    MySqlCommand cmd = new MySqlCommand(query, connection);
                    cmd.ExecuteNonQuery();
                    this.CloseConnection();
                }
            }*/

            //Select statement
            public List<string>[] Select()
            {
                string query = "SELECT * FROM tableinfo";

                //Create a list to store the result
                List<string>[] list = new List<string>[3];
                list[0] = new List<string>();
                list[1] = new List<string>();
                list[2] = new List<string>();

                //Open connection
                if (this.OpenConnection() == true)
                {
                    //Create Command
                    MySqlCommand cmd = new MySqlCommand(query, connection);
                    //Create a data reader and Execute the command
                    MySqlDataReader dataReader = cmd.ExecuteReader();

                    //Read the data and store them in the list
                    while (dataReader.Read())
                    {
                        list[0].Add(dataReader["id"] + "");
                        list[1].Add(dataReader["name"] + "");
                        list[2].Add(dataReader["age"] + "");
                    }

                    //close Data Reader
                    dataReader.Close();

                    //close Connection
                    this.CloseConnection();

                    //return list to be displayed
                    return list;
                }
                else
                {
                    return list;
                }
            }

            //Count statement
            public int Count()
            {
                string query = "SELECT Count(*) FROM tableinfo";
                int Count = -1;

                //Open Connection
                if (this.OpenConnection() == true)
                {
                    //Create Mysql Command
                    MySqlCommand cmd = new MySqlCommand(query, connection);

                    //ExecuteScalar will return one value
                    Count = int.Parse(cmd.ExecuteScalar() + "");

                    //close Connection
                    this.CloseConnection();

                    return Count;
                }
                else
                {
                    return Count;
                }
            }
        }
        class DBConnectWarning
        {

            private MySqlConnection connection;
            private string server;
            private string database;
            private string uid;
            private string password;

            //Constructor
            public DBConnectWarning()
            {
                Initialize();
            }

            //Initialize values
            public void Initialize()
            {
                server = "192.168.100.7";
                database = "babywarning";
                uid = "104360082";
                password = "104360082";
                string connectionString;
                connectionString = "SERVER=" + server + ";" + "DATABASE=" +
                database + ";" + "UID=" + uid + ";" + "PASSWORD=" + password + ";" + "SslMode=None;" + "charset=utf8";

                connection = new MySqlConnection(connectionString);
            }

            //open connection to database
            public bool OpenConnection()
            {
                try
                {
                    connection.Open();
                    return true;
                }
                catch (MySqlException ex)
                {
                    //When handling errors, you can your application's response based 
                    //on the error number.
                    //The two most common error numbers when connecting are as follows:
                    //0: Cannot connect to server.
                    //1045: Invalid user name and/or password.
                    switch (ex.Number)
                    {
                        case 0:
                            //MessageBox.Show("Cannot connect to server.  Contact administrator");
                            break;

                        case 1045:
                            //MessageBox.Show("Invalid username/password, please try again");
                            break;
                    }
                    return false;
                }
            }

            //Close connection
            public bool CloseConnection()
            {
                try
                {
                    connection.Close();
                    return true;
                }
                catch (MySqlException ex)
                {
                    //MessageBox.Show(ex.Message);
                    return false;
                }
            }

            //Insert statement
            public void Insert(string Time, string mysqlTemp)
            {

                string query = "INSERT INTO babywarning (Time,warning) VALUES('" + Time + "', '" + mysqlTemp + "')";

                //open connection
                if (this.OpenConnection() == true)
                {
                    //create command and assign the query and connection from the constructor
                    MySqlCommand cmd = new MySqlCommand(query, connection);

                    //Execute command
                    cmd.ExecuteNonQuery();

                    //close connection
                    this.CloseConnection();
                }
            }

            //Update statement
            public void Update()
            {
                string query = "UPDATE tableinfo SET name='Joe', age='22' WHERE name='John Smith'";

                //Open connection
                if (this.OpenConnection() == true)
                {
                    //create mysql command
                    MySqlCommand cmd = new MySqlCommand();
                    //Assign the query using CommandText
                    cmd.CommandText = query;
                    //Assign the connection using Connection
                    cmd.Connection = connection;

                    //Execute query
                    cmd.ExecuteNonQuery();

                    //close connection
                    this.CloseConnection();
                }
            }

            //Delete statement
            /*public void Delete()
            {
                string query = "DELETE FROM tableinfo WHERE name='" + + "'";

                if (this.OpenConnection() == true)
                {
                    MySqlCommand cmd = new MySqlCommand(query, connection);
                    cmd.ExecuteNonQuery();
                    this.CloseConnection();
                }
            }*/

            //Select statement
            public List<string>[] Select()
            {
                string query = "SELECT * FROM tableinfo";

                //Create a list to store the result
                List<string>[] list = new List<string>[3];
                list[0] = new List<string>();
                list[1] = new List<string>();
                list[2] = new List<string>();

                //Open connection
                if (this.OpenConnection() == true)
                {
                    //Create Command
                    MySqlCommand cmd = new MySqlCommand(query, connection);
                    //Create a data reader and Execute the command
                    MySqlDataReader dataReader = cmd.ExecuteReader();

                    //Read the data and store them in the list
                    while (dataReader.Read())
                    {
                        list[0].Add(dataReader["id"] + "");
                        list[1].Add(dataReader["name"] + "");
                        list[2].Add(dataReader["age"] + "");
                    }

                    //close Data Reader
                    dataReader.Close();

                    //close Connection
                    this.CloseConnection();

                    //return list to be displayed
                    return list;
                }
                else
                {
                    return list;
                }
            }

            //Count statement
            public int Count()
            {
                string query = "SELECT Count(*) FROM tableinfo";
                int Count = -1;

                //Open Connection
                if (this.OpenConnection() == true)
                {
                    //Create Mysql Command
                    MySqlCommand cmd = new MySqlCommand(query, connection);

                    //ExecuteScalar will return one value
                    Count = int.Parse(cmd.ExecuteScalar() + "");

                    //close Connection
                    this.CloseConnection();

                    return Count;
                }
                else
                {
                    return Count;
                }
            }
        }


    }
}