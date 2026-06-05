using System.Globalization;
using System.IO.Ports;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;
using YMMKeyboardPlugin.Hid;
using YMMKeyboardPlugin.Key;
using YMMKeyboardPlugin.Settings;

namespace YMMKeyboardPlugin.Diagnostics;

public static class PluginConnectionDiagnosticCollector
{
    private const ushort FormalVendorId = 0x2E8A;
    private const ushort FormalProductId = 0x4020;
    private const string FormalManufacturer = "YMMKeyboard";
    private const ushort FormalUsagePage = 0xFF00;
    private const ushort FormalUsage = 0x0001;
    private static readonly string[] FormalProductNames =
    [
        "YMMKeyboard RP2040",
        "YMM Control HID",
    ];

    public static PluginConnectionDiagnosticReport Capture(string scanMode)
    {
        var settings = YMMKeyboardSettings.Current;
        var hidEnumeration = HidDeviceProbe.EnumerateAllWithDiagnostics();
        var hidDevices = hidEnumeration.EnumeratedDevices;
        var comPorts = EnumerateComPorts();
        var candidates = new List<ConnectionCandidateDiagnostic>();
        var warnings = new List<string>();
        var errors = new List<string>();

        try
        {
            candidates.AddRange(BuildHidCandidates(hidDevices, settings));
            candidates.AddRange(BuildComCandidates(comPorts));
            candidates.AddRange(BuildSerialCandidates(comPorts, settings));
        }
        catch (Exception ex)
        {
            errors.Add($"{ex.GetType().Name}: {ex.Message}");
        }

        var selected = candidates
            .Where(c => c.Selected)
            .OrderByDescending(c => c.MatchScore)
            .FirstOrDefault();

        var rejected = candidates.Where(c => !c.Selected).ToList();

        if (hidEnumeration.TotalDeviceCount == 0)
            warnings.Add("No HID devices detected.");
        else if (hidDevices.Count == 0)
            warnings.Add("No HID devices matched after raw enumeration.");

        var problematicHidDevices = hidEnumeration.FailedCount + hidEnumeration.PartialCount + hidEnumeration.SkippedCount;
        if (problematicHidDevices > 0)
            warnings.Add($"HID raw enumeration had {problematicHidDevices} problematic device(s).");

        if (comPorts.Count == 0)
            warnings.Add("No Legacy COM ports detected.");
        if (selected is null)
            warnings.Add("No candidate was selected.");

        return new PluginConnectionDiagnosticReport
        {
            GeneratedAt = DateTimeOffset.Now,
            AppVersion = GetAppVersion(),
            PluginVersion = GetPluginVersion(),
            YmmVersion = GetYmmVersion(),
            MachineName = Environment.MachineName,
            OsVersion = RuntimeInformation.OSDescription,
            ScanMode = scanMode,
            ConfiguredDeviceIdentity = new ConfiguredDeviceIdentity
            {
                ConnectionMode = settings.ConnectionMode.ToString(),
                HidVendorId = settings.HidVendorIdHex,
                HidProductId = settings.HidProductIdHex,
                HidProductNameFilter = settings.HidProductNameFilter,
                HidManufacturerFilter = settings.HidManufacturerFilter,
                PortName = settings.PortName,
                StartupPortNames = settings.GetStartupPortNames().ToList(),
            },
            RawHidEnumeration = hidEnumeration,
            DetectedHidDevices = hidDevices.Select(d => new DetectedHidDeviceDiagnostic
            {
                Vid = d.VendorId,
                Pid = d.ProductId,
                ProductName = d.ProductName,
                Manufacturer = d.Manufacturer,
                Serial = d.SerialNumber,
                UsagePage = d.UsagePage,
                Usage = d.Usage,
                MaxInputReportLength = d.MaxInputReportLength,
                MaxOutputReportLength = d.MaxOutputReportLength,
                IdentityKind = ClassifyHidIdentity(d),
            }).ToList(),
            DetectedComPorts = comPorts.ToList(),
            ConnectionCandidates = candidates,
            SelectedCandidate = selected,
            RejectedCandidates = rejected,
            Warnings = warnings,
            Errors = errors,
        };
    }

