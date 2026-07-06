namespace ScreenPulse.Services;

public class RetentionCleanupService
{
    private readonly ActivityLogStore _store;

    public RetentionCleanupService(ActivityLogStore store)
    {
        _store = store;
    }

    public void CleanupOlderThan(int retentionDays)
    {
        var cutoff = DateTime.Today.AddDays(-retentionDays);
        foreach (var day in _store.GetAllLoggedDays().ToList())
        {
            if (day < cutoff)
            {
                _store.DeleteDay(day);
            }
        }
    }
}
