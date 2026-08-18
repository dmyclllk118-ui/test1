#!/usr/bin/env python3
"""Collect Windows laptop hardware and activation information.

This script is intentionally read-only. It uses PowerShell/CIM/WMI queries and
registry reads to gather a laptop's serial number, memory, CPU, GPU, battery,
display, and Windows activation/product-key information.
"""
from __future__ import annotations

import argparse
import json
import os
import platform
import subprocess
import sys
from typing import Any


def run_powershell(script: str) -> Any:
    """Run a PowerShell snippet and return parsed JSON output."""
    completed = subprocess.run(
        [
            "powershell",
            "-NoProfile",
            "-ExecutionPolicy",
            "Bypass",
            "-Command",
            script,
        ],
        check=False,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
    )
    if completed.returncode != 0:
        raise RuntimeError(completed.stderr.strip() or "PowerShell command failed")

    output = completed.stdout.strip()
    if not output:
        return None
    return json.loads(output)


def normalize(value: Any) -> Any:
    """Recursively replace empty strings with None for cleaner output."""
    if isinstance(value, dict):
        return {key: normalize(item) for key, item in value.items()}
    if isinstance(value, list):
        return [normalize(item) for item in value]
    if isinstance(value, str):
        stripped = value.strip()
        return stripped or None
    return value