    private static List<string> EnumerateComPorts()
    {
        try
        {
            var ports = SerialPort.GetPortNames();
            if (ports.Length > 0)
                return ports.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();

            return EnumerateComPortsFromRegistry();
        }
        catch (PlatformNotSupportedException)
        {
            return EnumerateComPortsFromRegistry();
        }
        catch
        {
            return EnumerateComPortsFromRegistry();
        }
    }

    private static List<string> EnumerateComPortsFromRegistry()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DEVICEMAP\SERIALCOMM");
            if (key is null)
                return [];

            return key.GetValueNames()
                .Select(name => key.GetValue(name)?.ToString())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private static IEnumerable<ConnectionCandidateDiagnostic> BuildHidCandidates(
        IReadOnlyList<HidDeviceInfo> devices,
        YMMKeyboardSettings settings)
    {
        var explicitVendor = settings.GetHidVendorId();
        var explicitProduct = settings.GetHidProductId();
        var useImplicitFilter = !explicitVendor.HasValue
            && !explicitProduct.HasValue
            && string.IsNullOrWhiteSpace(settings.HidProductNameFilter)
            && string.IsNullOrWhiteSpace(settings.HidManufacturerFilter);

        var result = new List<ConnectionCandidateDiagnostic>();
        foreach (var device in devices)
        {
            var matchReasons = new List<string>();
            var rejectReasons = new List<string>();
            var score = 0;

            if (settings.ConnectionMode == ConnectionMode.Hid)
                matchReasons.Add("connectionMode=HID");

            if (explicitVendor.HasValue)
            {
                if (device.VendorId == explicitVendor.Value)
                {
                    score += 3000;
                    matchReasons.Add($"vid={device.VendorId:X4}");
                }
                else
                {
                    rejectReasons.Add($"vidMismatch expected={explicitVendor.Value:X4} actual={device.VendorId:X4}");
                }
            }

            if (explicitProduct.HasValue)
            {
                if (device.ProductId == explicitProduct.Value)
                {
                    score += 3000;
                    matchReasons.Add($"pid={device.ProductId:X4}");
                }
                else
                {
                    rejectReasons.Add($"pidMismatch expected={explicitProduct.Value:X4} actual={device.ProductId:X4}");
                }
            }

            if (!string.IsNullOrWhiteSpace(settings.HidProductNameFilter))
            {
                if (device.ProductName.Contains(settings.HidProductNameFilter, StringComparison.OrdinalIgnoreCase))
                {
                    score += 1200;
                    matchReasons.Add($"productName~{settings.HidProductNameFilter}");
                }
                else
                {
                    rejectReasons.Add($"productNameMismatch filter={settings.HidProductNameFilter}");
                }
            }

            if (!string.IsNullOrWhiteSpace(settings.HidManufacturerFilter))
            {
                if (device.Manufacturer.Contains(settings.HidManufacturerFilter, StringComparison.OrdinalIgnoreCase))
                {
                    score += 1200;
                    matchReasons.Add($"manufacturer~{settings.HidManufacturerFilter}");
                }
                else
                {
                    rejectReasons.Add($"manufacturerMismatch filter={settings.HidManufacturerFilter}");
                }
            }

            if (useImplicitFilter)
            {
                if (LooksLikeFormalYmmDevice(device))
                {
                    score += 2500;
                    matchReasons.Add("implicitFormalIdentityHeuristic");
                }
                else
                {
                    rejectReasons.Add("implicitFormalIdentityHeuristicMismatch");
                }
            }

            if (device.UsagePage == FormalUsagePage && device.Usage == FormalUsage)
            {
                score += 5000;
                matchReasons.Add("usagePage=FF00 usage=0001");
            }

            if (device.MaxOutputReportLength > 0)
                score += 100;
            if (device.MaxInputReportLength > 0)
                score += device.MaxInputReportLength;

            var selected = settings.ConnectionMode == ConnectionMode.Hid && rejectReasons.Count == 0;
            if (!selected)
            {
                if (settings.ConnectionMode != ConnectionMode.Hid)
                    rejectReasons.Add("legacySerialFallbackSelected");
                if (rejectReasons.Count == 0)
                    rejectReasons.Add("notSelected");
            }

            result.Add(new ConnectionCandidateDiagnostic
            {
                TransportType = "HID",
                Vid = device.VendorId,
                Pid = device.ProductId,
                ProductName = device.ProductName,
                Manufacturer = device.Manufacturer,
                Serial = device.SerialNumber,
                UsagePage = device.UsagePage,
                Usage = device.Usage,
                MatchScore = score,
                MatchReasons = matchReasons,
                RejectReasons = rejectReasons,
                Selected = selected,
            });
        }

        return result;
    }

