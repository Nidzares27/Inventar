using System.Globalization;
using System.Text.RegularExpressions;
using Inventar.Models;
using Inventar.Resources;

namespace Inventar.Utils
{
    public static class StorefrontStatusLocalizer
    {
        private static readonly IReadOnlyDictionary<string, string> ResourceKeys = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [WebOrderStatuses.Pending] = "WebOrderStatus_Pending",
            [WebOrderStatuses.AwaitingPayment] = "WebOrderStatus_AwaitingPayment",
            [WebOrderStatuses.Paid] = "WebOrderStatus_Paid",
            [WebOrderStatuses.Processing] = "WebOrderStatus_Processing",
            [WebOrderStatuses.Shipped] = "WebOrderStatus_Shipped",
            [WebOrderStatuses.Completed] = "WebOrderStatus_Completed",
            [WebOrderStatuses.Cancelled] = "WebOrderStatus_Cancelled",
            [WebOrderStatuses.Refunded] = "WebOrderStatus_Refunded",

            [WebPaymentStatuses.Pending] = "WebPaymentStatus_Pending",
            [WebPaymentStatuses.Authorized] = "WebPaymentStatus_Authorized",
            [WebPaymentStatuses.Paid] = "WebPaymentStatus_Paid",
            [WebPaymentStatuses.Failed] = "WebPaymentStatus_Failed",
            [WebPaymentStatuses.Refunded] = "WebPaymentStatus_Refunded",

            [WebFulfillmentStatuses.Unfulfilled] = "WebFulfillmentStatus_Unfulfilled",
            [WebFulfillmentStatuses.Processing] = "WebFulfillmentStatus_Processing",
            [WebFulfillmentStatuses.Shipped] = "WebFulfillmentStatus_Shipped",
            [WebFulfillmentStatuses.Completed] = "WebFulfillmentStatus_Completed",
            [WebFulfillmentStatuses.Cancelled] = "WebFulfillmentStatus_Cancelled",

            [InventoryReservationStatuses.Active] = "InventoryReservationStatus_Active",
            [InventoryReservationStatuses.Released] = "InventoryReservationStatus_Released",
            [InventoryReservationStatuses.Converted] = "InventoryReservationStatus_Converted",
            [InventoryReservationStatuses.Expired] = "InventoryReservationStatus_Expired"
        };

        public static string Localize(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return string.Empty;
            }

            if (ResourceKeys.TryGetValue(status, out var resourceKey))
            {
                var localizedValue = Resource.ResourceManager.GetString(resourceKey, CultureInfo.CurrentUICulture);
                if (!string.IsNullOrWhiteSpace(localizedValue))
                {
                    return localizedValue;
                }
            }

            return Humanize(status);
        }

        public static string LocalizeHistoryNote(string? note)
        {
            if (string.IsNullOrWhiteSpace(note))
            {
                return string.Empty;
            }

            var localizedSegments = note
                .Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(LocalizeHistoryNoteSegment)
                .ToArray();

            return localizedSegments.Length == 0
                ? note
                : string.Join(" | ", localizedSegments);
        }

        private static string LocalizeHistoryNoteSegment(string segment)
        {
            if (TryLocalizeLabeledStatusSegment(segment, "Payment:", Resource.Payment, out var localizedPaymentSegment))
            {
                return localizedPaymentSegment;
            }

            if (TryLocalizeLabeledStatusSegment(segment, "Fulfillment:", Resource.Fulfillment, out var localizedFulfillmentSegment))
            {
                return localizedFulfillmentSegment;
            }

            return segment;
        }

        private static bool TryLocalizeLabeledStatusSegment(
            string segment,
            string prefix,
            string localizedLabel,
            out string localizedSegment)
        {
            if (!segment.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                localizedSegment = string.Empty;
                return false;
            }

            var rawStatus = segment[prefix.Length..].Trim();
            localizedSegment = $"{localizedLabel}: {Localize(rawStatus)}";
            return true;
        }

        private static string Humanize(string value)
        {
            return Regex.Replace(value, "(?<=[a-z])([A-Z])", " $1");
        }
    }
}
