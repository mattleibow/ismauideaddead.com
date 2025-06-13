using System.Net;
using HtmlAgilityPack;

namespace IsMauiDeadDead.Services;

public interface IStatusService
{
    Task<SiteStatus> CheckSiteStatusAsync(string url);
    Task<SiteStatus> CheckSiteWithDataStreamsAsync(string mainUrl, Dictionary<string, string> dataStreamUrls);
    Task<SiteStatus> CheckSiteWithDiscoveredUrlsAsync(string mainUrl);
}

public class StatusService : IStatusService
{
    private readonly HttpClient _httpClient;

    public StatusService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
    }

    public async Task<SiteStatus> CheckSiteWithDataStreamsAsync(string mainUrl, Dictionary<string, string> dataStreamUrls)
    {
        var mainStatus = await CheckSiteStatusAsync(mainUrl);
        var dataStreams = new List<EndpointStatus>();
        
        // If main site is offline, return offline status immediately
        if (!mainStatus.IsOnline)
        {
            return new SiteStatus
            {
                IsOnline = false,
                StatusCode = mainStatus.StatusCode,
                ResponseTime = mainStatus.ResponseTime,
                LastChecked = mainStatus.LastChecked,
                ErrorMessage = mainStatus.ErrorMessage,
                DataStreams = dataStreams,
                OverallStatus = SiteHealthStatus.OFFLINE
            };
        }

        // Check all data streams in parallel
        var dataStreamTasks = dataStreamUrls.Select(async kvp =>
        {
            var startTime = DateTime.UtcNow;
            try
            {
                var response = await _httpClient.GetAsync(kvp.Value);
                var responseTime = DateTime.UtcNow - startTime;
                
                return new EndpointStatus
                {
                    Name = kvp.Key,
                    Url = kvp.Value,
                    IsHealthy = response.IsSuccessStatusCode,
                    StatusCode = (int)response.StatusCode,
                    ResponseTime = responseTime,
                    ErrorMessage = response.IsSuccessStatusCode ? null : $"HTTP {response.StatusCode}"
                };
            }
            catch (Exception ex)
            {
                return new EndpointStatus
                {
                    Name = kvp.Key,
                    Url = kvp.Value,
                    IsHealthy = false,
                    StatusCode = 0,
                    ResponseTime = DateTime.UtcNow - startTime,
                    ErrorMessage = ex.Message.Contains("CORS") || ex.Message.Contains("cors") ? 
                        "CORS restriction" : 
                        "Connection error"
                };
            }
        });

        dataStreams = (await Task.WhenAll(dataStreamTasks)).ToList();
        
        // Determine overall status
        var overallStatus = DetermineOverallStatus(mainStatus.IsOnline, dataStreams);
        
        return new SiteStatus
        {
            IsOnline = mainStatus.IsOnline,
            StatusCode = mainStatus.StatusCode,
            ResponseTime = mainStatus.ResponseTime,
            LastChecked = DateTime.UtcNow,
            ErrorMessage = overallStatus == SiteHealthStatus.DOWN ? "Some data streams are experiencing issues" : mainStatus.ErrorMessage,
            DataStreams = dataStreams,
            OverallStatus = overallStatus
        };
    }

    public async Task<SiteStatus> CheckSiteWithDiscoveredUrlsAsync(string mainUrl)
    {
        var mainStatus = await CheckSiteStatusAsync(mainUrl);
        var dataStreams = new List<EndpointStatus>();
        
        // If main site is offline, return offline status immediately
        if (!mainStatus.IsOnline)
        {
            return new SiteStatus
            {
                IsOnline = false,
                StatusCode = mainStatus.StatusCode,
                ResponseTime = mainStatus.ResponseTime,
                LastChecked = mainStatus.LastChecked,
                ErrorMessage = mainStatus.ErrorMessage,
                DataStreams = dataStreams,
                OverallStatus = SiteHealthStatus.OFFLINE
            };
        }

        // Get the homepage content and extract URLs
        var discoveredUrls = await DiscoverUrlsFromHomepageAsync(mainUrl);
        
        // Check all discovered URLs in parallel
        var dataStreamTasks = discoveredUrls.Select(async kvp =>
        {
            var startTime = DateTime.UtcNow;
            try
            {
                var response = await _httpClient.GetAsync(kvp.Value);
                var responseTime = DateTime.UtcNow - startTime;
                
                return new EndpointStatus
                {
                    Name = kvp.Key,
                    Url = kvp.Value,
                    IsHealthy = response.IsSuccessStatusCode,
                    StatusCode = (int)response.StatusCode,
                    ResponseTime = responseTime,
                    ErrorMessage = response.IsSuccessStatusCode ? null : $"HTTP {response.StatusCode}"
                };
            }
            catch (Exception ex)
            {
                return new EndpointStatus
                {
                    Name = kvp.Key,
                    Url = kvp.Value,
                    IsHealthy = false,
                    StatusCode = 0,
                    ResponseTime = DateTime.UtcNow - startTime,
                    ErrorMessage = ex.Message.Contains("CORS") || ex.Message.Contains("cors") ? 
                        "CORS restriction" : 
                        "Connection error"
                };
            }
        });

        dataStreams = (await Task.WhenAll(dataStreamTasks)).ToList();
        
        // Determine overall status
        var overallStatus = DetermineOverallStatus(mainStatus.IsOnline, dataStreams);
        
        return new SiteStatus
        {
            IsOnline = mainStatus.IsOnline,
            StatusCode = mainStatus.StatusCode,
            ResponseTime = mainStatus.ResponseTime,
            LastChecked = DateTime.UtcNow,
            ErrorMessage = overallStatus == SiteHealthStatus.DOWN ? "Some URLs are experiencing issues" : mainStatus.ErrorMessage,
            DataStreams = dataStreams,
            OverallStatus = overallStatus
        };
    }

    private async Task<Dictionary<string, string>> DiscoverUrlsFromHomepageAsync(string mainUrl)
    {
        try
        {
            var response = await _httpClient.GetStringAsync(mainUrl);
            var doc = new HtmlDocument();
            doc.LoadHtml(response);

            var discoveredUrls = new Dictionary<string, string>();

            // Find all anchor tags with href attributes
            var anchorNodes = doc.DocumentNode.SelectNodes("//a[@href]");
            if (anchorNodes != null)
            {
                foreach (var anchor in anchorNodes)
                {
                    var href = anchor.GetAttributeValue("href", "");
                    var linkText = anchor.InnerText?.Trim() ?? "";

                    if (string.IsNullOrEmpty(href) || string.IsNullOrEmpty(linkText))
                        continue;

                    // Convert relative URLs to absolute URLs
                    if (Uri.TryCreate(new Uri(mainUrl), href, out var absoluteUri))
                    {
                        var absoluteUrl = absoluteUri.ToString();
                        
                        // Filter out internal anchors, mailto, tel, javascript, etc.
                        if (absoluteUrl.StartsWith("http://") || absoluteUrl.StartsWith("https://"))
                        {
                            // Skip the main site URL itself to avoid redundancy
                            if (!absoluteUrl.Equals(mainUrl, StringComparison.OrdinalIgnoreCase) && 
                                !absoluteUrl.Equals(mainUrl.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
                            {
                                // Use link text as name, but truncate if too long
                                var name = linkText.Length > 50 ? linkText.Substring(0, 47) + "..." : linkText;
                                
                                // Avoid duplicate names by appending a counter
                                var uniqueName = name;
                                int counter = 1;
                                while (discoveredUrls.ContainsKey(uniqueName))
                                {
                                    uniqueName = $"{name} ({counter++})";
                                }
                                
                                discoveredUrls[uniqueName] = absoluteUrl;
                            }
                        }
                    }
                }
            }

            return discoveredUrls;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error discovering URLs from homepage: {ex.Message}");
            return new Dictionary<string, string>();
        }
    }

    private static SiteHealthStatus DetermineOverallStatus(bool mainSiteOnline, List<EndpointStatus> dataStreams)
    {
        if (!mainSiteOnline)
            return SiteHealthStatus.OFFLINE;
            
        if (dataStreams.All(ds => ds.IsHealthy))
            return SiteHealthStatus.UP;
            
        return SiteHealthStatus.DOWN;
    }

    public async Task<SiteStatus> CheckSiteStatusAsync(string url)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            var response = await _httpClient.GetAsync(url);
            var responseTime = DateTime.UtcNow - startTime;
            
            return new SiteStatus
            {
                IsOnline = response.IsSuccessStatusCode,
                StatusCode = (int)response.StatusCode,
                ResponseTime = responseTime,
                LastChecked = DateTime.UtcNow,
                ErrorMessage = response.IsSuccessStatusCode ? null : $"HTTP {response.StatusCode}"
            };
        }
        catch (HttpRequestException ex)
        {
            return new SiteStatus
            {
                IsOnline = false,
                StatusCode = 0,
                ResponseTime = DateTime.UtcNow - startTime,
                LastChecked = DateTime.UtcNow,
                ErrorMessage = ex.Message.Contains("CORS") || ex.Message.Contains("cors") ? 
                    "CORS restriction - please check manually" : 
                    "Network error"
            };
        }
        catch (TaskCanceledException)
        {
            return new SiteStatus
            {
                IsOnline = false,
                StatusCode = 408,
                ResponseTime = DateTime.UtcNow - startTime,
                LastChecked = DateTime.UtcNow,
                ErrorMessage = "Request timeout"
            };
        }
        catch (Exception ex)
        {
            return new SiteStatus
            {
                IsOnline = false,
                StatusCode = 0,
                ResponseTime = DateTime.UtcNow - startTime,
                LastChecked = DateTime.UtcNow,
                ErrorMessage = ex.Message.Contains("CORS") || ex.Message.Contains("cors") ? 
                    "CORS restriction - please check manually" : 
                    "Unknown error"
            };
        }
    }
}

public class SiteStatus
{
    public bool IsOnline { get; set; }
    public int StatusCode { get; set; }
    public TimeSpan ResponseTime { get; set; }
    public DateTime LastChecked { get; set; }
    public string? ErrorMessage { get; set; }
    public List<EndpointStatus> DataStreams { get; set; } = new();
    public SiteHealthStatus OverallStatus { get; set; } = SiteHealthStatus.OFFLINE;
}

public class EndpointStatus
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public bool IsHealthy { get; set; }
    public int StatusCode { get; set; }
    public TimeSpan ResponseTime { get; set; }
    public string? ErrorMessage { get; set; }
}

public enum SiteHealthStatus
{
    UP,      // Site and all URLs working
    DOWN,    // Site is up but some URLs have issues
    OFFLINE  // Site not working at all
}