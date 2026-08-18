using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using NotebookInfo.Core.Models;
using NotebookInfo.Core.Services;

namespace NotebookInfo.App.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly IComputerInfoService computerInfoService;
    private readonly ICpuInfoService cpuInfoService;
    private readonly IMemoryInfoService memoryInfoService;
    private readonly IGpuInfoService gpuInfoService;
    private readonly IDisplayInfoService displayInfoService;
    private bool isScanning;
    private ComputerInfo computer = ComputerInfo.Unknown;
    private MemoryInfo memory = MemoryInfo.Unknown;

    public MainViewModel() : this(CreateDefaultServices()) { }

    public MainViewModel((IComputerInfoService Computer, ICpuInfoService Cpu, IMemoryInfoService Memory, IGpuInfoService Gpu, IDisplayInfoService Display) services)
    {
        computerInfoService = services.Computer;
        cpuInfoService = services.Cpu;
        memoryInfoService = services.Memory;
        gpuInfoService = services.Gpu;
        displayInfoService = services.Display;
        ScanCommand = new RelayCommand(ScanAsync, () => !IsScanning);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public ICommand ScanCommand { get; }
    public ObservableCollection<CpuInfo> Cpus { get; } = new();
    public ObservableCollection<MemoryModuleInfo> MemoryModules { get; } = new();
    public ObservableCollection<GpuInfo> Gpus { get; } = new();
    public ObservableCollection<DisplayInfo> Displays { get; } = new();

    public ComputerInfo Computer { get => computer; private set => SetField(ref computer, value); }
    public MemoryInfo Memory { get => memory; private set { SetField(ref memory, value); OnPropertyChanged(nameof(TotalMemory)); } }
    public string TotalMemory => Memory.DisplayTotalCapacity;
    public bool IsScanning { get => isScanning; private set { SetField(ref isScanning, value); ((RelayCommand)ScanCommand).RaiseCanExecuteChanged(); } }

    public async Task ScanAsync()
    {
        IsScanning = true;
        try
        {
            var computerTask = computerInfoService.GetComputerInfoAsync();
            var cpuTask = cpuInfoService.GetCpuInfoAsync();
            var memoryTask = memoryInfoService.GetMemoryInfoAsync();
            var gpuTask = gpuInfoService.GetGpuInfoAsync();
            var displayTask = displayInfoService.GetDisplayInfoAsync();
            await Task.WhenAll(computerTask, cpuTask, memoryTask, gpuTask, displayTask);

            Computer = await computerTask;
            Replace(Cpus, await cpuTask);
            Memory = await memoryTask;
            Replace(MemoryModules, Memory.Modules);
            Replace(Gpus, await gpuTask);
            Replace(Displays, await displayTask);
        }
        finally
        {
            IsScanning = false;
        }
    }

    private static (IComputerInfoService Computer, ICpuInfoService Cpu, IMemoryInfoService Memory, IGpuInfoService Gpu, IDisplayInfoService Display) CreateDefaultServices()
    {
        var wmi = new WmiQueryService();
        return (new ComputerInfoService(wmi), new CpuInfoService(wmi), new MemoryInfoService(wmi), new GpuInfoService(wmi), new DisplayInfoService());
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> values)
    {
        target.Clear();
        foreach (var value in values) target.Add(value);
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        OnPropertyChanged(propertyName);
    }

    private void OnPropertyChanged(string? propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
