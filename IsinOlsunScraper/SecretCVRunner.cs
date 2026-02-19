using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace IsinOlsunScraper
{
    /// <summary>
    /// SecretCV tarama iþlemlerini yöneten ana sýnýf
    /// </summary>
    public class SecretCVRunner
    {
        private readonly string _basePath;
        private readonly string _outputPath;
        private readonly string _logsPath;

        public SecretCVRunner()
        {
            _basePath = AppContext.BaseDirectory;
            _outputPath = Path.Combine(_basePath, "output");
            _logsPath = Path.Combine(_basePath, "logs");

            // Klasörleri oluþtur
            Directory.CreateDirectory(_outputPath);
            Directory.CreateDirectory(_logsPath);
        }

        /// <summary>
        /// Tüm þehirleri tarar
        /// </summary>
        public async Task RunAllCitiesAsync(CityConfigRoot config)
        {
            if (config?.Cities == null || config.Cities.Count == 0)
            {
                Console.WriteLine("Þehir yapýlandýrmasý bulunamadý!");
                return;
            }

            Console.WriteLine($"Toplam {config.Cities.Count} þehir taranacak.");
            Console.WriteLine("=====================================\n");

            int cityIndex = 0;
            foreach (var city in config.Cities)
            {
                cityIndex++;
                try
                {
                    Console.WriteLine($"[{cityIndex}/{config.Cities.Count}] {city.Name} taranýyor...");
                    
                    // Þehrin ilçeleri varsa onlarý tara, yoksa ana URL'yi tara
                    if (city.Districts != null && city.Districts.Count > 0)
                    {
                        await ScrapeDistrictsAsync(city);
                    }
                    else
                    {
                        await ScrapeCityAsync(city.Name, city.Url);
                    }

                    Console.WriteLine($"[{cityIndex}/{config.Cities.Count}] {city.Name} tamamlandý!\n");
                }
                catch (Exception ex)
                {
                    LogError(city.Name, $"Þehir taranýrken hata: {ex.Message}");
                    Console.WriteLine($"[{cityIndex}/{config.Cities.Count}] {city.Name} - HATA!\n");
                }

                // Þehirler arasý bekleme (rate limiting)
                if (cityIndex < config.Cities.Count)
                {
                    await Task.Delay(2000);
                }
            }

            Console.WriteLine("\n=====================================");
            Console.WriteLine("TÜM ÞEHÝRLER TARAMA TAMAMLANDI!");
            Console.WriteLine("=====================================");
        }

        /// <summary>
        /// Tek bir þehri tarar
        /// </summary>
        private async Task ScrapeCityAsync(string cityName, string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                Log(cityName, "URL boþ - atlandý.");
                return;
            }

            var logFile = Path.Combine(_logsPath, $"{SanitizeFileName(cityName)}.log");
            var excelFile = Path.Combine(_outputPath, $"{SanitizeFileName(cityName)}.xlsx");

            try
            {
                Log(cityName, "Tarama baþlýyor...");

                using (var scraper = new SecretCVScraper())
                {
                    scraper.SetLogger((msg) =>
                    {
                        File.AppendAllText(logFile, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {msg}\n");
                    });

                    // Þehri tara
                    var jobs = await scraper.ScrapeCity(cityName, url);

                    Log(cityName, $"Toplam {jobs.Count} ilan bulundu.");

                    // Excel'e yaz
                    if (jobs.Count > 0)
                    {
                        var excelWriter = new SecretCVExcelWriter(excelFile);
                        excelWriter.AppendJobs(jobs);
                        Log(cityName, $"Excel dosyasý oluþturuldu: {excelFile}");
                    }
                    else
                    {
                        Log(cityName, "Ýlan bulunamadý, Excel dosyasý oluþturulmadý.");
                    }
                }

                Log(cityName, "Tarama tamamlandý.");
            }
            catch (Exception ex)
            {
                LogError(cityName, $"Kritik hata: {ex.Message}\n{ex.StackTrace}");
                throw;
            }
            finally
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                await Task.Delay(1000);
            }
        }

        /// <summary>
        /// Þehrin ilçelerini tarar
        /// </summary>
        private async Task ScrapeDistrictsAsync(City city)
        {
            if (city.Districts == null || city.Districts.Count == 0)
            {
                return;
            }

            Console.WriteLine($"  {city.Name} için {city.Districts.Count} ilçe taranacak...");

            var allJobs = new List<SecretCVJobListing>();
            var logFile = Path.Combine(_logsPath, $"{SanitizeFileName(city.Name)}.log");

            int districtIndex = 0;
            foreach (var district in city.Districts)
            {
                districtIndex++;
                try
                {
                    Console.WriteLine($"    [{districtIndex}/{city.Districts.Count}] {district.Name} taranýyor...");

                    using (var scraper = new SecretCVScraper())
                    {
                        scraper.SetLogger((msg) =>
                        {
                            File.AppendAllText(logFile, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - [{district.Name}] {msg}\n");
                        });

                        var jobs = await scraper.ScrapeCity($"{city.Name}/{district.Name}", district.Url);
                        allJobs.AddRange(jobs);

                        Log(city.Name, $"{district.Name}: {jobs.Count} ilan bulundu.");
                    }
                }
                catch (Exception ex)
                {
                    LogError(city.Name, $"{district.Name} taranýrken hata: {ex.Message}");
                }

                // Ýlçeler arasý bekleme
                if (districtIndex < city.Districts.Count)
                {
                    await Task.Delay(1500);
                }
            }

            // Tüm ilçelerin ilanlarýný tek Excel'e yaz
            if (allJobs.Count > 0)
            {
                var excelFile = Path.Combine(_outputPath, $"{SanitizeFileName(city.Name)}.xlsx");
                var excelWriter = new SecretCVExcelWriter(excelFile);
                excelWriter.AppendJobs(allJobs);
                Log(city.Name, $"Toplam {allJobs.Count} ilan Excel'e yazýldý: {excelFile}");
            }
            else
            {
                Log(city.Name, "Hiç ilan bulunamadý.");
            }
        }

        /// <summary>
        /// Log mesajý yazar
        /// </summary>
        private void Log(string cityName, string message)
        {
            var logFile = Path.Combine(_logsPath, $"{SanitizeFileName(cityName)}.log");
            var logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}";
            
            try
            {
                File.AppendAllText(logFile, logMessage + "\n");
            }
            catch { }

            Console.WriteLine($"  [{cityName}] {message}");
        }

        /// <summary>
        /// Hata logu yazar
        /// </summary>
        private void LogError(string cityName, string message)
        {
            var logFile = Path.Combine(_logsPath, $"{SanitizeFileName(cityName)}_errors.log");
            var logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - ERROR - {message}";

            try
            {
                File.AppendAllText(logFile, logMessage + "\n");
            }
            catch { }

            Console.WriteLine($"  [HATA - {cityName}] {message}");
        }

        /// <summary>
        /// Dosya adýný temizler
        /// </summary>
        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "unnamed";
            }

            var invalid = Path.GetInvalidFileNameChars();
            var sanitized = string.Concat(name.Where(ch => !invalid.Contains(ch)))
                .Replace(" ", "_")
                .Replace("/", "_");

            return string.IsNullOrWhiteSpace(sanitized) ? "unnamed" : sanitized;
        }
    }
}
