using Meshtastic.Connections;
using Meshtastic.Protobufs;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Xml.Linq;

namespace Meshtastic.Discovery;

public class DeviceDiscovery(ILogger<DeviceDiscovery>? logger = null)
{
    private readonly ILogger<DeviceDiscovery>? _logger = logger;
    
    // Known Meshtastic vendor IDs
    private static readonly HashSet<string> MeshtasticVendorIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "239A",
        "10C4",
        "1A86",
        "0403",
        "2E8A",
        "2886",
    };

    public IEnumerable<MeshtasticDevice> DiscoverUsbDevices()
    {
		return [];
    }

    private static string? FindLinuxSerialPort(string deviceDir, string[] availablePorts)
    {
        try
        {
            // Look for tty subdirectories
            var ttyDirs = Directory.GetDirectories(deviceDir, "*tty*", SearchOption.AllDirectories);
            foreach (var ttyDir in ttyDirs)
            {
                var ttyName = Path.GetFileName(ttyDir);
                var portPath = $"/dev/{ttyName}";
                if (availablePorts.Contains(portPath))
                {
                    return portPath;
                }
            }
        }
        catch
        {
            // Ignore errors
        }

        return null;
    }

    private static string TryReadFile(string filePath)
    {
        try
        {
            return File.Exists(filePath) ? File.ReadAllText(filePath).Trim() : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private void ExtractUsbDevicesFromItems(XElement itemsElement, List<(string name, string vendorId, string productId, string? bsdName, string? manufacturer)> devices)
    {
        foreach (var dictElement in itemsElement.Elements("dict"))
        {
            var name = FindKeyValue(dictElement, "_name") as string;
            var vendorId = FindKeyValue(dictElement, "vendor_id") as string;
            var productId = FindKeyValue(dictElement, "product_id") as string;
            var bsdName = FindKeyValue(dictElement, "bsd_name") as string;
            var manufacturer = FindKeyValue(dictElement, "manufacturer") as string;
            
            if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(vendorId) && !string.IsNullOrEmpty(productId))
            {
                devices.Add((name, vendorId, productId, bsdName, manufacturer));
            }
            
            // Recursively check nested _items arrays
            var nestedItemsArray = FindKeyValue(dictElement, "_items") as XElement;
            if (nestedItemsArray != null)
            {
                ExtractUsbDevicesFromItems(nestedItemsArray, devices);
            }
        }
    }

    private static object? FindKeyValue(XElement dictElement, string key)
    {
        var elements = dictElement.Elements().ToList();
        
        for (int i = 0; i < elements.Count - 1; i++)
        {
            if (elements[i].Name == "key" && elements[i].Value == key)
            {
                var valueElement = elements[i + 1];
                if (valueElement.Name == "string")
                {
                    return valueElement.Value;
                }
                else if (valueElement.Name == "array")
                {
                    return valueElement;
                }
            }
        }
        
        return null;
    }

    private static bool IsKnownVendor(string vendorId)
    {
        // Remove 0x prefix if present and check if it's in our known vendor list
        var cleanVendorId = vendorId.Replace("0x", "").Replace("0X", "");
        return MeshtasticVendorIds.Any(v => cleanVendorId.Contains(v, StringComparison.OrdinalIgnoreCase));
    }
}
