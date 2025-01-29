using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Management;
using MeSoft.MeCom.Core;
using MeSoft.MeCom.PhyWrapper;
using System.Timers;
using System.Windows.Threading;
using System.Globalization;
using System.Linq.Expressions;
using System.IO;
using NLog;
using System.Reflection;


namespace AutoDefrost
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private FileSystemWatcher watcher; // Class-level variable
        private MeComBasicCmd meComBasicCmd;
        private string meComPort;
        //private HttpServer dpm;
        E5EC controller;
        //PosiTectorDPM dpm;
        PosiTectorDPM dpm;



        int TecDeviceStatus;
        float TecObjectTemp;
        float TecSinkTemp;
        float TecTargetTemp;
        float TecCurrent;
        float TecVoltage;
        float TecBoardTemp;
        int TecStablity;
        bool TecConnected = false;

        float TecMaxChange; // The max temp change per second. 
        float TecDesiredTemp;
        bool TecValidSetPoint;
        float ChamberCurrentTemp;
        float ChamberSetPoint;
        bool ChamberConnected = false;


        

        private static readonly string folderPath = "C:\\sramperlogs"; // Change this to your folder path



        public MainWindow()
        {
            InitializeComponent();
            LogBuildTime();

            //dpm = new HttpServer(5);
            //dpm.Port = 8085;
            //dpm.Start();


            dpm = new PosiTectorDPM();
            dpm.Connect();


            controller = new E5EC();
            if (controller.Connect())
            {
                Logger.Info("connected to E5EC");
                var temp = controller.GetCurrentTemperature();
                var setpoint = controller.GetSetpoint();
                Logger.Info($"Current temp: {temp}, setpoint: {setpoint}");
                ChamberConnected = true;
            }

            SetupTEC();


            DispatcherTimer timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += timer_Tick;
            timer.Start();

            DispatcherTimer timer2 = new DispatcherTimer();
            timer2.Interval = TimeSpan.FromSeconds(1);
            timer2.Tick += tec_ramper;
            timer2.Start();

            InitializeFileWatcher();

        }
        private void LogBuildTime()
        {
            try
            {
                // Get the path of the running executable
                string exePath = Assembly.GetExecutingAssembly().Location;

                // Get the last write time of the executable (build time)
                DateTime buildTime = File.GetLastWriteTime(exePath);


                // Log the build time using NLog
                Logger.Info($"Executable Build Time: {buildTime:yyyy-MM-dd HH:mm:ss}");
                this.Title = $"AutoDefrost build {buildTime:yyyy-MM-dd HH:mm:ss}";
            }
            catch (Exception ex)
            {
                // Log any errors encountered while trying to get the build time
                Logger.Error(ex, "Failed to log the build time of the executable.");
            }
        }

        private void InitializeFileWatcher()
        {
            if (!Directory.Exists(folderPath))
            {
                Logger.Error($"The folder '{folderPath}' does not exist. Please check the path.");
                return;
            }

            watcher = new FileSystemWatcher(folderPath, "*")
            {
                EnableRaisingEvents = true
            };
            watcher.Created += OnNewFileDetected;

            Logger.Info($"Watching folder: {folderPath}");
        }
        private void SetupTEC()
        {
            foreach (string portName in SerialPort.GetPortNames())
            {
                try
                {
                    MeComPhySerialPort meComPhySerialPort = new MeComPhySerialPort
                    {
                        ReadTimeout = 500, 
                        WriteTimeout = 500 
                    };
                    meComPhySerialPort.OpenWithDefaultSettings(portName, 57600);
                    
                    MeComQuerySet meComQuerySet = new MeComQuerySet(meComPhySerialPort);
                    meComQuerySet.SetDefaultDeviceAddress(0);
                    meComQuerySet.SetIsReady(true);
                    meComBasicCmd = new MeComBasicCmd(meComQuerySet);
                    string identString = meComBasicCmd.GetIdentString(null);
                    Logger.Info("TEC: Port: " + portName);

                    Logger.Info("TEC: IF String: " + identString);
                    if (!String.IsNullOrEmpty(identString)) { meComPort = portName; TecConnected = true; break; }
                }
                catch
                {
                    Logger.Info("TEC: Port ERROR: " + portName);
                }

            }

            if (!TecConnected) { return; }
            // init settings - see see 5136j TEC coms document. 
            meComBasicCmd.SetINT32Value(null, 50010, 1, 1);  // Sine Ramp Start Point
            meComBasicCmd.SetINT32Value(null, 50011, 1, 1);  // Object Target Temperature Source Selection


        }
        

        private void OnNewFileDetected(object sender, FileSystemEventArgs e)
        {
            try
            {
                string originalFilePath = e.FullPath;
                string originalFileName = Path.GetFileName(originalFilePath);
                string directory = Path.GetDirectoryName(originalFilePath);
                string originalFileNameWithoutExtension = Path.GetFileNameWithoutExtension(originalFilePath);
                string newFileName = $"{originalFileNameWithoutExtension}-ThermalData.txt";
                string newFilePath = Path.Combine(directory, newFileName);
                if (originalFileName.EndsWith("-ThermalData.txt", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
                string variablesToWrite = "";
                variablesToWrite += $"DPM_dewpoint: {dpm.DewPointTemperature}\r\n";
                variablesToWrite += $"DPM_airtemp: {dpm.AirTemperature}\r\n";
                variablesToWrite += $"TecConnected: {TecConnected}\r\n";

                variablesToWrite += $"TecCurrent: {TecCurrent}\r\n";
                variablesToWrite += $"TecTargetTemp: {TecTargetTemp}\r\n";
                variablesToWrite += $"TecObjectTemp: {TecObjectTemp}\r\n";
                variablesToWrite += $"ChamberConnected: {ChamberConnected}\r\n";
                variablesToWrite += $"ChamberCurrentTemp: {ChamberCurrentTemp}\r\n";
                variablesToWrite += $"ChamberSetPoint: {ChamberSetPoint}\r\n";

                Logger.Info("Shutter detected, adding log file: {newFilePath}");
                Logger.Info(variablesToWrite);



                // Dump variables into the new file
                File.WriteAllText(newFilePath, variablesToWrite);

                Logger.Info($"Processed file: {e.Name}, created: {newFileName}");
            }
            catch (Exception ex)
            {
                Logger.Info($"Error processing file: {ex.Message}");
            }
        }
        void timer_Tick(object sender, EventArgs e)
        {
            dpm.ReadData();
            ReadTecValues();
            ReadChamberValues();
            ReadDPMValues();
            UpdateTargetTempStage();
            UpdateTargetTempChamber();
            UpdateUIControls();

        }
        void UpdateUIControls()
        {
            if (TecConnected)
            {
                RadioAutomatic.IsEnabled = true;
                RadioManual.IsEnabled = true;
                BoxMaxChangePerSecond.IsEnabled = true;
                BoxAutomaticOffset.IsEnabled = true;
                BoxManualSetpoint.IsEnabled = true;
                BoxTecObjectTemp.IsEnabled = true;
                BoxTecTargetTemp.IsEnabled = true;
                BoxTecAmps.IsEnabled = true;
            }
            else
            {
                RadioAutomatic.IsEnabled = false;
                RadioManual.IsEnabled = false;
                BoxMaxChangePerSecond.IsEnabled = false;
                BoxAutomaticOffset.IsEnabled = false;
                BoxManualSetpoint.IsEnabled = false;
                BoxTecObjectTemp.IsEnabled = false;
                BoxTecTargetTemp.IsEnabled = false;
                BoxTecAmps.IsEnabled = false;
            }
            if (ChamberConnected)
            {
                RadioAutomaticChamberFromAir.IsEnabled = true;
                RadioAutomaticChamberFromDP.IsEnabled = true;
                RadioManualChamber.IsEnabled = true;
                BoxAutomaticOffsetChamber.IsEnabled = true;
                BoxManualSetpointChamber.IsEnabled = true;
                BoxChamberTemp.IsEnabled = true;
                BoxChamberTargetTemp.IsEnabled = true;
            }
            else
            {
                RadioAutomaticChamberFromAir.IsEnabled = false;
                RadioAutomaticChamberFromDP.IsEnabled = false;
                RadioManualChamber.IsEnabled = false;
                BoxAutomaticOffsetChamber.IsEnabled = false;
                BoxManualSetpointChamber.IsEnabled = false;
                BoxChamberTemp.IsEnabled = false;
                BoxChamberTargetTemp.IsEnabled = false;
            }
        }
        void tec_ramper(object sender, EventArgs e)
        {
            if (!TecConnected) { return; }
            if (TecValidSetPoint == false) { return; }

            try { TecMaxChange = float.Parse(BoxMaxChangePerSecond.Text); } catch { return;  }
            if (Math.Abs(TecDesiredTemp - TecTargetTemp) < TecMaxChange)
            {
                SetTecTargetTemp(TecDesiredTemp);
            } else if (TecDesiredTemp > TecTargetTemp)
            {
                SetTecTargetTemp(TecTargetTemp + TecMaxChange);
            } else if (TecDesiredTemp < TecTargetTemp)
            {
                SetTecTargetTemp(TecTargetTemp - TecMaxChange);
            } 

        }
        private void UpdateTargetTempChamber()
        {
            if (!ChamberConnected) { return; }
            TimeSpan age = DateTime.Now - dpm.LastUpdate;
            float ChamberOffset; 
            float ChamberManualSetPoint;
            if ((bool)RadioAutomaticChamberFromAir.IsChecked)
            {
                if (age.TotalSeconds > 10) { return; }
                try { ChamberOffset = float.Parse(BoxAutomaticOffsetChamber.Text); } catch { return; }
                controller.SetSetpoint(dpm.AirTemperature + ChamberOffset);

            }
            else if ((bool)RadioAutomaticChamberFromDP.IsChecked)
            {
                if (age.TotalSeconds > 10) { return; }
                try { ChamberOffset = float.Parse(BoxAutomaticOffsetChamber.Text); } catch { return; }
                controller.SetSetpoint(dpm.DewPointTemperature + ChamberOffset);

            }

            else // Manual Mode
            {
                try { ChamberManualSetPoint = float.Parse(BoxManualSetpointChamber.Text); } catch { return; }
                controller.SetSetpoint(ChamberManualSetPoint);
            }
        }
        private void UpdateTargetTempStage()
        {
            if (!TecConnected) { return; }
            TimeSpan age = DateTime.Now - dpm.LastUpdate;

            float StageOffset;
            float StageManualSetPoint;

            if ((bool)RadioAutomatic.IsChecked)
            {
                try { StageOffset = float.Parse(BoxAutomaticOffset.Text); } catch { return; }
                if (age.TotalSeconds > 10) { return; }
                if (dpm.DewPointTemperature  + StageOffset > dpm.AirTemperature)
                {
                    SetTecDesiredTemp((float) (dpm.AirTemperature ));
                }
                else 
                {
                    SetTecDesiredTemp((float)(dpm.DewPointTemperature ) + StageOffset);
                }
            } else // Manual Mode
            {
                try { StageManualSetPoint = float.Parse(BoxManualSetpoint.Text); } catch { return; }
                SetTecDesiredTemp(StageManualSetPoint);
            }



        }
        private void SetTecDesiredTemp(float target)
        {
            TecDesiredTemp = target;
            TecValidSetPoint = true; 
        }
        private void SetTecTargetTemp(float target) {
            if (!TecConnected) { return; }
            Logger.Info("Set TEC temp to: " + target);

            //meComBasicCmd.SetFloatValue(null, 3000, 1, target);
            meComBasicCmd.SetFloatValue(null, 50012, 1, target);

        }
        private void ReadDPMValues()
        {
            TimeSpan age = DateTime.Now - dpm.LastUpdate;

            if (age.TotalSeconds < 30)
            {
                BoxDpm_dp.Text = dpm.DewPointTemperature.ToString("F", new CultureInfo("en-US"));
                BoxDpm_airtemp.Text = dpm.AirTemperature.ToString("F", new CultureInfo("en-US"));
            } else
            {
                BoxDpm_dp.Text = "NaN";
                BoxDpm_airtemp.Text = "NaN";
            }

        }
        private void ReadChamberValues()
        {
            if (!ChamberConnected) { return; }
            ChamberSetPoint = (float)controller.GetSetpoint();
            ChamberCurrentTemp = (float) controller.GetCurrentTemperature();

            BoxChamberTargetTemp.Text = ChamberSetPoint.ToString();
            BoxChamberTemp.Text = ChamberCurrentTemp.ToString();

        }
        private void ReadTecValues()
        {
            if (!TecConnected) { return; }
            try
            {

                TecDeviceStatus = meComBasicCmd.GetINT32Value(null, 104, 1);
                TecObjectTemp = meComBasicCmd.GetFloatValue(null, 1000, 1);
                TecSinkTemp = meComBasicCmd.GetFloatValue(null, 1001, 1);
                TecTargetTemp = meComBasicCmd.GetFloatValue(null, 1010, 1);
                TecCurrent = meComBasicCmd.GetFloatValue(null, 1020, 1);
                TecVoltage = meComBasicCmd.GetFloatValue(null, 1021, 1);
                TecBoardTemp = meComBasicCmd.GetFloatValue(null, 1063, 1);
                TecStablity = meComBasicCmd.GetINT32Value(null, 1200, 1);
                //Logger.Info("Device status: " + TecDeviceStatus + " Object Temp: " + TecObjectTemp + " Amps: " + TecCurrent);
            } 
            catch { Logger.Info("Tec protocol error");  }

            BoxTecObjectTemp.Text = TecObjectTemp.ToString("F", new CultureInfo("en-US"));
            BoxTecTargetTemp.Text = TecTargetTemp.ToString("F", new CultureInfo("en-US"));
            BoxTecAmps.Text = TecCurrent.ToString("F", new CultureInfo("en-US"));
            //BoxTecObjectTemp.Text = "blah";


        }
        private void textbox_float_filter(object sender, KeyEventArgs e)
        {
            // allows 0-9, backspace, and decimal
            Logger.Info(Convert.ToChar(e.Key));
            if (((Convert.ToChar(e.Key) < 48 || Convert.ToChar(e.Key) > 57) && Convert.ToChar(e.Key) != 8 && Convert.ToChar(e.Key) != 46))
            {
                e.Handled = true;
                return;
            }

            // checks to make sure only 1 decimal is allowed
            if (Convert.ToChar(e.Key) == 46)
            {
                if ((sender as TextBox).Text.IndexOf(Convert.ToChar(e.Key)) != -1)
                    e.Handled = true;
            }
        }
    }
}
