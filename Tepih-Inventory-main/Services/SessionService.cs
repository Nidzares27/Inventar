namespace Inventar.Services
{
    public interface ISessionService
    {
        void ClearScannedProducts(ISession session);
    }

    public class SessionService : ISessionService
    {
        private const string ScannedProductsSessionKey = "scannedProducts";

        public void ClearScannedProducts(ISession session)
        {
            session.Remove(ScannedProductsSessionKey);
        }
    }

}
