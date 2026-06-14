# ExamCoach — тренажёр для ручного переписывания проекта на экзамене

Офлайн HTML-приложение и **WPF desktop-приложение**: показывает **что создавать в Visual Studio**, **какие NuGet ставить**, код **построчно / по словам / по буквам**, и **таймеры модулей экзамена**.

## Быстрый старт (накануне экзамена)

1. Сгенерируйте manifest, данные и HTML из текущего кода:

```powershell
cd KodShopWeb\ExamCoach
powershell -ExecutionPolicy Bypass -File .\Generate-ExamCoach.ps1 -UpdateManifest
```

2. **HTML (флешка):** откройте **`index.html`** двойным щелчком (Chrome / Edge).
3. **Desktop (новый проект в VS — не эталон KodShopWeb):**

```powershell
cd KodShopWeb\ExamCoach\Desktop
dotnet build -c Release
.\bin\Release\net8.0-windows\ExamCoachDesktop.exe
```

**Порядок:**
1. В Visual Studio: **File → New → Project → ASP.NET Core Web App (MVC)**, .NET 8, имя любое (например `KodShopWeb`).
2. Запустите ExamCoachDesktop → **Подключить Visual Studio** → **Из VS — найти проект**.
3. Идите по шагам — файлы **создаются в новом проекте**, код **вставляется в редактор VS**.

Эталонный репозиторий `KodShopWeb` на диске **не изменяется** — только ваш новый проект в VS.

4. Скопируйте папку **`ExamCoach`** на флешку — интернет не нужен.

## Автообновление manifest

Список файлов больше не нужно править вручную. Скрипт **`Update-Manifest.ps1`** сканирует проект:

- корень: `KodShopWeb.csproj`, `appsettings.json`, `Program.cs`
- `Models/`, `Data/`, `Services/`, `ViewModels/`, `Controllers/`
- `Views/**/*.cshtml`, `wwwroot/css/*.css`, `wwwroot/js/*.js`

Подсказки `vsHint` из старого manifest сохраняются. Для новых файлов генерируются автоматически.

Только обновить manifest без HTML:

```powershell
powershell -ExecutionPolicy Bypass -File .\Update-Manifest.ps1
```

## Таймеры модулей (4 часа экзамена)

| Модуль | Время | Содержание |
|--------|-------|------------|
| **М1 — БД** | 50 мин | Models + Data + DbSeeder + ER + импорт |
| **М2 — Вход** | 40 мин | Program + Account + Products Index + Login |
| **М3 — Товары** | 1ч 30 | ProductService + Edit view + products.js |
| **М4 — Заказы** | 1ч | Orders (вариант ПУ) |

В HTML и Desktop: **▶ Старт**, **⏸ Пауза**, **↺** сброс. Прогресс таймеров сохраняется локально.

## Как пользоваться

| Кнопка / клавиша | Действие |
|------------------|----------|
| **Построчно** | Рекомендуется — одна строка за раз |
| **По словам / буквам** | Для запоминания |
| **F8** / **→** | Следующая строка или слово |
| **+10 строк** | Быстро показать блок |
| **✓ Готово** | Отмечает шаг |
| **Поверх всех окон** | Desktop — держать рядом с Visual Studio |

## NuGet (версия 8.x)

```
Microsoft.EntityFrameworkCore.Sqlite 8.0.11
Microsoft.EntityFrameworkCore.Design 8.0.11
ClosedXML 0.105.0
```

## Файлы

| Файл | Назначение |
|------|------------|
| `steps-manifest.json` | Сценарий шагов (авто-скан + setup) |
| `coach-config.json` | Setup-шаги и описание модулей (русский текст) |
| `steps-data.json` | JSON для HTML и Desktop |
| `index.html` | Офлайн-тренажёр в браузере |
| `Desktop/` | WPF-приложение поверх VS |
| `Generate-ExamCoach.ps1` | Сборка HTML + JSON |
| `Update-Manifest.ps1` | Авто-скан файлов проекта |

Не пытайтесь переписать всё по буквам — используйте **«Построчно»** и **✓ Готово** как чеклист.
