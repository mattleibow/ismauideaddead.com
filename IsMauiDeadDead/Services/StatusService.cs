using System.Net;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace IsMauiDeadDead.Services;

public interface IStatusService
{
    Task<SiteStatus> CheckSiteStatusAsync(string mainUrl);
}

public partial class StatusService(HttpClient httpClient) : IStatusService
{
    private readonly HttpClient _httpClient = httpClient;

    // Source-generated regex for better performance in .NET 9
    [GeneratedRegex(@"(?:""|\')([^""\']*\.json[^""\']*?)(?:""|\')", RegexOptions.IgnoreCase)]
    private static partial Regex JsonUrlRegex();

    static StatusService()
    {
        // Configure HttpClient timeout once for all instances
    }

    public async Task<SiteStatus> CheckSiteStatusAsync(string mainUrl)
    {
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
        
        var mainStatus = await CheckMainSiteStatusAsync(mainUrl);
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
                DataStreams = [], // Collection expression (C# 12)
                OverallStatus = SiteHealthStatus.OFFLINE
            };
        }

        // Get the homepage content and extract JSON URLs
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
            ErrorMessage = overallStatus == SiteHealthStatus.DOWN ? "Some JSON data streams are experiencing issues" : mainStatus.ErrorMessage,
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

            // Find all script tags and scan their content for .json URLs
            var scriptNodes = doc.DocumentNode.SelectNodes("//script");
            if (scriptNodes != null)
            {
                foreach (var script in scriptNodes)
                {
                    var scriptContent = script.InnerText ?? script.InnerHtml ?? "";
                    
                    if (string.IsNullOrEmpty(scriptContent))
                        continue;

                    // Use source-generated regex for better performance
                    var jsonUrlMatches = JsonUrlRegex().Matches(scriptContent);

                    foreach (Match match in jsonUrlMatches)
                    {
                        var jsonUrl = match.Groups[1].Value;
                        
                        if (string.IsNullOrEmpty(jsonUrl))
                            continue;

                        // Convert relative URLs to absolute URLs
                        if (Uri.TryCreate(new Uri(mainUrl), jsonUrl, out var absoluteUri))
                        {
                            var absoluteUrl = absoluteUri.ToString();
                            
                            // Only include HTTP/HTTPS URLs
                            if (absoluteUrl.StartsWith("http://") || absoluteUrl.StartsWith("https://"))
                            {
                                // Extract a meaningful name from the URL
                                var uri = new Uri(absoluteUrl);
                                var fileName = Path.GetFileNameWithoutExtension(uri.LocalPath);
                                var name = !string.IsNullOrEmpty(fileName) ? fileName : "JSON Data";
                                
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
            Console.WriteLine($"Error discovering JSON URLs from homepage: {ex.Message}");
            return [];  // Collection expression for empty dictionary
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

    private async Task<SiteStatus> CheckMainSiteStatusAsync(string url)
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
    public List<EndpointStatus> DataStreams { get; set; } = []; // Collection expression
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