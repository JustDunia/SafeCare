using Microsoft.JSInterop;

namespace SafeCare.Services
{
    public interface ITimezoneService
    {
        Task<int> GetUserTimezoneOffsetAsync();
        DateTime ConvertLocalToUtc(DateTime localDateTime, int timezoneOffsetMinutes);
        DateTime ConvertLocalToUtc(DateOnly localDate, TimeOnly localTime, int timezoneOffsetMinutes);
        DateTime ConvertLocalToUtc(DateOnly localDate);
    }

    public class TimezoneService : ITimezoneService
    {
        private readonly Lazy<Task<IJSObjectReference>> _moduleTask;
        private int? _cachedOffset;
        private readonly SemaphoreSlim _offsetLock = new(1, 1);

        public TimezoneService(IJSRuntime jsRuntime)
        {
            _moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>(
                "import", "./js/timezone.js").AsTask());
        }

        public async Task<int> GetUserTimezoneOffsetAsync()
        {
            if (_cachedOffset.HasValue)
            {
                return _cachedOffset.Value;
            }

            await _offsetLock.WaitAsync();
            try
            {
                if (!_cachedOffset.HasValue)
                {
                    var module = await _moduleTask.Value;
                    _cachedOffset = await module.InvokeAsync<int>("getTimezoneOffset");
                }
                return _cachedOffset.Value;
            }
            finally
            {
                _offsetLock.Release();
            }
        }

        public DateTime ConvertLocalToUtc(DateTime localDateTime, int timezoneOffsetMinutes)
        {
            var utcDateTime = localDateTime.AddMinutes(timezoneOffsetMinutes);
            return DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
        }

        public DateTime ConvertLocalToUtc(DateOnly localDate, TimeOnly localTime, int timezoneOffsetMinutes)
        {
            var localDateTime = localDate.ToDateTime(localTime);
            return ConvertLocalToUtc(localDateTime, timezoneOffsetMinutes);
        }

        /// <summary>
        /// Only for dates without time -> creates local day with 0 offset
        /// which may be invalid when converting to local dateTime
        /// </summary>
        public DateTime ConvertLocalToUtc(DateOnly localDate)
        {
            var dateTime = localDate.ToDateTime(TimeOnly.MinValue);
            return DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
        }
    }
}
