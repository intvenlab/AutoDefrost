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
using System.Windows.Shapes;
using System.Management;
using MeSoft.MeCom.Core;
using MeSoft.MeCom.PhyWrapper;
using System.Timers;
using System.Windows.Threading;
using System.Globalization;
using System.Linq.Expressions;

namespace AutoDefrost
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private MeComBasicCmd meComBasicCmd;
        private string meComPort;
        private HttpServer dpm;
        E5EC controller;

        int TecDeviceStatus;
        float TecObjectTemp;
        float TecSinkTemp;
        float TecTargetTemp;
        float TecCurrent;
        float TecVoltage;
        float TecBoardTemp;
        int TecStablity;

        float TecMaxChange; // The max temp change per second. 
        float TecDesiredTemp;
        bool TecValidSetPoint;
        float ChamberCurrentTemp;
        float ChamberSetPoint;


        public MainWindow()
        {
            InitializeComponent();

            dpm = new HttpServer(5);
            dpm.Port = 8085;
            dpm.Start();
            

            controller = new E5EC();
            if (controller.Connect())
            {
                Console.WriteLine("woo");
                var temp = controller.GetCurrentTemperature();
                controller.SetSetpoint(59);
                var setpoint = controller.GetSetpoint();
                Console.WriteLine($"Current temp: {temp}, setpoint: {setpoint}");

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

        }
        private void SetupTEC()
        {
            foreach (string portName in SerialPort.GetPortNames())
            {
                try
                {
                    MeComPhySerialPort meComPhySerialPort = new MeComPhySerialPort();
                    meComPhySerialPort.OpenWithDefaultSettings(portName, 57600);
                    
                    MeComQuerySet meComQuerySet = new MeComQuerySet(meComPhySerialPort);
                    meComQuerySet.SetDefaultDeviceAddress(0);
                    meComQuerySet.SetIsReady(true);
                    meComBasicCmd = new MeComBasicCmd(meComQuerySet);
                    string identString = meComBasicCmd.GetIdentString(null);
                    Console.WriteLine("Port: " + portName);

                    Console.WriteLine("IF String: " + identString);
                    if (!String.IsNullOrEmpty(identString)) { meComPort = portName; break; }
                }
                catch
                {
                    Console.WriteLine("Port ERROR: " + portName);
                }

            }
            
            // init settings - see see 5136j TEC coms document. 
            meComBasicCmd.SetINT32Value(null, 50010, 1, 1);  // Sine Ramp Start Point
            meComBasicCmd.SetINT32Value(null, 50011, 1, 1);  // Object Target Temperature Source Selection


        }
        void timer_Tick(object sender, EventArgs e)
        {
            ReadTecValues();
            ReadDiffusionValues();
            ReadDPMValues();
            UpdateTargetTempStage();
            UpdateTargetTempChamber();

        }
        void tec_ramper(object sender, EventArgs e)
        {
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
            TimeSpan age = DateTime.Now - dpm.dpm_last_update;
            float ChamberOffset;
            float ChamberManualSetPoint;
            if ((bool)RadioAutomaticDiffusionFromAir.IsChecked)
            {
                if (age.TotalSeconds > 10) { return; }
                try { ChamberOffset = float.Parse(BoxAutomaticOffsetDiffusion.Text); } catch { return; }
                controller.SetSetpoint(dpm.dpm_airtemp + ChamberOffset);

            }
            else if ((bool)RadioAutomaticDiffusionFromDP.IsChecked)
            {
                if (age.TotalSeconds > 10) { return; }
                try { ChamberOffset = float.Parse(BoxAutomaticOffsetDiffusion.Text); } catch { return; }
                controller.SetSetpoint(dpm.dpm_dewpoint + ChamberOffset);

            }

            else // Manual Mode
            {
                try { ChamberManualSetPoint = float.Parse(BoxManualSetpointDiffusion.Text); } catch { return; }
                controller.SetSetpoint(ChamberManualSetPoint);
            }
        }
        private void UpdateTargetTempStage()
        {
            TimeSpan age = DateTime.Now - dpm.dpm_last_update;

            float StageOffset;
            float StageManualSetPoint;

            if ((bool)RadioAutomatic.IsChecked)
            {
                try { StageOffset = float.Parse(BoxAutomaticOffset.Text); } catch { return; }
                if (age.TotalSeconds > 10) { return; }
                if (dpm.dpm_dewpoint + StageOffset > dpm.dpm_airtemp)
                {
                    SetTecDesiredTemp(dpm.dpm_airtemp);
                }
                else 
                {
                    SetTecDesiredTemp(dpm.dpm_dewpoint + StageOffset);
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
            Console.WriteLine("Set TEC temp to: " + target);

            //meComBasicCmd.SetFloatValue(null, 3000, 1, target);
            meComBasicCmd.SetFloatValue(null, 50012, 1, target);

        }
        private void ReadDPMValues()
        {
            TimeSpan age = DateTime.Now - dpm.dpm_last_update;

            if (age.TotalMinutes < 1)
            {
                BoxDpm_dp.Text = dpm.dpm_dewpoint.ToString("F", new CultureInfo("en-US"));
                BoxDpm_airtemp.Text = dpm.dpm_airtemp.ToString("F", new CultureInfo("en-US"));
            } else
            {
                BoxDpm_dp.Text = "NaN";
                BoxDpm_airtemp.Text = "NaN";
            }

        }
        private void ReadDiffusionValues()
        {
            
            ChamberSetPoint = (float)controller.GetSetpoint();
            ChamberCurrentTemp = (float) controller.GetCurrentTemperature();

            BoxDiffusionTargetTemp.Text = ChamberSetPoint.ToString();
            BoxDiffusionTemp.Text = ChamberCurrentTemp.ToString();

        }
        private void ReadTecValues()
        {
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
                Console.WriteLine("Device status: " + TecDeviceStatus + " Object Temp: " + TecObjectTemp + " Amps: " + TecCurrent);
            } 
            catch { Console.WriteLine("Tec protocol error");  }

            BoxTecObjectTemp.Text = TecObjectTemp.ToString("F", new CultureInfo("en-US"));
            BoxTecTargetTemp.Text = TecTargetTemp.ToString("F", new CultureInfo("en-US"));
            BoxTecAmps.Text = TecCurrent.ToString("F", new CultureInfo("en-US"));
            //BoxTecObjectTemp.Text = "blah";


        }
        private void textbox_float_filter(object sender, KeyEventArgs e)
        {
            // allows 0-9, backspace, and decimal
            Console.WriteLine(Convert.ToChar(e.Key));
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
