namespace PayPalSample.Models
{
    public class TransactionViewData
    {
        public string TransactionId { get; set; }
        public string Payer { get; set; }
        public string DateTime { get; set; }
        public string Description { get; set; }
        public decimal Total { get; set; }
        public string Status { get; set; }
    }
}