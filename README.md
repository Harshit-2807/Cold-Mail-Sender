# 📧 Email Sender Tool (C# .NET 9 Console Application)

A lightweight and configurable **email automation console application** built using **.NET 9**.
It reads recipient data from a CSV file, personalizes an HTML email template, attaches a resume file, and sends emails using SMTP.

All configuration values (SMTP credentials + file paths) are externalized using **appsettings.json**, **appsettings.Development.json**, and optional **environment variables**.

---

## 🚀 Features

* Reads recipient list from `master.csv`
* Replaces template variables (e.g., `{{name}}`) in `MailBody.html`
* Sends personalized emails via SMTP
* Tracks previously sent emails using `sentMailList.json`
* Supports **retry logic** and auto-stop after repeated failures
* All sensitive data is stored outside code using:

  * `appsettings.json`
  * `appsettings.Development.json` (ignored by Git)
  * Environment variables
* Works on Windows / macOS / Linux

---

## 📂 Project Structure

```
EmailSender/
│
├── Program.cs
├── appsettings.json
├── appsettings.Development.json   (ignored in Git)
├── resources/
│   ├── master.csv
│   ├── MailBody.html
│   ├── Resume_Harshit.pdf
│   └── sentMailList.json
├── bin/
└── obj/
```

---

## ⚙️ Configuration

The app uses `IConfiguration` to load:

1️⃣ `appsettings.json`
2️⃣ `appsettings.Development.json` (optional, ignored by Git)
3️⃣ Environment variables (if added)

### **appsettings.json**

```json
{
  "Smtp": {
    "Host": "smtp.gmail.com",
    "Port": 587,
    "Username": "",
    "Password": ""
  },
  "FilePaths": {
    "MasterCsv": "resources/master.csv",
    "MailTemplate": "resources/MailBody.html",
    "SentMailList": "resources/sentMailList.json",
    "Resume": "resources/Resume_Harshit.pdf"
  }
}
```

### **appsettings.Development.json**

(not included in Git)

```json
{
  "Smtp": {
    "Username": "your-email@gmail.com",
    "Password": "your-app-password"
  }
}
```

---

## 🔐 Security Notes

* **Never commit SMTP passwords or app passwords** into your repo.
* `appsettings.Development.json` is excluded via `.gitignore` to keep secrets safe.
* Gmail requires generating an **App Password** (not your Gmail login password).

---

## ▶️ Running the Application

### **1. Restore dependencies**

```
dotnet restore
```

### **2. Build**

```
dotnet build
```

### **3. Run**

```
dotnet run
```

Emails will begin sending and logs will appear in the console.

---

## 📨 CSV Format (`master.csv`)

```
Name,Organization,Email
Harshit Patel,MAANG,test@example.com
```

Index positions used:

* `row[0]` → Name
* `row[2]` → Email

---

## 🖼️ Email Template (`MailBody.html`)

Supports placeholder variables:

```
Hello {{name}},
Thank you for reviewing my profile...
```

Placeholders are replaced dynamically.

---

## 📄 Attachments

The resume is dynamically loaded from:

```
resources/Resume_Harshit.pdf
```

You can update this file or path in config.

---

## 🔁 Retry Handling

* If sending fails more than **5 consecutive times**, the tool stops automatically.
* Previously sent emails are stored in:

```
sentMailList.json
```

This prevents sending duplicate emails.

---

## 🛠️ Requirements

* .NET SDK 9.0+
* Gmail App Password (if using Gmail SMTP)
* CSV + template + resume files inside `/resources`

---

## 🤝 Contributions

Improvements welcome!
If you'd like help adding CLI arguments, scheduling, or parallel sending, feel free to propose enhancements.

---

## 📜 License

MIT License — free for personal and commercial use.

