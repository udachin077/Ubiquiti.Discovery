# Ubiquiti.Discovery

[English](#english) | [Русский](#русский)

---

<a name="english"></a>

# English

A lightweight .NET library that discovers **Ubiquiti** devices on the local network via UDP broadcast (port 10001).

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

---

<a name="русский"></a>

# Русский

Лёгкая .NET-библиотека для поиска устройств **Ubiquiti** в локальной сети через UDP-broadcast (порт 10001).

## Установка

```bash
dotnet add package Ubiquiti.Discovery
```

или ссылка на проект:

```bash
dotnet add reference path/to/Ubiquiti.Discovery.csproj
```

## Использование

### Без DI

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

### С Dependency Injection

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

### Сбор всех устройств в список

```csharp
IReadOnlyList<DeviceInfo> devices = await discovery.DiscoverAllAsync();
```

### Отмена поиска

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

await foreach (var device in discovery.DiscoverDevicesAsync(cts.Token))
{
    // обработка устройства
}
```

### Свойства устройства

`IpAddress`, `MacAddress`, `Hostname`, `Model`, `Platform`, `Firmware`, `Uptime`, `WebUiPort`, `Status`, `ESSID`, а также «сырые» TLV-записи в `Records`.

Помощники форматирования:

```csharp
device.ToUptimeDisplay();   // "3d 04:12:07"
device.ToFirmwareDisplay(); // "v6.3.23 (XM)"
```
