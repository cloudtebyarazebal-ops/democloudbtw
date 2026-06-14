# Рабочее место KodShopWeb + ExamCoach

## Структура

```
KodShopWeb/                 ← MVC-проект (democloudbtw)
  ExamCoach/                ← адаптер ТЗ (отдельный репозиторий)
  apply-tz.cmd              ← применить ТЗ к проекту
  check-tz.cmd              ← сверка ТЗ без изменений
  setup-workspace.ps1       ← первичная настройка
```

## Первый запуск (новый ПК)

1. Клонировать KodShopWeb:

```cmd
git clone https://github.com/cloudtebyarazebal-ops/democloudbtw.git KodShopWeb
cd KodShopWeb
```

2. Настроить ExamCoach и собрать всё:

```cmd
powershell -ExecutionPolicy Bypass -File .\setup-workspace.ps1
```

Скрипт сам клонирует ExamCoach в `ExamCoach/`, если папки нет.

## Применить новое ТЗ

1. Закройте запущенный `dotnet run`
2. Выполните:

```cmd
apply-tz.cmd "C:\path\to\tz.txt"
```

### Пустой / новый MVC-проект

Скрипт сам создаст `dotnet new mvc` и заполнит всё под ТЗ:

```cmd
apply-empty-tz.cmd "C:\path\to\tz.txt"
```

Или в свою папку:

```cmd
apply-empty-tz.cmd "C:\path\to\tz.txt" "C:\Projects\MyShop"
```

3. Запустите:

```cmd
dotnet run
```

Логин: `admin` / `admin123`

## Репозитории

| Репозиторий | Назначение |
|-------------|------------|
| [democloudbtw](https://github.com/cloudtebyarazebal-ops/democloudbtw) | Эталонный MVC-проект |
| [ExamCoach](https://github.com/cloudtebyarazebal-ops/ExamCoach) | Парсер ТЗ, AdaptTest, desktop-тренажёр |
