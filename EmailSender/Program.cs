using System.Net;
using System.Net.Mail;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace EmailSender
{
    public static class Program
    {
        private static readonly IConfiguration Config = BuildConfiguration();
        private static readonly SmtpClient SmtpClient = CreateSmtpClient();
        private static int _errorCount;
        private static readonly Dictionary<string, string> MailTemplates = LoadMailTemplates();

        private static IConfiguration BuildConfiguration()
        {
            var projectRoot = Directory.GetParent(AppContext.BaseDirectory).Parent.Parent.Parent.FullName;
            var builder = new ConfigurationBuilder();
            builder.SetBasePath(projectRoot);
            builder.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            builder.AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true);
            return builder.Build();
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

        private static Dictionary<string, string> LoadMailTemplates()
        {
            var mailTemplates = Directory.GetFiles(Config["FilePaths:Resources"], "*.html");
            return mailTemplates.ToDictionary(
                Path.GetFileNameWithoutExtension,
                filePath =>
                {
                    var body = File.ReadAllText(filePath);
                    return Regex.Replace(body, @"[\r\n\t]+", string.Empty);
                },
                StringComparer.OrdinalIgnoreCase);
        }

        private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

        private static readonly Attachment Resume = new(Config["FilePaths:Resume"]);

        public static void Main(string[] args)
        {
            var masterFilePath = Config["FilePaths:MasterCsv"];
            var mailTemplateFileName = Config["FilePaths:MailTemplate"];
            var sentMailFilePath = Config["FilePaths:SentMailList"];

            if (!File.Exists(masterFilePath) || !MailTemplates.ContainsKey(mailTemplateFileName))
            {
                Console.WriteLine("File not found");
                Environment.Exit(1);
            }

            Dictionary<string, DateTime> sentMap;
            if (File.Exists(sentMailFilePath))
            {
                var json = File.ReadAllText(sentMailFilePath);
                sentMap = JsonSerializer.Deserialize<Dictionary<string, DateTime>>(json) ?? [];
            }
            else
                sentMap = [];

            var body = MailTemplates[mailTemplateFileName];
            IList<IList<string>> masterData = [];
            int nameIndex = 0, emailIndex = 2, organizationIndex = 1;
            try
            {
                var sr = new StreamReader(masterFilePath);
                var headers = sr.ReadLine().Split(',');
                nameIndex = Array.IndexOf(headers, "Name");
                emailIndex = Array.IndexOf(headers, "Email");
                organizationIndex = Array.IndexOf(headers, "Organization");
                if (nameIndex == -1 || emailIndex == -1 || organizationIndex == -1)
                    throw new Exception("All headers not present in the CSV file");
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
                var name = row[nameIndex];
                var email = row[emailIndex];
                if (sentMap.ContainsKey(email)) continue;
                var organization = row[organizationIndex].Split(' ')[0];

                var newBody = body;
                if (MailTemplates.TryGetValue(organization, out var value))
                    newBody = value;

                var firstName = name.Split(' ')[0];
                newBody = newBody.Replace("{{name}}", firstName);

                if (_errorCount > 5) break;

                if (!TrySendMail(newBody, email, name)) continue;

                sentMap.Add(email, DateTime.Now);
                sentMailCount++;
                if (sentMailCount == Convert.ToInt32(Config["Mail:DailyLimit"])) break;
            }

            File.WriteAllText(sentMailFilePath, JsonSerializer.Serialize(sentMap, SerializerOptions));

            Console.WriteLine(_errorCount < 5 ? "Mail sent successfully" : "Limit Exceeded, stopped sending mails.");

            var failed = masterData.Count - sentMap.Count;
            if (failed > 0)
                Console.WriteLine($"Failed to send {failed} mails");
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