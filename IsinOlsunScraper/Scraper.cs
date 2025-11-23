using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace IsinOlsunScraper
{
    public class Scraper : IDisposable
    {
        private IWebDriver _driver;
        private WebDriverWait _wait;
        private Action<string> _logger = (s) => { };
        private bool _disposed = false;

        public Scraper()
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
            Console.WriteLine($"[Scraper] {message}");
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
            _wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(15));
            _driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(60);
        }

        public async Task StartAsync(string baseUrl, Action<int, List<IlanBilgisi>> pageCallback)
        {
            try
            {
                _driver.Navigate().GoToUrl(baseUrl);
                Thread.Sleep(1500);

                int totalPages = GetTotalPagesFallback();

                Log($"Found total pages: {totalPages} (baseUrl: {baseUrl})");

                for (int p = 1; p <= totalPages; p++)
                {
                    try
                    {
                        string pageUrl = p == 1 ? baseUrl : $"{baseUrl}&pn={p}";
                        _driver.Navigate().GoToUrl(pageUrl);
                        Log($"Navigate -> {pageUrl}");
                        Thread.Sleep(1200);

                        int ilanSayisi = 0;
                        int maxScrollAttempts = 5;
                        for (int i = 0; i < maxScrollAttempts; i++)
                        {
                            ((IJavaScriptExecutor)_driver).ExecuteScript("window.scrollTo(0, document.body.scrollHeight);");
                            Thread.Sleep(1500);

                            var currentIlanlar = _driver.FindElements(By.CssSelector("div._3A7p")).Count;

                            if (currentIlanlar == ilanSayisi && currentIlanlar > 0 || currentIlanlar >= 40)
                            {
                                Log($"Scroll {i + 1} sonrası {currentIlanlar} ilana ulaşıldı. Yükleme tamamlandı varsayılıyor.");
                                break;
                            }

                            ilanSayisi = currentIlanlar;

                            if (i == maxScrollAttempts - 1)
                            {
                                Log($"Maksimum {maxScrollAttempts} kaydırma denemesine ulaşıldı. ({ilanSayisi} ilan ile devam ediliyor.)");
                            }
                        }

                        var ilanlar = ScrapeCurrentListingPage();
                        Log($"Sayfa {p} - {ilanlar.Count} ilan bulundu.");

                        pageCallback?.Invoke(p, ilanlar);
                    }
                    catch (Exception exPage)
                    {
                        Log($"Sayfa {p} hata: {exPage.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"StartAsync genel hata: {ex.Message}");
            }
            finally
            {
                TryQuitDriver();
            }

            await Task.CompletedTask;
        }

        private int GetTotalPagesFallback()
        {
            try
            {
                try
                {
                    var last = _driver.FindElements(By.CssSelector("a[aria-label='Son Sayfa']")).FirstOrDefault();
                    if (last != null)
                    {
                        var href = last.GetAttribute("href");
                        var m = Regex.Match(href ?? "", @"[?&]pn=(\d+)");
                        if (m.Success) return int.Parse(m.Groups[1].Value);
                    }
                }
                catch { }

                try
                {
                    var lis = _driver.FindElements(By.CssSelector("ul.pagination li"))
                        .Where(e => int.TryParse(e.Text.Trim(), out _)).Select(e => int.Parse(e.Text.Trim())).ToList();
                    if (lis.Any()) return lis.Max();
                }
                catch { }

                try
                {
                    var text = _driver.FindElements(By.XPath("//span[contains(text(),'/')]")).Select(t => t.Text).FirstOrDefault();
                    if (!string.IsNullOrEmpty(text))
                    {
                        var m = Regex.Match(text, @"/\s*(\d+)");
                        if (m.Success) return int.Parse(m.Groups[1].Value);
                    }
                }
                catch { }

            }
            catch { }

            return 100;
        }

        private List<IlanBilgisi> ScrapeCurrentListingPage()
        {
            var ilanList = new List<IlanBilgisi>();

            try
            {
                var ilanKartiElementleri = _driver.FindElements(By.CssSelector("div._3A7p")).ToList();

                if (!ilanKartiElementleri.Any())
                {
                    Log("Sayfada ilan kartı bulunamadı. Alternatif yaklaşıma geçiliyor.");

                    var ilanLinkElems = _driver.FindElements(By.CssSelector("a[href*='/is-ilani/']")).ToList();
                    foreach (var linkElem in ilanLinkElems)
                    {
                        try
                        {
                            var text = linkElem.Text?.Trim();
                            var href = linkElem.GetAttribute("href");
                            if (string.IsNullOrWhiteSpace(text)) continue;
                            ilanList.Add(new IlanBilgisi
                            {
                                Baslik = text,
                                Firma = "",
                                Link = href
                            });
                        }
                        catch { }
                    }
                    return ilanList;
                }

                foreach (var ilanKarti in ilanKartiElementleri)
                {
                    string link = "";
                    string baslik = "";
                    string firma = "";

                    try
                    {
                        var anchor = ilanKarti.FindElement(By.XPath("./ancestor::a"));
                        link = anchor.GetAttribute("href");

                        baslik = ilanKarti.FindElement(By.CssSelector("h3._158X")).Text?.Trim() ?? "";

                        var firmaElem = ilanKarti.FindElements(By.CssSelector("p._1Z9_")).FirstOrDefault();
                        firma = firmaElem?.Text?.Trim() ?? "";

                        if (string.IsNullOrWhiteSpace(baslik) || string.IsNullOrWhiteSpace(link)) continue;

                        var detay = ScrapeIlanDetail(link);

                        ilanList.Add(new IlanBilgisi
                        {
                            Baslik = baslik,
                            Firma = CleanFirma(firma),
                            Link = link,
                            IsTanimi = detay.IsTanimi,
                            CalismaTuru = detay.CalismaTuru,
                            BasvuruSayisi = detay.BasvuruSayisi,
                            YanHaklar = detay.YanHaklar,
                            IsverenHakkinda = detay.IsverenHakkinda,
                            IlanDetayAciklama = detay.IlanDetayAciklama,
                            CalismaSaatleri = detay.CalismaSaatleri,
                            CalismaGunleri = detay.CalismaGunleri,
                            UyelikTarihi = detay.UyelikTarihi,
                            IlanSayisi = detay.IlanSayisi,
                            SonAktifTarih = detay.SonAktifTarih,
                            CompanyId = detay.CompanyId,
                            OtherJobs = detay.OtherJobs,
                        });
                    }
                    catch (NoSuchElementException)
                    {
                        Log($"Bir ilan kartında kritik element (Başlık/Link) bulunamadı. Kart atlandı.");
                    }
                    catch (Exception ex)
                    {
                        Log($"Liste içi tek bir ilan işlenirken hata: {ex.Message}. Link: {link}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"ScrapeCurrentListingPage genel hata: {ex.Message}");
            }

            return ilanList;
        }

        private IlanBilgisi ScrapeIlanDetail(string link)
        {
            var result = new IlanBilgisi();
            if (string.IsNullOrWhiteSpace(link)) return result;

            string originalWindow = null;
            try
            {
                originalWindow = _driver.CurrentWindowHandle;
                ((IJavaScriptExecutor)_driver).ExecuteScript("window.open();");
                var tabs = _driver.WindowHandles.ToList();
                var newTab = tabs.Last();
                _driver.SwitchTo().Window(newTab);
                _driver.Navigate().GoToUrl(link);
                Thread.Sleep(1200);

                try { result.IsTanimi = SafeGetText(By.CssSelector("div.Akbn")); } catch { }
                try { result.IsverenHakkinda = string.Join("\n", _driver.FindElements(By.CssSelector("p.Akbn")).Select(p => p.Text.Trim())); } catch { }
                try { result.IlanDetayAciklama = string.Join("\n", _driver.FindElements(By.CssSelector("div.Yg1d p")).Select(p => p.Text.Trim())); } catch { }

                try
                {
                    var sections = _driver.FindElements(By.CssSelector("div.col-md-6.mb-3"));
                    foreach (var sec in sections)
                    {
                        try
                        {
                            var baslik2 = sec.FindElement(By.CssSelector("h2._1vVn")).Text.Trim();
                            var deger = sec.FindElement(By.CssSelector("div._3s3m")).Text.Trim();

                            if (baslik2.Contains("Çalışma Türü")) result.CalismaTuru = deger;
                            else if (baslik2.Contains("Başvuru Sayısı")) result.BasvuruSayisi = deger;
                            else if (baslik2.Contains("Yan Haklar")) result.YanHaklar = deger;
                            else if (baslik2.Contains("Çalışma Saatleri")) result.CalismaSaatleri = deger;
                            else if (baslik2.Contains("Çalışma Günleri")) result.CalismaGunleri = deger;
                        }
                        catch { }
                    }
                }
                catch { }

                try
                {
                    var firmaLinkElem = _driver.FindElement(By.CssSelector("a._1v6a"));
                    var firmaLink = firmaLinkElem.GetAttribute("href");
                    ((IJavaScriptExecutor)_driver).ExecuteScript("window.open();");
                    var allTabs = _driver.WindowHandles.ToList();
                    var profTab = allTabs.Last();
                    _driver.SwitchTo().Window(profTab);
                    _driver.Navigate().GoToUrl(firmaLink);
                    Thread.Sleep(1200);

                    try
                    {
                        var profilSections = _driver.FindElements(By.CssSelector("div.col-md-6.mb-3"));
                        foreach (var sec in profilSections)
                        {
                            try
                            {
                                var baslik3 = sec.FindElement(By.CssSelector("div._2y9_")).Text.Trim();
                                var deger3 = sec.FindElement(By.CssSelector("div.text")).Text.Trim();

                                if (baslik3.Contains("Üyelik Tarihi")) result.UyelikTarihi = deger3;
                                else if (baslik3.Contains("Yayınladığı ilan")) result.IlanSayisi = deger3;
                                else if (baslik3.Contains("Son Aktif")) result.SonAktifTarih = deger3;
                            }
                            catch { }
                        }

                        var currentUrl = _driver.Url;
                        var cidMatch = Regex.Match(currentUrl, @"(c\d+)$");
                        if (cidMatch.Success)
                            result.CompanyId = cidMatch.Value;

                        var container = _driver.FindElement(By.CssSelector("div._3hT3"));
                        var otherJobs = container.FindElements(By.CssSelector("div.NPGR"));
                        var list = new List<string>();

                        foreach (var job in otherJobs)
                        {
                            try
                            {
                                var title = job.FindElement(By.CssSelector("h3")).Text.Trim();
                                var city = job.FindElement(By.CssSelector("div._35Fk em")).Text.Trim();
                                var time = job.FindElement(By.CssSelector("em._2z4S")).Text.Trim();

                                list.Add($"{title} - {city} - {time}");
                            }
                            catch { }
                        }

                        result.OtherJobs = string.Join("\n", list);

                    }
                    catch { }

                    _driver.Close();
                    _driver.SwitchTo().Window(newTab);
                }
                catch { }

                _driver.Close();
                _driver.SwitchTo().Window(originalWindow);
            }
            catch (Exception ex)
            {
                Log($"Ilan detay çekme hata: {ex.Message}");
                try
                {
                    var handles = _driver.WindowHandles.ToList();
                    if (handles.Count > 1)
                    {
                        _driver.SwitchTo().Window(handles.Last());
                        _driver.Close();
                        _driver.SwitchTo().Window(handles.First());
                    }
                }
                catch { }
            }

            return result;
        }

        private string SafeGetText(By by)
        {
            try { return _driver.FindElement(by).Text.Trim(); } catch { return ""; }
        }

        private string CleanFirma(string firma)
        {
            try
            {
                return Regex.Replace(firma ?? "", @"^\s*(\d+[.,]\d+|\b[1-5]\b)\s+", "");
            }
            catch { return firma; }
        }

        private void TryQuitDriver()
        {
            try
            {
                _driver?.Quit();
                _driver?.Dispose();
            }
            catch { }
            finally
            {
                _driver = null;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            TryQuitDriver();
            _disposed = true;
        }
    }
}