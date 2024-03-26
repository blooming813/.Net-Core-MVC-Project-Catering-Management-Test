using CateringManagement.ViewModels;

namespace CateringManagement.Utilities
{
    public interface IMyEmailSender
    {
        Task SendOneAsync(string name, string email, string subject, string htmlMessage);
        Task SendToManyAsync(EmailMessage emailMessage);
    }
}
