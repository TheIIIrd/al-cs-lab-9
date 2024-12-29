/*
Задание 1
Создайте многопоточное приложение для получения средних цен акций за год.

Используйте сайт https://finance.yahoo.com/ для получения дневных котировок
списка акций из файла ticker.txt. Формат ссылки следующий:
<тут_очень_большая_ссылка> ,где:
- Код_бумаги – тикер из списка акций
- Начальная_дата – метка времени начала запрашиваемого периода в UNIX формате (год назад).
- Конечная_дата – метка времени конца запрашиваемого периода в UNIX формате (текущая дата).

Например, формат ссылки для AAPL:
<тут_еще_одна_очень_большая_ссылка>

По мере получения данных выполните запуск задачи(Task), которая будет считать среднюю
цену акции за год (используйте среднее значение для каждого дня как (High+Low)/2.
Сложите все полученные значения и поделите на число дней).

Результатом работы задачи будет являться среднее значение цены за год, которое необходимо
вывести в файл в формате «Тикер:Цена». При этом обеспечьте потокобезопасный доступ к
файлу между всеми задачами.
*/

using System.Collections.Concurrent;  // Для потокобезопасных коллекций
using System.Text.Json;  // Для сериализации и десериализации JSON

class StockAverageCalculator
{
    // HttpClient для выполнения запросов с тайм-аутом
    private static readonly HttpClient httpClient = new HttpClient() { Timeout = TimeSpan.FromSeconds(30) };
    
    // Аутентификационный токен для API
    private static readonly string apiKey = "API_KEY";
    
    // Путь к выходному файлу для записи результатов
    private static readonly string outputPath = "results.txt";
    
    // Семафор для ограничения параллельных задач (5 одновременных запросов)
    private static readonly SemaphoreSlim throttler = new SemaphoreSlim(5);
    
    // Счетчик завершенных запросов
    private static int completedRequests = 0;
    
    // Потокобезопасный словарь для хранения средних цен акций
    private static ConcurrentDictionary<string, double> averages = new ConcurrentDictionary<string, double>();

    static async Task Main()
    {
        // Загружаем список тикеров из файла
        var tickerList = await LoadTickersAsync("ticker.txt");
        var currentDate = DateTime.Now; // Текущая дата
        var startDate = currentDate.AddMonths(-11); // Дата год назад

        // Устанавливаем заголовки для HTTP-запросов
        SetHttpClientHeaders(apiKey);

        var tasksCollection = new List<Task>();
        
        // Обрабатываем тикеры партиями по 25 штук
        for (int i = 0; i < tickerList.Length; i += 25)
        {
            var batch = tickerList.Skip(i).Take(25);
            tasksCollection.AddRange(batch.Select(ticker => ProcessTickerInBatchAsync(ticker, startDate, currentDate)));
        }

        // Ожидаем завершения всех задач
        await Task.WhenAll(tasksCollection);

        // Сортируем и записываем результаты в файл
        var sortedAverages = averages.OrderBy(r => r.Key).Select(r => $"{r.Key}:{r.Value:F2}");
        await File.WriteAllLinesAsync(outputPath, sortedAverages);
    }

    // Асинхронный метод для загрузки тикеров из файла
    private static async Task<string[]> LoadTickersAsync(string filePath)
    {
        var tickers = await File.ReadAllLinesAsync(filePath); // Читаем строки из файла
        // Возвращаем только непустые и обрезанные строки
        return tickers.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()).ToArray();
    }

    // Метод для настройки заголовков HttpClient
    private static void SetHttpClientHeaders(string token)
    {
        httpClient.DefaultRequestHeaders.Clear(); // Очистка заголовков
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}"); // Добавление токена
        httpClient.DefaultRequestHeaders.Add("Accept", "application/json"); // Устанавливаем прием JSON
    }

    // Асинхронный метод для обработки тикеров в группе
    private static async Task ProcessTickerInBatchAsync(string ticker, DateTime startDate, DateTime endDate)
    {
        await throttler.WaitAsync(); // Ожидаем освобождения семафора
        try
        {
            // Запускаем расчет средней цены для тикера
            await FetchAndCalculateAverageAsync(ticker, startDate, endDate);
        }
        finally
        {
            throttler.Release(); // Освобождаем семафор
        }
    }

    // Асинхронный метод получения данных о цене и расчета средней цены
    private static async Task FetchAndCalculateAverageAsync(string ticker, DateTime startDate, DateTime endDate)
    {
        try
        {
            // Формируем URL для запроса
            string apiUrl = $"https://api.marketdata.app/v1/stocks/candles/D/{ticker}/?from={startDate:yyyy-MM-dd}&to={endDate:yyyy-MM-dd}&format=json&adjusted=true";
            var response = await httpClient.GetStringAsync(apiUrl); // Выполняем запрос
            var stockData = JsonSerializer.Deserialize<StockInfo>(response); // Десериализуем ответ

            // Рассчитываем среднюю цену
            double totalAverage = stockData.h.Select((high, index) => (high + stockData.l[index]) / 2).Average();
            averages.TryAdd(ticker, totalAverage); // Добавляем среднюю цену в словарь
            Interlocked.Increment(ref completedRequests); // Увеличиваем счетчик завершенных запросов
        }
        catch
        {
            // Здесь можно обработать ошибку при запросе
        }
    }
}

// Класс для представления данных о ценах акции
class StockInfo
{
    public double[] o { get; set; } // Цены открытия
    public double[] h { get; set; } // Максимальные цены
    public double[] l { get; set; } // Минимальные цены
    public double[] c { get; set; } // Цены закрытия
    public long[] v { get; set; } // Объемы торгов
}