POWERSHELL_COLLECTOR = r'''
function ConvertTo-WindowsProductKey {
    param([byte[]]$DigitalProductId)

    if (-not $DigitalProductId -or $DigitalProductId.Length -lt 67) {
        return $null
    }

    $keyOffset = 52
    $chars = "BCDFGHJKMPQRTVWXY2346789"
    $key = ""
    $isWin8OrLater = [math]::Floor($DigitalProductId[66] / 6) -band 1
    $DigitalProductId[66] = ($DigitalProductId[66] -band 0xF7) -bor (($isWin8OrLater -band 2) * 4)

    for ($i = 24; $i -ge 0; $i--) {
        $current = 0
        for ($j = 14; $j -ge 0; $j--) {
            $current = ($current * 256) -bxor $DigitalProductId[$j + $keyOffset]
            $DigitalProductId[$j + $keyOffset] = [math]::Floor($current / 24)
            $current = $current % 24
        }
        $key = $chars[$current] + $key
    }

    if ($isWin8OrLater -eq 1) {
        $first = $key.Substring(1, $current)
        $last = $key.Substring($current + 1, $key.Length - ($current + 1))
        $key = $first + "N" + $last
    }

    return (($key -split '(.{5})' | Where-Object { $_ }) -join '-').Trim('-')
}

function ConvertFrom-WmiStringArray {
    param($Value)
    if (-not $Value) { return $null }
    return (($Value | ForEach-Object { [char]$_ }) -join '').Trim([char]0).Trim()
}

$computerSystem = Get-CimInstance Win32_ComputerSystem
$bios = Get-CimInstance Win32_BIOS
$os = Get-CimInstance Win32_OperatingSystem
$cpu = Get-CimInstance Win32_Processor
$memoryModules = Get-CimInstance Win32_PhysicalMemory
$gpus = Get-CimInstance Win32_VideoController
$batteries = Get-CimInstance Win32_Battery -ErrorAction SilentlyContinue
$desktopMonitors = Get-CimInstance Win32_DesktopMonitor -ErrorAction SilentlyContinue
$monitorIds = Get-CimInstance -Namespace root\wmi -ClassName WmiMonitorID -ErrorAction SilentlyContinue
$activation = Get-CimInstance SoftwareLicensingProduct -Filter "PartialProductKey IS NOT NULL AND LicenseStatus = 1" -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -match 'Windows' } |
    Select-Object -First 1

$digitalProductId = $null
$decodedProductKey = $null
try {
    $digitalProductId = (Get-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion' -Name DigitalProductId -ErrorAction Stop).DigitalProductId
    $decodedProductKey = ConvertTo-WindowsProductKey -DigitalProductId ([byte[]]$digitalProductId.Clone())
} catch {}

$oa3Key = $null
try {
    $oa3Key = (Get-CimInstance SoftwareLicensingService).OA3xOriginalProductKey
} catch {}

$result = [ordered]@{
    generatedAt = (Get-Date).ToString('o')
    computer = [ordered]@{
        manufacturer = $computerSystem.Manufacturer
        model = $computerSystem.Model
        name = $computerSystem.Name
        systemType = $computerSystem.SystemType
        serialNumber = $bios.SerialNumber
        biosVersion = ($bios.SMBIOSBIOSVersion -join ', ')
    }
    windows = [ordered]@{
        caption = $os.Caption
        version = $os.Version
        buildNumber = $os.BuildNumber
        architecture = $os.OSArchitecture
        activationStatus = if ($activation) { 'Licensed' } else { 'Unknown or not licensed' }
        productName = $activation.Name
        partialProductKey = $activation.PartialProductKey
        productKeyFromFirmware = $oa3Key
        decodedInstalledProductKey = $decodedProductKey
    }
    cpu = @($cpu | ForEach-Object {
        [ordered]@{
            name = $_.Name
            manufacturer = $_.Manufacturer
            cores = $_.NumberOfCores
            logicalProcessors = $_.NumberOfLogicalProcessors
            maxClockMHz = $_.MaxClockSpeed
            processorId = $_.ProcessorId
        }
    })
    memory = [ordered]@{
        totalPhysicalGB = [math]::Round($computerSystem.TotalPhysicalMemory / 1GB, 2)
        modules = @($memoryModules | ForEach-Object {
            [ordered]@{
                manufacturer = $_.Manufacturer
                partNumber = $_.PartNumber
                serialNumber = $_.SerialNumber
                capacityGB = [math]::Round($_.Capacity / 1GB, 2)
                speedMHz = $_.Speed
                configuredClockSpeedMHz = $_.ConfiguredClockSpeed
                bankLabel = $_.BankLabel
                deviceLocator = $_.DeviceLocator
            }
        })
    }
    graphics = @($gpus | ForEach-Object {
        [ordered]@{
            name = $_.Name
            adapterRAMGB = if ($_.AdapterRAM) { [math]::Round($_.AdapterRAM / 1GB, 2) } else { $null }
            driverVersion = $_.DriverVersion
            videoModeDescription = $_.VideoModeDescription
            currentHorizontalResolution = $_.CurrentHorizontalResolution
            currentVerticalResolution = $_.CurrentVerticalResolution
            currentRefreshRate = $_.CurrentRefreshRate
        }
    })
    battery = @($batteries | ForEach-Object {
        [ordered]@{
            name = $_.Name
            status = $_.Status
            estimatedChargeRemainingPercent = $_.EstimatedChargeRemaining
            estimatedRunTimeMinutes = $_.EstimatedRunTime
            chemistry = $_.Chemistry
            designCapacity = $_.DesignCapacity
            fullChargeCapacity = $_.FullChargeCapacity
        }
    })
    screens = [ordered]@{
        desktopMonitors = @($desktopMonitors | ForEach-Object {
            [ordered]@{
                name = $_.Name
                screenWidth = $_.ScreenWidth
                screenHeight = $_.ScreenHeight
                status = $_.Status
                pnpDeviceId = $_.PNPDeviceID
            }
        })
        monitorIds = @($monitorIds | ForEach-Object {
            [ordered]@{
                manufacturerName = ConvertFrom-WmiStringArray $_.ManufacturerName
                productCodeId = ConvertFrom-WmiStringArray $_.ProductCodeID
                serialNumberId = ConvertFrom-WmiStringArray $_.SerialNumberID
                userFriendlyName = ConvertFrom-WmiStringArray $_.UserFriendlyName
                yearOfManufacture = $_.YearOfManufacture
                weekOfManufacture = $_.WeekOfManufacture
                active = $_.Active
            }
        })
    }
}

$result | ConvertTo-Json -Depth 8
'''


def collect_windows_info() -> dict[str, Any]:
    if platform.system().lower() != "windows":
        raise OSError("This collector must be run on Windows because it uses CIM/WMI and the Windows registry.")
    return normalize(run_powershell(POWERSHELL_COLLECTOR))


def main() -> int:
    parser = argparse.ArgumentParser(description="Read local Windows laptop hardware and activation information.")
    parser.add_argument("-o", "--output", help="Optional path to save JSON output.")
    parser.add_argument("--pretty", action="store_true", help="Pretty-print JSON with indentation.")
    args = parser.parse_args()

    try:
        data = collect_windows_info()
    except Exception as exc:  # keep CLI user-friendly while preserving non-zero exit
        print(f"Error: {exc}", file=sys.stderr)
        return 1

    json_text = json.dumps(data, ensure_ascii=False, indent=2 if args.pretty else None)
    print(json_text)

    if args.output:
        with open(args.output, "w", encoding="utf-8") as file:
            file.write(json_text + os.linesep)

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
