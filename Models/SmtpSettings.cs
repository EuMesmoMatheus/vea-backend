namespace VEA.API.Models // ajuste o namespace se precisar
{
    public class SmtpSettings
    {
        public string Host { get; set; } = "smtp.gmail.com";
        public int Port { get; set; } = 587;
        public string Username { get; set; } = "veaenterpriseapi@gmail.com";
        public string Password { get; set; } = string.Empty; // será sobrescrito no Railway
        public string From { get; set; } = "veaenterpriseapi@gmail.com";
    }
}