    private static IEnumerable<ConnectionCandidateDiagnostic> BuildComCandidates(IReadOnlyList<string> ports)
    {
        foreach (var port in ports)
        {
            yield return new ConnectionCandidateDiagnostic
            {
                TransportType = "COM",
                ComPort = port,
                MatchScore = 10,
                MatchReasons = ["detectedComPort"],
                RejectReasons = ["legacySerialDiagnosticOnly"],
                Selected = false,
            };
        }
    }

    private static IEnumerable<ConnectionCandidateDiagnostic> BuildSerialCandidates(
        IReadOnlyList<string> ports,
        YMMKeyboardSettings settings)
    {
        var selectedPorts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var startupPort in settings.GetStartupPortNames())
            selectedPorts.Add(startupPort);
        if (!string.IsNullOrWhiteSpace(settings.PortName))
            selectedPorts.Add(settings.PortName);

        foreach (var port in selectedPorts)
        {
            var detected = ports.Contains(port, StringComparer.OrdinalIgnoreCase);
            var reasons = new List<string>();
            var rejects = new List<string>();

            if (settings.ConnectionMode != ConnectionMode.Com)
                rejects.Add("legacySerialFallbackDisabled");
            else
                reasons.Add("legacySerialFallbackEnabled");

            if (settings.PortName.Equals(port, StringComparison.OrdinalIgnoreCase))
                reasons.Add("configuredPort");
            else if (settings.GetStartupPortNames().Contains(port, StringComparer.OrdinalIgnoreCase))
                reasons.Add("startupPort");

            if (!detected)
                rejects.Add("notEnumerated");

            var selected = settings.ConnectionMode == ConnectionMode.Com && detected && rejects.Count == 0;
            if (!selected && rejects.Count == 0)
                rejects.Add("notSelected");

            yield return new ConnectionCandidateDiagnostic
            {
                TransportType = "Serial",
                ComPort = port,
                MatchScore = selected ? 1000 : 0,
                MatchReasons = reasons,
                RejectReasons = rejects,
                Selected = selected,
            };
        }
    }

    private static bool LooksLikeFormalYmmDevice(HidDeviceInfo device)
    {
        return device.VendorId == FormalVendorId
            && device.ProductId == FormalProductId
            && string.Equals(device.Manufacturer, FormalManufacturer, StringComparison.OrdinalIgnoreCase)
            && (HasKnownFormalProductName(device.ProductName)
                || (device.UsagePage == FormalUsagePage && device.Usage == FormalUsage));
    }

    private static string ClassifyHidIdentity(HidDeviceInfo device)
    {
        if (!LooksLikeFormalYmmDevice(device))
            return "other";

        if (HasKnownFormalProductName(device.ProductName)
            && device.UsagePage == FormalUsagePage
            && device.Usage == FormalUsage)
            return "formal";

        if (HasKnownFormalProductName(device.ProductName))
            return "formal-observed-product";

        if (device.UsagePage == FormalUsagePage && device.Usage == FormalUsage)
            return "formal-usage";

        return "formal";
    }

    private static bool HasKnownFormalProductName(string productName)
    {
        return FormalProductNames.Any(name => string.Equals(productName, name, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetAppVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "unknown";
    }

    private static string GetPluginVersion()
    {
        var assembly = typeof(YMMKeyboardPlugin.Plugin.MyToolPlugin).Assembly;
        return assembly.GetName().Version?.ToString()
            ?? "unknown";
    }

    private static string GetYmmVersion()
    {
        var assembly = typeof(YukkuriMovieMaker.Plugin.IToolPlugin).Assembly;
        return assembly.GetName().Version?.ToString()
            ?? "unknown";
    }
}
