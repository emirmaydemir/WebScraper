using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using OfficeOpenXml;
using System.Text.RegularExpressions;

namespace IsinOlsunScraper
{
    public class IlanBilgisi
    {
        public string Baslik { get; set; }
        public string Firma { get; set; }
        public string IsTanimi { get; set; }
        public string CalismaTuru { get; set; }
        public string BasvuruSayisi { get; set; }
        public string YanHaklar { get; set; }
        public string IsverenHakkinda { get; set; }
        public string IlanDetayAciklama { get; set; }
        public string CalismaSaatleri { get; set; }
        public string CalismaGunleri { get; set; }
        public string UyelikTarihi { get; set; }
        public string IlanSayisi { get; set; }
        public string SonAktifTarih { get; set; }
        public string Link { get; set; }
    }

    public class Main
    {
        public void Bot()
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            var options = new ChromeOptions();
            options.AddArgument("--start-maximized");
            options.AddArgument("--headless");
            options.AddArgument("--disable-gpu");
            options.AddArgument("--no-sandbox");
            options.AddArgument("--disable-dev-shm-usage");
            options.AddArgument("--window-size=1920,1080");

            IWebDriver driver = new ChromeDriver(options);
            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(15));
            var rnd = new Random();

            string baseUrl = "https://isinolsun.com/is-ilanlari";

            string excelPath = Path.Combine(Directory.GetCurrentDirectory(), "ilanlar.xlsx");
            var fileInfo = new FileInfo(excelPath);

            if (!fileInfo.Exists)
            {
                using (var package = new ExcelPackage(fileInfo))
                {
                    var ws = package.Workbook.Worksheets.Add("İlanlar");
                    string[] headers =
                    {
                        "İş Başlığı","Firma Adı","İş Tanımı","Çalışma Türü","Başvuru Sayısı","Yan Haklar",
                        "İşveren Hakkında","İlan Detaylı Açıklama","Çalışma Saatleri","Çalışma Günleri",
                        "Üyelik Tarihi","Yayınladığı İlan Sayısı","Son Aktif Olduğu Tarih","Link"
                    };

                    for (int i = 0; i < headers.Length; i++)
                        ws.Cells[1, i + 1].Value = headers[i];

                    package.Save();
                }
            }

            driver.Navigate().GoToUrl(baseUrl);
            Thread.Sleep(1500);

