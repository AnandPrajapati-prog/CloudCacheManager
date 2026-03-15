# ☁️ CloudCache Manager — .NET Core Web API

A secure file management Web API + UI for browsing, downloading, and editing the `D:\CloudCache\UserAppSettings` directory.

---

## 🚀 Quick Start

### 1. Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows 10/11

### 2. Setup
```bash
# Clone / copy the project to your machine, then:
cd CloudCacheManager
dotnet restore
dotnet run
```

The app will start at: **http://localhost:5000**  
Swagger docs: **http://localhost:5000/swagger**

---

## ⚙️ Configuration

Edit `appsettings.json` to change the base path or credentials:

```json
{
  "AppSettings": {
    "BasePath": "D:\\CloudCache\\UserAppSettings"
  },
  "Jwt": {
    "Key": "YourSuperSecretKey_AtLeast32Characters!"
  }
}
```

### Default Credentials
| Username | Password      |
|----------|---------------|
| admin    | Admin@1234    |
| manager  | Manager@1234  |

> ⚠️ Change these in `Services/AuthService.cs` before production use!

---

## 🔐 Features

### 1. Directory Tree + File Browser
- Auto-loads the full `UserAppSettings` directory tree on startup
- Shows sub-folders recursively with file counts
- Displays files with name, size, and last modified date
- **Click any folder** to expand/collapse
- **Click any file** to view its contents (syntax-highlighted JSON)

### 2. File Download (No Auth Required)
- Click the ⬇ icon next to any file to download it
- Works without login

### 3. Edit File (Auth Required 🔒)
- Login required to access the Edit tab
- Before saving, **a backup is automatically created** in a `_Backups` subfolder
- Backup naming: `filename_yyyyMMdd_HHmmss.json`
- JSON is validated before saving

### 4. Add Key/Value to JSON (Auth Required 🔒)
- Login required to access the Add Key/Value tab
- **A backup is automatically created** before adding any key
- Works only with valid JSON files

---

## 📡 API Endpoints

| Method | Endpoint                  | Auth | Description                          |
|--------|---------------------------|------|--------------------------------------|
| GET    | `/api/files/tree`         | No   | Get full directory tree              |
| GET    | `/api/files/download`     | No   | Download a file (`?path=...`)        |
| GET    | `/api/files/read`         | Yes  | Read file content (`?path=...`)      |
| PUT    | `/api/files/edit`         | Yes  | Edit file (auto-backup)              |
| POST   | `/api/files/addkeyvalue`  | Yes  | Add key/value to JSON (auto-backup)  |
| POST   | `/api/auth/login`         | No   | Get JWT token                        |

---

## 🗂️ Project Structure

```
CloudCacheManager/
├── Controllers/
│   ├── AuthController.cs       ← Login endpoint
│   └── FilesController.cs      ← File operations
├── Models/
│   └── Models.cs               ← Request/response models
├── Services/
│   ├── AuthService.cs          ← JWT token generation
│   └── FileManagerService.cs   ← File read/write/backup logic
├── wwwroot/
│   └── index.html              ← Full-featured web UI
├── Program.cs                  ← App setup + JWT config
├── appsettings.json            ← Configuration
└── CloudCacheManager.csproj    ← Project file
```

---

## 🛡️ Security Notes

- JWT tokens expire after **8 hours**
- All file paths are validated to prevent **directory traversal attacks**
- Only authenticated users can **edit** files or **add key-values**
- Every write operation creates a **timestamped backup** automatically
- Backups are stored in a `_Backups` folder inside each directory

---

## 📦 Backup Location

For a file at:
```
D:\CloudCache\UserAppSettings\EASEMYTRIPCOM\CloudSettingsPath.json
```

Backup will be created at:
```
D:\CloudCache\UserAppSettings\EASEMYTRIPCOM\_Backups\CloudSettingsPath_20260313_143022.json
```
