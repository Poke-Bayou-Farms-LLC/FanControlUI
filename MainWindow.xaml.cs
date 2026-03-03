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
    public class TelemetryNode : INotifyPropertyChanged
    {
        public ISensor? Sensor { get; set; }
        public string Name { get; set; } = string.Empty;

        private float _value;
        public float Value 
        {
            get => _value;
            set 
            {
                if (_value != value)
                {
                    _value = value;
                    OnPropertyChanged(nameof(ValueText));
                }
            }
        }
        
        public string ValueText => $"{Math.Round(_value, 1)} °C";

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

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
                
                float safeValue = Math.Clamp(value, 15f, 100f);

                if (_targetSpeed != safeValue)
                {
                    _targetSpeed = safeValue;
                    OnPropertyChanged(nameof(TargetSpeed));
                    OnPropertyChanged(nameof(SpeedText));
                    
                    if (IsManual && Sensor != null) 
                    {
                        Sensor.Control.SetSoftware(_targetSpeed);
                    }
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
        private ObservableCollection<TelemetryNode> _telemetryNodes = new ObservableCollection<TelemetryNode>();
        
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
            
            ControllersList.ItemsSource = _fanNodes; 
            TelemetryList.ItemsSource = _telemetryNodes;
            
            this.Loaded += MainWindow_Loaded;
            this.Closing += MainWindow_Closing;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeUniversalHardware();
            
            if (_fanNodes.Count > 0 || _telemetryNodes.Count > 0)
            {
                _cancellationTokenSource = new CancellationTokenSource();
                Task.Run(() => HardwareMonitoringLoop(_cancellationTokenSource.Token));
            }
            else
            {
                MessageBox.Show("No hardware sensors detected. Administrator permissions may be missing.", "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                        {
                            _cpuTemperatureSensors.Add(sensor);
                            
                            if (sensor.Name.Contains("Core #") || sensor.Name.Contains("Tctl") || sensor.Name.Contains("Package") || sensor.Name.Contains("CCD"))
                            {
                                string cleanName = sensor.Name.Replace("CPU ", "");
                                _telemetryNodes.Add(new TelemetryNode { Sensor = sensor, Name = $"CPU {cleanName}" });
                            }
                        }
                    }
                }

                if (hardware.HardwareType == HardwareType.GpuNvidia || hardware.HardwareType == HardwareType.GpuAmd)
                {
                    foreach (ISensor sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Temperature)
                        {
                            _gpuTemperatureSensors.Add(sensor);
                            string cleanName = sensor.Name.Replace("GPU ", "");
                            _telemetryNodes.Add(new TelemetryNode { Sensor = sensor, Name = $"GPU {cleanName}" });
                        }
                    }
                }

                if (hardware.HardwareType == HardwareType.Motherboard || hardware.HardwareType == HardwareType.SuperIO)
                {
                    foreach (ISensor sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Temperature)
                        {
                            _telemetryNodes.Add(new TelemetryNode { Sensor = sensor, Name = $"MB {sensor.Name}" });
                        }
                    }
                    
                    foreach (IHardware subHardware in hardware.SubHardware)
                    {
                        foreach (ISensor sensor in subHardware.Sensors)
                        {
                            if (sensor.SensorType == SensorType.Temperature)
                            {
                                _telemetryNodes.Add(new TelemetryNode { Sensor = sensor, Name = $"SYS {sensor.Name}" });
                            }
                        }
                    }
                }

                foreach (ISensor sensor in hardware.Sensors)
                {
                    if (sensor.SensorType == SensorType.Control)
                        _fanNodes.Add(new FanNode { Sensor = sensor, Name = $"{hardware.Name} - {sensor.Name}", ParentType = hardware.HardwareType });
                }

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

                float currentCpuMax = GetMaxTemp(_cpuTemperatureSensors);
                float currentGpuMax = GetMaxTemp(_gpuTemperatureSensors);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    CpuMaxTempText.Text = $"{Math.Round(currentCpuMax, 1)} °C";
                    GpuMaxTempText.Text = $"{Math.Round(currentGpuMax, 1)} °C";

                    foreach (var node in _telemetryNodes)
                    {
                        if (node.Sensor != null && node.Sensor.Value.HasValue)
                        {
                            node.Value = node.Sensor.Value.GetValueOrDefault();
                        }
                    }
                });

                foreach (var fan in _fanNodes)
                {
                    if (fan.IsAuto && fan.Sensor != null)
                    {
                        float referenceTemp = (fan.ParentType == HardwareType.GpuNvidia || fan.ParentType == HardwareType.GpuAmd) ? currentGpuMax : currentCpuMax;
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