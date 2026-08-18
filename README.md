# Windows Laptop Info Reader

A small read-only Python command-line tool that collects local Windows laptop information:

- machine manufacturer, model, BIOS serial number, and BIOS version
- Windows version, activation status, partial product key, firmware product key, and decoded installed product key when Windows exposes them
- CPU details
- physical memory module details
- graphics adapter details
- battery details
- screen/monitor details

> The program must be run on Windows. It uses PowerShell CIM/WMI queries and Windows registry reads, so it will exit with an explanatory error on macOS or Linux.

## Requirements

- Windows 10/11
- Python 3.9+
- PowerShell available on `PATH`

## Usage

```powershell
python system_info_reader.py --pretty
```

Save the collected JSON to a file:

```powershell
python system_info_reader.py --pretty --output laptop-info.json
```

## Privacy note

The output can contain sensitive identifiers such as serial numbers and Windows product keys. Store and share the generated JSON carefully.
