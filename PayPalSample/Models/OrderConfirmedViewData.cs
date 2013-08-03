using System;

namespace PayPalSample.Models
{
    public class OrderConfirmedViewData : PayPalViewData
    {
        public Guid Id { get; set; }

        public string Token { get; set; }

        public string PayerId { get; set; }

        public string AuthorizationId { get; set; }

        public string TransactionId { get; set; }
    }
}