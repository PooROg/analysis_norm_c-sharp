# Инструкция по установке NuGet пакетов для Чата 2

## Необходимые пакеты

В консоли Package Manager Console (Visual Studio) выполните следующие команды:

```powershell
# Математическая библиотека для интерполяции
Install-Package MathNet.Numerics

# Библиотека для работы с Excel
Install-Package ClosedXML
```

## Альтернативный способ (через NuGet Package Manager):

1. Правой кнопкой на проекте → Manage NuGet Packages
2. Вкладка "Browse"
3. Найти и установить:
   - **MathNet.Numerics** (последняя стабильная версия)
   - **ClosedXML** (последняя стабильная версия)

## Зачем нужны эти пакеты?

### MathNet.Numerics
- **Назначение**: Замена Python scipy для гиперболической интерполяции
- **Используется в**: `Core/NormStorage.cs`
- **Функционал**: Метод наименьших квадратов (МНК) для подгонки кривых

### ClosedXML
- **Назначение**: Чтение Excel файлов (замена Python openpyxl)
- **Используется в**: `Core/CoefficientsManager.cs`
- **Функционал**: Загрузка коэффициентов локомотивов из Excel
- **Почему ClosedXML**: Open-source, не требует лицензии (в отличие от EPPlus)

## Проверка установки

После установки пакетов убедитесь, что:
1. Проект компилируется без ошибок
2. В References появились:
   - MathNet.Numerics
   - ClosedXML
   - ClosedXML.Parser (зависимость ClosedXML)
   - DocumentFormat.OpenXml (зависимость ClosedXML)

## Уже установленные пакеты (из Чата 1):
- Serilog
- Serilog.Sinks.File  
- Serilog.Sinks.Console
- Newtonsoft.Json
