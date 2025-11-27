using System.Threading.Tasks;

namespace VEA.API.Services
{
    public interface IEmailService
    {
        Task SendConfirmationEmail(string to, string subject, string body);
        Task SendInviteEmail(string to, string subject, string body);
    }
}