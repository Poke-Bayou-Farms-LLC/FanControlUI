using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using LibreHardwareMonitor.Hardware;

namespace FanControlUI
{
    public partial class MainWindow : Window
    {
        private Computer? _computer;
        private List<ISensor> _cpuTemperatureSensors = new List<ISensor>();
        private List<ISensor> _allFanControllers = new List<ISensor>();
        private CancellationTokenSource? _cancellationTokenSource;
        
        // Critical State Machine Flag
        private bool _isAutoMode = true;

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
            InitializeUniversalHardware();
            
            if (_cpuTemperatureSensors.Count > 0 && _allFanControllers.Count > 0)
            {
                _cancellationTokenSource = new CancellationTokenSource();
                Task.Run(() => HardwareMonitoringLoop(_cancellationTokenSource.Token));
            }
            else
            {
                string errorMsg = $"Initialization Diagnostics:\nCPU Temp Sensors Found: {_cpuTemperatureSensors.Count}\nFan Controllers Found: {_allFanControllers.Count}\n\nApp will run, but hardware control is unavailable.";
                MessageBox.Show(errorMsg, "Sensor Mapping Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                
                // Keep UI alive but disable controls if no hardware is found
                ModeToggleButton.IsEnabled = false;
                ManualSpeedSlider.IsEnabled = false;
            }
        }

        private void InitializeUniversalHardware()
        {
            _computer = new Computer 
            { 
                IsCpuEnabled = true, 
                IsMotherboardEnabled = true, 
                IsControllerEnabled = true,
                IsGpuEnabled = true 
            };
            
            _computer.Open();
            _computer.Accept(new UpdateVisitor()); 

            foreach (IHardware hardware in _computer.Hardware)
            {
                if (hardware.HardwareType == HardwareType.Cpu)
                {
                    foreach (ISensor sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Temperature)
                            _cpuTemperatureSensors.Add(sensor);
                    }
                }

                foreach (ISensor sensor in hardware.Sensors)
                {
                    if (sensor.SensorType == SensorType.Control)
                        _allFanControllers.Add(sensor);
                }

                foreach (IHardware subHardware in hardware.SubHardware)
                {
                    foreach (ISensor sensor in subHardware.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Control)
                            _allFanControllers.Add(sensor);
                    }
                }
            }
        }

        private async Task HardwareMonitoringLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                _computer?.Accept(new UpdateVisitor());

                float currentTemp = 0f;
                var validTemps = _cpuTemperatureSensors.Where(s => s.Value.HasValue).Select(s => s.Value.Value).ToList();
                
                if (validTemps.Any())
                {
                    currentTemp = validTemps.Max();
                }

                // UI Update for Temperature is always active
                Application.Current.Dispatcher.Invoke(() =>
                {
                    CpuTempText.Text = $"{Math.Round(currentTemp, 1)} °C";
                });

                // Hardware writes are locked behind the Auto mode flag
                if (_isAutoMode)
                {
                    float targetFanSpeed = CalculateFanSpeed(currentTemp);

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        FanSpeedText.Text = $"{Math.Round(targetFanSpeed, 1)} %";
                    });

                    ApplyFanSpeedToHardware(targetFanSpeed);
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

        private void ApplyFanSpeedToHardware(float speed)
        {
            foreach (var controller in _allFanControllers)
            {
                controller.Control.SetSoftware(speed);
            }
        }

        // --- UI Event Handlers ---

        private void ModeToggleButton_Click(object sender, RoutedEventArgs e)
        {
            _isAutoMode = !_isAutoMode; // Flip the state

            if (_isAutoMode)
            {
                ModeToggleButton.Content = "Mode: AUTO (Dynamic Curve)";
                ModeToggleButton.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(51, 51, 51)); // Dark Gray
                ManualSpeedSlider.IsEnabled = false;
            }
            else
            {
                ModeToggleButton.Content = "Mode: MANUAL (Slider Override)";
                ModeToggleButton.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(165, 42, 42)); // Brown/Red alert color
                ManualSpeedSlider.IsEnabled = true;
                
                // Instantly apply whatever value the slider is currently sitting at
                float sliderValue = (float)ManualSpeedSlider.Value;
                FanSpeedText.Text = $"{sliderValue} % (Manual)";
                ApplyFanSpeedToHardware(sliderValue);
            }
        }

        private void ManualSpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Only push to hardware if we are actually in manual mode
            if (!_isAutoMode)
            {
                float newSpeed = (float)e.NewValue;
                FanSpeedText.Text = $"{newSpeed} % (Manual)";
                ApplyFanSpeedToHardware(newSpeed);
            }
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            foreach (var controller in _allFanControllers)
            {
                controller.Control.SetDefault();
            }
            
            _cancellationTokenSource?.Cancel();
            _computer?.Close();
        }
    }
}