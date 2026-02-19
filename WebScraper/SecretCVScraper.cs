using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace WebScraper
{
    /// <summary>
    /// SecretCV sitesi için özel scraper sýnýfý.
    /// Þehir bazlý ilanlarý sayfalama ile tarar.
    /// </summary>
    public class SecretCVScraper : IDisposable
    {
        private IWebDriver _driver;
        private WebDriverWait _wait;
        private Action<string> _logger = (s) => { };
        private bool _disposed = false;
        private const int PAGE_LOAD_WAIT = 3000; // 3 saniye
        private const int SCROLL_WAIT = 1000; // 1 saniye

        public SecretCVScraper()
        {
            InitDriver();
        }

        public void SetLogger(Action<string> logger)
        {
            _logger = logger ?? ((s) => { });
        }

        public void Log(string message)
        {
            try { _logger?.Invoke(message); } catch { }
            Console.WriteLine($"[SecretCV] {message}");
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
            options.AddArgument("--disable-blink-features=AutomationControlled");
            options.AddArgument("user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

            var service = ChromeDriverService.CreateDefaultService();
            service.HideCommandPromptWindow = true;

            _driver = new ChromeDriver(service, options, TimeSpan.FromSeconds(180));
            _wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(20));
            _driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(60);
            _driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(5);
        }

        /// <summary>
        /// Belirtilen þehir için tüm ilanlarý sayfa sayfa tarar
        /// </summary>
        public async Task<List<SecretCVJobListing>> ScrapeCity(string cityName, string baseUrl)
        {
            var allJobs = new List<SecretCVJobListing>();

            try
            {
                // Ýlk sayfaya git ve toplam ilan sayýsýný al
                _driver.Navigate().GoToUrl(baseUrl);
                Log($"[{cityName}] Ana sayfa açýldý: {baseUrl}");
                Thread.Sleep(PAGE_LOAD_WAIT);

                // Sayfayý biraz scroll et (lazy loading için)
                ScrollPage();

                // Toplam ilan sayýsýný bul
                int totalJobs = GetTotalJobCount();
                Log($"[{cityName}] Toplam ilan sayýsý: {totalJobs}");

                if (totalJobs == 0)
                {
                    Log($"[{cityName}] Ýlan bulunamadý.");
                    return allJobs;
                }

                // 50'þer listeleme ile toplam sayfa sayýsýný hesapla
                int totalPages = (int)Math.Ceiling(totalJobs / 50.0);
                Log($"[{cityName}] Toplam sayfa sayýsý: {totalPages}");

                // Her sayfayý tara
                for (int pageNum = 1; pageNum <= totalPages; pageNum++)
                {
                    try
                    {
                        var pageJobs = await ScrapePage(cityName, baseUrl, pageNum);
                        allJobs.AddRange(pageJobs);
                        Log($"[{cityName}] Sayfa {pageNum}/{totalPages} tamamlandý. Bu sayfada {pageJobs.Count} ilan bulundu.");

                        // Rate limiting - siteden ban yememek için
                        if (pageNum < totalPages)
                        {
                            Thread.Sleep(2000);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"[{cityName}] Sayfa {pageNum} taranýrken hata: {ex.Message}");
                    }
                }

                Log($"[{cityName}] Tarama tamamlandý. Toplam {allJobs.Count} ilan toplandý.");
            }
            catch (Exception ex)
            {
                Log($"[{cityName}] Genel tarama hatasý: {ex.Message}");
            }

            return allJobs;
        }

        /// <summary>
        /// Belirtilen sayfadaki ilanlarý tarar
        /// </summary>
        private async Task<List<SecretCVJobListing>> ScrapePage(string cityName, string baseUrl, int pageNumber)
        {
            var jobs = new List<SecretCVJobListing>();

            try
            {
                // Sayfa URL'sini oluþtur
                string pageUrl;
                if (pageNumber == 1)
                {
                    pageUrl = baseUrl;
                }
                else
                {
                    // URL yapýsý: ...?l=50&sf=2 (2. sayfa için)
                    if (baseUrl.Contains("?"))
                    {
                        pageUrl = baseUrl + $"&sf={pageNumber}";
                    }
                    else
                    {
                        pageUrl = baseUrl + $"?l=50&sf={pageNumber}";
                    }
                }

                _driver.Navigate().GoToUrl(pageUrl);
                Log($"[{cityName}] Sayfa {pageNumber} yükleniyor: {pageUrl}");
                Thread.Sleep(PAGE_LOAD_WAIT);

                // Sayfayý scroll et (lazy loading için)
                ScrollPage();

                // Ýlan kartlarýný bul
                var jobCards = _driver.FindElements(By.CssSelector("div.cv-job-box.job-list.job-search-cv-box")).ToList();

                // Reklam kartlarýný filtrele
                jobCards = jobCards.Where(card =>
                {
                    var classes = card.GetAttribute("class");
                    return !classes.Contains("cv-ads");
                }).ToList();

                Log($"[{cityName}] Sayfa {pageNumber}'de {jobCards.Count} ilan kartý bulundu.");

                foreach (var jobCard in jobCards)
                {
                    try
                    {
                        var job = ExtractJobFromCard(jobCard, cityName);
                        if (job != null && !string.IsNullOrEmpty(job.JobTitle))
                        {
                            // Detay sayfasýndan bilgileri al
                            if (!string.IsNullOrEmpty(job.JobUrl))
                            {
                                ScrapeJobDetails(job);
                            }
                            jobs.Add(job);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"[{cityName}] Sayfa {pageNumber} - Kart çýkarýlýrken hata: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"[{cityName}] Sayfa {pageNumber} taranýrken kritik hata: {ex.Message}");
            }

            await Task.CompletedTask;
            return jobs;
        }

        /// <summary>
        /// Ýlan kartýndan bilgileri çýkarýr
        /// </summary>
        private SecretCVJobListing ExtractJobFromCard(IWebElement jobCard, string cityName)
        {
            var job = new SecretCVJobListing
            {
                SourceCity = cityName
            };

            try
            {
                // Ýþ baþlýðý - div.body > a.title
                job.JobTitle = SafeGetText(jobCard, By.CssSelector("div.body a.title"));

                // Firma adý - div.body > a.company
                job.CompanyName = SafeGetText(jobCard, By.CssSelector("div.body a.company"));

                // Lokasyon - span.city içindeki ilk span
                var citySpan = jobCard.FindElements(By.CssSelector("span.city span")).FirstOrDefault();
                if (citySpan != null)
                {
                    job.Location = citySpan.Text.Trim();
                }

                // Ýlçe bilgisi - span.city içindeki ikinci span (varsa)
                var districtSpan = jobCard.FindElements(By.CssSelector("span.city span.fs-11")).FirstOrDefault();
                if (districtSpan != null)
                {
                    var districtText = districtSpan.Text.Trim();
                    // "Akyurt, Altýndað, Ayaþ, Bala, Beypaza..." gibi
                    job.District = districtText;
                }

                // Ýlan tarihi - small.text-muted
                var dateElement = jobCard.FindElements(By.CssSelector("span.city small.text-muted")).FirstOrDefault();
                if (dateElement != null)
                {
                    var dateText = dateElement.Text.Replace("Ýlan Tarihi:", "").Trim();
                    job.PostDate = dateText;
                }

                // Ýlan URL'si - baþlýk linkinden
                var titleLink = jobCard.FindElements(By.CssSelector("div.body a.title")).FirstOrDefault();
                if (titleLink != null)
                {
                    var href = titleLink.GetAttribute("href");
                    if (!string.IsNullOrEmpty(href))
                    {
                        if (href.StartsWith("http"))
                        {
                            job.JobUrl = href;
                        }
                        else
                        {
                            job.JobUrl = "https://www.secretcv.com" + href;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"[{cityName}] Kart bilgisi çýkarýlýrken hata: {ex.Message}");
            }

            return job;
        }

        /// <summary>
        /// Ýlan detay sayfasýndan bilgileri çeker
        /// </summary>
        private void ScrapeJobDetails(SecretCVJobListing job)
        {
            string originalWindow = null;
            try
            {
                originalWindow = _driver.CurrentWindowHandle;
                
                // Yeni sekme aç
                ((IJavaScriptExecutor)_driver).ExecuteScript("window.open();");
                var tabs = _driver.WindowHandles.ToList();
                var newTab = tabs.Last();
                _driver.SwitchTo().Window(newTab);
                
                // Detay sayfasýna git
                _driver.Navigate().GoToUrl(job.JobUrl);
                Log($"Detay sayfasý açýlýyor: {job.JobTitle}");
                Thread.Sleep(2000);

                // Sayfanýn yüklenmesini bekle
                _wait.Until(driver => driver.FindElements(By.CssSelector("div.cv-card.content-job")).Any());

                // Ýþ Açýklamasý
                try
                {
                    var jobDescElements = _driver.FindElements(By.XPath("//span[contains(text(), 'Ýþ Açýklamasý')]/following-sibling::p")).ToList();
                    if (jobDescElements.Any())
                    {
                        var descriptions = jobDescElements.Select(p => p.GetAttribute("innerText")?.Trim()).Where(t => !string.IsNullOrEmpty(t));
                        job.JobDescription = string.Join("\n\n", descriptions);
                    }
                    else
                    {
                        // Alternatif: Tüm p etiketlerini al (Ýþ Açýklamasý baþlýðýndan sonraki)
                        var contentJobDiv = _driver.FindElements(By.CssSelector("div.cv-card.content-job")).FirstOrDefault();
                        if (contentJobDiv != null)
                        {
                            var allParagraphs = contentJobDiv.FindElements(By.TagName("p")).ToList();
                            if (allParagraphs.Any())
                            {
                                // Ýlk p etiketlerini al (genelde iþ açýklamasý burada)
                                var firstParagraphs = allParagraphs.Take(3).Select(p => p.GetAttribute("innerText")?.Trim()).Where(t => !string.IsNullOrEmpty(t));
                                job.JobDescription = string.Join("\n\n", firstParagraphs);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log($"Ýþ açýklamasý çekilirken hata: {ex.Message}");
                }

                // Ýstenen Yetenek ve Uzmanlýklar
                try
                {
                    var skillsElements = _driver.FindElements(By.XPath("//span[contains(text(), 'Ýstenen Yetenek ve Uzmanlýklar')]/following-sibling::p")).ToList();
                    if (skillsElements.Any())
                    {
                        var skills = skillsElements.Select(p => p.GetAttribute("innerText")?.Trim()).Where(t => !string.IsNullOrEmpty(t));
                        job.RequiredSkills = string.Join("\n\n", skills);
                    }
                    else
                    {
                        // Alternatif: "Ýstenen Yetenek" baþlýðýndan sonraki ul li'leri al
                        var skillsUl = _driver.FindElements(By.XPath("//span[contains(text(), 'Ýstenen Yetenek ve Uzmanlýklar')]/following-sibling::ul")).FirstOrDefault();
                        if (skillsUl != null)
                        {
                            var liElements = skillsUl.FindElements(By.TagName("li"));
                            var skillsList = liElements.Select(li => "• " + li.Text.Trim()).Where(t => !string.IsNullOrEmpty(t));
                            job.RequiredSkills = string.Join("\n", skillsList);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log($"Yetenek bilgisi çekilirken hata: {ex.Message}");
                }

                Log($"Detay bilgileri alýndý: {job.JobTitle}");
            }
            catch (Exception ex)
            {
                Log($"Detay sayfasý çekilirken hata ({job.JobUrl}): {ex.Message}");
            }
            finally
            {
                // Sekmeyi kapat ve ana sekmeye dön
                if (originalWindow != null)
                {
                    try
                    {
                        _driver.Close();
                        _driver.SwitchTo().Window(originalWindow);
                    }
                    catch { }
                }
            }
        }

        /// <summary>
        /// Toplam ilan sayýsýný sayfadan çeker
        /// </summary>
        private int GetTotalJobCount()
        {
            try
            {
                // "872 Ýþ Ýlaný Listelendi" gibi bir metin arýyoruz
                var totalJobElement = _driver.FindElements(By.CssSelector("span.total-job-text")).FirstOrDefault();
                if (totalJobElement != null)
                {
                    var text = totalJobElement.Text.Trim();
                    // Sayýyý çýkar: "872 Ýþ Ýlaný Listelendi" -> 872
                    var match = Regex.Match(text, @"(\d+)\s*Ýþ Ýlaný");
                    if (match.Success)
                    {
                        return int.Parse(match.Groups[1].Value);
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Toplam ilan sayýsý alýnýrken hata: {ex.Message}");
            }

            return 0;
        }

        /// <summary>
        /// Sayfayý aþaðý kaydýrýr (lazy loading için)
        /// </summary>
        private void ScrollPage()
        {
            try
            {
                // Sayfanýn sonuna kadar scroll et
                for (int i = 0; i < 3; i++)
                {
                    ((IJavaScriptExecutor)_driver).ExecuteScript("window.scrollTo(0, document.body.scrollHeight);");
                    Thread.Sleep(SCROLL_WAIT);
                }

                // Baþa dön
                ((IJavaScriptExecutor)_driver).ExecuteScript("window.scrollTo(0, 0);");
                Thread.Sleep(500);
            }
            catch { }
        }

        /// <summary>
        /// Element bulma ve text çýkarma iþlemini güvenli yapar
        /// </summary>
        private string SafeGetText(IWebElement parent, By by)
        {
            try
            {
                var element = parent.FindElement(by);
                return element?.Text?.Trim() ?? "";
            }
            catch (NoSuchElementException)
            {
                return "";
            }
            catch (StaleElementReferenceException)
            {
                return "";
            }
            catch
            {
                return "";
            }
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
