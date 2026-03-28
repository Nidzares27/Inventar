namespace Inventar.Interfaces
{
    public interface IWebOrderProcessingService
    {
        Task<WebOrderProcessingResult> ApplyStatusUpdateAsync(
            int orderId,
            string status,
            string paymentStatus,
            string fulfillmentStatus,
            string? note,
            string? internalNote,
            string changedBy,
            CancellationToken cancellationToken = default);

        Task<int> ExpireReservationsAsync(CancellationToken cancellationToken = default);
    }

    public sealed class WebOrderProcessingResult
    {
        public bool Succeeded { get; init; }
        public string Message { get; init; } = string.Empty;
    }
}
