using System.Net;
using System.Net.Mail;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace EmailSender
{
    public static class Program
    {
        private static IConfiguration? _config;
        private static SmtpClient? _smtpClient;
        private static int _errorCount;

        private static IConfiguration Config
        {
            get
            {
                _config ??= BuildConfiguration();
                return _config;
            }
        }

        private static SmtpClient SmtpClient
        {
            get
            {
                _smtpClient ??= CreateSmtpClient();
                return _smtpClient;
            }
        }

        private static SmtpClient CreateSmtpClient()
        {
            var host = Config["Smtp:Host"];
            var port = Convert.ToInt32(Config["Smtp:Port"]);
            var username = Config["Smtp:Username"];
            var password = Config["Smtp:Password"];
            return new SmtpClient(host, port)
            {
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(username, password),
                EnableSsl = true
            };
        }

        private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

        private static readonly Attachment Resume = new(Config["FilePaths:Resume"]);

        public static void Main(string[] args)
        {
            var masterFilePath = Config["FilePaths:MasterCsv"];
            var mailTemplateFilepath = Config["FilePaths:MailTemplate"];
            var sentMailList = Config["FilePaths:SentMailList"];

            if (!File.Exists(masterFilePath) || !File.Exists(mailTemplateFilepath))
            {
                Console.WriteLine("File not found");
                Environment.Exit(1);
            }

            Dictionary<string, DateTime> sentMap;
            if (File.Exists(sentMailList))
            {
                var json = File.ReadAllText(sentMailList);
                sentMap = JsonSerializer.Deserialize<Dictionary<string, DateTime>>(json) ?? [];
            }
            else
                sentMap = [];

            var body = File.ReadAllText(mailTemplateFilepath);
            body = Regex.Replace(body, @"[\r\n\t]+", string.Empty);
            IList<IList<string>> masterData = [];
            try
            {
                var sr = new StreamReader(masterFilePath);
                sr.ReadLine();
                while (!sr.EndOfStream)
                {
                    string line = sr.ReadLine();
                    masterData.Add(line.Split(','));
                }

                sr.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Environment.Exit(1);
            }

            int sentMailCount = 0;
            foreach (var row in masterData)
            {
                var name = row[0];
                var email = row[2];

                var firstName = name.Split(' ')[0];
                var newBody = body.Replace("{{name}}", firstName);

                if (_errorCount > 5) break;
                
                if (sentMap.ContainsKey(email) || !TrySendMail(newBody, email, name)) continue;
                
                sentMap.Add(email, DateTime.Now);
                sentMailCount++;
                if (sentMailCount == Convert.ToInt32(Config["Mail:DailyLimit"])) break;
            }

            File.WriteAllText(sentMailList, JsonSerializer.Serialize(sentMap, SerializerOptions));

            Console.WriteLine(_errorCount < 5 ? "Mail sent successfully" : "Limit Exceeded, stopped sending mails.");

            var failed = masterData.Count - sentMap.Count;
            if (failed > 0)
                Console.WriteLine($"Failed to send {failed} mails");
        }

        private static IConfiguration BuildConfiguration()
        {
            var projectRoot = Directory.GetParent(AppContext.BaseDirectory).Parent.Parent.Parent.FullName;
            var builder = new ConfigurationBuilder();
            builder.SetBasePath(projectRoot);
            builder.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            builder.AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true);
            return builder.Build();
        }

        private static bool TrySendMail(string body, string toEmailAddress, string name)
        {
            try
            {
                var mailMessage = new MailMessage();
                mailMessage.From = new MailAddress(Config["Smtp:Username"], Config["Mail:SenderName"]);
                mailMessage.To.Add(new MailAddress(toEmailAddress, name));
                mailMessage.Subject = Config["Mail:Subject"];
                mailMessage.Body = body;
                mailMessage.IsBodyHtml = true;
                mailMessage.Attachments.Add(Resume);
                SmtpClient.Send(mailMessage);

                Console.WriteLine($"Mail sent to {toEmailAddress}");
                _errorCount = 0;
                return true;
            }
            catch (Exception e)
            {
                _errorCount++;
                Console.WriteLine($"Error sending mail for {toEmailAddress}: {e.Message}");
                return false;
            }
        }
    }
}