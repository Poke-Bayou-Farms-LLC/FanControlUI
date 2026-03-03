using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using LibreHardwareMonitor.Hardware;

namespace FanControlUI
{
    public partial class MainWindow : Window
    {
        private Computer _computer;
        private ISensor _cpuTempSensor;
        private ISensor _caseFanControl;
        private CancellationTokenSource _cancellationTokenSource;

        public class UpdateVisitor : IVisitor
        {
            public void VisitComputer(IComputer computer) => computer.Traverse(this);
            public void VisitHardware(IHardware hardware)
            {
                hardware.Update();
                foreach (IHardware sub in hardware.SubHardware) sub.Accept(this);
            }
            public void VisitSensor(ISensor sensor) { }
            public void VisitParameter(IParameter parameter) { }
        }

        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += MainWindow_Loaded;
            this.Closing += MainWindow_Closing;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeHardware();
            
            if (_cpuTempSensor != null && _caseFanControl != null)
            {
                _cancellationTokenSource = new CancellationTokenSource();
                Task.Run(() => HardwareMonitoringLoop(_cancellationTokenSource.Token));
            }
            else
            {
                MessageBox.Show("Failed to detect sensors. The app will now exit. Ensure you run this as Administrator.", "Hardware Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
            }
        }

        private void InitializeHardware()
        {
            _computer = new Computer { IsCpuEnabled = true, IsMotherboardEnabled = true, IsControllerEnabled = true };
            _computer.Open();
            _computer.Accept(new UpdateVisitor());

            foreach (IHardware hardware in _computer.Hardware)
            {
                if (hardware.HardwareType == HardwareType.Cpu)
                {
                    foreach (ISensor sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Temperature && sensor.Name.Contains("Package"))
                            _cpuTempSensor = sensor;
                    }
                }

                if (hardware.HardwareType == HardwareType.Motherboard)
                {
                    foreach (IHardware subHardware in hardware.SubHardware)
                    {
                        foreach (ISensor sensor in subHardware.Sensors)
                        {
                            if (sensor.SensorType == SensorType.Control)
                            {
                                // Grabs the first fan controller found for demonstration
                                _caseFanControl = sensor;
                            }
                        }
                    }
                }
            }
        }

        private async Task HardwareMonitoringLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                _computer.Accept(new UpdateVisitor());

                float currentTemp = _cpuTempSensor.Value ?? 0f;
                float targetFanSpeed = CalculateFanSpeed(currentTemp);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    CpuTempText.Text = $"{Math.Round(currentTemp, 1)} °C";
                    FanSpeedText.Text = $"{Math.Round(targetFanSpeed, 1)} %";
                });

                // Safety check to ensure we don't push null values to the hardware
                if (_caseFanControl != null)
                {
                    _caseFanControl.Control.SetSoftware(targetFanSpeed);
                }

                await Task.Delay(2000, token);
            }
        }

        private float CalculateFanSpeed(float temperature)
        {
            if (temperature < 40f) return 30f;
            if (temperature > 85f) return 100f;
            return 30f + ((temperature - 40f) / (85f - 40f)) * (100f - 30f);
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_caseFanControl != null)
            {
                _caseFanControl.Control.SetDefault();
            }
            
            _cancellationTokenSource?.Cancel();
            _computer?.Close();
        }
    }
}