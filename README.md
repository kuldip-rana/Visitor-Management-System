Step 1: Create/Update the File
In your project folder, create a new file named README.md.

Open it with Notepad or VS Code.

Paste the following content exactly:

Markdown
# Visitor Management System 🏢

A .NET-based web application for managing and tracking visitors, featuring OTP verification, automated pass generation, and an administrative dashboard.

---

## 🚀 Getting Started (Setup on a New PC)

Follow these steps to set up the development environment on a new machine.

### 1. Prerequisites
Ensure you have the following installed before starting:
* **Visual Studio 2022** (with 'ASP.NET and web development' workload)
* **SQL Server & SQL Server Management Studio (SSMS)**
* **Git** (Download from [git-scm.com](https://git-scm.com/))
* **Git LFS** (Run `git lfs install` after installing Git)

### 2. Downloading the Project
You can either download the ZIP from GitHub or use the terminal. Using the terminal is recommended to ensure **Git LFS** files (like `.exe` and `.pdf`) download correctly.

**Via Terminal:**
```bash
# Initialize Git LFS
git lfs install

# Clone the repository
git clone [https://github.com/kuldip-rana/Visitor-Management-System.git](https://github.com/kuldip-rana/Visitor-Management-System.git)

# Move into the project directory
cd "Visitor Management System"
3. Database Restoration (.bak file)
This project requires a SQL Server database to function.

Open SQL Server Management Studio (SSMS).

Right-click on the Databases folder in the Object Explorer.

Select Restore Database...

Select Device and click the three dots ... to locate your .bak file (included in the project).

Ensure the Destination Database name is correct (e.g., VisitorDB).

Click OK to complete the restoration.

4. Configuration in Visual Studio
You must link the code to your local SQL Server instance.

Open the .sln or .slnx file in Visual Studio 2022.

In the Solution Explorer, open the registar/Web.config file.

Locate the <connectionStrings> section.

Update the Data Source to match your local server name:

Example: Data Source=.;Initial Catalog=VisitorDB;Integrated Security=True;

(Note: . usually represents the local default instance).

5. Restore Packages & Run
Right-click on the Solution (at the very top of Solution Explorer).

Click Restore NuGet Packages to download the necessary libraries.

Press Ctrl + Shift + B to Build the solution.

Verify the registar/Rotativa folder contains wkhtmltopdf.exe and wkhtmltoimage.exe.

Press F5 or click the Start button to run the application in your browser.

🛠 Features
Visitor Registration: Securely capture visitor details and host information.

OTP Verification: Email-based OTP system for secure identity verification.

PDF Pass Generation: Automatically generates visitor passes using Rotativa.

Admin Dashboard: Full management of departments, visitor history, and verification.

📂 Project Structure
registar/Controllers: Contains the backend logic for Visitors and Admin actions.

registar/Views: UI Razor templates for all pages.

registar/Models: Entity Framework/Database models.

registar/Rotativa: External tools used for PDF rendering.

registar/Content/Passes: Destination folder for generated PDF passes.

Developed by: Kuldip Rana


---

### Step 2: Push the new README to GitHub
To make this visible on your GitHub page immediately, run these commands in your terminal:

```bash
git add README.md
git commit -m "Add comprehensive setup and download guide"
git push origin main
