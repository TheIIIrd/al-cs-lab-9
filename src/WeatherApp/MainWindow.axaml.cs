using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace WeatherApp
{
    public partial class MainWindow : Window
    {
        private class Town
        {
            public string Name { get; set; }
            public double Latitude { get; set; }
            public double Longitude { get; set; }
            public override string ToString() => Name;
        }

        private class WeatherDetails
        {
            public string Name { get; set; } = "";
            public string Country { get; set; } = "";
            public double Temperature { get; set; }
            public string Conditions { get; set; } = "";
            public override string ToString() =>
                $"Погода в {Name}, {Country}:\n" +
                $"Температура: {Temperature:F1}°C\n" +
                $"Условия: {Conditions}";
        }

        private class WeatherService
        {
            private readonly HttpClient _httpClient;
            private readonly string _apiKey;

            public WeatherService(string apiKey)
            {
                _httpClient = new HttpClient();
                _apiKey = apiKey;
            }

            public async Task<WeatherDetails> FetchWeatherAsync(double lat, double lon)
            {
                var response = await _httpClient.GetStringAsync(
                    $"https://api.openweathermap.org/data/2.5/weather?lat={lat}&lon={lon}&appid={_apiKey}&units=metric&lang=ru");
                var jsonDocument = JsonDocument.Parse(response);
                var root = jsonDocument.RootElement;
                var sys = root.GetProperty("sys");
                var main = root.GetProperty("main");
                var weather = root.GetProperty("weather")[0];
                return new WeatherDetails
                {
                    Country = sys.GetProperty("country").GetString()!,
                    Name = root.GetProperty("name").GetString()!,
                    Temperature = main.GetProperty("temp").GetDouble(),
                    Conditions = weather.GetProperty("description").GetString()!
                };
            }
        }

        private WeatherService _weatherService;
        private List<Town> _towns;

        public MainWindow()
        {
            InitializeComponent();
            LoadTowns();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private async void LoadTowns()
        {
            try
            {
                var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "city.txt");
                var lines = File.ReadAllLines(path);
                _towns = new List<Town>();

                foreach (var line in lines)
                {
                    if (string.IsNullOrEmpty(line)) continue;

                    var parts = line.Split('\t');
                    if (parts.Length == 2)
                    {
                        var townName = parts[0].Trim();
                        var coords = parts[1].Split(',');
                        if (coords.Length == 2)
                        {
                            if (double.TryParse(coords[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double lat) &&
                                double.TryParse(coords[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double lon))
                            {
                                _towns.Add(new Town
                                {
                                    Name = townName,
                                    Latitude = lat,
                                    Longitude = lon
                                });
                            }
                        }
                    }
                }

                _weatherService = new WeatherService("API_KEY"); // Замените "API_KEY" на ваш настоящий ключ.
                // Привязка данных к ComboBox
                var citiesList = this.FindControl<ComboBox>("CitiesList");
                citiesList.Items = _towns;
            }
            catch (IOException ex)
            {
                await ShowError($"Ошибка при загрузке городов: {ex.Message}");
            }
            catch (Exception ex)
            {
                await ShowError($"Произошла ошибка: {ex.Message}");
            }
        }

        private async void GetWeatherButton_Click(object sender, RoutedEventArgs e)
        {
            var citiesList = this.FindControl<ComboBox>("CitiesList");
            var selectedTown = citiesList.SelectedItem as Town;

            if (selectedTown == null) return;

            var weatherData = await _weatherService.FetchWeatherAsync(selectedTown.Latitude, selectedTown.Longitude);
            var weatherInfo = this.FindControl<TextBox>("WeatherInfo");
            weatherInfo.Text = weatherData.ToString();
        }

        private async Task ShowError(string message)
        {
            await MessageBox.Show(this, message, "Ошибка", MessageBox.MessageBoxButtons.Ok);
        }
    }
}
