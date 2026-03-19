# 🏢 Visitor Management System

A web-based Visitor Management System built using **ASP.NET MVC** to efficiently manage visitor check-ins, approvals, and tracking within an organization.

---

## 🚀 Features

* 🔐 Secure Visitor Registration with OTP Verification
* 📝 Visitor Check-In / Check-Out System
* 📊 Admin Dashboard with Analytics
* 📅 Visit History Tracking
* 📄 Auto-Generated Visitor Pass (PDF)
* 👨‍💼 Admin Approval for Visitor Requests
* 🔁 Revisit Request Feature
* 📧 Email Notification System

---

## 🛠️ Tech Stack

* **Frontend:** HTML, CSS, Bootstrap, JavaScript
* **Backend:** ASP.NET MVC (C#)
* **Database:** SQL Server
* **ORM:** Entity Framework
* **PDF Generation:** Rotativa (wkhtmltopdf)

---

## 📂 Project Structure

```
Visitor Management System/
│
├── Controllers/        # MVC Controllers (Admin, Visitor, Home)
├── Models/             # Database Models & Entities
├── Views/              # Razor Views (UI Pages)
├── ViewModels/         # Data transfer between Views & Controllers
├── Content/            # CSS, Bootstrap, Static Files
├── Scripts/            # JavaScript & jQuery
├── Rotativa/           # PDF generation tools
└── Web.config          # Application configuration
```

---

## ⚙️ Installation & Setup

1. Clone the repository:

   ```bash
   git clone https://github.com/your-username/Visitor-Management-System.git
   ```

2. Open the project in **Visual Studio**

3. Restore NuGet Packages:

   ```
   Tools → NuGet Package Manager → Restore Packages
   ```

4. Update Database Connection:

   * Open `Web.config`
   * Modify connection string as per your SQL Server

5. Run the application:

   ```
   Press F5 or Click Run
   ```

---

## 🧪 How It Works

1. Visitor registers and verifies OTP
2. Admin reviews and approves request
3. Visitor receives a digital pass (PDF)
4. Visitor checks in/out using the system
5. Admin can track all visit records

---

## 📸 Screenshots (Optional)

*Add screenshots here for better presentation*

---

## 📌 Future Improvements

* Role-based authentication (Admin/User separation)
* QR Code-based Check-In System
* Mobile responsiveness improvements
* Cloud deployment (Azure / AWS)
* Real-time notifications

---

## 👨‍💻 Author

**Your Name**

* GitHub: https://github.com/kuldip-rana

---

## 📄 License

This project is for educational purposes.

---

## ⭐ Show Your Support

If you like this project, give it a ⭐ on GitHub!

---
