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
    }

    public async Task<SiteStatus> CheckSiteStatusAsync(string url)
    {
        try
        {
            var response = await _httpClient.GetAsync(url);
            return new SiteStatus
            {
                IsOnline = response.IsSuccessStatusCode,
                StatusCode = (int)response.StatusCode,
                ResponseTime = TimeSpan.FromMilliseconds(100), // Simplified
                LastChecked = DateTime.UtcNow
            };
        }
        catch (HttpRequestException)
        {
            return new SiteStatus
            {
                IsOnline = false,
                StatusCode = 0,
                ResponseTime = TimeSpan.Zero,
                LastChecked = DateTime.UtcNow
            };
        }
        catch (TaskCanceledException)
        {
            return new SiteStatus
            {
                IsOnline = false,
                StatusCode = 408, // Request Timeout
                ResponseTime = TimeSpan.Zero,
                LastChecked = DateTime.UtcNow
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
}