using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using LibreHardwareMonitor.Hardware;

namespace FanControlUI
{
    public class FanNode : INotifyPropertyChanged
    {
        public ISensor? Sensor { get; set; }
        public HardwareType ParentType { get; set; }

        private string _name = string.Empty;
        public string Name 
        { 
            get => _name; 
            set { _name = value; OnPropertyChanged(nameof(Name)); } 
        }

        private bool _isAuto = true;
        public bool IsAuto 
        { 
            get => _isAuto; 
            set 
            { 
                _isAuto = value; 
                OnPropertyChanged(nameof(IsAuto)); 
                OnPropertyChanged(nameof(IsManual)); 
            } 
        }

        public bool IsManual => !_isAuto;

        private float _targetSpeed;
        public float TargetSpeed 
        {
            get => _targetSpeed;
            set 
            {
                _targetSpeed = value;
                OnPropertyChanged(nameof(TargetSpeed));
                OnPropertyChanged(nameof(SpeedText));
                
                // Only command hardware directly from the slider if we are in Manual mode
                if (IsManual && Sensor != null) 
                {
                    Sensor.Control.SetSoftware(value);
                }
            }
        }

        public string SpeedText => $"{Math.Round(_targetSpeed, 1)} %";

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public partial class MainWindow : Window
    {
        private Computer? _computer;
        private List<ISensor> _cpuTemperatureSensors = new List<ISensor>();
        private List<ISensor> _gpuTemperatureSensors = new List<ISensor>();
        
        
        private ObservableCollection<FanNode> _fanNodes = new ObservableCollection<FanNode>();
        private CancellationTokenSource? _cancellationTokenSource;

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
            ControllersList.ItemsSource = _fanNodes; // Bind the data to the UI
            this.Loaded += MainWindow_Loaded;
            this.Closing += MainWindow_Closing;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeUniversalHardware();
            
            if (_fanNodes.Count > 0)
            {
                _cancellationTokenSource = new CancellationTokenSource();
                Task.Run(() => HardwareMonitoringLoop(_cancellationTokenSource.Token));
            }
            else
            {
                MessageBox.Show("No fan controllers detected. Pre-built OEM locks may be active.", "Sensor Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void InitializeUniversalHardware()
        {
            _computer = new Computer 
            { 
                IsCpuEnabled = true, 
                IsMotherboardEnabled = true, 
                IsControllerEnabled = true, // Required to catch external USB fan hubs
                IsGpuEnabled = true 
            };
            
            _computer.Open();
            _computer.Accept(new UpdateVisitor()); 

            foreach (IHardware hardware in _computer.Hardware)
            {
                // Hunt CPU Temps
                if (hardware.HardwareType == HardwareType.Cpu)
                {
                    foreach (ISensor sensor in hardware.Sensors)
                        if (sensor.SensorType == SensorType.Temperature) _cpuTemperatureSensors.Add(sensor);
                }

                // Hunt GPU Temps
                if (hardware.HardwareType == HardwareType.GpuNvidia || hardware.HardwareType == HardwareType.GpuAmd)
                {
                    foreach (ISensor sensor in hardware.Sensors)
                        if (sensor.SensorType == SensorType.Temperature) _gpuTemperatureSensors.Add(sensor);
                }

                // Hunt Root Fan Controllers (GPU and External Hubs)
                foreach (ISensor sensor in hardware.Sensors)
                {
                    if (sensor.SensorType == SensorType.Control)
                        _fanNodes.Add(new FanNode { Sensor = sensor, Name = $"{hardware.Name} - {sensor.Name}", ParentType = hardware.HardwareType });
                }

                // Hunt Sub-Hardware Fan Controllers (Motherboard Super I/O)
                foreach (IHardware subHardware in hardware.SubHardware)
                {
                    foreach (ISensor sensor in subHardware.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Control)
                            _fanNodes.Add(new FanNode { Sensor = sensor, Name = $"{subHardware.Name} - {sensor.Name}", ParentType = hardware.HardwareType });
                    }
                }
            }
        }

        private async Task HardwareMonitoringLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                _computer?.Accept(new UpdateVisitor());

                float currentCpuTemp = GetMaxTemp(_cpuTemperatureSensors);
                float currentGpuTemp = GetMaxTemp(_gpuTemperatureSensors);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    CpuTempText.Text = $"{Math.Round(currentCpuTemp, 1)} °C";
                    GpuTempText.Text = $"{Math.Round(currentGpuTemp, 1)} °C";
                });

                foreach (var fan in _fanNodes)
                {
                    if (fan.IsAuto && fan.Sensor != null)
                    {
                        // Thermal Decoupling: Route GPU temps to GPU fans, CPU temps to everything else
                        float referenceTemp = (fan.ParentType == HardwareType.GpuNvidia || fan.ParentType == HardwareType.GpuAmd) ? currentGpuTemp : currentCpuTemp;
                        
                        float targetSpeed = CalculateFanSpeed(referenceTemp);

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            fan.TargetSpeed = targetSpeed;
                        });

                        fan.Sensor.Control.SetSoftware(targetSpeed);
                    }
                }

                await Task.Delay(2000, token);
            }
        }

        private float GetMaxTemp(List<ISensor> sensors)
        {
            var validTemps = sensors.Where(s => s.Value.HasValue).Select(s => s.Value.GetValueOrDefault()).ToList();
            return validTemps.Any() ? validTemps.Max() : 0f;
        }

        private float CalculateFanSpeed(float temperature)
        {
            if (temperature < 40f) return 30f;
            if (temperature > 85f) return 100f;
            return 30f + ((temperature - 40f) / (85f - 40f)) * (100f - 30f);
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            foreach (var fan in _fanNodes)
            {
                fan.Sensor?.Control.SetDefault();
            }
            
            _cancellationTokenSource?.Cancel();
            _computer?.Close();
        }
    }
}