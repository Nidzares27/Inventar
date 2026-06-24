using Inventar.Interfaces;

namespace Inventar.Services
{
    public class ExpiredReservationCleanupService : BackgroundService
    {
        private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(10);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ExpiredReservationCleanupService> _logger;

        public ExpiredReservationCleanupService(
            IServiceScopeFactory scopeFactory,
            ILogger<ExpiredReservationCleanupService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(CleanupInterval);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var orderProcessingService = scope.ServiceProvider.GetRequiredService<IWebOrderProcessingService>();
                    await orderProcessingService.ExpireReservationsAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Expired reservation cleanup failed.");
                }

                await timer.WaitForNextTickAsync(stoppingToken);
            }
        }
    }
}