            int sayfa = 1;
            while (sayfa <= 1847)
            {
                string pageUrl = sayfa == 1 ? baseUrl : $"{baseUrl}?pn={sayfa}";
                driver.Navigate().GoToUrl(pageUrl);
                Console.WriteLine($"\n--- {sayfa}. sayfa okunuyor: {pageUrl} ---");

                try
                {
                    wait.Until(d => d.FindElements(By.CssSelector("h3._158X")).Count > 0);
                }
                catch { }

                ((IJavaScriptExecutor)driver).ExecuteScript("window.scrollTo(0, document.body.scrollHeight);");
                Thread.Sleep(2000);

                var basliklar = driver.FindElements(By.CssSelector("h3._158X")).ToList();
                var firmalar = driver.FindElements(By.CssSelector("p._1Z9_")).ToList();

                List<IlanBilgisi> sayfaIlanlari = new List<IlanBilgisi>();

                for (int i = 0; i < basliklar.Count; i++)
                {
                    string baslik = basliklar[i].Text.Trim();
                    string firma = i < firmalar.Count ? firmalar[i].Text.Trim() : "";
                    firma = Regex.Replace(firma, @"^\s*(\d+[.,]\d+|\b[1-5]\b)\s+", "");

                    if (string.IsNullOrEmpty(baslik))
                        continue;

                    string link = "", isTanimi = "", calismaTuru = "", basvuruSayisi = "",
                           yanHaklar = "", isverenHakkinda = "", ilanDetayAciklama = "",
                           calismaSaatleri = "", calismaGunleri = "",
                           uyelikTarihi = "", ilanSayisi = "", sonAktifTarih = "";

                    try
                    {
                        var linkElem = basliklar[i].FindElement(By.XPath("./ancestor::a"));
                        link = linkElem.GetAttribute("href");

                        ((IJavaScriptExecutor)driver).ExecuteScript("window.open();");
                        var tabs = driver.WindowHandles;
                        driver.SwitchTo().Window(tabs[1]);
                        driver.Navigate().GoToUrl(link);
                        Thread.Sleep(2000);

                        try { isTanimi = driver.FindElement(By.CssSelector("div.Akbn")).Text.Trim(); } catch { }
                        try { isverenHakkinda = string.Join("\n", driver.FindElements(By.CssSelector("p.Akbn")).Select(p => p.Text.Trim())); } catch { }
                        try { ilanDetayAciklama = string.Join("\n", driver.FindElements(By.CssSelector("div.Yg1d p")).Select(p => p.Text.Trim())); } catch { }

                        try
                        {
                            var sections = driver.FindElements(By.CssSelector("div.col-md-6.mb-3"));
                            foreach (var sec in sections)
                            {
                                try
                                {
                                    var baslik2 = sec.FindElement(By.CssSelector("h2._1vVn")).Text.Trim();
                                    var deger = sec.FindElement(By.CssSelector("div._3s3m")).Text.Trim();

                                    if (baslik2.Contains("Çalışma Türü"))
                                        calismaTuru = deger;
                                    else if (baslik2.Contains("Başvuru Sayısı"))
                                        basvuruSayisi = deger;
                                    else if (baslik2.Contains("Yan Haklar"))
                                        yanHaklar = deger;
                                    else if (baslik2.Contains("Çalışma Saatleri"))
                                        calismaSaatleri = deger;
                                    else if (baslik2.Contains("Çalışma Günleri"))
                                        calismaGunleri = deger;
                                }
                                catch { }
                            }
                        }
                        catch { }

                        try
                        {
                            var firmaLinkElem = driver.FindElement(By.CssSelector("a._1v6a"));
                            var firmaLink = firmaLinkElem.GetAttribute("href");

                            ((IJavaScriptExecutor)driver).ExecuteScript("window.open();");
                            var allTabs = driver.WindowHandles;
                            driver.SwitchTo().Window(allTabs[2]);
                            driver.Navigate().GoToUrl(firmaLink);
                            Thread.Sleep(2000);

                            try
                            {
                                var profilSections = driver.FindElements(By.CssSelector("div.col-md-6.mb-3"));
                                foreach (var sec in profilSections)
                                {
                                    try
                                    {
                                        var baslik3 = sec.FindElement(By.CssSelector("div._2y9_")).Text.Trim();
                                        var deger3 = sec.FindElement(By.CssSelector("div.text")).Text.Trim();

                                        if (baslik3.Contains("Üyelik Tarihi"))
                                            uyelikTarihi = deger3;
                                        else if (baslik3.Contains("Yayınladığı ilan"))
                                            ilanSayisi = deger3;
                                        else if (baslik3.Contains("Son Aktif"))
                                            sonAktifTarih = deger3;
                                    }
                                    catch { }
                                }
                            }
                            catch { }

                            driver.Close();
                            driver.SwitchTo().Window(allTabs[1]);
                        }
                        catch { }

                        driver.Close();
                        driver.SwitchTo().Window(tabs[0]);
                    }
                    catch { }

                    sayfaIlanlari.Add(new IlanBilgisi
                    {
                        Baslik = baslik,
                        Firma = firma,
                        Link = link,
                        IsTanimi = isTanimi,
                        CalismaTuru = calismaTuru,
                        BasvuruSayisi = basvuruSayisi,
                        YanHaklar = yanHaklar,
                        IsverenHakkinda = isverenHakkinda,
                        IlanDetayAciklama = ilanDetayAciklama,
                        CalismaSaatleri = calismaSaatleri,
                        CalismaGunleri = calismaGunleri,
                        UyelikTarihi = uyelikTarihi,
                        IlanSayisi = ilanSayisi,
                        SonAktifTarih = sonAktifTarih
                    });

                    Console.WriteLine($" + {baslik} | {firma}");
                }

                AppendToExcel(fileInfo, sayfaIlanlari);
                Console.WriteLine($"{sayfa}. sayfa Excel'e kaydedildi ({sayfaIlanlari.Count} ilan).");

                sayfa++;

                GC.Collect();
                Thread.Sleep(1000);
            }

            driver.Quit();
            Console.WriteLine("\n Tüm sayfalar tamamlandı ve Excel'e kaydedildi!");
        }

        private void AppendToExcel(FileInfo fileInfo, List<IlanBilgisi> ilanlar)
        {
            using (var package = new ExcelPackage(fileInfo))
            {
                var ws = package.Workbook.Worksheets.First();
                int row = ws.Dimension?.End.Row + 1 ?? 2;

                foreach (var ilan in ilanlar)
                {
                    ws.Cells[row, 1].Value = ilan.Baslik;
                    ws.Cells[row, 2].Value = ilan.Firma;
                    ws.Cells[row, 3].Value = ilan.IsTanimi;
                    ws.Cells[row, 4].Value = ilan.CalismaTuru;
                    ws.Cells[row, 5].Value = ilan.BasvuruSayisi;
                    ws.Cells[row, 6].Value = ilan.YanHaklar;
                    ws.Cells[row, 7].Value = ilan.IsverenHakkinda;
                    ws.Cells[row, 8].Value = ilan.IlanDetayAciklama;
                    ws.Cells[row, 9].Value = ilan.CalismaSaatleri;
                    ws.Cells[row, 10].Value = ilan.CalismaGunleri;
                    ws.Cells[row, 11].Value = ilan.UyelikTarihi;
                    ws.Cells[row, 12].Value = ilan.IlanSayisi;
                    ws.Cells[row, 13].Value = ilan.SonAktifTarih;
                    ws.Cells[row, 14].Value = ilan.Link;
                    row++;
                }

                ws.Cells[ws.Dimension.Address].AutoFitColumns();
                package.Save();
            }
        }
    }
}
