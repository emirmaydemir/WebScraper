using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace WebScraper
{
    /// <summary>
    /// SecretCV sitesinden şehir listesini otomatik çeken sınıf
    /// </summary>
    public class CityScraper : IDisposable
    {
        private IWebDriver _driver;
        private bool _disposed = false;

        public CityScraper()
        {
            InitDriver();
        }

        private void InitDriver()
        {
            var options = new ChromeOptions();
            options.AddArgument("--headless");
            options.AddArgument("--disable-gpu");
            options.AddArgument("--no-sandbox");
            options.AddArgument("--disable-dev-shm-usage");
            options.AddArgument("--window-size=1920,1080");
            options.AddArgument("--lang=tr-TR");

            var service = ChromeDriverService.CreateDefaultService();
            service.HideCommandPromptWindow = true;

            _driver = new ChromeDriver(service, options, TimeSpan.FromSeconds(180));
            _driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(60);
            _driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(5);
        }

        /// <summary>
        /// SecretCV sitesinden tüm şehirleri çeker
        /// </summary>
        public List<City> GetAllCities()
        {
            var cities = new List<City>();

            try
            {
                Console.WriteLine("🌍 SecretCV sitesinden şehir listesi çekiliyor...");

                // Ana sayfaya git
                _driver.Navigate().GoToUrl("https://www.secretcv.com/is-ilanlari");
                Thread.Sleep(2000);

                // Şehir checkbox'larını bul
                var cityCheckboxes = _driver.FindElements(By.CssSelector("div.form-check.city-form-check input.filter-city-input")).ToList();

                Console.WriteLine($"✓ {cityCheckboxes.Count} şehir bulundu.");

                foreach (var checkbox in cityCheckboxes)
                {
                    try
                    {
                        var cityId = checkbox.GetAttribute("id");
                        var cityName = checkbox.GetAttribute("data-name");
                        var cityValue = checkbox.GetAttribute("value");

                        if (string.IsNullOrEmpty(cityName) || string.IsNullOrEmpty(cityId))
                        {
                            continue;
                        }

                        // URL'yi oluştur
                        // ID'den URL slug'ını al (örn: "istanbul-anadolu" -> "istanbul-anadolu-is-ilanlari")
                        var urlSlug = cityId;
                        var cityUrl = $"https://www.secretcv.com/is-ilanlari/{urlSlug}-is-ilanlari?l=50";

                        var city = new City
                        {
                            Name = SanitizeCityName(cityName),
                            Url = cityUrl
                        };

                        cities.Add(city);

                        Console.WriteLine($"  • {city.Name} - {cityUrl}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  ⚠️ Şehir bilgisi alınırken hata: {ex.Message}");
                    }
                }

                Console.WriteLine($"\n✓ Toplam {cities.Count} şehir başarıyla alındı!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Şehir listesi alınırken kritik hata: {ex.Message}");
            }

            return cities;
        }

        /// <summary>
        /// Şehir adını dosya adı için güvenli hale getirir
        /// </summary>
        private string SanitizeCityName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "Unknown";

            // Türkçe karakterleri koru, sadece özel karakterleri temizle
            var sanitized = name.Trim();

            // Birden fazla boşluğu tek boşluğa çevir
            sanitized = Regex.Replace(sanitized, @"\s+", " ");

            // Dosya adı için güvenli olmayan karakterleri kaldır
            var invalidChars = System.IO.Path.GetInvalidFileNameChars();
            sanitized = string.Concat(sanitized.Where(ch => !invalidChars.Contains(ch)));

            return sanitized;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            if (disposing)
            {
                TryQuitDriver();
            }
            _disposed = true;
        }

        private void TryQuitDriver()
        {
            try
            {
                _driver?.Quit();
                _driver?.Dispose();
            }
            catch { }
            _driver = null;
        }
    }
}
