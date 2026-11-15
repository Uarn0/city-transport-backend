# Середовище розробки та тестування бекенду (CityTransport)
**Jira Task:** [DU23-44] [QA] Підготовка тест-дизайну та документування середовища  
**Проєкт:** CityTransport (Е-квиток для громадського транспорту з QR-кодом)  
**Компонент:** Backend API (`CityTransport` та `CityTransport.Tests`)  

---

## 1. Специфікація стеку технологій та версій

| Компонент / Інструмент | Версія | Призначення | Примітки |
| :--- | :--- | :--- | :--- |
| **Операційна система** | Windows 10/11 / Windows Server (x64) (підтримка Linux/macOS) | Хост-середовище виконання та збірки | Підтримується кросплатформна збірка .NET |
| **Платформа / SDK** | .NET SDK `10.0.402` (Runtime `10.0.13`) | Базова середа компіляції та виконання | Target Framework: `net10.0` |
| **Мова програмування** | C# 14 | Основна мова розробки | Nullable reference types: `enable`, Implicit usings: `enable` |
| **Веб-фреймворк** | ASP.NET Core Web API (`net10.0`) | Маршрутизація, контролери, DI, безпека | Контролери API та Scalar OpenAPI Reference |
| **ORM / Доступ до даних** | Entity Framework Core `10.0.12` | Мапінг сутностей, міграції, робота з БД | Пакети `Microsoft.EntityFrameworkCore.Design`, `Tools` |
| **Основна СУБД** | PostgreSQL 16+ (Npgsql `10.0.3`) | Реляційне сховище (`citypass_db`) | Контейнер `citypass_postgres`, порт 5432 |
| **Тестова СУБД (In-Memory)** | SQLite In-Memory (`Microsoft.EntityFrameworkCore.Sqlite 10.0.12`) | Ізольоване модульне тестування сервісів БД | DataSource=:memory:, швидке виконання тестів |
| **Тестовий фреймворк** | xUnit `2.9.3` | Створення та запуск модульних тестів | Пакети: `xunit`, `xunit.runner.visualstudio 3.1.4` |
| **Test Runner SDK** | `Microsoft.NET.Test.Sdk 17.14.1` | Інтеграція runner з dotnet test CLI | Підтримка VSTest та фільтрації тестів |
| **Бібліотека моків** | Moq `4.21.0` | Мокування інтерфейсів, ILogger, IOptions | Тестування контрактів ізольовано |
| **Аналіз покриття коду** | Coverlet (`coverlet.collector 6.0.4`) | Збір метрик покриття коду (Line, Branch) | Підтримка Cobertura, OpenCover, lcov |
| **Криптографія** | .NET Standard Cryptography (AES-256-CBC) | Шифрування/дешифрування QR-пейлоаду | Ключ довжиною 32 байти з `CryptoSettings` |
| **Генерація QR-кодів** | QRCoder `1.8.0` (`PngByteQRCode`) | Рендеринг QR-кодів у форматі Base64 PNG | ECC-рівень Q для надійності сканування |
| **Аутентифікація** | `Microsoft.AspNetCore.Authentication.JwtBearer 10.0.12` | JWT-токени для безпеки API | Валідація Issuer, Audience, Lifetime, SigningKey |
| **API Documentation** | `Scalar.AspNetCore 2.17.13` + `Microsoft.AspNetCore.OpenApi` | Інтерактивна документація ендпоінтів | Доступна за маршрутом `/scalar/v1` у Development |
| **IDE / Редактори** | Visual Studio Code / Visual Studio 2026 / JetBrains Rider | Розробка, налагодження та запуск | Розширення VS Code: *C# Dev Kit*, *.NET Install Tool* |

---

## 2. Підготовка локального середовища розробки

### Крок 2.1. Перевірка встановленого .NET SDK
Переконайтеся, що встановлено .NET 10 SDK:
```powershell
dotnet --version
# Очікуваний вивід: 10.0.xxx
dotnet --info
```

### Крок 2.2. Клонування репозиторію та вибір робочої гілки
```powershell
git clone https://github.com/Uarn0/city-transport-backend.git
cd city-transport-backend
git checkout feature/DU-23-44-test-design-environment
```

### Крок 2.3. Відновлення залежностей (Restore)
Відновлення пакетів NuGet для основного проєкту та тестів:
```powershell
dotnet restore CityTransport/CityTransport.csproj
dotnet restore CityTransport.Tests/CityTransport.Tests.csproj
```

### Крок 2.4. Компіляція проєкту (Build)
Перевірка успішності компіляції:
```powershell
dotnet build CityTransport/CityTransport.csproj -c Debug
dotnet build CityTransport.Tests/CityTransport.Tests.csproj -c Debug
```

### Крок 2.5. Налаштування бази даних PostgreSQL (Dev Environment)
Запуск локального контейнера PostgreSQL у Docker:
```powershell
docker run --name citypass_postgres `
  -e POSTGRES_DB=citypass_db `
  -e POSTGRES_USER=dev_user `
  -e POSTGRES_PASSWORD=dev_password `
  -p 5432:5432 `
  -d postgres:16-alpine
```

Рядок підключення у `CityTransport/appsettings.Development.json`:
```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Port=5432;Database=citypass_db;Username=dev_user;Password=dev_password"
}
```

Застосування міграцій Entity Framework Core:
```powershell
dotnet ef database update --project CityTransport
```

---

## 3. Інструкції із запуску тестів у командному рядку (CLI)

### 3.1. Запуск одного тривіального тесту (Single Test Run)
Запуск перевірки неавторизованого доступу з класу `AuthTests`:
```powershell
dotnet test CityTransport.Tests/CityTransport.Tests.csproj `
  --filter "FullyQualifiedName~AuthTests.GetMyTickets_ReturnsUnauthorized_WhenUserClaimsAreMissing" `
  --logger "console;verbosity=detailed"
```

### 3.2. Запуск тестів конкретного модуля (Filter by Class)
Запуск тестів тарифів:
```powershell
dotnet test CityTransport.Tests/CityTransport.Tests.csproj `
  --filter "FullyQualifiedName~TariffServiceTests" `
  --logger "console;verbosity=normal"
```

Запуск тестів криптографії та генерації QR-кодів:
```powershell
dotnet test CityTransport.Tests/CityTransport.Tests.csproj `
  --filter "FullyQualifiedName~QrCodeGeneratorServiceTests" `
  --logger "console;verbosity=normal"
```

### 3.3. Запуск усіх тестів тестового проєкту
```powershell
dotnet test CityTransport.Tests/CityTransport.Tests.csproj --logger "console;verbosity=normal"
```

### 3.4. Збір метрик покриття коду (Code Coverage)
Запуск збору покриття за допомогою `coverlet.collector`:
```powershell
dotnet test CityTransport.Tests/CityTransport.Tests.csproj `
  --collect:"XPlat Code Coverage" `
  --results-directory ./TestResults
```

Результат покриття зберігається у форматі XML (`coverage.cobertura.xml`) у вказаній директорії `./TestResults`.

### 3.5. Генерація людиночитаного HTML-звіту покриття (опціонально)
За наявності встановленого інструменту `reportgenerator`:
```powershell
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator `
  -reports:"./TestResults/**/coverage.cobertura.xml" `
  -targetdir:"./TestResults/CoverageReport" `
  -reporttypes:Html
```
Після генерації відкрийте `TestResults/CoverageReport/index.html` у браузері для перегляду відсотку покриття класів, методів та гілок (Line & Branch Coverage).

