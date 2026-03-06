# DataGuard - Project Context

## Project Overview

**DataGuard** is a full-stack web application for secure file storage and management. It features a React-based frontend with a .NET 10 backend, implementing user authentication with JWT tokens and challenge-response security.

### Architecture

- **Backend**: ASP.NET Core 10.0 Web API with SQLite database
- **Frontend**: React 19 with Vite (using rolldown-vite)
- **Authentication**: JWT-based with challenge-response protocol (SHA-256)
- **Storage**: File system storage with database metadata tracking
- **i18n**: Built-in language selection (Russian/English) with localStorage persistence

### Project Structure

```
DataGuard/
├── WebDemo/          # Main .NET backend project
│   ├── Controllers/  # API endpoints (Auth, Finder)
│   ├── Data/         # EF Core DbContext
│   ├── Models/       # Data models (User, File, Folder)
│   ├── Services/     # Business logic (TokenService, FileSystem)
│   ├── Migrations/   # EF Core migrations
│   └── wwwroot/      # Static files (built frontend)
├── FrontEnd/         # React frontend
│   ├── src/
│   │   ├── modules/  # Reusable UI components
│   │   ├── pages/    # Page components (Finder, Home, Auth, LanguageSelect)
│   │   ├── services/ # API service layer
│   │   ├── hooks/    # Custom React hooks
│   │   └── context/  # React Context providers (LanguageContext)
│   └── public/
└── Server/           # Additional server project (minimal)
```

### User Flow

1. **First visit**: User lands on language selection page (`/`)
2. **After language selection**: Redirected to signup page with selected language
3. **Subsequent visits**: Language persisted in localStorage; direct access to protected routes requires authentication
4. **Session refresh**: JWT automatically refreshed on finder page load

## Building and Running

### Backend (WebDemo)

```bash
# Navigate to WebDemo directory
cd WebDemo

# Restore dependencies
dotnet restore

# Run development server (configured for https://192.168.1.44:7075)
dotnet run

# Or with Visual Studio: Open DataGuard.slnx and run
```

### Frontend

```bash
# Navigate to FrontEnd directory
cd FrontEnd

# Install dependencies
npm install

# Start development server with HMR
npm run dev

# Build for production (outputs to WebDemo/wwwroot)
npm run build

# Lint code
npm run lint
```

### Database

The application uses SQLite with Entity Framework Core. The database file `DataGuard.db` is located in the WebDemo directory.

```bash
# Apply pending migrations
dotnet ef database update
```

### Docker

The project includes Docker support for Windows containers:

```bash
# Build and run with Docker (from WebDemo directory)
docker build -t dataguard .
docker run -p 7075:8080 dataguard
```

## API Endpoints

### Authentication (`/api/auth`)

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/nonce/{userName}` | Get nonce for challenge-response auth |
| POST | `/login` | Authenticate with challenge response |
| POST | `/signup` | Register new user |

### File Management (`/api/finder`)

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/?path=...` | Get files and folders for path |
| POST | `/new-file` | Create new file entry |
| POST | `/new-folder` | Create new folder entry |
| POST | `/upload` | Upload file chunk |
| GET | `/download` | Download file |

## Development Conventions

### Backend (.NET)

- **Target Framework**: .NET 10.0
- **Nullable Reference Types**: Enabled
- **Implicit Usings**: Enabled
- **Authentication**: JWT Bearer tokens
- **CORS**: Configured with "AllowAll" policy for development

### Frontend (React)

- **TypeScript**: Partial (mix of .jsx and .tsx files)
- **Styling**: SCSS with CSS variables
- **Routing**: React Router DOM v7
- **Build Tool**: Vite with rolldown-vite (experimental)
- **ESLint**: Configured with react-hooks and react-refresh plugins
- **i18n**: Custom LanguageContext with Russian/English translations

### Key Configuration

- Backend server is hardcoded to `https://192.168.1.44:7075` in `Program.cs`
- Frontend builds output to `WebDemo/wwwroot` for serving static files
- JWT secret key is stored in `appsettings.json` (should be moved to user secrets for production)
- Language preference stored in localStorage

## Security Notes

- Uses challenge-response authentication with SHA-256 hashing
- JWT tokens expire after 15 minutes
- File ownership is enforced via database relationships
- CORS is permissive in development - restrict for production

## Testing

No test projects are currently present in the solution. Consider adding:
- xUnit tests for backend controllers and services
- React Testing Library tests for frontend components
