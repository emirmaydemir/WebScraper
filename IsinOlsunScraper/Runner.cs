using System;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using ElemanNetScraper;

namespace IsinOlsunScraper
{
    public class Runner
    {
        private readonly string _basePath;

        public Runner()
        {
            _basePath = AppContext.BaseDirectory;
        }

        public async Task RunAllCitiesAsync(CityConfig config)
        {
            var cities = config.Cities;

            foreach (var city in cities)
            {
                try
                {
                    if (city.Districts != null && city.Districts.Count > 0)
                    {
                        foreach (var district in city.Districts)
                        {
                            if (string.IsNullOrWhiteSpace(district.Url))
                            {
                                Log(city.Name, district.Name, "URL boş — atlandı.");
                                continue;
                            }

                            Console.WriteLine($"\n--- {city.Name} / {district.Name} başlatılıyor ---");
                            await RunCityInstanceAsync(city.Name, district.Name, district.Url);
                        }
                    }
                    else
                    {
                        if (string.IsNullOrWhiteSpace(city.Url))
                        {
                            Log(city.Name, null, "URL boş — atlandı.");
                            continue;
                        }

                        Console.WriteLine($"\n--- {city.Name} başlatılıyor ---");
                        await RunCityInstanceAsync(city.Name, null, city.Url);
                    }
                }
                catch (Exception ex)
                {
                    Log(city.Name, null, $"GENEL HATA: {ex}");
                }
            }
        }

        private async Task RunCityInstanceAsync(string cityName, string districtName, string url)
        {
            var logPrefix = districtName == null ? cityName : $"{cityName}_{districtName}";
            var logFile = Path.Combine(_basePath, "logs", $"{SanitizeFileName(logPrefix)}.log");

            try
            {
                using (var scraper = new Scraper())
                {
                    scraper.SetLogger((msg) =>
                    {
                        File.AppendAllText(logFile, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {msg}\n");
                    });

                    var excelName = districtName == null
                        ? $"ilanlar_{SanitizeFileName(cityName)}.xlsx"
                        : $"ilanlar_{SanitizeFileName(cityName)}_{SanitizeFileName(districtName)}.xlsx";

                    var excelPath = Path.Combine(_basePath, "output", excelName);
                    var writer = new ExcelWriter(excelPath);

                    await scraper.StartAsync(url, (pageIndex, ilanList) =>
                    {
                        try
                        {
                            writer.AppendIlanlar(ilanList);
                            scraper.Log($"Sayfa {pageIndex} yazıldı -> {excelName} ({ilanList.Count} ilan).");
                        }
                        catch (Exception ex)
                        {
                            scraper.Log($"Excel yazma hatası sayfa {pageIndex}: {ex.Message}");
                        }
                    });

                    scraper.Log($"Tamamlandı: {cityName} {(districtName != null ? "/" + districtName : "")}");
                }
            }
            catch (Exception ex)
            {
                File.AppendAllText(logFile, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - CRITICAL: {ex}\n");
            }
            finally
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                await Task.Delay(1000);
            }
        }

        private static string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return string.Concat(name.Where(ch => !invalid.Contains(ch))).Replace(" ", "_");
        }

        private void Log(string city, string district, string message)
        {
            var logFile = Path.Combine(_basePath, "logs", $"{SanitizeFileName(city)}.log");
            var prefix = district == null ? city : $"{city}_{district}";
            File.AppendAllText(logFile, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {prefix} - {message}\n");
        }
    }
}
