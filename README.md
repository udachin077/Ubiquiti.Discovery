<div align="center">

# Ubiquiti.Discovery

A lightweight .NET library that discovers **Ubiquiti** devices on the local network via UDP broadcast (port 10001).

**[English](README.md)** · **[Русский](README.ru.md)**

[![NuGet](https://img.shields.io/nuget/v/Ubiquiti.Discovery?style=flat-square&logo=nuget&label=Ubiquiti.Discovery)](https://www.nuget.org/packages/Ubiquiti.Discovery)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Ubiquiti.Discovery?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/Ubiquiti.Discovery)
![.NET](https://img.shields.io/badge/.NET-10-512BD4?style=flat-square&logo=dotnet&logoColor=white)
![License](https://img.shields.io/badge/License-MIT-green?style=flat-square)

</div>

---

## Installation

```bash
dotnet add package Ubiquiti.Discovery
```

or add a project reference:

```bash
dotnet add reference path/to/Ubiquiti.Discovery.csproj
```

## Usage

### Without DI

```csharp
using Microsoft.Extensions.Options;
using Ubiquiti.Discovery.Options;
using Ubiquiti.Discovery.Services;

var service = new UbiquitiDiscoveryService(Options.Create(new UbiquitiDiscoveryOptions()));

await foreach (var device in service.DiscoverDevicesAsync())
{
    Console.WriteLine($"{device.IpAddress} — {device.Model} ({device.MacAddress})");
}
```

### With Dependency Injection

```csharp
using Microsoft.Extensions.DependencyInjection;
using Ubiquiti.Discovery.Abstractions;
using Ubiquiti.Discovery.Extensions;

var services = new ServiceCollection();

services.AddUbiquitiDiscovery();

// or
//
// services.AddUbiquitiDiscovery(options =>
// {
//     options.DiscoveryPort = 10001;        // default
//     options.IdleTimeoutSeconds = 1.5;     // default
//     options.TimeoutSeconds = 10;          // default
// });

var provider = services.BuildServiceProvider();
var discovery = provider.GetRequiredService<IUbiquitiDiscoveryService>();

await foreach (var device in discovery.DiscoverDevicesAsync())
{
    Console.WriteLine(device);
}
```

### Collect all devices into a list

```csharp
IReadOnlyList<DeviceInfo> devices = await discovery.DiscoverAllAsync();
```

### Cancellation

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

await foreach (var device in discovery.DiscoverDevicesAsync(cts.Token))
{
    // handle the device
}
```

### Device properties

`IpAddress`, `MacAddress`, `Hostname`, `Model`, `Platform`, `Firmware`, `Uptime`, `WebUiPort`, `Status`, `ESSID`, plus raw TLV records in `Records`.

Formatting helpers:

```csharp
device.ToUptimeDisplay();   // "3d 04:12:07"
device.ToFirmwareDisplay(); // "v6.3.23 (XM)"
```
