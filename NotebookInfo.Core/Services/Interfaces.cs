using NotebookInfo.Core.Models;

namespace NotebookInfo.Core.Services;

public interface IComputerInfoService { Task<ComputerInfo> GetComputerInfoAsync(CancellationToken cancellationToken = default); }
public interface ICpuInfoService { Task<IReadOnlyList<CpuInfo>> GetCpuInfoAsync(CancellationToken cancellationToken = default); }
public interface IMemoryInfoService { Task<MemoryInfo> GetMemoryInfoAsync(CancellationToken cancellationToken = default); }
public interface IGpuInfoService { Task<IReadOnlyList<GpuInfo>> GetGpuInfoAsync(CancellationToken cancellationToken = default); }
public interface IDisplayInfoService { Task<IReadOnlyList<DisplayInfo>> GetDisplayInfoAsync(CancellationToken cancellationToken = default); }
