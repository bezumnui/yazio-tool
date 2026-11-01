using YazioTool.Core.Domain;

namespace YazioTool.Core.Abstractions;

public interface IDiaryReader
{
    Task<DailyDiary> GetDayAsync(DateOnly date, CancellationToken cancellationToken = default);
}
