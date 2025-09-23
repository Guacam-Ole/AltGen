using Microsoft.Extensions.Logging;

namespace AltGen;

public class Looper
{
    private readonly Mastodon _mastodon;
    private readonly ILogger<Looper> _logger;
    private const int MaxErrors = 5;

    public Looper(Mastodon mastodon, ILogger<Looper> logger)
    {
        _mastodon = mastodon;
        _logger = logger;
    }


    public async Task Loop()
    {
        string? lastCheckedId = null;
        var errorCount = 0;
        _logger.LogInformation("Application Started");
        while (true)
        {
            try
            {
                lastCheckedId = await _mastodon.GetNewPosts(lastCheckedId);
                errorCount = 0;
                Thread.Sleep(TimeSpan.FromSeconds(10));
            }
            catch (Exception ex)
            {
                errorCount++;

                _logger.LogWarning(ex, "Error in Loop. '{Count}/{Max}' errors until now", errorCount, MaxErrors);
                if (ex.ToString().Contains("Too many requests"))
                {
                    var waitTime = TimeSpan.FromMinutes(10 * errorCount);
                    _logger.LogError("Too many requests. Will wait '{Delay}' minutes ", waitTime);
                    Thread.Sleep(waitTime);
                }
                else
                {
                    var waitTime = TimeSpan.FromMinutes(errorCount);
                    _logger.LogInformation("Will wait '{Delay}' minutes ", waitTime);
                    Thread.Sleep(waitTime);
                }

                if (errorCount < MaxErrors) continue;
                _logger.LogCritical("Sorry. Have to give up");
                return;
            }
        }
    }
}