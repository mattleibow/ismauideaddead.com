using System.Net;

namespace IsMauiDeadDead.Services;

public interface IStatusService
{
    Task<SiteStatus> CheckSiteStatusAsync(string url);
}

public class StatusService : IStatusService
{
    private readonly HttpClient _httpClient;

    public StatusService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
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
}